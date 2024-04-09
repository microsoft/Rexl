// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Harness;

partial class SimpleHarnessBase
{
    /// <summary>
    /// Storage abstraction to read/write file streams from/to specified location.
    /// The interpretation of the specified location is up to the implementation.
    /// </summary>
    public abstract class Storage
    {
        protected Storage()
        {
        }

        /// <summary>
        /// Open a data stream from a <see cref="Link"/>.
        /// The optional <paramref name="linkCtx"/> value is for resolving relative paths.
        /// The returned <see cref="Link"/> value is the resolved "full path" link.
        /// Throws an exception (typically an <see cref="IOException"/>) on failure.
        /// </summary>
        public abstract (Link full, Stream stream) LoadStream(Link linkCtx, Link link);

        /// <summary>
        /// Open a data stream from a <see cref="Link"/>.
        /// The optional <paramref name="linkCtx"/> value is for resolving relative paths.
        /// The returned <see cref="Link"/> value is the resolved "full path" link.
        /// Throws an exception (typically an <see cref="IOException"/>) on failure.
        /// </summary>
        public virtual Task<(Link full, Stream stream)> LoadStreamAsync(Link linkCtx, Link link)
        {
            return Task.FromResult(LoadStream(linkCtx, link));
        }

        /// <summary>
        /// Create a data stream given the intended location as <paramref name="link"/>.
        /// The optional <paramref name="linkCtx"/> value is for resolving relative paths.
        /// The returned <see cref="Link"/> value is the resolved "full path" link.
        /// Throws an exception (typically an <see cref="IOException"/>) on failure.
        /// </summary>
        public abstract Task<(Link full, Stream stream)> CreateStreamAsync(Link linkCtx, Link link,
            StreamOptions options = default);

        /// <summary>
        /// Get the files contained in the directory indicated by the <paramref name="link"/> value. If it is
        /// null, the "current directory" is assumed.
        /// The returned <see cref="Link"/> value is the resolved "full path" link.
        /// </summary>
        public abstract (Link, IEnumerable<Link>) GetFiles(Link linkCtx, Link link);
    }

    /// <summary>
    /// Storage abstraction providing default methods to read/write file streams
    /// from/to the local file system and http.
    /// </summary>
    public abstract class LocalFileStorage : Storage
    {
        private readonly HttpClient _http;

        protected LocalFileStorage()
        {
            _http = new HttpClient();
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; Rexl/1.0)");
        }

        /// <summary>
        /// Called to resolve a parent path (<paramref name="linkPar"/>) and <paramref name="path"/> into
        /// the full path of an existing file that can be opened or where that file should be reported
        /// as "missing". If <paramref name="path"/> is null or empty, this returns false with
        /// <paramref name="pathFull"/> set to <paramref name="path"/>.
        /// 
        /// The default implementation forwards to <see cref="TryFindLocalFile(string, string, out string)"/>
        /// to look for a local file.
        /// </summary>
        protected virtual bool TryFindFile(Link linkPar, string path, out string pathFull)
        {
            Validation.BugCheckValueOrNull(linkPar);
            return TryFindLocalFile(linkPar.GetLocalPath(), path, out pathFull);
        }

        /// <summary>
        /// Called to resolve a parent path (<paramref name="pathPar"/>) and <paramref name="path"/> into
        /// the full path of an existing file that can be opened or where that file should be reported
        /// as "missing". If <paramref name="path"/> is null or empty, this returns false with
        /// <paramref name="pathFull"/> set to <paramref name="path"/>.
        /// 
        /// The default implementation forwards to <see cref="TryFindLocalFile(string, string, out string)"/>
        /// to look for a local file.
        /// </summary>
        protected virtual bool TryFindFile(string pathPar, string path, out string pathFull)
        {
            return TryFindLocalFile(pathPar, path, out pathFull);
        }

