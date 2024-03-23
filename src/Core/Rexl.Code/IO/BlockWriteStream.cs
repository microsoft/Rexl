// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.IO;

using Conditional = System.Diagnostics.ConditionalAttribute;

/// <summary>
/// Implements a writable stream where the actual storage is updated at a block level, for example,
/// with an azure file. Typically, the storage is "remote" with block writes being preferred over
/// small writes. Note that this does not (by default) keep an open stream on the remote storage.
/// </summary>
public abstract partial class BlockWriteStream : Stream
{
    protected const int DefaultBlockCapacity = 1 << 16;

    // The current block.
    private Block _block;

    // The current position. Note that this might not be within the bounds of the current
    // block (when seeking is supported).
    private long _posCur;

    /// <summary>
    /// The current block.
    /// </summary>
    protected Block BlockCur => _block;

    public sealed override bool CanWrite => true;

    public sealed override bool CanTimeout => false;

    public sealed override long Position
    {
        get => _posCur;
        set => Seek(value, SeekOrigin.Begin);
    }

    protected BlockWriteStream(int capBlock = DefaultBlockCapacity)
        : base()
    {
        Validation.Assert(capBlock > 0);
        _block = new Block(capBlock);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            FlushCore(_block, force: true);
            _block.Dispose();
        }
        base.Dispose(disposing);
    }

    public sealed override async ValueTask DisposeAsync()
    {
        // Do not change this code. Put cleanup code in the 'Dispose(bool)' method
        // and/or `DisposeCoreAsync()` method.
        try
        {
            await DisposeCoreAsync().ConfigureAwait(false);

            // Dispose unmanaged resources.
            Dispose(disposing: false);
        }
        finally
        {
            GC.SuppressFinalize(this);
        }
    }

    protected virtual async ValueTask DisposeCoreAsync()
    {
        await FlushCoreAsync(_block, force: true, default).ConfigureAwait(false);
        _block.Dispose();
    }

    public sealed override void Flush()
    {
        FlushCore(_block, force: false);
    }

    public sealed override Task FlushAsync(CancellationToken ct)
    {
        return FlushCoreAsync(_block, force: false, ct).AsTask();
    }

    public void Flush(bool force)
    {
        FlushCore(_block, force);
    }

    public ValueTask FlushAsync(bool force, CancellationToken ct)
    {
        return FlushCoreAsync(_block, force, ct);
    }

    /// <summary>
    /// Called to signal that flushing to the ultimate storage may be desirable. If <paramref name="force"/>
    /// is <c>true</c>, all remaining writes should be performed.
    /// </summary>
    protected abstract void FlushCore(Block block, bool force);

    /// <summary>
    /// Called to signal that flushing to the ultimate storage may be desirable. If <paramref name="force"/>
    /// is <c>true</c>, all remaining writes should be performed.
    /// </summary>
    protected abstract ValueTask FlushCoreAsync(Block block, bool force, CancellationToken ct);

    protected void SetPositionCore(long posDst)
    {
        Validation.Assert(CanSeek);
        Validation.AssertIndexInclusive(posDst, Length);
        _posCur = posDst;
    }

    private int GetCurSpace()
    {
        long posLim = _block.PosCap;
        if (Validation.IsInRange(_posCur, _block.Pos, posLim))
            return (int)(posLim - _posCur);
        return 0;
    }

    /// <summary>
    /// Makes sure the current block contains the logical position and there is room for at least
    /// one more byte at that position.
    /// </summary>
    private int EnsureSpace()
    {
        long posLim = _block.PosCap;
        if (Validation.IsInRange(_posCur, _block.Pos, posLim))
            return (int)(posLim - _posCur);

        _block = GetBlock(_posCur, _block);
        Validation.Assert(_block != null);
        posLim = _block.PosCap;
        Validation.AssertInRange(_posCur, _block.Pos, posLim);

        return (int)(posLim - _posCur);
    }

    /// <summary>
    /// Makes sure the current block contains the logical position and there is room for at least
    /// one more byte at that position.
    /// </summary>
    private async ValueTask<int> EnsureSpaceAsync(CancellationToken ct)
    {
        long posLim = _block.PosCap;
        if (Validation.IsInRange(_posCur, _block.Pos, posLim))
            return (int)(posLim - _posCur);

        _block = await GetBlockAsync(_posCur, _block, ct);
        Validation.Assert(_block != null);
        posLim = _block.PosCap;
        Validation.AssertInRange(_posCur, _block.Pos, posLim);

        return (int)(posLim - _posCur);
    }

    /// <summary>
    /// This is called when the current block (parameter <paramref name="block"/>) does not contain
    /// the current position (parameter <paramref name="pos"/>). The implementation should return a block
    /// that does contain the current position. A simple implementation will write out the dirty
    /// portion of the current block and return that block after calling <see cref="Block.Reset"/>.
    /// A more sophisticated implementation may schedule writing of the current block in the future
    /// and return a different block.
    /// </summary>
    protected abstract Block GetBlock(long pos, Block block);

    /// <summary>
    /// This is called when the current block (parameter <paramref name="block"/>) does not contain
    /// the current position (parameter <paramref name="pos"/>). The implementation should return a block
    /// that does contain the current position. A simple implementation will write out the dirty
    /// portion of the current block and return that block after calling <see cref="Block.Reset"/>.
    /// A more sophisticated implementation may schedule writing of the current block in the future
    /// and return a different block.
    /// </summary>
    protected abstract ValueTask<Block> GetBlockAsync(long pos, Block block, CancellationToken ct);

    /// <summary>
    /// Writes into the current block. The current position must be within the current block.
    /// </summary>
    private void WriteToBlock(ReadOnlySpan<byte> src)
    {
        Validation.Assert(src.Length > 0);
        Validation.AssertInRange(_posCur, _block.Pos, _block.PosCap);
        Validation.Assert(_posCur + src.Length <= _block.PosCap);

        int ib = (int)(_posCur - _block.Pos);
        src.CopyTo(_block.ForWrite(ib, src.Length));
        _posCur += src.Length;
        ib += src.Length;
    }

    public sealed override void WriteByte(byte value)
    {
        int cb = EnsureSpace();
        Validation.AssertInRange(_posCur, _block.Pos, _block.PosCap);

        int ib = (int)(_posCur - _block.Pos);
        _block.ForWrite(ib, 1)[0] = value;
        _posCur += 1;
    }

    public sealed override void Write(byte[] buffer, int offset, int count)
    {
        Write(buffer.AsSpan(offset, count));
    }

    public sealed override void Write(ReadOnlySpan<byte> src)
    {
        if (src.Length == 0)
            return;

        for (; ; )
        {
            int cb = EnsureSpace();
            if (src.Length <= cb)
                break;
            WriteToBlock(src.Slice(0, cb));
            src = src.Slice(cb);
        }
        WriteToBlock(src);
    }

    public sealed override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
    {
        if (count == 0)
            return Task.CompletedTask;

        int cb = GetCurSpace();
        if (count <= cb)
        {
            WriteToBlock(buffer.AsSpan(offset, count));
            return Task.CompletedTask;
        }
        return WriteCoreAsync(buffer.AsMemory(offset, count), ct).AsTask();
    }

    public sealed override ValueTask WriteAsync(ReadOnlyMemory<byte> src, CancellationToken ct = default)
    {
        if (src.Length == 0)
            return ValueTask.CompletedTask;

        int cb = GetCurSpace();
        if (src.Length <= cb)
        {
            WriteToBlock(src.Span);
            return ValueTask.CompletedTask;
        }
        return WriteCoreAsync(src, ct);
    }

    private async ValueTask WriteCoreAsync(ReadOnlyMemory<byte> src, CancellationToken ct = default)
    {
        Validation.Assert(src.Length > 0);
        for (; ; )
        {
            int cb = await EnsureSpaceAsync(ct);
            if (src.Length <= cb)
                break;
            WriteToBlock(src.Slice(0, cb).Span);
            src = src.Slice(cb);
        }
        WriteToBlock(src.Span);
    }
}

