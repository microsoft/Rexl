// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

using Microsoft.Rexl;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Flow;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;

namespace DocBench;

using CodeGenerator = CachingEnumerableCodeGenerator;
using FmaTuple = Immutable.Array<(RexlFormula fma, int grp)>;
using GlobalFunc = Func<object[], object>;
using GlobalGuidTuple = Immutable.Array<(GlobalInfo glob, Guid guid)>;
using GlobalTuple = Immutable.Array<GlobalInfo>;
using GuidSet = Immutable.Set<Guid>;

/// <summary>
/// The document node class.
/// </summary>
sealed class FlowNode : DocumentBase.DocExtNode
{
    public FlowNode(Guid guid, NPath name, RexlFormula fma, DocumentBase.NodeConfig config, FmaTuple extra)
        : base(guid, name, fma, config, extra)
    {
    }
}

/// <summary>
/// The document class.
/// </summary>
sealed class Document : DocumentExt<Document, FlowNode>
{
    public Document()
        : base()
    {
    }

    private Document(in Fields flds)
        : base(in flds)
    {
    }

    protected override Document Wrap(in Fields flds)
    {
        AssertValid(in flds);
        if (Match(in flds))
            return this;
        return new Document(in flds);
    }

    protected override FlowNode SetConfigCore(FlowNode node, NodeConfig config)
    {
        return new FlowNode(node.Guid, node.Name, node.Formula, config, node.ExtraFormulas);
    }

    protected override FlowNode SetExtraCore(FlowNode node, Immutable.Array<(RexlFormula fma, int grp)> extra)
    {
        return new FlowNode(node.Guid, node.Name, node.Formula, node.Config, extra);
    }

    protected override FlowNode SetFormulaCore(FlowNode node, RexlFormula fma)
    {
        return new FlowNode(node.Guid, node.Name, fma, node.Config, node.ExtraFormulas);
    }

    protected override FlowNode SetNameCore(FlowNode node, NPath name)
    {
        return new FlowNode(node.Guid, name, node.Formula, node.Config, node.ExtraFormulas);
    }

    public Document CreateFlowNode(NPath name, RexlFormula fma, NodeConfig config, Guid guid)
    {
        return AddNode(new FlowNode(guid, name, fma, config, default));
    }
}

sealed class Executor : SbSysTypeSink
{
    private const int _icolGuid = 0;
    private const int _icolStatus = 1;
    private const int _icolName = 2;
    private const int _icolType = 3;
    private const int _icolExpr = 4;

    private Document _doc;
    private readonly Stack<Document> _undo;
    private readonly Stack<Document> _redo;

    public Document Doc => _doc;
    public int UndoCount => _undo.Count;
    public int RedoCount => _redo.Count;

    private readonly Document.GraphHost _host;
    private Document.Graph _graph;
    private Document.Graph _graphPrev;

    public event Action OnDocChanged;

    public Document.Graph Graph => _graph;

    private readonly OperationRegistry _funcs;
    private readonly GeneratorRegistry _operGens;
    private readonly CodeGenerator _codeGen;

    private sealed class NodeState
    {
        public Guid Guid { get; }
        public bool HasError { get; private set; }
        public bool HasValue { get; private set; }
        public object Value { get; private set; }

        public bool Recalcing;
        public GlobalFunc Func;
        public GlobalGuidTuple GlobalMap;
        public GlobalFunc FuncExtra;
        public GlobalGuidTuple GlobalMapExtra;

        public NodeState(Guid guid)
        {
            Guid = guid;
        }

        public NodeState ClearValue(bool hasError = false)
        {
            // REVIEW: Perhaps keep both base and full value?
            HasValue = false;
            Value = null;
            HasError = hasError;
            return this;
        }

        public NodeState SetValue(object value)
        {
            Validation.AssertValueOrNull(value);
            HasValue = true;
            Value = value;
            HasError = false;
            return this;
        }

        public void ClearBaseCode()
        {
            Func = null;
            GlobalMap = default;
        }

        public void ClearMoreCode()
        {
            FuncExtra = null;
            GlobalMapExtra = default;
        }
    }

    // For assigning guids to nodes.
    private int _iidPrev;
    private readonly byte[] _guidBytes;

    // Mapping from node id to the value.
    private readonly Dictionary<Guid, NodeState> _nidToState;

    private readonly TextBox _txtRes;
    private readonly ListView _listNodes;
    private readonly Func<bool> _getShowIL;

    private int _maxItems = 5;

    /// <summary>
    /// These are the globals available while loading a script. The assumption is
    /// a script being loaded should NOT see nodes in the document, but only globals
    /// that the script has created. Once the script is done being loaded, we transfer
    /// these to the document as static data flow nodes.
    /// </summary>
    private Dictionary<NPath, (BoundNode bnd, object res)> _globalsScript;

    /// <summary>
    /// The bind host for loading a script.
    /// </summary>
    private ScriptBindHost _hostScript;

    public Executor(TextBox txtRes, ListView listNodes, Func<bool> getShowIL)
    {
        Validation.AssertValue(txtRes);
        Validation.AssertValue(listNodes);
        Validation.AssertValue(getShowIL);

        _getShowIL = getShowIL;

        _funcs = new Functions();
        _operGens = BuiltinGenerators.Instance;
        _hostScript = new ScriptBindHost(this);

        _doc = new Document();
        _undo = new Stack<Document>();
        _redo = new Stack<Document>();

        _host = new GraphHostImpl(this);
        _graph = Doc.GetGraph(_host, null);

        _guidBytes = new byte[8];

        _codeGen = new CodeGenerator(new StdEnumerableTypeManager(), _operGens);
        _nidToState = new Dictionary<Guid, NodeState>();

        _txtRes = txtRes;
        _listNodes = listNodes;
    }

