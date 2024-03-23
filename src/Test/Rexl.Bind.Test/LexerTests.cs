// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;

using Microsoft.Rexl;
using Microsoft.Rexl.Lex;
using Microsoft.Rexl.Sink;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

[TestClass]
public class LexerTests : RexlTestsBaseText<bool>
{
    [TestMethod]
    public void LexerBaselineTests()
    {
        DoBaselineTests(ProcessLexFile, @"Lexer");
    }

    [TestMethod]
    public void LexerTextLiterals()
    {
        int count = DoBaselineTests(Run, @"LexerEscape/Text.txt");
        Assert.AreEqual(1, count);

        void Do(string src)
        {
            const string hex = "0123456789ABCDEF";
            Sink.Write("Src: ");
            foreach (var ch in src)
            {
                if (' ' <= ch && ch < '\x7F')
                    Sink.Write(ch);
                else
                {
                    var u = (ushort)ch;
                    Sink.Write('[');
                    if (u >= 0x100)
                    {
                        Sink.Write(hex[(u >> 12) & 0xF]);
                        Sink.Write(hex[(u >> 8) & 0xF]);
                    }
                    Sink.Write(hex[(u >> 4) & 0xF]);
                    Sink.Write(hex[(u >> 0) & 0xF]);
                    Sink.Write(']');
                }
            }
            Sink.WriteLine();

            var sb = new StringBuilder();
            LexUtils.AppendTextLiteral(sb, src);
            var res = sb.ToString();

            Sink.TWrite("Res: ").WriteLine(res);
            Sink.WriteLine("###");

            var tokens = RexlLexer.Instance.LexSource(SourceContext.Create(res));
            if (tokens.Length != 2)
                Sink.Write("Bad!");

            Assert.AreEqual(2, tokens.Length);
            Assert.AreEqual(TokKind.Eof, tokens[1].Kind);
            Assert.AreEqual(TokKind.TxtLit, tokens[0].Kind);
            var tok = tokens[0].As<TextLitToken>();
            Assert.AreEqual(TextLitFlags.None, tok.Flags);
        }

        void Run(string pathHead, string pathTail, string text, bool opts)
        {
            Do("");
            Do("ABC");
            Do("AB\\C");
            Do("AB\"C");
            Do("AB\r\nC");

            var sb = new StringBuilder();
            for (int i = 0; i < (int)'A'; i++)
                sb.Append((char)i);
            Do(sb.ToString());

            sb.Clear();
            for (int i = (int)'z'; i < 0xA0; i++)
                sb.Append((char)i);
            Do(sb.ToString());

            sb.Clear();
            for (int i = 0xA0; i < 0xC0; i++)
                sb.Append((char)i);
            Do(sb.ToString());

            sb.Clear();
            for (int i = 0x0100; i < 0x0120; i++)
                sb.Append((char)i);
            Do(sb.ToString());

            sb.Clear();
            for (int i = 0x2020; i < 0x2040; i++)
                sb.Append((char)i);
            Do(sb.ToString());
        }
    }

    private void ProcessLexFile(string pathHead, string pathTail, string text, bool opts)
    {
        var segs = SplitHashBlocks(text);
        foreach (var source in segs)
        {
            var lines = SplitLines(source);
            foreach (var line in lines)
                Sink.WriteLine("> {0}", line);

            var toks = RexlLexer.Instance.LexSource(SourceContext.Create(source));
            foreach (var tok in toks)
                Sink.WriteLine(tok.Render());
            Sink.WriteLine("###");
        }

        // Test the line mapping API.
        var ctx = SourceContext.Create(text);
        int ln = 0;
        int ichMin = 0;
        int ichMinPrev = -1;
        for (int ich = 0; ich <= ctx.Text.Length; ich++)
        {
            var (line, col) = ctx.GetLineCol(ich, end: false);
            Assert.AreEqual(ln, line);
            Assert.AreEqual(ich - ichMin, col);
            (line, col) = ctx.GetLineCol(ich, end: true);
            if (ich == ichMin && ich > 0)
            {
                Assert.AreEqual(ln - 1, line);
                Assert.AreEqual(ich - ichMinPrev, col);
            }
            else
            {
                Assert.AreEqual(ln, line);
                Assert.AreEqual(ich - ichMin, col);
            }

            if (ich < ctx.Text.Length && ctx.Text[ich] == '\n')
            {
                ln++;
                ichMinPrev = ichMin;
                ichMin = ich + 1;
            }
        }
    }
}