// This partial contains members that are affected by seekability. These should be overridden
// when seeking is supported.
partial class BlockWriteStream
{
    public override bool CanSeek => false;

    public override long Length => Position;

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public sealed override void SetLength(long value)
    {
        // REVIEW: Should this be implemented for the seeking case?
        throw new NotSupportedException();
    }
}

// This partial contains the read members (not supported).
partial class BlockWriteStream
{
    public sealed override bool CanRead => false;

    public sealed override int ReadByte()
    {
        throw new NotSupportedException();
    }

    public sealed override int Read(Span<byte> dst)
    {
        throw new NotSupportedException();
    }

    public sealed override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public sealed override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public sealed override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        throw new NotSupportedException();
    }
}

// This partial contains the block definition.
partial class BlockWriteStream
{
    /// <summary>
    /// A block. The subclass may maintain a bunch of these. This base class maintains only one,
    /// the "current" block.
    /// </summary>
    protected sealed class Block : IDisposable
    {
        private readonly int _cap;
        private byte[]? _data;

        // The size of this block. If this block is at the end of the stream, this size
        // will increase as the block is filled to capacity.
        private int _size;

        // The "dirty" range with the block. This can be used to optimize sending changes
        // of the block to the ultimate storage.
        private int _minDirty;
        private int _limDirty;