    protected override void FlushCore()
    {
        if (_sbOut.Length > 0)
        {
            _txtRes.AppendText(_sbOut.ToString());
            _sbOut.Clear();
        }
    }

    protected override void PostWrite()
    {
        if (_sbOut.Length > 1000)
            Flush();
    }

    public void Set(Document doc)
    {
        Validation.BugCheckValue(doc, nameof(doc));
        if (_doc == doc)
            return;

        _undo.Push(_doc);
        _redo.Clear();
        _doc = doc;
        HandleDocChanged();
        OnDocChanged?.Invoke();
    }

    public void Undo()
    {
        Validation.BugCheck(_undo.Count > 0);
        _redo.Push(_doc);
        _doc = _undo.Pop();
        HandleDocChanged();
        OnDocChanged?.Invoke();
    }

    public void Redo()
    {
        Validation.BugCheck(_redo.Count > 0);
        _undo.Push(_doc);
        _doc = _redo.Pop();
        HandleDocChanged();
        OnDocChanged?.Invoke();
    }

    public Guid GuidNext()
    {
        return new Guid(++_iidPrev, 0, 0, _guidBytes);
    }

    public TypeManager TypeManager { get { return _codeGen.TypeManager; } }

    private bool TryFindItem(Guid guid, out int index, out ListViewItem item)
    {
        Guid guidCur;
        var items = _listNodes.Items;
        int len = items.Count;
        int min = 0;
        int lim = len;
        while (min < lim)
        {
            int mid = (int)((uint)(min + lim) / 2);
            Validation.Assert(min <= mid && mid < lim);
            guidCur = (Guid)items[mid].Tag;
            Validation.Assert(guidCur != default);
            if (guid.CompareTo(guidCur) <= 0)
                lim = mid;
            else
                min = mid + 1;
        }
        Validation.Assert(min == lim);
        Validation.Assert(0 <= min && min <= len);
        Validation.Assert(min == 0 || guid.CompareTo((Guid)items[min - 1].Tag) > 0);
        index = min;
        if (min >= len)
        {
            item = null;
            return false;
        }
        item = items[min];
        guidCur = (Guid)item.Tag;
        Validation.Assert(guidCur != default);
        Validation.Assert(guid.CompareTo(guidCur) <= 0);
        if (guid != guidCur)
        {
            item = null;
            return false;
        }
        return true;
    }

    public void ShowNamespaces()
    {
        var nss = Doc.GetNamespaces();
        nss.Sort((x, y) => string.CompareOrdinal(x.name.ToDottedSyntax(), y.name.ToDottedSyntax()));

        WriteLine("Namespaces:");
        foreach (var (name, ns) in nss)
        {
            if (ns == null)
                WriteLine("  {0}: <auto>", name);
            else
                WriteLine("  {0}: {1}", name, ns.Guid);
        }
        Flush();
    }

    private void HandleGlobalAdded(Document.GraphNode gn)
    {
        Validation.AssertValue(gn);
        var guid = gn.Guid;

        WriteLine("*** Global added: {0}, {1}", guid, gn.Name);
        Flush();

        Validation.Assert(!_nidToState.ContainsKey(guid));
        _nidToState.Add(guid, new NodeState(guid));

        UpdateRow(gn);
    }

    private void HandleGlobalDeleted(FlowNode node)
    {
        Validation.AssertValue(node);
        var guid = node.Guid;

        WriteLine("*** Global deleted: {0}, {1}", guid, node.Name);
        Flush();

        Validation.Assert(_nidToState.ContainsKey(guid));
        _nidToState.Remove(guid);

        TryFindItem(guid, out int index, out var item).Verify();
        Validation.Assert((Guid)item.Tag == guid);
        _listNodes.Items.RemoveAt(index);
    }

    private void HandleGlobalRenamed(Document.GraphNode gn, NPath namePrev)
    {
        Validation.AssertValue(gn);

        WriteLine("*** Global renamed: {0}, {1} => {2}", gn.Guid, namePrev, gn.Name);
        Flush();

        Validation.Assert(_nidToState.ContainsKey(gn.Guid));

        UpdateRow(gn);
    }

    private void HandleFormulaChanged(Document.GraphNode gn, RexlFormula fmaPrev, FmaTuple extraPrev)
    {
        Validation.AssertValue(gn);
        if (gn.Formula != fmaPrev)
            WriteLine("*** Main formula changed: {0}, '{1}' => '{2}'", gn.Guid, fmaPrev?.Text, gn.Formula?.Text);
        var old = extraPrev;
        var cur = gn.ExtraFormulas;
        if (old.Length != cur.Length || old.Zip(cur, (a, b) => a != b).Any(x => x))
            WriteLine("*** Extra formulas changed: {0}", gn.Guid);
        Flush();

        Validation.Assert(_nidToState.ContainsKey(gn.Guid));

        UpdateRow(gn);
    }

    private void HandleConfigChanged(FlowNode node)
    {
        Validation.AssertValue(node);
        WriteLine("*** Data changed: {0}", node.Guid);
        Flush();

        Validation.Assert(_nidToState.ContainsKey(node.Guid));
    }

