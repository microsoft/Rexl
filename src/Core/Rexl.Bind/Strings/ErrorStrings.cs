// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// WARNING: This .cs file is generated from the corresponding .tt file. DO NOT edit this .cs directly.

namespace Microsoft.Rexl;

static partial class ErrorStrings
{

    public static readonly StringId InfoNode_Node = new(nameof(InfoNode_Node), @"Node: {0}");
    public static readonly StringId InfoTok_Tok = new(nameof(InfoTok_Tok), @"Tok: '{0}'");
    public static readonly StringId InfoTokNode_Tok_Node = new(nameof(InfoTokNode_Tok_Node), @"Node: {0}, Tok: '{1}'");
    public static readonly StringId FormatSpan_Min_Lim = new(nameof(FormatSpan_Min_Lim), @"({0},{1}) ");
    public static readonly StringId FormatRange_LineMin_ColMin_LineLim_ColLim = new(nameof(FormatRange_LineMin_ColMin_LineLim_ColLim), @"({0},{1})-({2},{3}) ");
    public static readonly StringId FormatRange_Min_Lim_LineMin_ColMin_LineLim_ColLim = new(nameof(FormatRange_Min_Lim_LineMin_ColMin_LineLim_ColLim), @"({0}-{1};{2}:{3}-{4}:{5}) ");
    public static readonly StringId FormatErrorSeparator = new(nameof(FormatErrorSeparator), @", ");

    public static readonly StringId ErrInternalError = new(nameof(ErrInternalError), @"Internal error");
    public static readonly StringId ErrNYI = new(nameof(ErrNYI), @"NYI");

