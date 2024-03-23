// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Microsoft.Rexl;
using Microsoft.Rexl.Code;
using Microsoft.Rexl.Utility;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RexlTest;

using EncodingOptions = TypeManager.JsonWriter.EncodingOptions;

/// <summary>
/// Test Json serialization implemented in TypeManager.
/// </summary>
[TestClass]
public sealed class TypeManagerJsonTests : RexlTestsBaseType<bool>
{
    private TestEnumTypeManager _typeManager;
    private EnumerableCodeGeneratorBase _codeGen;

    [TestInitialize]
    public void TestInitialize()
    {
        _typeManager = new TestEnumTypeManager();
        _codeGen = new CachingEnumerableCodeGenerator(_typeManager, TestGenerators.Instance);
    }

    [TestCategory("L0")]
    [TestMethod]
    public void WriteValues()
    {
        DoBaselineTests(GetScriptRunner(useCustomizer: false), @"Json\Write");
    }

    [TestCategory("L0")]
    [DataTestMethod]
    [DataRow(EncodingOptions.SymbolicInfs, DisplayName = nameof(EncodingOptions.SymbolicInfs))]
    [DataRow(EncodingOptions.StringNum, DisplayName = nameof(EncodingOptions.StringNum))]
    [DataRow(EncodingOptions.Base64Tensors, DisplayName = nameof(EncodingOptions.Base64Tensors))]
    [DataRow(EncodingOptions.PngTensors, DisplayName = nameof(EncodingOptions.PngTensors))]
    public void WriteValuesOptions(EncodingOptions option)
    {
        DoBaselineTests(GetScriptRunner(useCustomizer: false, option),
            string.Format(@"Json\WriteOptions\{0}.txt", option.ToString()));
    }

    [TestCategory("L0")]
    [TestMethod]
    public void WriteValuesCustom()
    {
        DoBaselineTests(GetScriptRunner(useCustomizer: true), @"Json\Write", @"Json\WriteCustom");
    }

    /// <summary>
    /// Tests that deserialize JSON into a provided DType. Non-customized forms of the JSON serialization tests
    /// perform a roundtrip serialize -> deserialize -> serialize, so most success cases are covered by the other
    /// tests. This is for testing deserialization of JSON that wouldn't be output from our own serialization,
    /// so it is mostly for error cases.
    /// </summary>
    [TestCategory("L0")]
    [TestMethod]
    public void ReadValues()
    {
        DoBaselineTests(ReadRaw, @"Json\Read", @"Json\ReadRaw");
    }

    void ReadRaw(string pathHead, string pathTail, string text, bool opts)
    {
        ReadCore(text);
    }

    void ReadCore(string text)
    {
        // Skip past empty lines and comment lines
        var lines = SplitLines(text).Where(line => !line.StartsWith('#') && !String.IsNullOrWhiteSpace(line)).ToArray();
        Assert.IsTrue(lines.Length % 2 == 0, "Deserialization test script should contain pairs of DType and JSON lines");

        var reader = _typeManager.CreateJsonReader();
        var writerRaw = _typeManager.CreateJsonWriter();

        for (var i = 0; i < lines.Length; i += 2)
        {
            Assert.IsTrue(DType.TryDeserialize(lines[i], out var type));

            var line = lines[i + 1].Trim();
            Sink.WriteLine($"*** {type} - {line}");

            if (reader.TryRead(type, line, out var value))
            {
                Assert.IsTrue(writerRaw.TryWriteToUtf8(type, value, out var res));
                var str = res.GetString();
                Sink.WriteLine($"Reserialized value: {str}");
            }
            else
            {
                Assert.IsNull(value);
                Sink.WriteLine("Read failed");
            }
            Sink.WriteLine();
        }
    }

    private Str8 WriteJsonToUtf8(TypeManager.JsonWriter writer, DType type, object value)
    {
        Assert.IsTrue(writer.TryWriteToUtf8(type, value, out var res));
        return res;
    }

    private Action<string, string, string, bool> GetScriptRunner(bool useCustomizer,
        EncodingOptions options = EncodingOptions.None)
    {
        return (pathHead, pathTail, text, tops) => RunJsonSerializationScript(text, useCustomizer, options);
    }