    private void HandleDocChanged()
    {
        _graphPrev = _graph;
        _graph = Doc.GetGraph(_host, _graphPrev);

        // Handle deletions.
        foreach (var gnOld in _graphPrev.GetNodesByGuid())
        {
            if (!_graph.TryGetNode(gnOld.Guid, out _))
                HandleGlobalDeleted(gnOld.FlowNode);
        }

        // Handle additions and modifications.
        Dictionary<NPath, NPath> nameMap = null;
        foreach (var gnNew in _graph.GetNodesByGuid())
        {
            var guid = gnNew.Guid;
            if (!_graphPrev.TryGetNode(guid, out var gnOld))
            {
                HandleGlobalAdded(gnNew);
                continue;
            }

            if (gnOld == gnNew)
                continue;

            if (gnOld.Config != gnNew.Config)
                HandleConfigChanged(gnNew.FlowNode);
            if (gnOld.Name != gnNew.Name)
            {
                Util.Add(ref nameMap, gnOld.Name, gnNew.Name);
                HandleGlobalRenamed(gnNew, gnOld.Name);
            }
            if (gnOld.Formula != gnNew.Formula || !gnOld.ExtraFormulas.AreSame(gnNew.ExtraFormulas))
                HandleFormulaChanged(gnNew, gnOld.Formula, gnOld.ExtraFormulas);
        }

        // Get info for each node. Need to use topo order for this, but we want to drive display
        // updates from the _listNodes, so need to build up display info in a collection.
        GuidSet dataChanged = default;
        GuidSet badData = default;
        GuidSet seen = default;
        var sb = new StringBuilder("               ");
        var infos = new Dictionary<Guid, string>();
        foreach (var gnNew in _graph.GetNodes())
        {
            var guid = gnNew.Guid;

            bool added = false;
            bool nameChanged = false;
            bool dataBaseChanged = false;
            bool dataFullChanged = false;
            bool typeBaseChanged = false;
            bool typeFullChanged = false;
            bool usesBaseChanged = false;
            bool usesFullChanged = false;
            bool bindMainChanged = false;
            bool bindMoreChanged = false;
            bool codeMainChanged = false;
            bool codeMoreChanged = false;
            bool numFmasChanged = false;
            bool hasError = false;

            if (gnNew.Formula != null && !gnNew.MainBoundFormula.IsGood)
                hasError = true;
            if (gnNew.ExtraFormulas.Length > 0 && gnNew.ExtraBoundFormulas.Any(item => !item.bfma.IsGood))
                hasError = true;

            if (!_graphPrev.TryGetNode(guid, out var gnOld))
            {
                added = true;
                typeBaseChanged = true;
                typeFullChanged = true;
                usesBaseChanged = !gnNew.BaseUses.IsEmpty;
                usesFullChanged = !gnNew.Uses.IsEmpty;
                dataBaseChanged = true;
                dataFullChanged = true;
                if (gnNew.Formula != null)
                {
                    bindMainChanged = true;
                    codeMainChanged = true;
                    if (!gnNew.MainBoundFormula.IsGood)
                        hasError = true;
                }
                if (gnNew.ExtraFormulas.Length > 0)
                {
                    numFmasChanged = true;
                    bindMoreChanged = true;
                    codeMoreChanged = true;
                    if (gnNew.ExtraBoundFormulas.Any(item => !item.bfma.IsGood))
                        hasError = true;
                }
            }
            else if (gnOld != gnNew)
            {
                if (gnOld.Config != gnNew.Config)
                {
                    dataBaseChanged = true;
                    dataFullChanged = true;
                }
                if (gnOld.Name != gnNew.Name)
                    nameChanged = true;
                if (gnOld.ExtraFormulas.Length != gnNew.ExtraFormulas.Length)
                    numFmasChanged = true;

                if (gnOld.BaseType != gnNew.BaseType)
                    typeBaseChanged = true;
                if (gnOld.Type != gnNew.Type)
                    typeFullChanged = true;
                if (gnOld.BaseUses != gnNew.BaseUses)
                    usesBaseChanged = true;
                if (gnOld.Uses != gnNew.Uses)
                    usesFullChanged = true;
                if (gnOld.MainBoundFormula != gnNew.MainBoundFormula)
                {
                    bindMainChanged = true;
                    var bfma0 = gnOld.MainBoundFormula;
                    var bfma1 = gnNew.MainBoundFormula;
                    if (!bfma0.IsGood || !bfma1.IsGood || !bfma0.BoundTree.Equivalent(bfma1.BoundTree, nameMap))
                        codeMainChanged = true;
                }
                if (gnNew.ExtraBoundFormulas.Length > 0 && !gnOld.ExtraBoundFormulas.AreSame(gnNew.ExtraBoundFormulas))
                {
                    bindMoreChanged = true;
                    if (gnOld.ExtraBoundFormulas.Length != gnNew.ExtraBoundFormulas.Length)
                        codeMoreChanged = true;
                    else
                    {
                        bool good = true;
                        for (int i = 0; i < gnOld.ExtraBoundFormulas.Length && good; i++)
                        {
                            var (bfma0, grp0) = gnOld.ExtraBoundFormulas[i];
                            var (bfma1, grp1) = gnNew.ExtraBoundFormulas[i];
                            // REVIEW: Should the grp affect this at all? Probably doesn't need to.
                            if (grp0 != grp1 || !bfma0.IsGood || !bfma0.IsGood || !bfma0.BoundTree.Equivalent(bfma1.BoundTree, nameMap))
                                good = false;
                        }
                        if (!good)
                            codeMoreChanged = true;
                    }
                }
            }

            if (codeMainChanged)
                dataBaseChanged = true;
            else if (dataChanged.Intersects(gnNew.BaseUses) || !gnNew.BaseUses.IsSubset(seen))
                dataBaseChanged = true;
            else if (numFmasChanged)
                dataFullChanged = true;
            else if (codeMoreChanged)
                dataFullChanged = true;
            else if (dataChanged.Intersects(gnNew.Uses) || !gnNew.Uses.IsSubset(seen))
                dataFullChanged = true;

            dataFullChanged |= dataBaseChanged;

            bool bad = hasError || !gnNew.Uses.IsSubset(seen) || badData.Intersects(gnNew.Uses);

            if (added | nameChanged |
                dataBaseChanged | dataFullChanged | typeBaseChanged | typeFullChanged |
                bindMainChanged | bindMoreChanged | codeMainChanged | codeMoreChanged |
                numFmasChanged | bad)
            {
                int ich = 0;
                sb[ich++] = added ? 'A' : ' ';
                sb[ich++] = nameChanged ? 'N' : ' ';
                sb[ich++] = numFmasChanged ? 'F' : ' ';
                sb[ich++] = typeBaseChanged ? 't' : ' ';
                sb[ich++] = typeFullChanged ? 'T' : ' ';
                sb[ich++] = usesBaseChanged ? 'u' : ' ';
                sb[ich++] = usesFullChanged ? 'U' : ' ';
                sb[ich++] = bindMainChanged ? 'B' : ' ';
                sb[ich++] = bindMoreChanged ? 'b' : ' ';
                sb[ich++] = codeMainChanged ? 'C' : ' ';
                sb[ich++] = codeMoreChanged ? 'c' : ' ';
                sb[ich++] = dataBaseChanged ? 'd' : ' ';
                sb[ich++] = dataFullChanged ? 'D' : ' ';
                sb[ich++] = hasError ? 'E' : ' ';
                sb[ich++] = bad ? 'X' : ' ';
                Validation.Assert(ich == sb.Length);

                var type = typeFullChanged ? gnNew.Type : default;

                infos.Add(guid, sb.ToString());
            }

            seen = seen.Add(guid);
            if (bad)
                badData = badData.Add(guid);
            if (dataFullChanged)
                dataChanged = dataChanged.Add(guid);

            Validation.Assert(_nidToState.ContainsKey(guid));

            if (!dataFullChanged)
            {
                // Binding may have changed (due to rename), but type and code should not have changed.
                Validation.Assert(!typeFullChanged);
                Validation.Assert(!codeMainChanged);
                Validation.Assert(!codeMoreChanged);
                continue;
            }

            var state = _nidToState[guid];
            Validation.AssertValue(state);
            Validation.Assert(state.Guid == guid);

            if (gnNew.ExtraFormulas.IsDefaultOrEmpty)
                state.ClearMoreCode();

            if (gnNew.HasFormulas)
            {
                state.ClearValue(hasError: bad);

                // When uses changes, the GlobalMap changes, so treat it the same as a code change.
                if (codeMainChanged | usesBaseChanged)
                {
                    Validation.Assert(gnNew.Formula != null);
                    Validation.Assert(dataBaseChanged);
                    Validation.Assert(dataFullChanged);
                    state.ClearBaseCode();
                }

                // When uses changes, the GlobalMap changes, so treat it the same as a code change.
                // REVIEW: Would be slightly better if we tracked "usesMainChanged" and "usesMoreChanged".
                if (codeMoreChanged | usesFullChanged)
                {
                    Validation.Assert(!gnNew.ExtraFormulas.IsDefaultOrEmpty | !codeMoreChanged);
                    Validation.Assert(dataFullChanged);
                    state.ClearMoreCode();
                }

                if (!bad)
                    Recalc(gnNew);
            }
            else
            {
                Validation.Assert(gnNew.Config is DataConfig);
                var config = (DataConfig)gnNew.Config;
                state.SetValue(config.Value);
            }
        }

        foreach (ListViewItem item in _listNodes.Items)
        {
            var guid = (Guid)item.Tag;

            if (!infos.TryGetValue(guid, out var status))
                status = "             ";

            var gn = _graph.GetNode(guid);
            _graphPrev.TryGetNode(guid, out var gnOld);

            item.SubItems[_icolStatus].Text = status;
            if (gnOld == null || gn.Type != gnOld.Type)
                item.SubItems[_icolType].Text = gn.Type.Serialize();
        }

        Flush();
    }