    public static readonly StringId ErrUnterminatedText = new(nameof(ErrUnterminatedText), @"Text literal needs a closing double quote");
    public static readonly StringId ErrBadTextEscape = new(nameof(ErrBadTextEscape), @"Bad escape in text literal");
    public static readonly StringId ErrUnterminatedQuotedIdentifier = new(nameof(ErrUnterminatedQuotedIdentifier), @"Quoted identifier needs a closing single quote");
    public static readonly StringId ErrRuleNestedTooDeeply = new(nameof(ErrRuleNestedTooDeeply), @"Expression is too complex");
    public static readonly StringId ErrGlobalIdentNotAllowed = new(nameof(ErrGlobalIdentNotAllowed), @"Globally scoped identifier not allowed");
    public static readonly StringId ErrBadMetaProp = new(nameof(ErrBadMetaProp), @"Unexpected `$`. Did you intend `.`?");
    public static readonly StringId ErrNeedSequenceForIn = new(nameof(ErrNeedSequenceForIn), @"Need a sequence on the right of in");
    public static readonly StringId ErrNeedSequence_Slot_Func = new(nameof(ErrNeedSequence_Slot_Func), @"The argument in position {0} of '{1}' should be a sequence");
    public static readonly StringId ErrNeedTensor_Slot_Func = new(nameof(ErrNeedTensor_Slot_Func), @"The argument in position {0} of '{1}' should be a tensor");
    public static readonly StringId ErrNeedName_Slot_Func = new(nameof(ErrNeedName_Slot_Func), @"The argument in position {0} of '{1}' should include a name ('name: value')");
    public static readonly StringId ErrNotGroupableType_Type = new(nameof(ErrNotGroupableType_Type), @"Invalid key type for GroupBy: '{0}'");
    public static readonly StringId ErrNotJoinableType_Type = new(nameof(ErrNotJoinableType_Type), @"Invalid key type for KeyJoin: '{0}'");
    public static readonly StringId ErrNotSortableType_Type = new(nameof(ErrNotSortableType_Type), @"Invalid type for Sort: '{0}'");
    public static readonly StringId ErrNeedSortSelector_Type = new(nameof(ErrNeedSortSelector_Type), @"Need selector argument to sort type: '{0}'");
    public static readonly StringId ErrNeedFieldName_Slot_Func = new(nameof(ErrNeedFieldName_Slot_Func), @"The argument in position {0} of '{1}' should include a field name");
    public static readonly StringId ErrIfNeedsBoolCondition_Type_Type = new(nameof(ErrIfNeedsBoolCondition_Type_Type), @"The condition for 'if' must be type '{0}' instead of type '{1}'");
    public static readonly StringId ErrExecuteNeedsText_Type_Type = new(nameof(ErrExecuteNeedsText_Type_Type), @"The value for `execute` must be type '{0}' instead of type '{1}'");
    public static readonly StringId ErrUnboundBox = new(nameof(ErrUnboundBox), @"The target box, '_', can only be used on the right side of =>");
    public static readonly StringId ErrMultipleUseOfBox = new(nameof(ErrMultipleUseOfBox), @"The target box, '_', can be used only once on the right side of =>. Consider using 'With(x : _, ...)'");
    public static readonly StringId ErrUnknownFieldNameForDrop = new(nameof(ErrUnknownFieldNameForDrop), @"Unknown field name can't be dropped");
    public static readonly StringId ErrBadDirective = new(nameof(ErrBadDirective), @"Unexpected directive");
    public static readonly StringId ErrBadAutoDirective = new(nameof(ErrBadAutoDirective), @"'[auto]' directive requires just a field name");
    public static readonly StringId ErrBadName = new(nameof(ErrBadName), @"Unexpected name");
    public static readonly StringId ErrBadName_Guess = new(nameof(ErrBadName_Guess), @"Unexpected name, did you intend '{0}'?");
    public static readonly StringId ErrGroupByNeedsKey = new(nameof(ErrGroupByNeedsKey), @"GroupBy needs at least one key");
    public static readonly StringId ErrFloatUnderflow = new(nameof(ErrFloatUnderflow), @"Constant floating point underflow to zero");
    public static readonly StringId ErrUnsupportedRatSuffix = new(nameof(ErrUnsupportedRatSuffix), @"Unsupported rational type suffix");
    public static readonly StringId ErrNeedArg_Name = new(nameof(ErrNeedArg_Name), @"Missing argument: '{0}'");
    public static readonly StringId ErrInequatableType_Type = new(nameof(ErrInequatableType_Type), @"Equality comparison is not supported for items of type '{0}'");
    public static readonly StringId ErrIncomparableType_Type = new(nameof(ErrIncomparableType_Type), @"Ordered comparison is not supported for items of type '{0}'");
    public static readonly StringId ErrIncomparableTypes_Type_Type = new(nameof(ErrIncomparableTypes_Type_Type), @"The given types are not comparable: '{0}' and '{1}'");
    public static readonly StringId ErrBadIt = new(nameof(ErrBadIt), @"There are no values in scope for 'it'");
    public static readonly StringId ErrBadModuleIt = new(nameof(ErrBadModuleIt), @"A module can't be referenced via 'it'");
    public static readonly StringId ErrBadItSlot = new(nameof(ErrBadItSlot), @"Invalid scope index; there are not enough values in scope");
    public static readonly StringId ErrBadItInd = new(nameof(ErrBadItInd), @"There are no indexed values in scope for '#'");
    public static readonly StringId ErrBadItIndSlot = new(nameof(ErrBadItIndSlot), @"Invalid scope index; there are not enough indexed values in scope");
    public static readonly StringId ErrDuplicateFieldName_Name = new(nameof(ErrDuplicateFieldName_Name), @"Duplicate field name: '{0}'");
    public static readonly StringId ErrDuplicateParamName_Name = new(nameof(ErrDuplicateParamName_Name), @"Duplicate parameter name: '{0}'");
    public static readonly StringId ErrDuplicateParamName_Name_Diff = new(nameof(ErrDuplicateParamName_Name_Diff), @"Parameter name '{0}' already specified {1} position(s) prior");
    public static readonly StringId ErrNameDoesNotExist = new(nameof(ErrNameDoesNotExist), @"Name does not exist in the current context");
    public static readonly StringId ErrNameDoesNotExist_Guess = new(nameof(ErrNameDoesNotExist_Guess), @"Name does not exist in the current context, did you intend '{0}'?");
    public static readonly StringId ErrFieldDoesNotExist_Type = new(nameof(ErrFieldDoesNotExist_Type), @"Field does not exist in type: '{0}'");
    public static readonly StringId ErrFieldDoesNotExist_Type_Guess = new(nameof(ErrFieldDoesNotExist_Type_Guess), @"Unknown field, did you intend '{1}' in type: '{0}'");
    public static readonly StringId ErrNamespaceNotValue = new(nameof(ErrNamespaceNotValue), @"Namespace can't be used as value");
    public static readonly StringId ErrBadNamespace = new(nameof(ErrBadNamespace), @"Unknown namespace");
    public static readonly StringId ErrBadNamespaceChild_Ns_Child = new(nameof(ErrBadNamespaceChild_Ns_Child), @"Namespace '{0}' doesn't contain '{1}'");
    public static readonly StringId ErrBadNamespaceChild_Ns_Child_Guess = new(nameof(ErrBadNamespaceChild_Ns_Child_Guess), @"Namespace '{0}' doesn't contain '{1}', did you intend '{2}'?");
    public static readonly StringId ErrBadNot = new(nameof(ErrBadNot), @"Invalid 'not' operator, consider `!` or parentheses");
    public static readonly StringId ErrBadBnot = new(nameof(ErrBadBnot), @"Invalid 'bnot' operator, consider `~` or parentheses");
    public static readonly StringId ErrBadTok_Cur_Instead = new(nameof(ErrBadTok_Cur_Instead), @"Invalid '{0}', did you intend '{1}'?");
    public static readonly StringId ErrBadNumLiteral = new(nameof(ErrBadNumLiteral), @"Invalid numeric literal");
    public static readonly StringId ErrInvalidThis = new(nameof(ErrInvalidThis), @"'this' is not valid in the current context");
    public static readonly StringId ErrOperandExpected = new(nameof(ErrOperandExpected), @"Expected an operand");
    public static readonly StringId ErrBadToken = new(nameof(ErrBadToken), @"Unexpected token");
    public static readonly StringId ErrRedundantToken_Tok = new(nameof(ErrRedundantToken_Tok), @"Redundant '{0}'");
    public static readonly StringId ErrConflictingCmpModifier_Bad_Good = new(nameof(ErrConflictingCmpModifier_Bad_Good), @"Comparison operator modifier '{0}' conflicts with '{1}'");
    public static readonly StringId ErrExpectedFound_Ex_Fnd = new(nameof(ErrExpectedFound_Ex_Fnd), @"Expected: '{0}', Found: '{1}'");
    public static readonly StringId ErrInvalidDot = new(nameof(ErrInvalidDot), @"Invalid use of '.'");
    public static readonly StringId ErrNotTuple = new(nameof(ErrNotTuple), @"Operand must be a tuple");
    public static readonly StringId ErrNotRecord = new(nameof(ErrNotRecord), @"Operand must be a record");
    public static readonly StringId ErrNotModule = new(nameof(ErrNotModule), @"Operand must be a module");
    public static readonly StringId ErrNotIndexable = new(nameof(ErrNotIndexable), @"Operand must be either a tensor or a tuple");
    public static readonly StringId ErrWrongNumberOfTensorIndices_Rank = new(nameof(ErrWrongNumberOfTensorIndices_Rank), @"Number of indices must not exceed the rank of the tensor, which is {0}");
    public static readonly StringId ErrUnexpectedIndex = new(nameof(ErrUnexpectedIndex), "Expected a single index or slice");
    public static readonly StringId ErrExpectedIndexOrSlice = new(nameof(ErrExpectedIndexOrSlice), "Expected an index or a slice");
    public static readonly StringId ErrArgIntLit = new(nameof(ErrArgIntLit), @"The argument must be an integer literal");
    public static readonly StringId ErrArgIntLit_Min_Lim = new(nameof(ErrArgIntLit_Min_Lim), @"The argument must be an integer literal with value at least {0} and less than {1}");
    public static readonly StringId ErrHetTupleIndexOutOfRange_Arity = new(nameof(ErrHetTupleIndexOutOfRange_Arity), @"Index is out of range, should be at least 0 and less than {0}");
    public static readonly StringId ErrHetTupleOffsetOutOfRange_Max = new(nameof(ErrHetTupleOffsetOutOfRange_Max), @"Index from end is out of range, should be at least ^1 and at most ^{0}");
    public static readonly StringId ErrTupleNegativeIndex_Index = new(nameof(ErrTupleNegativeIndex_Index), @"Negative tuple index not supported, use '^{0}' to index from the end of the tuple");
    public static readonly StringId ErrBadTupleSliceRange = new(nameof(ErrBadTupleSliceRange), @"Invalid tuple slice, all indices must be either integer literals or omitted");
    public static readonly StringId ErrBadHetTupleIndex = new(nameof(ErrBadHetTupleIndex), @"Invalid item index for a heterogeneous tuple, should be a constant integer");
    public static readonly StringId ErrEmptyTupleNoIndexing = new(nameof(ErrEmptyTupleNoIndexing), @"An empty tuple can't be indexed");
    public static readonly StringId ErrEmptyTupleNoItems = new(nameof(ErrEmptyTupleNoItems), @"An empty tuple has no items");
    public static readonly StringId ErrTupleTooSmall_Has_Need = new(nameof(ErrTupleTooSmall_Has_Need), @"Tuple operand has {0} items, needs at least {1}");
    public static readonly StringId ErrUnknownFunction = new(nameof(ErrUnknownFunction), @"Invocation of unknown or unsupported function");
    public static readonly StringId ErrUnknownFunction_Guess = new(nameof(ErrUnknownFunction_Guess), @"Invocation of unknown function, did you intend '{0}'?");
    public static readonly StringId ErrUnknownFunction_Proc = new(nameof(ErrUnknownFunction_Proc), @"Procedure '{0}' cannot be used as a function");
    public static readonly StringId ErrUnknownProp_Guess = new(nameof(ErrUnknownProp_Guess), @"Unknown property, did you intend '{0}'?");
    public static readonly StringId ErrFuncUsedAsProc = new(nameof(ErrFuncUsedAsProc), @"Function used as procedure");
    public static readonly StringId ErrProcCallExpected = new(nameof(ErrProcCallExpected), @"Expected a procedure invocation");
    public static readonly StringId ErrBadArity = new(nameof(ErrBadArity), @"Invalid number of arguments");
    public static readonly StringId ErrArityTooSmall_Path_Num = new(nameof(ErrArityTooSmall_Path_Num), @"Too few arguments for {0}, expected {1} additional");
    public static readonly StringId ErrArityTooBig_Path_Num = new(nameof(ErrArityTooBig_Path_Num), @"Too many arguments for {0}, expected {1} fewer");
    public static readonly StringId ErrBadType_Src_Dst = new(nameof(ErrBadType_Src_Dst), @"Invalid operand type: cannot convert type '{0}' to '{1}'");
    public static readonly StringId ErrBadOperatorForType_Op_Left_Right = new(nameof(ErrBadOperatorForType_Op_Left_Right), @"Operator '{0}' not valid for types '{1}' and '{2}'");
    public static readonly StringId ErrBadTypeScope_Src_Dst = new(nameof(ErrBadTypeScope_Src_Dst), @"Invalid operand type for scope slot: given type '{0}', need type '{1}'");
    public static readonly StringId ErrIncompatibleTypes_Type_Type = new(nameof(ErrIncompatibleTypes_Type_Type), @"The given types are incompatible: '{0}' and '{1}'");
    public static readonly StringId ErrOperatorExpected = new(nameof(ErrOperatorExpected), @"Expected an operator");
    public static readonly StringId ErrComparisonOperatorExpected = new(nameof(ErrComparisonOperatorExpected), @"Expected a comparison operator");
    public static readonly StringId ErrOperatorExpected_Name_Guess = new(nameof(ErrOperatorExpected_Name_Guess), @"Expected an operator but got '{0}'. Did you intend '{1}'?");
    public static readonly StringId ErrUseFuzzy_Name_Guess = new(nameof(ErrUseFuzzy_Name_Guess), @"Expected '{1}' but got '{0}'");
    public static readonly StringId ErrEmptyInvalidIdentifier = new(nameof(ErrEmptyInvalidIdentifier), @"The identifier is invalid.");
    public static readonly StringId ErrRecursiveUdf = new(nameof(ErrRecursiveUdf), @"Recursion not supported in user defined functions.");
    public static readonly StringId ErrNoOverload = new(nameof(ErrNoOverload), @"No overload can be found matching these names and directives.");
    public static readonly StringId ErrNotActiveScope = new(nameof(ErrNotActiveScope), @"Operand must be an active scope");
    public static readonly StringId ErrNotIndexedScope = new(nameof(ErrNotIndexedScope), @"Operand must be an indexed scope");
    public static readonly StringId ErrBadVolatileCall = new(nameof(ErrBadVolatileCall), @"Volatile function calls aren't permitted in this context");
    public static readonly StringId ErrBadVolatileArg_Slot = new(nameof(ErrBadVolatileArg_Slot), @"Argument in position {0} can't be volatile");
    public static readonly StringId ErrBadVolatileKey_Func = new(nameof(ErrBadVolatileKey_Func), @"Key in '{0}' can't be volatile");

