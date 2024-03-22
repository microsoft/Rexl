// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Statement;

// This partial handles suspend/resume, that is, serialization and suspension of the
// interpreter state as well as deserialization and resumption.
partial class StmtInterp
{
    private const uint k_sigInterp = 0x43497352; // RsIC.
    private const uint k_endInterp = 0x52734943; // CIsR.

    // The current version number. This should be changed when the format changes.
    private const uint k_verCur = 0x00010001;
    // Readers back to this can still read this version.
    private const uint k_verBack = 0x00010001;
    // This code can read back to this version.
    private const uint k_verRead = 0x00010001;

    /// <summary>
    /// Statement handling code can throw this exception to trigger a suspension.
    /// </summary>
    public abstract class SuspendException : ApplicationException
    {
        protected SuspendException(string msg)
            : base(msg)
        {
        }
    }

    /// <summary>
    /// Statement handling code can throw this exception to trigger a suspension. The exception
    /// contains a <see cref="Cookie"/> value that can hold extra information.
    /// </summary>
    public sealed class SuspendException<T> : SuspendException
    {
        public T Cookie { get; }

        public SuspendException(T cookie, string msg = null)
            : base(msg ?? "Suspending interpreter")
        {
            Cookie = cookie;
        }
    }

    /// <summary>
    /// Throws a <see cref="SuspendExecution"/> exception to trigger suspending execution.
    /// </summary>
    [DoesNotReturn]
    public virtual SuspendException Suspend<T>(T cookie, string msg = null)
    {
        Validation.BugCheck(IsActive, "Can't suspend inactive interpreter");
        throw new StmtInterp.SuspendException<T>(cookie, msg);
    }

    /// <summary>
    /// The default implementation calls <see cref="TryGetStreamForSuspend(out Stream)"/>. If that returns
    /// false, this just returns <c>null</c>. Otherwise, it calls <see cref="WriteState(Stream)"/> to write
    /// the current state into the stream and returns the stream.
    /// </summary>
    protected virtual Stream GetSuspendState(SuspendException ex)
    {
        Validation.BugCheckValueOrNull(ex);
        return GetSuspendStateCore();
    }

    /// <summary>
    /// Calls <see cref="TryGetStreamForSuspend(out Stream)"/>. If that returns false, this just returns
    /// <c>null</c>. Otherwise, it calls <see cref="WriteState(Stream)"/> to write the current state into
    /// the stream and returns the stream.
    /// </summary>
    protected Stream GetSuspendStateCore()
    {
        Validation.BugCheck(IsActive, "Can't suspend an inactive interpreter");

        if (!TryGetStreamForSuspend(out var strm))
            return null;
        WriteState(strm);
        return strm;
    }

    /// <summary>
    /// Try to get a stream for writing suspend state. If this fails, the state is not captured, but
    /// execution is still exited.
    /// </summary>
    protected abstract bool TryGetStreamForSuspend(out Stream strm);

