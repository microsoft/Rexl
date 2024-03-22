// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Rexl;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;

namespace Microsoft.Rexl.Harness;

// This partial handles global values.
partial class SimpleHarnessBase
{
    /// <summary>
    /// The globals. The bnd value may be null. If not, bnd.Type should match type.
    /// </summary>
    private readonly Dictionary<NPath, (DType type, BoundNode bnd, object res)> _globals;

    public override bool HasGlobal(NPath name) => _globals.ContainsKey(name);

    public override Immutable.Array<NPath> GetGlobalNames()
    {
        var bldr = Immutable.Array.CreateBuilder<NPath>(_globals.Count, init: true);
        int i = 0;
        foreach (var key in _globals.Keys)
            bldr[i++] = key;
        Validation.Assert(i == _globals.Count);
        bldr.Sort(NPathComparer.Instance.Compare);
        return bldr.ToImmutable();
    }

    public override Immutable.Array<(NPath name, DType type)> GetGlobalInfos()
    {
        var bldr = Immutable.Array.CreateBuilder<(NPath name, DType type)>(_globals.Count, init: true);
        int i = 0;
        foreach (var kvp in _globals)
            bldr[i++] = (kvp.Key, kvp.Value.type);
        Validation.Assert(i == _globals.Count);
        bldr.Sort((a, b) => NPathComparer.Instance.Compare(a.name, b.name));
        return bldr.ToImmutable();
    }

    protected override bool TryGetThisType(out DType type)
    {
        if (!_globals.TryGetValue(NPath.Root, out var info))
        {
            type = default;
            return false;
        }

        type = info.type;
        Validation.Assert(type.IsValid);
        return true;
    }

    public override bool TryGetGlobalType(NPath name, out DType type)
    {
        if (!_globals.TryGetValue(name, out var info))
        {
            type = default;
            return false;
        }

        type = info.type;
        Validation.Assert(type.IsValid);
        return true;
    }

    /// <summary>
    /// Processes a global definition, including setting the value in the global map. When
    /// <paramref name="name"/> is the root path, the definition is for <c>this</c>.
    /// </summary>
    protected virtual void HandleGlobal(bool error, NPath name, DType type, BoundNode bnd, ref object value)
    {
        Validation.Assert(type.IsValid);
        Validation.Assert(bnd == null || bnd.Type == type || bnd.IsProcCall);

        bool existing = _globals.TryGetValue(name, out var info);
        Validation.Assert(existing == info.type.IsValid);
        if (error)
        {
            HandleGlobalBad(name, settingToNull: existing);
            if (existing)
            {
                // REVIEW: Would it be better to remove the global? That's a bit tricky given
                // the current data structures.
                SetGlobal(name, DType.Null, null, null);
            }
        }
        else
        {
            HandleGlobalGood(name, type, value?.GetType(), info.type);
            SetGlobal(name, type, bnd, value);
        }
    }

    /// <summary>
    /// Sets the <paramref name="type"/> and <paramref name="value"/> of the global with the given
    /// <paramref name="name"/>. Assignment to <c>this</c> is indicated by the <paramref name="name"/>
    /// being the root path.
    /// Note that this does <e>not</e> call <see cref="HandleGlobal"/>.
    /// </summary>
    protected void SetGlobal(NPath name, DType type, BoundNode bnd, object value)
    {
        Validation.Assert(name.IsRoot | name.StartsWith(NsRoot));
        Validation.Assert(type.IsValid);
        Validation.Assert(bnd == null || bnd.Type == type || bnd.IsProcCall);

        _globals[name] = (type, bnd, value);
        UpdateNamespaces(name);
    }

    /// <summary>
    /// Called after a value has been computed from an expression.
    /// </summary>
    protected virtual void HandleValue(DType type, BoundNode bnd, object value, ExecCtx ctx)
    {
        Validation.Assert(type.IsValid);
        Validation.Assert(bnd == null || bnd.Type == type || bnd.IsProcCall);

        Sink.PostValue(type, value);
        if (IsVerbose)
            WritePingInfo(ctx);
    }

    protected virtual void WritePingInfo(ExecCtx ctx)
    {
        if (ctx is TotalPingsExecCtx pctx)
            Sink.WriteLine("*** Ctx ping count: {0}", pctx.PingCount);
        else if (ctx is IdBndMapExecCtx tctx)
        {
            Sink.WriteLine("*** Ctx ping count: {0}", tctx.GetTotalPingCount());
            if (tctx.PingCountNoId > 0)
                Sink.WriteLine("    [_] {0}", tctx.PingCountNoId);

            foreach (var (sub, rng) in tctx.Map.BndToIdRng)
            {
                Validation.Assert(rng.Count > 0);
                bool hasPings = false;
                for (int id = rng.Min; id < rng.Lim; id++)
                {
                    if (tctx.GetIdPingCount(id) > 0)
                    {
                        hasPings = true;
                        break;
                    }
                }

                if (!hasPings)
                    continue;

                long count = tctx.GetIdPingCount(rng.Min);
                string idRest = "";
                string countRest = "";
                if (rng.Count > 1)
                {
                    var sb = new StringBuilder("=").Append(count);
                    for (int id = rng.Min + 1; id < rng.Lim; id++)
                    {
                        long pings = tctx.GetIdPingCount(id);
                        sb.Append('+').Append(pings);
                        count += pings;
                    }
                    countRest = sb.ToString();
                    idRest = ":" + rng.Lim;
                }

                string strBnd = BndNodePrinter.Run(sub, BndNodePrinter.Verbosity.Terse);
                Sink.WriteLine("    [{0}{1}]({2}{3}): {4}", rng.Min, idRest, count, countRest, strBnd);
            }
        }
    }

    /// <summary>
    /// A global declaration had some error. If there is already a global with the given path,
    /// its value is being set to <c>null</c>, otherwise, it is not being added.
    /// </summary>
    protected virtual void HandleGlobalBad(NPath name, bool settingToNull)
    {
        Validation.Assert(settingToNull == HasGlobal(name));

        if (!IsVerbose)
            return;

        Sink.WriteLine(settingToNull ? "Error, setting global to null: {0}" : "Error, not creating global: {0}",
            name.IsRoot ? "this" : (object)name);
    }

    /// <summary>
    /// A global declaration was processed. The <paramref name="typePrev"/> is valid if
    /// there is already a global with the given <paramref name="name"/> that will be overwritten.
    /// </summary>
    protected virtual void HandleGlobalGood(NPath name, DType type, Type st, DType typePrev)
    {
        Validation.Assert(typePrev.IsValid == HasGlobal(name));

        if (!IsVerbose)
            return;

        string qual = "";
        if (typePrev.IsValid)
        {
            if (typePrev == type)
                return;
            qual = "(modified) ";
        }
        if (name.IsRoot)
            Sink.Write("this");
        else
            Sink.Write("Global '{0}'", name);
        Sink.Write(" has {0}DType: {1}, SysType: ", qual, type);
        Sink.TWritePrettyType(st).WriteLine();

        for (var ns = name; !(ns = ns.Parent).IsRoot;)
        {
            if (HasGlobal(ns))
                Sink.WriteLine("Namespace for '{0}' hidden by global: {1}", name, ns);
            if (HasTask(ns))
                Sink.WriteLine("Namespace for '{0}' hidden by task: {1}", name, ns);
        }
    }

    protected override bool TryGetGlobalValue(NPath name, DType type, out object value)
    {
        if (_globals.TryGetValue(name, out var glb))
        {
            Expected(glb.type == type, "Global type mismatch");
            value = glb.res;
            return true;
        }

        value = null;
        return false;
    }
}