    public static readonly StringId ErrModuleDuplicateSymbolName_Name = new(nameof(ErrModuleDuplicateSymbolName_Name), @"Duplicate module symbol name: '{0}'");
    public static readonly StringId ErrModuleDuplicateVarDomainClause = new(nameof(ErrModuleDuplicateVarDomainClause), @"Duplicate free variable domain clause");
    public static readonly StringId ErrModuleConstCantReferenceVar = new(nameof(ErrModuleConstCantReferenceVar), @"A module constant can't reference a variable");
    public static readonly StringId ErrModuleParamDefCantReferenceVar = new(nameof(ErrModuleParamDefCantReferenceVar), @"The default expression for a module parameter can't reference a variable");
    public static readonly StringId ErrModuleVarDomCantReferenceVar = new(nameof(ErrModuleVarDomCantReferenceVar), @"A domain expression for a module free variable can't reference a variable");
    public static readonly StringId ErrModuleFreeWithMustBeSeq_Type = new(nameof(ErrModuleFreeWithMustBeSeq_Type), @"The 'with' expression for a module free variable must have a sequence type, not: '{0}'");
    public static readonly StringId ErrModuleFreeInMustBeSeq_Type = new(nameof(ErrModuleFreeInMustBeSeq_Type), @"The 'in' expression for a module free variable must have a sequence type, not: '{0}'");
    public static readonly StringId ErrModuleFreeWithNeedsBnds = new(nameof(ErrModuleFreeWithNeedsBnds), @"A module free 'with' variable needs 'from', 'to', or 'default'");
    public static readonly StringId ErrModuleFreeConflict_Kwd_Kwd = new(nameof(ErrModuleFreeConflict_Kwd_Kwd), @"A module free variable can't have both '{0}' and '{1}'");
    public static readonly StringId ErrModuleFreeIllegalTo = new(nameof(ErrModuleFreeIllegalTo), @"A module free variable can't have 'to' when its domain is a sequence");
    public static readonly StringId ErrModuleConMustBeBool_Type = new(nameof(ErrModuleConMustBeBool_Type), @"A module constraint must be of boolean type, not: '{0}'");
    public static readonly StringId ErrModuleUnknown = new(nameof(ErrModuleUnknown), @"Unknown module");
    public static readonly StringId ErrModuleUnknownMeasure_Name = new(nameof(ErrModuleUnknownMeasure_Name), @"Unknown measure: '{0}'");
    public static readonly StringId ErrModuleNeedRecord = new(nameof(ErrModuleNeedRecord), @"Module 'as' needs record value");
    public static readonly StringId ErrModuleUnknownSymbol_Name = new(nameof(ErrModuleUnknownSymbol_Name), @"Record field name is not a symbol in the source module: '{0}'");
    public static readonly StringId ErrModuleSymbolNotSettable_Name = new(nameof(ErrModuleSymbolNotSettable_Name), @"Record field name is not a settable symbol in the source module: '{0}'");
    public static readonly StringId ErrModuleNotSupported = new(nameof(ErrModuleNotSupported), @"Modules are not supported by this host");
    public static readonly StringId ErrModuleOptNeedMeasure = new(nameof(ErrModuleOptNeedMeasure), @"The objective for an optimize function must be a measure in the module");

