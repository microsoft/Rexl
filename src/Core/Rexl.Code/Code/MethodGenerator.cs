// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;

namespace Microsoft.Rexl.Code;

/// <summary>
/// Build a <see cref="DynamicMethod"/> that is associated with the given module. This also implements
/// a cache of locals. A client can acquire a local, use it, then release (Dispose) it so the local can
/// be reused. This cuts down on the number of declared locals.
/// </summary>
public sealed class MethodGenerator
{
    private readonly DynamicMethod _method;
    private ILWriter _ilw;
    private Dictionary<Type, List<LocalBuilder>> _mptypelocals;

    public int ArgCount { get; }

    public int LocCount => _ilw.LocCount;

    public MethodGenerator(string name, ILLogKind logKind, Type returnType, params Type[] parameterTypes)
        : this(name, null, logKind, returnType, parameterTypes)
    {
    }

    public MethodGenerator(string name, Module module, ILLogKind logKind, Type returnType, params Type[] parameterTypes)
    {
        Validation.BugCheckNonEmpty(name, nameof(name));
        Validation.BugCheckValueOrNull(module);
        Validation.BugCheckValueOrNull(returnType);
        Validation.BugCheckValueOrNull(parameterTypes);

        // REVIEW: What should the default module be?
        if (module == null)
            module = typeof(MethodGenerator).Module;

        _method = new DynamicMethod(name, returnType, parameterTypes, module);
        ArgCount = Util.Size(parameterTypes);
        _ilw = new ILWriter(parameterTypes, _method.GetILGenerator(), logKind);
    }

    public ILWriter Il
    {
        get
        {
            Validation.BugCheck(_ilw != null, "Cannot access IL for a method that has already been created");
            return _ilw;
        }
    }

    public string GetIL(string indent = "  ")
    {
        var sink = GetHeader(new SbSysTypeSink()).TWriteLine();
        foreach (var line in Il.ResetLines())
            sink.TWrite(indent).WriteLine(line);
        return sink.Builder.ToString();
    }

    public string GetHeader()
    {
        return GetHeader(new SbSysTypeSink()).Builder.ToString();
    }

    public TSink GetHeader<TSink>(TSink sink)
        where TSink : SysTypeSink
    {
        Validation.BugCheckValue(sink, nameof(sink));
        sink.TWrite(_method.Name).Write('(');
        string pre = "";
        foreach (var pi in _method.GetParameters())
        {
            sink.TWrite(pre).WriteRawType(pi.ParameterType);
            pre = ", ";
        }
        sink.TWrite("):").WriteRawType(_method.ReturnType);
        return sink;
    }

    private void Clear()
    {
        _ilw = null;
        _mptypelocals = null;
    }

    public MethodInfo GetMethodInfo()
    {
        Clear();
        return _method;
    }

    public Delegate CreateDelegate(Type delegateType)
    {
        Validation.BugCheckValue(delegateType, nameof(delegateType));
        Clear();
        return _method.CreateDelegate(delegateType);
    }

    public Delegate CreateInstanceDelegate(Type delegateType, object target)
    {
        Validation.BugCheckValue(delegateType, nameof(delegateType));
        Clear();
        return _method.CreateDelegate(delegateType, target);
    }

    public Local AcquireLocal(Type type)
    {
        Validation.BugCheckValue(type, nameof(type));
        Validation.BugCheck(_ilw != null, "Cannot access IL for a method that has already been created");

        if (_mptypelocals != null && _mptypelocals.TryGetValue(type, out var locals) && locals.Count > 0)
            return new Local(this, locals.Pop());
        return new Local(this, _ilw.DeclareLocalRaw(type));
    }

    private void ReleaseLocal(LocalBuilder localBuilder)
    {
        Validation.AssertValue(localBuilder);

        if (_ilw == null)
            return;

        var type = localBuilder.LocalType;
        Validation.Assert(type != null);

        List<LocalBuilder> locals;
        if (_mptypelocals == null)
            _mptypelocals = new Dictionary<Type, List<LocalBuilder>>();
        else if (_mptypelocals.TryGetValue(type, out locals))
        {
            Validation.Assert(!locals.Contains(localBuilder));
            locals.Add(localBuilder);
            return;
        }

        locals = new List<LocalBuilder>(4);
        _mptypelocals.Add(type, locals);
        locals.Add(localBuilder);
    }

    public struct Local : IDisposable
    {
        private readonly MethodGenerator _owner;
        private LocalBuilder _local;

        /// <summary>
        /// Should only be created by MethodGenerator. Too bad C# can't enforce this without
        /// reversing the class nesting.
        /// </summary>
        internal Local(MethodGenerator owner, LocalBuilder local)
        {
            Validation.AssertValue(owner);
            Validation.AssertValue(local);
            _owner = owner;
            _local = local;
        }

        public void Dispose()
        {
            if (_local == null)
                return;

            Validation.AssertValue(_owner);
            _owner.ReleaseLocal(_local);
            _local = null;
        }

        public bool IsActive { get { return _local != null; } }

        public int Index { get { return _local != null ? _local.LocalIndex : -1; } }

        public Type Type { get { return _local?.LocalType; } }

        /// <summary>
        /// Swap contents of this entry with the given one.
        /// </summary>
        public void Swap(ref Local other)
        {
            Util.Swap(ref this, ref other);
        }

        public static implicit operator LocalBuilder(Local loc)
        {
            Validation.BugCheck(loc.IsActive);
            return loc._local;
        }

        public static implicit operator LocArgInfo(Local loc)
        {
            Validation.BugCheck(loc.IsActive);
            return loc._local;
        }
    }
}
