using System.Linq;
using Microsoft.Rexl;
using Microsoft.Rexl.Bind;
using Microsoft.Rexl.Private;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

[TestClass]
public class SignatureTests
{
    [TestMethod]
    public void GetSignature_GetDefaultSignatures_Success()
    {
        var rexlFunc = new TestFunc1(new DName("Test"), 1, 2);
        var signature = rexlFunc.GetSignatures().FirstOrDefault(s => s.Arguments.Length == 0);
        Assert.IsNull(signature);

        signature = rexlFunc.GetSignatures().FirstOrDefault(s => s.Arguments.Length == 1);
        Assert.IsNotNull(signature);
        Assert.AreEqual(1, signature.Arguments.Length);
        Assert.AreEqual(AboutTestFunc, signature.Description);
        Assert.AreEqual(TestFuncArg1, signature.Arguments[0].Name);
        Assert.AreEqual(AboutTestFuncArg1, signature.Arguments[0].Description);

        signature = rexlFunc.GetSignatures().FirstOrDefault(s => s.Arguments.Length == 2);
        Assert.IsNotNull(signature);
        Assert.AreEqual(2, signature.Arguments.Length);
        Assert.AreEqual(AboutTestFunc, signature.Description);
        Assert.AreEqual(TestFuncArg1, signature.Arguments[0].Name);
        Assert.AreEqual(AboutTestFuncArg1, signature.Arguments[0].Description);
        Assert.AreEqual(TestFuncArg2, signature.Arguments[1].Name);
        Assert.AreEqual(AboutTestFuncArg2, signature.Arguments[1].Description);
    }

    [TestMethod]
    public void GetSignature_VerifyStringsProvidedForFunctions_Success()
    {
        foreach (var info in BuiltinFunctions.Instance.GetInfos())
        {
            var oper = info.Oper;
            if (!oper.IsFunc)
                continue;
            foreach (var signature in info.GetSignatures())
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(signature.Description.GetString()));
                Assert.IsTrue(!signature.Arguments.IsDefault);
                foreach (var argument in signature.Arguments)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(argument.Name.GetString()));
                    Assert.IsFalse(string.IsNullOrWhiteSpace(argument.Description.GetString()));
                }
            }
        }
    }

    private sealed class TestFunc1 : RexlOper, IHaveSignatures
    {
        public TestFunc1(DName name, int arityMin, int arityMax)
            : base(isFunc: true, name, arityMin, arityMax)
        {
        }

        public Immutable.Array<Signature> GetSignatures()
        {
            return Immutable.Array.Create(
                new Signature(AboutTestFunc, Argument.Create(TestFuncArg1, AboutTestFuncArg1)),
                new Signature(AboutTestFunc, Argument.Create(TestFuncArg1, AboutTestFuncArg1), Argument.Create(TestFuncArg2, AboutTestFuncArg2)));
        }

        protected override ArgTraits GetArgTraitsCore(int carg)
        {
            Validation.Assert(SupportsArity(carg));
            return ArgTraitsSimple.Create(this, eager: true, carg);
        }

        protected override bool CertifyCore(BndCallNode call, ref bool full)
        {
            return false;
        }
    }

    private static readonly StringId AboutTestFunc = new StringId(nameof(AboutTestFunc), "AboutTestFunc");
    private static readonly StringId TestFuncArg1 = new StringId(nameof(TestFuncArg1), "TestFuncArg1");
    private static readonly StringId AboutTestFuncArg1 = new StringId(nameof(AboutTestFuncArg1), "AboutTestFuncArg1");
    private static readonly StringId TestFuncArg2 = new StringId(nameof(TestFuncArg2), "TestFuncArg2");
    private static readonly StringId AboutTestFuncArg2 = new StringId(nameof(AboutTestFuncArg2), "AboutTestFuncArg2");
}