    // REVIEW: Need a better message.
    public static readonly StringId ErrModuleTypeMismatch_Name = new(nameof(ErrModuleTypeMismatch_Name), @"Type mismatch for symbol '{0}'");
    public static readonly StringId ErrModuleSpecializationFailed = new(nameof(ErrModuleSpecializationFailed), @"Module specialization failed");
    public static readonly StringId WrnModuleBadReq = new(nameof(WrnModuleBadReq), @"Ignoring unexpected 'req'");
    public static readonly StringId ErrTaskWithNeedRecord = new(nameof(ErrTaskWithNeedRecord), @"Task 'with' needs record value");
    public static readonly StringId ErrTaskUnknown = new(nameof(ErrTaskUnknown), @"Unknown task");
    public static readonly StringId ErrMetaContainerUnknown = new(nameof(ErrMetaContainerUnknown), @"Unknown meta property container");
    public static readonly StringId ErrMetaPropUnknown_Name = new(nameof(ErrMetaPropUnknown_Name), @"Container doesn't have meta property '{0}'");
    public static readonly StringId ErrSerializeG = new(nameof(ErrSerializeG), @"Cannot serialize types containing the general type");
    public static readonly StringId ErrBadStreamUse = new(nameof(ErrBadStreamUse), @"Improper use of a streaming task result");
    public static readonly StringId ErrInvalidFlavor_Flavor = new(nameof(ErrInvalidFlavor_Flavor), @"Invalid flavor: {0}");
    public static readonly StringId ErrNonTextLiteralFlavor = new(nameof(ErrNonTextLiteralFlavor), @"Flavor is not a text literal");