    public BoundFormula BindTrial(RexlFormula fma, NPath nameCloak, NPath nameCur)
    {
        return BoundFormula.Create(fma, new TrialBindHost(this, nameCur.Parent, nameCloak),
            BindOptions.DontReduce);
    }

    private sealed class TrialBindHost : ReferenceBindHost
    {
        private readonly Executor _parent;
        private readonly NPath _nameCloak;

        public TrialBindHost(Executor parent, NPath ns, NPath nameCloak)
            : base(ns, NPath.Root)
        {
            _parent = parent;
            _nameCloak = nameCloak;
        }

        public override bool IsFuzzyMatch(string a, string b)
        {
            return _parent._host.IsFuzzyMatch(a, b);
        }

        protected override bool TryFindNamespaceItemCore(NPath ns, DName name, out NPath path, out DType type, out bool isStream)
        {
            Validation.Assert(name.IsValid);

            isStream = false;
            path = ns.Append(name);

            // REVIEW: Just cloaking the name isn't sufficient to find all
            // errors, because other globals could depend on the cloaked global.
            // If this references them, then we'll end up with a circularity.
            // Perhaps we should also pass a cloaked nid, and "block" any globals
            // that depend on the nid?
            if (path != _nameCloak)
            {
                if (_parent.Doc.TryGetNode(path, out var node))
                {
                    type = _parent._graph.GetNode(node.Guid).Type;
                    Validation.Assert(type.IsValid);
                    return true;
                }

                if (_parent.Doc.ContainsNamespace(path))
                {
                    type = default;
                    return true;
                }
            }

            path = default;
            type = default;
            return false;
        }

        public override bool TryFindNamespaceItemFuzzy(ExprNode ctx, NPath ns, DName name, out DName nameGuess, out DType type, out bool isStream)
        {
            Validation.Assert(name.IsValid);

            isStream = false;

            if (_parent._host.TryGetGlobalFuzzyMatch(ns, name, out nameGuess) && _parent.Doc.TryGetNode(ns.Append(nameGuess), out var node))
            {
                type = _parent._graph.GetNode(node.Guid).Type;
                Validation.Assert(type.IsValid);
                return true;
            }

            type = default;
            if (_parent._host.TryGetNamespaceFuzzyMatch(ns, name, out nameGuess) && IsNamespace(ns.Append(nameGuess)))
                return true;

            nameGuess = default;
            return false;
        }

