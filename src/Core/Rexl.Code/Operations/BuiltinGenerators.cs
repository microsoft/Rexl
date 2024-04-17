// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Rexl.Code;

/// <summary>
/// The registry of operator code generators for the builtin operations.
/// </summary>
public sealed class BuiltinGenerators : GeneratorRegistry
{
    public static readonly BuiltinGenerators Instance = new BuiltinGenerators();

    private BuiltinGenerators()
    {
        Add(ForEachGen.Instance);

        Add(ReverseGen.Instance);

        Add(CrossJoinGen.Instance);
        Add(KeyJoinGen.Instance);
        Add(DistinctGen.Instance);

        Add(ChainMapGen.Instance);

        Add(TakeAtGen.Instance);
        Add(TakeOneGen.Instance);
        Add(TakeDropGen.Instance);

        Add(RepeatGen.Instance);
        Add(CountGen.Instance);
        Add(AnyAllGen.Instance);
        Add(MakePairsGen.Instance);
        Add(FoldGen.Instance);
        Add(ScanGen.Instance);
        Add(GenerateGen.Instance);

        Add(LikeGen.Instance);

        Add(IsNullGen.Instance);
        Add(IsEmptyGen.Instance);

        Add(AbsGen.Instance);

        Add(DateGen.Instance);
        Add(TimeGen.Instance);
        Add(ChronoPartGen.Instance);
        Add(CastChronoGen.Instance);
        Add(DateNowGen.Instance);

        Add(CastGuidGen.Instance);
        Add(ToGuidGen.Instance);
        Add(MakeGuidGen.Instance);

        Add(TextLenGen.Instance);
        Add(TextCaseGen.Instance);
        Add(TextConcatGen.Instance);
        Add(TextIndexOfGen.Instance);
        Add(TextStartsWithGen.Instance);
        Add(TextPartGen.Instance);
        Add(TextTrimGen.Instance);
        Add(TextReplaceGen.Instance);

        Add(typeof(SumFunc), SumBaseGen.Instance);
        Add(typeof(MeanFunc), SumBaseGen.Instance);
        Add(MinMaxGen.Instance);
        Add(RangeGen.Instance);
        Add(SequenceGen.Instance);
        Add(DivGen.Instance);
        Add(ModGen.Instance);
        Add(BinGen.Instance);

        Add(UniformGen.Instance);

        Add(CastGen.Instance);
        Add(ToXXGen.Instance);
        Add(ToTextGen.Instance);

        Add(R8Gen.Instance);
        Add(IntHexGen.Instance);

        Add(FloatIsNanGen.Instance);
        Add(FloatBitsGen.Instance);
        Add(FloatFromBitsGen.Instance);

        Add(TTestOneSampleGen.Instance);
        Add(TTestTwoSampleGen.Instance);
        Add(TTestPairedGen.Instance);

        Add(TensorFillGen.Instance);
        Add(TensorFromGen.Instance);
        Add(TensorBuildGen.Instance);
        Add(TensorShapeGen.Instance);
        Add(TensorValuesGen.Instance);
        Add(TensorReshapeGen.Instance);
        Add(TensorTransposeGen.Instance);
        Add(TensorExpandDimsGen.Instance);
        Add(TensorDotGen.Instance);
        Add(TensorPointWiseGen.Instance);
        Add(TensorForEachGen.Instance);
        Add(TensorSoftMaxGen.Instance);

        Add(PixelsToPngGen.Instance);
        Add(GetPixelsGen.Instance);
        Add(ReadPixelsGen.Instance);
        Add(ResizePixelsGen.Instance);
        Add(ToBase64Gen.Instance);

        Add(LinkGen.Instance);
        Add(LinkPropGen.Instance);

        Add(ModuleOptimizeGen.Instance);

        Add(ReadBytesGen.Instance);

        // Procedures.
        Add(ReadParquetProcGen.Instance);
        Add(ReadRbinProcGen.Instance);
        Add(ReadBytesProcGen.Instance);
        Add(ReadByteBlocksProcGen.Instance);
        Add(ReadTextProcGen.Instance);
        Add(ReadLinesProcGen.Instance);

        Add(WriteParquetProcGen.Instance);
        Add(WriteRbinProcGen.Instance);
        Add(WriteBytesProcGen.Instance);

        Add(NowGen.Instance);

        Add(ListFilesProcGen.Instance);

        Add(UserProcGen.Instance);

        // Test procs. REVIEW: Remove these at some point.
        Add(EchoProcGen.Instance);
        Add(SyncProcGen.Instance);
        Add(ThreadProcGen.Instance);
        Add(PipeProcGen.Instance);
        Add(FailProcGen.Instance);
    }
}