    public static readonly StringId ErrBadIndexModifierInSlice_Tok = new(nameof(ErrBadIndexModifierInSlice_Tok), @"Index modifier '{0}' not allowed in slice");
    public static readonly StringId ErrBadTimesModifierInSlice_Tok = new(nameof(ErrBadTimesModifierInSlice_Tok), @"Index modifier '{0}' only allowed for stop value");
    public static readonly StringId ErrBadIndexModifierWithTuple_Tok = new(nameof(ErrBadIndexModifierWithTuple_Tok), @"Index modifier '{0}' not allowed with tuple slice value");
    public static readonly StringId ErrConflictingIndexModifier_Bad_Good = new(nameof(ErrConflictingIndexModifier_Bad_Good), @"Index modifier '{0}' conflicts with '{1}'");

    public static readonly StringId ErrBadLabel = new(nameof(ErrBadLabel), @"A nested statement cannot have a label");
    public static readonly StringId ErrDuplicateLabel = new(nameof(ErrDuplicateLabel), @"Duplicate label");
    public static readonly StringId ErrUnknownLabel = new(nameof(ErrUnknownLabel), @"Label not found");
    public static readonly StringId ErrCantJumpInto = new(nameof(ErrCantJumpInto), @"Can't jump into a block. See next error.");
    public static readonly StringId ErrLabelInBlock = new(nameof(ErrLabelInBlock), @"Can't be targeted by jump outside the block");
    public static readonly StringId ErrInfiniteLoop = new(nameof(ErrInfiniteLoop), @"Infinite loop");

