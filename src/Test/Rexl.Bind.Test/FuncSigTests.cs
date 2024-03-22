// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;

using Microsoft.Rexl;
using Microsoft.Rexl.Parse;
using Microsoft.Rexl.Private;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

[TestClass]
public class FuncSigTests : RexlTestsBaseText<bool>
{
    [TestMethod]
    public void All()
    {
        int count = DoBaselineTests(
            Run, @"FuncSig/All.txt");
        Assert.AreEqual(1, count);

        void Run(string pathHead, string pathTail, string text, bool opts)
        {
            var infos = BuiltinFunctions.Instance.GetInfos(includeHidden: true, includeDeprecated: true).OrderBy(info => info.Path.ToDottedSyntax());

            foreach (var info in infos.Concat(GetUdos()))
            {
                var oper = info.Oper;
                if (!oper.IsFunc)
                    continue;

                int min = oper.ArityMin;
                int max = oper.ArityMax;
                Assert.IsTrue(0 <= min && min <= max);
                Assert.IsTrue(oper.SupportsArity(min));
                Assert.IsTrue(oper.SupportsArity(max));

                Sink.WriteLine(
                    "Flags: {0}{1}, min: {2}, max: {3}, Path: {4}{5}{6}",
                    info.Hidden ? "H" : "_", info.Deprecated ? "D" : "_",
                    min, max < int.MaxValue ? max.ToString() : "*",
                    info.Path,
                    !info.PathAlt.IsRoot ? ", Alt: " : "", !info.PathAlt.IsRoot ? info.PathAlt.ToDottedSyntax() : "");

                foreach (var sig in info.GetSignatures())
                {
                    DumpSig(sig, sig.Description.GetString());
                    var sigExpand = sig.Expand();
                    if (sigExpand != sig)
                        DumpSig(sigExpand, "Expanded Form");
                }
            }
        }
    }

    private void DumpSig(Signature sig, string description)
    {
        Assert.IsNotNull(sig);

        var vol = sig.IsVolatile ? "[volatile] " : "";
        Sink.WriteLine("  {0}{1}", vol, description);
        for (int i = 0; i < sig.Arguments.Length; i++)
        {
            var arg = sig.Arguments[i];
            Sink.WriteLine("    {0}) {1}: {2}", i, arg.NameStr, arg.Description.GetString());
        }

        foreach (var group in sig.Groups)
        {
            if (group is Signature.OptionalGroup)
                Sink.WriteLine("    [{0}, {1}): Optional", group.Min, group.Lim);
            else if (group is Signature.RepetitiveGroup rep)
                Sink.WriteLine("    [{0}, {1}): Repetitive, min count = {2}", rep.Min, rep.Lim, rep.MinCount);
            else
                Assert.Fail();
        }
    }

    private IEnumerable<OperInfo> GetUdos()
    {
        var fma = RexlFormula.Create(SourceContext.Create("1"));
        var name = MakeName("Foo");
        yield return new OperInfo(
            name,
            UserFunc.Create(
                name, NPath.Root,
                Immutable.Array<DName>.Empty,
                fma, false));

        fma = RexlFormula.Create(SourceContext.Create("x + y"));
        name = MakeName("Bar");
        yield return new OperInfo(
            name,
            UserFunc.Create(
                name, NPath.Root,
                Immutable.Array<DName>.Create(new DName("x"), new DName("y")),
                fma, false, "Some custom description."));


    }

    private static NPath MakeName(string str)
    {
        Validation.BugCheck(LexUtils.TryLexPath(str, out NPath full));
        return full;
    }
}
