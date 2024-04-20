# Rexl Lexical and Syntactic Grammars #

This document specifies the Rexl lexical and syntactic grammars. It does not specify semantics or provide
comprehensive examples.

There are two syntactic grammars, the **_expression grammar_** and the **_statement grammar_**. The **_expression
grammar_** is for **_core Rexl_**. The **_statement grammar_** includes the expression grammar and is for a scripting
language, **_RexlScript_**, that leverages core Rexl. The statement grammer is used by the Jupyter notebook
kernel, test suites, and the internal tools, `RexlBench` and `RexlRun`.

The input to the syntactic grammars is a sequence of tokens produced from the [lexical grammar](#lexical-grammar).
Comment tokens produced by the lexical grammar are removed and not passed to the syntactic grammar.

In the syntactic grammar productions, [tokens](#productions-for-token) from the lexical grammar are written
either as code literals (for punctuators and keywords) or as **_bold_** token kind names, as defined by
the [lexical grammar](#lexical-grammar).

When a production (non-terminal) is used, its name may be suffixed with _-opt_, indicating that the term is optional.

## Expression Grammar

The top level non-terminal in the syntactic expression grammar is _expr_, indicating an expression.

### Productions for _expr_

> _expr_ :  
>   &emsp;[_pipe-expr_](#productions-for-pipe-expr)  

### Productions for _pipe-expr_

The pipe operator replaces an occurrence of `_` on the right with the value on the left, for example,
`MySequence | Count(_)` is equivalent to `Count(MySequence)`. It is pure _syntactic sugar_.

> _pipe-expr_ :  
>   &emsp;[_if-expr_](#productions-for-if-expr)  
>   &emsp;_pipe-expr_&emsp;`|`&emsp;_if-expr_  

**REVIEW**: Should we support multiple targets on the right such as `F(3) | _ * _ + _`? The user can get around
the restriction by using the `With` function.

### Productions for _if-expr_

The _if-expr_ syntax mimics Python's inline conditional.

> _if-expr_ :  
>   &emsp;[_coalesce-expr_](#productions-for-coalesce-expr)  
>   &emsp;_coalesce-expr_&emsp;`if`&emsp;_expr_&emsp;`else`&emsp;_if-expr_  

### Productions for _coalesce-expr_

The coalesce operator is right associative.

> _coalesce-expr_ :  
>   &emsp;[_or-expr_](#productions-for-logical-operator-expressions)  
>   &emsp;_or-expr_&emsp;`??`&emsp;_coalesce-expr_  

### Productions for logical operator expressions

The logical operators use keywords (all but `not` are contextual).

> _or-expr_ :  
>   &emsp;_xor-expr_  
>   &emsp;_or-expr_&emsp;`or`&emsp;_xor-expr_  
>
> _xor-expr_ :  
>   &emsp;_and-expr_  
>   &emsp;_xor-expr_&emsp;`xor`&emsp;_and-expr_  
>
> _and-expr_ :  
>   &emsp;_not-expr_  
>   &emsp;_and-expr_&emsp;`and`&emsp;_not-expr_  
>
> _not-expr_ :  
>   &emsp;[_compare-expr_](#productions-for-compare-expr)  
>   &emsp;`not`&emsp;_not-expr_  

### Productions for _compare-expr_

The comparison operators can chain, with semantics similar to Python. For example `a < b <= c` is semantically
equivalent to `a < b and b <= c` (except that `b` is only evaluated once). The chaining semantics are not
apparent from the grammar, since the grammar specifies legal token streams, not their semantics.

> _compare-expr_ :  
>   &emsp;[_in-has-expr_](#productions-for-in-has-expr)  
>   &emsp;_compare-expr_&emsp;_compare-modifiers-opt_&emsp;_root-compare-oper_&emsp;_in-has-expr_  
>
> _root-compare-oper_ : one of  
>   &emsp;`=`&emsp;`<`&emsp;`>`&emsp;`<=`&emsp;`>=`  
>
> _compare-modifiers_ :  
>   &emsp;_compare-modifier_  
>   &emsp;_compare-modifiers_&emsp;_compare-modifier_  
>
> _compare-modifier_ : one of  
>   &emsp;`~`&emsp;`!`&emsp;`not`&emsp;`$`&emsp;`@`  

The compare modifiers:
* May not contain multiple occurrences of a modifier.
* May not contain both `!` and `not`.
* May not contain both `$` and `@`.

The `~` modifier indicates case insensitivity.

The `!` and `not` modifiers indicate logical negation.

The `$` modifier indicates **_strict comparison_**, where if either operand is `null` or a floating point `NaN`
value, the result (before applying a negation modifier) is `false`.
The `@` modifier indicates **_total comparison_**, where `null` is considered less than any non-`null` value and
`NaN` is considered less than any value other than `null` or `NaN`.
When neither `$` nor `@` is specified the comparison is **_total_** for `=` and **_strict_** for the ordered root
comparison operators.

Since `=` performs total comparison by default, `a = b` is `true` if both `a` and `b` are `null` or both are
`NaN`. This is different than may other languages. To get SQL comparison semantics, use `a $= b`. The C# equality
operator produces `true` when both operands are `null` but `false` when both operands are `NaN`. To match C#
semantics for `a = b`, one must use something like `a = b and a != 0/0`.

The total comparison `a !@< b` is equivalent to `a @>= b`. In contrast, the strict comparison `a !$< b` is not
equivalent to `a $>= b` when the comparison involves a type that includes `null` or `NaN` values. Consequently,
since the ordered operators are strict by default, `a !< b` is not equivalent to `a >= b` when either value
may be `null` or `NaN`. This matches the semantics of both SQL and C#.

### Productions for _in-has-expr_

The `in` operator tests for inclusion of the left operand in the right sequence of values.
The `has` operator tests for inclusion of the right operand as sub-text of the left operand.

> _in-has-expr_ :  
>   &emsp;[_concat-expr_](#productions-for-concat-expr)  
>   &emsp;_in-has-expr_&emsp;_in-has-modifiers-opt_&emsp;_root-in-has-oper_&emsp;_concat-expr_  
>
> _root-in-has-oper_ : one of  
>   &emsp;`in`&emsp;`has`  
>
> _in-has-modifiers_ :  
>   &emsp;_in-has-modifier_  
>   &emsp;_in-has-modifiers_&emsp;_in-has-modifier_  
>
> _in-has-modifier_ : one of  
>   &emsp;`~`&emsp;`!`&emsp;`not`&emsp;  

The in-has modifiers:
* May not contain multiple occurrences of a modifier.
* May not contain both `!` and `not`.

The `~` modifier indicates case insensitivity.

The `!` and `not` modifiers indicate logical negation.

**REVIEW**: Should in/has support strict?

### Productions for _concat-expr_

Sequence concatenation uses the `++` punctuator. Value concatenation uses the `&` punctuator and applies to text,
record, and tuple values.

> _concat-expr_ :  
>   &emsp;[_min-max-expr_](#productions-for-min-max-expr)  
>   &emsp;_concat-expr_&emsp;`&`&emsp;_min-max-expr_  
>   &emsp;_concat-expr_&emsp;`++`&emsp;_min-max-expr_  

### Productions for _min-max-expr_

The `min` and `max` operators produce the smaller and larger of the two operands, respectively.

> _min-max-expr_ :  
>   &emsp;[_bit-or-expr_](#productions-for-bitwise-operator-expressions)  
>   &emsp;_min-max-expr_&emsp;`min`&emsp;_bit-or-expr_  
>   &emsp;_min-max-expr_&emsp;`max`&emsp;_bit-or-expr_  

### Productions for bitwise operator expressions

The bitwise operators use keywords.

> _bit-or-expr_ :  
>   &emsp;_bit-xor-expr_  
>   &emsp;_bit-or-expr_&emsp;`bor`&emsp;_bit-xor-expr_  
>
> _bit-xor-expr_ :  
>   &emsp;_bit-and-expr_  
>   &emsp;_bit-xor-expr_&emsp;`bxor`&emsp;_bit-and-expr_  
>
> _bit-and-expr_ :  
>   &emsp;_bit-not-expr_  
>   &emsp;_bit-and-expr_&emsp;`band`&emsp;_bit-not-expr_  
>
> _bit-not-expr_ :  
>   &emsp;_bit-shift-expr_  
>   &emsp;`bnot`&emsp;_bit-not-expr_  
>
> _bit-shift-expr_ :  
>   &emsp;[_add-expr_](#productions-for-arithmetic-operator-expressions)  
>   &emsp;_bit-shift-expr_&emsp;_bit-shift-token_&emsp;_add-expr_  
>
> _bit-shift-token_ : one of  
>   &emsp;`shl`&emsp;`shr`&emsp;`shri`&emsp;`shru`  

### Productions for arithmetic operator expressions

The arithmetic operators use the typical punctuators.
Integer division and modulo use the keywords `div` and `mod`, respectively.

**REVIEW**: Should `mod` (and possibly `div`) also apply to floating point?

> _add-expr_ :  
>   &emsp;_mul-expr_  
>   &emsp;_add-expr_&emsp;`+`&emsp;_mul-expr_  
>   &emsp;_add-expr_&emsp;`-`&emsp;_mul-expr_  
>
> _mul-expr_ :  
>   &emsp;[_prefix-unary-expr_](#productions-for-prefix-unary-expr)  
>   &emsp;_mul-expr_&emsp;`*`&emsp;_prefix-unary-expr_  
>   &emsp;_mul-expr_&emsp;`/`&emsp;_prefix-unary-expr_  
>   &emsp;_mul-expr_&emsp;`div`&emsp;_prefix-unary-expr_  
>   &emsp;_mul-expr_&emsp;`mod`&emsp;_prefix-unary-expr_  

### Productions for _prefix-unary-expr_

The prefix unary operators are numeric negation, numeric posation, logical negation, and bitwise negation,
using typical punctuators. Note that the `!` and `~` operators  have different precedence
than the `not` and `bnot` operators. For example, `not x < y` is equivalent to `not (x < y)` while
`! x < y` is equivalent to `(! x) < y`.

> _prefix-unary-expr_ :  
>   &emsp;[_power-expr_](#productions-for-power-expr)  
>   &emsp;`+`&emsp;_prefix-unary-expr_  
>   &emsp;`-`&emsp;_prefix-unary-expr_  
>   &emsp;`!`&emsp;_prefix-unary-expr_  
>   &emsp;`~`&emsp;_prefix-unary-expr_  

### Productions for _power-expr_

The exponentiation operator is right associative.

> _power-expr_ :  
>   &emsp;[_postfix-unary-expr_](#productions-for-postfix-unary-expr)  
>   &emsp;_postfix-unary-expr_&emsp;`^`&emsp;_prefix-unary-expr_  

### Productions for _postfix-unary-expr_

The percent operator is a postfix unary operator and is semantically equivalent to
dividing by `100`.

> _postfix-unary-expr_ :  
>   &emsp;[_primary-expr_](#productions-for-primary-expr)  
>   &emsp;_postfix-unary-expr_&emsp;`%`  

### Productions for _primary-expr_

A primary expression is an atomic expression, dotted expression, indexing expression, function call (with two
possible forms), projection expression, or module expression.

> _primary-expr_ :  
>   &emsp;[_atomic-expr_](#productions-for-atomic-expr)  
>   &emsp;[_dotted-expr_](#productions-for-dotted-expr)  
>   &emsp;[_indexing-expr_](#productions-for-indexing-expr)  
>   &emsp;[_call-expr_](#productions-for-call-expr)  
>   &emsp;[_projection-expr_](#productions-for-projection-expr)  
>   &emsp;[_module-expr_](#productions-for-module-expr)  

### Productions for _dotted-expr_

A dotted expression uses the `.` punctuator. This is used for accessing a field, accessing an item in a
namespace, or evaluating a property value.

> _dotted-expr_  
>   &emsp;[_postfix-unary-expr_](#productions-for-postfix-unary-expr)&emsp;`.`&emsp;[**_identifier_**](#productions-for-identifier)  

### Productions for _indexing-expr_

An indexing expression uses the `[` and `]` punctuators, surrounding zero or more slice items, separated by commas.
This is used for accessing a single item value within a tuple, tensor, or text value or for specifying a slice of a
tuple, tensor, or text value.

> _indexing-expr_  
>   &emsp;[_postfix-unary-expr_](#productions-for-postfix-unary-expr)&emsp;`[`&emsp;_slice-items-opt_&emsp;`]`  
>
> _slice-items_ :  
>   &emsp;_slice-item_  
>   &emsp;_slice-items_&emsp;`,`&emsp;_slice-item_  

 Each slice item is either a slice index or a slice range.

> _slice-item_ :  
>   &emsp;[_slice-index_](#productions-for-slice-index)  
>   &emsp;[_slice-range_](#productions-for-slice-range)  

### Productions for _slice-index_

A _slice-index_ is an expression optionally prefixed with the `^` _back modifier_, or an _edge-modifier_, or
both, in either order.

> _slice-index_ :  
>   &emsp;_expr_  
>   &emsp;`^`&emsp;_expr_  
>   &emsp;_edge-modifier_&emsp;_expr_  
>   &emsp;`^`&emsp;_edge-modifier_&emsp;_expr_  
>   &emsp;_edge-modifier_&emsp;`^`&emsp;_expr_  
>
> _edge-modifier_ : one of  
>   &emsp;`&`&emsp;`%`  

The `^` back modifier indicates that the index value is a negative offset from the end of the index domain.
For example, if `x` is a text value, `x[^1]` accesses the last character in the text.

The `%` edge modifier indicates that the index value should be reducted modulo the length of the index domain.
For example, if `x` is a text value of length `5`, `x[%-3]` is equivalent to `x[2]` and `x[%8]` is equivalent
to `x[3]`.

The `&` edge modifier indicates that the index value should be _clamped_ to the closest value in the index domain.
For example, if `x` is a text value of length `5`, `x[&-3]` is equivalent to `x[0]` and `x[&8]` is equivalent
to `x[4]`.

If both the `^` back modifier and an _edge-modifier_ are specified, the back modifier is applied first, regardless
of the order in which the modifiers are written. That is, `x[^%i]` is equivalent to `x[%^i]` and `x[^&i]` is
equivalent to `x[&^i]`.

### Productions for _slice-range_

A _slice-range_ consists of an optional _start-indicator_ followed by `:` and an optional _stop-indicator_,
optionally followed by a second `:` and an optional _step_ expression.

> _slice-range_ :  
>   &emsp;_start-indicator-opt_&emsp;`:`&emsp;_stop-indicator-opt_  
>   &emsp;_start-indicator-opt_&emsp;`:`&emsp;_stop-indicator-opt_&emsp;`:`&emsp;_expr-opt_  

A _start-indicator_ consists of an expression optionally prefixed with the `^` back modifier.

> _start-indicator_ :  
>   &emsp;_expr_  
>   &emsp;`^`&emsp;_expr_  

As in a [_slice-index_](#productions-for-slice-index), the `^` back modifier indicates that the index value
is a negative offset from the end of the index domain.

A _stop-indicator_ consists of an expression optionally prefixed with the `^` back modifier, or the `*`
_count-modifier_, or both, in either order.

> _stop-indicator_ :  
>   &emsp;_expr_  
>   &emsp;`^`&emsp;_expr_  
>   &emsp;`*`&emsp;_expr_  
>   &emsp;`^`&emsp;`*`&emsp;_expr_  
>   &emsp;`*`&emsp;`^`&emsp;_expr_  

The `*` count modifier indicates that the value indicates a number of items rather than a stop index position.
The `^` back modifier indicates that the value is a negative offset from the index domain, when no `*` count
modifier is present, or as a negative offset from the maximum available count, when the `*` count modifier
is present. Note that this maximum available count may depend on the step value, when present.

For example, if `x` is a text value of length `10` containing `0123456789`,
* `x[1:3]` consists of `12`, the characters starting at index `1` and ending at index `3`.
* `x[1:*3]` consists of `123`, the characters starting at index `1` and ending after `3` characters have
  been included.
* `x[1:^3]` consists of `123456`, the characters starting at index `1` and ending at index `10 - 3 = 7`.
* `x[1:^*3]` and `x[1:*^3]` consist of `123456`, the characters starting at index `1` and ending after `9 - 3 = 6`
  characters have been included. Note that this is the same as `x[1:^3]`, since the step size is `1`.

When the step size is `2`,
* `x[1:3:2]` consists of `1`, the characters starting at index `1` and ending at index `3`, with step `2`.
* `x[1:*3:2]` consists of `135`, the characters starting at index `1` and ending after `3` characters have
  been included, with step `2`.
* `x[1:^3]` consists of `135`, the characters starting at index `1` and ending at index `10 - 3 = 7`, with step `2`.
* `x[1:^*3]` and `x[1:*^3]` consist of `13`, the characters starting at index `1` and ending after `5 - 3 = 2`
  characters have been included. `5` is computed as the number of items that could be included starting at index `1`
  with step `2`, i.e., the length of `13579`.

### Productions for _call-expr_

A _call-expr_ has two forms, namely _std-call-expr_ and _pipe-call-expr_.

> _call-expr_ :  
>   &emsp;[_std-call-expr_](#productions-for-std-call-expr)  
>   &emsp;[_pipe-call-expr_](#productions-for-pipe-call-expr)  

### Productions for _std-call-expr_

A _std-call-expr_ includes a function path, possibly including namespace specification, together with
the argument list. A function path that includes namespace specification uses the `.` punctuator
between names.

> _std-call-expr_ :  
>   &emsp;[_ident-path_](#productions-for-ident-path)&emsp;`(`&emsp;_arg-list-opt_&emsp;`)`  
>
> _arg-list_ :  
>   &emsp;_arg_  
>   &emsp;_arg-list_&emsp;`,`&emsp;_arg_  

Each item in the argument list may include a [**_directive_**](#productions-for-directive), an associated name,
or both. An _arg-name_ may be either an [**_identifier_**](#productions-for-identifier) or the blank
[keyword](#productions-for-keyword) `_` indicating the absence of a name, effectively
inhibiting an _implicit_ name.

### Productions for _arg_

An _arg_ is an item in an _arg-list_.

> _arg_ :  
>   &emsp;_expr_  
>   &emsp;_arg-name_&emsp;`:`&emsp;_expr_  
>   &emsp;_expr_&emsp;`as`&emsp;_arg-name_  
>   &emsp;[**_directive_**](#productions-for-directive)&emsp;_expr_  
>   &emsp;[**_directive_**](#productions-for-directive)&emsp;_arg-name_&emsp;`:`&emsp;_expr_  
>   &emsp;[**_directive_**](#productions-for-directive)&emsp;_expr_&emsp;`as`&emsp;_arg-name_  
>
> _arg-name_ :  
>   &emsp;`_`  
>   &emsp;[**_identifier_**](#productions-for-identifier)  

### Productions for _pipe-call-expr_

A _pipe-call-expr_ consists of a first argument (as a [_postfix-unary-expr_](#productions-for-postfix-unary-expr))
followed by the `->` (projection) token followed by a function path and optional argument list. An alternative
form includes a name specification for the first argument.

> _pipe-call-expr_ :  
>   &emsp;_postfix-unary-expr_&emsp;`->`&emsp;[_ident-path_](#productions-for-ident-path)&emsp;`(`&emsp;_arg-list-opt_&emsp;`)`  
>   &emsp;_postfix-unary-expr_&emsp;`->`&emsp;[_ident-path_](#productions-for-ident-path)&emsp;`(`&emsp;`as`&emsp;_arg-name_&emsp;`,`&emsp;_arg-list_&emsp;`)`  

### Productions for _ident-path_

A _ident-path_ specifies one or more identifiers separated by `.` optionally prefixed with `@`. Paths that
start with `@` are interpreted as absolute, starting at the _global namespace_.

> _ident-path_ :  
>   &emsp;[**_identifier_**](#productions-for-identifier)  
>   &emsp;`@`&emsp;[**_identifier_**](#productions-for-identifier)  
>   &emsp;_ident-path_&emsp;`.`&emsp;[**_identifier_**](#productions-for-identifier)  
>
> _ident-path-list_ :  
>   &emsp;_ident-path_  
>   &emsp;_ident-path-list_&emsp;`,`&emsp;_ident-path_  

### Productions for _projection-expr_

There are multiple forms of _projection-expr_. In projection, the left operand is _in scope_ when evaluating the
right operand. That is, the left operand value can be referenced by `it` in the right operand. Furthermore, if the
left operand is record-based, fields of the record are available in the right operand.

The projection forms with `+>` are _augmenting_ forms, whose result is the left operand augmented by the right
operand.
Notes:
* Augmenting tuple projection concatenates the left and right values.
* Augmenting tuple projection with a _paren-expr_ on the right is equivalent to specifying an arity-one tuple on the
  right. This effectively makes the trailing comma of the arity-one tuple optional in this form.
* Augmenting record projection uses the same semantics as the `SetFields` function. Note that this is _not_
  equivalent to record concatenation, specifically:
  * When a value in a _field-item_ is the `null` literal, the field with the name specified in the _field-item_ is
    dropped. That is, `R+>{ A: null }` drops the field named `A`.
  * When a value in a _field-item_ is a source field name, that source field is dropped, effectively renaming it to
    the name specified in the _field-item_. That is, `R+>{ B: A }` makes the field `B` contain the value of the
    source field `A` and drops field `A`.
* There is no augmenting form of _value-projection-expr_ or _module-projection-expr_.

> _projection-expr_ :  
>   &emsp;_value-projection-expr_  
>   &emsp;_tuple-projection-expr_  
>   &emsp;_record-projection-expr_  
>   &emsp;_module-projection-expr_  
>
> _value-projection-expr_ :  
>   &emsp;_postfix-unary-expr_&emsp;`->`&emsp;[_paren-expr_](#productions-for-paren-expr)  
>
> _tuple-projection-expr_ :  
>   &emsp;_postfix-unary-expr_&emsp;`->`&emsp;[_tuple-expr_](#productions-for-tuple-expr)  
>   &emsp;_postfix-unary-expr_&emsp;`+>`&emsp;[_tuple-expr_](#productions-for-tuple-expr)  
>   &emsp;_postfix-unary-expr_&emsp;`+>`&emsp;[_paren-expr_](#productions-for-paren-expr)  
>
> _record-projection-expr_ :  
>   &emsp;_postfix-unary-expr_&emsp;`->`&emsp;[_record-expr_](#productions-for-record-expr)  
>   &emsp;_postfix-unary-expr_&emsp;`+>`&emsp;[_record-expr_](#productions-for-record-expr)  

### Productions for _module-projection-expr_

Module projection is for setting parameter and free variable values to produce a new module value.

> _module-projection-expr_ :  
>   &emsp;_postfix-unary-expr_&emsp;`=>`&emsp;[_record-expr_](#productions-for-record-expr)  
>   &emsp;_postfix-unary-expr_&emsp;`=>`&emsp;`(`&emsp;_expr_&emsp;`)`  
>   &emsp;_postfix-unary-expr_&emsp;`with`&emsp;[_record-expr_](#productions-for-record-expr)  
>   &emsp;_postfix-unary-expr_&emsp;`with`&emsp;`(`&emsp;_expr_&emsp;`)`  

### Productions for _atomic-expr_

An atomic expression consists of a literal, a _box_, indicated by `_`, an identifier, possibly scoped as global,
an _it-name_, a _hash-name_, a parenthesized expression, a tuple expression, a sequence expression, a record
expression, or a module expression.

> _atomic-expr_ :  
>   &emsp;[_literal_](#productions-for-literal)  
>   &emsp;`_`  
>   &emsp;[**_identifier_**](#productions-for-identifier)  
>   &emsp;`@`&emsp;[**_identifier_**](#productions-for-identifier)  
>   &emsp;[_meta-prop-access_](#productions-for-meta-prop-access)  
>   &emsp;[_it-name_](#productions-for-it-name)  
>   &emsp;[_hash-name_](#productions-for-hash-name)  
>   &emsp;[_paren-expr_](#productions-for-paren-expr)  
>   &emsp;[_tuple-expr_](#productions-for-tuple-expr)  
>   &emsp;[_sequence-expr_](#productions-for-sequence-expr)  
>   &emsp;[_record-expr_](#productions-for-record-expr)  
>   &emsp;[_module-expr_](#productions-for-module-expr)  

### Productions for _literal_

A literal consists of a null literal, boolean literal, integer literal, rational literal (also known as floating
point literal), or text literal.

**REVIEW**: Should we have a date/time literal?

> _literal_ :  
>   &emsp;`null`  
>   &emsp;`false`  
>   &emsp;`true`  
>   &emsp;[**_integer-literal_**](#productions-for-integer-literal)  
>   &emsp;[**_rational-literal_**](#productions-for-rational-literal)  
>   &emsp;[**_text-literal_**](#productions-for-text-literal)  

### Productions for _meta-prop-access_

A task has _meta properties_ that are accessed using the `$` punctuator. For example, if `T` is a task,
`T$Finished` results in a boolean value indicating whether the task has finished executing.

> _meta-prop-access_ :  
>   &emsp;[_ident-path_](#productions-for-ident-path)&emsp;`$`&emsp;[**_identifier_**](#productions-for-identifier)  

### Productions for _it-name_

An _it-name_ consists of either the keyword `it` or an [**_it-slot_**](#productions-for-it-slot) token,
consisting of the characters `it$` followed by one or more decimal digits. Leading zeros are ignored,
so `it$01` is equivalent to `it$1`.

An _it-name_ is used to reference a value that is _in scope_. The tokens `it` and `it$0` reference the
_closest_ value in scope, with subsequent (outer) values referenced by `it$1`, `it$2`, and so on.

> _it-name_ :  
>   &emsp;`it`  
>   &emsp;**_it-slot_**  

### Productions for _hash-name_

A _hash-name_ is used to reference the index associated with a sequence item scope variable. A _hash-name_ consists
of the token `#` alone, the token `#` followed by an `it-name` or [**_identifier_**](#productions-for-identifier),
or a [**_hash-slot_**](#productions-for-hash-slot) token, consisting of the character `#` followed by one or more
decimal digits. As with an **_it-slot_** token, leading zeros in a **_hash-slot_** token are ignored, so `#01`
is equivalent to `#1`.

> _hash-name_ :  
>   &emsp;`#`  
>   &emsp;`#`&emsp;_it-name_  
>   &emsp;`#`&emsp;[**_identifier_**](#productions-for-identifier)  
>   &emsp;[**_hash-slot_**](#productions-for-hash-slot)  

For the identifier form, if the identifier is a **_contextual-keyword_**, there must be no other characters
between `#` and the identifier. For example, `#mod` is parsed as a _hash-name_ while `# mod` is parsed as the
_hash-name_ `#` followed by the `mod` operator.

### Productions for _paren-expr_

A parenthesized expression enables explicit specification of order of operations.

> _paren-expr_ :  
>   &emsp;`(`&emsp;_expr_&emsp;`)`  

### Productions for _tuple-expr_

A _tuple-expr_ indicates a tuple value consisting of the given items. The number of items in a tuple is
known as its _arity_.

In these productions, an _expr-list_ may be followed by a trailing comma. An arity-one tuple _requires_ a trailing
comma, to distinguish it from a _paren-expr_.

> _tuple-expr_ :  
>   &emsp;`(`&emsp;`)`  
>   &emsp;`(`&emsp;_expr_&emsp;`,`&emsp;`)`  
>   &emsp;`(`&emsp;_expr_&emsp;`,`&emsp;_expr-list_&emsp;`)`  
>   &emsp;`(`&emsp;_expr_&emsp;`,`&emsp;_expr-list_&emsp;`,`&emsp;`)`  
>
> _expr-list_ :  
>   &emsp;_expr_  
>   &emsp;_expr-list_&emsp;`,`&emsp;_expr_  

### Productions for _sequence-expr_

A _sequence-expr_ indicates a sequence consisting of the given items.

As with _tuple-expr_, an _expr-list_ may be followed by a trailing comma.

> _sequence-expr_ :  
>   &emsp;`[`&emsp;`]`  
>   &emsp;`[`&emsp;_expr-list_&emsp;`]`  
>   &emsp;`[`&emsp;_expr-list_&emsp;`,`&emsp;`]`  

It is considered good practice to use a space between the opening bracket `[` and the first item to avoid
confusion between the _sequence-expr_ and a [**_directive_**](#productions-for-directive).

### Productions for _record-expr_

A record expression indicates a record value. The field types are inferred from the values. A _field-list_ may be
followed by a trailing comma.

> _record-expr_ :  
>   &emsp;`{`&emsp;`}`  
>   &emsp;`{`&emsp;_field-list_&emsp;`}`  
>   &emsp;`{`&emsp;_field-list_&emsp;`,`&emsp;`}`  
>
> _field-list_ :  
>   &emsp;_field-item_  
>   &emsp;_field-list_&emsp;`,`&emsp;_field-item_  
>
> _field-item_ :  
>   &emsp;[**_identifier_**](#productions-for-identifier)  
>   &emsp;[_dotted-expr_](#productions-for-dotted-expr)  
>   &emsp;[**_identifier_**](#productions-for-identifier)&emsp;`:`&emsp;_expr_  
>   &emsp;_expr_&emsp;`as`&emsp;[**_identifier_**](#productions-for-identifier)  

Note that the single **_identifer_** form of _field-item_ uses the **_identifer_** for both the name and value.
Similarly, the _dotted-expr_ form of _field-item_ uses the **_identifer_** of the _dotted-expr_ for the name and the
full _dotted-expr_ for the value. In these forms, the name is said to be _implicit_.

### Productions for _module-expr_

A module is, in essence, a computational flow graph containing various kinds of symbols. Symbol kinds include
_parameters_, _constants_, _free variables_, and _computed variables_. The _computed variable_ kind includes
sub-kinds _measure_ and _constraint_.

A module expression defines a module value. The symbol types are inferred from the values.

A derivative module value is produced by a [_module-projection-expr_](#productions-for-module-projection-expr),
which specifies new values for parameters and free variables.

> _module-expr_ :  
>   &emsp;`module`&emsp;`{`&emsp;`}`  
>   &emsp;`module`&emsp;`{`&emsp;_symbol-list_&emsp;`}`  
>   &emsp;`plan`&emsp;`{`&emsp;`}`  
>   &emsp;`plan`&emsp;`{`&emsp;_symbol-list_&emsp;`}`  
>
> _symbol-list_ :  
>   &emsp;`;`  
>   &emsp;_symbol-decl_  
>   &emsp;_symbol-list_&emsp;`;`  
>   &emsp;_symbol-list_&emsp;`;`&emsp;_symbol-decl_  
>
> _symbol-decl_ :  
>   &emsp;_parameter-decl_  
>   &emsp;_constant-decl_  
>   &emsp;_free-variable-decl_  
>   &emsp;_computed-variable-decl_  
>
> _parameter-decl_ :  
>   &emsp;`param`&emsp;**_identifier_**&emsp;`:=`&emsp;_expr_  
>   &emsp;`param`&emsp;**_identifier_**&emsp;`def`&emsp;_expr_  
>   &emsp;`param`&emsp;**_identifier_**&emsp;`default`&emsp;_expr_  
>   &emsp;`parameter`&emsp;**_identifier_**&emsp;`:=`&emsp;_expr_  
>   &emsp;`parameter`&emsp;**_identifier_**&emsp;`def`&emsp;_expr_  
>   &emsp;`parameter`&emsp;**_identifier_**&emsp;`default`&emsp;_expr_  
>
> _constant-decl_ :  
>   &emsp;`const`&emsp;**_identifier_**&emsp;`:=`&emsp;_expr_  
>   &emsp;`constant`&emsp;**_identifier_**&emsp;`:=`&emsp;_expr_  
>
> _free-variable-decl_ :  
>   &emsp;`var`&emsp;**_identifier_**&emsp;`:=`&emsp;_expr_  
>   &emsp;`var`&emsp;**_identifier_**&emsp;_domain-clauses_  
>   &emsp;`variable`&emsp;**_identifier_**&emsp;`:=`&emsp;_expr_  
>   &emsp;`variable`&emsp;**_identifier_**&emsp;_domain-clauses_  
>
> _domain-clauses_ :  
>   &emsp;_in-clause_&emsp;_default-clause-opt_  
>   &emsp;_default-clause_&emsp;_in-clause_  
>   &emsp;_from-clause_&emsp;_to-clause-opt_&emsp;_default-clause-opt_  
>   &emsp;_from-clause_&emsp;_default-clause_&emsp;_to-clause_  
>   &emsp;_to-clause_&emsp;_from-clause-opt_&emsp;_default-clause-opt_  
>   &emsp;_to-clause_&emsp;_default-clause_&emsp;_from-clause_  
>   &emsp;_default-clause_&emsp;_from-clause-opt_&emsp;_to-clause-opt_  
>   &emsp;_default-clause_&emsp;_to-clause_&emsp;_from-clause_  
>
> _in-clause_ :  
>   &emsp;`in`&emsp;_expr_  
>
> _from-clause_ :  
>   &emsp;`from`&emsp;_expr_  
>
> _to-clause_ :  
>   &emsp;`to`&emsp;_expr_  
>
> _default-clause_ :  
>   &emsp;`def`&emsp;_expr_  
>   &emsp;`default`&emsp;_expr_  
>
> _computed-variable-decl_ :  
>   &emsp;_computed-variable-specifier_&emsp;**_identifier_**&emsp;`:=`&emsp;_expr_  
>
> _computed-variable-specifier_ :  
>   &emsp;`let`  
>   &emsp;`msr`  
>   &emsp;`measure`  
>   &emsp;`con`  
>   &emsp;`constraint`  

## Statement Grammar

The statement grammar defines a scripting language known as **_RexlScript_**. It is used by the Jupyter
Notebook kernel, test suites, and the internal tools, `RexlBench` and `RexlRun`.

The top level non-terminal in the syntactic statement grammar is [_statement-list_](#productions-for-statement-list),
indicating an ordered list of statements.

### Productions for _statement-list_

A statement list consists of statements, possibly separated by semicolons. Some statements are naturally
_closed_ so that a semicolon is not needed between it and a following statement. Other statements
are _open_ so that a semicolon _is required_ between it and a following statement. One kind of statement,
_block-stmt_, is not allowed in a _statement-list_. This is to avoid confusion with an _expr-stmt_
consisting of a _record-expr_. The _block-stmt_ is supported in other contexts.

> _statement-list_ :  
>   &emsp;_closed-stmt-list_  
>   &emsp;_open-stmt-list_  

### Productions for _closed-stmt-list_

A _closed-stmt-list_ is one that does _not_ require a semicolon between it and a subsequent statement.

> _closed-stmt-list_ :  
>   &emsp;_semis-opt_  
>   &emsp;_closed-stmt-list_&emsp;[_closed-stmt_](#productions-for-closed-stmt)&emsp;_semis-opt_  
>   &emsp;_open-stmt-list_&emsp;_semis_  
>
> _semis_ :  
>   &emsp;`;`  
>   &emsp;_semis_&emsp;`;`  

### Productions for _open-stmt-list_

An _open-stmt-list_ is one that _requires_ a semicolon between it and a subsequent statement.

> _open-stmt-list_ :  
>   &emsp;_closed-stmt-list_&emsp;[_open-stmt_](#productions-for-open-stmt)  

### Productions for _open-stmt_

There are many forms of statements. The _open-stmt_ forms are those that require a semicolon between
them and a following statement.

> _open-stmt_ :  
>   &emsp;[_expr-stmt_](#productions-for-expr-stmt)  
>   &emsp;[_definition-stmt_](#productions-for-definition-stmt)  
>   &emsp;[_publish-stmt_](#productions-for-publish-stmt)  
>   &emsp;[_goto-stmt_](#productions-for-goto-stmt)  
>   &emsp;[_import-stmt_](#productions-for-import-stmt)  
>   &emsp;[_execute-stmt_](#productions-for-execute-stmt)  
>   &emsp;[_namespace-open-stmt_](#productions-for-namespace-open-stmt)  
>   &emsp;[_with-open-stmt_](#productions-for-with-open-stmt)  
>   &emsp;[_while-open-stmt_](#productions-for-while-open-stmt)  
>   &emsp;[_if-open-stmt_](#productions-for-if-open-stmt)  
>   &emsp;[_task-command-stmt_](#productions-for-task-command-stmt)  
>   &emsp;[_task-invoke-stmt_](#productions-for-task-invoke-stmt)  
>   &emsp;[_user-function-stmt_](#productions-for-user-function-stmt)  

### Productions for _closed-stmt_

There are many forms of statements. The _closed-stmt_ forms are those that do _not_ require a semicolon
between them and a following statement. Note that [_block-stmt_](#productions-for-block-stmt) is not in
_closed-stmt_ since it is not allowed in a _stmt-list_ but is instead used in very specific settings.

> _closed-stmt_ :  
>   &emsp;[_label-stmt_](#productions-for-label-stmt)  
>   &emsp;[_namespace-block-stmt_](#productions-for-namespace-block-stmt)  
>   &emsp;[_with-block-stmt_](#productions-for-with-block-stmt)  
>   &emsp;[_while-block-stmt_](#productions-for-with-block-stmt)  
>   &emsp;[_if-block-stmt_](#productions-for-if-block-stmt)  
>   &emsp;[_task-block-stmt_](#productions-for-task-block-stmt)  
>   &emsp;[_user-procedure-stmt_](#productions-for-user-procedure-stmt)  

### Productions for _label-stmt_

A _label-stmt_ provides a target location for a [_goto-stmt_](#productions-for-goto-stmt).

> _label-stmt_ :  
>   &emsp;**_identifier_**&emsp;`:`  

### Productions for _block-stmt_

A _block-stmt_ is allowed in certain constructs, for example [_while-block-stmt_](#productions-for-while-block-stmt),
but not directly in [_closed-stmt_](#productions-for-closed-stmt) or [_open-stmt_](#productions-for-open-stmt).

> _block-stmt_ :  
>   &emsp;`{`&emsp;[_statement-list_](#productions-for-statement-list)&emsp;`}`  

### Productions for _nested-stmt_

A _nested-stmt_ may be any kind of statement except _label-stmt_ and _block-stmt_.

> _nested-stmt_ :  
>   &emsp;[_expr-stmt_](#productions-for-expr-stmt)  
>   &emsp;[_definition-stmt_](#productions-for-definition-stmt)  
>   &emsp;[_publish-stmt_](#productions-for-publish-stmt)  
>   &emsp;[_goto-stmt_](#productions-for-goto-stmt)  
>   &emsp;[_import-stmt_](#productions-for-import-stmt)  
>   &emsp;[_execute-stmt_](#productions-for-execute-stmt)  
>   &emsp;[_namespace-open-stmt_](#productions-for-namespace-open-stmt)  
>   &emsp;[_namespace-block-stmt_](#productions-for-namespace-block-stmt)  
>   &emsp;[_with-open-stmt_](#productions-for-with-open-stmt)  
>   &emsp;[_with-block-stmt_](#productions-for-with-block-stmt)  
>   &emsp;[_while-open-stmt_](#productions-for-while-open-stmt)  
>   &emsp;[_while-block-stmt_](#productions-for-with-block-stmt)  
>   &emsp;[_if-open-stmt_](#productions-for-if-open-stmt)  
>   &emsp;[_if-block-stmt_](#productions-for-if-block-stmt)  
>   &emsp;[_task-command-stmt_](#productions-for-task-command-stmt)  
>   &emsp;[_task-invoke-stmt_](#productions-for-task-invoke-stmt)  
>   &emsp;[_task-block-stmt_](#productions-for-task-block-stmt)  
>   &emsp;[_user-function-stmt_](#productions-for-user-function-stmt)  
>   &emsp;[_user-procedure-stmt_](#productions-for-user-procedure-stmt)  

### Productions for _expr-stmt_

An _expr-stmt_ simply consists of an _expr_. Since the core Rexl language is a pure functional language, an
expression statement has no side effects, so is typically interpreted by applications as a request to display
the value of the expression.

> _expr-stmt_ :  
>   &emsp;_expr_  

### Productions for _definition-stmt_

A _definition-stmt_ is used to assign a value and type to a (dotted) name or to the special `this` symbol.

> _definition-stmt_ :  
>   &emsp;[_ident-path_](#productions-for-ident-path)&emsp;`:=`&emsp;_expr_  
>   &emsp;`this`&emsp;`:=`&emsp;_expr_  

### Productions for _publish-stmt_

A _publish-stmt_ is used to assign a value and type to a (dotted) name in a task and to publish the
name as a result of the task. A _publish-stmt_ is only allowed inside a
[_block-stmt_](#productions-for-block-stmt) of a [_task-prime-clause_](#productions-for-task-block-stmt)
or [_task-play-clause_](#productions-for-task-block-stmt).

> _publish-stmt_ :  
>   &emsp;_publish-modifier_&emsp;[_ident-path_](#productions-for-ident-path)&emsp;`:=`&emsp;_expr_  
>
> _publish-modifier_ : one of  
>   &emsp;`publish`&emsp;`primary`&emsp;`stream`  

### Productions for _goto-stmt_

A _goto-stmt_ transfers execution to a _label-stmt_ with the provided name.

> _goto-stmt_ :  
>   &emsp;`goto`&emsp;**_identifier_**  

### Productions for _import-stmt_

An _import-stmt_ loads and processes an indicated script. The expression identifies the location of the script.

> _import-stmt_ :  
>   &emsp;`import`&emsp;_expr_  
>   &emsp;`import`&emsp;_expr_&emsp;`in`&emsp;`namespace`  
>   &emsp;`import`&emsp;_expr_&emsp;`in`&emsp;`namespace`&emsp;[_namespace-spec_](#productions-for-namespace-spec)  
>

### Productions for _execute-stmt_

An _execute-stmt_ takes an expression defining the text of a script and executes it.

> _execute-stmt_ :  
>   &emsp;`execute`&emsp;_expr_  
>   &emsp;`execute`&emsp;_expr_&emsp;`in`&emsp;`namespace`  
>   &emsp;`execute`&emsp;_expr_&emsp;`in`&emsp;`namespace`&emsp;[_namespace-spec_](#productions-for-namespace-spec)  

### Productions for _namespace-spec_

A _namespace-spec_ specifies a namespace in which to execute a script. It is used in
[_import-stmt_](#productions-for-import-stmt) and [_execute-stmt_](#productions-for-execute-stmt).

> _namespace-spec_ :  
>   &emsp;`@`  
>   &emsp;[_ident-path_](#productions-for-ident-path)  

### Productions for _namespace-open-stmt_

A _namespace-open-stmt_ changes the namespace for execution of subsequent statements.

> _namespace-open-stmt_ :  
>   &emsp;`namespace`  
>   &emsp;`namespace`&emsp;[_namespace-spec_](#productions-for-ident-path)  

### Productions for _namespace-block-stmt_

A _namespace-block-stmt_ specifies the namespace while executing the statements in the specified
[_block-stmt_](#productions-for-block-stmt).

> _namespace-block-stmt_ :  
>   &emsp;`namespace`&emsp;[_block-stmt_](#productions-for-block-stmt)  
>   &emsp;`namespace`&emsp;[_namespace-spec_](#productions-for-ident-path)&emsp;[_block-stmt_](#productions-for-block-stmt)  

### Productions for _with-open-stmt_

A _with-open-stmt_ specifies one or more namespaces that should be in scope when executing
subsequent statements.

> _with-open-stmt_ :  
>   &emsp;`with`&emsp;[_ident-path-list_](#productions-for-ident-path)  

### Productions for _with-block-stmt_

A _with-block-stmt_ specifies one or more namespaces that should be implicitly in scope while executing
the statements in the specified [_block-stmt_](#productions-for-block-stmt).

> _with-block-stmt_ :  
>   &emsp;`with`&emsp;[_ident-path-list_](#productions-for-ident-path)&emsp;[_block-stmt_](#productions-for-block-stmt)  

### Productions for _while-open-stmt_

A _while-open-stmt_ specifies a [_nested-stmt_](#productions-for-nested-stmt) to execute repeatedly while the
condition expression is `true`.

> _while-open-stmt_ :  
>   &emsp;`while`&emsp;`(`&emsp;_expr_&emsp;`)`&emsp;[_nested-stmt_](#productions-for-nested-stmt)  

### Productions for _while-block-stmt_

The _while-block-stmt_ differs from [_while-open-stmt_](#productions-for-while-open-stmt) in that the trailing
[_nested-stmt_](#productions-for-nested-stmt) is replaced with a [_block-stmt_](#productions-for-block-stmt).

> _while-block-stmt_ :  
>   &emsp;`while`&emsp;`(`&emsp;_expr_&emsp;`)`&emsp;[_block-stmt_](#productions-for-block-stmt)  

### Productions for _if-open-stmt_

There are various forms of _if-stmt_ depending on whether an `else` clause is included. The final clause for
all forms is a [_nested-stmt_](#productions-for-nested-stmt).

> _if-open-stmt_ :  
>   &emsp;`if`&emsp;`(`&emsp;_expr_&emsp;`)`&emsp;[_nested-stmt_](#productions-for-nested-stmt)  
>   &emsp;`if`&emsp;`(`&emsp;_expr_&emsp;`)`&emsp;[_nested-stmt_](#productions-for-nested-stmt)&emsp;`;`&emsp;`else`&emsp;[_nested-stmt_](#productions-for-nested-stmt)  
>   &emsp;`if`&emsp;`(`&emsp;_expr_&emsp;`)`&emsp;[_block-stmt_](#productions-for-block-stmt)&emsp;`else`&emsp;[_nested-stmt_](#productions-for-nested-stmt)  
>   &emsp;`if`&emsp;`(`&emsp;_expr_&emsp;`)`&emsp;[_block-stmt_](#productions-for-block-stmt)&emsp;`;`&emsp;`else`&emsp;[_nested-stmt_](#productions-for-nested-stmt)  

Note that a semicolon is permitted, but not required, after a [_block-stmt_](#productions-for-block-stmt).

### Productions for _if-block-stmt_

The _if-block-stmt_ differs from [_if-open-stmt_](#productions-for-if-open-stmt) in that the trailing
[_nested-stmt_](#productions-for-nested-stmt) is replaced with a [_block-stmt_](#productions-for-block-stmt).

> _if-open-stmt_ :  
>   &emsp;`if`&emsp;`(`&emsp;_expr_&emsp;`)`&emsp;[_block-stmt_](#productions-for-block-stmt)  
>   &emsp;`if`&emsp;`(`&emsp;_expr_&emsp;`)`&emsp;[_nested-stmt_](#productions-for-nested-stmt)&emsp;`;`&emsp;`else`&emsp;[_block-stmt_](#productions-for-block-stmt)  
>   &emsp;`if`&emsp;`(`&emsp;_expr_&emsp;`)`&emsp;[_block-stmt_](#productions-for-block-stmt)&emsp;`else`&emsp;[_block-stmt_](#productions-for-block-stmt)  
>   &emsp;`if`&emsp;`(`&emsp;_expr_&emsp;`)`&emsp;[_block-stmt_](#productions-for-block-stmt)&emsp;`;`&emsp;`else`&emsp;[_block-stmt_](#productions-for-block-stmt)  

### Productions for _task-command-stmt_

A _task-command-stmt_ controls execution of the task whose name is the included
[_ident-path_](#productions-for-ident-path).

> _task-command-stmt_ :  
>   &emsp;_task-cmd_&emsp;[_ident-path_](#productions-for-ident-path)
>
> _task-cmd_ : one of  
>   &emsp;`prime`&emsp;`play`&emsp;`pause`  
>   &emsp;`poke`&emsp;`poll`  
>   &emsp;`finish`&emsp;`abort`

### Productions for _task-invoke-stmt_

A _task-invoke-stmt_ invokes a procedure to create a new task and applies the _task-create-cmd_ to it (if not `task`).
If provided, the [_ident-path_](#productions-for-ident-path) is the name of the new task. The name
([_ident-path_](#productions-for-ident-path)) in the [_std-call-expr_](#productions-for-std-call-expr) must resolve to
a _procedure_ rather than a _function_.

> _task-invoke-stmt_ :  
>   &emsp;_task-create-cmd_&emsp;[_std-call-expr_](#productions-for-std-call-expr)  
>   &emsp;_task-create-cmd_&emsp;[_ident-path_](#productions-for-ident-path)&emsp;`as`&emsp;[_std-call-expr_](#productions-for-std-call-expr)  
>
> _task-create-cmd_ : one of  
>   &emsp;`task`  
>   &emsp;`prime`&emsp;`play`&emsp;`pause`  
>   &emsp;`poke`&emsp;`poll`  
>   &emsp;`finish`&emsp;`abort`

**REVIEW**: Should this support [_call-expr_](#productions-for-call-expr) rather than just
[_std-call-expr_](#productions-for-std-call-expr)?

### Productions for _task-block-stmt_

A _task-block-stmt_ creates a new task and applies the _task-create-cmd_ to it (if not `task`).
The [_ident-path_](#productions-for-ident-path) is the name of the new task.
If a _task-with-clause_ is included, the record field values are available in the subsequent clauses.
If a _task-prime-clause_ is included, its [_block-stmt_](#productions-for-block-stmt) is executed when
priming the task.
The [_block-stmt_](#productions-for-block-stmt) of the _task-play-clause_ is executed when the task
is playing (after priming).
A result is published from a _task-prime-clause_ or _task-play-clause_ using a
[_publish-stmt_](#productions-for-publish-stmt).

A [_task-block-stmt_](#productions-for-task-block-stmt) combines the functionality of a
[_user-procedure-stmt_](#productions-for-user-procedure-stmt) and
[_task-invoke-stmt_](#productions-for-task-invoke-stmt), so is also called an
_inline task_ or _inline procedure_.

> _task-block-stmt_ :  
>   &emsp;_task-create-cmd_&emsp;[_ident-path_](#productions-for-ident-path)&emsp;_task-with-clause-opt_&emsp;[_task-prime-clause-opt_](#productions-for-task-prime-clause)&emsp;[_task-play-clause_](#productions-for-task-play-clause)  
>
> _task-with-clause_ :  
>   &emsp;`with`&emsp;_record-expr_  
>   &emsp;`with`&emsp;`(`&emsp;_expr_&emsp;`)`  

### Productions for _task-prime-clause_

A _task-prime-clause_ specifies the statements to execute when priming the task. This is used in
[_task-block-stmt_](#productions-for-task-block-stmt) and
[_user-procedure-stmt_](#productions-for-user-procedure-stmt).

> _task-prime-clause_ :  
>   &emsp;`prime`&emsp;[_block-stmt_](#productions-for-block-stmt)  

### Productions for _task-play-clause_

A _task-play-clause_ specifies the statements to execute when playing the task. This is used in
[_task-block-stmt_](#productions-for-task-block-stmt) and
[_user-procedure-stmt_](#productions-for-user-procedure-stmt).

> _task-play-clause_ :  
>   &emsp;`play`&emsp;[_block-stmt_](#productions-for-block-stmt)  
>   &emsp;`as`&emsp;[_block-stmt_](#productions-for-block-stmt)  

### Productions for _user-function-stmt_

A _user-function-stmt_ declares a user-defined function or property.

> _user-function-stmt_ :  
>   &emsp;_func-specifier_&emsp;[_ident-path_](#productions-for-ident-path)&emsp;`(`&emsp;_param-names-opt_&emsp;`)`&emsp;`:=`&emsp;[_expr_](#productions-for-expr)
>
> _func-specifier_ : one of  
>   &emsp;`func`&emsp;`function`&emsp;`prop`&emsp;`property`  
>
> _param-names_ :  
>   &emsp;**_identifier_**  
>   &emsp;_param-names_&emsp;`,`&emsp;**_identifier_**  

### Productions for _user-procedure-stmt_

A _user-procedure-stmt_ declares a user-defined _procedure_ and defines the behavior of the tasks created
by the procedure (when used in a [_task-invoke-stmt_](#productions-for-task-invoke-stmt)). See also
[_task-block-stmt_](#productions-for-task-block-stmt).

> _user-procedure-stmt_ :  
>   &emsp;_proc-specifier_&emsp;[_ident-path_](#productions-for-ident-path)&emsp;`(`&emsp;_param-names-opt_&emsp;`)`&emsp;[_task-prime-clause-opt_](#productions-for-task-prime-clause)&emsp;[_task-play-clause_](#productions-for-task-play-clause)  

## Lexical Grammar

The lexical grammar is fairly conventional, and similar to C# in many respects.

In the BNF specification below, names in **_bold_** are tokens exposed to the syntactic grammar. Non-bold names are
internal to the lexical grammar and not exposed to the syntactic grammar as tokens.

The top level non-terminal is **_token_**. Before defining **_token_**, we need some preliminary non-terminals.

> _end-of-line_ :  
>   &emsp;U+000A (line feed)  
>   &emsp;U+000D (carriage return)  
>   &emsp;U+0085 (next line)  
>   &emsp;U+2028 (line separator)  
>   &emsp;U+2029 (paragraph separator)  
>
> _white-space_ :  
>   &emsp;any character of unicode class :Zs (space separator)  
>   &emsp;U+0008 (vertical tab)  
>   &emsp;U+0009 (horizontal tab)  
>   &emsp;U+000C (form feed)  
>
> _white-spaces_ :  
>   &emsp;_white-space_  
>   &emsp;_white-spaces_&emsp;_white-space_  
>
> _comment_ :  
>   &emsp;_line-comment_  
>   &emsp;_block-comment_  
>
> _line-comment_ :  
>   &emsp;`//`&emsp;_input-chars-opt_  
>
> _input-char_ :  
>   &emsp;any character except an _end-of-line_
>
> _input-chars_ :  
>   &emsp;_input-char_  
>   &emsp;_input-chars_&emsp;_input-char_  
>
> _block-comment_ :  
>   &emsp;`/*`&emsp;_block-comment-contents_&emsp;`*/`  
>
> _block-comment-contents_ :  
>   &emsp;any (possibly empty) sequence of unicode characters that does not contain `*` followed immediately by `/`

**Note**: the above non-terminals are _not_ exposed to the syntactic grammar. In particular, _white-space_ and
_end-of-line_ elements are dropped, and not emitted by the Rexl Lexer or exposed to the syntactic grammar.
They are essentially separators between other elements. The Rexl Lexer _does_ emit token objects for comments,
but they are dropped from the token stream that is passed to the Rexl Parser.

### Productions for **_token_**

> **_token_** :  
>   &emsp;[**_identifier_**](#productions-for-identifier)  
>   &emsp;[**_keyword_**](#productions-for-keyword)  
>   &emsp;[**_directive_**](#productions-for-directive)  
>   &emsp;[**_punctuator_**](#productions-for-punctuator)  
>   &emsp;[**_integer-literal_**](#productions-for-integer-literal)  
>   &emsp;[**_rational-literal_**](#productions-for-rational-literal)  
>   &emsp;[**_text-literal_**](#productions-for-text-literal)  

**REVIEW**: Perhaps we should have literal syntax for date-time.

### Productions for **_identifier_**

There are two styles of identifiers, quoted and non-quoted. A _quoted-identifier_ can contain characters
that a non-quoted identifier cannot, such as spaces. A _quoted-identifier_ can match the name of a
**_keyword_** and still be considered an identifier. A non-quoted _identifer-or-keyword_ is either an
**_identifer_** or a **_keyword_**, but not both. Thus, the keywords can be considered _reserved words_ of
the language. Note that we may reserve words that are not currently used in the syntactic
grammar (but may be in the future), such as `then`. We also reserve words that are only
used in the statement grammar, such as `import` and `namespace`.

As a _quoted-char_ (a character of a _quoted-identifier_), an instance of `''` is resolved as a
single `'` character. Quoted identifiers must have at least one non-_white-space_ character.

> **_identifier_** :  
>   &emsp;_quoted-identifier_  
>   &emsp;any _identifier-or-keyword_ that is not a [**_keyword_**](#productions-for-keyword)  
>
> _identifier-or-keyword_ :  
>   &emsp;_identifier-start-char_&emsp;_identifier-rest-opt_  
>
> _identifier-rest_ :  
>   &emsp;_identifier-rest-char_  
>   &emsp;_identifier-rest_&emsp;_identifier-rest-char_  
>
> _identifier-start-char_ :  
>   &emsp;`_`  
>   &emsp;any character of unicode class :L, which consists of the union of classes:  
>   &emsp;&emsp;:Lu (upper case letter),  
>   &emsp;&emsp;:Ll (lower case letter)  
>   &emsp;&emsp;:Lt (title case letter)  
>   &emsp;&emsp;:Lm (modifier letter)  
>   &emsp;&emsp;:Lo (other letter)  
>   &emsp;any character of unicode class :Nl (letter number)  
>
> _identifier-rest-char_ :  
>   &emsp;_identifier-start-char_  
>   &emsp;any character of unicode class :Mn (non-spacing mark)  
>   &emsp;any character of unicode class :Mc (spacing combining mark)  
>   &emsp;any character of unicode class :Nd (decimal digit number)  
>   &emsp;any character of unicode class :Pc (connector punctuation)  
>   &emsp;any character of unicode class :Cf (format)  
>
> _quoted-identifier_ :  
>   &emsp;`'`&emsp;_quoted-chars-opt_&emsp;`'`  
>
> _quoted-chars_ :  
>   &emsp;_white-spaces-opt_&emsp;_quoted-char_&emsp;_white-spaces-opt_  
>   &emsp;_quoted-chars_&emsp;_quoted-char_&emsp;_white-spaces-opt_  
>
> _quoted-char_ :  
>   &emsp;`''`  
>   &emsp;any _input-char_ except a _white-space_ or `'` character  

Note that _input-char_ excludes _end-of-line_, so _quoted-char_ also excludes _end-of-line_.

### Productions for **_keyword_**

A **_keyword_** is a reserved word of the language. It is not interpreted as a
normal **_identifier_**. To use the character sequence of a **_keyword_** as an
**_identifier_**, it must be quoted as specified [here](#productions-for-identifier).

> **_keyword_** : one of  
>   &emsp;`_`  
>   &emsp;`if`&emsp;`then`&emsp;`else`  
>   &emsp;`not`&emsp;`bnot`  
>   &emsp;`true`&emsp;`false`&emsp;`null`  
>   &emsp;`this`&emsp;`it`  
>   &emsp;`in`&emsp;`has`&emsp;`as`&emsp;`is`  
>   &emsp;`from`&emsp;`to`&emsp;`with`  
>   &emsp;`import`&emsp;`execute`&emsp;`namespace`  
>   &emsp;`goto`&emsp;`while`&emsp;`for`&emsp;`break`&emsp;`continue`  

As the language evolves, adding keywords is a _breaking change_, so should be done only
when necessary.

### Productions for **_contextual-keyword_**

A **_contextual-keyword_** is an identifier that may be used in a special way depending
on surrounding tokens. For example `mod` may be used as standard identifier, as in the
[_definition-stmt_](#productions-for-definition-stmt) `mod := 3`, but may also be used as
an operator, as in the [_mul-expr_](#productions-for-arithmetic-operator-expressions)
`A mod B`.

> **_contextual-keyword_** : one of  
>   &emsp;`_`  
>   &emsp;`or`&emsp;`xor`&emsp;`and`  
>   &emsp;`bor`&emsp;`bxor`&emsp;`band`  
>   &emsp;`shl`&emsp;`shr`&emsp;`shri`&emsp;`shru`  
>   &emsp;`div`&emsp;`mod`&emsp;`min`&emsp;`max`  
>   &emsp;`func`&emsp;`function`&emsp;`prop`&emsp;`property`&emsp;`proc`&emsp;`procedure`  
>   &emsp;`module`&emsp;`plan`  
>   &emsp;`param`&emsp;`parameter`&emsp;`const`&emsp;`constant`  
>   &emsp;`var`&emsp;`variable`&emsp;`let`  
>   &emsp;`msr`&emsp;`measure`&emsp;`con`&emsp;`constraint`  
>   &emsp;`opt`&emsp;`optional`&emsp;`req`&emsp;`required`&emsp;`def`&emsp;`default`  
>   &emsp;`task`&emsp;`prime`&emsp;`play`&emsp;`pause`&emsp;`poll`&emsp;`poke`&emsp;`finish`&emsp;`abort`  
>   &emsp;`publish`&emsp;`paimary`&emsp;`stream`  

### Productions for **_it-slot_**

An **_it-slot_** consists of the characters `it$` followed by any number of decimal digits.
Leading zeros are ignored, so `it$01` is considered equivalent to `it$1`.

> **_it-slot_** :  
>   &emsp;`it$`
>   &emsp;_it-slot_&emsp;_decimal-digit_  

### Productions for **_hash-slot_**

A **_hash-slot_** token consists of the character `#` followed by one or more decimal digits.
Leading zeros are ignored, so `#01` is considered equivalent to `#1`.

> **_hash-slot_** :  
>   &emsp;`#`&emsp;_decimal-digit_  
>   &emsp;_hash-slot_&emsp;_decimal-digit_  

### Productions for **_directive_**

Directives can be thought of as special punctuators that are only used as part of an 
[_arg_](#productions-for-arg). Directives start with `[` and end with `]`, with no spaces
between the bracket and adjacent character of the directive.

For example, the characters `[up]` form a single directive token while the characters
`[ up]` or `[up ]` form three tokens, namely the punctuator `[`, followed by the identifier
`up`, followed by the punctuator `]`.

> **_directive_** : one of  
>   &emsp;`[~]`&emsp;`[<]`&emsp;`[~<]`&emsp;`[>]`&emsp;`[~>]`&emsp;`[=]`&emsp;`[~=]`  
>   &emsp;`[up]`&emsp;`[~up]`&emsp;`[down]`&emsp;`[~down]`  
>   &emsp;`[key]`&emsp;`[agg]`&emsp;`[group]`&emsp;`[map]`&emsp;`[item]`  
>   &emsp;`[guard]`&emsp;`[with]`  

### Productions for **_punctuator_**

Punctuators are symbolic tokens that are meaningful to the syntactic grammar. Note that the set of
punctuators may include tokens that aren't currently used by the syntactic grammar, such as `?`.

> **_punctuator_** : one of  
>   &emsp;`(`&emsp;`)`&emsp;`[`&emsp;`]`&emsp;`{`&emsp;`}`  
>   &emsp;`.`&emsp;`!`&emsp;`~`&emsp;`,`&emsp;`;`&emsp;`:`&emsp;`?`&emsp;`@`&emsp;`$`&emsp;`#`  
>   &emsp;`+`&emsp;`-`&emsp;`*`&emsp;`/`&emsp;`%`&emsp;`|`&emsp;`&`&emsp;`^`  
>   &emsp;`++`&emsp;`**`&emsp;`||`&emsp;`&&`&emsp;`^^`&emsp;`??`  
>   &emsp;`=`&emsp;`<`&emsp;`>`&emsp;`<=`&emsp;`>=`  
>   &emsp;`<<`&emsp;`>>`&emsp;`>>>`  
>   &emsp;`->`&emsp;`+>`&emsp;`=>`&emsp;`:=`  

### Productions for **_integer-literal_**

The grammar for **_integer-literal_** is almost identical to that of C#, except that Rexl allows
additional type suffixes, denoting the intended size and signedness of the integer literal.

> **_integer-literal_** :  
>   &emsp;_decimal-integer-literal_  
>   &emsp;_hexadecimal-integer-literal_  
>   &emsp;_binary-integer-literal_  
>
> _decimal-integer-literal_ :  
>   &emsp;_decimal-digits_&emsp;_integer-type-suffix-opt_  
>
> _hexadecimal-integer-literal_ :  
>   &emsp;`0x`&emsp;_hexadecimal-digits_&emsp;_integer-type-suffix-opt_  
>   &emsp;`0X`&emsp;_hexadecimal-digits_&emsp;_integer-type-suffix-opt_  
>
> _binary-integer-literal_ :  
>   &emsp;`0b`&emsp;_binary-digits_&emsp;_integer-type-suffix-opt_  
>   &emsp;`0B`&emsp;_binary-digits_&emsp;_integer-type-suffix-opt_  
>
> _decimal-digits_ :  
>   &emsp;_decimal-digit-span_  
>   &emsp;_decimal-digits_&emsp;`_`&emsp;_decimal-digit-span_  
>
> _hexadecimal-digits_ :  
>   &emsp;_hexadecimal-digit-span_  
>   &emsp;_hexadecimal-digits_&emsp;`_`&emsp;_hexadecimal-digit-span_  
>
> _binary-digits_ :  
>   &emsp;_binary-digit-span_  
>   &emsp;_binary-digits_&emsp;`_`&emsp;_binary-digit-span_  
>
> _decimal-digit-span_ :  
>   &emsp;_decimal-digit_  
>   &emsp;_decimal-digit-span_&emsp;_decimal-digit_  
>
> _hexadecimal-digit-span_ :  
>   &emsp;_hexadecimal-digit_  
>   &emsp;_hexadecimal-digit-span_&emsp;_hexadecimal-digit_  
>
> _binary-digit-span_ :  
>   &emsp;_binary-digit_  
>   &emsp;_binary-digit-span_&emsp;_binary-digit_  
>
> _decimal-digit_ : one of  
>   &emsp;`0`&emsp;`1`&emsp;`2`&emsp;`3`&emsp;`4`&emsp;`5`&emsp;`6`&emsp;`7`&emsp;`8`&emsp;`9`  
>
> _hexadecimal-digit_ : one of  
>   &emsp;`0`&emsp;`1`&emsp;`2`&emsp;`3`&emsp;`4`&emsp;`5`&emsp;`6`&emsp;`7`&emsp;`8`&emsp;`9`  
>   &emsp;`A`&emsp;`B`&emsp;`C`&emsp;`D`&emsp;`E`&emsp;`F`  
>   &emsp;`a`&emsp;`b`&emsp;`c`&emsp;`d`&emsp;`e`&emsp;`f`  
>
> _binary-digit_ : one of  
>   &emsp;`0`&emsp;`1`  
>
> _integer-type-suffix_ : one of  
>   &emsp;`u`&emsp;`U`  
>   &emsp;`l`&emsp;`L`  
>   &emsp;`ul`&emsp;`lu`&emsp;`Ul`&emsp;`lU`&emsp;`uL`&emsp;`Lu`&emsp;`UL`&emsp;`LU`  
>   &emsp;`i`&emsp; `I`  
>   &emsp;`ia`&emsp; `iA`&emsp; `Ia`&emsp; `IA`  
>   &emsp;`u1` &emsp;`u2` &emsp;`u4` &emsp;`u8`  
>   &emsp;`U1` &emsp;`U2` &emsp;`U4` &emsp;`U8`  
>   &emsp;`i1` &emsp;`i2` &emsp;`i4` &emsp;`i8`  
>   &emsp;`I1` &emsp;`I2` &emsp;`I4` &emsp;`I8`  

**REVIEW**: Should we deprecate the old C# forms, eg UL?

### Productions for **_rational-literal_**

The grammar for **_rational-literal_** is similar to that of C#. However:
* We don't support the decimal type, so don't accept the `M` or `m` type suffixes.
* We support `r`, `r8`, `r4`, `r2`, and `r1` type suffixes. The binder currently rejects
  `r2` and `r1` and maps `r` to `r8`.

When there is a decimal point, we require at least one digit after it.
C# also has this requirement but some other languages don't.

> **_rational-literal_** :  
>   &emsp;_decimal-digits_&emsp;_rational-type-suffix_  
>   &emsp;_decimal-digits_&emsp;_exponent-part_&emsp;_rational-type-suffix-opt_  
>   &emsp;_decimal-digits-opt_&emsp;`.`&emsp;_decimal-digits_&emsp;_exponent-part-opt_&emsp;_rational-type-suffix-opt_  
>
> _exponent-part_ :  
>   &emsp;`e`&emsp;_decimal-digits_  
>   &emsp;`E`&emsp;_decimal-digits_  
>   &emsp;`e+`&emsp;_decimal-digits_  
>   &emsp;`E+`&emsp;_decimal-digits_  
>   &emsp;`e-`&emsp;_decimal-digits_  
>   &emsp;`E-`&emsp;_decimal-digits_  
>
> _rational-type-suffix_ : one of  
>   &emsp;`f`&emsp;`F`  
>   &emsp;`d`&emsp;`D`  
>   &emsp;`r`&emsp;`R`  
>   &emsp;`r1` &emsp;`r2` &emsp;`r4` &emsp;`r8`  
>   &emsp;`R1` &emsp;`R2` &emsp;`R4` &emsp;`R8`  

### Productions for **_text-literal_**

**REVIEW**: We need a specification for text literal.
