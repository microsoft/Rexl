// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Rexl;

/// <summary>
/// The standard "builtin" procedures for rexl.
/// </summary>
public sealed class BuiltinProcedures : OperationRegistry
{
    public static readonly BuiltinProcedures Instance = new BuiltinProcedures();

    private BuiltinProcedures()
    {
        AddOne(ReadParquetProc.ReadParquet);
        AddOne(ReadParquetProc.ReadParquetReq, hidden: true);
        AddOne(ReadRbinProc.ReadRbin);
        AddOne(ReadBytesProc.Instance);
        AddOne(ReadByteBlocksProc.Instance);
        AddOne(ReadTextProc.Instance);
        AddOne(ReadLinesProc.Instance);

        AddOne(WriteParquetProc.WriteParquet);
        AddOne(WriteRbinProc.WriteRbin);
        AddOne(WriteBytesProc.Instance);
        AddOne(WritePngProc.Instance);

        AddOne(NowProc.NowUtc);
        AddOne(NowProc.NowLocal);
        AddOne(NowProc.NowOffset);

        AddOne(ListFilesProc.Instance);
    }
}
