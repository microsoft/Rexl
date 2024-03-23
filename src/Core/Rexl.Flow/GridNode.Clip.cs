// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Flow;

using FieldMap = ReadOnly.Dictionary<DName, DName>;

partial class DocumentBase
{
    partial class GridConfig
    {
        /// <summary>
        /// This encapsulates information in a grid of columns and rows.
        /// The contained information is contractually immutable.
        /// </summary>
        public abstract partial class Clip : DataClip
        {
            private protected Clip(DType typeRaw)
                : base(typeRaw)
            {
            }

            /// <summary>
            /// Return the number of columns.
            /// </summary>
            public abstract int ColCount { get; }

            /// <summary>
            /// Return the number of rows.
            /// </summary>
            public abstract int RowCount { get; }

            /// <summary>
            /// Return the record <see cref="DType"/>.
            /// </summary>
            public DType RecordType => ClipItemType;
        }
    }

    partial class GridConfigImpl
    {
        /// <summary>
        /// A wrapper around an <see cref="GridConfigImpl"/>. This owns the <see cref="GridConfigImpl"/> and
        /// protects it from mutation.
        /// </summary>
        private sealed partial class ClipCore : Clip
        {
            private readonly GridConfigImpl _impl;

            public override DType ClipItemType => RawItemType;
            public override bool MapsFields => false;
            public override int ColCount => _impl.ColCount;
            public override int RowCount => _impl.RowCount;

            public GridConfigImpl GetImpl() => _impl;

            private ClipCore(GridConfigImpl impl)
                : base(impl.VerifyValue().RecordType.ToSequence())
            {
                Validation.AssertValue(impl);
                _impl = impl;
            }

            public static ClipCore Wrap(GridConfigImpl impl)
            {
                if (impl == null)
                    return null;
                Validation.Assert(impl.IsReadOnly);
                return new ClipCore(impl);
            }

            public override DName ClipFieldToRawField(DName nameFld)
            {
                Validation.Assert(!MapsFields);
                Validation.Assert(ClipItemType == RawItemType);
                Validation.BugCheckParam(RawItemType.Contains(nameFld), nameof(nameFld));
                return nameFld;
            }

            public override DataClip MapFields(FieldMap fieldMap)
            {
                var impl = _impl.CreateSubClip(fieldMap, default);
                return impl == _impl ? this : new ClipCore(impl);
            }

            public override DataClip CreateSubClip(FieldMap fieldMap = default, ReadOnly.Array<(int min, int lim)> rowMap = default)
            {
                var impl = _impl.CreateSubClip(fieldMap, rowMap);
                return impl == _impl ? this : new ClipCore(impl);
            }

            public override long GetCount(Action callback)
            {
                Validation.BugCheckValueOrNull(callback);
                return RowCount;
            }

            public override bool TryGetCount(out long count)
            {
                count = RowCount;
                return true;
            }

            protected override void WriteClipItems(Stream strm)
            {
                Validation.AssertValue(strm);

                _impl.Save(strm);
            }
        }
    }
}