        public override bool IsNamespace(NPath name)
        {
            return _parent.Doc.ContainsNamespace(name);
        }

        protected override bool TryGetOperInfoCore(NPath name, bool user, bool fuzzy, int arity, out OperInfo info)
        {
            return _parent._host.TryGetOperInfo(name, user, fuzzy, arity, out info);
        }
    }

    public IEnumerable<NPath> GetGlobalNames()
    {
        foreach (var node in Doc.GetFlowNodes())
            yield return node.Name;
    }

    /// <summary>
    /// Translate a name based global map to a guid based global map. This keep the result valid
    /// across renames.
    /// </summary>
    private GlobalGuidTuple TranslateGlobalMap(GlobalTuple gm)
    {
        if (gm.IsDefaultOrEmpty)
            return GlobalGuidTuple.Empty;

        var bldr = GlobalGuidTuple.CreateBuilder(gm.Length, init: true);
        foreach (var glob in gm)
        {
            Validation.Assert(bldr[glob.Slot].glob == null);
            if (glob.Name.IsRoot)
                bldr[glob.Slot] = (glob, default);
            else
            {
                var node = Doc.GetNode(glob.Name);
                bldr[glob.Slot] = (glob, node.Guid);
            }
        }
        return bldr.ToImmutable();
    }

    private void Recalc(Document.GraphNode gn)
    {
        Validation.AssertValue(gn);
        Validation.Assert(gn.HasFormulas);
        Validation.Assert(_nidToState.ContainsKey(gn.Guid));
        var state = _nidToState[gn.Guid];
        Validation.BugCheck(!state.Recalcing);
        Validation.Assert(!state.HasValue);
        Validation.Assert(!state.HasError);

        object[] argsMain;
        object[] argsExtra;
        int slotThisExtra;
        state.Recalcing = true;
        try
        {
            bool needMain = state.Func == null && gn.Formula != null;
            bool needMore = state.FuncExtra == null && !gn.ExtraFormulas.IsDefaultOrEmpty;
            if (needMain || needMore)
            {
                // Need to generate code.
                try
                {
                    if (needMain)
                    {
                        WriteLine("*** Generating main code for {0} ({1})", gn.Guid, gn.Name);
                        var bfma = gn.MainBoundFormula;
                        Validation.Assert(bfma != null);
                        Validation.Assert(bfma.IsGood);
                        var resCodeGen = _codeGen.Run(bfma.BoundTree, host: null, _getShowIL() ? new Action<string>(WriteLine) : null);
                        state.Func = resCodeGen.Func;
                        state.GlobalMap = TranslateGlobalMap(resCodeGen.Globals);
                    }

                    if (needMore)
                    {
                        WriteLine("*** Generating extra code for {0} ({1})", gn.Guid, gn.Name);
                        var bfma = gn.GetExtraBoundFormula(0);
                        Validation.Assert(bfma != null);
                        Validation.Assert(bfma.IsGood);
                        var resCodeGen = _codeGen.Run(bfma.BoundTree, host: null, _getShowIL() ? new Action<string>(WriteLine) : null);
                        state.FuncExtra = resCodeGen.Func;
                        state.GlobalMapExtra = TranslateGlobalMap(resCodeGen.Globals);
                    }
                }
                catch (Exception ex)
                {
                    // REVIEW: Record the exception.
                    WriteLine("*** Generating code for {0} ({1}) threw: {2}", gn.Guid, gn.Name, ex);
                    state.ClearValue(hasError: true);
                    return;
                }
            }

            object[] BuildArgs(GlobalFunc func, GlobalGuidTuple globals, out int slotThis)
            {
                slotThis = -1;

                // Build the args.
                var args = new object[globals.Length];
                int carg = args.Length;
                if (carg > 0)
                {
                    ExecCtx ctx = null;
                    foreach ((GlobalInfo glob, Guid guid) in globals)
                    {
                        int slot = glob.Slot;
                        Validation.AssertIndex(slot, carg);
                        Validation.Assert(args[slot] == null);

                        if (glob.IsCtx)
                        {
                            // Execution context.
                            // REVIEW: Implement logging?
                            Validation.Assert(ctx == null);
                            ctx = ExecCtx.CreateBare();
                            args[slot] = ctx;
                        }
                        else if (glob.IsThis)
                        {
                            Validation.Assert(slotThis < 0);
                            slotThis = slot;
                        }
                        else
                        {
                            Validation.Assert(glob.Type == _graph.GetNode(guid).Type);
                            Validation.Assert(_nidToState.ContainsKey(guid));
                            var stateArg = _nidToState[guid];
                            if (stateArg.HasValue)
                            {
                                object value = stateArg.Value;
#if DEBUG
                                Type st = _codeGen.TypeManager.GetSysTypeOrNull(glob.Type).VerifyValue();
                                if (value != null)
                                    Validation.Assert(st.IsAssignableFrom(value.GetType()));
                                else
                                    Validation.Assert(st.IsClass || st.IsInterface || st.IsGenericType && st.GetGenericTypeDefinition() == typeof(Nullable<>));
#endif
                                args[slot] = value;
                            }
                            else
                            {
                                Validation.Assert(stateArg.HasError);
                                return null;
                            }
                        }
                    }
                }

                return args;
            }

            if (state.Func == null)
                argsMain = null;
            else if ((argsMain = BuildArgs(state.Func, state.GlobalMap, out slotThisExtra)) == null)
            {
                // "this" is illegal in the main formula, so any use of it should have produced an error.
                Validation.Assert(slotThisExtra < 0);
                state.ClearValue(hasError: true);
                return;
            }

            if (state.FuncExtra == null)
            {
                argsExtra = null;
                slotThisExtra = -1;
            }
            else if ((argsExtra = BuildArgs(state.FuncExtra, state.GlobalMapExtra, out slotThisExtra)) == null)
            {
                state.ClearValue(hasError: true);
                return;
            }
        }
        finally
        {
            Validation.Assert(state.Recalcing = true);
            state.Recalcing = false;
        }

        try
        {
            WriteLine("*** Re-evaluating {0} ({1}):", gn.Guid, gn.Name);
            Flush();

            object value;

            if (state.Func != null)
                value = state.Func(argsMain);
            else
            {
                Validation.Assert(gn.Config is DataConfig);
                var config = (DataConfig)gn.Config;
                value = config.Value;
            }

            if (state.FuncExtra != null)
            {
                if (slotThisExtra >= 0)
                {
                    Validation.Assert(argsExtra[slotThisExtra] == null);
                    argsExtra[slotThisExtra] = value;
                }
                value = state.FuncExtra(argsExtra);
            }

            state.SetValue(value);
        }
        catch (Exception ex)
        {
            // REVIEW: Record the exception.
            WriteLine("*** Evaluation of {0} ({1}) threw: {2}", gn.Guid, gn.Name, ex);
            state.ClearValue(hasError: true);
            return;
        }

        WriteLine("*** Evaluation of {0} ({1}):", gn.Guid, gn.Name);
        bool line = WriteValue(state.Value, max: _maxItems, startOfLine: true);
        if (!line)
            WriteLine();
    }