    public static readonly StringId ErrImportBadType_Type = new(nameof(ErrImportBadType_Type), @"Unhandled import type: '{0}'");
    public static readonly StringId ErrImportBadFlavor_Flavor = new(nameof(ErrImportBadFlavor_Flavor), @"Expected link flavor 'Text.Rexl', not: '{0}'");
    public static readonly StringId ErrImportEmptyPath = new(nameof(ErrImportEmptyPath), @"Empty import path");
    public static readonly StringId ErrExecTooDeep = new(nameof(ErrExecTooDeep), @"Nesting of 'import' and 'execute' is too deep");

    public static readonly StringId ErrCmpModOn_Oper_Mod = new(nameof(ErrCmpModOn_Oper_Mod), @"The '{0}' operator doesn't support the comparison modifier '{1}'");

    public static readonly StringId WrnFloatOverflow = new(nameof(WrnFloatOverflow), @"Constant floating point overflow to infinity");
    public static readonly StringId WrnIntOverflow = new(nameof(WrnIntOverflow), @"Constant integer overflow");
    public static readonly StringId WrnU8ToI8 = new(nameof(WrnU8ToI8), @"Conversion from unsigned to signed integer can reinterpret large values as negative");
    public static readonly StringId WrnIntDivZero = new(nameof(WrnIntDivZero), @"Integer division by zero");
    public static readonly StringId WrnCoalesceLeftNotOpt_Type = new(nameof(WrnCoalesceLeftNotOpt_Type), @"Coalesce operator '??' is not necessary with left operand of non optional type: '{0}'");
    public static readonly StringId WrnTypeNoReq_Type = new(nameof(WrnTypeNoReq_Type), @"The type has no required form: '{0}'");
    public static readonly StringId WrnTypeAlreadyOpt_Type = new(nameof(WrnTypeAlreadyOpt_Type), @"The type is already optional: '{0}'");
    public static readonly StringId WrnUnusedPipeValue = new(nameof(WrnUnusedPipeValue), @"The right side of | should contain the target box, '_'");
    public static readonly StringId WrnIntLiteralOutOfRange = new(nameof(WrnIntLiteralOutOfRange), @"Int literal out of range for specified type");
    public static readonly StringId WrnUnsignedIntLiteralOverflow = new(nameof(WrnUnsignedIntLiteralOverflow), @"Unsigned int literal overflow as signed");
    public static readonly StringId WrnShruOn_Type = new(nameof(WrnShruOn_Type), @"Shift right unsigned operator ('shru') on type '{0}' interpreted as signed ('shri')");
    public static readonly StringId WrnSecondArgumentNotUsed_Func = new(nameof(WrnSecondArgumentNotUsed_Func), @"Second argument of '{0}' is not used");
    public static readonly StringId WrnTensorDimTooSmall = new(nameof(WrnTensorDimTooSmall), @"Tensor dimension should be non-negative");
    public static readonly StringId WrnOutOfRange_Min_Lim = new(nameof(WrnOutOfRange_Min_Lim), @"The number should be at least {0} and less than {1}");
    public static readonly StringId WrnAxisAlreadySpecified = new(nameof(WrnAxisAlreadySpecified), @"The {0} axis has already been specified");
    public static readonly StringId WrnCmpCi_Type = new(nameof(WrnCmpCi_Type), @"Case insensitive comparison doesn't apply to type '{0}'");
    public static readonly StringId WrnHomTupleIndexOutOfRange = new(nameof(WrnHomTupleIndexOutOfRange), @"Homogeneous tuple index out of range, this will produce the item type's default value");
    public static readonly StringId WrnTensorIndexOutOfRange = new(nameof(WrnTensorIndexOutOfRange), @"Tensor index out of range, this will produce the item type's default value");
    public static readonly StringId WrnLikeSrcNotGeneral_Func_Type = new(nameof(WrnLikeSrcNotGeneral_Func_Type), @"The '{0}' function isn't needed since the source argument type is not the general type: '{1}'");
    public static readonly StringId ErrLikeVal_Func_Type = new(nameof(ErrLikeVal_Func_Type), @"Illegal type for the value argument of '{0}' function: '{1}'");

