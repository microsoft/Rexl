# Functions

* [Argument Names and Scoping](#argument-names-and-scoping)
* [Auto Indexing](#auto-indexing)
* [Directives](#directives)
* [Core Functions](#core-functions)
* [Record Functions](#record-functions)
* [Sequence Functions](#sequence-functions)
* [Math Functions](#math-functions)
* [Text Functions](#text-functions)
* [Chrono Functions](#chrono-functions)
* [Conversion Functions](#conversion-functions)
* [Tuple Functions](#tuple-functions)
* [Tensor Functions](#tensor-functions)

The Rexl language includes a library of standard functions. Note that AI models are also functions but are not
documented here.

To **_call_** a function, the function name is followed by a parenthesized, comma-separated, list of
**_arguments_**. The meaning of these arguments is specific to the function.

## Argument Names and Scoping

Some function arguments may include a name. For example
```
Sum(order: Orders, order.Amt * order.Price)

ForEach(order: Orders, order+>{ Total: order.Amt * order.Price })

ForEach(order: Orders, SetFields(order, Total: order.Amt * order.Price))
```
Argument names may also be provided using the `as` keyword:
```
Sum(Orders as order, order.Amt * order.Price)

ForEach(Orders as order, order+>{ Total: order.Amt * order.Price })

ForEach(Orders as order, SetFields(order, order.Amt * order.Price as Total))
```
The `as` syntax can be used with [function projection](04-Operators.md#function-projection) to provide
a name for the first argument, as in:
```
Orders->Sum(as order, order.Amt * order.Price)

Orders->ForEach(as order, order+>{ Total: order.Amt * order.Price })

Orders->ForEach(as order, SetFields(order, order.Amt * order.Price as Total))
```
In the case of [`Sum`](#sum-functions) and [`ForEach`](#foreach-functions), the name
`order` is associated with a **_scope_** introduced by the first argument. In the case of
[`SetFields`](#record-functions), the name `Total` indicates a field name of the resulting
record value.

This section is primarily about the former situation, where an argument name is associated with a scope:
```
Sum(order: Orders, order.Amt * order.Price)

ForEach(order: Orders, order+>{ Total: order.Amt * order.Price })
```
Here, `Orders` is a table (sequence of record) with numeric columns named `Amt` and `Price`.
For example, `Orders` might be a table equivalent to
```
[ { Customer: "Sally", Amt: 3, Price: 25 },
  { Customer: "Bob",   Amt: 7, Price: 21 },
  { Customer: "Ahmad", Amt: 2, Price: 26 }, ]
```
The name `order` is a **_local name_** introduced as part of the first argument. It represents an
item (row or record) from the `Orders` sequence (table) and is available in the second argument of
the function call. The second argument is evaluated repeatedly, once for each item in the first
argument sequence. The first argument is said to **_provide_** the current item to the second argument.
The current item is said to be **_available_** or **_in scope_** in the second argument. The second
argument is known as a **_selector_**, since for each item it **_selects_** or computes a value.

The first expression
```
Sum(order: Orders, order.Amt * order.Price)
```
calculates the sum of `Amt` times `Price` across all the `Orders`. The second expression
```
ForEach(order: Orders, order+>{ Total: order.Amt * order.Price })
```
uses the [**_augmenting record projection_**](04-Operators.md#record-projection) operator `+>` and produces
a table with all of the columns from `Orders` plus a computed column named `Total` containing the product
of `Amt` and `Price`. For the example table above, this evaluates to a table containing:
```
[ { Customer: "Sally", Amt: 3, Price: 25, Total:  75 },
  { Customer: "Bob",   Amt: 7, Price: 21, Total: 147 },
  { Customer: "Ahmad", Amt: 2, Price: 26, Total:  52 }, ]
```
Local names are often optional. The `it` keyword references the scope (current item), so the `Sum` example
can  be written in these forms:
```
Sum(Orders, it.Amt * it.Price)

Orders->Sum(it.Amt * it.Price)
```
and the `ForEach` example can be written in these forms:
```
ForEach(Orders, it+>{ Total: it.Amt * it.Price })

Orders->ForEach(it+>{ Total: it.Amt * it.Price })
```
**_Technical Note_**: The **_augmenting record projection_** operator `+>` introduces a second scope
containing its left operand, and the `it` keyword inside the curly braces references that second
(innermost) scope. However, in this example, that inner scope value is the same as the outer scope value.

When a record value is in scope, fields of the record can be used unqualified, omitting an explicit reference to
the record, as in:
```
Sum(Orders, Amt * Price)

Orders->Sum(Amt * Price)

ForEach(Orders, it+>{ Total: Amt * Price })

Orders->ForEach(it+>{ Total: Amt * Price })
```
Moreover, since the augmenting record projection operator `+>` extends to sequence, the last expression can
be abbreviated to:
```
Orders+>{ Total: Amt * Price }
```
Generally, forms that avoid the use of `it` are clearer and less prone to errors.

The [`ForEach` function](#foreach-functions) can accept multiple sequence arguments, followed by a single
**_selector_** argument, as in:
```
ForEach(order: Orders, index: Range(Count(Orders)), order+>{ Index: index })
```
This iterates over both the orders and the range of integers, in parallel, to produce the orders with an
additional column named `Index`. For the example table above, this evaluates to a table containing:
```
[ { Index: 0, Customer: "Sally", Amt: 3, Price: 25 },
  { Index: 1, Customer: "Bob",   Amt: 7, Price: 21 },
  { Index: 2, Customer: "Ahmad", Amt: 2, Price: 26 }, ]
```
In this expression,
```
ForEach(order: Orders, index: Range(Count(Orders)), order+>{ Index: index })
```
each sequence argument provides a scope value to the selector. Furthermore, the augmenting record
projection operator `+>` introduces an additional scope containing its left operand. That is, the expression
`{ Index: index }` has three scopes available, being, from outermost to innermost, the one named `order`
introduced by the first sequence argument, the one named `index` introduced by the second sequence
argument, and the one (with no name) introduced by the projection operator. Therefore, to drop the name 
index, it is incorrect to write:
```
ForEach(order: Orders, Range(Count(Orders)), order+>{ Index: it })
```
Here, the `it` keyword refers to the left operand of the `+>` operator, which is the same as `order`, and not
to the scope introduced by the second sequence. To reference a specific scope, rather than the innermost, use
one of the forms `it$0`, `it$1`, `it$2`, etc. The form `it$0` is equivalent to `it`. In this example, the
correct formula uses `it$1`:
```
ForEach(order: Orders, Range(Count(Orders)), order+>{ Index: it$1 })
```
Outside the braces, there are only the two scopes introduced by the sequence arguments, so `order` can also
be replaced by `it$1`:
```
ForEach(Orders, Range(Count(Orders)), it$1+>{ Index: it$1 })
```
Note that the two uses of `it$1` indicate entirely different values, since the second occurrence has more scopes
available. More precisely, in the selector but outside the curly braces, `it$1` is an item from `Orders` and
`it$0` is the corresponding integer from the range sequence. _Inside_ the curly braces, there is an additional
scope, so these two values become `it$2` and `it$1`, while `it$0` becomes the left operand of the projection
operator, which happens to be the item from `Orders`.

Clearly, in this situation, using explicit names for the scopes is clearer:
```
ForEach(order: Orders, index: Range(Count(Orders)), order+>{ Index: index })
```
Fortunately, Rexl has a feature, [auto-indexing](#auto-indexing), that makes assigning an index like this
much simpler. The simplified form is:
```
ForEach(order: Orders, order+>{ Index: #order })
```
or, even simpler,
```
ForEach(order: Orders, order+>{ Index: # })
```
Since `+>` extends to sequence, this can be abbreviated further to:
```
Orders+>{ Index: # }
```
See the [auto-indexing](#auto-indexing) section for complete details.

There are various kinds of scopes. [`Sum`](#sum-functions) and [`ForEach`](#foreach-functions) introduce a
**_sequence item scope_** that represents an item from the corresponding sequence. The scope is provided
to a **_selector_** argument that is evaluated multiple times, once for each item (or set of items) in the
sequence argument(s).

In contrast, for the [`With`](#with-and-guard) function, each argument except the last introduces a
**_value scope_** that is provided to each additional argument. Each argument is evaluated just
once. For example,
```
With(x: 3, y: x * x, z: y * y + x, z + y + x)
```
Sets the **_local name_** `x` to `3`, then computes `x * x` to get `9` and assigns that value to the
local name `y`, then computes `y * y + x` to get `84` and assigns that value to `z`, then finally computes
`z + y + x` to get `96`, which is the value of the entire expression.

## Auto Indexing

The previous section includes the example:
```
ForEach(order: Orders, index: Range(Count(Orders)), order+>{ Index: index })
```
There is a feature of Rexl called **_auto-indexing_** that avoids explicitly using the `Range` sequence
in this situation. Any of the following forms produce the same result:
```
ForEach(order: Orders, order+>{ Index: #order })

ForEach(order: Orders, order+>{ Index: #0 })

ForEach(order: Orders, order+>{ Index: # })
```
The expression `#order` is the **_index_** of the item referenced by `order`, that is, the index of the current
item in the sequence `Orders`. Note that indices are zero-based, that is, they start at zero. The form `#0` refers
to the closest indexing scope. In this example there is only one indexing scope, that is associated with the sequence
item scope named `order`. When there are multiple indexing scopes, the terms `#0`, `#1`, `#2`, etc., can be used to
specify a particular one. Like the `it$0`, `it$1`, `$it$2` terms, `#0` corresponds to the innermost available index.
The term `#0` may be written simply `#`, as shown in the third expression above.

These expressions can also be written
```
ForEach(Orders, it+>{ Index: #it$1 })

ForEach(Orders, it+>{ Index: #0 })

ForEach(Orders, it+>{ Index: # })
```
Using the fact that `+>` extends to sequence, these can be abbreviated further:
```
Orders+>{ Index: #it$1 }

Orders+>{ Index: #0 }

Orders+>{ Index: # }
```
Many functions that have one or more sequence parameters support auto-indexing. These include:
* [`ForEach`](#foreach-functions) and its variants
* [`Count`](#count)
* [`Any` and `All`](#any-and-all)
* [`TakeOne` and `First`](#takeone-and-first)
* [`Take` and `Drop`](#take-and-drop-functions) and their variants
* [`ChainMap`](#chain-functions)
* [`Sum`](#sum-functions) and its variants
* [`Mean`](#mean-functions) and its variants
* [`Min` and `Max`](#min-and-max-functions) and their variants
* [`Fold`](#fold)
* [`ScanX` and `ScanZ`](#scanx-and-scanz)
* [`Sort`](#sort-sortup-sortdown) and its variants
* [`KeyJoin`](#keyjoin) and [`CrossJoin`](#crossjoin)
* [`GroupBy`](#groupby) and [`Distinct`](#distinct)
* [`Tensor.Build`](#tensorbuild)

For example, if `seq` is a sequence
```
TakeIf(seq, # mod 2 = 0)
```
produces the sub-sequence consisting of every other item, starting with the first (zeroth) item.

Additional functions may support auto-indexing in the future.

## Directives

Some function arguments may include a **_directive_**. For example,
```
Sort(Orders, [<] Customer, [<] Price, [>] Amt)

GroupBy(
  Orders,
  [key] Customer,
  [group] Total: Sum(group, Amt * Price),
  [group] MaxAmt: Max(group, Amt),
  [auto] Detail)
```
The symbols `[<]`, `[>]`, `[key]`, `[group]`, and `[auto]` are known as directives. These specify semantics
of the corresponding arguments.

Each function determines whether directives are allowed, which are allowed on which arguments, and what 
they mean.

In the [`Sort`](#sort-sortup-sortdown) example, sorting should happen first by the `Customer` field in
the upward direction (`A` before `Z`), then by the `Price` field in the upward direction (smallest first),
then by `Amt` in the downward direction (largest first).

For example, if `Orders` is a table equivalent to
```
[
  { Customer: "Sally", Amt: 3, Price: 25 },
  { Customer: "Bob",   Amt: 7, Price: 21 },
  { Customer: "Ahmad", Amt: 2, Price: 26 },
  { Customer: "Bob",   Amt: 8, Price: 21 },
  { Customer: "Sally", Amt: 4, Price: 25 },
  { Customer: "Ahmad", Amt: 23, Price: 17 },
  { Customer: "Sally", Amt: 1, Price: 25 },
]
```
the `Sort` expression
```
Sort(Orders, [<] Customer, [<] Price, [>] Amt)
```
evaluates to a table equivalent to:
```
[
  { Customer: "Ahmad", Amt: 23, Price: 17 },
  { Customer: "Ahmad", Amt: 2, Price: 26 },
  { Customer: "Bob",   Amt: 8, Price: 21 },
  { Customer: "Bob",   Amt: 7, Price: 21 },
  { Customer: "Sally", Amt: 4, Price: 25 },
  { Customer: "Sally", Amt: 3, Price: 25 },
  { Customer: "Sally", Amt: 1, Price: 25 },
]
```
In the [`GroupBy`](#groupby) expression
```
GroupBy(
  Orders,
  [key] Customer,
  [group] Total: Sum(group, Amt * Price),
  [group] MaxAmt: Max(group, Amt),
  [auto] Detail)
```
the `[key]` directive indicates that the corresponding argument should be used as a key value for grouping. The
`[group]` directive indicates that the corresponding argument should be provided a scope with name `group`
containing the entire group, as a sequence. The `[auto]` directive indicates that the result should include
a new table-valued column containing all items in the group, but with `[key]` columns dropped. With the example
`Orders` table above, this formula evaluates to a table equivalent to:
```
[
  { Customer: "Sally", Total: 200, MaxAmt: 4,
      Detail: [{Amt:3, Price:25}, {Amt:4, Price:25}, {Amt:1, Price:25}] },
  { Customer: "Bob", Total: 315, MaxAmt: 8,
      Detail: [{Amt:7, Price:21}, {Amt:8, Price:21}] },
  { Customer: "Ahmad", Total: 443, MaxAmt: 23,
      Detail: [{Amt:2, Price:26}, {Amt:23, Price:17}] },
]
```
Note that the `Detail` column is tabled-valued. That is, each group record contains a nested table named `Detail`.

## Core Functions

* [IsNull and IsEmpty](#isnull-and-isempty)
* [If](#if)
* [With and Guard](#with-and-guard)

### IsNull and IsEmpty

The `IsNull` and `IsEmpty` functions each take a single parameter:
```
IsNull(value)

IsEmpty(value)
```
For `IsNull`, the value can be of any type. This produces `true` if the value is `null` and `false` otherwise.

The `IsEmpty` function applies to the text type and to sequence types. For these, `IsEmpty(value)` evaluates to
`true` if the value is either empty or `null` and evaluates to `false` otherwise.

Note that an empty sequence is considered to be `null`, so when value is a sequence, `IsNull(value)` and
`IsEmpty(value)` are equivalent.

Recall however that the text type contains both the `null` value and a distinct empty text value, which can be 
written `""`. For both `null` and empty text, `IsEmpty` evaluates to `true`, but `IsNull("")` evaluates to `false`.
Semantically, `null` text can be viewed as "unknown", while empty text should be viewed as "known" but with no
characters. For example, a table of employees may have a column `MiddleName`. For someone that has no middle
name this should be set to the empty text value, while for someone whose middle name is unknown it should
be set to `null`.

For example,
```
Employees->TakeIf(MiddleName->IsEmpty())

Employees->TakeIf(MiddleName->IsNull())
```
would produce the sub-sequences of employees without a middle name and with unknown middle name,
respectively. Moreover,
```
Employees->TakeIf(MiddleName->IsEmpty())->IsEmpty()

Employees->TakeIf(MiddleName->IsNull())->IsEmpty()
```
would produce bool values indicating whether these sequences are empty. Since empty sequences and `null`
sequences are the same, these would be equivalent to
```
Employees->TakeIf(MiddleName->IsEmpty())->IsNull()

Employees->TakeIf(MiddleName->IsNull())->IsNull()
```
Of course, these would also be equivalent to using the [`Any`](#any-and-all) function and inverting:
```
not Employees->Any(MiddleName->IsEmpty())

not Employees->Any(MiddleName->IsNull())
```

### If

The `If` function evaluates one or more **_conditions_** and produces a value based on those results.
It has two basic forms:
```
If(condition, value)

If(condition, value, else_value)
```
The first form is equivalent to
```
If(condition, value, null)
```
The `condition` must be of type bool. If condition evaluates to `true`, then the `value` is evaluated and
produced. Otherwise, the `else_value` is evaluated and produced. The result type is the common super type
for the `value` and `else_value`.

The extended forms allow any number of condition / value pairs, with an optional final `else_value`:
```
If(c1, v1, c2, v2, ..., cn, vn)

If(c1, v1, c2, v2, ..., cn, vn, else_value)
```
Like the basic forms, when the final `else_value` is omitted `null` is used. All the `ck` values must be of
type bool. The result type is the common super type for all the `vk` values and the `else_value`.
If `c1` evaluates to `true`, then `v1` is evaluated and produced. Otherwise if `c2` evaluates to `true`, then
`v2` is evaluated and produced, and so on. If none of the `vk` evaluate to `true` then the `else_value`
is evaluated and produced.

### With and Guard

The `With` and `Guard` functions take two or more arguments. Each argument except the last introduces a
[**_value scope_**](#argument-names-and-scoping) that is provided to all later arguments. For `With`, the
result of the invocation is the value of the last argument. `Guard` provides additional functionality
explained below.

`With` is particularly useful when a computed value is used multiple times in a formula. For example, to
compute the volume of a (square) pyramid with width 25 feet and height 30 feet, and express the volume in
cubic centimeters, one could write
```
With(
  w: 25, h: 30,
  cm_per_ft: 12 * 2.54,
  w_cm: w * cm_per_ft,
  h_cm: h * cm_per_ft,
  w_cm * w_cm * h_cm / 3)
```
The final argument is the result value. Note that the final argument is not referenced by other arguments, so is 
not given a name. This example evaluates to `176_980_291.2`.

Even when values aren't used multiple times, it can be helpful to introduce names for quantities, as is the case 
for `h_cm` above. As another example, to convert a value `x` from miles to millimeters, one could write
```
With(
  miles: x,
  feet: miles * 5280,
  inches: feet * 12,
  inches * 25.4)
```
The `Guard` function is like `With` in many respects but with a critical difference: if any of the scope-providing
arguments is `null`, the final result is `null`. Moreover, the scope values corresponding to those arguments are
of the corresponding required type (when there is such a type). As explained in the discussion of
[extending to optional](03-ExtendedOperatorsAndFunctions.md#extending-to-optional), if `X` and `Y` are of optional
numeric type, then `X + Y` is shorthand for
```
Guard(x: X, y: Y, x + y)
```
The [**_scope values_**](#argument-names-and-scoping) `x` and `y` are of the corresponding required numeric
types. If either `X` or `Y` is `null`, the sum of the scope values `x + y` is not performed and instead the
result value is `null`. In fact, if `X` is `null` the value of `Y` isn't even evaluated. That is `Guard` is
**_short-circuiting_** when a scope expression evaluates to `null`. The `Guard` function is primarily
introduced automatically by the Rexl compilation process. It rarely needs to be used explicitly.

**_Technical Note_**: The `Guard` function supports both guarded and non-guarded arguments. To specify a
non-guarded argument, where `null` is passed through, include the `[with]` directive. To explicitly specify
a guarded argument, you may include the `[guard]` directive.

The `WithMap` function is identical to `With` except that it extends to sequence for its first parameter. That is,
if the first argument is a sequence, the call is automatically wrapped in an invocation of `ForEach`.

Similarly, the `GuardMap` function is identical to `Guard` except that it extends to sequence for its first parameter.

## Record Functions

The `SetFields` and `AddFields` functions are used to create a record from an existing record via additional
[**_field specifications_**](02-TypesAndValues.md#record-types). Recall that a record may be constructed using
field specifications enclosed in curly braces, as in:
```
{ Name: "Sally", DOB: Date(1994, 6, 5) }
```
If the name `R` is associated with this record, then
```
SetFields(R, NickName: "Sal", BirthYear: DOB.Year)
```
produces a record with two additional fields named `NickName` and `BirthYear`. Note that the source record
instance `R` is in scope in the field specification arguments, so `DOB` references that field of the record `R`.
The functionality is identical to using the
[**_augmenting record projection_** operator](04-Operators.md#record-projection) as in:
```
R+>{ Nickname: "Sal", BirthYear: DOB.Year }
```
In fact, the augmenting record projection operator is simply shorthand for invoking `SetFields`. Both of
these produce a record value equivalent to the result of:
```
{ Name: "Sally", DOB: Date(1994, 6, 5), BirthYear: 1994 }
```
The `SetFields` function can also **_rename_** a field. For example, the following also rename `DOB` to `BirthDate`:
```
R->SetFields(NickName: "Sal", BirthYear: DOB.Year, BirthDate: DOB)

R+>{ Nickname: "Sal", BirthYear: DOB.Year, BirthDate: DOB }
```
More precisely, when the value of a field specification is just an existing field of the source record,
that existing field is dropped from the result. These expressions produce a record value equivalent to:
```
{ Name: "Sally", BirthDate: Date(1994, 6, 5), BirthYear: 1994 }
```
To **_drop_** a field, without creating a new field with same name, provide a field specification with value
expression `null`, as in:
```
R->SetFields(NickName: "Sal", BirthYear: DOB.Year, DOB: null)

R+>{ Nickname: "Sal", BirthYear: DOB.Year, DOB: null }
```
These produce
```
{ Name: "Sally", NickName: "Sal", BirthYear: 1994 }
```

The `AddFields` function is almost identical to `SetFields`. The one difference is that fields that are the value
for new fields are not automatically dropped. That is, `AddFields` does not **_rename_** fields. Consequently, these
expressions
```
R->SetFields(NickName: "Sal", BirthDate: DOB)

R->AddFields(NickName: "Sal", BirthDate: DOB)
```
produce these different record values, respectively
```
{ Name: "Sally", BirthDate: Date(1994, 6, 5) }

{ Name: "Sally", BirthDate: Date(1994, 6, 5), DOB: Date(1994, 6, 5) }
```

## Sequence Functions

* [ForEach Functions](#foreach-functions)
* [Repeat](#repeat)
* [Range](#range)
* [Sequence](#sequence)
* [Count](#count)
* [Any and All](#any-and-all)
* [TakeOne and First](#takeone-and-first)
* [Take and Drop Functions](#take-and-drop-functions)
* [TakeAt](#takeat)
* [Chain Functions](#chain-functions)
* [Sum Functions](#sum-functions)
* [Mean Functions](#mean-functions)
* [Min and Max Functions](#min-and-max-functions)
* [Fold](#fold)
* [ScanX and ScanZ](#scanx-and-scanz)
* [Reverse](#reverse)
* [Sort, SortUp, SortDown](#sort-sortup-sortdown)
* [KeyJoin](#keyjoin)
* [CrossJoin](#crossjoin)
* [GroupBy](#groupby)
* [Distinct](#distinct)

Many Rexl functions produce or operate on one or more sequences.

### ForEach Functions

The `ForEach` function takes one or more sequence arguments and a **_selector_** argument and produces a
sequence. Note that the [Extending to Sequence](03-ExtendedOperatorsAndFunctions.md#extending-to-sequence),
[Argument Names and Scoping](#argument-names-and-scoping) and [Auto Indexing](#auto-indexing) sections
use and explain `ForEach` in their examples. Note that `ForEach` encompasses the functionality of the
traditional `map` and `zip` functions in pure functional languages. In Rexl, `Map` and `Zip` are aliases
for the `ForEach` function.

The general form of `ForEach` is
```
ForEach(seq_1, seq_2, ..., seq_n, selector)
```
Each `seq_k` is a sequence. Each sequence argument **_provides_** a **_current item value_** to the
`selector`, as explained in [Argument Names and Scoping](#argument-names-and-scoping). That is, the
current item of each sequence argument is **_in scope_** in the `selector`. The sequences are
iterated in parallel. The `selector` is evaluated for each set of corresponding sequence items. Each such
evaluation contributes one item to the result sequence. The result type of the invocation is the sequence type
with item type being the type of the selector. For example, if `Orders` is a table equivalent to
```
[ { Customer: "Sally", Amt: 3, Price: 25 },
  { Customer: "Bob",   Amt: 7, Price: 21 },
  { Customer: "Ahmad", Amt: 2, Price: 26 }, ]
```
then the invocation
```
ForEach(order: Orders, order.Amt * order.Price)
```
produces a sequence of numbers that are the product of the `Amt` and `Price` fields of the records in `Orders`.
The result is a sequence of numbers equivalent to `[ 75, 147, 52 ]`. As explained in
[Argument Names and Scoping](#argument-names-and-scoping), this expression may be abbreviated to
```
ForEach(Orders, Amt * Price)
```
In the following example, two sequences are iterated in parallel to create a sequence of records with two fields
named `Id` and `Customer`.
```
ForEach(order: Orders, id: Range(10, 1000, 10), { Id:id, order.Customer })
```
As in the previous example, the local name `order` may be omitted in the abbreviated formula
```
ForEach(Orders, id: Range(10, 1000, 10), { Id:id, Customer })
```
With the example table above, the result is a table equivalent to
```
[ { Id: 10, Customer: "Sally" },
  { Id: 20, Customer: "Bob"   },
  { Id: 30, Customer: "Ahmad" }, ]
```
Note that when there are multiple sequences, iteration ends when the shortest sequence is fully consumed. In
particular, the sequences do not need to be the same length.

As explained in [Auto Indexing](#auto-indexing), the `ForEach` function supports auto-indexing, so the
previous example can be accomplished via the expression
```
ForEach(Orders, { Id: 10 + 10 * #, Customer })
```
Since [record projection](04-Operators.md#record-projection) extends to sequence, this can be abbreviated to
```
Orders->{ Id: 10 + 10 * #, Customer }
```
As in this example, many invocations of `ForEach` are introduced by the Rexl compiler automatically
because of [extending to sequence](03-ExtendedOperatorsAndFunctions.md#extending-to-sequence).

The `ForEachIf` function is similar to the `ForEach` function with the difference that it takes an additional
parameter, before the `selector`, that is a `predicate` of bool type.
```
ForEachIf(seq_1, seq_2, ..., seq_n, predicate, selector)
```
The current sequence item values are in scope in the `predicate` as well as in the `selector`.
The `predicate` is evaluated for each set of corresponding sequence items. When this evaluation result
is `false`, evaluation of the `selector` is skipped and no corresponding item is contributed to the result
sequence. When the `predicate` evaluation is `true`, the `selector` is evaluated and that result is an item
of the result sequence. For example, with the `Orders` table above,
```
ForEachIf(order: Orders, order.Amt <= 5, order.Amt * order.Price)
```
produces a sequence of numbers that are the product of the `Amt` and `Price` fields of those records in `Orders`
for which the order `Amt` is no more than `5`. This effectively skips Bob's order (where `Amt` is `7`),
resulting in a sequence of numbers equivalent to `[ 75, 52 ]`. As explained in
[Argument Names and Scoping](#argument-names-and-scoping), this expression may be abbreviated to
```
ForEachIf(Orders, Amt <= 5, Amt * Price)
```

Actually, the `ForEach` function supports an optional predicate and invocations of `ForEachIf`
expand invocations of `ForEach` with the optional predicate specified via the `[if]` directive.
For example the final invocation of `ForEachIf` above
```
ForEachIf(Orders, Amt <= 5, Amt * Price)
```
is equivalent to
```
ForEach(Orders, [if] Amt <= 5, Amt * Price)
```

The `ForEachWhile` function is similar to `ForEachIf`, taking a `predicate` as well as `selector`.
The difference is that `ForEachWhile` stops producing items the first time that the `predicate`
is `false`. Similar to `ForEachIf`, an invocation of `ForEachWhile` expands to an invocation
of `ForEach` with optional predicate indicated by the `[while]` directive. For example,
```
ForEachWhile(Orders, Amt <= 5, Amt * Price)
```
is equivalent to
```
ForEach(Orders, [while] Amt <= 5, Amt * Price)
```
and stops producing items as soon as the `Amt` of an order exceeds `5`.

As another example
```
ForEach(k: Range(1, 10), k * k)

ForEachIf(k: Range(1, 10), k mod 3 != 0, k * k)

ForEachWhile(k: Range(1, 10), k mod 3 != 0, k * k)
```
are equivalent to
```
ForEach(k: Range(1, 10), k * k)

ForEach(k: Range(1, 10), [if] k mod 3 != 0, k * k)

ForEach(k: Range(1, 10), [while] k mod 3 != 0, k * k)
```
and produce the respective sequences
```
[1, 4, 9, 16, 25, 36, 49, 64, 81]

[1, 4, 16, 25, 49, 64]

[1, 4]
```

### Repeat

The `Repeat` function generates a sequence containing `count` items with each item being the given `value`.
It has the form
```
Repeat(value, count)
```
where `value` is any expression and `count` is the number of items in the resulting sequence. For example,
```
Repeat("Happy", 3)
```
produces a sequence of text consisting of three items, all with value `"Happy"`.

### Range

The `Range` function generates a sequence of `I8` (`8` byte signed integer) values. Each parameter type is also `I8`.

There are three forms:
```
Range(stop)

Range(start, stop)

Range(start, stop, step)
```
The first form is equivalent to
```
Range(0, stop, 1)
```
The second form is equivalent to
```
Range(start, stop, 1)
```
For the general form
```
Range(start, stop, step)
```
the result is an empty sequence if any of the following is true:
* `step` is zero.
* `step` is positive and `start >= stop`.
* `step` is negative and `start <= stop`.

Otherwise, the first item in the generated sequence is `start`, each subsequent item is the sum of its
predecessor and `step`, and the sequence ends when that sum reaches or "passes" the `stop` value. For
example, these expressions
```
Range(5)

Range(1, 6)

Range(1, 6, 2)

Range(6, 1, -2)
```
produce sequences equivalent to
```
[ 0, 1, 2, 3, 4 ]

[ 1, 2, 3, 4, 5 ]

[ 1, 3, 5 ]

[ 6, 4, 2 ]
```
As more complex examples, the expression
```
ForEach(a: Range(10),
  ForEach(b: Range(10),
    With(p: a * b, t: ToText(p), "  "[t.Len:] & t))
 ->Concat("|"))
```
produces a "multiplication table" as a sequence of text values, equivalent to:
```
[
" 1| 2| 3| 4| 5| 6| 7| 8| 9",
" 2| 4| 6| 8|10|12|14|16|18",
" 3| 6| 9|12|15|18|21|24|27",
" 4| 8|12|16|20|24|28|32|36",
" 5|10|15|20|25|30|35|40|45",
" 6|12|18|24|30|36|42|48|54",
" 7|14|21|28|35|42|49|56|63",
" 8|16|24|32|40|48|56|64|72",
" 9|18|27|36|45|54|63|72|81",
]
```
The expression
```
Fold(i: Range(1, 31), cur: 1ia, cur * i)
```
produces `30` factorial as `265252859812191058636308480000000`. Note that this uses the arbitrary precision 
integer type to avoid losing precision.

The expression:
```
Fold(i: Range(20, 0, -1), cur: 1.0, cur / i + 1)
```
uses the Taylor expansion of $e^x$ to compute an approximation of $e$, the base of the natural logarithm, which is 
approximately `2.718281828459045`.

Because `I8` multiplication and addition satisfy the normal distributive law,
```
Range(start, stop, step)
``` 
will be equivalent to
```
Range(count, start, step)
```
for some `count` value. Part of the benefit of `Range` is that the formula author need not
compute `count`.

### Sequence

The `Sequence` function is similar to [`Range`](#range) in that it produces a sequence of numeric values with a
constant `step` between values. There are three main differences:
* `Sequence` takes a `count` of values to produce rather than a `stop` value. When specifying a `stop` value is
  simpler, use `Range`. When specifying a `count` value is simpler, use `Sequence`.
* The default start value for `Sequence` is one rather than zero.
* The item type for `Sequence` can be any [**_major numeric type_**](02-TypesAndValues.md#major-numeric-types),
  that is, one of `U8`, `I8`, `IA`, or `R8`. The item type for `Sequence(count, start, step)` is the same as
  the result type for `start + step`.

There are three forms of the `Sequence` function:
```
Sequence(count)

Sequence(count, start)

Sequence(count, start, step)
```
The first form is equivalent to
```
Sequence(count, 1, 1)
```
The second form is equivalent to
```
Sequence(count, start, 1)
```
For the general form
```
Sequence(count, start, step)
```
the `count` value is an `I8` (`8` byte signed integer) value. The `start` and `step` values must be numeric.
The item type of the sequence is the selected type when the addition operator is applied to `start` and `step`,
as defined in the [**_major numeric types_**](02-TypesAndValues.md#major-numeric-types) section.

When `count` is less than or equal to zero, the result is an empty sequence. Otherwise, the sequence
length is `count`, the first value is `start`, the second value is `start + 1 * step`, the third value is
`start + 2 * step`, and so on.

For example, the following invocations of `Sequence` and `Range` are equivalent
```
Sequence(5)
Range(1, 6)

Sequence(5, 0)
Range(5)

Sequence(3, 1, 2)
Range(1, 6, 2)

Sequence(3, 6, -2)
Range(6, 1, -2)
```
and each equivalent pair produces, respectively,
```
[ 1, 2, 3, 4, 5 ]

[ 0, 1, 2, 3, 4 ]

[ 1, 3, 5 ]

[ 6, 4, 2 ]
```

In general,
```
Sequence(count, start, step)
```
is _almost always_ equivalent to
```
Range(0, count, 1) * step + start
```
The first value of the former is always `start` (converted to the item type). The first value
of the latter is `0 * step + start`, which is `NaN` when `step` is a _non-finite_ floating-point value
(`NaN` or positive or negative inifinity).

When the item type is `R8`, the sequence values may differ from using repeated addition because of floating
point rounding. For example
```
Sequence(10, 0.1, 0.1);

Generate(10, k:0.0, k + 0.1);
```
produce slightly different values for the last four items.

### Count

The `Count` function has two forms:
```
Count(seq)

Count(seq, predicate)
```
For each form, the first parameter must be a sequence (possibly `null`). The first form produces the number of
items in the sequence. In the second form, the current item of `seq` is in scope in the `predicate`, the
`predicate` must be a bool-valued expression, and the result is the number of items in `seq` for which the
`predicate` is true. The invocation may include a name for the first argument, which will be the local name
associated with the current sequence item. `Count` supports [**_auto-indexing_**](#auto-indexing) so the
`predicate` can also reference the index of the current item.

Note that the two-parameter form is equivalent to
```
TakeIf(seq, predicate)->Count()
```
As examples,
```
Count(Range(10))

Count(Range(10), it mod 3 = 1)
```
evaluate to `10` and `3` respectively.

For an `Orders` table,
```
Orders->Count(Amt >= 10)
```
evaluates to the number of orders where the `Amt` field is at least `10`.

### Any and All

The `Any` and `All` functions each have a single-parameter form:
```
Any(seq)

All(seq)
```
The `seq` argument must be a sequence with item type bool.

`Any` produces true if any of the items is `true` and produces `false` if none of the items are `true` or if the 
sequence is empty.

`All` produces `true` if the sequence is empty or all of the items are `true` and produces `false` if any of the items
are `false`.

Note that these are equivalent to
```
!All(!seq)

!Any(!seq)
```
respectively.

The `Any` and `All` functions also have two-parameter forms:
```
Any(seq, predicate)

All(seq, predicate)
```
In these forms, `seq` can be a sequence of any item type, the current item of `seq` is in scope in the
`predicate`, and the `predicate` must be a bool-valued expression. The invocation may include a name for
the first argument, which will be the local name associated with the current sequence item. `Any` and `All`
support [**_auto indexing_**](#auto-indexing) so the `predicate` can also reference the index of the current
item.

`Any` produces `true` if the `predicate` evaluates to `true` for any of the items and `false` if the
`predicate` evaluates to `false` for all of the items, or if the sequence is empty.

`All` produces `true` if the sequence is empty or if the `predicate` evaluates to `true` for all of the
items and produces `false` if the predicate evaluates to `false` for any of the items.

Note that these are equivalent to
```
ForEach(seq, predicate)->Any()

ForEach(seq, predicate)->All()
```
respectively.

### TakeOne and First

The `TakeOne` and `First` functions produce the first item in a sequence that satisfies
an optional predicate, if there is such an item, and produce an explicit or implicit
**_else-value_** otherwise.

The difference between them is that when no `else_value` is specified, `TakeOne`
uses the [**_default value_**](02-TypesAndValues.md#default-values) of the sequence item type
while `First` uses `null`. When `else_value` is specified `TakeOne` and `First` are identical. 

The `TakeOne` and `First` functions have the forms:
```
TakeOne(seq)
TakeOne(seq, predicate)
TakeOne(seq, [else] else_value)
TakeOne(seq, predicate, else_value)

First(seq)
First(seq, predicate)
First(seq, [else] else_value)
First(seq, predicate, else_value)
```
The `seq` parameter must be a sequence (of any type).

The `predicate` argument may include the `[if]` directive. The `else_value` must include the `[else]`
directive if there is no `predicate` and may include the `[else]` directive if there is a `predicate`.

When specified, the `else_value` must be convertible to the optional form of the sequence item type.
For example, if the item type of `seq` is `I8`, it is illegal for the `else_value` to be floating-point,
since that is not convertible to optional `I8`.

The result type is either the sequence item type or the optional form of that type, depending on whether
the explicit or implicit else-value is optional.

For forms that include a `predicate`, the current item of `seq` is in scope in the `predicate`, and the
`predicate` must be a bool-valued expression. For these forms, the invocation may include a name for the
first argument, which will be the local name associated with the current sequence item. The forms that
include a `predicate` support [**_auto indexing_**](#auto-indexing) so the `predicate` can also reference
the index of the current item.

As examples,
```
Range(100)->TakeOne(it * it > 50)
Range(100)->TakeOne(it * it > 50_000)
Range(100)->TakeOne(it * it > 50_000, -12)
Range(100)->First(it * it > 50_000)
```
produce `8`, `0`, `-12`, and `null`, respectively. All of these have type `I8` except the last,
which has type _optional_ `I8`. The first produces an item from the sequence, while the remaining
ones produce the explicit or implicit else-value.

For an `Orders` table,
```
Orders->First()
```
evaluates to the first record (row) of the table, or `null` if the table has no rows. Similary,
```
Orders->First(Amt >= 10)
```
evaluates to the first order record for which the `Amt` field is at least `10`, or `null`
if there is no such record in the table.

### Take and Drop Functions

The `Take`/`Drop` family of functions includes the following forms:
```
Take(seq, count)
Drop(seq, count)

Take(seq, [if] predicate)
Drop(seq, [if] predicate)

Take(seq, [while] predicate)
Drop(seq, [while] predicate)

Take(seq, count, predicate)
Drop(seq, count, predicate)

Take(seq, count, [if] predicate)
Drop(seq, count, [if] predicate)

Take(seq, count, [while] predicate)
Drop(seq, count, [while] predicate)

TakeIf(seq, predicate)
Filter(seq, predicate)
DropIf(seq, predicate)

TakeWhile(seq, predicate)
DropWhile(seq, predicate)

DropOne(seq)
DropOne(seq, predicate)
```
For each of these, the `seq` parameter must be a sequence (of any item type). For forms that include a `count`,
the `count` must be of type `I8` (`8` byte signed integer). For forms that include a `predicate`, the current
item of `seq` is in scope in the `predicate`, and the `predicate` must be a bool-valued expression. For these
forms, the invocation may include a name for the first argument, which will be the local name associated with
the current sequence item. The forms that include a `predicate` support [**_auto indexing_**](#auto-indexing)
so the `predicate` can also reference the index of the current item.

All of these forms produce a sub-sequence of `seq`, that is, for each item in `seq`, the function will
either _take_ the item, meaning produce it as part of the result sequence, or _drop_ the item, meaning
skip the item and not produce it as part of the result sequence. Note that order of the items is preserved.

In particular, when `seq` is an empty sequence, these all produce an empty sequence.

The items of `seq` produced by the `Take` form are exactly those items that are dropped (omitted) by the
`Drop` form. That is, the results of the `Take` and `Drop` forms are complementary. Consequently, we need
only specify either the `Take` form or the `Drop` form, with the other being completely determined by the
one specified. This discussion focuses on the `Take` forms.

For example, the expression
```
Take(Range(10), 3, it mod 2 != 0)
```
_takes_ (or _keeps_) up to `3` values that are not divisible by `2`, producing `[1, 3, 5]`, while
```
Drop(Range(10), 3, it mod 2 != 0)
```
_drops_ up to `3` values that are not divisible by `2`, producing `[0, 2, 4, 6, 7, 8, 9]`. Together these
two results include all the items of the original sequence, namely, `[0, 1, 2, 3, 4, 5, 6, 7, 8, 9]`.

Invocations of `Take` and `Drop` that include a predicate without a directive use the `[if]` directive.
That is,
* `Take(seq, count, predicate)` is equivalent to `Take(seq, count, [if] predicate)`.
* `Drop(seq, count, predicate)` is equivalent to `Drop(seq, count, [if] predicate)`.

Invocations of a function with suffix (`If`, `While`, or `One`) are equivalent to an invocation of
`Take` or `Drop`. In particular:
* `TakeIf(seq, predicate)` is equivalent to `Take(seq, [if] predicate)`.
* `Filter` is identical to (an alias of) `TakeIf`.
* `DropIf(seq, predicate)` is equivalent to `Drop(seq, [if] predicate)`.
* `TakeWhile(seq, predicate)` is equivalent to `Take(seq, [while] predicate)`.
* `DropWhile(seq, predicate)` is equivalent to `Drop(seq, [while] predicate)`.
* `DropOne(seq)` is equivalent to `Drop(seq, 1)`.
* `DropOne(seq, predicate)` is equivalent to `Drop(seq, 1, predicate)`.

The form
```
Take(seq, count)
```
_keeps_ (or _keeps_) the first (up to) `count` items of `seq`. More precisely, it produces an empty sequence
if `count` is less than or equal to zero. Otherwise, if `count` is less than the number of items in `seq`,
the result sequence consists of the first `count` items from `seq`. Otherwise, the result sequence is the
same as `seq`.

The form
```
Take(seq, [if] predicate)
```
produces the items in `seq` for which the `predicate` is `true`. Similarly, `Drop(seq, [if] predicate)`
produces the items in `seq` for which the `predicate` is `false`.

The form
```
Take(seq, [while] predicate)
```
produces the items from `seq` up to (but not including) the first item in `seq` for which the `predicate` is
`false`.

The form
```
Take(seq, count, [if] predicate)
```
_keeps_ the first (up to) `count` items for which the `predicate` is `true`. This is equivalent to the
composition
```
seq->Take([if] predicate)->Take(count)
```
Note that the corresponding `Drop` form is defined in terms of this `Take` form but is not equivalent to
a composition of other `Drop` forms. More specifically,
```
Drop(seq, count, [if] predicate)
```
_drops_ the first (up to) `count` items for which the `predicate` is `true`.

The form
```
Take(seq, count, [while] predicate)
```
produces no more than `count` items from `seq` up to (but not including) the first item in `seq` for
which `predicate` is `false`. This is equivalent to the composition
```
seq->Take([while] predicate)->Take(count)
```
Note that the corresponding `Drop` form is defined in terms of this `Take` form but is not equivalent to
a composition of other `Drop` forms. More specifically,
```
Drop(seq, count, [while] predicate)
```
_drops_ no more than `count` items from `seq` up to (but not including) the first item in `seq` for
which `predicate` is `false`.

### TakeAt

REVIEW: Need documentation for `TakeAt`.

### Chain Functions

The `Chain` function concatenates together multiple sequences. It has the form
```
Chain(seq_1, seq_2, ..., seq_n)
```
Each `seq_k` is a sequence. The result is a sequence that is the concatenation of these sequences in order. It is
equivalent to using the sequence concatenation operator on the sequences. For example, the expressions
```
Chain(Range(3), Range(4))

Range(3) ++ Range(4)
```
both produce a sequence equivalent to `[ 0, 1, 2, 0, 1, 2, 3 ]`.

When the given sequences do not all have the same item type, the item type of the resulting sequence is the
common super type of the item types. For example,
```
Chain(Range(3), [ 3.5, -5.25 ])
```
is a sequence of `R8` equivalent to `[ 0.0, 1.0, 2.0, 3.5, -5.25 ]`.

The `ChainMap` function has form
```
ChainMap(seq_1, seq_2, ..., seq_n, selector)
```
This form looks similar to the form for `Chain`, but that similarity is deceptive. The sequence parameters for
`ChainMap` are iterated _in parallel_, like with [`ForEach`](#foreach-functions). With `ForEach`, the selector
can be of any type. With `ChainMap`, the selector must itself be sequence-valued.

Each sequence argument **_provides_** a current item value to the `selector`, as explained in
[Argument Names and Scoping](#argument-names-and-scoping). That is, the current item of each sequence argument
is **_in scope_** in the `selector`. The `selector` is evaluated for each set of corresponding sequence items.
Each such evaluation contributes a sequence (possibly empty) and these sequences are concatenated together to
produce the final result sequence. For example,
```
ChainMap(n:Range(5), Range(n))
```
produces a sequence of `I8` equivalent to the result of
```
Range(0) ++ Range(1) ++ Range(2) ++ Range(3) ++ Range(4)
```
which produces a sequence equivalent to
```
[0, 0,1, 0,1,2, 0,1,2,3]
```
Note that the initial `Range(0)` is empty, so contributes no items to the result sequence. Note that this example 
may be accomplished using [`Fold`](#fold-scan-and-generate-functions) as
```
Fold(n:Range(5), cur:[], cur ++ Range(n))
```
but the use of `ChainMap` is more direct and more efficient.

`ChainMap` can be used to **_un-group_** the result of a [`GroupBy`](#groupby) invocation. For example,
suppose `T` is the table containing the first `100` non-negative integers together with their squares
and cubes generated by the Rexl
```
Range(100)->{ N:it, N2:it^2, N3:it^3 }
```
and `G` is the result of grouping these by residue class (remainder) mod `5`, accomplished via
```
T->GroupBy(Residue: N mod 5, Items)
```
Then `G` is like
```
[
  { Residue: 0, Items:[{N:0, N2:0, N3:0}, {N:5, N2:25, N3:125}, {N:10, N2:100, N3:1000}, ...]},
  { Residue: 1, Items:[{N:1, N2:1, N3:1}, {N:6, N2:36, N3:216}, {N:11, N2:121, N3:1331}, ...]},
  { Residue: 2, Items:[{N:2, N2:4, N3:8}, {N:7, N2:49, N3:343}, {N:12, N2:144, N3:1728}, ...]},
  { Residue: 3, Items:[{N:3, N2:9, N3:27}, {N:8, N2:64, N3:512}, {N:13, N2:169, N3:2197}, ...]},
  { Residue: 4, Items:[{N:4, N2:16, N3:64}, {N:9, N2:81, N3:729}, {N:14, N2:196, N3:2744}, ...]},
]
```
`ChainMap` can now be used to get the original table rows from `G` via
```
G->ChainMap(Items)
```
These rows will _not_ be in the same order as in `T`. Instead they will be in the order they appear
in `G` as in
```
[
  {N:0, N2:0, N3:0},
  {N:5, N2:25, N3:125},
  {N:10, N2:100, N3:1000},
  ...
  {N:1, N2:1, N3:1},
  {N:6, N2:36, N3:216},
  {N:11, N2:121, N3:1331},
  ...
  {N:2, N2:4, N3:8},
  {N:7, N2:49, N3:343},
  {N:12, N2:144, N3:1728},
  ...
  {N:3, N2:9, N3:27},
  {N:8, N2:64, N3:512},
  {N:13, N2:169, N3:2197},
  ...
  {N:4, N2:16, N3:64},
  {N:9, N2:81, N3:729},
  {N:14, N2:196, N3:2744},
  ...
]
```
The [`Sort`](#sort-sortup-sortdown) functions are useful to change the order to whatever is desired,
for example
```
G->ChainMap(Items)->SortUp(N)
```
puts the records in the order they appear in the original table `T`.

### Sum Functions

The `Sum` function has two forms, namely the single parameter form, that takes a sequence, and the general 
form that takes one or more sequences plus a selector.
```
Sum(seq)

Sum(seq_1, seq_2, ..., seq_n, selector)
```
In the single parameter form, the item type of `seq` must be numeric, either
[required or optional](02-TypesAndValues.md#optional-types). The **_result type_** is one of the
[major numeric types](02-TypesAndValues.md#major-numeric-types) `U8`, `I8`, `IA`, or `R8`, depending
on the **_summand type_**, which is the required form of the item type of `seq`. More precisely, the
result type is the same as the result type for adding two values of the summand type. The result is the
sum of the non-`null` items of the sequence. That is, `null` items are ignored.

The general form is similar to the general form of `ForEach`. In fact, the general form,
```
Sum(seq_1, seq_2, ..., seq_n, selector)
```
is equivalent to
```
ForEach(seq_1, seq_2, ..., seq_n, selector)->Sum()
```
Each `seq_k` is a sequence and the `selector` must be of numeric type, either
[required or optional](02-TypesAndValues.md#optional-types). Each sequence argument **_provides_** a
current item value to the `selector`, as explained in the
[Argument Names and Scoping](#argument-names-and-scoping) section. That is, the current item of each
sequence argument is **_in scope_** in the `selector`. The sequences are iterated in parallel. The
`selector` is evaluated for each set of corresponding sequence items. Each such evaluation that is not `null`
contributes a **_summand_** to the final sum. The **_summand type_** is the required form of the type of the
`selector`. As with the simple form, the result type is one of the
[major numeric types](02-TypesAndValues.md#major-numeric-types) `U8`, `I8`, `IA`, or `R8`, 
depending on the **_summand type_**.

For example, if Orders is a table equivalent to
```
[ { Customer: "Sally", Amt: 3, Price: 25 },
  { Customer: "Bob",   Amt: 7, Price: 21 },
  { Customer: "Ahmad", Amt: 2, Price: 26 },
  { Customer: "Yael",  Amt: 5, Price: null }, ]
```
then the invocation
```
Sum(order: Orders, order.Amt * order.Price)
```
produces `274`. Note that Yael’s order is ignored for this sum, since the corresponding product is `null`.

As explained in the [Argument Names and Scoping](#argument-names-and-scoping) section, this expression
may be abbreviated to
```
Sum(Orders, Amt * Price)
```
The `SumBig` function differs from `Sum` only in its choice of **_result type_**. When the summand type
is a (required or optional) floating-point type, the result type is `R8`. Otherwise, the summand type is
`IA`, the arbitrary precision integer type. In the latter case, `SumBig` avoids the possibility of overflow.

The `SumK` function uses result type `R8`, regardless of the summand type. This function applies a
[Kahan summation algorithm](https://en.wikipedia.org/wiki/Kahan_summation_algorithm) to improve the
accuracy of the result.

The `SumC`, `SumBigC`, and `SumKC` functions are like `Sum`, `SumBig`, and `SumK`, respectively, except
that they return a record containing two fields, named `Sum` and `Count`. The `Count` field contains the
number of non-`null` values added to produce the `Sum`. For the table above,
```
SumC(Orders, Amt * Price)
```
produces a record equivalent to
```
{ Count: 3, Sum: 274 }
```
Note that the table contains `4` records, but the last row produced a `null` value, so it is omitted
from the `Sum` and `Count` fields.

### Mean Functions

The `Mean` function is similar to the `SumK` function, except that it produces the sum divided by the
count of non-`null` values. When the count of non-`null` values is zero, this produces zero.

The `MeanC` function is like `Mean` except it produces a record containing two fields, named `Mean` and
`Count`. The `Count` field contains the number of non-`null` values used to produce the `Mean` field. When
the `Count` field is zero, the `Mean` field is also zero.

### Min and Max Functions

The `Min` and `Max` functions are similar to `Sum` in that there are two forms, namely the single parameter
forms, that take a sequence, and the general forms that take one or more sequences plus a `selector`.
```
Min(seq)
Max(seq)

Min(seq_1, seq_2, ..., seq_n, selector)
Max(seq_1, seq_2, ..., seq_n, selector)
```
In the single parameter forms, the item type of `seq` must be numeric, either
[required or optional](02-TypesAndValues.md#optional-types). The result type is the required form of
that item type. The result is the smallest or largest value of the non-`null` items of the sequence.
That is, `null` items are ignored.

The general forms are similar to the general form of `ForEach`. In fact, the general forms,
```
Min(seq_1, seq_2, ..., seq_n, selector)
Max(seq_1, seq_2, ..., seq_n, selector)
```
are equivalent to
```
ForEach(seq_1, seq_2, ..., seq_n, selector)->Min()
ForEach(seq_1, seq_2, ..., seq_n, selector)->Max()
```
Each `seq_k` is a sequence and the `selector` must be of numeric type, either
[required or optional](02-TypesAndValues.md#optional-types). Each sequence argument **_provides_** a
current item value to the `selector`, as explained in the
[Argument Names and Scoping](#argument-names-and-scoping) section. That is, the current item of each
sequence argument is **_in scope_** in the `selector`. The sequences are iterated in parallel. The
`selector` is evaluated for each set of corresponding sequence items. Each such evaluation that is not
`null` is considered for the final result. The result type is the required form of the selector type.

When there are no non-`null` items, the result is the zero value (the
[**_default value_**](02-TypesAndValues.md#default-values)) of the result type.

The `MinMax` function is like `Min` and `Max` except that it produces a record containing two fields,
named `Min` and `Max`. This computes both the smallest and largest values of the non-`null` items.

The `MinC`, `MaxC`, and `MinMaxC` functions are like `Min`, `Max`, and `MinMax`, respectively except
that they return a record with a `Count` field as well as a `Min` or `Max` field, or both in the case
of `MinMaxC`.

### Fold

The `Fold` function performs general aggregation over a sequence. It has the forms
```
Fold(seq, init_value, new_value)

Fold(seq, init_value, new_value, result)
```
`Fold` is very powerful. In fact, it is possible (though not necessarily easy) to use `Fold` to produce the
results of any other aggregation function, such as `Sum`, `Mean`, or `MinMaxC`.

`Fold` iteratively computes values, starting with the provided `init_value`, by evaluating `new_value` in the
context of both the **_current value_** and the **_current item_** of the sequence. In the first form, the
final value is the result. In the second form, the final value is provided to the `result` argument to produce
the result. Since the current item of `seq` is provided to the `new_value` argument, the `seq` argument may
include a local name specification. Similarly, the **_current value_** is provided to the `new_value`
argument. Since the first current value is the `init_value` argument, that argument may include a local name
specification, which is used as the name of the current value when evaluating `new_value`.

For example, suppose `Events` is a table of event outcomes, each occurring with a certain probability,
specified in a column named `Probability`. If these events are independent, then the probability that
all of them occur simultaneously is the product of the values in the `Probability` column. The expression
```
Fold(Events as evt, 1.0 as prob, evt.Probability * prob)
```
computes the product of the probabilities. Similarly,
```
Fold(k: Sequence(30), cur: 1ia, cur * k)
```
computes `30` factorial (the product of the first `30` positive integers) as 
```
265_252_859_812_191_058_636_308_480_000_000ia
```
Note that we specified `1ia` as the initial value, causing the computation to use the `IA`
(arbitrary precision integer) type to avoid overflow. Without it,
```
Fold(k: Sequence(30), cur: 1, cur * k)
```
Evaluates to the `I8` value `-8_764_578_968_847_253_504`, which is the correct answer reduced
modulo $2^{64}$.

We could instead use floating-point to get an approximate value, as in
```
Fold(k: Sequence(30), cur: 1.0, cur * k)
```
which evaluates to `2.6525285981219103E+32`.

As a more complex example, the sequence of prime numbers up to one hundred may be computed using
```
Fold(n: Range(2, 100), cur: [], cur if cur->Any(n mod it = 0) else cur ++ [n])
```
This produces the sequence
```
[
   2,  3,  5,  7, 11,
  13, 17, 19, 23, 29,
  31, 37, 41, 43, 47,
  53, 59, 61, 67, 71,
  73, 79, 83, 89, 97,
]
```
The **_Fibonacci sequence_** is an example of a linear recurrence, where the next item is a prescribed linear
combination of some previous items. In the case of the Fibonacci sequence, the next item is defined to be the
sum of the previous two items. The $0^{th}$ and $1^{st}$ item are defined to be `0` and `1`, respectively. To
compute the $100^{th}$ item, we will need to perform `99` addition operations on the previous two items.
We will start with a pair of items and then, at each step, we will produce a new pair. The `seq` parameter is
used only to prescribe the number of steps, `99` in this case:
```
Fold(Range(99), cur: (0ia, 1ia), (cur[1], cur[1] + cur[0]))
```
The result is the final two values, as a tuple, namely
```
(218922995834555169026, 354224848179261915075)
```
Note that we used the `IA` type, as in the factorial example, to avoid overflow.

The second form of `Fold`,
```
Fold(seq, init_value, new_value, result)
```
allows computing a desired `result` value from the final aggregation value. In the case of Fibonacci,
we want the result to be only the _last value_, not the last _pair of values_. Since `cur` is the
pair of values, using `cur[1]` for the `result` parameter achieves the desired result. That is,
```
Fold(Range(99), cur: (0ia, 1ia), (cur[1], cur[1] + cur[0]), cur[1])
```
produces the result `354224848179261915075`.

### ScanX and ScanZ

The `ScanX` and `ScanZ` functions are similar to `Fold` except they produce a sequence of results rather
than just a final result. These have forms similar to those of `Fold`, namely
```
ScanX(seq, init_value, new_value)
ScanX(seq, init_value, new_value, result)

ScanZ(seq, init_value, new_value)
ScanZ(seq, init_value, new_value, result)
```
For example,
```
ScanX(k: Sequence(100), cur: 1ia, cur * k)
```
produces the sequence of factorials for `0` through `100`, namely
```
[ 1, 1, 2, 6, 24, 120, 720, ...]
```
Note that the resulting sequence contains `101` values, one for the initial value, and one for each
value of the sequence `Sequence(100)`. In contrast, the result of `ScanZ` has the same number of
items as the `seq` argument, so
```
ScanZ(k: Sequence(100), cur: 1ia, cur * k)
```
produces the sequence of factorials for `1` through `100`, namely
```
[ 1, 2, 6, 24, 120, 720, ...]
```
A good way to remember the distinction between `ScanX` and `ScanZ` is that the `X` version produces
an _extra_ value, corresponding to the `init_value`, while the `Z` version _zips_ the input sequence,
producing one value for each item of `seq` and no more.

Both `ScanX` and `ScanZ` have a form with a final `result` argument. For `ScanZ`, this result argument
is provided both the **_current value_** and the **_current item_** of `seq`. Consequently
```
ScanZ(k: Sequence(100), cur: 1ia, cur * k, { K:k, KFact:cur })
```
produces a table equivalent to
```
[
  { K: 1, KFact: 1 },
  { K: 2, KFact: 2 },
  { K: 3, KFact: 6 },
  { K: 4, KFact: 24 },
  { K: 5, KFact: 120 },
  { K: 6, KFact: 720 },
  ...
]
```
In contrast, for `ScanX`, the result argument is provided the **_current value_** but not the
**_current item_** of `seq`. The result argument is evaluated for each output item, including for the
`init_value`. For the initial value, there is no corresponding sequence item. Consequently,
```
ScanX(k: Sequence(100), cur: 1ia, cur * k, { K: k, KFact: cur })
```
is an error since `k` is not available in the `result` argument. Getting the desired output requires a bit
more complexity, namely
```
ScanX(k:Sequence(100), cur:(0,1ia), (k,cur[1]*k), {K:cur[0], KFact:cur[1]})
```
or, without a final `result` argument,
```
ScanX(k:Sequence(100), cur:{K:0, KFact:1ia}, {K:k, KFact:KFact*k})
```
These produce a table equivalent to
```
[
  { K: 0, KFact: 1 },
  { K: 1, KFact: 1 },
  { K: 2, KFact: 2 },
  { K: 3, KFact: 6 },
  { K: 4, KFact: 24 },
  { K: 5, KFact: 120 },
  { K: 6, KFact: 720 },
  ...
]
```
The `Generate` function is driven by a `count` rather than a sequence. It has the forms
```
Generate(count, selector)

Generate(count, init_value, new_value)

Generate(count, init_value, new_value, result)
```
The first form is essentially shorthand for `ForEach(Range(count), selector)`.
The other forms are essentially shorthand for
```
ScanX(Range(count), init_value, new_value)

ScanX(Range(count), init_value, new_value, result)
```
For example, the factorial table generation above can be accomplished with
```
Generate(k: 100, cur: { K: 0, KFact: 1ia }, { K: k+1, KFact: KFact*(k+1) })
```
Note that the count variable `k` starts at `0`, so we have to add `1` to it in this situation.

### Reverse

The `Reverse` function reverses the order of items in a sequence. It has the form
```
Reverse(seq)
```
where `seq` is a sequence. `Reverse(seq)` is functionally equivalent to `SortDown(seq, #)`, but
generally faster. That is, it is equivalent to sorting the sequence by item index from largest to
smallest but the implementation is more efficient than sorting.

For example,
```
Range(5)->Reverse()
```
produces
```
[4, 3, 2, 1, 0]
```

### Sort, SortUp, SortDown

The `Sort` functions reorder items in a sequence. The items are ordered by one or more **_key_** values and
associated **_direction_** or **_sort order_**. The key values must be of a **_sortable type_**, that is,
of **_text_**, **_numeric_**, **_date_**, or **_time_** type, or the optional form of one of these types.
The **_sortable types_** are exactly the same as the **_comparable types_** defined in the
[Comparison Operators](04-Operators.md#comparison-operators) section.

The **_up_** direction puts smaller items before larger items. The **_down_** direction does the opposite.
A `null` key value is considered to be smaller than a non-`null` key value. A floating-point `NaN` is
considered to be smaller than all other (non-`null`) floating-point values. Note that this ordering
is consistent with the
[**_total comparison operators_**](04-Operators.md#strict-and-total-comparison-modifiers).

The three sort functions differ in their **_default sort order_**. The `SortUp` function uses **_up_**
ordering, the `SortDown` function uses **_down_** ordering, and the `Sort` function uses **_up_** for
text keys and **_down_** for other key types. To override (or to emphasize) these defaults, a
**_sort directive_** is specified. The **_up directive_** is `[<]` and the **_down directive_** is `[>]`.

For text, sort order is also classified as **_case-sensitive_** or **_case-insensitive_**. By default,
the sort functions use case-sensitive sorting. For case-insensitive sorting, a case-insensitive sort
directive must be specified. These are `[~]`, `[~<]`, and `[~>]`, indicating case-insensitive using
the default direction, case-insensitive up, and case-insensitive down, respectively.

The sort functions have one-parameter forms
```
Sort(dir seq)

SortUp(dir seq)

SortDown(dir seq)
```
where `dir` is an optional sort order directive and `seq` is a sequence. In these forms, there is one
sort key, namely the item of the sequence. Consequently, these forms require the item type of `seq` to
be text, numeric, date, or time, or the optional form of one of these types.

For example, if `S` is the sequence `[ 1, 3,-2, null ]`,
```
Sort(S)

SortUp(S)

SortDown(S)
```
result in sequences equivalent to
```
[ 3, 1,-2, null ]

[ null,-2, 1, 3 ]

[ 3, 1,-2, null ]
```
respectively. Note that since `I8` is not the text type, `Sort` defaults to down order (largest first).

When a directive is included, it overrides the default sort order. For example,
```
Sort([<] S)

SortUp([<] S)

SortDown([<] S)
```
all result in a sequence equivalent to `[ null,-2, 1, 3 ]`. Note that the `[<]` directive in `SortUp`
merely emphasizes the sort order, since that directive matches the default sort order.

In the case of the `[~]` directive, the default sort order applies, but is modified to be case insensitive.
For  example, if T is the sequence `[ "A", "b", "B", "a", null ]`,
```
Sort(T)

SortUp(T)

SortDown(T)
```
result in sequences equivalent to
```
[ null, "a", "A", "b", "B" ]

[ null, "a", "A", "b", "B" ]

[ "B", "b", "A", "a", null ]
```
respectively. When the `[~]` directive is used,
```
Sort([~] T)

SortUp([~] T)

SortDown([~] T)
```
result in sequences equivalent to
```
[ null, "A", "a", "b", "B" ]

[ null, "A", "a", "b", "B" ]

[ "b", "B", "A", "a", null ]
```
Note that when the direction is up (for `Sort` and `SortUp`), `"a"` is moved before both `"B"` and `"b"`,
but not before `"A"`, while the order of the values `"b"` and `"B"` is left unchanged.

Similarly, when the order is down (for `SortDown`), `"A"` is moved after both `"b"` and `"B"`, but not
after `"a"`, while the order of the values `"b"` and `"B"` is left unchanged.

This example also demonstrates that reversing the sort order does _not_ necessarily reverse the items
in the result. To reverse a sort order, in addition to the given keys, use a final additional sort key
of `[>] #`, as explained below.

The `Sort` functions also have multi-parameter **_keyed_** forms
```
Sort(seq, dir_1 key_1, dir_2 key_2, ..., dir_n key_n)

SortUp(seq, dir_1 key_1, dir_2 key_2, ..., dir_n key_n)

SortDown(seq, dir_1 key_1, dir_2 key_2, ..., dir_n key_n)
```
where `seq` is a sequence, each `dir_k` is an optional sort order directive, and each `key_k` is a key value.
In these forms, `seq` may have any item type. The current item of `seq` is provided to each `key_k`,
that is, the current item is in scope in the keys, as described in the
[Argument Names and Scoping](#argument-names-and-scoping) section. Consequently, a local name may be
specified as part of the `seq` argument. Each key expression must be of a **_sortable_** type.

For example, if `S` is the sequence above, namely `[ 1, 3,-2, null ]`,
```
SortUp(S, it * it)

SortUp(S as s, s * s)
```
each result in a sequence equivalent to `[ null, 1,-2, 3 ]`. Note that the key expression does not
affect the item values in the result sequence. It only impacts how the values are ordered. In this
case, `-2` is placed after `1` but before `3` because `(-2)*(-2)` falls between `1*1` and `3*3`.

When there are multiple key expressions, the items of `seq` are ordered primarily according to the first
sort key (and direction). When two items in `seq` have equivalent first keys, then the second sort key
(and direction) is used to **_break the tie_**. If an item has equivalent first and second keys, then
the next sort key is used, and so on. That is, the keys after the first are only used to **_break ties_**.

For example, if `T` is the sequence above, namely `[ "A", "b", "B", "a", null ]`,
```
Sort(T, [~] it, [>] it)

Sort(T as t, [~] t, [>] t)
```
each result in a sequence equivalent to `[ null, "A", "a", "B", "b" ]`. The first key uses case-insensitive
up, while the second key breaks ties (when values differ only in case) using the down direction, which
places upper case before lower case. Note that this second key is only used on values that match according
to the first key. In particular, the second key is not used to determine the order of "A" and "B" since
the first key determined that they must occur in that order.

As explained above, reversing the direction of all sort keys does not necessarily reverse the resulting
sequence. In particular, when multiple values are equivalent according to all the specified sort keys,
the relative order of those items is not changed. For example, if `Employees` is the following table,
```
[
  { LastName: "Mason", FirstName: "Amber", Id:101 },
  { LastName: "Smith", FirstName: "Sally", Id:123 },
  { LastName: "Mason", FirstName: "Sally", Id:215 },
  { LastName: "Smith", FirstName: "Amber", Id:357 },
]
```
then `Sort(Employess, [<] LastName)` produces
```
[
  { LastName: "Mason", FirstName: "Amber", Id:101 },
  { LastName: "Mason", FirstName: "Sally", Id:215 },
  { LastName: "Smith", FirstName: "Sally", Id:123 },
  { LastName: "Smith", FirstName: "Amber", Id:357 },
]
```
and `Sort(Employess, [>] LastName)` produces
```
[
  { LastName: "Smith", FirstName: "Sally", Id:123 },
  { LastName: "Smith", FirstName: "Amber", Id:357 },
  { LastName: "Mason", FirstName: "Amber", Id:101 },
  { LastName: "Mason", FirstName: "Sally", Id:215 },
]
```
These are _not_ in opposite orders since there are items that share equivalent key values
(`LastName` in this example). To produce the reverse of the first sorting, we can use
the [_index_ of the item](#auto-indexing) as a tie breaker. That is,
```
Sort(Employees, [>] LastName, [>] #)
```
produces the reversed result:
```
[
  { LastName: "Smith", FirstName: "Amber", Id:357 },
  { LastName: "Smith", FirstName: "Sally", Id:123 },
  { LastName: "Mason", FirstName: "Sally", Id:215 },
  { LastName: "Mason", FirstName: "Amber", Id:101 },
]
```

### KeyJoin

The `KeyJoin` function combines two sequences into a single result sequence by **_matching_** items from the
two sequences. The `KeyJoin` function has the forms
```
KeyJoin(seq_1, seq_2, key_1, key_2, selector)

KeyJoin(seq_1, seq_2, key_1, key_2, selector, left_selector)

KeyJoin(seq_1, seq_2, key_1, key_2, selector, left_selector, right_selector)
```
The `seq_1` and `seq_2` parameters are the sequences to join. Each of these arguments provides item values
to some of the remaining arguments, so each may include a local name for the current item. The `key_1` and
`key_2` parameters specify key values, computed from the corresponding current sequence item. The current
item of `seq_1` is provides to `key_1` and the current item of `seq_2` is provided to `key_2`. When an
item from `seq_1` and an item from `seq_2` result in equivalent key values, those items **_match_** and
are provided to the `selector` argument. The result of the `selector` is then added to the
**_result sequence_**.

The **_key type_** is the type of the `key` arguments (or a common super type of these when they are
different). The key type must be a [**_groupable type_**](#groupby).
The groupable types include **_text_**, **_numeric_**, **_date_**, and **_time_** types and their
optional forms. Moreover, a (required or optional) tuple type is groupable when all of its slot types
are groupable, and a (required or optional) record type is groupable when all of its field types are
groupable. The **_groupable types_** are exactly the same as the **_equatable types_** defined in the
[Comparison Operators](04-Operators.md#comparison-operators) section.

When matching should be on multiple keys, those keys can be combined into a single tuple or record.

The key arguments may include the `[key]` directive. This can enhance readability of the expression by
emphasizing that those are the key arguments.

The first form of `KeyJoin`,
```
KeyJoin(seq_1, seq_2, key_1, key_2, selector)
```
is known as **_inner join_**. Its result sequence contains items corresponding
to matched pairs of items. For example, if Orders is a table (sequence of records) equivalent to
```
[
  { Customer: "Sally", Amt:  3, Price: 25 },
  { Customer: "Bob",   Amt:  7, Price: 21 },
  { Customer: "Ahmad", Amt:  2, Price: 26 },
  { Customer: "Bob",   Amt:  8, Price: 21 },
  { Customer: "Sally", Amt:  4, Price: 25 },
  { Customer: "Ahmad", Amt: 23, Price: 17 },
  { Customer: "Sally", Amt:  1, Price: 25 },
]
```
and `Customers` is a table equivalent to
```
[ { Name: "Alice", State: "WA" },
  { Name: "Bob",   State: "ID" },
  { Name: "Ahmad", State: "MT" }, ]
```
then the expression
```
KeyJoin(o: Orders, c: Customers, o.Customer, c.Name,
    { State: c.State, Value: o.Amt * o.Price })
```
produces a table equivalent to
```
[ { State: "ID", Value: 147 },
  { State: "MT", Value:  52 },
  { State: "ID", Value: 168 },
  { State: "MT", Value: 391 }, ]
```
Note that the item names (`o` and `c`) may be omitted and the `State` field name may be omitted,
shortening the expression to
```
KeyJoin(Orders, Customers, Customer, Name, { State, Value: Amt * Price })
```
When the tables are specified in the opposite order as in
```
KeyJoin(Customers, Orders, Name, Customer, { State, Value: Amt * Price })
```
the order of the results changes to a table equivalent to
```
[ { State: "ID", Value: 147 },
  { State: "ID", Value: 168 },
  { State: "MT", Value:  52 },
  { State: "MT", Value: 391 }, ]
```
Note that this contains the same records as above but in a different order.

The second form of `KeyJoin`,
```
KeyJoin(seq_1, seq_2, key_1, key_2, selector, left_selector)
```
is known as **_left-outer join_**. When an item in `seq_1` has no match in `seq_2`, that item is
provided to the `left_selector` argument, and the result of that is added to the **_result sequence_**.
For example, the two expressions
```
KeyJoin(Orders, Customers, Customer, Name,
    { State, Value: Amt * Price }, { Value: Amt * Price })

KeyJoin(Customers, Orders, Name, Customer,
    { State, Value: Amt * Price }, { State })
```
result in tables equivalent to, respectively,
```
[
  { State: null, Value:  75 },
  { State: "ID", Value: 147 },
  { State: "MT", Value:  52 },
  { State: "ID", Value: 168 },
  { State: null, Value: 100 },
  { State: "MT", Value: 391 },
  { State: null, Value:  25 },
]

[
  { State: "WA", Value: null },
  { State: "ID", Value:  147 },
  { State: "ID", Value:  168 },
  { State: "MT", Value:   52 },
  { State: "MT", Value:  391 },
]
```
In the first, the orders for Sally result in records with `null` value for `State` since Sally is missing
from the `Customers` table. In the second, the customer `Alice` (with state `WA`) has no corresponding
orders, so the result record with `State: "WA"` contains `null` for the Value field.

The third form of `KeyJoin`
```
KeyJoin(seq_1, seq_2, key_1, key_2, selector, left_selector, right_selector)
```
is known as **_full-outer join_**. When an item from `seq_2` has no match in `seq_1`, that item
is provided to the `right_selector` argument, and the result of that is added to the **_result sequence_**.
For example, the expressions
```
KeyJoin(Orders, Customers, Customer, Name,
    { State, Value: Amt * Price }, { Value: Amt * Price }, { State })

KeyJoin(Customers, Orders, Name, Customer,
    { State, Value: Amt * Price }, { State }, { Value: Amt * Price })
```
result in tables equivalent to, respectively,
```
[
  { State: null, Value:   75 },
  { State: "ID", Value:  147 },
  { State: "MT", Value:   52 },
  { State: "ID", Value:  168 },
  { State: null, Value:  100 },
  { State: "MT", Value:  391 },
  { State: null, Value:   25 },
  { State: "WA", Value: null },
]

[
  { State: "WA", Value: null },
  { State: "ID", Value:  147 },
  { State: "ID", Value:  168 },
  { State: "MT", Value:   52 },
  { State: "MT", Value:  391 },
  { State: null, Value:   75 },
  { State: null, Value:  100 },
  { State: null, Value:   25 },
]
```
Note that these are basically the union of the result tables of the two left-outer joins and that these
two tables differ only in the order of their items.

When a key value is `null` or `NaN`, it will not match any item. For example, if `S` is the sequence
```
[ 1.0, 3.0,-2.0, 3.0, null, 0.0/0.0 ]
```
then
```
KeyJoin(a:S, b:S, a, b, a)
```
results in the sequence
```
[ 1.0, 3.0, 3.0,-2.0, 3.0, 3.0 ]
```
Note that this contains no `null` or `NaN` values. Also notice that it contains the value `3.0` a total
of four times. This is because `S` contains two occurrences of `3.0` and each occurrence matches with
each occurrence, for a total of four matchings.

This illustrates that `KeyJoin` (by default) uses
[**_strict equality_**](04-Operators.md#strict-and-total-comparison-modifiers) when determining a match.
If one or both of the `key` arguments has the equality directive `[=]`,
[**_total equality_**](04-Operators.md#strict-and-total-comparison-modifiers) is used instead.
Consequently, these expressions
```
KeyJoin(a:S, b:S, [=] a,       b, a)

KeyJoin(a:S, b:S,     a,  [=]  b, a)

KeyJoin(a:S, b:S, [=] a,  [=]  b, a)

KeyJoin(a:S, b:S, [=] a, [key] b, a)
```
all produce the sequence
```
[ 1.0, 3.0, 3.0, -2.0, 3.0, 3.0, null, 0/0 ]
```

When matching should be done using multiple key values, those values may be combined into a tuple
or record. For example, if the `Orders` table also had a `State` column then a tuple-valued key could
match on both customer name and state, as in
```
KeyJoin(Customers, Orders, (Name, State), (Customer, State), { ... })
```

### CrossJoin

The `CrossJoin` function combines two sequences into a single **_result sequence_** by **_matching_** items
from the two sequences. While [`KeyJoin`](#keyjoin) evaluates two **_key_** expressions to determine a match,
the `CrossJoin` function evaluates a single **_predicate_** expression for each possible pair of items
(one from each sequence). Generally, `KeyJoin` is more efficient and should be used when applicable.
Rexl will automatically reduce some invocations of `CrossJoin` to an equivalent invocation of `KeyJoin`.
For example, with the tables used in the [`KeyJoin`](#keyjoin) section,
```
CrossJoin(o: Orders, c: Customers, o.Customer = c.Name, ...)
```
is reduced to
```
KeyJoin(o: Orders, c: Customers, o.Customer, c.Name, ...)
```

The `CrossJoin` function has the forms
```
CrossJoin(seq_1, seq_2, predicate, selector)

CrossJoin(seq_1, seq_2, predicate, selector, left_selector)

CrossJoin(seq_1, seq_2, predicate, selector, left_selector, right_selector)
```
The `seq_1` and `seq_2` parameters are the sequences to join. Each of these arguments provides item values
to some of the remaining arguments, so each may include a local name for the current item. The `predicate`
parameter is a bool-valued expression computed from a pair of sequence items (one from each sequence).
When the `predicate` produces `true`, the items **_match_** and are provided to the selector argument.
The result of the `selector` is then added to the **_result sequence_**.

As with [`KeyJoin`](#keyjoin), the three forms of `CrossJoin` are known as **_inner join_**,
**_left-outer join_**, and **_full-outer join_**, respectively.

When the examples in the [`KeyJoin`](#keyjoin) section are changed to use `CrossJoin` with the
`key` arguments replaced with the `predicate` expression `key_1 $= key_2`, the results will be identical.
Note the use of [**_strict equality_**](04-Operators.md#strict-and-total-comparison-modifiers),
which is the default for [`KeyJoin`](#keyjoin). For the examples that use the `[=]` directive,
the equivalent predicate with `CrossJoin` is just `key_1 = key_2` or `key_1 @= key_2` instead,
using [**_total equality_**](04-Operators.md#strict-and-total-comparison-modifiers). In fact,
any use of `KeyJoin` can be translated to use `CrossJoin`, but it is best to avoid doing so.

To emphasize, if the `predicate` for `CrossJoin` is of the form `a = b` then it is likely that
`KeyJoin` is a better choice than `CrossJoin` and Rexl will, when possible, translate to use
`KeyJoin`.

The real power of `CrossJoin` comes from the fact that the predicate is not restricted to comparing
for equality. For example, if `Trucks` is a table with a column named `Capacity` and `Loads` is a table
with a column named `Weight`, then
```
CrossJoin(Trucks as Truck, Loads as Load, Weight <= Capacity, { Load, Truck })
```
produces a table of all possible pairings of a load with a truck that has sufficient capacity
for the load.

As another example, suppose `Pets` is a sequence of animal names, such as,
```
[ "dog", "cat", "rabbit", "python", "turtle" ]
```
You are considering getting two pets of different kinds from this sequence. What are all the
possible pairs of pets you could choose? The expression
```
CrossJoin(a:S, b:S, #a < #b, (a, b))
```
produces the ten possible pairs
```
[
  ("dog", "cat"),
  ("dog", "rabbit"),
  ("dog", "python"),
  ("dog", "turtle"),
  ("cat", "rabbit"),
  ("cat", "python"),
  ("cat", "turtle"),
  ("rabbit", "python"),
  ("rabbit", "turtle"),
  ("python", "turtle"),
]
```

### GroupBy

The `GroupBy` function gathers items of a **_source sequence_** into **_groups_**. Each **_group_** results
in an item in the **_result sequence_**. The groups are determined by one or more **_key_** arguments.
The result item for a group is determined by the remaining arguments. The `GroupBy` function is very
flexible. It supports many variations. This discussion includes many examples to demonstrate common
variations.

`GroupBy` has the form
```
GroupBy(seq, arg_1, arg_2, ..., arg_n)
```
The `seq` argument is the **_source sequence_** of items to group. The `seq` argument may include a local
name for the current item, as described in the [Argument Names and Scoping](#argument-names-and-scoping)
section.

Each `arg_k` is a **_selector_** of a particular **_kind_**. The selector kinds are **_key_**,
**_group-map_**, **_item-map_**, and **_auto_**. Each selector may include a directive specifying the kind.
The directives are `[key]`, `[group]`, `[item]`, and `[auto]`, respectively. When a selector does not
include a directive, it is a **_key_** selector unless there are at least two selectors, and it is the
last selector. When there are at least two selectors, and the last selector does not include a directive,
it is an **_auto_** selector if is consists of only a single identifier and it is an **_item-map_**
selector otherwise. From this,
* A **_key_** selector need not include a directive if it is not last or if there is only one selector.
* A **_group-map_** selector must include a directive.
* An **_item-map_** selector need not include a directive if it is last and does not consist of only a single
  [**_identifier_** (simple name)](01-AboutRexl.md#names).
* An **_auto_** selector need not include a directive if it is last.

There must be at least one **_key_** selector. The **_key_** selectors specify how items are grouped.
Each **_key type_** must be a **_groupable type_**.
The groupable types include **_text_**, **_numeric_**, **_date_**, and **_time_** types and their
optional forms. Moreover, a (required or optional) tuple type is groupable when all of its slot types
are groupable, and a (required or optional) record type is groupable when all of its field types are
groupable. The **_groupable types_** are exactly the same as the **_equatable types_** defined in the
[Comparison Operators](04-Operators.md#comparison-operators) section.

Each **_group_** consists of the source items for which all the key arguments **_match_**. For example,
the invocations
```
GroupBy(n: Range(10), n mod 3)

GroupBy(n: Range(10), [key] n mod 3)
```
are equivalent since the [key] directive is optional in this case. These invocations group the integers from
`0` through `9` by their remainder modulo `3`. The result is the sequence of groups. Each group is itself a
sequence of `I8`, so the result of these is equivalent to
```
[
  [ 0, 3, 6, 9 ],
  [ 1, 4, 7 ],
  [ 2, 5, 8 ]
]
```
Similarly, with two keys,
```
GroupBy(n: Range(10), [key] n mod 3, [key] n mod 2)
```
the result is a sequence of sequence of `I8` equivalent to
```
[
  [ 0, 6 ],
  [ 1, 7 ],
  [ 2, 8 ],
  [ 3, 9 ],
  [ 4 ],
  [ 5 ]
]
```
The items in each group share the same remainder module `3` and the same remainder modulo `2`.

The result item type is determined by which kinds of selectors are used and whether they include
local name specifications. The preceding example demonstrates that when there are only key selectors with no
names, the result item type is the same as the source sequence type. Each result item represents one group as
the sequence of source items in that group. Adding names changes things. For example,
```
GroupBy(n: Range(10), [key] Mod3: n mod 3, [key] Mod2: n mod 2)
```
produces a table equivalent to
```
[
  { Mod2: 0, Mod3: 0 },
  { Mod2: 1, Mod3: 1 },
  { Mod2: 0, Mod3: 2 },
  { Mod2: 1, Mod3: 0 },
  { Mod2: 0, Mod3: 1 },
  { Mod2: 1, Mod3: 2 },
]
```
Since some of the selectors specify names, the result is forced to be a sequence of records (table). The 
selectors with names become columns of that table. If a key doesn't include a name (explicitly or
implicitly), that key is not in the result table. For example,
```
GroupBy(n: Range(10), [key] Mod3: n mod 3, [key] n mod 2)
```
produces a table equivalent to
```
[
  { Mod3: 0 },
  { Mod3: 1 },
  { Mod3: 2 },
  { Mod3: 0 },
  { Mod3: 1 },
  { Mod3: 2 },
]
```
To emphasize that a key is not in the result table, one may use `_` for its name. For example,
```
GroupBy(n: Range(10), [key] Mod3: n mod 3, [key] _: n mod 2)
```
produces an equivalent table, that is, with only the `Mod3` column.

To include the items that make up a group, one may use an **_auto_** selector. The auto selector consists
of just a name, with no expression. That name is the field name in the resulting records. For example,
```
GroupBy(n: Range(10), [key] Mod3: n mod 3, [auto] Items)
```
produces a table equivalent to:
```
[
  { Mod3: 0, Items: [ 0, 3, 6, 9 ] },
  { Mod3: 1, Items: [ 1, 4, 7 ] },
  { Mod3: 2, Items: [ 2, 5, 8 ] },
]
```
The `Items` field is of type sequence of `I8` and contains the source items that are in the group.

In this particular example, the last selector is **_auto_**, and the other selector is a **_key_**,
so the directives may be omitted. That is,
```
GroupBy(n: Range(10), Mod3: n mod 3, Items)
```
produces the same table.

It is very common for the source sequence to be a table (sequence of records) and the keys to be fields of the 
records. For example, if `Orders` is a table equivalent to
```
[
  { Customer: "Sally", Amt: 3, Price: 25 },
  { Customer: "Bob",   Amt: 7, Price: 21 },
  { Customer: "Ahmad", Amt: 2, Price: 26 },
  { Customer: "Bob",   Amt: 8, Price: 21 },
  { Customer: "Sally", Amt: 4, Price: 25 },
  { Customer: "Ahmad", Amt: 23, Price: 17 },
  { Customer: "Sally", Amt: 1, Price: 25 },
]
```
then
```
GroupBy(Orders, Customer, Items)
```
produces a table equivalent to
```
[
  {Customer:"Sally", Items:[{Amt:3,Price:25}, {Amt: 4,Price:25}, {Amt:1,Price:25}]},
  {Customer:"Bob",   Items:[{Amt:7,Price:21}, {Amt: 8,Price:21}]},
  {Customer:"Ahmad", Items:[{Amt:2,Price:26}, {Amt:23,Price:17}]},
]
```
Note that the `Items` field is from an auto selector. The values in this field are themselves (nested)
tables. These tables have two fields, `Amt` and `Price`, but not `Customer`. Since `Customer` is used as
a key with an implicit field name, that field is dropped from `Items`. This is another purpose of **_auto_**,
to drop key fields in the nested table.

In contrast
```
GroupBy(Orders, _: Customer, Items)
```
produces a table equivalent to
```
[
  {Items:[
    {Customer:"Sally", Amt: 3, Price:25},
    {Customer:"Sally", Amt: 4, Price:25},
    {Customer:"Sally", Amt: 1, Price:25}]},
  {Items:[
    {Customer:"Bob",   Amt: 7, Price:21},
    {Customer:"Bob",   Amt: 8, Price:21}]},
  {Items:[
    {Customer:"Ahmad", Amt: 2,Price:26},
    {Customer:"Ahmad", Amt:23,Price:17}]},
]
```
where each group record does not have a `Customer` field, but instead `Items` has the `Customer`
field.

When the source sequence is a table, a key need not be a field of the table. For example
```
GroupBy(Orders, Customer, Big: Amt > 3, Items)
```
produces a table equivalent to
```
[
  {Customer:"Sally", Big: false,
   Items:[{Amt:3,Price:25}, {Amt:1,Price:25}]},
  {Customer:"Bob", Big: true,
   Items:[{Amt:7,Price:21}, {Amt:8,Price:21}]},
  {Customer:"Ahmad", Big: false,
   Items:[{Amt:2,Price:26}]},
  {Customer:"Sally", Big: true,
   Items:[{Amt:4,Price:25}]},
  {Customer:"Ahmad", Big: true,
   Items:[{Amt:23,Price:17}]},
]
```
The orders are grouped by both the customer's name and whether the order has `Amt > 3`.

The remaining selector kinds, **_group-map_** and **_item-map_** are used to include additional
computed information for the groups. In the case of **_group-map_**, the selector is provided the
group of source items as a sequence with the name `group`. For example,
```
GroupBy(Orders, Customer, [group] Total: Sum(group, Amt * Price))
```
produces a table equivalent to
```
[
  { Customer: "Sally", Total: 200 },
  { Customer: "Bob",   Total: 315 },
  { Customer: "Ahmad", Total: 443 },
]
```
The selector `Sum(group, Amt * Price)` sums the product of `Amt` and `Price` over the records in the group.
That sum is the value of the `Total` field.

In the case of **_item-map_**, the selector is provided a source **_item_** of the group with the name `item`.
The result is then a _sequence_ of result items. For example,
```
GroupBy(Orders, Customer, [item] Amts: item.Amt)
```
produces a table equivalent to
```
[
  { Customer: "Sally", Amts: [ 3,  4, 1 ] },
  { Customer: "Bob",   Amts: [ 7,  8 ] },
  { Customer: "Ahmad", Amts: [ 2, 23 ] },
]
```
Since the **_item-map_** selector is last, the directive isn't needed. Moreover, since the items are records,
`item` and the dot may be omitted. Consequently, this may be abbreviated to
```
GroupBy(Orders, Customer, Amts: Amt)
```
If the sequence argument includes a name, then that name, rather than `item`, is used to reference the current 
item of the group. For example,
```
GroupBy(order: Orders, Customer, [item] Amts: order.Amt)
```
is equivalent.

Note that an **_item-map_** selector is shorthand for using **_ForEach_** in a **_group-map_** selector.
For example, the expressions
```
GroupBy(Orders, Customer, Amts: Amt)

GroupBy(Orders, Customer, [group] Amts: ForEach(group, Amt))

GroupBy(Orders, Customer, [group] Amts: group.Amt)

GroupBy(Orders, Customer, [group] Amts: Amt)
```
all produce tables equivalent to that shown above. The last two of these use the fact that the dot operator 
extends to sequence.

### Distinct

The `Distinct` function has the forms
```
Distinct(seq)

Distinct(seq, key)
```
where `seq` is a sequence and `key` is an optional selector. The first form is equivalent to
```
Distinct(seq, it)
```
The `key` type must be a [**_groupable type_**](#groupby).
The groupable types include **_text_**, **_numeric_**, **_date_**, and **_time_** types and their
optional forms. Moreover, a (required or optional) tuple type is groupable when all of its slot types
are groupable, and a (required or optional) record type is groupable when all of its field types are
groupable. The **_groupable types_** are exactly the same as the **_equatable types_** defined in the
[Comparison Operators](04-Operators.md#comparison-operators) section.

This produces a sequence consisting of the same values as in `seq` but where all but the first
occurrence of each key value is removed. For example,
```
Distinct([ 1, 0, 1, 1, -2, 0, 1, 2, -2 ])
```
produces a sequence equivalent to `[ 1, 0, -2, 2 ]`. Similarly,
```
Distinct([ 1, 0, 1, 1, -2, 0, 1, 2, -2 ], Abs(it))
```
produces `[ 1, 0, -2 ]`. Since `-2` and `2` have the same absolute value, only the first
occurrence of those is kept.

In general, `Distinct(seq, key)` is equivalent to (but often more efficent than)
```
GroupBy(seq, [key] _: key, [group] _: TakeOne(group))
```

## Math Functions

* [Abs](#abs)
* [Sqrt](#sqrt)
* [Angle and Trigonometric](#angle-and-trigonometric)
* [Exponential and Logarithmic](#exponential-and-logarithmic)
* [Rounding](#rounding)

Rexl includes many mathematical functions. These all extend to
[optional](03-ExtendedOperatorsAndFunctions.md#extending-to-optional),
[sequence](03-ExtendedOperatorsAndFunctions.md#extending-to-sequence), and
[tensor](03-ExtendedOperatorsAndFunctions.md#extending-to-tensor).

### Abs

The `Abs` function takes one parameter of any numeric type and produces the absolute value of that parameter
(of the same numeric type).

**Caution**: The fixed-sized signed integer types, `I1`, `I2`, `I4`, and `I8` all contain one more negative
value than positive value. This smallest negative value is $-2^{n-1}$, where $n$ is the number of bits
(8 times the number of bytes) in the numeric type. Taking the absolute value of such a value overflows
back to the same value. For example,
```
Abs(-128i1)
```
produces `-128` of type `I1`. The problem is that when the true mathematical value, `128` is reduced
module $2^8$ to a value in the `I1` type, the result is `-128`. This is the same issues as when
the [negation operator](04-Operators.md#negation-and-posation) is appled to the smallest `I8` value,
as discussed there.

This is not a problem unique to Rexl, but is inherent with these fixed-sized signed integer types.
For example, the same phenomenon occurs with the `numpy` integer types in Python. The following
Python
```
import numpy as np
x = np.int8(-128)
abs(x)
```
results in
```
RuntimeWarning: overflow encountered in scalar absolute
-128
```

### Sqrt

The `Sqrt` function takes one parameter of floating-point type `R8` and produces the square root of that
parameter (of the same type). When the argument is negative, the result is NaN.

### Angle and Trigonometric

The angle and trigonometric functions all take a single parameter of floating-point type `R8` and produce a value
of that same type.

There are two angle conversion functions, `Radians` and `Degrees`, that convert from degrees to radians and
from radians to degrees, respectively. For example, `Radians(180)` produces an approximation of $\pi$.

The trigonometric functions have both radian and degree forms. These functions include `Sin`, `Cos`, `Tan`,
`Csc`, `Sec`, and `Cot`, for angles in radians, and `SinD`, `CosD`, `TanD`, `CscD`, `SecD`, and `CotD`, for
angles in degrees. When the angle is non-finite (positive infinity, negative infinity, or `NaN`), these
produce `NaN`.

There are three inverse trigonometric functions, namely `Asin`, `Acos`, and `Atan`. These take a value and
produce an angle measured in radians. The `Asin` and `Atan` functions produce angles within $-\pi/2$ to
$\pi/2$. The `Acos` function produces an angle within $0$ to $\pi$. When the argument is not within
$-1.0$ to $1.0$, the `Asin` and `Acos` functions produce `NaN`.

### Exponential and Logarithmic

The exponential and logarithmic functions all take a single parameter of floating-point type `R8` and
produce a value of that same type.

The `Exp(x)` function produces (an approximation to) $e^{x}$, where $e$ is the base of the natural
logarithm.

The `Ln` and `Log10` functions produce the natural logarithm and base-ten logarithm, respectively.

The standard hyperbolic trigonometric functions are `Sinh`, `Cosh`, `Tanh`, `Csch`, `Sech`, and `Coth`.
These are all defined in terms of $e^x$ in the standard way.

### Rounding

The rounding functions take one parameter of floating-point type `R8` and produce a value of that same type.
These functions differ in the direction of rounding. When the parameter is any of the non-finite values
(positive infinity, negative infinity, or `NaN`), the result is that same value. Otherwise, the result is
an integer value (but still of floating-point type) that is "close to" the argument value.

The `Round` function rounds to the closest integer value. When a value is halfway between two integer values,
as in `2.5` or `3.5`, the result is the closest _even_ integer. This is called **_unbiased_** or
**_banker's_** rounding. For example,
```
Round(2.5)

Round(3.5)
```
result in `2.0` and `4.0` respectively.

`RoundUp` rounds toward positive infinity and `RoundDown` rounds toward negative infinity. `RoundIn` rounds
toward zero and `RoundOut` rounds away from zero. `RoundUp` corresponds to what is commonly called
**_ceiling_** and `RoundDown` corresponds to **_floor_**. `RoundIn` corresponds to what some languages call
**_trunc_** or **_truncate_**.

Note that `RoundIn` is the same as `RoundDown` for positive values and the same as `RoundUp` for negative
values. Similarly, `RoundOut` is the same as `RoundUp` for positive values and the same as `RoundDown` for
negative values.

Here are some example inputs and the corresponding results from each of the round functions.

|  x  | Round(x) | RoundUp(x) | RoundDown(x) | RoundIn(x) | RoundOut(x) |
|:---:|:--------:|:----------:|:------------:|:----------:|:-----------:|
| 1.1 |    1.0   |     2.0    |      1.0     |     1.0    |     2.0     |
|-1.1 |   -1.0   |    -1.0    |     -2.0     |    -1.0    |    -2.0     |
| 1.9 |    2.0   |     2.0    |      1.0     |     1.0    |     2.0     |
|-1.9 |   -2.0   |    -1.0    |     -2.0     |    -1.0    |    -2.0     |
| 1.5 |    2.0   |     2.0    |      1.0     |     1.0    |     2.0     |
|-1.5 |   -2.0   |    -1.0    |     -2.0     |    -1.0    |    -2.0     |
| 2.5 |    2.0   |     3.0    |      2.0     |     2.0    |     3.0     |
|-2.5 |   -2.0   |    -2.0    |     -3.0     |    -2.0    |    -3.0     |

## Text Functions

* [Text Length](#text-length)
* [Text Case Mapping](#text-case-mapping)
* [Text Trimming](#text-trimming)
* [Text Extraction](#text-extraction)
* [Text Concatenation](#text-concatenation)
* [Text Search and Replace](#text-search-and-replace)

The text functions are all in the `Text` namespace, that is, their full names start with `Text`, and are
written with a dot between `Text` and the rest of their name. For example, `Text.Len` is the function that
produces the length of a text value. Some of the text functions may also be used as properties on text
values, as described in the [Dot Operator](04-Operators.md#dot-operator) section.

When these functions are used with the
[Function Projection Operator](04-Operators.md#function-projection), the namespace `Text`
may be omitted.

### Text Length

The `Text.Len` function takes a single parameter of type text and produces the length of that value. The
length of the `null` text value is zero. The length of the empty text value is also zero. `Text.Len` may
be used as a property on text values. The expressions
```
Text.Len("Hello")

"Hello"->Text.Len()

"Hello"->Len()

"Hello".Len
```
are equivalent and produce the value `5`.

### Text Case Mapping

The `Text.Lower` function takes a single parameter of type text and produces the text value containing the
lower-case forms of the original characters. Similarly, `Text.Upper` produces the text value containing the
upper-case forms of the original characters. For example,
```
Text.Lower("Sally")

Text.Upper("Sally")
```
produce
```
"sally"

"SALLY"
```
respectively. These functions may be used as properties on text values, so these examples may be written
```
"Sally".Lower

"Sally".Upper
```
respectively.

### Text Trimming

The `Text.Trim`, `Text.TrimStart`, and `Text.TrimEnd` functions all take a single parameter of type text.
These produce the text value containing characters in the original value, with leading and/or trailing white
space characters removed. `Text.Trim` removes both leading and trailing white space characters, while
`Text.TrimStart` removes only leading white space characters, and `Text.TrimEnd` removes only trailing white
space characters. These may be used as properties on text values. For example,
```
"  X  ".Trim

"  X  ".TrimStart

"  X  ".TrimEnd
```
produce the equivalent of
```
"X"

"X  "

"  X"
```
respectively.

### Text Extraction

The `Text.Part` function produces the portion of given `source` text indicated by the `start` and
(optional) `end` indices. It has the following forms:
```
Text.Part(source, start)

Text.Part(source, start, stop)
```
where `source` is a text value, `start` is an `I8` (signed integer) value, and `stop` is an optional `I8`
value.

If the source value is `null`, then the result is also `null`.

The `start` and `stop` values specify indices. An index value `0` means the beginning of `source` and an
index value that is greater than or equal to `Text.Len(source)` means the end of `source`. When `stop` is
omitted, the stop position is the end of `source`. When an index value is negative, the value `Text.Len(source)`
is added to it to get the position. Effectively, a negative index value is interpreted as an offset from the
end of the text. When a position value is (still) negative, it means the beginning of `source`.

If the stop position is at or before the start position, the result is the empty text value. Otherwise,
the result is the text value consisting of the characters between the start and stop positions. For example,
the following invocations produce the values indicated in the corresponding comment:
```
Text.Part("ABCDE",  2) // "CDE"

Text.Part("ABCDE", -3) // "CDE"

Text.Part("ABCDE",  0) // "ABCDE"

Text.Part("ABCDE", -7) // "ABCDE"

Text.Part("ABCDE",  5) // ""

Text.Part("ABCDE",  7) // ""

Text.Part("ABCDE",  2,  4) // "CD"

Text.Part("ABCDE", -3,  4) // "CD"

Text.Part("ABCDE",  2, -1) // "CD"

Text.Part("ABCDE",  0,  2) // "AB"

Text.Part("ABCDE", -5,  2) // "AB"

Text.Part("ABCDE", -4,  2) // "B"

Text.Part("ABCDE", 4,  2) // ""
```

### Text Concatenation

The `Text.Concat` function has the form
```
Text.Concat(seq, separator)
```
where `seq` is a sequence of text and `separator` is a text value. The result is a single text value consisting
of the concatenation of the items from `seq`, separated by the characters in `separator`. For example,
```
Text.Concat([ "Sally", "Bob", "Ahmad" ], "/")
```
produces the text value equivalent to
```
"Sally/Bob/Ahmad"
```

### Text Search and Replace

* [Text IndexOf and LastIndexOf](#text-indexof-and-lastindexof)
* [Text StartsWith and EndsWith](#text-startswith-and-endswith)
* [Text Replace](#text-replace)

#### Text IndexOf and LastIndexOf

The `Text.IndexOf` function has the two forms
```
Text.IndexOf(source, lookup)

Text.IndexOf(source, lookup, start_index)
```
The `source` and `lookup` parameters are text values and the optional `start_index` parameter is an `I8`
(signed integer) value. When `start_index` is omitted, zero is used. When `start_index` is negative, the
value `Text.Len(source)` is added to it to get the start position. Effectively, a negative index value is
interpreted as an offset from the end of the text. If that sum is still negative, the start position is
the beginning of the source text.

This function searches the text in `source` for the text in `lookup` from the start position toward the
end of `source`. If the `lookup` text is not found, this produces `-1`. If the `lookup` text is found,
this produces the starting index in `source` of the text in `lookup`. This treats `null` the same as the
empty text value.

Note that if the start position is beyond `source.Len - lookup.Len`, or if this quantity is negative,
the result will be `-1`. Otherwise, if lookup is `null` or empty, the result will be the start position.

These examples produce the values indicated in the corresponding comments:
```
Text.IndexOf("ABCABC", "B")  //  1

Text.IndexOf("ABCABC", "D")  // -1

Text.IndexOf("ABCABC", "")   //  0

Text.IndexOf("ABCABC", null) // 0

Text.IndexOf("ABCABC", "B", 2) //  4

Text.IndexOf("ABCABC", "B", 5) // -1

Text.IndexOf("ABCABC", "",  3) //  3

Text.IndexOf("ABCABC", "",  6) //  6

Text.IndexOf("ABCABC", "",  7) // -1
```

REVIEW: Need documentation for `Text.LastIndexOf`. Also, also document the ci directive.

#### Text StartsWith and EndsWith

REVIEW: Need documentation for `Text.StartsWith` and `Text.EndsWith`.

#### Text Replace

REVIEW: Need documentation for `Text.Replace`.

## Chrono Functions

* [Date Construction](#date-construction)
* [Time Construction](#time-construction)
* [Date Parts](#date-parts)
* [Time Parts](#time-parts)

The chrono functions can be used to construct date and time values as well as to extract components of
these values.

The Rexl **_date_** type, introduced in [Chrono Types](02-TypesAndValues.md#chrono-types) and further explained
in [Chrono Operators](04-Operators.md#chrono-arithmetic-operators), represents a date within an idealized
Gregorian calendar, together with a time within that date. This type is also called the **_date-time_** type to
emphasize that it represents both a date and a time within that date.

The idealized Gregorian calendar starts at the beginning of the day (midnight) on January `1` of year `1` and
extends to the end of the day December `31` of year `9999`. Each non-leap year contains the standard `365`
days, while leap years contain an extra day, namely February `29`. A year is a leap year if it is divisible
by `4` but not divisible by `100` unless it is also divisible by `400`. For example, `2024` is a leap year
while `2100`, `2200`, and `2300` are not leap years, but `2400` is a leap year. The minimum value, midnight
of January 1 of year 1 is called the default value of the date type. When an operation results in a value
outside the range of the date type, this default value is produced instead.

The Rexl **_time_** type, introduced in [Chrono Types](02-TypesAndValues.md#chrono-types) and further explained
in [Chrono Operators](04-Operators.md#chrono-arithmetic-operators), represents an amount of time, which
may be positive, zero, or negative. A time value can be thought of as the difference between two **_date_**
values. This type is also called the **_time-span_** type to emphasize that it represents an amount of time
and _not_ a particular time of day.

The resolution of both the date and time types is `0.0000001` second, which is equivalent to `0.1`
microseconds or `100` nanoseconds. This resolution unit is called a **_tick_**. That is, one tick is
`100` nanoseconds. Equivalently, there are `10` million ticks per second.

The time component of a date (date-time) value consists of a number of ticks, at least zero and less than
`864000000000`, which is the number of ticks in `24` hours. The date type does _not_ include any indication
of time zone. When needed, a time-zone offset should be tracked separately as a time (time-span) value.

The time (time-span) type represents an integer number of ticks. The number of ticks that can be represented 
is the same as the range of the `I8` signed integer type.

### Date Construction

The `Date` function constructs a date value from components. It has the full form:
```
Date(year, month, day, hour, minute, second, millisecond, tick)
```
It also has the abbreviated forms:
```
Date(year, month, day, hour, minute, second, millisecond)

Date(year, month, day, hour, minute, second)

Date(year, month, day, hour, minute)

Date(year, month, day, hour)

Date(year, month, day)
```
That is, the parameters from `hour` onward are optional. When any of these is omitted, the corresponding 
argument value is zero.

All of the parameters of the `Date` function are `I8` (signed integer) values. They each have an
acceptable range. If any of the arguments is outside the acceptable range, the result is the **_default_**
date value.
* The `year` argument must be from `1` to `9999`, inclusive.
* The `month` argument must be from `1` to `12`, inclusive.
* The `day` argument must be from `1` to the number of days in the indicated month, inclusive.
* The `hour` argument must be from `0` to `23` inclusive.
* The `minute` argument must be from `0` to `59` inclusive.
* The `second` argument must be from `0` to `59` inclusive.
* The `millisecond` argument must be from `0` to `999` inclusive.
* The `tick` argument must be from 0 to `9999` inclusive.

For example,
```
Date(2022, 5, 11, 8, 11, 52)
```
evaluates to the 5th of May in the year 2022, at 8:11:52 in the morning. Note that there is no AM/PM
parameter. To specify a time component after noon, add 12 to the hour component as in
```
Date(2022, 5, 11, 20, 11, 52)
```

### Time Construction

The `Time` function constructs a time value from components. It has the full form:
```
Time(days, hours, minutes, seconds, milliseconds, ticks)
```
It also has the abbreviated forms:
```
Time(days, hours, minutes, seconds, milliseconds)

Time(days, hours, minutes, seconds)

Time(days, hours, minutes)

Time(days, hours)

Time(days)
```
That is, the parameters from `hours` onward are optional. When any of these is omitted, the corresponding
argument value is zero.

All of the parameters of the `Time` function are `I8` (signed integer) values. Unlike with the `Date`
function, these values do not have explicitly acceptable ranges. Each can be any `I8` value. However, if
the incremental computation of the total number of ticks overflows, the result is the **_default_** time
value, which is equivalent to zero total ticks.

For example, `Time(15)` represents the amount of time in `15` complete days, while `Time(15, -1)` represents
one hour less than that. Furthermore, `Time(10_000_000)` represents the amount of time in 10 million days.
Note that `Time(11_000_000)` results in the default (zero) time value, since the total number of ticks in
11 million days exceeds the maximum `I8` value.

### Date Parts

The date part functions are all in the `Date` namespace, that is, their full names start with `Date` followed by dot,
followed by their specific name. These functions all accept a single parameter of date type. All of them may be
used as properties on date values.

The **_primitive date component_** functions all produce an `I4` value and produce the values that would be passed
to the [`Date` function](#date-construction) to construct an equivalent date value. Some of these have a corresponding
**_short name_**. These functions are:
* `Date.Year` produces the year component of the date value, from `1` to `9999`, inclusive.
* `Date.Month` produces the month component of the date value, from `1` to `12`, inclusive.
* `Date.Day` produces the day of the month component of the date value, from `1` to `31`, inclusive.
* `Date.Hour` produces the hour component of the date value, from `0` to `23`, inclusive. This has short
  name `Date.Hr`.
* `Date.Minute` produces the minute component of the date value, from `0` to `59`, inclusive. This has short
  name `Date.Min`.
* `Date.Second` produces the second component of the date value, from `0` to `59`, inclusive. This has short
  name `Date.Sec`.
* `Date.Millisecond` produces the millisecond component of the date value, from `0` to `999`, inclusive.
  This has short name `Date.Ms`.
* `Date.Tick` produces the tick component of the date value, from `0` to `9999`, inclusive.

For example, if `d` is a date value, the expressions
```
Date.Millisecond(d)
Date.Ms(d)

d->Date.Millisecond()
d->Date.Ms()

d->Millisecond()
d->Ms()

d.Millisecond
d.Ms
```
all produce the millisecond component of the date value as an integer between `0` and `999`, inclusive.

There are also some specialty date part functions:
* `Date.DayOfYear` produces the day number within the year of the date value, from `1` to `366`, inclusive,
  as an `I4` value. Note that this is one-based, not zero-based.
* `Date.DayOfWeek` produces the day number within the week of the date value, from `0` to `6`, inclusive,
  as an `I4` value. Note that this is zero-based, with `0` representing Sunday.
* `Date.StartOfYear` produces a date value representing the start of the year of the given date value.
  The result type is the date type.
* `Date.StartOfMonth` produces a date value representing the start of the month of the given date
  value. The result type is the date type.
* `Date.StartOfWeek` produces a date value representing the start of the week of the given date value.
  The result type is the date type.
* `Date.Date` produces a date value representing the start of the day of the given date value. That is, the
  result has the same year, month, and day but with zero time components. The result type is the date type.
  This has the alternate name `Date.StartOfDay`.
* `Date.Time` produces a time value representing the amount of time since the beginning of the day. The
  result type is the time type. This has alternate name `Date.TimeOfDay`. Note that adding the results of
  `Date.Date` and `Date.Time` results in the original date value.

For example, if `d` is a date value equivalent to the result of `Date(2022, 5, 11, 8, 11, 52)`,
the expressions
```
d.DayOfYear

d.DayOfWeek

d.StartOfYear

d.StartOfMonth

d.StartOfWeek

d.Date // or d.StartOfDay

d.Time // or d.TimeOfDay
```
evaluate respectively to the equivalent of
```
131i4

3i4

Date(2022, 1, 1)

Date(2022, 5, 1)

Date(2022, 5, 8)

Date(2022, 5, 11)

Time(0, 8, 11, 52)
```

### Time Parts

The time part functions are all in the `Time` namespace, that is, their full names start with `Time` followed by dot,
followed by their specific name. These function all accept a single parameter of time type. All of them may be
used as properties on time values.

The **_primitive time component_** functions all produce an `I4` value and produce values that could be passed to
the [`Time` function](#time-construction) to construct an equivalent time value. If the time value is negative,
then all of these produce values that are less than or equal to zero. Some of these have a corresponding
**_short name_**. These functions are:
* `Time.Day` produces the integer number of days of the time value.
* `Time.Hour` produces the hour component of the time value, from `-23` to `23`, inclusive. This has short
  name `Time.Hr`.
* `Time.Minute` produces the minute component of the time value, from `-59` to `59`, inclusive. This has
  short name `Time.Min`.
* `Time.Second` produces the second component of the time value, from `-59` to `59`, inclusive. This has
  short name `Time.Sec`.
* `Time.Millisecond` produces the millisecond component of the time value, from `-999` to `999`,
  inclusive. This has short name `Time.Ms`.
* `Time.Tick` produces the tick component of the time value, from `-9999` to `9999`, inclusive.

For example, if `t` is a time value representing 1 day plus 35 hours plus 105 minutes, then it is equivalent to
`Time(1, 35, 105)`, which is also equivalent to `Time(2, 12, 45)`. Then the expressions
```
t.Day

t.Hr

t.Min

t.Sec
```
evaluate respectively to the equivalent of
```
2i4

12i4

45i4

0i4
```

As a more complex example, if `t` is a time value equivalent to 3 days in the past plus 35 hours plus 105
minutes, then it is equivalent to `Time(-3, 35, 105)`, which is also equivalent to `Time(-2, 12, 45)`, which is
also equivalent to `Time(-1, -11, -15)`. Then the expressions
```
t.Day

t.Hr

t.Min

t.Sec
```
evaluate respectively to the equivalent of
```
-1i4

-11i4

-15i4

0i4
```

There are also some total time functions. These effectively do unit conversion on the time value, representing
the time value as a single number. For example, `Time.TotalHours` produces the time measured in hours. The
result is of floating-point type `R8` since the time can include fractions of an hour. Similarly, all of the
total time functions produce `R8` except for `TotalTicks`, which produces `I8`. The total time functions are:
* `Time.TotalDays` produces the total number of days (including fractions of a day) in the time value.
  This has short name `Time.TotDays`.
* `Time.TotalHours` produces the total number of hours (including fractions of an hour) in the time value.
  This has short name `Time.TotHrs`.
* `Time.TotalMinutes` produces the total number of minutes (including fractions of a minute) in the time value.
  This has short name `Time.TotMins`.
* `Time.TotalSeconds` produces the total number of seconds (including fractions of a second) in the time value.
  This has short name `Time.TotSecs`.
* `Time.TotalMilliseconds` produces the total number of milliseconds (including fractions of a millisecond)
  in the time value. This has short name `Time.TotMs`.
* `Time.TotalTicks` produces the total number of ticks in the time value. Unlike the other total time
  functions, the result is of type `I8`, since the time value is always an integral number of ticks. This has
  short name `Time.TotTicks`.

For example, if `t` is a time value representing 1 day plus 35 hours plus 105 minutes, then it is equivalent to
`Time(1, 35, 105)`, which is also equivalent to `Time(2, 12, 45)`. Then the expressions
```
t.TotDays

t.TotHrs

t.TotMins

t.TotSecs

t.TotMs

t.TotTicks
```
evaluate respectively to the equivalent of
```
2.53125

60.75

3645.0

218700.0

218700000.0

2187000000000
```
Note that the last value is of type `I8`, while the others are of type `R8`.

## Conversion Functions

* [Numeric CastX Functions](#numeric-castx-functions)
* [Numeric ToX Functions](#numeric-tox-functions)
* [CastDate and ToDate](#castdate-and-todate)
* [CastTime and ToTime](#casttime-and-totime)
* [To](#to)
* [ToText](#totext)

Conversion functions convert from one type of value to another type. A conversion function is identified by the
type that it produces. That function handles all source types that are supported. For example, `CastI8` (also
known as `CastInt`) converts to the `I8` type. It supports many different source types, but only the destination
type `I8`.

Conversion functions typically fall into two groups, the `CastX` functions and the `ToX` functions. Typically, a
`CastX` function produces a required (non-optional) type. If a particular conversion doesn't make sense, the
result is a **_default value_** of the destination type. In contrast, the corresponding `ToX` function produces
`null` or a provided default value when the conversion doesn't make sense.

For example,
```
CastI8("123")
```
produces `123` of type `I8`, but
```
CastI8("Hello")
```
doesn't make sense, so the result is the default `I8` value, namely `0`. In contrast,
```
ToI8("123")
```
produces `123` of type _optional_ `I8` and
```
ToI8("Hello")
```
produces `null` of type optional `I8`.

The `ToI8` function may accept a second parameter to be used as the default value (rather than `null`). In
particular,
```
ToI8("123", -1)
```
produces `123` of type `I8` (_not_ optional) and
```
ToI8("Hello", -1)
```
produces `-1` of type `I8` (not optional). This is similar to doing the more verbose
```
ToI8("Hello") ?? -1
```

### Numeric CastX Functions

The numeric `CastX` functions have the forms
```
CastReal(source)
CastR8(source)
CastR4(source)

CastIA(source)

CastInt(source)
CastI8(source)
CastShort(source)
CastI4(source)
CastI2(source)
CastI1(source)

CastU8(source)
CastU4(source)
CastU2(source)
CastU1(source)
```
The `CastReal` function is an alias for `CastR8`. Similarly, `CastInt` is an alias for `CastI8` and `CastShort`
is an alias for `CastI4`.

The **_result type_** is the numeric type indicated by the function name. The `source` argument may be of
**_numeric_** type, **_text_** type, **_date_** type, or **_time_** type. If the conversion fails, the result
is `0` (of the destination type).

When the source type is **_floating-point_** numeric and the destination type is an **_integer_** type, any
fractional part of the source value is dropped. That is, only the integer part of the value is considered.
This step is equivalent to applying the [`RoundIn`](#rounding) function. When the destination type is a
fixed-sized integer type (not IA), then the remaining integer value is reduced modulo $2^N$ where $N$ is the
number of bits in the destination type. If the source value is non-finite (positive infinity, negative infinity,
or `NaN`), the result is `0` (of the destination type).

For example,
```
CastI8(-500.5)

CastI1(-500.5)
```
produce the equivalent of
```
-500

12i1
```
respectively. To understand the I1 case, note that `-500 + 2*256 = -500 + 512 = 12` so `-500` is
equivalent modulo $2^8$ (`256`) to `12`, which is a value within the `I1` type.

When the source type is an **_integer_** type and the destination type is another **_integer_** type, there
are two cases: the value **_fits_** in the destination type or does not fit. In the former case, that value is the
result. In the latter case, the destination type is a fixed-sized integer type (not `IA`) and the result is
the value reduced modulo $2^N$ where $N$ is the number of bits in the destination type.

For example,
```
CastI4(-500)

CastI1(-500)
```
produce the equivalent of
```
-500i4

12i1
```
When the source type is **_numeric_** and the destination type is a **_floating-point_** type, the conversion
is the standard one. Note that casting an `R8` value to `R4` may produce an infinite value even though the
source value is finite. For example, `CastR4(1e100)` produces positive infinity of type `R4`.

When the source type is **_text_** and the destination type is an **_integer_** type, the text value is parsed
as an integer. If that parsing fails, the result is zero of the destination type. If that parsing succeeds,
the integer value is cast to the destination integer type. Note that this may involve reducing module $2^N$
where $N$ is the number of bits in the integer type. For example,
```
CastInt("Hello")

CastInt("$150")

CastInt("3.50")
```
all produce `0` of type `I8` since each of the text values is not a valid representation of an integer value.
The expressions
```
CastInt("1234567890123456789")

CastInt("12345678901234567890")
```
both succeed at parsing the value as an integer. However, the latter results in an integer value that is
too large for `I8`, so the value is reduced modulo $2^{64}$ and the result of these is
```
1234567890123456789

-6101065172474983726
```
respectively.

When the source type is **_text_** and the destination type is a **_floating-point_** type, the text value
is parsed as a floating-point number. If that parsing fails, the result is zero of the destination type.
If that parsing succeeds, the value is cast to the destination floating-point type. Note that the result
may be infinite if the parsed value doesn't fit in the destination type as a finite value. For example,
`CastR4("1e100")` results in an infinite value and not zero.

When the source type is **_time_**, the result is the same as applying the `Time.TotalTicks` function to
get an `I8` value and then applying the `CastX` function to that `I8` value.

When the source type is **_date_**, the result is the same as subtracting the default date value,
`Date(1, 1, 1)`, to get a **_time_** value and then applying the `CastX` function to that time value.

### Numeric ToX Functions

The numeric `ToX` functions have the single parameter forms
```
ToReal(source)
ToR8(source)
ToR4(source)

ToIA(source)

ToInt(source)
ToI8(source)
ToShort(source)
ToI4(source)
ToI2(source)
ToI1(source)

ToU8(source)
ToU4(source)
ToU2(source)
ToU1(source)
```
These also have the two parameter forms
```
ToReal(source, default)
ToR8(source, default)
ToR4(source, default)

ToIA(source, default)

ToInt(source, default)
ToI8(source, default)
ToShort(source, default)
ToI4(source, default)
ToI2(source, default)
ToI1(source, default)

ToU8(source, default)
ToU4(source, default)
ToU2(source, default)
ToU1(source, default)
```
The `ToReal` function is an alias for `ToR8`. Similarly, `ToInt` is an alias for `ToI8` and `ToShort` is
an alias for `ToI4`.

By **_destination type_**, we mean the numeric type indicated by the function name.

The **_result type_** is either the **_required_** or **_optional_** form of the **_destination type_**.
Whether the result type is required or optional depends on the source type as well as on whether a `default`
is supplied and on the type of the default.

The `source` argument may be of **_numeric_** type, **_text_** type, **_date_** type, or **_time_** type.
The type of `default`, when provided, is either the **_required_** or **_optional_** form of the destination
type.

When there is a [standard numeric conversion](02-TypesAndValues.md#standard-numeric-conversions) from the
source type to the destination type, then the result type is the **_required_** form of the destination type,
and the result value will never be `null`. In this case, if `default` is specified, a warning is issued
indicating that the `default` value is ignored.

When there is _not_ a [standard numeric conversion](02-TypesAndValues.md#standard-numeric-conversions)
from the source type to the destination type, then the one parameter form (when `default` is not provided)
is equivalent to providing a `default` value of `null`. When `default` is specified and is of the **_required_**
form of the destination type, the result type is also the required form of the destination type.

When the source type is **_floating-point_** numeric and the destination type is an **_integer_** type, any
fractional part of the source value is dropped. That is, only the integer part of the value is considered.
This step is equivalent to applying the [`RoundIn`](#rounding) function. When the destination type is a
fixed-sized integer type (not `IA`), then the remaining integer value may not fit in the integer type. When
the value doesn't fit, the result is the `default` value (`null` if not specified). If the source value is
non-finite (positive infinity, negative infinity, or `NaN`), the result is the default value.

For example,
```
ToI8(-500.5)
ToI8(-500.5, -1)

ToI1(-500.5)
ToI8(-500.5, -1i1)
```
produce the equivalent of
```
-500 // of type optional I8
-500 // of type required I8

null // of type optional I1
-1i1 // of type required I1
```
respectively. The `I1` cases produce the `default` value, `null` or `-1i1`, because the value `-500` does
not fit in type `I1`.

When the source type is an **_integer_** type and the destination type is another **_integer_** type, there
are two cases: the value **_fits_** in the destination type or does not fit. In the former case, that value
is the result. In the latter case, the result is the `default` value (`null` if not specified).

For example,
```
ToI4(-500)
ToI4(-500, -1i4)

ToI1(-500)
ToI1(-500, -1i1)
```
produce the equivalent of
```
-500i4 // of type optional I4
-500i4 // of type required I4

null // of type optional I1
-1i1 // of type required I1
```
When the source type is **_numeric_** and the destination type is a **_floating-point_** type, the conversion
is the standard one and the result type is the required form of the floating-point type. Note that converting
an `R8` value to `R4` may produce an infinite value even though the source value is finite. For example,
`ToR4(1e100)` produces positive infinity of type `R4`.

When the source type is **_text_** and the destination type is an **_integer_** type, the text value is parsed
as an integer.  If that parsing fails, the result is the `default` value. If that parsing succeeds, but the
integer value doesn't fit in the destination type, the result is the `default` value. Otherwise, the result
is the parsed integer value of the destination type. For example,
```
ToInt("Hello")
ToInt("Hello", -1)

ToInt("$150")
ToInt("$150", -1)

ToInt("3.50")
ToInt("3.50", -1)
```
all produce the `default` value since each of the text values is not a valid representation of an integer value.
Specifically, these produce
```
null
-1

null
-1

null
-1
```
respectively.

In contrast, the expressions
```
ToInt("1234567890123456789")
ToInt("1234567890123456789", -1)

ToInt("12345678901234567890")
ToInt("12345678901234567890", -1)
```
all succeed at parsing the value as an integer. However, the latter two result in an integer value that
is too large for `I8`, so the result of these is
```
1234567890123456789
1234567890123456789

null
-1
```
respectively.

When the source type is **_text_** and the destination type is a **_floating-point_** type, the text value
is parsed as a floating-point number. If that parsing fails, the result is the `default` value. If that
parsing succeeds, the value is cast to the destination floating-point type. Note that the result may be
infinite if the parsed value doesn't fit in the destination type as a finite value. For example,
`ToR4("1e100")` results in an infinite value and not `null`.

When the source type is **_time_**, the result is the same as applying the `Time.TotalTicks` function to
get an `I8` value and then applying the `ToX` function to that `I8` value.

When the source type is **_date_**, the result is the same as subtracting the default date value,
`Date(1, 1, 1)`, to get a **_time_** value and then applying the `ToX` function to that time value.

### CastDate and ToDate

The `CastDate` function has two forms
```
CastDate()

CastDate(source)
```
The form with no parameters produces the **_default date value_**, namely the equivalent of `Date(1, 1, 1)`.

In the single parameter form, `source` may be of numeric type or text type. The `source` value is converted
to a date value. If the conversion fails, the result is the default value, `Date(1, 1, 1)`.

For a numeric `source` value, the number is first converted to an integer (if it is floating-point). For this,
non-finite floating-point values result in zero. The result is the date value with that integer as its
**_total tick count_** as specified in [Chrono Types](02-TypesAndValues.md#chrono-types). If the number
of ticks is out of range, the result is the default date value, `Date(1, 1, 1)`. For example,
```
CastDate(0)
CastDate(1)
CastDate(-1)

CastDate(3155378975999999999)
CastDate(3155378976000000000)
```
produce the equivalent of
```
Date(1, 1, 1)
Date(1, 1, 1, 0, 0, 0, 0, 1)
Date(1, 1, 1)

Date(9999, 12, 31, 23, 59, 59, 999, 9999)
Date(1, 1, 1)
```
Both `-1` and `3155378976000000000` are outside the tick range for the date type, so the corresponding result is
the default date value. In contrast, 3155378975999999999 is the maximum tick value for the date type, so the
result is the maximum date value.

When the `source` parameter is of type text, the text is parsed as a date value. If that parsing fails, the
result is the default date value. For example,
```
CastDate("5/14/2022 9:33 AM")
CastDate("5/14/2022 9:33 PM")

CastDate("Saturday May 14 2022 9:33 AM")
CastDate("Friday May 14 2022 9:33 AM")
```
produce the equivalent of
```
Date(2022, 5, 14, 9, 33)
Date(2022, 5, 14, 21, 33)

Date(2022, 5, 14, 9, 33)
Date(1, 1, 1)
```
The last value fails to convert since the indicated date is Saturday and not Friday, so the result is the
default date value.

The `ToDate` function has the form
```
ToDate(source)
```
It is similar to the `CastDate` function except that its result type is the optional date type and when the 
conversion fails, the result is `null` rather than the default date value. For example,
```
ToDate(0)
ToDate(1)
ToDate(-1)

ToDate(3155378975999999999)
ToDate(3155378976000000000)
```
produce the equivalent of
```
Date(1, 1, 1)
Date(1, 1, 1, 0, 0, 0, 0, 1)
null

Date(9999, 12, 31, 23, 59, 59, 999, 9999)
null
```
as optional date values. Similarly, when source is text,
```
ToDate("5/14/2022 9:33 AM")
ToDate("5/14/2022 9:33 PM")

ToDate("Saturday May 14 2022 9:33 AM")
ToDate("Friday May 14 2022 9:33 AM")
```
produce the equivalent of
```
Date(2022, 5, 14, 9, 33)
Date(2022, 5, 14, 21, 33)

Date(2022, 5, 14, 9, 33)
null
```
as optional date values.
Unlike the numeric `ToX` functions, `ToDate` does not have a two-parameter form.

### CastTime and ToTime

The `CastTime` function has two forms
```
CastTime()

CastTime(source)
```
The form with no parameters produces the default time value, namely the equivalent of `Time(0)`.

In the single parameter form, `source` may be of numeric type or text type. The `source` value is
converted to a time value. If the conversion fails, the result is the default value, `Time(0)`.

For a numeric `source` value, the number is first converted to an integer (if it is floating-point). For
this, non-finite floating-point values result in zero. The result is the time value with that integer
as its **_total tick count_** as specified in [Chrono Types](02-TypesAndValues.md#chrono-types). If the
number of ticks is out of range, the result is the default time value, `Time(0)`. For example,
```
CastTime( 0)
CastTime( 1)
CastTime(-1)

CastTime( 0x7FFF_FFFF_FFFF_FFFFia)
CastTime( 0x8000_0000_0000_0000ia) // too big
CastTime(-0x8000_0000_0000_0000ia)
CastTime(-0x8000_0000_0000_0001ia) // too small
```
produce the equivalent of
```
Time(0)
Time(0, 0, 0, 0, 0,  1) //  1 tick
Time(0, 0, 0, 0, 0, -1) // -1 tick

Time( 10675199,  2,  48,  5,  477,  5807) // maximum time
Time(0)
Time(-10675199, -2, -48, -5, -477, -5808) // minimum time
Time(0)
```
Both `0x8000_0000_0000_0000ia` and `0x8000_0000_0000_0001ia` are outside the tick range for the time
type, so the corresponding result is the default (zero) time value. In contrast, `0x7FFF_FFFF_FFFF_FFFFia`
and `-0x8000_0000_0000_0000ia` are the maximum and minimum tick values for the time type, so the results
are the maximum and minimum time values.

When the `source` parameter is of type text, the text is parsed as a time value. If that parsing fails,
the result is the default time value. For example,
```
CastTime(" 0")
CastTime(" 1")
CastTime("-1")

CastTime(" 0:00:00")
CastTime(" 1:00:00")
CastTime("-1:00:00")

CastTime(" 1.23:00:00")
CastTime(" 1.24:00:00") // bad number of hours
CastTime("-1.23:00:00")

CastTime(" 10675199.02:48:05.4775807")
CastTime(" 10675199.02:48:05.4775808") // too big
CastTime("-10675199.02:48:05.4775808")
CastTime("-10675199.02:48:05.4775809") // too small
```
produce the equivalent of
```
Time( 0) //  0 days
Time( 1) //  1 day
Time(-1) // -1 day

Time( 0,  0) //  0 hours
Time( 0,  1) //  1 hour
Time( 0, -1) // -1 hour

Time( 1,  23) // 1 day and 23 hours
Time(0)       // hours out of range
Time(-1, -23) // negative 1 day and 23 hours

Time( 10675199,  2,  48,  5,  477,  5807) // maximum time
Time(0)                                   // out of range
Time(-10675199, -2, -48, -5, -477, -5808) // minimum time
Time(0)                                   // out of range
```
Note that text values that are outside the time tick range result in the default (zero) time.

The `ToTime` function has the form
```
ToTime(source)
```
It is similar to the `CastTime` function except that its result type is the optional time type and when
the conversion fails, the result is `null` rather than the default time value. For example,
```
ToTime( 0)
ToTime( 1)
ToTime(-1)

ToTime( 0x7FFF_FFFF_FFFF_FFFFia)
ToTime( 0x8000_0000_0000_0000ia) // too big
ToTime(-0x8000_0000_0000_0000ia)
ToTime(-0x8000_0000_0000_0001ia) // too small
```
produce the equivalent of
```
Time(0)
Time(0, 0, 0, 0, 0,  1) //  1 tick
Time(0, 0, 0, 0, 0, -1) // -1 tick

Time( 10675199,  2,  48,  5,  477,  5807) // maximum time
null
Time(-10675199, -2, -48, -5, -477, -5808) // minimum time
null
```
as optional time values. Similarly, when source is text,
```
ToTime(" 0")
ToTime(" 1")
ToTime("-1")

ToTime(" 0:00:00")
ToTime(" 1:00:00")
ToTime("-1:00:00")

ToTime(" 1.23:00:00")
ToTime(" 1.24:00:00") // bad number of hours
ToTime("-1.23:00:00")

ToTime(" 10675199.02:48:05.4775807")
ToTime(" 10675199.02:48:05.4775808") // too big
ToTime("-10675199.02:48:05.4775808")
ToTime("-10675199.02:48:05.4775809") // to small
```
Produce the equivalent of
```
Time( 0) //  0 days
Time( 1) //  1 day
Time(-1) // -1 day

Time( 0,  0) //  0 hours
Time( 0,  1) //  1 hour
Time( 0, -1) // -1 hour

Time( 1,  23) // 1 day and 23 hours
null          // hours out of range
Time(-1, -23) // negative 1 day and 23 hours

Time( 10675199,  2,  48,  5,  477,  5807) // maximum time
null                                      // out of range
Time(-10675199, -2, -48, -5, -477, -5808) // minimum time
null                                      // out of range
```
as optional time values.

Unlike the numeric `ToX` functions, `ToTime` does not have a two-parameter form.

### To

The `To` function has the form
```
To(source, default)
```
This is similar to the numeric `ToX` functions, except that the destination type is inferred from the
type of the `default` argument rather than from the function name. More specifically, if the type of the
`default` argument is the required or optional form of one of the numeric types for which there is a `ToX`
function (which is all numeric types except bool), then the `To` function behaves like the two-parameter
form of that `ToX` function. Otherwise, the `To` function behaves like the two-parameter form of `ToI8`.
For example,
```
To("3.5", -1.0)

To("3.5", -1  )

To("3.5", null)
```
are equivalent to
```
ToR8("3.5", -1.0)

ToI8("3.5", -1  )

ToI8("3.5", null)
```
and produce values equivalent to
```
3.5r8 // of type required R8

-1i8 // of type required I8

null // of type optional I8
```
respectively.

### ToText

* [Chrono Format Specifiers](#chrono-format-specifiers)

The `ToText` function converts a `source` value to text. It has two forms:
```
ToText(source)

ToText(source, format)
```
For the first form, the `source` type may be **_numeric_**, **_date_**, or **_time_**. For the
two-parameter form, the `source` type may be **_date_** or **_time_** and the format should be a
text value containing a [format specifier](#chrono-format-specifiers).

For example,
```
ToText(0xFF)

ToText(1.23e10)

ToText(1.23e100)
```
produce text values equivalent to
```
"255"

"12300000000"

"1.23E+100"
```
Date and time values may be used with or without a [format specifier](#chrono-format-specifiers).
For example,
```
ToText(Date(2022, 5, 11, 16, 28, 37, 123, 4567))

ToText(Date(2022, 5, 11, 16, 28, 37, 123, 4567), "F")

ToText(Date(2022, 5, 11, 16, 28, 37, 123, 4567), "O")

ToText(Date(2022, 5, 11, 16, 28, 37, 123, 4567),
    "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'FFFFFFF")
```
produce text values equivalent to
```
"05/11/2022 16:28:37"

"Wednesday, 11 May 2022 16:28:37"

"2022-05-11T16:28:37.1234567"

"2022-05-11T16:28:37.1234567"
```
The last example uses custom component formatting, while the others use standard format specifiers.

#### Chrono Format Specifiers

When `source` is a [chrono value](02-TypesAndValues.md#chrono-types) (date or time), a format may also
be specified. The format is a text value specifying how the chrono value should be converted to text.

Chrono format specifiers are divided into **_standard_** specifiers and **_custom_** specifiers. The
standard specifiers consist of a single letter, while custom specifiers are any other text value.
A custom specifier encodes the desired constituent parts and may include literal text enclosed between
single quotation characters. For example,
```
"'Year 'yyyy' was at its best in the hour 'HH"
```
is a custom date specifier that encodes some literal text (between ' characters) together with a four
digit year, `yyyy`, and two digit hour, `HH`, in the range `00` to `23`, inclusive. The expression
```
ToText(Date(2022, 05, 11, 16), "'Year 'yyyy' was at its best in the hour 'HH")
```
produces the text value
```
"Year 2022 was at its best in the hour 16"
```
**_Technical note_**: The supported formats match those supported by the .Net libraries for the
`System.DateTime` and `System.TimeSpan` types. Where the .Net documentation states that a format is
culture sensitive, Rexl uses the invariant culture so the output is not dependent on current culture
settings. The .Net documentation for these can be found via the following links:
* Standard date format specifiers: https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings.
* Custom date format specifiers: https://learn.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings.
* Standard time format specifiers: https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-timespan-format-strings.
* Custom time format specifiers: https://learn.microsoft.com/en-us/dotnet/standard/base-types/custom-timespan-format-strings.

The standard format specifiers for the date type are summarized in the following table. The example result is 
when the source value is the date value `Date(2022, 8, 11, 16, 28, 37, 123, 4567)`.

| **Format<br/>Specifier** | **Example**             | **Description** |
|:------------------------:|:-----------------------:|:---------------:|
| `"d"` | `"08/11/2022"`                             | Short date |
| `"D"` | `"Thursday, 11 August 2022"`               | Long date |
| `"f"` | `"Thursday, 11 August 2022 16:28"`         | Full date/time (short time) |
| `"F"` | `"Thursday, 11 August 2022 16:28:37"`      | Full date/time (long time) |
| `"g"` | `"08/11/2022 16:28"`                       | General date/time (short time) |
| `"G"` | `"08/11/2022 16:28:37"`                    | General date/time (long time) |
| `"M"` or `"m"` | `"August 11"`                     | Month/day |
| `"O"` or `"o"` | `"2022-08-11T16:28:37.1234567"`   | Round-trip date/time |
| `"R"` or `"r"` | `"Thu, 11 Aug 2022 16:28:37 GMT"` | RFC1123 |
| `"s"` | `"2022-08-11T16:28:37"`                    | Sortable date/time |
| `"t"` | `"16:28"`                                  | Short time |
| `"T"` | `"16:28:37"`                               | Long time |
| `"u"` | `"2022-08-11 16:28:37Z"`                   | Universal sortable date/time |
| `"U"` | `"Thursday, 11 August 2022 23:28:37"`      | Universal full date/time |
| `"Y"` or `"y"` | `"2022 August"`                   | Year month |
| Other single<br/>character | `null`                | An unsupported format results in<br/>a `null` text value |


The standard format specifiers for the time type are summarized in the following table. The example results
are when the source value is `Time(5, 7, 28, 37, 123, 0)` and `Time(0, 7, 28, 37, 123, 0)`,
respectively.

| **Format<br/>Specifier** | Example               | Description |
|:------------------------:|----------------------:|:-----------:|
| "c" | `"5.07:28:37.1230000"`<br/>`"07:28:37.1230000"` | Constant short:<br/>uses `.` to separate days |
| "g" | `"5:7:28:37.123"`<br/>`"7:28:37.123"` | General short:<br/>uses `:` to separate days,<br/>omit information when not needed |
| "G" | `"5:07:28:37.1230000"`<br/>`"0:07:28:37.1230000"` | General long:<br/>uses `:` to separate days |

The following table specifies some available encodings in custom date format specifiers.

| Character | Description | Supported patterns |
|:---------:|:-----------:|:------------------ |
| `y` | Year | `y` and `yy` produce the year modulo `100` (without the century).<br/>`yy` always produces two digits, with a leading zero when needed.<br/>`yyy` and `yyyy` produce the full year with leading zeros when needed. |
| `M` | Month | `M` and `MM` produce the month number in the range `1` to `12`, inclusive.<br/>`MM` always produces two digits, with a leading zero when needed.<br/>`MMM` produces a short month name, such as `Feb`, while `MMMM` produces a<br/>full month name, such as `February`. |
| `d` | Day | `d` and `dd` produce the day number in the range `1` to `31`, inclusive.<br/>`dd` always produces two digits, with a leading zero when needed.<br/>`ddd` produces a short weekday name, such as `Wed`, while `dddd` produces<br/>a full weekday name, such as `Wednesday`. |
| `h` or `H` | Hour | `h` and `hh` produce the hour in the range `1` to `12`, inclusive.<br/>`hh` always produces two digits, with a leading zero when needed.<br/>`H` and `HH` produce the hour in the range `0` to `23`, inclusive.<br/>`HH` always produces two digits, with a leading zero when needed. |
| `t` | AM/PM | `t` produces the first character of the AM/PM designator.<br/>`tt` produces the full AM/PM designator. |
| `m` | Minute | `m` and `mm` produce the minute of the hour in the range `0` to `59`, inclusive.<br/>`mm` always produces two digits, with a leading zero when needed. |
| `s` | Second | `s` and `ss` produce the second in the minute in the range `0` to `59`, inclusive.<br/>`ss` always produces two digits, with a leading zero when needed. |
| `f` or `F` | Fraction of a<br/>second | Up to seven consecutive `f` or `F` characters produces the fraction of a second.<br/>The number of characters determines the precision displayed.<br/>For example, `fff` produces the milliseconds, `ffffff` produces the<br/>microseconds, and so on. Using `F` suppresses trailing zeros. |

The following table shows thee result of `ToText(Date(2022, 8, 11, 16, 28, 37, 123, 4567), fmt)` for 
various values of `fmt`.

| **Custom Format Specifier**                 | **Date string** |
|:--------------------------------------------|:----------------|
| `"yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffffff"` | `2022-08-11T16:28:37.1234567` |
| `"ddd, dd MMM yyyy HH':'mm':'ss 'PDT'"`     | `Thu, 11 Aug 2022 16:28:37 PDT` |
| `"dddd, dd MMMM yyyy h':'mm':'ss tt"`       | `Thursday, 11 August 2022 4:28:37 PM` |
| `"yyyy'-'MM'-'dd'T'HH':'mm':'ss"`           | `2022-08-11T16:28:37` |
| `"yyyy'-'MM'-'dd HH':'mm':'ss'.'fffff"`     | `2022-08-11 16:28:37.12345` |


The following table specifies some available encodings in custom time format specifiers.

| **Character** | **Description** | **Supported patterns** |
|:-------------:|:---------------:|:-----------------------|
| `d` | Days | Any number of `d` characters in succession produces<br/>the day component using at least the indicated<br/>number of digits, with leading zeros when<bar/>needed. |
| `h` | Hours | `h` and `hh` produce the hour in the range `0` to `23`, inclusive.<br/>`hh` always produces two digits, with a leading zero when needed.<br/>Unlike with date values, `H` is not legal. |
| `m` | Minute | `m` and `mm` produce the minute of the hour in the range `0` to `59`, inclusive.<br/>`mm` always produces two digits, with a leading zero when needed. |
| `s` | Second | `s` and `ss` produce the second in the minute in the range `0` to `59`, inclusive.<br/>`ss` always produces two digits, with a leading zero when needed. |
| `f` or `F` | Fraction of a<br/>second | Up to seven consecutive `f` or `F` characters produces the fraction of a second.<br/>The number of characters determines the precision displayed.<br/>For example, `fff` produces the milliseconds, `ffffff` produces the<br/>microseconds, and so on. Using `F` suppresses trailing zeros. |


## Tuple Functions

The ten tuple item functions take a tuple argument and produce the value from the indicated slot in the tuple.
```
Tuple.Item0(tuple)
Tuple.Item1(tuple)
Tuple.Item2(tuple)
Tuple.Item3(tuple)
Tuple.Item4(tuple)
Tuple.Item5(tuple)
Tuple.Item6(tuple)
Tuple.Item7(tuple)
Tuple.Item8(tuple)
Tuple.Item9(tuple)
```
Tuple slot values can also be accessed using [tuple indexing](04-Operators.md#indexing), for example, `tuple[2]`.
While there are tuple item functions only for slots zero through nine, tuple indexing can be used to access tuple
slots beyond nine. The tuple item functions can be used with [function projection](04-Operators.md#function-projection)
and also as [properties](04-Operators.md#dot-operator), so the following are equivalent:
```
tuple[2]

Tuple.Item2(tuple)

tuple->Tuple.Item2()

tuple->Item2()

tuple.Item2
```
These functions extend to [optional](03-ExtendedOperatorsAndFunctions.md#extending-to-optional),
[sequence](03-ExtendedOperatorsAndFunctions.md#extending-to-sequence), and
[tensor](03-ExtendedOperatorsAndFunctions.md#extending-to-tensor).

## Tensor Functions

* [Shape Reconciliation](#shape-reconciliation)
* [Tensor.Fill and Tensor.From](#tensorfill-and-tensorfrom)
* [Tensor.Build](#tensorbuild)
* [Tensor.Rank and Tensor.Shape](#tensorrank-and-tensorshape)
* [Tensor.Reshape](#tensorreshape)
* [Tensor.ExpandDims](#tensorexpanddims)
* [Tensor.Transpose](#tensortranspose)
* [Tensor.Dot](#tensordot)
* [Tensor Arithmetic Functions](#tensor-arithmetic-functions)
* [Tensor.ForEach](#tensorforeach)

Recall that Rexl is a pure functional expression language. That is, all values are immutable. This includes tensor
values. Other languages such as Python allow values within a tensor to change. Rexl does not allow this. When
a value needs to be modified, a new value is created instead. Consequently, tensor functions and operators do
not modify their input tensors, but instead produce new tensors. In some cases, the result tensor can share
information with an input tensor. For example, the results of the `Tensor.Transpose` and `Tensor.ExpandDims`
functions and the tensor slicing operator always share the cell buffer of their input. Similarly, `Tensor.Reshape`
often shares the input buffer. Other functions may produce a result that shares an input buffer, depending on
the circumstance.

Recall that the [arithmetic operators](04-Operators.md#arithmetic-operators) and others
[extend to tensor](03-ExtendedOperatorsAndFunctions.md#extending-to-tensor).
For example, if `X` and `Y` are two tensors, the expression
```
X + Y
```
is shorthand for the invocation of [`Tensor.ForEach`](#tensorforeach)
```
Tensor.ForEach(x:X, y:Y, x + y)
```

### Shape Reconciliation

### Tensor.Fill and Tensor.From

### Tensor.Build

### Tensor.Rank and Tensor.Shape

### Tensor.Reshape

### Tensor.ExpandDims

### Tensor.Transpose

### Tensor.Dot

### Tensor Arithmetic Functions

### Tensor.ForEach



[_Back to Contents_](../RexlUserGuide.md)

[_Previous section - Operators_](04-Operators.md)