    /// <summary>
    /// Writes the state needed to be able to later resume. This is normally called in order to
    /// suspend or check-point script execution. Note that an override can write additional information
    /// before and/or after the core state written here.
    /// </summary>
    public virtual void WriteState(Stream strm)
    {
        Validation.BugCheck(IsActive, "No need to save state of an inactive interpreter");
        Validation.BugCheckValue(strm, nameof(strm));

        Validation.Assert(!_scrCur.IsDone);
        Validation.Assert(_scrCur.BlockDepthCur == BlockDepth);

        // Version number should be bumped (and encoding may need to change) if these asserts fire.
        Validation.Assert(!((LinkKind)0x00).IsValid());
        Validation.Assert(!((LinkKind)0x07).IsValid());
        Validation.Assert(!((LinkKind)0xFF).IsValid());
        Validation.Assert(((LinkKind)0x01).IsValid());
        Validation.Assert(((LinkKind)0x06).IsValid());

        using var wrt = new BinaryWriter(strm, Util.StdUTF8, leaveOpen: true);

        wrt.Write(k_sigInterp);
        wrt.Write(k_verCur);
        wrt.Write(k_verBack);

        Validation.Assert(_scrCur.Depth > 0);
        wrt.Write(_scrCur.Depth);
        wrt.Write(_blkCur.Depth);

        // Write the script frames in bottom to top (outer to inner) order.
        var scrs = new ScriptFrame[_scrCur.Depth];
        for (var scr = _scrCur; scr.Depth > 0; scr = scr.Parent)
        {
            Validation.Assert(scr.Parent != null);
            Validation.Assert(scr.Depth == scr.Parent.Depth + 1);
            int index = scr.Depth - 1;
            Validation.Assert(scrs[index] == null);
            scrs[index] = scr;
        }
        for (int i = 0; i < scrs.Length; i++)
        {
            Validation.Assert(scrs[i] != null);
            Validation.Assert(scrs[i].Depth == i + 1);
            scrs[i].Write(wrt);
        }

        // Write the block frames in bottom to top (outer to inner) order.
        var blks = new BlockFrame[_blkCur.Depth];
        for (var blk = _blkCur; blk.Depth > 0; blk = blk.Parent)
        {
            Validation.Assert(blk.Parent != null);
            Validation.Assert(blk.Depth == blk.Parent.Depth + 1);
            int index = blk.Depth - 1;
            Validation.Assert(blks[index] == null);
            blks[index] = blk;
        }
        for (int i = 0; i < blks.Length; i++)
        {
            Validation.Assert(blks[i] != null);
            Validation.Assert(blks[i].Depth == i + 1);
            blks[i].Write(wrt);
        }

        wrt.Write(k_endInterp);
    }

    /// <summary>
    /// Reads interpreter state from the stream. Throws if the stream information is bad.
    /// Sets the new state of this interpreter to the deserialized state. Note that this
    /// requires the interpreter to currently be inactive.
    /// </summary>
    private void ReadState(Stream strm)
    {
        Validation.Assert(!IsActive, "Can't read state into an active interpreter");
        Validation.AssertValue(strm);

        using var rdr = new BinaryReader(strm, Util.StdUTF8, leaveOpen: true);

        Validation.Assert(_scrCur.Parent == null);
        Validation.Assert(_blkCur.Parent == null);

        // Read and check header information.
        uint sig = rdr.ReadUInt32();
        CheckRead(sig == k_sigInterp, "Bad signature");
        var ver = rdr.ReadUInt32();
        CheckRead(ver >= k_verRead, "Can't read old version");

        // Read the "back" version and validate it.
        uint verBack = rdr.ReadUInt32();
        CheckRead(verBack <= ver, "Inconsistent back version");
        CheckRead(verBack <= k_verCur, "New version not readable by this code");

        int cscr = rdr.ReadInt32();
        CheckRead(cscr > 0, "No scripts in state");
        int cblk = rdr.ReadInt32();
        CheckRead(cblk >= 0, "Bad block count");

        var scr = _scrCur;
        for (int i = 0; i < cscr; i++)
        {
            scr = new ScriptFrame(rdr, scr);
            Validation.Assert(!scr.IsDone);
            CheckRead(scr.BlockDepthCur <= cblk, "Bad script current block depth");
        }
        Validation.Assert(scr.Depth == cscr);
        CheckRead(scr.BlockDepthCur == cblk, "Bad script final block depth");

        var blk = _blkCur;
        for (int i = 0; i < cblk; i++)
            blk = new BlockFrame(rdr, blk);
        Validation.Assert(blk.Depth == cblk);

        uint end = rdr.ReadUInt32();
        CheckRead(end == k_endInterp, "Bad end");

        // Update the state.
        _scrCur = scr;
        _blkCur = blk;
        Validation.Assert(IsActive);
    }