        /// <summary>
        /// Looks for an exsting local file.
        /// </summary>
        protected virtual bool TryFindLocalFile(string pathPar, string path, out string pathFull)
        {
            Validation.BugCheckValueOrNull(pathPar);
            Validation.BugCheckValueOrNull(path);

            if (string.IsNullOrEmpty(path))
            {
                pathFull = path;
                return false;
            }

            if (Path.IsPathRooted(path))
            {
                pathFull = Path.GetFullPath(path);
                return File.Exists(pathFull);
            }

            string dir;
            if (string.IsNullOrEmpty(pathPar))
                dir = Directory.GetCurrentDirectory();
            else
            {
                dir = Path.GetDirectoryName(pathPar);
                if (string.IsNullOrEmpty(dir))
                    dir = Path.GetPathRoot(pathPar);
            }

            pathFull = Path.GetFullPath(Path.Combine(dir, path));
            if (File.Exists(pathFull))
                return true;

            pathFull = Path.GetFullPath(path);
            if (File.Exists(pathFull))
                return true;

            pathFull = path;
            return false;
        }

        /// <summary>
        /// Called to resolve a parent link (<paramref name="linkPar"/>) and <paramref name="path"/> into
        /// the full path of an existing directory or where that directory should be reported as "missing".
        /// Note that <paramref name="path"/> and/or <paramref name="linkPar"/> may be null or empty.
        /// 
        /// The default implementation delegates to <see cref="TryFindLocalDir(string, string, out string)"/>
        /// to look for a local directory.
        /// </summary>
        protected virtual bool TryFindDir(Link linkPar, string path, out string pathFull)
        {
            Validation.BugCheckValueOrNull(linkPar);
            return TryFindDir(linkPar.GetLocalPath(), path, out pathFull);
        }

        /// <summary>
        /// Called to resolve a parent path (<paramref name="pathPar"/>) and <paramref name="path"/> into
        /// the full path of an existing directory or where that directory should be reported as "missing".
        /// Note that <paramref name="path"/> and/or <paramref name="pathPar"/> may be null or empty.
        /// 
        /// The default implementation delegates to <see cref="TryFindLocalDir(string, string, out string)"/>
        /// to look for a local directory.
        /// </summary>
        protected virtual bool TryFindDir(string pathPar, string path, out string pathFull)
        {
            return TryFindLocalDir(pathPar, path, out pathFull);
        }

        /// <summary>
        /// Looks for an existing local directory.
        /// </summary>
        protected virtual bool TryFindLocalDir(string pathPar, string path, out string pathFull)
        {
            Validation.CheckValueOrNull(pathPar);
            Validation.CheckValueOrNull(path);

            if (Path.IsPathRooted(path))
            {
                pathFull = Path.GetFullPath(path);
                return Directory.Exists(pathFull);
            }

            string dir;
            if (string.IsNullOrEmpty(pathPar))
                dir = Directory.GetCurrentDirectory();
            else
            {
                dir = Path.GetDirectoryName(pathPar);
                if (string.IsNullOrEmpty(dir))
                    dir = Path.GetPathRoot(pathPar);
            }

            pathFull = Path.GetFullPath(Path.Combine(dir, path ?? ""));
            if (Directory.Exists(pathFull))
                return true;

            pathFull = Path.GetFullPath(path ?? "");
            if (Directory.Exists(pathFull))
                return true;

            pathFull = path;
            return false;
        }

        /// <summary>
        /// Resolves file location information to a full form that could be created.
        /// 
        /// The default implementation delegates to <see cref="ResolveNewLocalFile(string, string, out string)"/>.
        /// </summary>
        protected virtual void ResolveNewFile(Link linkPar, string path, out string pathFull)
        {
            Validation.CheckValueOrNull(linkPar);
            ResolveNewFile(linkPar.GetLocalPath(), path, out pathFull);
        }

        /// <summary>
        /// Resolves file location information to a full form that could be created.
        /// 
        /// The default implementation delegates to <see cref="ResolveNewLocalFile(string, string, out string)"/>.
        /// </summary>
        protected virtual void ResolveNewFile(string pathPar, string path, out string pathFull)
        {
            ResolveNewLocalFile(pathPar, path, out pathFull);
        }

