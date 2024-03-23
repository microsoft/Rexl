// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Rexl.Private;
using Microsoft.Rexl.Sink;

namespace Microsoft.Rexl;

using TypeTuple = Immutable.Array<DType>;

partial struct DType
{
    private string ToStringCore(bool compact)
    {
        AssertValidOrDefault();

        SbTextSink? sink = null;
        string? res = null;
        switch (_kind)
        {
        default:
            res = _kind.ToStr(_opt);
            break;

        case DKind.Record:
            sink = new SbTextSink();
            _AppendRecordType(sink, _GetRecordInfo(), _opt, compact);
            break;
        case DKind.Module:
            sink = new SbTextSink();
            _AppendModuleType(sink, _GetModuleInfo(), _opt, compact);
            break;
        case DKind.Tuple:
            sink = new SbTextSink();
            _AppendTupleType(sink, _GetTupleInfo(), _opt, compact);
            break;
        case DKind.Tensor:
            sink = new SbTextSink();
            _AppendTensorType(sink, _GetTensorInfo(), _opt, compact);
            break;
        case DKind.Uri:
            sink = new SbTextSink();
            _AppendUriType(sink, GetRootUriFlavor());
            break;
        }
        // One or other should be defined.
        Validation.Assert((res is null) != (sink is null));

        if (_seqCount <= 0)
            return res ?? sink!.Builder.ToString();

        if (sink == null)
            sink = new SbTextSink().TWrite(res);
        sink.Write('*', _seqCount);
        return sink.Builder.ToString();
    }

    public void AppendTo(TextSink sink, bool compact = false)
    {
        AssertValidOrDefault();
        Validation.AssertValue(sink);

        switch (_kind)
        {
        default:
            sink.Write(_kind.ToStr(_opt));
            break;

        case DKind.Record:
            _AppendRecordType(sink, _GetRecordInfo(), _opt, compact);
            break;
        case DKind.Module:
            _AppendModuleType(sink, _GetModuleInfo(), _opt, compact);
            break;
        case DKind.Tuple:
            _AppendTupleType(sink, _GetTupleInfo(), _opt, compact);
            break;
        case DKind.Tensor:
            _AppendTensorType(sink, _GetTensorInfo(), _opt, compact);
            break;
        case DKind.Uri:
            _AppendUriType(sink, GetRootUriFlavor());
            break;
        }

        if (_seqCount > 0)
            sink.Write('*', _seqCount);
    }

    private static void _AppendModuleType(TextSink sink, ModuleInfo info, bool opt, bool compact)
    {
        Validation.AssertValue(sink);

        sink.Write("M{");

        string strPre = "";
        string sep = compact ? "," : ", ";
        foreach (var (name, type, sk) in info.GetInfos())
        {
            Validation.Assert(name.IsValid);
            Validation.Assert(type.IsValid);
            Validation.Assert(sk.IsValid());

            sink.Write(strPre);
            sink.Write(compact ? sk.ToCode() : sk.ToStr());
            sink.Write(compact ? '!' : ' ');
            sink.WriteEscapedName(name);
            sink.Write(':');
            type.AppendTo(sink, compact);
            strPre = sep;
        }

        sink.Write("}");
        if (opt)
            sink.Write('?');
    }

    private static void _AppendTupleType(TextSink sink, TupleInfo info, bool opt, bool compact)
    {
        Validation.AssertValue(sink);

        sink.Write('(');

        string strPre = "";
        string sep = compact ? "," : ", ";
        for (int slot = 0; slot < info.Count; slot++)
        {
            sink.Write(strPre);
            info.GetSlotType(slot).AppendTo(sink, compact);
            strPre = sep;
        }

        sink.Write(')');
        if (opt)
            sink.Write('?');
    }

    private static void _AppendTensorType(TextSink sink, TensorInfo info, bool opt, bool compact)
    {
        Validation.AssertValue(sink);
        Validation.AssertValue(info);

        info.ItemType.AppendTo(sink, compact);
        info.AppendShape(sink);
        if (opt)
            sink.Write('?');
    }

    private static void _AppendUriType(TextSink sink, NPath flavor)
    {
        sink.Write("U<");
        sink.WriteDottedSyntax(flavor);
        sink.Write('>');
    }

    /// <summary>
    /// Serialize the DType.
    /// </summary>
    public string Serialize(bool compact = false)
    {
        string res = ToStringCore(compact);
#if DEBUG
        bool tmp = TryDeserialize(res, out var type);
        Validation.Assert(tmp);
        Validation.Assert(type == this);
#endif
        return res;
    }