    /// <summary>
    /// Resumes a previously suspended execution.
    /// </summary>
    public Task<(bool success, Stream suspendState)> ResumeAsync(Stream strm, Action<StmtInterp, Stream> callback = null)
    {
        Validation.BugCheck(!IsActive, nameof(ResumeAsync) + " is not re-entrant!");
        Validation.BugCheckValue(strm, nameof(strm));
        Validation.BugCheckValueOrNull(callback);

        ReadState(strm);
        return LoopAsync(callback, strm);
    }

    /// <summary>
    /// Throws an exception if the condition is false.
    /// </summary>
    private static void CheckRead(bool cond, string msg)
    {
        if (!cond)
            throw new IOException(msg ?? "Bad encoded interpreter state");
    }

    /// <summary>
    /// Reads a boolean. Unfortunately, the standard method doesn't test for either 0/1. It treats any
    /// non-zero as true.
    /// </summary>
    private static bool ReadBool(BinaryReader rdr)
    {
        byte b = rdr.ReadByte();
        CheckRead(b <= 1, "Bad bool");
        return b != 0;
    }

    partial class ScriptFrame
    {
        public void Write(BinaryWriter wrt)
        {
            Validation.AssertValue(wrt);
            Validation.Assert(Parent != null);
            Validation.Assert(!IsDone);
            Validation.Assert(BlockDepthEnter == Parent.BlockDepthCur);

            wrt.Write(Depth);
            wrt.Write(Recover);
            wrt.Write(WithBlock);
            wrt.Write(BlockDepthEnter);

            // Write the instruction count (as a sanity check).
            // REVIEW: Should we also write the instruction kinds and depths? We can't really write
            // the entire instructions - better to just reproduce them from the source code.
            wrt.Write(Count);

            // Write the current position.
            Validation.AssertIndex(Pos, Count);
            wrt.Write(Pos);

            WriteSource(wrt, Source);
        }

        /// <summary>
        /// This ctor corresponds to the <see cref="Write(BinaryWriter)"/> method.
        /// </summary>
        internal ScriptFrame(BinaryReader rdr, ScriptFrame parent)
        {
            Validation.AssertValue(rdr);
            Validation.AssertValue(parent);

            int depth = rdr.ReadInt32();
            CheckRead(depth == parent.Depth + 1, "Bad script depth");
            bool recover = ReadBool(rdr);
            bool withBlock = ReadBool(rdr);
            int blockDepthEnter = rdr.ReadInt32();
            CheckRead(parent.BlockDepthCur == blockDepthEnter, "Bad script enter depth");

            int count = rdr.ReadInt32();
            int pos = rdr.ReadInt32();
            CheckRead(0 <= pos & pos < count, "Bad instruction index");

            var source = ReadSource(rdr);
            var rsl = RexlStmtList.Create(source);
            CheckRead(!rsl.HasErrors, "Parse errors");

            var flow = StmtFlow.Create(rsl);
            CheckRead(!flow.HasErrors, "Flow errors");
            CheckRead(count == flow.Instructions.Length, "Bad instruction count");

            Parent = parent;
            Root = parent.Root ?? this;
            Depth = parent.Depth + 1;
            Flow = flow;
            Source = source;
            Recover = recover;
            WithBlock = withBlock;
            BlockDepthEnter = blockDepthEnter;
            BlockDepthBase = BlockDepthEnter + withBlock.ToNum();
            Pos = pos;

            _insts = flow.Instructions;
        }

        private static void WriteSource(BinaryWriter wrt, SourceContext source)
        {
            Validation.AssertValue(wrt);
            Validation.AssertValue(source);

            WriteLink(wrt, source.LinkCtx, null);
            WriteLink(wrt, source.LinkFull, source.LinkCtx);
            wrt.Write(source.PathTail ?? "");
            wrt.Write(source.Text);
        }