        // The starting position of this block in the whole stream.
        private long _pos;

        /// <summary>
        /// The capacity of this block. This doesn't change.
        /// </summary>
        public int Capacity => _cap;

        /// <summary>
        /// The (current) position of the start of this block in the whole stream.
        /// </summary>
        public long Pos => _pos;

        /// <summary>
        /// The limit position of this block in the whole stream based on <see cref="Pos"/>
        /// and <see cref="Capacity"/>.
        /// </summary>
        public long PosCap => _pos + _cap;

        /// <summary>
        /// The current size of this block.
        /// </summary>
        public int Size => _size;

        /// <summary>
        /// The limit position of this block in the whole stream based on <see cref="Pos"/>
        /// and <see cref="Size"/>.
        /// </summary>
        public long PosLim => _pos + _size;

        /// <summary>
        /// The current dirty range in this block. Note that this is considered empty if the
        /// min is larger than the lim.
        /// </summary>
        public (int min, int lim) DirtyRange => (_minDirty, _limDirty);

        public Block(int cap)
        {
            Validation.Assert(cap > 0);
            _cap = cap;
            _data = ArrayPool<byte>.Shared.Rent(_cap);
        }

        public void Dispose()
        {
            var data = Interlocked.Exchange(ref _data, null);
            if (data != null)
                ArrayPool<byte>.Shared.Return(data);
        }

        /// <summary>
        /// This sets the block's position and size. It also marks everything as clean.
        /// </summary>
        public void Reset(long pos, int size)
        {
            Validation.Assert(pos >= 0);
            Validation.Assert(pos + _cap > pos);
            Validation.AssertIndexInclusive(size, _cap);

            _pos = pos;
            _size = size;
            Clean();
        }

        /// <summary>
        /// This marks everything as clean (not dirty).
        /// </summary>
        public void Clean()
        {
            _minDirty = _size;
            _limDirty = 0;
        }

        /// <summary>
        /// Whether any contents of the block is dirty (hasn't been written to the ultimate storage).
        /// </summary>
        public bool IsDirty => _minDirty < _limDirty;

        /// <summary>
        /// Marks the given range as dirty. The range must start at or before the current end of the block.
        /// This updates the size of the block if the range extends beyond it.
        /// </summary>
        public void Dirty(int min, int lim)
        {
            Validation.AssertIndexInclusive(min, _size);
            Validation.AssertInRange(lim - 1, min, _cap);
            if (_minDirty > min)
                _minDirty = min;
            if (_limDirty < lim)
            {
                _limDirty = lim;
                if (_size < _limDirty)
                    _size = _limDirty;
            }
            Validation.AssertIndexInclusive(_limDirty, _size);
        }