    #region Deprecations
    public static readonly StringId WrnDeprecatedFunction = new(nameof(WrnDeprecatedFunction), @"This function is deprecated and may be removed in the future");
    public static readonly StringId WrnDeprecatedFunction_Alt = new(nameof(WrnDeprecatedFunction_Alt), @"This function is deprecated and may be removed in the future, use '{0}' instead");
    public static readonly StringId WrnDeprecatedUniOp_Old_New = new(nameof(WrnDeprecatedUniOp_Old_New), @"The unary operator '{0}' is deprecated, use '{1}' instead");
    public static readonly StringId WrnDeprecatedBinOp_Old_New = new(nameof(WrnDeprecatedBinOp_Old_New), @"The binary operator '{0}' is deprecated, use '{1}' instead");
    public static readonly StringId WrnDeprecatedToken_Old_New = new(nameof(WrnDeprecatedToken_Old_New), @"The token '{0}' is deprecated, use '{1}' instead");
    public static readonly StringId WrnDeprecatedTupleGetSlot = new(nameof(WrnDeprecatedTupleGetSlot), @"This form of tuple item access is deprecated, use '[<slot>]' instead");
    public static readonly StringId WrnDeprecatedStrCat = new(nameof(WrnDeprecatedStrCat), @"The binary operator '+' for text concatenation is deprecated, use '&' instead");
    #endregion Deprecations