        /// <summary>
        /// Corresponds to <see cref="WriteSource(BinaryWriter, SourceContext)"/>.
        /// </summary>
        private static SourceContext ReadSource(BinaryReader rdr)
        {
            Validation.AssertValue(rdr);

            Link linkCtx = ReadLink(rdr, null);
            Link linkFull = ReadLink(rdr, linkCtx);
            var tail = rdr.ReadString();
            var text = rdr.ReadString();
            return SourceContext.Create(linkFull, tail, text);
        }

        /// <summary>
        /// Writes the <paramref name="link"/> with an optimization if it matches <paramref name="linkBase"/>.
        /// </summary>
        private static void WriteLink(BinaryWriter wrt, Link link, Link linkBase)
        {
            Validation.AssertValue(wrt);
            Validation.AssertValueOrNull(linkBase);

            if (link == null)
                wrt.Write((byte)0);
            else if (link.IsSame(linkBase))
            {
                var key = ~(LinkKind)0;
                Validation.Assert(!key.IsValid());
                wrt.Write((byte)key);
            }
            else
            {
                Validation.Assert(link.Kind != 0);
                Validation.Assert(link.Kind != ~(LinkKind)0);
                wrt.Write((byte)link.Kind);
                if (link.Kind.NeedsAccount())
                    wrt.Write(link.AccountId);
                wrt.Write(link.Path);
            }
        }

        /// <summary>
        /// Reads a link, using <paramref name="linkBase"/> if the serialized form indicates that it
        /// should be used.
        /// </summary>
        private static Link ReadLink(BinaryReader rdr, Link linkBase)
        {
            Validation.AssertValue(rdr);
            Validation.AssertValueOrNull(linkBase);

            var kind = (LinkKind)rdr.ReadByte();
            if (kind == 0)
                return null;

            if (kind == ~(LinkKind)0)
            {
                CheckRead(linkBase != null, "Base link is null");
                return linkBase;
            }

            CheckRead(kind.IsValid(), "Invalid link kind");
            var acct = kind.NeedsAccount() ? rdr.ReadString() : null;
            var path = rdr.ReadString();
            return Link.Create(kind, acct, path);
        }
    }

    partial class BlockFrame
    {
        public void Write(BinaryWriter wrt)
        {
            AssertValid();
            Validation.AssertValue(wrt);
            Validation.Assert(Parent != null);
            Validation.Assert(Depth == Parent.Depth + 1);

            wrt.Write(Depth);
            wrt.Write(IsRoot);

            WritePathBased(wrt, NsRoot, Parent.NsRoot);
            WritePathBased(wrt, NsBlock, Parent.NsBlock);
            WritePathBased(wrt, NsCur, NsBlock);

            if (With == null)
                wrt.Write(0);
            else if (With.Next == null)
            {
                wrt.Write(1);
                Validation.Assert(With.Path.StartsWith(NsRoot));
                WritePathBased(wrt, With.Path, NsBlock);
            }
            else
            {
                var paths = new List<NPath>();
                for (var with = With; with != null; with = with.Next)
                {
                    Validation.Assert(with.Path.StartsWith(NsRoot));
                    paths.Add(with.Path);
                }
                wrt.Write(paths.Count);
                for (int i = paths.Count; --i >= 0;)
                    WritePathBased(wrt, paths[i], NsBlock);
            }
        }