        /// <summary>
        /// Get a span for reading. The range must be within the current size of the block.
        /// </summary>
        public ReadOnlySpan<byte> ForRead(int ib, int cb)
        {
            Validation.AssertIndex(ib, _cap);
            Validation.Assert(cb <= _cap - ib);
            return _data.AsSpan(ib, cb);
        }

        /// <summary>
        /// Get a memory stream for reading. The range must be within the current size of the block.
        /// </summary>
        public MemoryStream StreamForRead(int ib, int cb)
        {
            Validation.AssertIndex(ib, _cap);
            Validation.Assert(cb <= _cap - ib);
            return new MemoryStream(_data!, ib, cb, writable: false);
        }

        /// <summary>
        /// Get a span for writing. The range must start at or before the current end of the block.
        /// This updates the size of the block if the range extends beyond it.
        /// </summary>
        public Span<byte> ForWrite(int ib, int cb)
        {
            Validation.AssertIndex(ib, _cap);
            Validation.Assert(cb <= _cap - ib);
            Dirty(ib, ib + cb);
            return _data.AsSpan(ib, cb);
        }

        /// <summary>
        /// Get a memory for writing. The range must start at or before the current end of the block.
        /// This updates the size of the block if the range extends beyond it.
        /// </summary>
        public Memory<byte> MemForWrite(int ib, int cb)
        {
            Validation.AssertIndex(ib, _cap);
            Validation.Assert(cb <= _cap - ib);
            Dirty(ib, ib + cb);
            return _data.AsMemory(ib, cb);
        }
    }
}

/// <summary>
/// Implements a seekable writable stream where the actual storage is updated at a block level, for example,
/// with an azure file. Typically, the storage is "remote" with block writes being preferred over small writes.
/// Note that this does not (by default) keep an open stream on the remote storage. It maintains a cache of
/// blocks which may or may not be "dirty". The maximum number of blocks and their size is specified in the
/// constructor. The blocks in the cache are kept in a linked list, but it's best to keep the maximum number
/// of blocks smallish. The default is 16.
/// </summary>
public abstract partial class BlockWriteSeekStream : BlockWriteStream
{
    protected const int DefaultBlockCount = 1 << 4;

    private sealed class Node
    {
        public readonly Block Block;
        public Node? Prev;
        public Node? Next;

        public Node(Block block)
        {
            Validation.AssertValue(block);
            Block = block;
        }
    }

    private readonly int _cnodeLim;
    private Node? _head;
    private Node? _tail;
    private int _cnode;

    /// <summary>
    /// The limit of the blocks handled so far. This does not include the current block.
    /// </summary>
    private long _posLim;

    protected BlockWriteSeekStream(int numBlock = DefaultBlockCount, int capBlock = DefaultBlockCapacity)
        : base(capBlock)
    {
        Validation.Assert(numBlock >= 0);
        _cnodeLim = numBlock;
    }

    [Conditional("DEBUG")]
    private void AssertValid(Block block)
    {
#if DEBUG
        Validation.AssertIndexInclusive(_cnode, _cnodeLim);
        Validation.AssertValue(block);
        Validation.Assert(block.Pos % block.Capacity == 0);

        int cap = block.Capacity;
        long len = Length;

        int cnode = 0;
        var node = _head;
        Node? nodePrev = null;
        while (node != null)
        {
            Validation.Assert(cnode < _cnode);
            Validation.Assert(nodePrev == node.Prev);
            cnode++;
            var b = node.Block;

            // The capacities should all the be same and the starting positions should be divisible
            // by capacity.
            Validation.Assert(b.Capacity == cap);
            Validation.Assert(b.Pos % cap == 0);

            // If this isn't the last block, it should be full.
            Validation.Assert(b.PosLim <= _posLim);
            if (b.PosLim < len)
                Validation.Assert(b.Size == cap);

            // The conditions above imply that we only need to test positions to guarantee that the
            // blocks don't overlap.
            Validation.Assert(b.Pos != block.Pos);
            for (var n = _head; n != node; n = n.Next)
            {
                Validation.AssertValue(n);
                Validation.Assert(n.Block.Pos != b.Pos);
            }

            nodePrev = node;
            node = node.Next;
        }
        Validation.Assert(_cnode == cnode);
        Validation.Assert(_tail == nodePrev);
#endif
    }