        /// <summary>
        /// Resolves local file location information to a full form that could be created.
        /// </summary>
        protected virtual void ResolveNewLocalFile(string pathPar, string path, out string pathFull)
        {
            Validation.CheckValueOrNull(pathPar);
            Validation.CheckValueOrNull(path);

            if (string.IsNullOrEmpty(path))
                throw new IOException("Can't create file with empty name");

            string dir;
            if (Path.IsPathRooted(path))
                pathFull = Path.GetFullPath(path);
            else
            {
                if (string.IsNullOrEmpty(pathPar))
                    dir = Directory.GetCurrentDirectory();
                else
                {
                    dir = Path.GetDirectoryName(pathPar);
                    if (string.IsNullOrEmpty(dir))
                        dir = Path.GetPathRoot(pathPar);
                }

                pathFull = Path.GetFullPath(Path.Combine(dir, path));
            }

            if (Directory.Exists(pathFull))
                throw new IOException($"Path '{pathFull}' is a directory");

            dir = Path.GetDirectoryName(pathFull);
            if (!Directory.Exists(dir))
            {
                try
                {
                    Directory.CreateDirectory(dir);
                }
                catch (IOException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new IOException($"Creating directory '{dir}' failed", ex);
                }
            }
        }

        /// <summary>
        /// Loads a stream for the given link, possibly using the context link to help locate
        /// the stream.
        /// 
        /// The default implementation handles only Generic links and for such, delegates to
        /// <see cref="LoadFileStream(Link, string)"/>.
        /// </summary>
        public override (Link full, Stream stream) LoadStream(Link linkCtx, Link link)
        {
            Validation.CheckValueOrNull(linkCtx);
            Validation.CheckValue(link, nameof(link));

            switch (link.Kind)
            {
            case LinkKind.Generic:
                {
                    var (full, stream) = LoadFileStream(linkCtx, link.Path);
                    Link linkFull = full != link.Path && !string.IsNullOrEmpty(full) ?
                        Link.CreateGeneric(full) : link;
                    return (linkFull, stream);
                }

            case LinkKind.Http:
                using (var request = new HttpRequestMessage(HttpMethod.Get, link.Path))
                {
                    var response = _http.Send(request);
                    try
                    {
                        // Wrap the stream and response so the response gets disposed after the stream.
                        var res = new DisposingStream(response.Content.ReadAsStream(), response);
                        response = null;
                        return (link, res);
                    }
                    finally
                    {
                        response?.Dispose();
                    }
                }

            default:
                throw new NotSupportedException("Link kind not supported");
            }
        }

        /// <summary>
        /// Open a data stream from a file location.
        /// Should throw an <see cref="IOException"/> on failure.
        /// 
        /// The default implementation calls <see cref="TryFindFile(Link, string, out string)"/>
        /// and then delegates to <see cref="LoadLocalFileStream(string)"/>.
        /// </summary>
        protected virtual (string full, Stream stream) LoadFileStream(Link linkCtx, string path)
        {
            Validation.CheckValueOrNull(linkCtx);
            Validation.CheckValueOrNull(path);

            if (TryFindFile(linkCtx, path, out var pathFull))
                return (pathFull, LoadLocalFileStream(pathFull));

            throw new FileNotFoundException($"File not found: '{pathFull}'");
        }

        /// <summary>
        /// Open a local file into a <see cref="Stream"/>.
        /// </summary>
        protected virtual Stream LoadLocalFileStream(string pathFull)
        {
            Validation.CheckNonEmpty(pathFull, nameof(pathFull));
            return new FileStream(pathFull, FileMode.Open, FileAccess.Read);
        }

