// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;

using Microsoft.Rexl;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

[TestClass]
public partial class NPathTests : RexlTestsBaseText<bool>
{
    [TestMethod]
    public void NPathCompareTest()
    {
        int count = DoBaselineTests(
            Run, @"NPathCompare");

        void Run(string pathHead, string pathTail, string text, bool opts)
        {
            var names = new List<NPath>();
            names.Add(NPath.Root);
            int count = 0;
            foreach (var line in SplitLines(text))
            {
                ++count;
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    continue;

                if (!LexUtils.TryLexPath(line, out var name))
                {
                    Sink.WriteLine("Skipped on line {0}: [{1}]", count, line);
                    continue;
                }

                names.Add(name);
            }

            Sink.WriteLine("*** User sorted ***");
            foreach (var name in names.OrderBy(n => n, NPathComparer.Instance))
                Sink.WriteDottedSyntax(name).WriteLine();
            Sink.WriteLine("*** Raw sorted ***");
            foreach (var name in names.OrderBy(n => n, NPathRawComparer.Instance))
                Sink.WriteDottedSyntax(name).WriteLine();
            Sink.WriteLine("*** End ***");
        }
    }
}
