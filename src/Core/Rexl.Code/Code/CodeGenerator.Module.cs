// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Code;

using LocEnt = MethodGenerator.Local;

partial class CodeGeneratorBase
{
    partial class Impl
    {
        protected override void PostVisitImpl(BndModToRecNode bnd, int idx)
        {
            Validation.AssertValue(bnd);

            var typeMod = bnd.Child.Type;
            var typeRec = bnd.Type;
            Validation.Assert(typeRec == typeMod.ModuleToRecord());

            var stMod = GetSysType(typeMod);
            var meth = stMod.GetMethod("GetRecord", Type.EmptyTypes).VerifyValue();
            Validation.Assert(meth.ReturnType == GetSysType(typeRec));
            Validation.Assert(meth.IsVirtual);
            IL.Callvirt(meth);
            PopType(typeMod);
            PushType(typeRec);
        }

        protected override bool PreVisitModule(BndModuleNode bmod, int idx)
        {
            // We create two sub-functions:
            // * Create/update the items tuple. This one may reference the exec context. Currently we
            //   do this with the normal mechanism - nothing novel. Note that referencing externals
            //   goes through the externals tuple, which is created at the top level, not in a sub-function.
            // * Create the record from the items tuple. This is "detached" and references only the
            //   items tuple.
            AssertIdx(bmod, idx);

            if (bmod.IsInvalid)
                throw Unexpected("Can't codegen invalid module");

            DType typeMod = bmod.Type;
            Validation.Assert(typeMod.IsModuleReq);
            DType typeRec = typeMod.ModuleToRecord();
            Validation.Assert(typeRec.IsRecordReq);
            DType typeItems = bmod.TypeItems;
            Validation.Assert(typeItems.IsTupleReq);
            Validation.Assert(typeItems.TupleArity == bmod.Items.Length);

            Type stRec = GetSysType(typeRec);
            Type stMod = GetSysType(typeMod);
            Type stItems = GetSysType(typeItems);

            bool hasExt = bmod.ScopeExt is not null;
            DType typeExt;
            Type stExt;
            if (hasExt)
            {
                typeExt = bmod.ScopeExt.Type;
                Validation.Assert(typeExt.IsTupleReq);
                Validation.Assert(typeExt.TupleArity == bmod.Externals.Length);
                stExt = GetSysType(typeExt);
            }
            else
            {
                typeExt = default;
                stExt = default;
            }

            FrameInfo frame;

            // Create the externals tuple and fill it in.
            // Start the items creation function and set up the externals scope.
            int idxCur = idx + 1;
            using var locExt = hasExt ? MethCur.AcquireLocal(stExt) : default;
            RefMaps.ScopeInfo siExt = default;
            if (hasExt)
            {
                // Create the externals tuple and fill it in.
                GenCreateTuple(typeExt, stExt, withType: false);
                IL.Stloc(locExt);

                for (int i = 0; i < bmod.Externals.Length; i++)
                {
                    IL.Ldloc(locExt);
                    var ext = bmod.Externals[i];
                    idxCur = ext.Accept(this, idxCur);
                    PopType(ext.Type);
                    GenStoreSlot(typeExt, stExt, i, ext.Type);
                }

                // The generated function modifies items in place. Items to "skip" should have
                // their corresponding flag (in dontGen) set to true.
                // Sig: stItems set_module_items_ext(items:stItems, dontGen:bool[], exts:stExt)
                // REVIEW: Fix the usesExec value!
                frame = StartFunctionCore("set_module_items_ext",
                    null, null, usesGlobals: false, usesExec: _frameCur.UsesExecCtx,
                    stItems, typeof(bool[]), stItems, stRec, stExt);

                // Set up the scope to use the externals tuple.
                siExt = _refMaps.GetScopeInfoFromOwner(bmod, idx, bmod.ScopeExt);
                Validation.Assert(siExt.Slot == -2);
                Util.Add(ref _nonMapLocals, (siExt.Scope, default));
                Util.Add(ref _frameCur.ScopeMap, siExt.Index, LocArgInfo.FromArg(4, stExt));
                PushScope(bmod.ScopeExt, bmod, idx, -2, isArgValid: true);
            }
            else
            {
                // The generated function modifies items in place. Items to "skip" should have
                // their corresponding flag (in dontGen) set to true.
                // Sig: stItems set_module_items_ext(items:stItems, dontGen:bool[])
                // REVIEW: Fix the usesExec value!
                frame = StartFunctionCore("set_module_items",
                    null, null, false, usesExec: _frameCur.UsesExecCtx,
                    stItems, typeof(bool[]), stItems, stRec);
            }

            var stSetItems = frame.DelegateType;
            var syms = bmod.Symbols;
            int csym = syms.Length;
            var items = bmod.Items;
            int citem = items.Length;
            {
                var laiFlags = LocArgInfo.FromArg(1, typeof(bool[]));
                var laiItems = LocArgInfo.FromArg(2, stItems);
                var laiRec = LocArgInfo.FromArg(3, stRec);

                // Set up the scope to use the items local.
                var siItems = _refMaps.GetScopeInfoFromOwner(bmod, idx, bmod.ScopeItems);
                Validation.Assert(siItems.Slot == -1);
                Util.Add(ref _nonMapLocals, (siItems.Scope, default));
                Util.Add(ref _frameCur.ScopeMap, siItems.Index, laiItems);
                PushScope(bmod.ScopeItems, bmod, idx, -1, isArgValid: true);

                // Set the slots of the items tuple.
                // For item ifma:
                // * If ifma is the value for symbol isym and the symbol is "settable" (parameter or free variable)
                //   and flags[citem + isym] is true, then the value comes from rec.
                // * Otherwise, if flags[ifma] is true, then the item slot is not set (assumed to be valid).
                // * Otherwise, the value is computed from its formula.
                int isym = 0;
                for (int ifma = 0; ifma < citem; ifma++)
                {
                    Validation.AssertIndex(isym, csym);
                    var sym = syms[isym];
                    Validation.Assert(ifma <= sym.IfmaValue);
                    var value = items[ifma];

                    Label labDontComp = default;
                    if (ifma == sym.IfmaValue)
                    {
                        switch (sym.SymKind)
                        {
                        case ModSymKind.Parameter:
                        case ModSymKind.FreeVariable:
                            Label labNotRec = default;
                            IL
                                .LdLocArg(in laiFlags)
                                .Ldc_I4(citem + isym)
                                .Ldelem_U1()
                                .Brfalse(ref labNotRec)
                                .LdLocArg(in laiItems)
                                .LdLocArg(in laiRec);
                            GenLoadField(typeRec, stRec, sym.Name, sym.Type);
                            GenStoreSlot(typeItems, stItems, ifma, sym.Type);
                            IL
                                .Br(ref labDontComp)
                                .MarkLabel(labNotRec);
                            break;
                        }
                        // Done with this symbol.
                        isym++;
                    }

                    IL
                        .LdLocArg(in laiFlags)
                        .Ldc_I4(ifma)
                        .Ldelem_U1()
                        .Brtrue(ref labDontComp);

                    IL.LdLocArg(in laiItems);

                    PushNestedArg(bmod, idx, ifma, needsDelegate: false);
                    int cur = value.Accept(this, idxCur);
                    PopNestedArg();

                    Validation.Assert(cur == idxCur + value.NodeCount);
                    idxCur = cur;
                    PopType(value.Type);
                    GenStoreSlot(typeItems, stItems, ifma, value.Type);

                    IL.MarkLabel(labDontComp);
                }
                Validation.Assert(isym == csym);

                // Tear down the scopes.
                PopScope(bmod.ScopeItems);
                var (sc, loc) = Util.Pop(_nonMapLocals);
                Validation.Assert(sc == siItems.Scope);
                Validation.Assert(!loc.IsActive);
                Validation.Assert(_frameCur.ScopeMap[siItems.Index].SysType == laiItems.SysType);
                Validation.Assert(_frameCur.ScopeMap[siItems.Index].Index == laiItems.Index);
                _frameCur.ScopeMap.Remove(siItems.Index);

                if (hasExt)
                {
                    PopScope(bmod.ScopeExt);
                    (sc, loc) = Util.Pop(_nonMapLocals);
                    Validation.Assert(sc == siExt.Scope);
                    Validation.Assert(!loc.IsActive);
                    Validation.Assert(_frameCur.ScopeMap[siExt.Index].SysType == stExt);
                    Validation.Assert(_frameCur.ScopeMap[siExt.Index].Index == -4);
                    _frameCur.ScopeMap.Remove(siExt.Index);
                }

                IL.LdLocArg(in laiItems);
                PushType(stItems);

                EndFunctionCore();
            }

            // Keep the set_module_items on the stack for creating the runtime module.
            // Also invoke it to fill in the items.
            IL.Dup();

            // Use an all-false array for the flags.
            IL
                .Ldc_I4(citem + csym)
                .Newarr(typeof(bool));
            PushType(typeof(bool[]));

            // Create the items tuple.
            GenCreateTuple(typeItems, stItems, withType: false);
            PushType(stItems);

            // Push null record for the call to set_module_items.
            IL.Ldnull();
            PushType(stRec);

            MethodInfo methInvoke;
            if (hasExt)
            {
                IL.Ldloc(locExt);
                PushType(stExt);
                methInvoke = stSetItems.GetMethod("Invoke", new[] { typeof(bool[]), stItems, stRec, stExt });
            }
            else
                methInvoke = stSetItems.GetMethod("Invoke", new[] { typeof(bool[]), stItems, stRec });
            Validation.Assert(methInvoke is not null);
            EmitCallVirt(methInvoke);
            PopType(stItems);

            // Get the make_module_record function.
            frame = StartFunctionDisjoint("make_module_record", stRec, stItems);
            Type stMakeRec = frame.DelegateType;

            {
                var laiItems = LocArgInfo.FromArg(1, stItems);

                // Create the record generator.
                using var rg = CreateRecordGenerator(typeRec);
                Validation.Assert(rg.RecSysType == stRec);
                for (int isym = 0; isym < bmod.Symbols.Length; isym++)
                {
                    var sym = bmod.Symbols[isym];
                    var name = sym.Name;
                    Validation.Assert(rg.RecType.GetNameTypeOrDefault(name) == sym.Type);

                    int ifmaVal = sym.IfmaValue;
                    Validation.AssertIndex(ifmaVal, items.Length);

                    var typeVal = items[ifmaVal].Type;
                    Validation.Assert(sym.Type == typeVal);

                    rg.SetFromStackPre(name, sym.Type);
                    IL.LdLocArg(in laiItems);
                    GenLoadSlot(typeItems, stItems, ifmaVal, typeVal);
                    rg.SetFromStackPost();
                }
                rg.Finish();
                PushType(stRec);
                EndFunctionCore();
            }
            PopType(stMakeRec);

            GenLoadConst(bmod);

            // Instantiate the runtime module.
            ConstructorInfo ctor;
            if (hasExt)
            {
                IL.Ldloc(locExt);
                Type stModImpl = typeof(RuntimeModule<,,>).MakeGenericType(stRec, stItems, stExt);
                ctor = stModImpl.GetConstructor(new Type[] { stSetItems, stItems, stMakeRec, typeof(BndModuleNode), stExt });
                if (ctor is null)
                    throw Unexpected("Ctor for runtime module with externals");
            }
            else
            {
                // Construct the module instance.
                Type stModImpl = typeof(RuntimeModule<,>).MakeGenericType(stRec, stItems);
                ctor = stModImpl.GetConstructor(new Type[] { stSetItems, stItems, stMakeRec, typeof(BndModuleNode) });
                if (ctor is null)
                    throw Unexpected("Ctor for runtime module");
            }
            IL.Newobj(ctor);
            PushType(typeMod);

            return false;
        }

