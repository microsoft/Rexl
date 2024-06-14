// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.Rexl;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

using UdoTuple = Immutable.Array<OperInfo>;

/// <summary>
/// Base class for rexl (baseline) tests where each line is its own test case.
/// </summary>
public abstract class RexlLineTestsBase<TSink, TOpts> : RexlTestsBase<TSink, TOpts>
    where TSink : TextSink
{
    // The current namespace.
    protected NPath _nsCur;

    // The global type information.
    protected Dictionary<NPath, DType> _nsToGlobalType;

    // Map from name/path to udf/udp overloads, sorted by arity.
    protected Dictionary<NPath, UdoTuple> _nameToUdos;

    // Maps from lower-cased udo path to actual path, created lazily.
    private Dictionary<string, NPath> _lowerUdoPathToPath;

    protected abstract bool AnyOut { get; }

    protected abstract OperationRegistry Operations { get; }

    protected virtual void InitFile()
    {
        // Set everything to the defaults.
        _nsCur = NPath.Root;
        _nsToGlobalType = new Dictionary<NPath, DType>();
        _nsToGlobalType.Add(NPath.Root, DType.EmptyRecordReq);
        _nameToUdos = new Dictionary<NPath, UdoTuple>();
    }

    protected virtual void EnsureNs(NPath ns)
    {
        for (; !ns.IsRoot; ns = ns.Parent)
        {
            if (_nsToGlobalType.ContainsKey(ns))
                break;
            _nsToGlobalType.Add(ns, DType.EmptyRecordReq);
        }
    }

    protected virtual void SetGlobalTypes(DType typeRec, bool add = false)
    {
        Assert.IsTrue(typeRec.IsRecordReq);
        Assert.IsTrue(_nsToGlobalType.ContainsKey(_nsCur));

        if (!add)
            _nsToGlobalType[_nsCur] = typeRec;
        else
        {
            var items = _nsToGlobalType[_nsCur];
            foreach (var tn in typeRec.GetNames())
                items = items.SetNameType(tn.Name, tn.Type);
            _nsToGlobalType[_nsCur] = items;
        }
    }

    /// <summary>
    /// This serves up segments/lines. It supports a stack of arrays of segments, enabling
    /// the file "include" functionality.
    /// </summary>
    private struct SegmentList
    {
        private string _pathCur;
        private string[] _segsCur;
        private int _isegCur;

        private List<(string path, string[] segs, int iseg)> _stack;

        /// <summary>
        /// The path of the current file.
        /// </summary>
        public string Path => _pathCur;

        public SegmentList(string path, string[] segs)
        {
            _pathCur = path;
            _segsCur = segs;
            _isegCur = 0;
            _stack = null;
        }

        /// <summary>
        /// Insert a new batch of segments.
        /// </summary>
        public void InsertSegs(string path, string[] segs)
        {
            Util.Add(ref _stack, (_pathCur, _segsCur, _isegCur));
            _pathCur = path;
            _segsCur = segs;
            _isegCur = 0;
        }

        /// <summary>
        /// Gets the next segment. Returns <c>null</c> when done.
        /// </summary>
        public string GetNext()
        {
            for (; ; )
            {
                if (_isegCur < _segsCur.Length)
                    return _segsCur[_isegCur++].VerifyValue();
                if (!Util.TryPop(_stack, out var tup))
                    return null;
                (_pathCur, _segsCur, _isegCur) = tup;
            }
        }
    }

    protected virtual bool UseBlock(TOpts testOpts) => false;

    protected void ProcessFile(string pathHead, string pathTail, string text, TOpts testOpts = default)
    {
        InitFile();

        bool useBlock = UseBlock(testOpts);
        var segs = new SegmentList(Path.Join(pathHead, pathTail),
            useBlock ? SplitHashBlocks(text).ToArray() : SplitLines(text));

        // Whether we're currently processing declarations of any form, either :: or ``. This is used to trigger
        // emitting an extra blank line to separate declaration related output from test case output.
        bool inDecls = false;
        var sbDefns = new StringBuilder();

        void ProcessDefns()
        {
            if (sbDefns.Length > 0)
            {
                if (!inDecls && AnyOut)
                    Sink.WriteLine();
                ProcessGlobalScript(sbDefns.ToString(), testOpts);
                sbDefns.Clear();
                inDecls = true;
            }
        }

        for (; ; )
        {
            var seg = segs.GetNext();
            if (seg is null)
                break;

            LAgain:
            var script = seg.Trim();
            if (script.Length == 0)
                continue;

            if (script.Length >= 2 && script[0] == script[1])
            {
                string orig = script;
                string rem = null;

                if (useBlock)
                {
                    int ich = script.IndexOf('\r');
                    if (ich < 0)
                        ich = script.IndexOf("\n");
                    if (ich >= 0)
                    {
                        rem = script.Substring(ich);
                        script = script.Substring(0, ich);
                    }
                }

                bool done = true;
                switch (script[0])
                {
                default:
                    script = orig;
                    rem = null;
                    done = false;
                    break;

                case '<':
                    // Include a file.
                    {
                        string rest = script.Substring(2).Trim();
                        if (rest.StartsWith('"') && rest.EndsWith('"'))
                            rest = rest.Substring(1, rest.Length - 2);
                        if (string.IsNullOrWhiteSpace(rest))
                            break;

                        string path = Path.Join(segs.Path, "..", rest);
                        path = Path.GetFullPath(path);
                        var tx = File.ReadAllText(path);

                        // Normalize line endings so character indices match in base lines.
                        tx = NormalizeLines(tx);
                        segs.InsertSegs(path, SplitLines(tx));
                    }
                    break;

                case '/':
                    // Skip comments.
                    break;
                case '$':
                    // Skip the rest of the file.
                    return;

                case '%':
                    {
                        // Namespace.
                        ProcessDefns();
                        string rest = script.Substring(2).Trim();
                        if (!LexUtils.TryLexPath(rest, out NPath nsNew) && !string.IsNullOrEmpty(rest))
                            Assert.Fail("Bad namespace specification");

                        EnsureNs(nsNew);
                        _nsCur = nsNew;

                        if (!inDecls && AnyOut)
                            Sink.WriteLine();
                        Sink.WriteLine("**** Changed namespace to: {0}", _nsCur);

                        inDecls = true;
                    }
                    break;

                case ':':
                    {
                        // This line defines a new set of, or additional, "globals". Parse it.

                        // REVIEW: Do we need this mechanism for CodeGen tests? Would be nice to
                        // have syntax to run code-gened formula on multiple "sets" of globals.

                        ProcessDefns();
                        int ich = 2;
                        bool add = false;
                        if (ich < script.Length && (script[ich] == ':' || script[ich] == '+'))
                            add = script[ich++] == '+';
                        if (ich >= script.Length)
                            break;

                        if (!DType.TryDeserialize(script.Substring(ich), out DType typeRec))
                            Assert.Fail("Bad global type specification");
                        Assert.IsTrue(typeRec.IsRecordXxx, "Bad global specification in '{0}'", pathTail);
                        if (!inDecls && AnyOut)
                            Sink.WriteLine();

                        SetGlobalTypes(typeRec, add);

                        if (_nsCur.IsRoot)
                            Sink.WriteLine("**** {0} globals: {1}", add ? "Add" : "New", typeRec);
                        else
                            Sink.WriteLine("**** {0} globals for {1}: {2}", add ? "Add" : "New", _nsCur, typeRec);

                        inDecls = true;
                    }
                    break;

                case '`':
                    {
                        int ich = 2;
                        if (ich < script.Length && script[ich] == '`')
                            ich++;

                        script = script.Substring(ich).Trim();
                        if (script.Length > 0)
                            sbDefns.AppendLine(script);
                    }
                    break;
                }

                if (done)
                {
                    if (rem is null)
                        continue;
                    seg = rem;
                    goto LAgain;
                }
            }

            ProcessDefns();

            if (inDecls)
            {
                Sink.WriteLine();
                inDecls = false;
            }

            ProcessScript(script, testOpts);
            Sink.WriteLine("###");
        }
    }

    protected virtual void ProcessGlobalScript(string code, TOpts testOpts)
    {
        var rsl = RexlStmtList.Create(SourceContext.Create(code));
        var stmts = rsl.ParseTree;

        if (rsl.HasDiagnostics)
        {
            Sink.WriteLine("Diagnostics while parsing global script.");
            foreach (var diag in rsl.Diagnostics)
            {
                diag.Format(Sink, options: DiagFmtOptions.DefaultTest | DiagFmtOptions.PositionBoth);
                Sink.WriteLine();
            }
        }

        foreach (var stmt in stmts.Children)
        {
            if (stmt is DefinitionStmtNode defn)
                ProcessDefn(rsl, defn, testOpts);
            else if (stmt is ExprStmtNode esn)
                ProcessThisExpr(rsl, esn, testOpts);
            else if (stmt is FuncStmtNode fsn)
                ProcessUdf(rsl, fsn);
            else
                Assert.Fail("The global statement ({0}) is not a definition.", stmt);
        }
    }

    protected virtual void ProcessDefn(RexlStmtList rsl, DefinitionStmtNode def, TOpts testOpts)
    {
        NPath name;
        string disp;
        if (def.ForThis)
        {
            name = default;
            disp = "<this>";
        }
        else
        {
            name = def.IdentPath.Combine(_nsCur, NPath.Root);
            disp = name.ToDottedSyntax();
        }
        ProcessDefnCore(rsl, def.Value, name, disp, testOpts);
    }

    protected virtual void ProcessThisExpr(RexlStmtList rsl, ExprStmtNode esn, TOpts testOpts)
    {
        ProcessDefnCore(rsl, esn.Value, NPath.Root, "<this>", testOpts);
    }

    protected abstract BindOptions TestOptsToBindOpts(TOpts testOpts);

    protected virtual void ProcessDefnCore(RexlStmtList rsl, ExprNode expr, NPath full, string disp, TOpts testOpts)
    {
        var bfma = Bind(rsl, expr, full, disp, testOpts);
        if (bfma.HasErrors)
            return;

        HandleGlobal(bfma, full, disp);
    }

    protected abstract void HandleGlobal(BoundFormula bfma, NPath full, string disp);

    protected virtual void ProcessUdf(RexlStmtList rsl, FuncStmtNode fsn)
    {
        var name = fsn.IdentPath.Combine(_nsCur, NPath.Root);
        var nameStr = name.ToDottedSyntax();

        if (!_nameToUdos.TryGetValue(name, out var udos))
            udos = UdoTuple.Empty;
        int arity = fsn.ParamNames.Length;
        bool have = TryFindUdoArity(udos, arity, out int index);

        if (fsn.Value is BoxNode)
        {
            // Remove the UDO.
            if (have)
            {
                if (udos.Length > 1)
                    _nameToUdos[name] = udos.RemoveAt(index);
                else
                {
                    _nameToUdos.Remove(name);
                    _lowerUdoPathToPath = null;
                }
                var kind = udos[index].Oper.IsProc ? "Udp" : "Udf";
                Sink.WriteLine("**** {0} removed: {1}", kind, nameStr);
            }
            return;
        }

        var fma = RexlFormula.CreateSubFormula(rsl, fsn.Value);
        var udf = UserFunc.Create(name, NPath.Root, fsn.ParamNames, fma, fsn.IsProp);
        var info = new OperInfo(udf.Path, udf);
        if (have)
        {
            udos = udos.SetItem(index, info);
            Sink.WriteLine("**** Redefined udf: {0}, arity: {1}", nameStr, udf.Arity);
        }
        else
        {
            udos = udos.Insert(index, info);
            Sink.WriteLine("**** New udf: {0}, arity: {1}", nameStr, udf.Arity);
        }
        _nameToUdos[name] = udos;
        if (_lowerUdoPathToPath != null)
            _lowerUdoPathToPath[nameStr.ToLowerInvariant()] = name;
    }

    protected static bool TryFindUdoArity(UdoTuple overloads, int arity, out int index)
    {
        Validation.Assert(!overloads.IsDefault);

        int min = 0;
        int lim = overloads.Length;
        while (min < lim)
        {
            int mid = (int)((uint)(min + lim) >> 1);
            if (arity <= overloads[mid].Oper.ArityMin)
                lim = mid;
            else
                min = mid + 1;
        }
        Validation.Assert(min == lim);
        Validation.AssertIndexInclusive(min, overloads.Length);
        Validation.Assert(min == overloads.Length || arity <= overloads[min].Oper.ArityMin);
        Validation.Assert(min == 0 || arity > overloads[min - 1].Oper.ArityMin);

        index = min;
        return min < overloads.Length && overloads[min].Oper.ArityMin == arity;
    }

    protected Dictionary<string, NPath> EnsureUdoFuzzyMap()
    {
        var map = _lowerUdoPathToPath;
        if (map == null)
        {
            map = new Dictionary<string, NPath>(_nameToUdos.Count);
            foreach (var path in _nameToUdos.Keys)
                map[path.ToDottedSyntax().ToLowerInvariant()] = path;
            _lowerUdoPathToPath = map;
        }
        return map;
    }

    protected abstract void ProcessScript(string script, TOpts testOpts);

    protected virtual DType GetGlobalType(NPath full)
    {
        Assert.IsTrue(!full.IsRoot);

        // Look for "type-only" globals.
        var ns = full.PopOne(out DName leaf);
        if (!_nsToGlobalType.TryGetValue(ns, out var items))
            return default;

        if (!items.TryGetNameType(leaf, out var type))
            return default;

        Validation.Assert(type.IsValid);
        return type;
    }

    protected abstract DType GetThisType();

    protected virtual BoundFormula Bind(RexlStmtList rsl, ExprNode expr, NPath full, string disp, TOpts testOpts)
    {
        var fma = RexlFormula.CreateSubFormula(rsl, expr);
        ValidateScript(fma);

        var bindOpts = TestOptsToBindOpts(testOpts);
        var host = new BindHostImpl(this);
        var bfma = BoundFormula.Create(fma, host, bindOpts);
        ValidateBfma(bfma, host);

        var source = fma.Tokens.Source;
        if (bfma.HasWarnings)
        {
            Sink.WriteLine("Warning while binding definition for [{0}]: [{1}].", disp, expr);
            Sink.WriteLine("Bind warnings:");
            foreach (var diag in bfma.Warnings)
            {
                var options = DiagFmtOptions.DefaultTest;
                if (diag is RexlDiagnostic rd && rd.Tok.Stream.Source != source)
                    options |= DiagFmtOptions.PositionBoth;
                diag.Format(Sink, options: options);
                Sink.WriteLine();
            }
            Sink.WriteLine();
        }

        if (bfma.HasErrors)
        {
            Sink.WriteLine("Error while binding definition for [{0}]: [{1}].", disp, expr);
            Sink.WriteLine();
            Sink.WriteLine("Bind errors:");
            foreach (var diag in bfma.Errors)
            {
                var options = DiagFmtOptions.DefaultTest;
                if (diag is RexlDiagnostic rd && rd.Tok.Stream.Source != source)
                    options |= DiagFmtOptions.PositionBoth;
                diag.Format(Sink, options: options);
                Sink.WriteLine();
            }
            Sink.WriteLine();
        }

        return bfma;
    }

    protected virtual void ValidateScript(RexlScript script)
    {
        // REVIEW: How much validation should we do here?
        Assert.IsNotNull(script);
    }

    protected virtual void ValidateBfma(BoundFormula bfma, BindHostImpl host)
    {
        Assert.IsNotNull(bfma);
        Assert.IsNotNull(host);

        // Ensures that the bound nodes have correct ChildCount and HasErrors properties.
        BoundTreeValidator.Run(bfma.BoundTree, bfma.HasErrors);
    }

    protected sealed class BindHostImpl : ReferenceBindHost
    {
        private readonly RexlLineTestsBase<TSink, TOpts> _parent;
        // Whether single letter globals of sequence type are streaming.
        private readonly bool _streaming;

        // For builtin operations, not UDOs.
        private Dictionary<string, OperInfo> _lowerPathToInfo;

        public BindHostImpl(RexlLineTestsBase<TSink, TOpts> parent, bool streaming = false)
            : base(parent.VerifyValue()._nsCur, NPath.Root)
        {
            _parent = parent;
            _streaming = streaming;
        }

        public override bool TryGetThisType(out DType type)
        {
            type = _parent.GetThisType();
            return type.IsValid;
        }

        protected override bool TryFindNamespaceItemCore(NPath ns, DName name, out NPath path, out DType type, out bool isStream)
        {
            Assert.IsTrue(name.IsValid);

            isStream = false;
            path = ns.Append(name);

            type = _parent.GetGlobalType(path);
            if (type.IsValid)
            {
                if (_streaming && type.IsSequence && name.Value.Length == 1)
                    isStream = true;
                return true;
            }

            if (_parent._nsToGlobalType.ContainsKey(path))
                return true;

            path = default;
            return false;
        }

        public override bool TryFindNamespaceItemFuzzy(ExprNode ctx, NPath ns, DName name, out DName nameGuess, out DType type, out bool isStream)
        {
            Assert.IsTrue(name.IsValid);

            isStream = false;

            if (_parent._nsToGlobalType.TryGetValue(ns, out var items))
            {
                foreach (var tn in items.GetNames())
                {
                    if (StrComparer.EqCi(tn.Name, name))
                    {
                        nameGuess = tn.Name;
                        type = tn.Type;
                        Validation.Assert(type.IsValid);
                        return true;
                    }
                }
            }

            type = default;
            foreach (var path in _parent._nsToGlobalType.Keys)
            {
                if (path.NameCount == ns.NameCount + 1 && path.Parent == ns && StrComparer.EqCi(path.Leaf, name))
                {
                    nameGuess = path.Leaf;
                    return true;
                }
            }

            return base.TryFindNamespaceItemFuzzy(ctx, ns, name, out nameGuess, out type, out isStream);
        }

        public override bool IsNamespace(NPath name)
        {
            return _parent._nsToGlobalType.ContainsKey(name);
        }

        protected override bool TryGetOperInfoCore(NPath name, bool user, bool fuzzy, int arity, out OperInfo info)
        {
            if (!fuzzy)
            {
                if (!user)
                {
                    // REVIEW: Which should have priority? Builtin or UDO?
                    info = _parent.Operations.GetInfo(name);
                    Validation.Assert(info is null || info.Oper is not null);
                    return info != null;
                }

                if (!_parent._nameToUdos.TryGetValue(name, out var udos))
                {
                    info = null;
                    return false;
                }
                Validation.Assert(!udos.IsDefaultOrEmpty);

                // Note that the first two cases could be combined, but we keep them split for ease of debugging.
                if (TryFindUdoArity(udos, arity, out int index))
                    info = udos[index];
                else if (index < udos.Length)
                    info = udos[index];
                else
                    info = udos[index - 1];
                return true;
            }
            else
            {
                var lower = name.ToDottedSyntax().ToLowerInvariant();

                if (!user)
                {
                    var map = _lowerPathToInfo;
                    if (map == null)
                    {
                        map = new Dictionary<string, OperInfo>();
                        foreach (var item in _parent.Operations.GetInfos(includeHidden: true, includeDeprecated: true))
                            map[item.Path.ToDottedSyntax().ToLowerInvariant()] = item;
                        _lowerPathToInfo = map;
                    }

                    if (map.TryGetValue(lower, out info))
                        return true;
                }

                var mapUdo = _parent.EnsureUdoFuzzyMap();
                if (!mapUdo.TryGetValue(lower, out var path) || !_parent._nameToUdos.TryGetValue(path, out var udos))
                {
                    info = null;
                    return false;
                }
                Validation.Assert(!udos.IsDefaultOrEmpty);

                // Note that the first two cases could be combined, but we keep them split for ease of debugging.
                if (TryFindUdoArity(udos, arity, out int index))
                    info = udos[index];
                else if (index < udos.Length)
                    info = udos[index];
                else
                    info = udos[index - 1];
                return true;
            }
        }
    }
}