    /// <summary>
    /// Try to deserialize a DType.
    /// </summary>
    public static bool TryDeserialize(string? str, out DType type)
    {
        Validation.BugCheckValueOrNull(str);

        if (str == null)
        {
            type = default;
            return false;
        }

        // REVIEW: Why do we do this? Can it be removed?
        if (str.Length == 1 && str[0] == 'x')
        {
            type = default;
            return true;
        }

        var tp = new TypeParser(str, 0, alts: false, union: false);
        if (tp.TryParse(out type) && tp.Ich == str.Length)
            return true;

        type = default;
        return false;
    }

    /// <summary>
    /// Try to deserialize a DType with encoded alternatives. Alternatives are replaced with the
    /// common super type.
    /// </summary>
    public static bool TryDeserializeWithAlts(string str, bool union, out DType type)
    {
        Validation.BugCheckValueOrNull(str);

        if (str == null)
        {
            type = default;
            return false;
        }

        var tp = new TypeParser(str, 0, alts: true, union);
        if (tp.TryParse(out type) && tp.Ich == str.Length)
            return true;

        type = default;
        return false;
    }

    private struct TypeParser
    {
        private readonly bool _alts;
        private readonly bool _union;
        private readonly string _str;
        private int _ich;

        public int Ich => _ich;

        public bool End => _ich >= _str.Length;

        public TypeParser(string str, int ich, bool alts, bool union)
        {
            Validation.AssertValue(str);
            Validation.AssertIndexInclusive(ich, str.Length);
            _alts = alts;
            _union = union;
            _str = str;
            _ich = ich;
        }

        /// <summary>
        /// Try to parse a DType.
        /// </summary>
        public bool TryParse(out DType type)
        {
            if (!TryParseCore(out type))
                return false;
            SkipSpace();
            return true;
        }

        private void SkipSpace()
        {
            while (_ich < _str.Length && _str[_ich] == ' ')
                _ich++;
        }

        private bool TryParseCore(out DType type)
        {
            type = default;

            SkipSpace();
            if (End)
                return false;

            DType res;
            char ch = _str[_ich++];
            switch (ch)
            {
            default:
                return false;

            case 'g':
                res = DType.General;
                break;
            case 'v':
                res = DType.Vac;
                break;
            case 'o':
                res = DType.Null;
                break;
            case 'b':
                res = DType.BitReq;
                break;
            case 'n':
                res = DType.R8Req;
                break;
            case 'r':
                res = DType.R8Req;
                if (!End)
                {
                    switch (_str[_ich])
                    {
                    case '8':
                        res = DType.R8Req;
                        _ich++;
                        break;
                    case '4':
                        res = DType.R4Req;
                        _ich++;
                        break;
                    default:
                        break;
                    }
                }
                break;
            case 'i':
                res = DType.IAReq;
                if (!End)
                {
                    switch (_str[_ich])
                    {
                    case '8':
                        res = DType.I8Req;
                        _ich++;
                        break;
                    case '4':
                        res = DType.I4Req;
                        _ich++;
                        break;
                    case '2':
                        res = DType.I2Req;
                        _ich++;
                        break;
                    case '1':
                        res = DType.I1Req;
                        _ich++;
                        break;
                    case 'a':
                        res = DType.IAReq;
                        _ich++;
                        break;
                    default:
                        break;
                    }
                }
                break;
            case 'u':
                if (End)
                    return false;
                switch (_str[_ich])
                {
                case '8':
                    res = DType.U8Req;
                    break;
                case '4':
                    res = DType.U4Req;
                    break;
                case '2':
                    res = DType.U2Req;
                    break;
                case '1':
                    res = DType.U1Req;
                    break;
                default:
                    return false;
                }
                _ich++;
                break;
            case 's':
                res = DType.Text;
                break;
            case 'd':
                res = DType.DateReq;
                break;
            case 't':
                res = DType.TimeReq;
                break;
            case 'G':
                res = DType.GuidReq;
                break;
            case 'U':
                // Uri. This uses a sequence of possibly quoted identifiers, separated by '.' as the flavor.
                // REVIEW: Should this allow whitespace around the "tokens"?
                {
                    if (End || _str[_ich] != '<')
                        return false;
                    _ich++;
                    NPath flavor = NPath.Root;
                    for (; ; )
                    {
                        if (End)
                            return false;
                        if (_str[_ich] == '>')
                            break;
                        if (flavor.NameCount > 0)
                        {
                            if (_str[_ich] != '.')
                                return false;
                            if (++_ich >= _str.Length)
                                return false;
                        }
                        if (!LexUtils.TryLexName(ref _ich, _str, out DName name))
                            return false;
                        flavor = flavor.Append(name);
                    }
                    Validation.Assert(!End && _str[_ich] == '>');
                    _ich++;
                    res = DType.CreateUriType(flavor);
                    // Allow a trailing '?' to support when Uri was not always opt.
                    if (!End && _str[_ich] == '?')
                        _ich++;
                }
                break;

            case '{':
                if (!TryParseRecType(out res))
                    return false;
                break;
            case 'M':
                if (!TryParseModType(out res))
                    return false;
                break;
            case '(':
                if (!TryParseTupleType(out res))
                    return false;
                break;
            }

            Validation.Assert(res.SeqCount == 0);
            if (!res.IsOpt && !End && _str[_ich] == '?')
            {
                res = res.ToOpt();
                _ich++;
            }

            // Process trailing stars and tensor dimensions.
            int stars = 0;
            for (; ; )
            {
                while (!End && _str[_ich] == '*')
                {
                    res = res.ToSequence();
                    _ich++;
                    stars++;
                }

                if (End || _str[_ich] != '[')
                    break;

                // Tensor dimensions. Note that tensor types formerly included shape range information,
                // so we parse anything that includes such ranges.
                Validation.Assert(_str[_ich] == '[');
                if (!TryParseDims(out var shapeMin, out _))
                    return false;
                bool opt = false;
                if (!End && _str[_ich] == '?')
                {
                    opt = true;
                    _ich++;
                }
                // All we need is the rank, not actual shape information.
                res = res.ToTensor(opt, shapeMin.Rank);
            }

            type = res;
            return true;
        }