        protected override void PostVisitImpl(BndModuleNode bnd, int idx)
        {
            // REVIEW: Implement!
            throw NYI("module");
        }

        protected override bool PreVisitModuleProjection(BndModuleProjectionNode bmp, int idx)
        {
            AssertIdx(bmp, idx);
            Validation.Assert(!bmp.HasErrors);
            Validation.Assert(bmp.Type.IsModuleReq);

            DType typeMod = bmp.Type;

            var mod = bmp.Module;
            Validation.Assert(mod.Type == typeMod);

            var scope = bmp.Scope;
            var rec = bmp.Record;

            int cur = idx + 1;

            Validation.Assert(scope.Kind == ScopeKind.With);
            Validation.Assert(rec.Type.IsRecordReq);
            Validation.Assert(rec.Type.FieldCount > 0);

            if (!IsLocOrArg(mod, cur, out var laiTmp))
                cur = mod.Accept(this, cur);
            else
                cur += mod.NodeCount;

            // Init the scope.
            // REVIEW: Perhaps the scope should be the record rather than the module?
            bool isValid = scope.IsValidArg(mod);
            InitScope(scope, bmp, idx, -1, isValid);
            PushScope(scope, bmp, idx, -1, isValid);
            Validation.Assert(_scopeCur.Scope == scope);
            Util.TryGetValue(_frameCur.ScopeMap, _scopeCur.Index, out var laiSrc).Verify();
            Validation.Assert(laiSrc.Index == laiTmp.Index || !laiTmp.IsActive);

            IL.LdLocArg(in laiSrc);
            PushType(typeMod);

            // Create the modifications record. Note that it may be partial and will end up with a null rrti.
            // The result will NOT be a complete functional record, but is fine for our purposes.
            var typeRec = typeMod.ModuleToRecord();
            using var rg = TypeManager.RecordGenerator.Create(_typeManager, MethCur, typeRec,
                genLoadRrti: null, partial: true);

            PushNestedArg(bmp, idx, 0, needsDelegate: false);

            var fields = new HashSet<DName>(rec.Type.FieldCount);
            if (rec is BndRecordNode brn)
            {
                int tmp = cur;
                cur += 1;
                cur = GenRecordFieldStores(rg, brn.Items, fields, cur);
                Validation.Assert(cur == tmp + brn.NodeCount);
            }
            else
            {
                var stInner = GetSysType(rec.Type);
                using LocEnt locInner = EnsureLocOrArg(rec, ref cur, stInner, out var laiInner, false);
                foreach (var tn in rec.Type.GetNames())
                {
                    fields.Add(tn.Name).Verify();
                    Validation.Assert(typeRec.GetNameTypeOrDefault(tn.Name) == tn.Type);
                    rg.SetFromLocalField(tn.Name, rec.Type, tn.Name, in laiInner);
                }
            }

            PopNestedArg();

            var stRec = rg.RecSysType;
            rg.Finish();
            PushType(typeRec);

            // Cleanup.
            PopScope(scope);
            DisposeScope(scope, bmp, idx, -1);

            GenLoadConst(fields);
            PushType(typeof(HashSet<DName>));

            var meth = typeof(RuntimeModule<>).MakeGenericType(stRec)
                .GetMethod("Update", new[] { stRec, typeof(HashSet<DName>) }).VerifyValue();
            EmitCallVirt(meth);

            PeekType(typeMod);

            return false;
        }

        protected override void PostVisitImpl(BndModuleProjectionNode bnd, int idx)
        {
            throw Unexpected("module projection post");
        }
    }
}