        /// <summary>
        /// Create a file stream given the intended file location and capabilities.
        /// Should throw an <see cref="IOException"/> on failure.
        /// 
        /// The default implementation handles only Generic links and delegates to
        /// <see cref="CreateFileStreamAsync(Link, string, StreamOptions)"/>
        /// </summary>
        public override async Task<(Link full, Stream stream)> CreateStreamAsync(Link linkCtx, Link link,
            StreamOptions options = default)
        {
            Validation.CheckValueOrNull(linkCtx);
            Validation.CheckValue(link, nameof(link));

            switch (link.Kind)
            {
            case LinkKind.Generic:
                {
                    var (full, stream) = await CreateFileStreamAsync(linkCtx, link.Path, options).ConfigureAwait(false);
                    Link linkFull = full != link.Path && !string.IsNullOrEmpty(full) ? Link.CreateGeneric(full) : link;
                    return (linkFull, stream);
                }

            default:
                throw new NotSupportedException("Link kind not supported");
            }
        }

        /// <summary>
        /// Create a file stream given the intended file location and capabilities.
        /// Should throw an <see cref="IOException"/> on failure.
        /// 
        /// The default implementation calls <see cref="ResolveNewFile(Link, string, out string)"/> and
        /// delegates to <see cref="CreateLocalFileStream(string, StreamOptions)"/>.
        /// </summary>
        protected virtual Task<(string full, Stream stream)> CreateFileStreamAsync(Link linkCtx, string path,
            StreamOptions options = default)
        {
            Validation.CheckValueOrNull(linkCtx);
            Validation.CheckNonEmpty(path, nameof(path));

            ResolveNewFile(linkCtx, path, out var pathFull);
            return Task.FromResult((pathFull, CreateLocalFileStream(pathFull, options)));
        }

        /// <summary>
        /// Create a file stream given the intended file location and capabilities.
        /// Should throw an <see cref="IOException"/> on failure.
        /// 
        /// The default implementation calls <see cref="ResolveNewFile(string, string, out string)"/> and
        /// delegates to <see cref="CreateLocalFileStream(string, StreamOptions)"/>.
        /// </summary>
        protected virtual Task<(string full, Stream stream)> CreateFileStreamAsync(string pathCtx, string path,
            StreamOptions options = default)
        {
            Validation.CheckValueOrNull(pathCtx);
            Validation.CheckNonEmpty(path, nameof(path));

            ResolveNewFile(pathCtx, path, out var pathFull);
            return Task.FromResult((pathFull, CreateLocalFileStream(pathFull, options)));
        }

        /// <summary>
        /// Create a local file with the given capabilities.
        /// </summary>
        protected virtual Stream CreateLocalFileStream(string pathFull, StreamOptions caps = default)
        {
            Validation.CheckNonEmpty(pathFull, nameof(pathFull));
            return new FileStream(pathFull, FileMode.Create, FileAccess.ReadWrite);
        }

        /// <summary>
        /// Get a sequence of links to the files in the given location.
        /// 
        /// The default implementation handles only Generic link or null link and delegates to
        /// <see cref="GetLocalFiles(string)"/>.
        /// </summary>
        public override (Link, IEnumerable<Link>) GetFiles(Link linkCtx, Link link)
        {
            Validation.CheckValueOrNull(linkCtx);
            Validation.CheckValueOrNull(link);

            if (link == null || link.Kind == LinkKind.Generic)
            {
                if (!TryFindDir(linkCtx, link?.Path, out var pathFull))
                    throw new DirectoryNotFoundException($"Directory not found: '{pathFull}'");
                var seq = GetLocalFiles(pathFull);
                Link linkFull = link;
                if (linkFull == null || linkFull.Path != pathFull)
                    linkFull = Link.CreateGeneric(pathFull);
                var res = seq.Select(p => Link.CreateGeneric(p));
                return (linkFull, res);
            }

            throw new NotSupportedException("Link kind not supported");
        }

        /// <summary>
        /// Enumerate the local files in the given directory.
        /// </summary>
        protected virtual IEnumerable<string> GetLocalFiles(string pathFull)
        {
            Validation.CheckNonEmpty(pathFull, nameof(pathFull));
            return Directory.EnumerateFiles(pathFull);
        }
    }

    /// <summary>
    /// Default storage implementation to read/write file streams from the local file system.
    /// The specified location is interpreted as a local path.
    /// </summary>
    private sealed class DefaultLocalFileStorage : LocalFileStorage
    {
    }
}