        /// <summary>
        /// Parse a record.
        /// </summary>
        private bool TryParseRecType(out DType type)
        {
            type = default;

            DType typeRec = DType.EmptyRecordReq;
            bool needComma = false;
            for (; ; )
            {
                SkipSpace();
                if (End)
                    return false;
                if (_str[_ich] == ',')
                {
                    needComma = false;
                    _ich++;
                    continue;
                }
                if (_str[_ich] == '}')
                {
                    // End of the record.
                    _ich++;
                    type = typeRec;
                    return true;
                }
                if (needComma)
                    return false;

                // Lex the field name.
                if (!LexUtils.TryLexName(ref _ich, _str, out DName name))
                    return false;
                if (typeRec.Contains(name))
                    return false;

                SkipSpace();
                if (End)
                    return false;

                // Digest the colon.
                if (_str[_ich] != ':')
                    return false;
                _ich++;

                // Get the type.
                if (!TryParseCore(out DType typeFld))
                    return false;

                typeRec = typeRec.AddNameType(name, typeFld);
                needComma = true;
            }
        }

        /// <summary>
        /// Parse a module.
        /// </summary>
        private bool TryParseModType(out DType type)
        {
            type = default;

            if (End)
                return false;
            if (_str[_ich] != '{')
                return false;
            _ich++;

            DType typeMod = DType.EmptyModuleReq;
            bool needComma = false;
            for (; ; )
            {
                SkipSpace();
                if (End)
                    return false;
                if (_str[_ich] == ',')
                {
                    needComma = false;
                    _ich++;
                    continue;
                }
                if (_str[_ich] == '}')
                {
                    // End of the module.
                    _ich++;
                    type = typeMod;
                    return true;
                }
                if (needComma)
                    return false;

                // Lex the kind.
                if (!LexUtils.TryLexName(ref _ich, _str, out DName kind))
                    return false;
                ModSymKind sk;
                switch (kind.Value)
                {
                case "param":
                case "P":
                    sk = ModSymKind.Parameter;
                    break;
                case "const":
                case "K":
                    sk = ModSymKind.Constant;
                    break;
                case "var":
                case "V":
                    sk = ModSymKind.FreeVariable;
                    break;
                case "let":
                case "L":
                    sk = ModSymKind.Let;
                    break;
                case "msr":
                case "M":
                    sk = ModSymKind.Measure;
                    break;
                case "con":
                case "C":
                    sk = ModSymKind.Constraint;
                    break;
                default:
                    return false;
                }

                SkipSpace();
                if (End)
                    return false;

                if (_str[_ich] == '!')
                {
                    _ich++;
                    SkipSpace();
                    if (End)
                        return false;
                }

                // Lex the symbol name.
                if (!LexUtils.TryLexName(ref _ich, _str, out DName name))
                    return false;

                if (typeMod.Contains(name))
                    return false;

                SkipSpace();
                if (End)
                    return false;

                // Digest the colon.
                if (_str[_ich] != ':')
                    return false;
                _ich++;

                // Get the type.
                if (!TryParseCore(out DType typeFld))
                    return false;

                typeMod = typeMod.AddNameType(name, typeFld, sk);
                needComma = true;
            }
        }

