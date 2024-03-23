// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Flow;

using BfmaTuple = Immutable.Array<(BoundFormula bfma, int grp)>;
using FmaTuple = Immutable.Array<(RexlFormula fma, int grp)>;

/// <summary>
/// Extension methods for an "extra" formula tuple. The items are sorted by group and
/// the formulas are non-null. The <see cref="IsValid(Immutable.Array{(RexlFormula fma, int grp)})"/>
/// method tests for validity. The others throw if the tuple isn't valid.
/// </summary>
public static class FmaTupleExtensions
{
    /// <summary>
    /// Return whether the tuple is valid. The formulas need to be non-null and the items
    /// need to be sorted by group number.
    /// </summary>
    public static bool IsValid(this FmaTuple extra)
    {
        if (extra.IsDefault)
            return false;

        if (extra.Length == 0)
            return true;
        if (extra[0].fma == null)
            return false;

        int grpPrev = extra[0].grp;
        for (int i = 0; i < extra.Length; i++)
        {
            (var fmaCur, int grpCur) = extra[i];
            if (fmaCur == null)
                return false;
            if (grpPrev > grpCur)
                return false;
            grpPrev = grpCur;
        }

        return true;
    }

    /// <summary>
    /// Get the index range of the formula group.
    /// </summary>
    public static (int ifmaMin, int ifmaLim) GetGrpIndices(this FmaTuple extra, int grp)
    {
        Validation.BugCheckParam(extra.IsValid(), nameof(extra));

        int ifmaMin = 0;
        while (ifmaMin < extra.Length && extra[ifmaMin].grp < grp)
            ifmaMin++;
        int ifmaLim = ifmaMin;
        while (ifmaLim < extra.Length && extra[ifmaLim].grp == grp)
            ifmaLim++;
        return (ifmaMin, ifmaLim);
    }

    /// <summary>
    /// Get the size of the formula group.
    /// </summary>
    public static int GetGrpCount(this FmaTuple extra, int grp)
    {
        (int min, int lim) = extra.GetGrpIndices(grp);
        return lim - min;
    }

    /// <summary>
    /// Return whether these have identical contents.
    /// </summary>
    public static bool AreSame(this FmaTuple a, FmaTuple b)
    {
        if (a.AreIdentical(b))
            return true;

        int cfma = a.Length;
        if (cfma != b.Length)
            return false;
        for (int i = 0; i < cfma; i++)
        {
            var (fma0, grp0) = a[i];
            var (fma1, grp1) = b[i];
            if (fma0 != fma1 || grp0 != grp1)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Return whether these have identical contents.
    /// </summary>
    public static bool AreSame(this BfmaTuple a, BfmaTuple b)
    {
        if (a.AreIdentical(b))
            return true;

        int cfma = a.Length;
        if (cfma != b.Length)
            return false;
        for (int i = 0; i < cfma; i++)
        {
            var (bfma0, grp0) = a[i];
            var (bfma1, grp1) = b[i];
            if (bfma0 != bfma1 || grp0 != grp1)
                return false;
        }
        return true;
    }
}

partial class DocumentBase
{
    /// <summary>
    /// Base class for <see cref="DocNode"/> and <see cref="GraphNode"/>.
    /// </summary>
    public abstract class NodeBase
    {
        /// <summary>
        /// Unique node id. This is used to represent the node to external client
        /// code (possibly in a different process).
        /// </summary>
        public abstract Guid Guid { get; }

        /// <summary>
        /// The name of this node.
        /// </summary>
        public abstract NPath Name { get; }

        /// <summary>
        /// The associated main formula, possibly null.
        /// </summary>
        public abstract RexlFormula Formula { get; }

        /// <summary>
        /// Whether this node has any formulas.
        /// </summary>
        public abstract bool HasFormulas { get; }

        /// <summary>
        /// The config information, possibly null.
        /// </summary>
        public abstract NodeConfig Config { get; }

        /// <summary>
        /// Whether this node has any formula parse errors.
        /// </summary>
        public abstract bool HasParseErrors { get; }

        private protected NodeBase()
        {
        }
    }

    /// <summary>
    /// Represents a node in the <see cref="Document"/>.
    /// </summary>
    public abstract class DocNode : NodeBase
    {
        public sealed override Guid Guid { get; }

        public sealed override NPath Name { get; }

        public sealed override RexlFormula Formula { get; }

        public sealed override NodeConfig Config { get; }

        protected DocNode(Guid guid, NPath name, RexlFormula fma, NodeConfig config)
        {
            Validation.BugCheckParam(!name.IsRoot, nameof(name));
            Validation.BugCheckParam(guid != default, nameof(guid));

            Guid = guid;
            Name = name;
            Formula = fma;
            Config = config;
        }

        public GridConfig GetGridConfig()
        {
            var grid = Config as GridConfig;
            Validation.BugCheck(grid != null);
            return grid;
        }

        public bool IsGridNode(out GridConfig grid)
        {
            grid = Config as GridConfig;
            return grid != null;
        }

        public bool IsGridNode()
        {
            return Config is GridConfig;
        }
    }

    /// <summary>
    /// Represents a node in the <see cref="Document"/> that has explicit extra formulas
    /// (rather than auto-generated).
    /// </summary>
    public abstract class DocExtNode : DocNode
    {
        /// <summary>
        /// The extra formulas, which are always "explicit" (not auto-generated).
        /// </summary>
        public FmaTuple ExtraFormulas { get; }

        public sealed override bool HasFormulas => Formula != null || ExtraFormulas.Length > 0;

        public sealed override bool HasParseErrors
        {
            get
            {
                if (Formula != null && Formula.HasErrors)
                    return true;
                return ExtraFormulas.Length > 0 && ExtraFormulas.Any(ef => ef.fma.HasErrors);
            }
        }

        protected DocExtNode(Guid guid, NPath name, RexlFormula fma, NodeConfig config, FmaTuple extra)
            : base(guid, name, fma, config)
        {
            Validation.BugCheckParam(extra.IsDefault || extra.IsValid(), nameof(extra));
            ExtraFormulas = !extra.IsDefault ? extra : FmaTuple.Empty;
        }
    }

    /// <summary>
    /// Node configuration information. Note that this is optional for a flow node.
    /// It is assumed to be immutable!
    /// </summary>
    public abstract class NodeConfig
    {
        protected NodeConfig()
        {
        }
    }
}
