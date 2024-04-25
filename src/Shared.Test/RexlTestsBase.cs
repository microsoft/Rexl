// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

/// <summary>
/// Base class for Rexl tests. Implements base-line based tests and logging.
/// </summary>
public abstract class RexlTestsBase<TSink, TOpts>
    where TSink : TextSink
{
    protected static readonly char[] _lfs = new[] { '\r', '\n' };

    protected static readonly string _strXout = $"{Path.DirectorySeparatorChar}xout{Path.DirectorySeparatorChar}";

    protected readonly string PathSrc;
    protected readonly string PathDst;
    protected readonly string PathBsl;
    protected readonly string PathData;

    protected abstract TSink Sink { get; }

    protected abstract string GetTextAndReset();

    protected virtual string GetSubPath(string name)
    {
        if (name.EndsWith(".Test"))
            name = name[..^5];
        if (name.StartsWith("Rexl."))
            name = name[5..];
        return name;
    }

    protected virtual string GetSrcPath(string pathSln, string name, string sub) => Path.Join(pathSln, "Test", name, "Scripts");

    protected virtual string GetDstPath(string pathSln, string name, string sub) => Path.Join(pathSln, "Test", "XOutput", sub);

    protected virtual string GetBslPath(string pathSln, string name, string sub) => Path.Join(pathSln, "Test", "Baseline", sub);

    protected virtual string GetDataPath(string pathSln, string name, string sub) => Path.Join(pathSln, "Test", "Data");

    protected RexlTestsBase()
    {
        // Find the project directory from the assembly (.dll) directory.
        var asm = GetType().Assembly;
        var pathCur = asm.Location;
        int ich = pathCur.LastIndexOf(_strXout);
        Assert.IsTrue(ich >= 0, "Couldn't find directory containing 'xout' directory from current directory: '{0}'", pathCur);

        var pathSln = pathCur.Substring(0, ich);
        var name = asm.GetName().Name;

        // REVIEW: It's a bit less than ideal to call virtual methods from this ctor, but this is test code,
        // so this shouldn't be a big deal. And currently, no one overrides.
        var sub = GetSubPath(name);
        PathSrc = GetSrcPath(pathSln, name, sub);
        PathDst = GetDstPath(pathSln, name, sub);
        PathBsl = GetBslPath(pathSln, name, sub);
        PathData = GetDataPath(pathSln, name, sub);

        Assert.IsTrue(Directory.Exists(PathSrc));
    }

    protected static string[] SplitLines(string text)
    {
        return text.Split(_lfs, StringSplitOptions.RemoveEmptyEntries);
    }

    protected static IEnumerable<string> SplitHashBlocks(string text)
    {
        foreach (var seg in text.Split("\n###", StringSplitOptions.RemoveEmptyEntries))
        {
            var cell = seg.Trim(_lfs);
            if (!string.IsNullOrEmpty(cell))
                yield return cell;
        }
    }

    /// <summary>
    /// Invoke this to perform base-line tests on a directory.
    /// </summary>
    protected int DoBaselineTests(Action<string, string, string, TOpts> doOne,
        string dirSrc, string dirDst = null, bool subDirs = false, TOpts options = default)
    {
        Validation.AssertValue(doOne);
        Validation.AssertNonEmpty(dirSrc);
        Validation.AssertNonEmptyOrNull(dirDst);

        var runner = new BaselineTestRunner(this, dirSrc, dirDst, subDirs);
        runner.CleanOuts();
        runner.GenerateOuts(doOne, options);
        runner.CompareDirectories();

        return runner.Count;
    }

    /// <summary>
    /// Invoke this to perform base-line tests on a directory.
    /// </summary>
    protected async Task<int> DoBaselineTestsAsync(Func<string, string, string, TOpts, Task> doOne,
        string dirSrc, string dirDst = null, bool subDirs = false, TOpts options = default)
    {
        Validation.AssertValue(doOne);
        Validation.AssertNonEmpty(dirSrc);
        Validation.AssertNonEmptyOrNull(dirDst);

        var runner = new BaselineTestRunner(this, dirSrc, dirDst, subDirs);
        runner.CleanOuts();
        await runner.GenerateOutsAsync(doOne, options).ConfigureAwait(false);
        await runner.CompareDirectoriesAsync().ConfigureAwait(false);

        return runner.Count;
    }

    private class BaselineTestRunner
    {
        protected readonly RexlTestsBase<TSink, TOpts> _parent;

        protected List<string> _fileNames;
        protected string _pathSrcDir;
        protected string _pathBslDir;
        protected string _pathOutDir;

        public int Count => _fileNames.Count;

        public BaselineTestRunner(RexlTestsBase<TSink, TOpts> parent, string dirSrc, string dirDst, bool subDirs)
        {
            Validation.AssertValue(parent);
            Validation.AssertNonEmpty(dirSrc);
            Validation.AssertNonEmptyOrNull(dirDst);

            _parent = parent;

            if (dirDst == null)
                dirDst = dirSrc;

            var chSep = Path.DirectorySeparatorChar;
            if (chSep != '\\')
            {
                dirSrc = dirSrc.Replace('\\', chSep);
                dirDst = dirDst.Replace('\\', chSep);
            }

            _pathSrcDir = Path.Join(_parent.PathSrc, dirSrc);
            _pathBslDir = Path.Join(_parent.PathBsl, dirDst);
            _pathOutDir = Path.Join(_parent.PathDst, dirDst);

            _fileNames = new List<string>();
            if (File.Exists(_pathSrcDir))
            {
                // Single file.
                _fileNames.Add(Path.GetFileName(_pathSrcDir));
                _pathSrcDir = Path.GetDirectoryName(_pathSrcDir);
                _pathBslDir = Path.GetDirectoryName(_pathBslDir);
                _pathOutDir = Path.GetDirectoryName(_pathOutDir);
                Directory.CreateDirectory(_pathBslDir);
                Directory.CreateDirectory(_pathOutDir);
            }
            else
            {
                Assert.IsTrue(Directory.Exists(_pathSrcDir), "Couldn't find test Scripts directory: {0}", _pathSrcDir);

                string[] dirs;
                if (subDirs)
                {
                    dirs = Directory.EnumerateDirectories(_pathSrcDir, "*", SearchOption.AllDirectories)
                        .Select(d =>
                        {
                            int len = _pathSrcDir.Length;
                            Assert.IsTrue(d.Length > len && (d[len] == '\\' || d[len] == '/') && d.IndexOf(_pathSrcDir) == 0,
                                "Subdir path '{0}' does not start with path '{1}'", d, _pathSrcDir);
                            return d.Substring(len + 1);
                        })
                        .Prepend("")
                        .ToArray();
                }
                else
                    dirs = new[] { "" };

                foreach (var d in dirs)
                {
                    string src = Path.Combine(_pathSrcDir, d);
                    string bsl = Path.Combine(_pathBslDir, d);
                    string dst = Path.Combine(_pathOutDir, d);
                    Directory.CreateDirectory(bsl);
                    Directory.CreateDirectory(dst);
                    _fileNames.AddRange(Directory.EnumerateFiles(src, "*.txt").Select(path => Path.Combine(d, Path.GetFileName(path))));
                }
            }
        }

        public void CleanOuts()
        {
            foreach (var fileName in _fileNames)
            {
                var pathDst = Path.Join(_pathOutDir, fileName);
                File.Delete(pathDst);
            }
        }

        public void GenerateOuts(Action<string, string, string, TOpts> doOne, TOpts options)
        {
            foreach (var fileName in _fileNames)
            {
                var pathSrc = Path.Join(_pathSrcDir, fileName);
                _parent.Log("Processing script: {0}", pathSrc);

                // Normalize line endings so character indices match in base lines.
                var text = _parent.NormalizeLines(File.ReadAllText(pathSrc));
                doOne(_pathSrcDir, fileName, text, options);
                var res = _parent.GetTextAndReset();

                // Unfortunately, res might mix CRLF and LF. Normalize to Environment.NewLine.
                res = res.Replace("\r\n", "\n");
                if (Environment.NewLine != "\n")
                    res = res.Replace("\n", Environment.NewLine);

                var pathDst = Path.Join(_pathOutDir, fileName);
                File.WriteAllText(pathDst, res);
            }
        }

        public async Task GenerateOutsAsync(Func<string, string, string, TOpts, Task> doOne, TOpts options)
        {
            foreach (var fileName in _fileNames)
            {
                var pathSrc = Path.Join(_pathSrcDir, fileName);
                _parent.Log("Processing script: {0}", pathSrc);

                // Normalize line endings so character indices match in base lines.
                var text = _parent.NormalizeLines(await File.ReadAllTextAsync(pathSrc).ConfigureAwait(false));
                await doOne(_pathSrcDir, fileName, text, options).ConfigureAwait(false);
                var res = _parent.GetTextAndReset();

                // Unfortunately, res might mix CRLF and LF. Normalize to Environment.NewLine.
                res = res.Replace("\r\n", "\n");
                if (Environment.NewLine != "\n")
                    res = res.Replace("\n", Environment.NewLine);

                var pathDst = Path.Join(_pathOutDir, fileName);
                await File.WriteAllTextAsync(pathDst, res).ConfigureAwait(false);
            }
        }

        public void CompareDirectories()
        {
            _parent.CompareDirectories(_pathBslDir, _pathOutDir, _fileNames);
        }

        public Task CompareDirectoriesAsync()
        {
            return _parent.CompareDirectoriesAsync(_pathBslDir, _pathOutDir, _fileNames);
        }
    }

    protected string NormalizeLines(string text)
    {
        return text.Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd('\n');
    }

    protected void Log(string msg)
    {
        Console.WriteLine(msg);
    }

    protected void Log(string fmt, params object[] args)
    {
        Log(string.Format(fmt, args));
    }

    protected void CompareDirectories(string pathBslDir, string pathOutDir, List<string> fileNames)
    {
        int all = 0;
        int good = 0;
        foreach (var name in fileNames)
        {
            all++;

            var pathBsl = Path.Join(pathBslDir, name);
            var pathOut = Path.Join(pathOutDir, name);

            if (!File.Exists(pathOut))
            {
                Log("*** Missing output file: {0}", pathOut);
                continue;
            }
            if (!File.Exists(pathBsl))
            {
                Log("*** Missing baseline file: {0}", pathBsl);
                continue;
            }

            var textBsl = NormalizeLines(File.ReadAllText(pathBsl));
            var textOut = NormalizeLines(File.ReadAllText(pathOut));
            if (textBsl != textOut)
            {
                Log("*** Output and Baseline mismatch: '{0}' vs '{1}'", pathBsl, pathOut);
                var linesBsl = textBsl.Split('\n');
                var linesOut = textOut.Split('\n');
                int cline = Math.Min(linesBsl.Length, linesOut.Length);
                int iline = 0;
                while (iline < cline && linesBsl[iline] == linesOut[iline])
                    iline++;
                Log("  Line {0}:", iline);
                Log("     bsl: [{0}]", iline < linesBsl.Length ? linesBsl[iline] : "<eof>");
                Log("     out: [{0}]", iline < linesOut.Length ? linesOut[iline] : "<eof>");
            }
            else
                good++;
        }

        Validation.Assert(good <= all);
        if (good == all)
        {
            Log("Output and baseline directories match: '{0}'", pathBslDir);
            return;
        }

        var msg = string.Format("*** Error: {0} differences between output and baseline directories: '{1}'", all - good, pathBslDir);
        Log(msg);
        Assert.Fail(msg);
    }

    protected async Task CompareDirectoriesAsync(string pathBslDir, string pathOutDir, List<string> fileNames)
    {
        int all = 0;
        int good = 0;
        foreach (var name in fileNames)
        {
            all++;

            var pathBsl = Path.Join(pathBslDir, name);
            var pathOut = Path.Join(pathOutDir, name);

            if (!File.Exists(pathOut))
            {
                Log("*** Missing output file: {0}", pathOut);
                continue;
            }
            if (!File.Exists(pathBsl))
            {
                Log("*** Missing baseline file: {0}", pathBsl);
                continue;
            }

            var taskBsl = File.ReadAllTextAsync(pathBsl);
            var taskOut = File.ReadAllTextAsync(pathOut);
            var textBsl = NormalizeLines(await taskBsl.ConfigureAwait(false));
            var textOut = NormalizeLines(await taskOut.ConfigureAwait(false));
            if (textBsl != textOut)
            {
                Log("*** Output and Baseline mismatch: '{0}' vs '{1}'", pathBsl, pathOut);
                var linesBsl = textBsl.Split('\n');
                var linesOut = textOut.Split('\n');
                int cline = Math.Min(linesBsl.Length, linesOut.Length);
                int iline = 0;
                while (iline < cline && linesBsl[iline] == linesOut[iline])
                    iline++;
                Log("  Line {0}:", iline);
                Log("     bsl: [{0}]", iline < linesBsl.Length ? linesBsl[iline] : "<eof>");
                Log("     out: [{0}]", iline < linesOut.Length ? linesOut[iline] : "<eof>");
            }
            else
                good++;
        }

        Validation.Assert(good <= all);
        if (good == all)
        {
            Log("Output and baseline directories match: '{0}'", pathBslDir);
            return;
        }

        var msg = string.Format("*** Error: {0} differences between output and baseline directories: '{1}'", all - good, pathBslDir);
        Log(msg);
        Assert.Fail(msg);
    }
}

public abstract class RexlTestsBaseText<TOpts> : RexlTestsBase<SbTextSink, TOpts>
{
    private readonly SbTextSink _sink = new SbTextSink();

    protected sealed override SbTextSink Sink => _sink;

    protected sealed override string GetTextAndReset()
    {
        var res = _sink.Builder.ToString();
        _sink.Builder.Clear();
        return res;
    }

    protected RexlTestsBaseText() : base()
    {
    }
}
