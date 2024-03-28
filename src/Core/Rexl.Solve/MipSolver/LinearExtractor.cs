// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;
using Microsoft.Rexl.Symbolic;
using Microsoft.Rexl.Utility;

namespace Microsoft.Rexl.Solve;

/// <summary>
/// This implements resolving a <see cref="BoundNode"/> to a linear combination <see cref="LinComb"/>
/// of variables. Note that this treats floating point arithmetic as associative, even though it isn't.
/// </summary>
public static class LinearExtractor
{
    /// <summary>
    /// Implements resolving <paramref name="bnd"/> as a linear combination of variables in
    /// the given <paramref name="varMap"/>. Returns <see cref="LinComb.Error"/> if the expression
    /// can't be reduced to a linear combination.
    /// </summary>
    public static LinComb Run(SymbolMap varMap, BoundNode bnd)
    {
        Validation.BugCheckValue(varMap, nameof(varMap));
        Validation.BugCheckValue(bnd, nameof(bnd));

        return Impl.Run(varMap, bnd);
    }

    private sealed class Impl : NoopBoundTreeVisitor
    {
        private readonly SymbolMap _varMap;
        private readonly List<(BoundNode node, LinComb lin)> _stack;

        private Impl(SymbolMap varMap)
        {
            _varMap = varMap;
            _stack = new List<(BoundNode node, LinComb lin)>();
        }

        public static LinComb Run(SymbolMap varMap, BoundNode eval)
        {
            var impl = new Impl(varMap);

            try
            {
                int num = eval.Accept(impl, 0);
                Validation.Assert(num == eval.NodeCount);
            }
            catch
            {
                return LinComb.Error;
            }

            Validation.Assert(impl._stack.Count == 1);
            return impl._stack[0].lin;
        }

        private Exception NYI()
        {
            throw new NotImplementedException();
        }

        private void PushError(BoundNode node)
        {
            Push(node, LinComb.Error);
        }

        private void Push(BoundNode node, double value)
        {
            Push(node, LinComb.Create(value));
        }

        private void Push(BoundNode node, LinComb lin)
        {
            Validation.AssertValue(node);
            _stack.Add((node, lin));
        }

        private LinComb Pop()
        {
            Validation.Assert(_stack.Count > 0);
            int index = _stack.Count - 1;
            var res = _stack[index];
            _stack.RemoveAt(index);
            return res.lin;
        }

        protected override void VisitCore(BndLeafNode node, int idx)
        {
            PushError(node);
        }

        protected override bool PreVisitCore(BndParentNode node, int idx)
        {
            PushError(node);
            return false;
        }

        protected override void PostVisitCore(BndParentNode node, int idx)
        {
            throw Validation.BugExcept();
        }

        protected override void VisitImpl(BndIntNode node, int idx)
        {
            Push(node, node.Value.ToR8());
        }

        protected override void VisitImpl(BndFltNode node, int idx)
        {
            Push(node, node.Value);
        }

        protected override void VisitImpl(BndGlobalNode node, int idx)
        {
            PushError(node);
        }

        protected override void VisitImpl(BndFreeVarNode node, int idx)
        {
            int vid = node.Id;
            if ((uint)vid < (uint)_varMap.VarCount && _varMap.GetVariable(vid) == node)
                Push(node, LinComb.Create(vid, 1));
            else
                base.VisitImpl(node, idx);
        }

        protected override bool PreVisitImpl(BndCastNumNode node, int idx)
        {
            Validation.Assert(node.Type.IsNumericReq);
            return true;
        }

        protected override void PostVisitImpl(BndCastNumNode node, int idx)
        {
            Validation.Assert(node.Type.IsNumericReq);
            var lin = Pop();
            Push(node, lin);
        }

        protected override bool PreVisitImpl(BndVariadicOpNode node, int idx)
        {
            switch (node.Op)
            {
            case BinaryOp.Add:
                DoAdd(node, idx);
                return false;
            case BinaryOp.Mul:
                DoMul(node, idx);
                return false;
            }
            throw NYI();
        }

        private void DoAdd(BndVariadicOpNode node, int idx)
        {
            Validation.Assert(node.Op == BinaryOp.Add);
            var args = node.Args;

            int cur = idx + 1;
            var bldr = LinComb.CreateBuilder(args.Length);
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                cur = arg.Accept(this, cur);
                var lin = Pop();
                if (!bldr.TryAdd(lin, node.Inverted.TestBit(i)))
                {
                    PushError(node);
                    return;
                }
            }
            Validation.Assert(cur == idx + node.NodeCount);
            Push(node, bldr.Make());
        }

        private void DoMul(BndVariadicOpNode node, int idx)
        {
            Validation.Assert(node.Op == BinaryOp.Mul);
            var args = node.Args;

            double coefNum = 1.0;
            double coefDen = 1.0;

            int count = 0;
            var lin = LinComb.Zero;

            int cur = idx + 1;
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                cur = arg.Accept(this, cur);
                var linCur = Pop();
                if (linCur.IsError)
                {
                    // Not a linear combination. Only OK if the coef ends up zero.
                    count += 2;
                }
                else if (linCur.IsConstant)
                {
                    if (node.Inverted.TestBit(i))
                        coefDen *= linCur.ConstantTerm;
                    else
                        coefNum *= linCur.ConstantTerm;
                }
                else
                {
                    lin = linCur;
                    count++;
                }
            }
            Validation.Assert(cur == idx + node.NodeCount);

            if (coefDen == 0 || !coefNum.IsFinite() || !coefDen.IsFinite())
            {
                PushError(node);
                return;
            }

            if (coefNum == 0)
            {
                Push(node, 0);
                return;
            }

            if (count > 1)
            {
                PushError(node);
                return;
            }

            if (count == 0)
            {
                Push(node, coefNum / coefDen);
                return;
            }

            if (coefNum != 1)
                lin *= coefNum / coefDen;
            else if (coefDen != 1)
                lin /= coefDen;

            Push(node, lin);
        }
    }
}