    private void UpdateRow(Document.GraphNode gn)
    {
        Validation.AssertValue(gn);

        if (!TryFindItem(gn.Guid, out int index, out var item))
        {
            Validation.Assert(item == null);
            _listNodes.Items.Insert(index, item = new ListViewItem());
        }

        item.Tag = gn.Guid;
        item.SubItems.Clear();
        item.SubItems[_icolGuid].Text = gn.Guid.ToString();
        item.SubItems.Add("");
        item.SubItems.Add(gn.Name.ToDottedSyntax());
        item.SubItems.Add(gn.Type.Serialize());

        if (gn.Formula != null)
            item.SubItems.Add(BeautifyScript(gn.Formula.Text));
        else
            item.SubItems.Add("");
        if (gn.ExtraFormulas.Length > 0)
            item.SubItems.Add(BeautifyScript(gn.ExtraFormulas[0].fma.Text));
        else
            item.SubItems.Add("");

        item.Selected = true;
        item.Focused = true;
    }

    private string BeautifyScript(string script)
    {
        var lines = script.Split('\n').Select(x => x.Trim()).Where(x => !string.IsNullOrEmpty(x));
        return string.Join(" ## ", lines);
    }

    public void RenderNodeValue(FlowNode node)
    {
        Validation.AssertValue(node);
        Validation.Assert(Doc.IsActive(node));
        Validation.Assert(_nidToState.ContainsKey(node.Guid));

        var state = _nidToState[node.Guid];
        if (state.HasValue)
        {
            bool line = WriteValue(state.Value, max: 1000, startOfLine: true);
            if (!line)
                WriteLine();
        }
        else
            WriteLine("*** Node {0} has no value", node.Guid);

        Flush();
    }

    /// <summary>
    /// Process the given script code as a statement list. If nameRec is null or empty, definitions
    /// each become top-level data flow nodes. Otherwise, definitions become fields in a single
    /// record, which becomes a top-level data flow node with the given name.
    /// </summary>
    public void LoadScript(string code)
    {
        Validation.AssertValue(code);

        try
        {
            try
            {
                _globalsScript = new Dictionary<NPath, (BoundNode bnd, object res)>();
                _hostScript = new ScriptBindHost(this);

                ProcessStatements(code);
            }
            catch (Exception ex)
            {
                WriteLine();
                WriteLine("*** Exception! ***");
                for (var exCur = ex; exCur != null; exCur = exCur.InnerException)
                {
                    WriteLine();
                    WriteLine(exCur.GetType().ToString());
                    WriteLine(exCur.Message);
                    WriteLine(exCur.StackTrace);
                }
            }

            if (_globalsScript.Count == 0)
            {
                WriteLine("*** No new static data nodes.");
                return;
            }

            // Transfer the globals to the document.
            var doc = Doc;
            foreach (var kvp in _globalsScript)
            {
                if (kvp.Key.IsRoot)
                    continue;

                BoundNode bnd = kvp.Value.bnd;
                object value = kvp.Value.res;

                var config = new BndDataConfig(kvp.Key, bnd, value);
                if (doc.TryGetNode(kvp.Key, out var flowNode))
                {
                    WriteLine("*** Overwriting flow node: '{0}'", kvp.Key);
                    doc = doc.DeleteNode(flowNode.Guid);
                }
                doc = doc.CreateFlowNode(kvp.Key, null, config, GuidNext());
            }
            Set(doc);
        }
        finally
        {
            _hostScript = null;
            _globalsScript = null;
        }

        foreach (var node in Doc.GetFlowNodes())
            WriteLine("Flow node: {0}, {1}", node.Guid, node.Name);
        WriteLine();
    }

    private void DumpDiagnostics<TDiag>(Immutable.Array<TDiag> diags)
        where TDiag : BaseDiagnostic
    {
        WriteLine("=== Parse diagnostics:");
        foreach (var diag in diags)
        {
            Write("  *** ");
            diag.Format(this);
            WriteLine();
        }
    }

    private bool ProcessStatements(string code)
    {
        var rsl = RexlStmtList.Create(SourceContext.Create(code));
        if (rsl.HasDiagnostics)
        {
            WriteLine("=== Parse diagnostics:");
            DumpDiagnostics(rsl.Diagnostics);
            if (rsl.HasErrors)
                return false;
        }

        var stmts = rsl.ParseTree;
        foreach (var stmt in stmts.Children)
        {
            if (!ProcessStmt(rsl, stmt))
                return false;
        }

        return true;
    }