    public sealed override bool CanSeek => true;

    public sealed override long Length => Math.Max(_posLim, BlockCur.PosLim);

    public sealed override long Seek(long offset, SeekOrigin origin)
    {
        long posCur = Position;

        long posDst;
        switch (origin)
        {
        case SeekOrigin.Begin:
            posDst = offset;
            break;
        case SeekOrigin.Current:
            posDst = posCur + offset;
            break;
        case SeekOrigin.End:
            posDst = Length + offset;
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(origin));
        }

        if ((ulong)posDst > (ulong)Length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        SetPositionCore(posDst);
        return posDst;
    }

    protected abstract void PushBlock(Block block);

    protected abstract ValueTask PushBlockAsync(Block block, CancellationToken ct);

    protected abstract void FetchBlock(long pos, Span<byte> buf);

    protected abstract ValueTask FetchBlockAsync(long pos, Memory<byte> buf, CancellationToken ct);

    private void AppendNode(Block block)
    {
        var node = new Node(block);
        if (_tail != null)
        {
            Validation.Assert(_head != null);
            Validation.Assert(_tail.Next == null);
            _tail.Next = node;
        }
        else
        {
            Validation.Assert(_head == null);
            _head = node;
        }
        node.Prev = _tail;
        _tail = node;

        _cnode++;
    }

    private void RemoveNode(Node node)
    {
        Validation.Assert(_cnode > 0);
        _cnode--;

        if (node.Prev != null)
            node.Prev.Next = node.Next;
        else
        {
            Validation.Assert(_head == node);
            _head = node.Next;
        }

        if (node.Next != null)
            node.Next.Prev = node.Prev;
        else
        {
            Validation.Assert(_tail == node);
            _tail = node.Prev;
        }
    }

    /// <summary>
    /// Return whether <paramref name="a"/> is a better candidate to boot from the cache
    /// than <paramref name="b"/> is.
    /// </summary>
    private static bool IsBetter(Node a, Node? b)
    {
        Validation.AssertValue(a);
        if (b == null)
            return true;
        if (a.Block.IsDirty == b.Block.IsDirty)
            return a.Block.Pos < b.Block.Pos;
        if (b.Block.IsDirty)
            return true;
        return false;
    }

    /// <summary>
    /// Look for the desired block. Look in reverse order since that is MRU order.
    /// If the block is not found, <paramref name="boot"/> will be set to the best
    /// candidate node to boot out of the cache. Non-dirty are better (to boot) than
    /// dirty and smaller positions are better than larger.
    /// </summary>
    private bool TryFindBlock(long pos, [NotNullWhen(true)] out Block? block, out Node? boot)
    {
        Node? best = null;
        for (var node = _tail; node != null; node = node.Prev)
        {
            if (node.Block.Pos == pos)
            {
                block = node.Block;
                RemoveNode(node);
                boot = null;
                return true;
            }

            if (IsBetter(node, best))
                best = node;
        }
        block = null;
        boot = best;
        return false;
    }

