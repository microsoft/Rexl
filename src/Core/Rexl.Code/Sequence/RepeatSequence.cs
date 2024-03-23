// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Sequence;

public sealed class RepeatSequence<T> : ICachingEnumerable<T>, ICanCount
{
    private readonly T _value;
    private readonly long _num;

    public RepeatSequence(T value, long num)
    {
        Validation.BugCheckParam(num > 0, nameof(num));
        _value = value;
        _num = num;
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (long i = 0; i < _num; i++)
            yield return _value;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public bool TryGetCount(out long count)
    {
        count = _num;
        return true;
    }

    public long GetCount(Action? callback)
    {
        Validation.BugCheckValueOrNull(callback);
        return _num;
    }
}