    // REVIEW: Extend to handle all the various forms, like Harness does?
    private bool ProcessCmd(CmdStmtNode csn, BoundNode bnd, object value)
    {
        Validation.AssertValue(csn);
        Validation.Assert(csn.Kind == NodeKind.ImportStmt | csn.Kind == NodeKind.ExecuteStmt);
        Validation.AssertValue(bnd);
        Validation.AssertValueOrNull(value);

        if (bnd.Type != DType.Text)
        {
            WriteLine("*** Error: need text value, not type: {0}", bnd.Type);
            return false;
        }

        Validation.Assert(value is string || value == null);
        var val = value as string;

        if (val == null)
        {
            WriteLine("*** Error: need non-null text value");
            return false;
        }

        // REVIEW: Support "in namespace X".
        string script = csn.Kind == NodeKind.ImportStmt ? File.ReadAllText(val) : val;
        return ProcessStatements(script);
    }

    private bool ProcessStmt(RexlStmtList rsl, StmtNode stmt)
    {
        if (stmt is ValueStmtNode vsn)
        {
            if (!TryBind(rsl, vsn.Value, out var bnd))
                return false;
            if (!TryExec(bnd, out var value))
                return false;

            if (stmt is CmdStmtNode csn)
                return ProcessCmd(csn, bnd, value);

            if (stmt is DefinitionStmtNode def)
            {
                // Note that there is no active/current namespace in DocBench.
                NPath full = def.FullName;
                if (_globalsScript.ContainsKey(full))
                    WriteLine("Overwriting global: '{0}'", full);

                this.TWrite("Global '{0}' has DType: ", full).WriteType(bnd.Type);
                this.TWrite(", SysType: ").TWritePrettyType(value?.GetType()).WriteLine();
                WriteLine();

                _globalsScript[full] = (bnd, value);
                return true;
            }

            if (stmt is ExprStmtNode esn)
            {
                bool line = WriteValue(value, max: _maxItems, startOfLine: true);
                if (!line)
                    WriteLine();
                return true;
            }
        }

        // REVIEW: Handle namespace?

        WriteLine("  *** Unexpected statement kind: {0}", stmt);
        return false;
    }

    private bool TryBind(RexlStmtList rsl, ExprNode node, out BoundNode bnd)
    {
        // Get the sub-formula.
        var fma = RexlFormula.CreateSubFormula(rsl, node);
        return TryBind(fma, out bnd);
    }

    private bool TryBind(RexlFormula fma, out BoundNode bnd)
    {
        // Binding.
        DType typeThis = default;
        if (_globalsScript.TryGetValue(NPath.Root, out var pair))
        {
            typeThis = pair.bnd.Type;
            Validation.Assert(typeThis.IsValid);
        }

        var bfma = BoundFormula.Create(fma, _hostScript, typeThis: typeThis);
        if (bfma.HasDiagnostics)
        {
            WriteLine("=== Bind Diagnostics:");
            DumpDiagnostics(bfma.Diagnostics);
            if (!bfma.IsGood)
            {
                bnd = null;
                return false;
            }
        }

        bnd = bfma.BoundTree;
        Validation.Assert(!bnd.HasErrors);
        return true;
    }

    private sealed class ScriptBindHost : FixedNamespaceBindHost
    {
        private readonly Executor _parent;

        // REVIEW: Should we bother handling namespaces properly?

        public ScriptBindHost(Executor parent)
            : base(NPath.Root, NPath.Root)
        {
            _parent = parent;
        }

        public override bool TryFindNamespaceItem(ExprNode ctx, NPath ns, DName name, out NPath path, out DType type, out bool isStream)
        {
            Validation.Assert(name.IsValid);

            isStream = false;
            path = ns.Append(name);
            if (_parent._globalsScript.TryGetValue(path, out var pair))
            {
                type = pair.bnd.Type;
                Validation.Assert(type.IsValid);
                return true;
            }

            path = default;
            type = default;
            return false;
        }

        public override bool IsNamespace(NPath name)
        {
            // REVIEW: Should we bother doing the proper thing?
            return name.IsRoot;
        }

        public override bool TryGetOperInfoOne(NPath name, bool user, bool fuzzy, int arity, out OperInfo info)
        {
            info = _parent._funcs.GetInfo(name);
            Validation.Assert(info is null || info.Oper is not null);
            return info != null;
        }
    }

    private bool TryExec(BoundNode bnd, out object value)
    {
        // Generate the code.
        var resCodeGen = _codeGen.Run(bnd, host: null, _getShowIL() ? new Action<string>(WriteLine) : null);
        int carg = resCodeGen.Globals.Length;

        // Build the args.
        var args = new object[carg];
        if (carg > 0)
        {
            foreach (var glob in resCodeGen.Globals)
            {
                int slot = glob.Slot;
                Validation.AssertIndex(slot, carg);
                Validation.Assert(args[slot] == null);

                if (glob.IsCtx)
                {
                    // Execution context.
                    // REVIEW: Implement logging?
                    args[slot] = ExecCtx.CreateBare();
                }
                else
                {
                    Validation.Assert(_globalsScript.ContainsKey(glob.Name));
                    (BoundNode bndCur, object valCur) = _globalsScript[glob.Name];
                    Validation.Assert(bndCur.Type == glob.Type);
                    args[slot] = valCur;
                }
            }
        }

        value = resCodeGen.Func(args);

        return true;
    }