    /// <summary>
    /// Expects each line in <paramref name="text"/> to be a Rexl formula. This function executes
    /// each formula, serializes the results to json, and writes it to the test output. It's intended to be used
    /// for baseline comparison tests.
    /// </summary>
    private void RunJsonSerializationScript(string text, bool useCustomizer, EncodingOptions options)
    {
        foreach (var line in SplitLines(text))
        {
            if (line.Length == 0)
                continue;
            if (line.StartsWith("//"))
                continue;
            ExecuteAndWriteSeq(line, useCustomizer, options);
        }
    }

    private void ExecuteAndWriteSeq(string formula, bool useCustomizer, EncodingOptions options)
    {
        Sink.WriteLine($"*** Formula: {formula}");
        var (seqType, values) = TestUtils.ExecuteFormula<IEnumerable>(formula, _codeGen);
        Assert.IsTrue(seqType.IsSequence);
        var itemType = seqType.ItemTypeOrThis;
        Sink.WriteLine($"*** Item DType: {itemType}");

        TestSerializeValues(formula, values, itemType, useCustomizer, options);
    }

    private void TestSerializeValues(string formula, IEnumerable values, DType itemType,
        bool useCustomizer, EncodingOptions options)
    {
        TypeManager.JsonWriter.Customizer custRaw = null;

        // Token writer is no longer supported, so it doesn't have all the newer encoding options.
        bool base64Tensors = (options & EncodingOptions.Base64Tensors) != 0;

        if (useCustomizer)
        {
            var custWriterRaw = _typeManager.CreateJsonWriter(customizer: null, options);
            custRaw = (RawJsonWriter wrt, TypeManager.JsonWriter.TypeStack stack, object value, out bool fail) =>
            {
                fail = false;
                var type = stack.Peek().type;
                Assert.IsTrue(custWriterRaw.TryWriteToUtf8(type, value, out var str8Default));
                var str = str8Default.GetString().Replace("\r\n", "\n");
                wrt.WriteStringValue(string.Format("custom {0} :: {1}", type, str));
                return true;
            };
        }

        var writerRaw = _typeManager.CreateJsonWriter(custRaw, options);
        var reader = useCustomizer ? null : _typeManager.CreateJsonReader();

        foreach (var value in values)
        {
            Assert.IsTrue(writerRaw.TryWriteToUtf8(itemType, value, out var str8), $"Initial TryWrite failed for {formula}");
            var jsonOrig = str8.GetString();

            Sink.WriteLine(jsonOrig);

            if (reader != null)
            {
                // Ensure that we get the same value when we roundtrip to deserialize -> serialize again.
                bool res;
                object deserialized;
                try
                {
                    res = reader.TryRead(itemType, jsonOrig, out deserialized);
                }
                catch (Exception ex)
                {
                    Sink.WriteLine("*** Reading threw!");
                    Sink.WriteLine(ex.Message);
                    continue;
                }
                if (!res)
                {
                    Sink.WriteLine("*** Reading failed!");
                    continue;
                }

                Assert.IsTrue(writerRaw.TryWriteToUtf8(itemType, deserialized, out str8), $"Second TryWrite failed for {formula}");
                var jsonRoundTrip = str8.GetString();

                if (jsonOrig != jsonRoundTrip)
                {
                    Sink.WriteLine("*** Round trip mismatch!");
                    Sink.WriteLine(jsonRoundTrip.ToString());
                }
            }
        }
        Sink.WriteLine();
    }

    #region Performance tests

    private const int PerfIterations = 100000;

    // The scripts that contain values that will be used in the performance tests. The list currently contains
    // all of the scripts used for the functional tests.
    private static string[] PerfScripts = new[]
    {
            @"Json\Write\JsonPrimitiveValues.txt",
            @"Json\Write\JsonNullValues.txt",
            @"Json\Write\JsonRecords.txt",
            @"Json\Write\JsonTuples.txt",
            @"Json\Write\JsonTensors.txt"
        };