        /// <summary>
        /// Parse a tuple type.
        /// </summary>
        private bool TryParseTupleType(out DType type)
        {
            type = default;

            SkipSpace();
            if (End)
                return false;

            TypeTuple.Builder? bldr = null;
            for (; ; )
            {
                if (_str[_ich] == ')')
                {
                    // End of the tuple.
                    _ich++;
                    TupleInfo info = bldr == null ? TupleInfo.Empty : TupleInfo.Create(bldr.ToImmutable());
                    type = new DType(DKind.Tuple, detail: info);
                    return true;
                }
                if (bldr != null)
                {
                    if (_str[_ich] != ',')
                        return false;
                    _ich++;
                }

                if (!TryParseCore(out DType typeCur))
                    return false;

                SkipSpace();
                if (End)
                    return false;

                if (bldr == null)
                {
                    if (_str[_ich] == '|')
                    {
                        // Alternatives.
                        if (!_alts)
                            return false;
                        if (!TryParseAlts(ref typeCur))
                            return false;
                        type = typeCur;
                        return true;
                    }

                    bldr = TypeTuple.CreateBuilder();
                }

                bldr.Add(typeCur);
            }
        }

        private bool TryParseAlts(ref DType type)
        {
            Validation.Assert(_alts);
            Validation.Assert(type.IsValid);
            Validation.Assert(!End);
            Validation.Assert(_str[_ich] == '|');

            _ich++;
            for (; ; )
            {
                if (!TryParseCore(out var typeCur))
                    return false;
                type = DType.GetSuperType(type, typeCur, _union);

                SkipSpace();
                if (End)
                    return false;

                if (_str[_ich] == ')')
                {
                    _ich++;
                    return true;
                }
                if (_str[_ich] != '|')
                    return false;
                _ich++;
            }
        }

        /// <summary>
        /// Parse tensor dimensions. Note that this full functionality was needed when tensor
        /// types included shape range information. This is no longer the case. Nevertheless,
        /// we want to continue parsing type specifications that include shape range information.
        /// </summary>
        private bool TryParseDims(out Shape shapeMin, out Shape shapeMax)
        {
            Validation.AssertValue(_str);
            Validation.Assert(0 <= _ich & _ich < _str.Length && _str[_ich] == '[');

            shapeMin = default(Shape);
            shapeMax = default(Shape);

            _ich++;
            bool same = true;
            var bldrMin = Shape.CreateBuilder();
            var bldrMax = Shape.CreateBuilder();
            for (; ; )
            {
                while (_ich < _str.Length && _str[_ich] == ' ')
                    _ich++;
                if (_ich >= _str.Length)
                    return false;
                if (_str[_ich] == ']')
                {
                    _ich++;
                    break;
                }
                if (bldrMin.Count > 0)
                {
                    if (_str[_ich] != ',')
                        return false;
                    _ich++;
                    while (_ich < _str.Length && _str[_ich] == ' ')
                        _ich++;
                    if (_ich >= _str.Length)
                        return false;
                }

                long dimMin;
                long dimMax;

                Validation.Assert(_ich < _str.Length);
                char ch = _str[_ich++];
                if (ch == '*')
                {
                    dimMin = 0;
                    dimMax = Shape.DimMax;
                }
                else if ('0' <= ch && ch <= '9')
                {
                    dimMin = ch - '0';
                    ParseOneDim(ref dimMin);
                    dimMax = dimMin;
                }
                else
                    return false;

                if (_ich >= _str.Length)
                    return false;
                if (_str[_ich] == '-')
                {
                    if (++_ich >= _str.Length)
                        return false;
                    ch = _str[_ich++];
                    if (ch == '*')
                        dimMax = Shape.DimMax;
                    else if ('0' <= ch && ch <= '9')
                    {
                        dimMax = ch - '0';
                        ParseOneDim(ref dimMax);
                    }
                    else
                        return false;
                }

                if (dimMin > dimMax)
                    return false;

                if (dimMin < dimMax)
                    same = false;
                bldrMin.Add(dimMin);
                bldrMax.Add(dimMax);
            }

            shapeMin = bldrMin.ToImmutable();
            shapeMax = same ? shapeMin : bldrMax.ToImmutable();
            return true;
        }

        /// <summary>
        /// Parses the remainder of a numeric dimension, assuming the first digit has already been digested.
        /// Clips the result to <see cref="Shape.DimMax"/>.
        /// </summary>
        private void ParseOneDim(ref long dim)
        {
            Validation.Assert(0 <= dim & dim <= 9);
            char ch;
            long val = dim;
            bool overflow = false;
            while (_ich < _str.Length && '0' <= (ch = _str[_ich]) && ch <= '9')
            {
                _ich++;
                if (val > Shape.DimMax / 10)
                    overflow = true;
                val = val * 10 + (ch - '0');
                if (val < 0)
                    overflow = true;
            }
            if (overflow)
                dim = Shape.DimMax;
            else
                dim = val;
        }
    }
}