    /// <summary>
    /// Writes a string representation of a value. Returns whether it ended with a new line.
    /// REVIEW: Make this use ValueWriter.
    /// </summary>
    private bool WriteValue(object val, int max = 10, string prefix = "", bool startOfLine = false)
    {
        if (val != null && !(val is string) && val is IEnumerable seq)
            return WriteSeq(seq, max, prefix, startOfLine);

        if (val is RecordBase rec)
            return WriteRec(rec, max, prefix, startOfLine);

        Write("{0}", val ?? "<null>");
        return false;
    }

    private bool WriteRec(RecordBase rec, int max, string prefix, bool startOfLine)
    {
        Validation.Assert(rec != null);

        Type st = rec.GetType();

        if (startOfLine)
        {
            Write(prefix);
            startOfLine = false;
        }

        Write("{ ");
        string sep = "";
        int index = 0;
        string prefixInner = prefix + "  ";
        string prefixValue = prefixInner + "  ";

        foreach (var fld in st.GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(x => x.Name))
        {
            if (startOfLine)
                Write(prefixInner);
            else
                Write(sep);
            sep = ", ";
            if (index++ >= max)
            {
                Write("...");
                break;
            }

            object v = fld.GetValue(rec);

            Write(fld.Name);
            Write(": ");
            startOfLine = WriteValue(v, max, prefixValue, false);
        }

        Write(startOfLine ? prefix : " ");
        Write("}");
        return false;
    }

    private bool WriteSeq(IEnumerable seq, int max, string prefix, bool startOfLine)
    {
        Validation.Assert(seq != null);

        if (!startOfLine)
            WriteLine();

        Write(prefix);
        WriteLine("Sequence: {0}", seq.GetType());

        int index = 0;
        prefix += "  ";
        string prefixInner = prefix + "    ";
        foreach (var v in seq)
        {
            Write(prefix);
            if (index >= max)
            {
                WriteLine("...");
                break;
            }
            Write("{0,2}) ", index);
            if (!WriteValue(v, max, prefixInner, false))
                WriteLine();
            index++;
        }

        return true;
    }

    public abstract class DataConfig : DocumentBase.NodeConfig
    {
        public NPath NameOrig { get; }

        public DType Type { get; }

        public object Value { get; }

        protected DataConfig(NPath nameOrig, DType type, object value)
        {
            Validation.Assert(!nameOrig.IsRoot);
            Validation.Assert(type.IsValid);
            Validation.AssertValueOrNull(value);

            NameOrig = nameOrig;
            Type = type;
            Value = value;
        }
    }

    private sealed class BndDataConfig : DataConfig
    {
        private readonly BoundNode _bnd;

        public BndDataConfig(NPath nameOrig, BoundNode bnd, object value)
            : base(nameOrig, bnd.VerifyValue().Type, value)
        {
            Validation.AssertValue(bnd);
            _bnd = bnd;
        }
    }

    private sealed class GraphHostImpl : Document.GraphHost
    {
        private readonly Executor _parent;
        private Dictionary<string, OperInfo> _lowerPathToFin;

        public GraphHostImpl(Executor parent)
        {
            _parent = parent;
        }

        public override BindOptions GetBindOptions() => default;

        public override bool TryGetBaseType(FlowNode node, out DType type)
        {
            Validation.AssertValue(node);
            Validation.Assert(node.Formula == null);

            if (node.Config is DataConfig dc)
            {
                type = dc.Type;
                return true;
            }

            // Shouldn't get here.
            Validation.Assert(false);
            type = default;
            return false;
        }

        public override Immutable.Array<(RexlFormula fma, int grp)> GetExtraFormulas(FlowNode node, DType typeBase, Document.GraphNode gnPrev)
        {
            Validation.AssertValue(node);
            Validation.Assert(typeBase.IsValid);
            Validation.AssertValueOrNull(gnPrev);
            return node.ExtraFormulas;
        }

        public override bool TryGetGlobalFuzzyMatch(NPath ns, DName name, out DName nameGuess)
        {
            int count = ns.NameCount + 1;
            foreach (var node in _parent.Doc.GetFlowNodes())
            {
                var pathCur = node.Name;
                if (pathCur.NameCount == count && IsFuzzyMatch(pathCur.Leaf, name) && pathCur.Parent == ns)
                {
                    nameGuess = pathCur.Leaf;
                    return true;
                }
            }
            return base.TryGetGlobalFuzzyMatch(ns, name, out nameGuess);
        }

        public override bool TryGetNamespaceFuzzyMatch(NPath ns, DName name, out DName nameGuess)
        {
            int count = ns.NameCount + 1;
            foreach (var (pathCur, _) in _parent.Doc.GetNamespaces())
            {
                if (pathCur.NameCount == count && IsFuzzyMatch(pathCur.Leaf, name) && pathCur.Parent == ns)
                {
                    nameGuess = pathCur.Leaf;
                    return true;
                }
            }
            return base.TryGetNamespaceFuzzyMatch(ns, name, out nameGuess);
        }

        public override bool TryGetOperInfo(NPath name, bool user, bool fuzzy, int arity, out OperInfo info)
        {
            if (user)
            {
                info = null;
                return false;
            }

            if (!fuzzy)
            {
                info = _parent._funcs.GetInfo(name);
                Validation.Assert(info is null || info.Oper is not null);
                return info != null;
            }

            if (_lowerPathToFin == null)
            {
                _lowerPathToFin = new Dictionary<string, OperInfo>();
                foreach (var item in _parent._funcs.GetInfos(includeHidden: true, includeDeprecated: true))
                    _lowerPathToFin[item.Path.ToDottedSyntax().ToLowerInvariant()] = item;
            }
            return _lowerPathToFin.TryGetValue(name.ToDottedSyntax().ToLowerInvariant(), out info);
        }
    }
}

sealed class Functions : OperationRegistry
{
    public Functions()
        : base(BuiltinFunctions.Instance)
    {
    }
}
