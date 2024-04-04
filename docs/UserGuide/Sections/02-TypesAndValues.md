# Type and Values

Every value in Rexl has a **_type_**. The type of a value determines the kinds of operations that can be performed
with the value. For example, the multiplication operator `*` can be applied to numeric values but not to text
values. When writing Rexl expressions it is important to understand the types of the values used in the
expression.

Rexl automatically infers the type of an expression from the functions, operators, and globals referenced by
the expression. Generally, the author of an expression does not explicitly indicate the types involved.

Rexl has a rich **_type system_**, including **_primitive_** types and several kinds of **_compound_** or **_constructed_** types
such as **_records_**, **_tuples_**, **_tensors_**, and **_sequences_**.

Rexl uses a **_structural type system_**, where compound types are defined purely by their structure and _not_
assigned a name. In contrast, C# uses a **_nominal type system_**, where compound types are given a name and
two types with distinct names can have identical structure. For example, in C# one can define two distinct
classes named `Person` and `Wine`, both containing only fields `Name` of type string and `Age` of type int. In Rexl,
both would be represented by the same record type, namely that with two fields, `Name` of type text and `Age` of
type `I4`.

## Primitive Types

Rexl supports several primitive types. These include:
* The [**_text_**](#text-type) type, which contains sequences (of any length) of Unicode characters, including the empty 
  text value consisting of zero characters. An example of a text literal value in Rexl is `"Hello, World"`.
* [**_Numeric_**](#numeric-types) types, which contain various forms of numeric values. There are several of these, including
  both integer and floating-point types with various precisions. Examples of numeric literal values in
  Rexl include `12`, `3.5`, and `6.02e23`.
* The **_logical_** type, which is also known as the **_bool_** type, contains two values, namely `false` and `true`. 
  This bool type is also considered a numeric type, since it is usable as a number, with `false`
  representing the numeric value `0` and true representing the numeric value `1`.
* The [**_date_**](#chrono-types) type, which represents both a date in an idealized Gregorian calendar, as well as the time 
  within that day. The resolution of this type is to 100 nanoseconds.
* The [**_time_**](#chrono-types) type, which represents a time interval consisting of a number of days, hours, minutes, 
  seconds, and fractions of a second. A time value may be negative. The difference of two date values is 
  a time value. Similarly, a time value may be added to a date value to produce a new date value. The 
  resolution of this type is to 100 nanoseconds.
* The **_link_** types represent links to resources, such as documents, images, videos, audio clips, etc.

## Constructed Types

Rexl supports a rich set of **_compound_** or **_constructed_** types. These are types that are constructed from other 
types. These include:

* A [**_sequence_**](#sequence-types) type is defined by an associated **_item type_**. It contains sequences of values from the
  item type. A sequence can be any length, including the empty sequence consisting of zero items. Note
  that the items in a sequence are ordered and are not necessarily unique. That is, a sequence is not just
  a **_set_**. For example, the Rexl expression `[ 3.5, 7, 12.2, 7 ]` evaluates to a sequence of numbers.
* A [**_tuple_**](#tuple-types) type has an associated **_arity_** (or number of slots) as well as **_slot types_**.
  For example, the Rexl expression `(3, "Hi")` evaluates to a tuple having two slots of types `I8` (the numeric type
  representing signed integers in `8` bytes) and **_text_**, respectively. Note that the slots are ordered and
  need not be of the same type.
* A [**_record_**](#record-types) type has an associated set of **_fields_**, each having a **_name_** and **_type_**.
  For example, the Rexl expression `{ A:3.5, B:true, C:"panda" }` evaluates to a record having field names `A`, `B`, and 
  `C`, of types `R8` (the **_numeric_** type representing floating point numbers in `8` bytes), **_bool_**, and **_text_**, 
  respectively. Unlike slots in a tuple, fields in a record are _not_ ordered. Consequently,
  `{ C:"panda", A:3.5, B:true }` evaluates to the exact same record value.
* A [**_table_**](#table-types) type is a sequence type whose item type is a record type. That is, a table is a sequence of
  records (all of the same type). The **_columns_** of the table type are the **_fields_** of the record type. For
  example, the Rexl expression `[ {A:3, B:"X"}, {A:7, B:"Y"} ]` evaluates to a table with two
  columns named `A` and `B`, with types `I8` and **_text_**.
* A [**_tensor_**](#tensor-types) type is an advanced type used in scientific applications. Like sequence types, tensor types 
  have an associated **_item type_**. They also have an associated **_rank_**, indicating the number of **_dimensions_**.
* A [**_module_**](#module-types) type is an advanced type that represents a collection of named symbols.
  Documentation for modules will be added soon.

## Special Types

Rexl supports two special (esoteric) types:
* The **_general type_**: This is the universal type that contains all possible values produced by Rexl
  formulas.
* The **_vacuous type_**: This is the type that contains no values.

The general type is most commonly produced by combining values of very different types. For example, if `B`
has **_bool_** type, the expression:
```
If(B, 3, "Hello")
```
has the general type. Similarly, the expression:
```
Chain([ 3 ], [ "Hello" ])
```
has type **_sequence of general_**. The general type should be avoided in Rexl formulas. Since the set of 
operations and functions that can be applied to a value depends on the value's type, there is little that can be 
done with a value of type general. Depending on the host of Rexl, many expressions having the general type, such as
`3 if B else "Hello"`, may generate a compilation error.

There is no way in Rexl to materialize a value of the vacuous type. However, the sequence construction 
expression `[]` has type **_sequence of vacuous_**. Similarly, the **_local name_** `x` in the expression
`ForEach(x:[], x)` has vacuous type.

## Optional Types

Rexl supports the concept of a **_missing value_** via the special value `null`. Users of SQL, other database
languages, or object-oriented languages will be familiar with this concept. Note that the handling of `null` in
Rexl is often slightly different from SQL.

Some data types inherently include the `null` value, while others do not. The primitive types that include the
`null` value are the **_text_** type and the **_link_** types. Note that the `null` text value is distinct from the empty text
value consisting of no characters. The only constructed types listed above that contain the `null` value are the
**_sequence_** types. Unlike with **_text_**, a `null` sequence value is indistinguishable from an empty sequence value (of 
the same item type).

The `null` value is _not_ included in the other primitive and constructed types listed above. Any such type is 
called a **_required type_**. However, every required type (that does not include `null`) has an associated **_optional_** 
type that includes all the same values as the required type and also includes the `null` value. For example, `I8` is 
the (required) numeric type representing signed integers using `8` bytes. The optional `I8` type contains all the 
same values as well as the `null` value.

Note that unnecessary use of optional types incurs additional computational cost, so data sources (such as 
imported SQL tables and parquet files) should be constructed to use required types when possible.

## Text Type

The text type includes all ordered finite sequences of Unicode characters, including the empty sequence, as
well as the special value `null`. The `null` value is distinct from the empty text value.

**_Note_**: Even though the text type is defined using the term **_sequence_**, it is not a sequence type, since Rexl does 
not have a type corresponding to Unicode character.

A text literal value is placed in double quotation characters. Text literals can contain a double quotation 
character by doubling the double quotation character. Text literals also support escaping (similar to C#) using
the `\` character. The following are literal text values:
```
"Hello, world"

"I wrote \"Hello\" to C:\\folder\\file.txt"

"I wrote ""Hello"" to C:\\folder\\file.txt"
```
The corresponding text values are:
```
Hello, world

I wrote "Hello" to C:\folder\file.txt

I wrote "Hello" to C:\folder\file.txt
```

Rexl also supports a **_verbatim_** form of text literal where the `\` character is not treated as an escape:
```
@"I wrote ""Hello"" to C:\folder\file.txt"
```

The escape character `\` is used for many types of escaping, well beyond the two cases of `\"` and `\\` in the 
examples above.

## Numeric Types

Rexl currently includes twelve distinct numeric types. These vary in whether they are integer versus floating-point
(capable of representing fractional values), whether they contain negative values, their size (number of
bytes or bits used to represent them), and precision (the number of bits used for their mantissa). These types
are summarized in the following table.

The kind column contains **_float_** for floating-point types, **_signed_** for the signed integer types (that can contain 
negative values), and **_unsigned_** for the unsigned integer types (that cannot contain negative values).

|**Name**|**Kind**|**Size**|**Precision**|  **Minimum Value**       |**Maximum Value**         |
|:------:|:------:|:------:|:-----------:|:------------------------:|:------------------------:|
|  `R8`  | float  |8 bytes | 53 bits     |-1.79769313486232e308     |1.79769313486232e308      |
|  `R4`  | float  |4 bytes | 24 bits     |-3.4028235e38             |3.4028235e38              |
|  `IA`  | signed |variable| variable    |no minimum                |no maximum                |
|  `I8`  | signed |8 bytes | 64 bits     |-9_223_372_036_854_775_808|9_223_372_036_854_775_807 |
|  `I4`  | signed |4 bytes | 32 bits     |-2_147_483_648            |2_147_483_647             |
|  `I2`  | signed |2 bytes | 16 bits     |-32_768                   |32_767                    |
|  `I1`  | signed |1 bytes |  8 bits     |-128                      |127                       |
|  `U8`  |unsigned|8 bytes | 64 bits     |0                         |18_446_744_073_709_551_615|
|  `U4`  |unsigned|4 bytes | 32 bits     |0                         |4_294_967_295             |
|  `U2`  |unsigned|2 bytes | 16 bits     |0                         |65_535                    |
|  `U1`  |unsigned|1 bytes |  8 bits     |0                         |255                       |
| `bool` |unsigned|1 bit   |  1 bit      |0                         |1                         |

The `IA` type is known as the **_arbitrary precision integer type_**. It can represent an integer of any size (subject to
available memory). Note that arithmetic with `IA` is significantly more expensive than arithmetic with the other
numeric types, so it should be used only when needed. The remaining integer types are called **_fixed-sized
integer types_**.

The floating-point types, `R8` and `R4`, use a certain number of bits for their mantissa, with the remaining bits
used to encode a base-two exponent and sign. Arithmetic using these types is inherently approximate since
any result needs to be rounded to the closest representable value. Note that the encoded exponent is a
base-two exponent, so these types can exactly represent only values that can be written as a fraction whose
denominator is a power of two (must be a dyadic rational). Fractions like `1/3` and `1/10` are not exactly
representable, but `1/2` and `3/8` are. The floating-point types contain three non-finite values known as positive
infinity `∞`, negative infinity `-∞`, and `NaN`. The latter stands for **_not a number_**. These values may be generated
from the Rexl formulas `1/0`, `-1/0`, and `0/0`, respectively. The indicated minimum and maximum (finite) values
for these types are approximate, with the number following `e` indicating a base 10 exponent. These are the
smallest and largest finite values that the types can represent.

For the unsigned integer types, the minimum is zero and the maximum is $2^N - 1$, where $N$ is the number of bits
of precision for the type.

For the signed integer types with finite precision, the minimum is $-2^{N-1}$ and the maximum is $2^{N-1} - 1$,
where $N$ is the number of bits of precision.

### Numeric Literals

In Rexl, when writing a numeric literal with no decimal point or exponent, the result will be either of type `I8`,
if the value fits within the range of `I8`, or of type `IA` otherwise. To indicate that the value is of any other
numeric type (except bool), append the name of the numeric type as a suffix. For example, `100` is of type `I8`,
but `100I2` is of type `I2`. Note that the type suffix can be either uppercase or lowercase as in `100i2`.
The bool type does not support a type suffix. The values of bool type must be written `false` (for zero) and `true`
(for one).

When writing a numeric literal with either a decimal point or exponent (or both), the result will be of type `R8`.
To specify that the value should be of type `R4`, append a type suffix, as in `1.5r4`.

Numeric literals may use the underscore character as a digit separator, as in `1_234_567`. Integer values may be 
written in hexadecimal (base sixteen) using the `0x` prefix or in binary (base two) using the `0b` prefix. For 
example, the integer `100` can be written as `0x64` or `0b0110_0100`.

### Standard Numeric Conversions

There are various **_standard numeric conversions_** that allow using a value of one numeric type, the source type,
where a different numeric type, the destination type, is needed. The Rexl compilation process automatically
promotes (or converts) the value from the source type to the destination type.

The standard numeric conversions consist of:
* From any numeric type to the same type (the identity conversion).
* To `R8` from any numeric type. This conversion can lose information when the source type is `IA`, `I8`, or 
  `U8`, since those types all have larger precision (`64` bits or more) than `R8` does (`53` bits).
* To `R4` from any numeric type other than `R8`. This conversion can lose information when the source 
  type is `IA`, `I8`, `U8`, `I4`, or `U4`, since those types all have larger precision (`32` bits or more) than `R4` does
  (`24` bits).
* To `IA`, the arbitrary precision integer type, from any integer type. These conversions do not lose
  information.
* To `I8` from any integer type other than `IA`. Note that this includes conversion from `U8` to `I8`. With this
  conversion (`U8` to `I8`) there is the possibility of large positive values being reinterpreted as negative, so
  Rexl issues a warning when this conversion is used. For any source other than `U8`, these conversions
  do not lose information.
* To a fixed-sized signed integer type from a fixed-sized integer type (signed or unsigned) with smaller
  size. These conversions do not lose information.
* To a fixed-sized unsigned integer type from a fixed-sized unsigned integer type with smaller size. These
  conversions do not lose information.

### Major Numeric Types

The major numeric types are `R8`, `IA`, `I8`, and `U8`.

Generally, the [**_numeric arithmetic operators_**](04-Operators.md#numeric-arithmetic-operators) (addition,
subtraction, multiplication, division, modulus, negation, exponentiation) always use one of these types.
These operators select one of the major numeric types, convert both operands to that type and perform the
operation within that type. The type that is selected depends on the operator and possibly on the types of
the operands. For example, [floating-point division `/`](04-Operators.md#floating-point-division) always
uses `R8`. [Exponentiation](04-Operators.md#exponentiation) selects one of the **_fixed-sized_** major numeric
types (not `IA`). The [integer division and modulus operators](04-Operators.md#integer-division-and-modulus)
select one of the **_integer_** major numeric types (not `R8`). In all cases, the selected type must have
[standard numeric conversions](#standard-numeric-conversions) from the operand types. When multiple
supported types have such conversions, the selected type is the first such type from the ordered list
`U8`, `I8`, `IA`, `R8`.

For example:
* [Adding or subtracting](04-Operators.md#addition-subtraction-multiplication) a `U2` value and a `U4` value
  with the `+` or `-` operator selects the `U8` type.
* [Adding or subtracting](04-Operators.md#addition-subtraction-multiplication) a `U2` value and an `I1` value
  with the `+` or `-` operator selects the `I8` type.
* [Dividing or moding](04-Operators.md#integer-division-and-modulus) a `U8` value by an `IA` value using the
  `div` or `mod` operator selects the `IA` type.
* [Dividing](04-Operators.md#floating-point-division) a `U8` value by an `IA` value using the `/` operator
  selects the `R8` type.

When the selected type is `R8` and the mathematical result requires more than `53` bits of precision, the result is
rounded to the closest value representable by `R8`.

When the selected type is `I8` or `U8` and the mathematical result is outside the range of that type, the result is
reduced modulo $2^{64}$ to a value within the selected type. When this happens, we say the computation
overflowed. For example, multiplying one trillion times itself, `1_000_000_000_000 * 1_000_000_000_000`,
overflows `I8` to produce `2_003_764_205_206_896_640`, which is $10^{24}$ reduced modulo $2^{64}$.

## Chrono Types

Rexl supports a **_date_** type and a **_time_** type, known collectively as **_chrono types_**.

A **_date_** value represents a day in an idealized Gregorian calendar as well as a time value within that day. The
resolution of the time value is 100 nanoseconds (0.0000001 second). The smallest (earliest) possible date value is
the beginning of year one. This smallest value is also the **_default value_** for the date type.
The largest (latest) possible date value is the final representable instant in year `9999`, that is 0.0000001 second
before the beginning of year `10000`.

A **_time_** value represents a time interval consisting of a number of days, hours, minutes, seconds, and fractions 
of a second. Time values can be positive, zero, or negative. The resolution of a time value is 100 nanoseconds
(0.0000001 second). A time value is represented as a number of **_ticks_** (positive, zero, or negative), where each
tick is 100 nanoseconds. This number of ticks must fit in the [`I8` integer type](#numeric-types). That is, the
smallest time value has tick count equal to the smallest value of the `I8` type and the largest time value has tick
count equal to the largest value of the `I8` type. A time value is often rendered with as `[s]D.H:M:S.F` where
`[s]` is an optional `-` or `+` sign, `D` is a number of (24 hour) days, `H` is a number of hours, `M` is a
number of minutes, `S` is a number of seconds, and `F` is the fractional part. Note that `.` is used to separate
the number of days from the time components and also to support the fractional part of a second from the whole
seconds. A `:` is used to separate the hours from minutes and minutes from seconds. With this notation,
the largest time value is `10675199.02:48:05.4775807` and the smallest time value is
`-10675199.02:48:05.4775808`. The default time value is zero, rendered `0.0:0:0.0`.

Some of the [arithmetic operators](04-Operators.md#date-and-time-arithmetic-operators) apply to chrono values.
For example, two date values can be subtracted to get a time value. Similarly, a date value and time value
can be added to get a new date value.

Date and time values can be constructed using the [`Date`](05-Functions.md#date-construction) and
[`Time`](05-Functions.md#time-construction) functions, respectively.
A chrono value can be converted to a text value using the [`ToText`](05-Functions.md#totext) function.

## Sequence Types

As explained in [Constructed Types](#constructed-types), a sequence type is defined by its associated
item type. Consequently, sequences are homogeneous, meaning that all items in a sequence are of the
same type.

There are many ways to generate a sequence. One way is as a constructed sequence, by comma separated
expressions between square braces. For example,
```
[ "Sally", "Bob", "Ahmad" ]
```
creates a sequence of text with three items.

Recall that the items of a sequence need to be of the same type. When the specified values are of different
types, a common super type is used as the item type and each item is converted to that type. For example, the
items in
```
[ true, 3, 7.5 ]
```
are of three distinct types, namely bool, `I8` and `R8`. Each of these can be converted to `R8`, so `R8` is used as the
item type of the sequence and the values `true` and `3` are converted to that type. Consequently, the result is
equivalent to
```
[ 1.0, 3.0, 7.5 ]
```
There are also many functions and operators that produce sequences. For example, the expressions
```
Range(5)

Range(1, 8, 2)

Repeat("Happy", 3)

[ 3, 5, 17 ] ++ Range(5)
```
produce sequences equivalent to
```
[ 0, 1, 2, 3, 4 ]

[ 1, 3, 5, 7 ]

[ "Happy", "Happy", "Happy" ]

[ 3, 5, 17, 0, 1, 2, 3, 4 ]
```

## Tuple Types

As explained in [constructed types](#constructed-types), a tuple type has an **_arity_**, which is the
number of **_slots_** in the tuple, together with a type for each slot. The arity may be any non-negative
integer, including zero. To construct a tuple, place the comma-separated tuple slot values between parentheses.

The arity-zero tuple is written `()`. It contains no information, so it is not very useful.

An arity-one tuple contains the same information as its single slot value, so it is also not commonly used. To
write an arity-one tuple, the value must be followed by a trailing comma, as in `(3,)`. The expression `(3)` is not
a tuple, but just a numeric value.

For higher arity tuples, the values may be followed by a trailing comma. For example, the expressions
```
(3, true, "hi")
(3, true, "hi",)
```
produce the same arity-three tuple value.

## Record Types

As explained in [constructed types](#constructed-types), a record type has an associated set of **_fields_** with
each field having a **_name_** and **_type_**. To construct a record value, specify names and values between
curly braces as in
```
{ First: "Sally", Last: "Ng", Age: 27, FullTime: true }
```
The order of the fields has no effect on the resulting value or type. The order that the fields are displayed in a
host application is determined entirely by the host. A host may use the field order written in a formula to determine
a desired display order.

One may use [**_implicit names_**](01-AboutRexl.md#names) within a record construction when a field value is just a
(dotted) name and the field name matches the last name of the dotted name. For example
```
ForEach(Item:MyTable, { Item:Item, Name:Name, Age:Item.Age, Addr:Item.HomeAddr })
```
may be shortened to
```
ForEach(Item:MyTable, { Item, Name, Item.Age, Addr:Item.HomeAddr })
```
where the field names `Item`, `Name`, and `Age` are **_implicit_**. The name `Addr` must be specified explicitly since it 
differs from `HomeAddr`.

## Table Types

As explained in [constructed types](#constructed-types), a table type is just a sequence type whose item type
is a record type. The record type is said to be the **_row type_** of the table type and the fields of the
record type are said to be the **_columns_** of the table type.

Since a table is a sequence of records, one can be constructed in Rexl as just that, a sequence of records. For 
example,
```
[{Name:"Sally", Age:27}, {Name:"Bob", Age:24}, {Name:"Ahmad", Age:32}]
```
produces a table with two columns, `Name` of type text and `Age` of type `I8` (the default signed integer type), and 
three rows, one for each record.

When one of the records does not contain all the fields of the others, that field is added with `null` value. For 
example, in this expression
```
[{Name:"Sally", Age:27}, {Name:"Bob"}, {Name:"Ahmad", Age:32} ]
```
no age is specified for Bob. In the resulting table, his record has `null` for `Age` and the type of the `Age` column is
**_optional_** `I8` rather than **_required_** `I8`.

## Tensor Types

As explained in [constructed types](#constructed-types), a tensor type is defined by its associated **_item type_**
and **_rank_**. Consequently, tensors are **_homogeneous_**, meaning that all items in a tensor are of the same type.
The **_rank_** is the number of dimensions of the tensor. The dimension values of a tensor define the **_shape_**
of the tensor. The shape is typically represented as a **_tuple_** with the number of slots matching the **_rank_**
of the tensor and each slot of type `I8`.

Rank-one tensors are called **_vectors_** and rank-two tensors are called **_matrices_**. For example, a point in
three-dimensional Euclidean space can be represented as a rank-one tensor with item type `R8` and shape `(3,)`.
Note that a point in five-dimensional Euclidean space would also be represented as a rank-one tensor with item type
`R8`, so these values would be part of the same tensor type. However, the shape of the latter would be `(5,)`.

An RGB image can be represented as a rank three tensor with item type `U1` (byte). The shape of such a tensor 
value would be `(H, W, C)`, where `H` is the height of the image, `W` is the width of the image, and `C` is the 
number of color channels, typically `3` or `4`, one for each of the component colors, red, green, and blue, and
possibly one for the transparency (alpha channel).

There are several ways to construct tensor values. For example,
```
Tensor.From([1, -1, 2, 0, 3, -2], 2, 3)
```
constructs a rank-two tensor of shape `(2, 3)`. In mathematics, this would also be called a `2 x 3` matrix.

If the name `x` references this tensor value, then the individual items (also called **_elements_** or **_cells_**)
of the tensor can be accessed using the tensor [indexing operator](04-Operators.md#indexing). Specifically,
the following indexing expressions result in the values indicated in the corresponding comments:
```
x[0, 0] // 1
x[0, 1] // -1
x[0, 2] // 2
x[1, 0] // 0
x[1, 1] // 3
x[1, 2] // -2
```
For a rank-two tensor like this, we'll often display values in a two dimensional layout such as
```
 1 -1  2
 0  3 -2
```
As explained in [Extending to Tensor](03-ExtendedOperatorsAndFunctions.md#extending-to-tensor)
and [Arithmetic Operators](04-Operators.md#arithmetic-operators), many operators extend to tensors.
In particular,
```
x + 5
```
results in another tensor with shape `(2, 3)` with values
```
 6  4  7
 5  8  3
```
Similarly, if `x` and `y` are two tensors with the same shape, `x + y` produces the **_item-wise_**
(or **_cell-wise_**) sum of those tensors. Similarly, `x * y` produces the item-wise product
(also known as the Hadamard product).

## Module Types

REVIEW: Need to document module types.

[_Back to Contents_](../RexlUserGuide.md)

[_Previous section - About Rexl_](01-AboutRexl.md)

[_Next section - Extended Operators and Functions_](03-ExtendedOperatorsAndFunctions.md)