    protected sealed override Block GetBlock(long pos, Block block)
    {
        Validation.Assert(pos == Position);
        Validation.AssertIndexInclusive(pos, Length);
        AssertValid(block);

        int cap = block.Capacity;
        pos -= pos % cap;
        int cb = (int)Math.Min(Length - pos, cap);

        if (_posLim < block.PosLim)
            _posLim = block.PosLim;

        Block use;
        if (_cnodeLim == 0)
        {
            if (block.IsDirty)
                PushBlock(block);
            use = block;
        }
        else
        {
            if (TryFindBlock(pos, out var b, out var boot))
            {
                AppendNode(block);
                AssertValid(b);
                return b;
            }

            Block? free = null;
            if (_cnode >= _cnodeLim)
            {
                Validation.Assert(_cnode == _cnodeLim);
                Validation.AssertValue(boot);
                if (boot.Block.IsDirty)
                    PushBlock(boot.Block);

                // Remove the node after pushing in case pushing throws.
                RemoveNode(boot);
                free = boot.Block;
            }
            use = free ?? new Block(cap);
        }

        use.Reset(pos, cb);
        if (cb > 0)
            FetchBlock(pos, use.ForWrite(0, cb));
        use.Reset(pos, cb);

        // Append block right before returning to avoid issues if something above throws.
        if (use != block)
            AppendNode(block);
        AssertValid(use);
        return use;
    }

    protected sealed override async ValueTask<Block> GetBlockAsync(long pos, Block block, CancellationToken ct)
    {
        Validation.Assert(pos == Position);
        Validation.AssertIndexInclusive(pos, Length);
        AssertValid(block);

        int cap = block.Capacity;
        pos -= pos % cap;
        int cb = (int)Math.Min(Length - pos, cap);

        if (_posLim < block.PosLim)
            _posLim = block.PosLim;

        Block use;
        if (_cnodeLim == 0)
        {
            if (block.IsDirty)
                await PushBlockAsync(block, ct).ConfigureAwait(false);
            use = block;
        }
        else
        {
            AppendNode(block);
            if (TryFindBlock(pos, out var b, out var boot))
                return b;

            Block? free = null;
            if (_cnode > _cnodeLim)
            {
                Validation.Assert(_cnode == _cnodeLim + 1);
                Validation.AssertValue(boot);
                RemoveNode(boot);
                if (boot.Block.IsDirty)
                    await PushBlockAsync(boot.Block, ct).ConfigureAwait(false);
                free = boot.Block;
            }
            use = free ?? new Block(cap);
        }

        use.Reset(pos, cb);
        if (cb > 0)
            await FetchBlockAsync(pos, use.MemForWrite(0, cb), ct).ConfigureAwait(false);
        use.Reset(pos, cb);
        return use;
    }

    private List<Block>? SortDirtyBlocks(ref Block? block)
    {
        List<Block>? blocks = null;
        for (var node = _head; node != null; node = node.Next)
        {
            if (node.Block.IsDirty)
                Util.Add(ref blocks, node.Block);
        }

        if (block != null && block.IsDirty)
        {
            if (blocks == null)
                return null;
            blocks.Add(block);
        }
        block = null;

        if (blocks?.Count > 1)
            blocks.Sort((a, b) => Math.Sign(a.Pos - b.Pos));
        return blocks;
    }

    protected sealed override void FlushCore(Block block, bool force)
    {
        AssertValid(block);
        if (!force)
            return;

        Block? extra = block;
        var blocks = SortDirtyBlocks(ref extra);

        if (blocks != null)
        {
            Validation.Assert(extra == null);
            foreach (var b in blocks)
                PushBlock(b);
        }
        else if (extra != null)
            PushBlock(extra);
    }

    protected sealed override ValueTask FlushCoreAsync(Block block, bool force, CancellationToken ct)
    {
        AssertValid(block);
        if (!force)
            return ValueTask.CompletedTask;

        Block? extra = block;
        var blocks = SortDirtyBlocks(ref extra);

        if (blocks != null)
        {
            Validation.Assert(extra == null);
            return PushBlocksAsync(blocks, ct);
        }
        else if (extra != null)
            return PushBlockAsync(extra, ct);

        return ValueTask.CompletedTask;
    }

    private async ValueTask PushBlocksAsync(List<Block> blocks, CancellationToken ct)
    {
        foreach (var b in blocks)
            await PushBlockAsync(b, ct).ConfigureAwait(false);
    }
}
