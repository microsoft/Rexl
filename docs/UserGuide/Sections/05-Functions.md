# Functions

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
* [`Count`](#count)
* [`ForEach`](#foreach-functions) and its variants
* [`Sum`](#sum-functions) and its variants
* [`Mean`](#mean-functions) and its variants
* [`Min` and `Max`](#min-and-max-functions) and their variants
* [`Any` and `All`](#any-and-all)
* [`Fold`, `ScanZ` and `ScanX`](#fold-scan-and-generate-functions)
* [`Take` and `Drop`](#take-and-drop-functions) and their variants
* [`Sort`](#sort-sortup-sortdown) and its variants
* [`KeyJoin`](#keyjoin) and [`CrossJoin`](#crossjoin)
* [`GroupBy`](#groupby) and [`Distinct`](#distinct)
* [`ChainMap`](#chain-functions)
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

## Aggregation Functions

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

### Fold, Scan, and Generate Functions




## Sort, Join and Group Functions

### Sort, SortUp, SortDown

### KeyJoin

### CrossJoin

### GroupBy

### Distinct



## Date and Time Functions

### Date Construction

### Time Construction

### Date Parts

### Time Parts



## Conversion Functions

### Numeric CastX Functions

### Numeric ToX Functions

### CastDate and ToDate

### CastTime and ToTime

### To

### ToText

#### Chrono Format Specifiers



## Tuple Functions



## Tensor Functions

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