    private List<string> ReadAllPerfFormulas()
    {
        var formulas = new List<string>();

        foreach (var scriptPath in PerfScripts)
        {
            var lines = File.ReadAllLines(Path.Join(PathSrc, scriptPath));
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (line.Length == 0)
                    continue;
                if (line.StartsWith("//"))
                    continue;
                formulas.Add(trimmed);
            }
        }
        return formulas;
    }

    private List<(DType, object)> ExecuteTestFormulas(List<string> formulas)
    {
        var testValues = new List<(DType, object)>();
        foreach (var formula in formulas)
        {
            var (seqType, values) = TestUtils.ExecuteFormula<IEnumerable>(formula, _codeGen);
            Assert.IsTrue(seqType.IsSequence);
            var itemType = seqType.ItemTypeOrThis;
            foreach (var value in values)
                testValues.Add((itemType, value));
        }

        return testValues;
    }

    [TestCategory("L3")]
    [TestMethod]
    [Ignore]
    public void JsonSerializationWriteRaw_Perf()
    {
        var formulas = ReadAllPerfFormulas();

        // Pre-execute all of the formulas, so that time isn't included in our perf measurements.
        var rexlValues = ExecuteTestFormulas(formulas);

        var writer = _typeManager.CreateJsonWriter();
        using var mem = new MemoryStream();
        using var wrt = new RawJsonWriter(mem);
        wrt.WriteStartArray();
        var watch = Stopwatch.StartNew();
        for (var i = 0; i < PerfIterations; i++)
        {
            foreach (var (type, value) in rexlValues)
                Assert.IsTrue(writer.TryWrite(wrt, type, value));
        }
        wrt.WriteEndArray();
        wrt.Flush();
        mem.Flush();
        watch.Stop();
        Log($"Elapsed write time: {watch.Elapsed.TotalMilliseconds}ms");
    }

    [TestCategory("L3")]
    [TestMethod]
    [Ignore]
    public void JsonSerializationRead_Perf()
    {
        var formulas = ReadAllPerfFormulas();

        // Pre-serialize all of the formulas, so that time isn't included in our perf measurements.
        var rexlValues = ExecuteTestFormulas(formulas);
        var jsonValues = new List<(DType, string)>();
        var writer = _typeManager.CreateJsonWriter();
        foreach (var (type, value) in rexlValues)
        {
            var utf8 = WriteJsonToUtf8(writer, type, value);
            jsonValues.Add((type, utf8.GetString()));
        }

        var reader = _typeManager.CreateJsonReader();
        var watch = Stopwatch.StartNew();
        for (var i = 0; i < PerfIterations; i++)
        {
            foreach (var (type, json) in jsonValues)
                Assert.IsTrue(reader.TryRead(type, json, out var _));
        }
        watch.Stop();
        Log($"Elapsed read time: {watch.Elapsed.TotalMilliseconds}ms");
    }

    [TestCategory("L3")]
    [TestMethod]
    [Ignore]
    public void JsonWrite_Perf()
    {
        var script = @"
Range(1000)
->Map(as k, { A:k, B:k*k, })
//->Map(as k, { A:k, B:k*k,  C:k*k/4, D:""Some text "" & ToText(k), E:""ABC"" if k mod 2 = 0 else ""XYZ"", F:Time(0, k), })
//+>{ G: D & "":"" & E, H: F->ToText(), I: F.Day }
";

        const int repeats = 1000;

        // Want caching for this.
        Assert.IsInstanceOfType(_codeGen, typeof(CachingEnumerableCodeGenerator));
        var (type, value) = TestUtils.ExecuteFormula<IEnumerable>(script, _codeGen);

        var writerRaw = _typeManager.CreateJsonWriter();
        using var memRaw = new MemoryStream();

        // Do it once before timing to "warm" (jit) all the necessary code.
        using (var rjw = new RawJsonWriter(memRaw))
        {
            rjw.WriteStartArray();
            Assert.IsTrue(writerRaw.TryWrite(rjw, type, value));
            rjw.WriteEndArray();
            rjw.Flush();
        }
        memRaw.Flush();

        // Now do three trials each, alternating.
        long ticksRaw = 0;
        for (int trial = 0; trial < 3; trial++)
        {
            // Time raw.
            memRaw.Seek(0, SeekOrigin.Begin);
            memRaw.SetLength(0);
            var watch = Stopwatch.StartNew();
            using (var rjw = new RawJsonWriter(memRaw))
            {
                rjw.WriteStartArray();
                for (var i = 0; i < repeats; i++)
                    Assert.IsTrue(writerRaw.TryWrite(rjw, type, value));
                rjw.WriteEndArray();
                rjw.Flush();
            }
            memRaw.Flush();
            watch.Stop();
            var ticks = watch.Elapsed.Ticks;
            Log($"Raw time: {ticks}");
            ticksRaw += ticks;
        }

        Log($"Total raw time: {ticksRaw}");
    }
    #endregion Performance tests
}