        /// <summary>
        /// This constructor corresponds to the <see cref="Write(BinaryWriter)"/> method.
        /// </summary>
        public BlockFrame(BinaryReader rdr, BlockFrame parent)
        {
            Validation.AssertValue(rdr);
            Validation.AssertValue(parent);
            parent.AssertValid();

            int depth = rdr.ReadInt32();
            CheckRead(depth == parent.Depth + 1, "Bad block depth");

            bool isRoot = ReadBool(rdr);
            var nsRoot = ReadPathBased(rdr, parent.NsRoot);
            var nsBlock = ReadPathBased(rdr, parent.NsBlock);
            CheckRead(nsBlock.StartsWith(nsRoot), "Bad block namespace");
            var nsCur = ReadPathBased(rdr, nsBlock);
            CheckRead(nsCur.StartsWith(nsBlock), "Bad current namespace");

            WithNode with = null;
            int cwith = rdr.ReadInt32();
            CheckRead(cwith >= 0, "Bad 'with' count");
            for (int i = 0; i < cwith; i++)
            {
                var path = ReadPathBased(rdr, nsBlock);
                CheckRead(path.StartsWith(nsRoot), "Bad 'with' namespace");
                with = new WithNode(with, path);
            }

            Parent = parent;
            Depth = parent.Depth + 1;

            NsRoot = nsRoot;
            NsBlock = nsBlock;
            NsCur = nsCur;
            NsRel = NsRoot.IsRoot ? NsCur : NPath.Root.AppendPartial(NsCur, nsRoot.NameCount);
            With = with;

            AssertValid();
        }

        /// <summary>
        /// Writes <paramref name="path"/> with an optimization if it matches or extends
        /// <paramref name="pathBase"/>.
        /// </summary>
        private static void WritePathBased(BinaryWriter wrt, NPath path, NPath pathBase)
        {
            Validation.AssertValue(wrt);

            if (path.IsRoot)
            {
                // Empty.
                wrt.Write((byte)0x00);
            }
            else if (path == pathBase)
            {
                // Equal to base.
                wrt.Write((byte)0x01);
            }
            else if (!pathBase.IsRoot && path.StartsWith(pathBase))
            {
                // Extends base.
                Validation.Assert(pathBase.NameCount > 0);
                Validation.Assert(path.NameCount > pathBase.NameCount);
                wrt.Write((byte)0x02);
                WritePath(wrt, path, pathBase.NameCount);
            }
            else
            {
                // Doesn't use base.
                wrt.Write((byte)0xFF);
                WritePath(wrt, path, 0);
            }
        }

        /// <summary>
        /// Corresponds to <see cref="WritePathBased(BinaryWriter, NPath, NPath)"/>.
        /// </summary>
        private static NPath ReadPathBased(BinaryReader rdr, NPath pathBase)
        {
            Validation.AssertValue(rdr);

            byte key = rdr.ReadByte();
            if (key == (byte)0x00)
            {
                // Empty.
                return default;
            }

            if (key == (byte)0x01)
            {
                // Equal to base.
                CheckRead(pathBase.NameCount > 0, "Bad base name count");
                return pathBase;
            }

            if (key == (byte)0x02)
            {
                // Extends base.
                CheckRead(pathBase.NameCount > 0, "Root base path");
                return ReadPath(rdr, pathBase);
            }

            // Doesn't use base.
            CheckRead(key == (byte)0xFF, "Unexpected path kind");
            return ReadPath(rdr, default);
        }

        private static void WritePath(BinaryWriter wrt, NPath path, int start)
        {
            Validation.AssertValue(wrt);
            Validation.Assert(0 <= start & start < path.NameCount);

            int num = path.NameCount - start;
            Validation.Assert(num > 0);
            wrt.Write(num);

            if (num <= 2)
            {
                if (num == 2)
                    wrt.Write(path.Parent.Leaf.Value);
                wrt.Write(path.Leaf.Value);
                return;
            }

            var names = path.ToNames(start);
            Validation.Assert(names.Count == path.NameCount - start);
            for (int i = 0; i < names.Count; i++)
                wrt.Write(names[i].Value);
        }

        private static NPath ReadPath(BinaryReader rdr, NPath pathBase)
        {
            Validation.AssertValue(rdr);

            int count = rdr.ReadInt32();
            CheckRead(count > 0, "Bad name count");
            NPath path = pathBase;
            for (int i = 0; i < count; i++)
            {
                var str = rdr.ReadString();
                CheckRead(DName.TryWrap(str, out var name), "Bad name in path");
                path = path.Append(name);
            }
            Validation.Assert(path.NameCount == count + pathBase.NameCount);

            return path;
        }
    }
}