    public static readonly StringId ErrCaughtException = new(nameof(ErrCaughtException), @"*** Exception! ***");
    public static readonly StringId ErrImportException_Msg = new(nameof(ErrImportException_Msg), @"*** Import Exception: {0}");

    #region Solvers
    public static readonly StringId ErrSolverUnkown_Name = new(nameof(ErrSolverUnkown_Name), @"Solver '{0}' unknown or not implemented");
    public static readonly StringId ErrSolverStartingSolverFailed_Name = new(nameof(ErrSolverStartingSolverFailed_Name), @"Failed to start solver: '{0}'");
    public static readonly StringId ErrSolverSolvingFailed = new(nameof(ErrSolverSolvingFailed), @"Solving failed!");
    public static readonly StringId ErrSolverInfeasible = new(nameof(ErrSolverInfeasible), @"Infeasible: contradictory constraints");
    public static readonly StringId ErrSolverUnbounded = new(nameof(ErrSolverUnbounded), @"Unbounded: possible missing constraints");
    public static readonly StringId ErrSolverBadConstraints = new(nameof(ErrSolverBadConstraints), @"Infeasible or unbounded: contradictory or insufficient constraints");
    public static readonly StringId ErrSolverConNotLinear_Name_Bnd = new(nameof(ErrSolverConNotLinear_Name_Bnd), @"Constraint '{0}' is not linear: {1}");
    public static readonly StringId ErrSolverDefNotLinear_Name_Bnd = new(nameof(ErrSolverDefNotLinear_Name_Bnd), @"Definition '{0}' is not linear: {1}");
    public static readonly StringId ErrSolverDomSeqEmpty_Name_Bnd = new(nameof(ErrSolverDomSeqEmpty_Name_Bnd), @"Domain sequence for '{0}' is empty: {1}");
    public static readonly StringId ErrSolverAddSymbolFailed_Name = new(nameof(ErrSolverAddSymbolFailed_Name), @"Adding module symbol '{0}' failed");
    public static readonly StringId ErrSolverBadTensorItemType_Name_Type = new(nameof(ErrSolverBadTensorItemType_Name_Type), @"Tensor variable '{0}' with unhandled item type: {1}");
    public static readonly StringId ErrSolverUnhandledVar_Name = new(nameof(ErrSolverUnhandledVar_Name), @"Unhandled variable: '{0}'");

    public static readonly StringId WrnSolverBadIntValue_Name_Val = new(nameof(WrnSolverBadIntValue_Name_Val), @"Integer variable '{0}' with bad value: {1}");
    public static readonly StringId WrnSolverBadBoolValue_Name_Val = new(nameof(WrnSolverBadBoolValue_Name_Val), @"Boolean variable '{0}' with bad value: {1}");
    public static readonly StringId WrnSolverVarBadType_Name_Val = new(nameof(WrnSolverVarBadType_Name_Val), @"Variable '{0}' with unexpected type: {1}");
    public static readonly StringId WrnSolverVarBadTenType_Name_Val = new(nameof(WrnSolverVarBadTenType_Name_Val), @"Tensor variable '{0}' with unexpected item type: {1}");
    #endregion Solvers

}
