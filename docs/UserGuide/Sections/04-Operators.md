# Operators

Rexl includes a rich set of operators, exceeding those found in many other expression languages.

The majority of Rexl operators are **_infix binary operators_**, meaning that they accept two operands with the
operator appearing between the operands. For example, the expression
```
3 * x
```
uses the [**_numeric multiplication_**](#addition-subtraction-multiplication) operator.

There are some **_prefix unary operators_**, where the operator precedes a single operand. For example, the 
expression
```
not x < y
```
uses the prefix unary [`not`](#logical-operators) operator with operand `x < y`. The operand is in
turn an instance of the [comparison operator `<`](#comparison-operators) with operands `x` and `y`.

There is one **_postfix unary operator_**, where the operand precedes the operator, namely
[percent](#percent) as in `x%`. This operator simply divides by `100`.

There are other operator forms that fall outside of these categorizations. For example
```
-1 if x < 0 else +1
```
is an application of the [**_if-else_**](#if-else-operator) operator and produces `-1` if `x < 0` is
`true` and (positive) `1` otherwise.

## Precedence and Associativity

When an expression involves multiple operators, the **_precedence_** and **_associativity_** of the operators
determines the order of operations. For example, the expression
```
-3 + 5 * 2^3
```
could be interpreted naively in several different ways. Left to right evaluation:
```
(((-3) + 5) * 2)^3
```
produces the value `(2 * 2)^3 = 64`. Similarly, right to left evaluation:
```
-(3 + (5 * (2^3)))
```
produces the value `–(3 + (5 * 8)) = -43`. Neither of these follows the Rexl evaluation order, which is:
```
(-3) + (5 * (2^3))
```
producing the value `(-3) + 40 = 37`.

We say that multiplication has higher precedence or binds stronger than addition and raising to a power has
higher precedence than multiplication.

**_Associativity_** governs the evaluation order when operators with the same precedence are involved. For
example, `x - y + z` is evaluated as `(x - y) + z` and not as `x - (y + z)`. We say that addition and
subtraction are **_left associative_**, meaning that the left operation is performed first. In contrast, `2^2^3` is
evaluated as `2^(2^3)` producing `256` and not as `(2^2)^3` producing `64`. We say that power is **_right associative_**.

The following table summarizes the precedence, associativity, and syntax of many of the Rexl operators.
Operators are listed in precedence order, from lowest to hightest precedence.

|**Precedence**|**Associativity**|**Operators**|**Examples**|
|:------------:|:---------------:|:-----------:|:----------:|
| Pipe         | Left            | `\|  _`        | `x + 3 \| _ * 2 \| Sqrt(_)` |
| If           | -               | `if-else`   | `-1 if x < 0 else +1` |
| Coalesce     | Right           | `??`        | `a ?? b ?? 0` |
| Or           | Left            | `or`        | `x < 3 or x > 10` |
| Xor          | Left            | `xor`       | `x < 3 xor y > 20` |
| And          | Left            | `and`       | `x > 10 and y > 20` |
| Not          | Prefix Right    | `not`       | `not 3 <= x < 10` |
| Comparison   | Chaining        | `=  <  >  <=  >=`<br/>or one of the above<br/>preceded by a subset of the<br/>modifiers `not`, `!`, `~`, `$`, `@`,<br/>for example `!~<` | `3 <= x < 10`<br/>`Name ~= "harvey"`<br/>`x not @< y` |
| InHas        | Left            | `in has`<br/>or one of the above<br/>preceded by a subset of the<br/>modifiers `not`, `!`, `~`,<br/>for example `!~has` | `x in [1,2,4]`<br/>`Name !~has "mac"` |
| Concatenation| Left            | `&`<br/>`++` | `"Hello, " & Name`<br/>`Range(3) ++ [7,12]` |
| MinMax       | Left            | `min`<br/>`max` | `x max 0 min 100` |
| BitOr        | Left            | `bor`       | `x bor 1 shl n` |
| BitXor       | Left            | `bxor`      | `x bxor 1 shl n` |
| BitAnd       | Left            | `band`      | `x band 1 shl n` |
| BitNot       | Prefix Right    | `bnot`      | `bnot 1 shl n` |
| BitShift     | Left            | `shl`<br/>`shr  shri  shru` | `1 shl n`<br/>`x shr 1` |
| Add          | Left            | `+  -`      | `x + 3 - 2 * y` |
| Mul          | Left            | `*`<br/>`/   div  mod` | `2 * y`<br/>`y / 2`<br/>`y div 2` |
| Prefix       | Prefix Right    | `+  -`<br/>`!  ~` | `-1 if x < 0 else +1`<br/>`!(3 <= x < 10)` |
| Power        | Right           | `^`         | `x^-y`     |
| Postfix      | Postfix Left    | `%`         | `25%`      |
| Primary      | Left            | `.`<br/>`[ ]`<br/>`->`<br/>`+>` | `record.Field`<br/>`tensor[3, 5:7]`<br/>`a->F(b, c)`<br/>`(x + 3)->(it * 2)`<br/>`x->{Num:it, Square:it*it}`<br/>`record->(A, B)`<br/>`record+>{N:3}` |

## Pipe Operator

The **_pipe_** operator facilitates chaining of computations. It replaces an occurrence of `_` on the right with the
value on the left. For example, the formula:
```
x + 3 | _ * 2 | Sqrt(_)
```
adds `x` and `3`, then multiplies that value times `2`, then takes the square root of that value. That is, this is 
equivalent to:
```
Sqrt((x + 3) * 2)
```
The value of using the pipe operator is that additional operations require only adding characters to the 
end of the expression, not editing on both sides of the expression.

## If-Else Operator

The **_if-else_** operator has three operands, with the **_condition_** in the middle. When the condition
evaluates to `true`, the left operand is evaluated, otherwise, the right operand is evaluated. For example,
```
-1 if x < 0 else +1
```
produces negative one if `x` is negative and positive one otherwise. Note that the left and right
operands and only evaluated if needed. Exactly one of them will be evaluated after the condition
is evaluated.

## Coalesce Operator

The **_coalesce_** operator produces the left operand if it is not `null` and otherwise produces the right operand. 
The right operand is evaluated only when evaluation of the left operand produces `null`. For example:
```
a ?? b ?? 0
```
evaluates `a` and produces its value if it is not `null` and otherwise evaluates `b` and produces its value
if it is not `null` and otherwise evaluates and produces the last operand `0`.

Note that `??` is **_right associative_** so the preceding example is equivalent to
```
a ?? (b ?? 0)
```
Right associativity means these implicit parentheses should be applied, but does _not_ mean that `(b ?? 0)`
is evaluated first in time. Indeed, `(b ?? 0)` is evaluated only if the value of `a` is `null`.

## Logical Operators

The **_logical_** operators are the binary operators `or`, `xor`, and `and`, together with the unary prefix operator `not`.
These operate on either **_required_** or **_optional_** bool values. When the operands are optional (that is, when the
operands can be `null`), three-way logic is used (as in SQL). For example, `true or null` produces `true`.

The operators are listed above in precedence order, so the expression:
```
x < 3 or x > 10 xor y > 20 and z < 100
```
is equivalent to
```
x < 3 or (x > 10 xor (y > 20 and z < 100))
```

Note that `not`, the logical negation operator, has lower precedence than comparison so
```
not 3 <= x < 10
```
is equivalent to
```
not (3 <= x < 10)
```
The prefix unary operator `!` also performs logical negation, but it has higher precedence than comparison.
Consequently, using `!` in the above requires parentheses as in:
```
!(3 <= x < 10)
```

The `or` and `and` operators are **_short-circuiting_**, meaning that their right operand is evaluated
only if needed. For example, in the expression `a or b`, if `a` evaluates to `true`, the result is `true`
regardless of the value of `b` so `b` is not evaluated. Since Rexl is a pure functional language (with no
side effects), the fact that `b` is not evaluated in cases is technically unobservable. However, it is
significant when evaluation of `b` is computationally expensive. It can be benefical to consider this
when applying these logical operators. For example if `a` is a named value and `s` is a sequence of bool,
it is better to use `a or Any(s)` than `Any(s) or a` since if `a` is `true` the former skips the
potentially expensive scan of the sequence `s` looking for a `true` value.

Note that `xor` is not **_short-circuiting_** since if `b` is `true` the result is the logical negation
of the value of `a`.

The logical operators extend to sequence and tensor, but not, as explained above, to optional, since they
inherently operate on both required and optional bool values.

## Comparison Operators

There are five **_root comparison operators_**, namely,
```
=   <   >   <=   >=
```

Comparison operators apply to the **_comparable types_**, which include the numeric, text, date, and time 
types. The result is a logical value, either `false` or `true`.

The operands are converted to a common super type before the comparison is performed. For example, in `A < B`,
if `A` is of type `I8` and `B` is of type `R8`, the value of `A` is converted to `R8` before the comparison
is performed. Since that conversion may involve rounding, the result may be different than expected.
For example:
```
9_999_999_999_999_999i8 < 10_000_000_000_000_000i8

9_999_999_999_999_999i8 < 10_000_000_000_000_000r8
```
result in `true` and `false`, respectively. In the latter, when the left operand is converted `R8`, the
value is rounded to the closest representable value, which happens to be the same as the right value,
not less than it.

**_Equatable types_** are those that support the `=` comparison operator (with or without modifiers). In
addition to the **_comparable types_** listed above, a record or tuple type is **_equatable_** if its
component (field/slot) types are all **_equatable_**. For such a record/tuple type, the result of `=`
is `true` if `=` applied to each pair of corresponding component values results in `true`. For example,
`(a, b) = (c, d)` is `true` if `a = c` and `b = d` are both true.

### Comparison Modifiers

There are five **_comparison-modifiers_**, namely,
```
not   !   ~   $   @
```

A subset of the **_comparison modifiers_** may be applied to a **_root comparison operator_** by placing
them before the operator. For example, `A != B`, `A !@< B`, and `A ~!>= B`.

#### Inverse Comparison Modifiers

The `not` and `!` comparison modifiers both indicate that the result is the logical inverse of the standard
result. That is, `x not = y` and `x != y` are both equivalent to `not x = y` and `!(x = y)`. Similarly,
`x !< y` is equivalent to `not x < y` and `!(x < y)`.

It is customary to place the `!` or `not` modifier before any other modifiers, that is, `A !~@< B` is
preferable to `A ~!@< B`.

#### Case Insensitive Comparison Modifier

The `~` modifier alters text comparison to be case insensitive. This is only relevant for text values and for
equatable types that contain one or more text component types. For example
```
Name ~= "harvey"
```
is true when the value of `Name` is any casing of the text `harvey`, for example, when it is `Harvey` or `HARVEY`.
The `~` modifier has no effect when comparing non-text primitive values.

#### Strict and Total Comparison Modifiers

The comparison operators inherently handle both optional and required types. That is, they inherently handle 
`null`. In particular, a `null` operand does not produce a `null` logical value. The result of applying comparison
to both optional and required types is always a `true`/`false` value, never `null`.

The special value `null` and special floating-point value `NaN` require careful handling. The `null` value is often
interpreted to mean an unknown value. Similarly, the floating-point `NaN` value is considered **_indeterminate_**,
meaning that the value is not well-defined by the rules of mathematics. For example, dividing zero by zero
produces `NaN`, as does subtracting infinity from infinity. This document uses the term **_non-value_** to mean
`null` or `NaN`.

Because of these meanings of `null` and `NaN`, one may ask whether `x = x` should be true when `x` is a non-value.
Traditionally, SQL produces `false` in these cases, while languages such as C# produce `true` for `null` but
`false` for `NaN`. One may hope that the behavior of `=` would be consistent with the behavior of the **_join_** or
**_group-by_** operations. Unfortunately, **_join_** and **_group-by_** are typically inconsistent, with **_group-by_**
matching non-values and **_join_** _not_ matching such values.

Similarly, one may ask how **_ordered_** root comparison operators (`<`, `>`, `<=`, and `>=`) should treat non-values.
Traditionally, both SQL and languages such as C# produce false when either operand of an ordered root
comparison operator is a non-value. This has the unfortunate effect that both `x < y` and `x >= y` are `false`
when either operand is a non-value. That is, `not x < y` is not necessarily equivalent to `x >= y`. Furthermore,
one may hope that the behavior of the ordered comparison operators would be consistent with the behavior
of the **_sort_** operation. However, typically **_sort_** does not discard items, so it cannot be consistent with
ordered root comparison operators producing `false` whenever either operand is a non-value.

To deal with these tricky issues, Rexl offers both **_strict_** and **_total_** (non-strict) forms of the comparison
operators. These forms differ only in their treatment of non-values.

The **_strict_** forms of the comparison operators produce `false` when either operand is a non-value, regardless of
the other operand value. This is consistent with standard SQL. This is also consistent with the default behavior
of the [`KeyJoin`](05-Functions.md#keyjoin) function.

The **_total_** form of `=` produces `true` when the operands are the same non-value. This is consistent with the 
behavior of the [`GroupBy`](05-Functions.md#groupby) and [`Distinct`](05-Functions.md#distinct) functions.

The **_total_** forms of the ordered comparison operators treat `null` as _less than_ all non-`null` values and
`NaN` as _less than_ all values other than `null` or `NaN`. This is consistent with the
[`Sort`](05-Functions.md#sort-sortup-sortdown) functions.

The `@` comparison modifier is used to specify the **_total_** form of a comparison operator while the `$` comparison 
modifier specifies the **_strict_** form. For example,
```
0/0 @< -1/0

0/0 @= 0/0

null @< "hello"

null @= (null if true else "hello")
```
are all `true` while
```
0/0 $< -1/0

0/0 $= 0/0

null $< "hello"

null $= (null if true else "hello")
```
are all `false`. Note in particular that `NaN` is treated as less than any other numeric value, including
**_negative infinity_**. While this is consistent with the behavior of the `Sort` functions, it doesn't
make sense mathematically.

When neither `@` nor `$` is specified, the equality operator `=` is **_total_**, while the ordered comparison
operators are **_strict_**. That is, the default for `=` is `@` and the default for ordered operators is `$`.
Note that this is very close to the behavior of C# except that in Rexl `x = x` is `true` when `x` is `NaN`,
while in C# the result would be `false`.

### Comparison Chaining

Comparison operators can be chained, so
```
3 <= x < 10
```
is equivalent to
```
3 <= x and x < 10
```
This feature is particularly useful when a middle term is a more complex expression as in:
```
3 <= x + y < Count(Items) < 10
```
To write this without chaining, but still avoid multiple evaluations of the same value, one would need to write:
```
With(a: x + y, 3 <= a and With(b: Count(Items), a < b and b < 10))
```
Clearly, chaining should be used in cases like this.

### Extended Comparison Operators

The comparison operators _do not extend_ to optional, since optional is handled directly.

The comparison operators _do extend_ to sequence and to tensor. That is, applying a comparison operator to two
sequences produces a sequence of bool. Similarly applying a comparison operator to two tensors produces
a tensor of bool.

## In and Has Operators

The `in` operator tests for inclusion of the left operand in the right sequence of values. The `has` operator tests
for inclusion of the right operand as sub-text of the left operand. As with comparison operators, the `in` and 
`has` operators support optional **_modifiers_**:
```
not   !   ~
```
The `~` modifier indicates case insensitive comparison should be used while `not` and `!` indicate that the result 
should be the logical inverse of the standard result.

The expression
```
x in [1,2,4]
```
produces `true` if x is one of the indicated values. The expression
```
Name !~has "mac"
```
produces `false` if Name contains consecutive characters that match some casing of the characters `mac`. For 
example, this will be `false` when `Name` has text value `Mack`, or `Amaco`, or `AMACO`, but `true` when it
is `amiable cat`. In the latter, the characters of `mac` are not consecutive.

The `in` operator uses [**_total_**](#strict-and-total-comparison-modifiers) equality testing,
not [**_strict_**](#strict-and-total-comparison-modifiers).

The `in` operator extends to sequence. The `has` operator extends to sequence and tensor. Both inherently 
handle optional.

## Concatenation Operators

The `++` operator performs sequence concatenation. For example,
```
Range(3) ++ [7, 12]
```
produces the sequence `[0, 1, 2, 7, 12]`.

The `&` operator performs text concatenation, record concatenation and tuple concatenation. For example,
when `Name` contains the text value `Sally`, the expressions
```
"Hello, " & Name

"TicTac" & "Toe"

{A:3, B:true} & {B:"New B", C:Name}

(3, true) & ("Hi", 2.5)
```
produce the equivalent of
```
"Hello, Sally"

"TicTacToe"

{A:3, B:"New B", C:"Sally"}

(3, true, "Hi", 2.5)
```
respectively.

With record concatenation, when the two record operands have a field with the same name, for example, the `B`
fields in the record example, the type and value of that field in the result is taken from the right operand.

## Min and Max Operators

The `min` and `max` operators produce the smaller or larger of their operands, respectively. For example,
```
x max 0 min 100
```
produces `0` if `x` is negative, `100` if x is larger than `100`, and the value of `x` otherwise.
It is functionally equivalent to
```
0 if x < 0 else 100 if x > 100 else x
```

These operators apply to the types that support ordered comparison. This includes numeric, text, date, and 
time types.

These operators extend to sequence, and tensor. For non-text comparison, they also extend to optional.

Generally, translation to an equivalent [**_if-else_**](#if-else-operator) expression is non-trivial
when the type is either optional or floating-point.

For non-text comparison, the operators extend to optional, so the result is `null` if either
operand in `null`. For text, these operators uses **_total_** comparison, so `null` is considered less
than non-`null`. For example,
```
null min 3.5

null max 3.5
```
both produce `null`, while
```
null min "Hello"

null max "Hello"
```
produce `null` and `"Hello"` respectively.

For required floating-point (`null` is not possible), if either operand is `NaN`, the result is `NaN`.
For example,
```
0/0 min 3.5

0/0 max 3.5
```
both produce `NaN`.

Floating-point offers another subtlety. The floating-point types contain two representations of zero,
namely both "positive" and "negative" zero. The [comparison operators](#comparison-operators) and the
[`Sort` functions](05-Functions.md#sort-sortup-sortdown) treat these as equal, but the `min` and `max`
operators distinguish between them, treating negative zero as the smaller.

## Bitwise Operators

The **_bitwise_** operators are the binary operators `bor`, `bxor`, and `band`, together with the unary
prefix operator `bnot`. These operate on integer values (not floating-point). They operate on each bit
position of the operands independently.

For example, when `x` is of type `I8` (`8` byte signed integer) and `n` if a non-negative integer less than `64`
(the number of bits in `I8`), the expressions
```
x bor 1 shl n

x bxor 1 shl n

x band bnot 1 shl n

x band 1 shl n
```
produce values with the nth bit set to one, the nth bit inverted, the nth bit set to zero, or all bits except the nth 
bit set to zero. More specifically, if `k` is `8` and `n` is `1`, the expressions
```
Range(k)

Range(k) bor 1 shl n

Range(k) bxor 1 shl n

Range(k) band bnot 1 shl n

Range(k) band 1 shl n
```
produce the sequences
```
[ 0, 1, 2, 3, 4, 5, 6, 7 ]

[ 2, 3, 2, 3, 6, 7, 6, 7 ]

[ 2, 3, 0, 1, 6, 7, 4, 5 ]

[ 0, 1, 0, 1, 4, 5, 4, 5 ]

[ 0, 0, 2, 2, 0, 0, 2, 2 ]
```
Note that these examples use the fact that the bitwise operators extend over sequence. Indeed, these 
operators extend over optional, sequence, and tensor.

## Shift Operators

The **_bit shift_** operators are the binary operators `shl`, `shr`, `shri`, and `shru`. The right operand must be
of type `I8`. The left operand must be an integer type. These shift the bits of the left operand by the amount specified
by the right operand. When the right operand is negative, zero is used instead.

The _left_ shift operator, `shl`, fills vacated bit positions with zero. For example, using the fact that the shift 
operators extend over sequence, the expression
```
1 shl Range(5)
```
produces the sequence
```
[1, 2, 4, 8, 16 ]
```
The remaining shift operators are all _right_ shift operators. When the left operand type is a signed integer type,
then `shr` is the same as `shri`. When the left operand type is an unsigned integer type, then `shr` is the same as
`shru`. The `shri` operator fills the vacated bit positions with the value of the high bit. The `shru` operator fills the
vacated bit positions with zero. For example, the expressions
```
0b10001000i1 shri Range(8)

0b10001000u1 shri Range(8)

0b10001000i1 shru Range(8)

0b10001000u1 shru Range(8)
```
produce these sequences (written in binary, omiting the `0b` prefix and the `i1` or `u1` type suffix),
```
[ 10001000, 11000100, 111000010, 11110001, 11111000, 11111100, 11111110, 11111111 ]

[ 10001000, 11000100, 111000010, 11110001, 11111000, 11111100, 11111110, 11111111 ]

[ 10001000, 01000100, 001000010, 00010001, 00001000, 00000100, 00000010, 00000001 ]

[ 10001000, 01000100, 001000010, 00010001, 00001000, 00000100, 00000010, 00000001 ]
```
Note that the bit patterns are identical in the first two and identical in the last two rows. The difference is that
in the first and third rows, the type is `I1` while in the second and fourth rows, the type is `U1`. This becomes
apparent when writing the result values in decimal:
```
[ -120, -60, -30, -15,  -8,  -4,  -2,  -1 ] // of type I1
[  136, 196, 226, 241, 248, 252, 254, 255 ] // of type U1
[ -120,  68,  34,  17,   8,   4,   2,   1 ] // of type I1
[  136,  68,  34,  17,   8,   4,   2,   1 ] // of type U1
```
Also note that in the first two rows, vacated bits are filled with the high bit, which is `1` in this example, while in 
the last two rows, vacated bits are filled with `0`. This demonstrates the difference between `shri` and `shru`.

These operators extend to optional, sequence, and tensor.

## Arithmetic Operators

Rexl includes several **_arithmetic_** operators. All of these operate on numeric operands. Some can also be used
with **_date_** and **_time_** operands.

For operators that apply to both numeric types and date/time types, the date/time form is used when one or 
both operand types is date or time. Otherwise, the numeric form is used. The following sections cover each 
case.

The addition and subtraction operators have the same precedence, which is lower than multiplication. These 
operators are _left_ associative.

The multiplication, division and modulus operators have the same precedence, which is higher than addition 
but lower than the prefix unary operators. These operators are _left_ associative.

The prefix unary operators have the same precedence, which is higher than multiplication but lower than 
power.

The power operator has higher precedence than multiplication. On the left, it also has higher precedence than 
prefix unary operators. It has lower precedence than postfix unary operators. It is _right_ associative.

The percent operator is the only postfix unary operator.

All these operators extend to optional, sequence, and tensor.

### Numeric Arithmetic Operators

This section defines the arithmetic operators applied to numeric types.
[Another section](#date-and-time-arithmetic-operators) defines the  arithmetic operators applied to date
and time types. If none of the operand types is the date or time type, the numeric form, specified here, applies.

Note that most arithmetic operators for floating-point involve rounding the result to the nearest representable 
value.

Arithmetic operators applied to the fixed-sized integer types may result in **_overflow_**, where the mathematically
correct value cannot fit in the type. In this case, the correct value is reduced modulo $2^N$ to a value in the
range of the type, where $N$ is the number of bits precision supported by the type. For example, multiplying
`0x1_0000_0001` by itself results in the integer value `0x1_0000_0002_0000_0001`, which is outside the `I8` type.
Consequently, this value is reduced modulo $2^{64}$ to the result `0x0000_0002_0000_0001`.

#### Addition, Subtraction, Multiplication

The numeric addition, subtraction, and multiplication operators, `+`, `-`, and `*`, respectively, select a
result type from the set of [**_major numeric types_**](02-TypesAndValues.md#major-numeric-types),
`U8`, `I8`, `IA` and `R8`, as described in that section. The operands are converted to the selected result
type before performing the operation.

#### Floating-Point Division

The numeric floating-point division operator `/` converts the operands to `R8` and produces an `R8` value.
Note that division by zero is not an error. When zero or `NaN` is divided by zero, the result is `NaN`.
The floating-point types have separate representations for "positive" and "negative" zero. These are treated
as equal by the [comparison operators](#comparison-operators), but distinguished by the `min`, `max` and `/`
operators. When a non-zero (and non-`NaN`) value is divided by a zero value, the result is _positive_ infinity
if the sign of the numerator matches the sign of the zero denominator, and the result is _negative_ infinity
if the signs differ.

#### Integer Division and Modulus

The integer division and modulus operators, `div` and `mod`, select a result type from the
[**_major integral types_**](02-TypesAndValues.md#major-numeric-types), `U8`, `I8`, or `IA`, as described
in that section. The operands are converted to the selected result type before performing the operation.

For any of the result types, `x div y` produces the mathematical fraction, `x` over `y`, rounded toward zero to the
closest integer value. When `y` is zero, the result is zero.

For any of the result types, `x mod y` produces the remainder when computing `x div y`. More precisely, the
result of `x mod y` is `x - y * (x div y)`, with the special case that when `y` is zero, the result is zero.

Generally, if `x mod y` is non-zero, then it has the same sign as `x`. This is different from Python where the
integer division operator `//` rounds toward negative infinity and the result of the remainder operation `x % y`
has the same sign as `y`.

#### Exponentiation

The exponentiation (or power) operator `^` selects a result type of `U8`, `I8`, or `R8`, depending on the operand
types. When the result type is `R8`, floating-point exponentiation is performed. Otherwise, integer
exponentiation is performed.

For integer exponentiation:
* If the right operand is less than or equal to `0`, the result is `1`, regardless of the value of the left operand.
* Otherwise, the result is computed modulo $2^{64}$.

#### Negation and Posation

The unary numeric **_negation_** operator `-` negates its operand. When the operand is an
[**_integer-literal_**](/docs/Grammars.md#productions-for-integer-literal), the result type is the
smallest signed integer type for which there is a
[Standard Numeric Conversions](02-TypesAndValues.md#standard-numeric-conversions) from the literal type.
This type need not be a [**_major numeric type_**](02-TypesAndValues.md#major-numeric-types). For example,
`-3i1` is of type `I1` while `-3u1` is of type `I2`.
When the operand is not an [**_integer-literal_**](/docs/Grammars.md#productions-for-integer-literal),
the result type is the same as if the operand were multiplied by negative one of type `I1`.

Note that when the operand is of type `I8` and its value is the smallest `I8` value, negating will
**_overflow_** back to the same value. That is, when `x` has value `-9_223_372_036_854_775_808i8`, then
`-x` will have that exact same value (because of overflow).

The unary numeric **_posation_** operator `+` merely ensures that its operand is numeric and produces
that value. This is included as an operator solely for readability. For example, a Rexl author may choose
to write `-1 if x < 0 else +1` to emphasize that the _else_ value is _positive_ one.

#### Percent

The percent operator divides its operand by `100`. The result is of type `R8`. For example, `25%` produces `0.25`.

### Date and Time Arithmetic Operators

The date and time arithmetic operators, also called the **_chrono arithmetic operators_**, are arithmetic
operators that apply to [date or time](02-TypesAndValues.md#chrono-types) values.

The chrono arithmetic operators are selected when at least one of the operands is of the date type or the time
type. Otherwise, the numeric arithmetic operators are selected.

Recall that the date type encodes a moment in an idealized calendar, including year, month, day within the 
month and time within the day, while the time type encodes a span (or amount) of time, whether positive,
zero, or negative. These types are known as the **_chrono types_**. The resolution of the chrono types is a unit
called **_tick_**, which represents `100` nanoseconds, or `0.0000001` of a second.

The possible number of ticks that the time type can hold is exactly the possible values of the `I8` type,
as described in the [chrono types](02-TypesAndValues.md#chrono-types) section. This number of ticks is called
the **_tick count_** of the time value. Note that this tick count may be negative. Also note that for each tick
count within the range of the `I8` type, there is a unique time value corresponding to that tick count. The
time value with zero tick count is known as the **_default time_** value. The minimum and maximum time values
can be constructed using the [`CastTime` function](05-Functions.md#casttime-and-totime) as
```
CastTime(0x8000_0000_0000_0000i8)

CastTime(0x7FFF_FFFF_FFFF_FFFFi8);
```

The date type has a minimum and maximum value. The minimum date value is also known as the **_default date_**
value and has a **_tick count_** of zero. In general, the **_tick count_** of a date value is the number of ticks
from the the minimum to the date value. The tick count of the maximum date value is `3_155_378_975_999_999_999`.
The maximum data value can be produced via the [`Date` function](05-Functions.md#date-construction) as
```
Date(9999, 12, 31, 23, 59, 59, 999, 9999)
```
It's total tick value can computed from this using the [`Date.TotalTicks` property](05-Functions.md#date-parts)
```
Date(9999, 12, 31, 23, 59, 59, 999, 9999).TotalTicks
```

Chrono operator forms that produce either a date or time value define their **_result tick count_**. That tick
count is converted to the result type. If the result type is date and the result tick count is not valid for
date, then the default date is produced. If the result type is time and the result tick count is outside the
range of `I8`, then the result tick count is reduced modulo $2^{64}$.

#### Chrono Addition

The supported forms of the chrono addition operator are:
* Date `+` time, and time `+` date, producing date.
* Time `+` time, producing time.

The result tick count is the sum of the tick counts of the operands.

#### Chrono Subtraction

The supported chrono forms of subtraction are:
* Date `-` date, producing time.
* Date `-` time, producing date.
* Time `-` time, producing time.

The result tick count is the difference of the tick counts of the operands.

#### Chrono Multiplication

The supported chrono forms of multiplication are:
* Time `*` `I8`, and `I8` `*` time, producing time.
* Time `*` `R8`, and `R8` `*` time, producing time.

The result tick count is the product of the tick count of the time operand, as an `I8` value,
and the numeric value of the numeric operand. When the result is `R8`, it is cast to `I8` as if the
[`CastI8`](05-Functions.md#numeric-castx-functions) function were applied.

#### Chrono Division and Modulus

The supported chrono forms of division and modulus are:
* Time `div` `I8`, producing time.
* Time `div` time, producing `I8`.
* Time `mod` `I8`, producing time.
* Time `/` `R8`, producing time.
* Time `/` time, producing `R8`.

The result or result tick count is the operator applied to the tick counts or numeric value of the operands.
For the time `/` `R8` form, the result tick count is the `R8` quotient cast to `I8` as if the
[`CastI8`](05-Functions.md#numeric-castx-functions) function were applied.

#### Chrono Negation

The chrono negation operator negates a time value. Just as with [numeric negation](#negation-and-posation),
the negation of the smallest time value overflows back to this smallest time value.

## Dot Operator

The **_dot_** operator has the form `x.N`, where `x` is an expression and `N` is a **_simple name_** (an
[**_identifier_**](/docs/Grammars.md#productions-for-identifier)). When the left operand is of record
type, the name must be a field name of that record type and the result is the value of that field.

The dot operator may also be used to evaluate a **_property_** of certain types of values. For example,
when `x` is of type text, `x.Len` is shorthand for `Text.Len(x)`. Similarly, when `x` is of type date,
`x.Year` is shorthand for `Date.Year(x)`. Recall that we say that the function `Text.Len` is in the
`Text` namespace. When there is a namespace `NS` associated with the type of `x` and there is a property
function `NS.P` that accepts a single operand of that type, then `x.P` is shorthand for `NS.P(x)`.

The dot operator extends to optional, sequence, and tensor. In particular, if `MyTable` is a table type (sequence
of record) with a column named `C`, then `MyTable.C` is the sequence of values in that column. For example, for
a table `Employees` having a column named `Age`, the expression `Employees.Age` is the sequence of ages, in the
same order as the records in `Employees`. More explicitly, as described in the
[Extending to Sequence](03-ExtendedOperatorsAndFunctions.md#extending-to-sequence) section,
`Employess.Age` is shorthand for
```
ForEach(Employees, Age)
```

## Indexing and Slicing

The **_indexable types_** include the **_text_** type, **_tuple_** types and **_tensor_** types.

Recall that a tensor type has a fixed number of dimensions, called its **_rank_**, and a fixed **_item type_**,
while a tuple type has a fixed number of **_slots_**, each having a fixed type. For the purposes of indexing and
slicing, a text value or tuple behaves much like a rank-one tensor (a tensor with one dimension).

An indexing or slicing operation applied to an indexable source value, `src`, is written
```
src[spec_1, spec_2, ..., spec_n]
```
where each `spec_k` value is either an **_index specification_** or a **_range specification_**.

When `src` is a text value or tuple, there must be exactly one specification. When `src` is a tensor, the number 
of specifications must be no more than the **_rank_** of the tensor.

An **_index specification_** is a value of type `I8` (the default integer type) optionally prefixed with
**_index modifiers_**. The valid index modifiers are `^`, `%`, and `&`. See the [indexing](#indexing) section
for a more comprehensive  explanation.

A **_range specification_** consists of an optional **_start specification_** followed by a colon and an
optional **_stop specification_**, all optionally followed by another colon and an optional **_step value_**
of type `I8`. A **_start specification_** is a value of type `I8` optionally preceded by the `^` index modifier.
A **_stop specification_** is a value of type `I8` optionally preceded by the `^` index modifier and/or the
`*` count modifier. The **_count modifier_** specifies that the stop value should be interpreted as a count
of items rather than a stop index. When both the `^` and `*` modifiers are present, the value is subtracted
from the maximum count of items available for that dimension (which depends on the start and step values as
well as the dimension size). See the [slicing](#slicing) section for a more comprehensive explanation.

Indexing and slicing **_extend to sequence_** in both the source operand and specification operands. Indexing and 
slicing also **_extend to optional_** in the source operand (except of type text) and in any index specification 
operands. In contrast, for a slice component, a `null` value is considered the same as if the component is omitted.

When indexing a `null` text value, the result is zero (of type `U2`), as if the text value were empty.
When slicing a `null` text value, the result is the `null` text value.

### Indexing

Indexing a source value produces an **_item_** from that source value. Indexing a source **_text_** value produces a
value of type `U2` (two-byte unsigned integer), namely the Unicode code point of the character at the indicated
position. Indexing a tuple produces a value from a **_slot_** of the tuple. Indexing a tensor produces a value from a
**_cell_** of the tensor.

Indices in Rexl are zero-based, meaning that the first item corresponds to index `0`. When `src` is a text value, a
tuple, or a rank-one tensor, `src[0]` results in the first item, `src[1]` results in the next item, and so on, up to
one less than the size of the source. For example,
```
"ABCDEF"[2]
```
results in the `U2` value `67`, which is the Unicode code point for the character `C`.

As previously explained, an **_index specification_** is a value of type `I8` optionally prefixed with **_index modifiers_**.
When indexing a **_text_** value or **_tuple_**, there must be a single index specification. When indexing a **_tensor_**, there
must be an index specification for each dimension, that is, the number of index specifications should be the
**_rank_** of the tensor. In particular, when `src` is a rank-zero tensor, it contains exactly one value, which is
accessed using `src[]`.

When `src` is a rank-two tensor (also known as a matrix) with shape `(3, 5)`, visualizing this matrix in the
standard way (top to bottom and left to right), `src[0, 0]` is the top left cell, `src[0, 4]` is the top right cell,
`src[2, 0]` is the bottom left cell, and `src[2, 4]` is the bottom right cell.

A tuple whose slot types are all the same is called **_homogeneous_**. For example, `("apple", "bug", "cat")` is 
homogeneous with item type **_text_**, while `(3.5, "apple", "bug", true, "cat")` is not homogeneous. For a
non-homogeneous tuple, the value in the index specification must be an integer literal and the resulting index
(after modifiers are applied) must be **_in bounds_**, meaning that it must be at least zero and less than the arity
of the tuple. The result type of the index operation is the type of the selected slot of the tuple.

For a **_homogeneous_** indexable type (text, tensor, or homogeneous tuple), the value in an index specification
may be a more general expression than an integer literal, and the resulting index need not be in bounds. When
the resulting index is not in bounds, the result is the **_default value_** of the item type. For example,
if `src` is homogeneous and rank-one (text, homogeneous tuple, or rank-one tensor),
```
ForEach(i:Range(5), src[i - 1])
```
results in a sequence containing the default value of the item type, followed by the first `4` items in `src`.
If `src` has less than `4` items, the remaining sequence items are the default value of the item type of the
source. Since indexing extends to sequence, this invocation of `ForEach` may be abbreviated to
```
src[Range(5) - 1]
```
When `src` is the text value `"ABC"`,
```
src[Range(5) - 1]
```
results in the sequence
```
[0u2, 65u2, 66u2, 67u2, 0u2]
```
Similarly, when `src` is the homogeneous tuple `("apple", "banana", "cat")`,
```
src[Range(5) - 1]
```
results in the sequence
```
[null, "apple", "banana", "cat", null]
```

The `^` modifier indicates that the index value should be subtracted from the dimension size. That is, the value is an
offset from the end of the dimension. For example, if `src` is rank-one with size `10`,
* `src[^1]` is equivalent to `src[9]`.
* `src[^2]` is equivalent to `src[8]`.
* `src[^3]` is equivalent to `src[7]`.
* `src[^12]` is equivalent to `src[-2]`. This is an error if `src` is a non-homogeneous tuple and the default 
  value of the item type otherwise.

The `^` modifier is particularly useful when the item is near the end of the dimension and the size of the 
dimension is not readily available. For example, to get the last item in a rank-one source, one may use `src[^1]`
regardless of the dimension size. When `src` is a text value, `src[^1]` is equivalent to `src[src.Len – 1]`, and 
when `src` is a rank-one tensor, `src[^1]` is equivalent to `src[src.Shape[0] - 1]`.

If `src` is homogeneous of rank-one, with size at least `k`, the expressions
```
src[Range(k)]

src[^ 1 + Range(k)]
```
produce the sequence of first `k` items (in order) and sequence of last `k` items (in reverse order), respectively.

The `%` and `&` modifiers indicate that when the index value is out of bounds (less than zero or greater than or
equal to the dimension size), the value should be replaced with an appropriate in bounds value.

The `%` modifier reduces the index value modulo the dimension size. For example, if `src` is rank-one tensor with 
size `10`,
* `src[%10]` is equivalent to `src[0]`.
* `src[%12]` is equivalent to `src[2]`.
* `src[%-1]` is equivalent to `src[9]`.
* `src[%-2]` is equivalent to `src[8]`.

The `&` index modifier **_clamps_** the index value by the size of the source, forcing the resulting index to be
**_in bounds_**. That is, `&` replaces negative values with `0` and replaces values greater than or equal to the dimension
size with one less than the dimension size. For example, if `src` is rank-one with size `10`,
* `src[&10]` is equivalent to `src[9]`.
* `src[&12]` is equivalent to `src[9]`.
* `src[&-1]` is equivalent to `src[0]`.
* `src[&-2]` is equivalent to `src[0]`.

The `^` modifier may be used in conjunction with the `%` or `&` modifier. Conceptually, the `^` modifier is applied
first, so the given index value is subtracted from the size of the source dimension and then that value is
modified according to the `%` or `&` modifier. For example, if `src` is rank-one with size `10`,
* `src[%^12]` is equivalent to `src[%-2]` which is equivalent to `src[8]`.
* `src[&^12]` is equivalent to `src[&-2]` which is equivalent to `src[0]`.
* `src[%^-2]` is equivalent to `src[%12]` which is equivalent to `src[2]`.
* `src[&^-2]` is equivalent to `src[&12]` which is equivalent to `src[9]`.

Note that `src[%^k]` is generally equivalent to `src[%-k]`. That is, using both the `%` and `^` modifiers is the same
as negating the index and just using the `%` modifier.

As an alternative to tuple indexing, the first ten values in a tuple may be accessed using the `Tuple.Item<k>`
functions defined in [Tuple Functions](05-Functions.md#tuple-functions). For example, when `src` is a tuple with
arity at least three, the following are equivalent:
```
x[2]

x.Item2

x->Item2()

Tuple.Item2(x)
```

### Slicing

Slicing a text value, tuple, or tensor produces another text value, tuple, or tensor, respectively. Recall that an
indexing or slicing operation applied to an indexable `src` is written
```
src[spec_1, spec_2, ..., spec_n]
```
where each `spec_k` value is either an **_index specification_** or a **_range specification_**.

Recall that a **_range specification_** consists of an optional **_start specification_** followed by a colon and an optional
**_stop specification_**, all optionally followed by another colon and an optional **_step value_** of type `I8`.
A **_start specification_** is a value of type `I8` optionally preceded by the `^` index modifier. A **_stop specification_**
is a value of type `I8` optionally preceded by the `^` index modifier and/or the `*` count modifier.

When `src` is a text value or tuple, there must be exactly one specification and the result is a slice when that
specification is a range specification rather than an index specification.

For a tensor, the number of specifications must not exceed the rank of the tensor and the result is a slice when
the number of **_index specifications_** (which may be less than the total number of specifications) is strictly
less than the rank of the tensor. The rank of the result is the rank of the source minus the number of index
specifications. For example, if `src` is a rank-two tensor (matrix) with shape `(3, 5)`, then `src[1]` is a
rank-one tensor with shape `(5,)`, and is equivalent to `src[1, 0:5]` and `src[1, :]`. Viewing `src` as a matrix
in typical top to bottom and left to right order, `src[1, :]` contains the values from the middle row. Similarly,
`src[:, 2]` is equivalent to `src[0:3, 2]` and is the rank-one tensor with shape `(3,)` containing values from
the middle column.

When slicing a tuple, each value in a range must be an integer literal, even when the tuple is homogeneous.
When slicing a text value or tensor, a value in a range may be a more general expression than an integer literal.
Conceptually, a range defines a sequence of indices that are **_in bounds_** for the dimension. When the dimension
size is zero, the sequence is necessarily empty (zero items), so need not be considered further. When the
sequence consists of at least two indices, the difference of consecutive indices is uniform and is known as the
**_step value_** for the range.

The following algorithm illustrates how the sequence of indices is determined from a range specification and 
dimension size. For this discussion:
* Let `size` indicate the size of the dimension.
* Let `start` be the **_start index_**, with value `null` if not yet determined.
* Let `stop` be the **_stop index_**, with value `null` if not yet determined.
* Let `step` be the step value.
* Let `max` be the **_max count_** of indices available.

Determine `start` from the **_start specification_**:
* If no **_start specification_** is provided, set `start` to `null`.
* Otherwise, let `val` be the value given in the start specification and:
  * If the `^` modifier is not present, set `start` to `val`.
  * Otherwise, set `start` to `size - val`.

Determine `stop` from the **_stop specification_**:
* If no **_stop specification_** is provided or the stop specification includes the count modifier `*`,
  set `stop` to `null`.
* Otherwise, let `val` be the value given in the stop specification and:
    * If the `^` modifier is not present, set `stop` to `val`.
    * Otherwise, set `stop` to `size - val`.

Determine `step`:
* If a step value is provided and that value is not zero or `null`, set `step` to that value.
* Otherwise, if neither `start` nor `stop` is `null` and `start > stop`, set `step` to `-1`.
* Otherwise, set `step` to `+1`.

Note that `step` is either positive or negative, not zero.

Adjust the `start` and `stop` values, if needed, and compute `max`:
* When `step > 0`:
  * If `start` is `null` or `start < 0`, set `start` to `0`.
  * If `stop` is `null` or `stop > size`, set `stop` to `size`.
  * If `start >= stop`, set `max` to `0`.
  * Otherwise, set `max` to the smallest positive integer for which `start + max * step >= stop`.
* Otherwise, when `step < 0`:
  * If `start` is `null` or `start >= size`, set `start` to `size - 1`.
  * If `stop` is `null` or `stop < -1`, set `stop` to `-1`.
  * If `start <= stop`, set `max` to `0`.
  * Otherwise, set `max` to the smallest positive integer for which `start + max * step <= stop`.

If `max` is zero, the index sequence is empty. Otherwise, we know that `start + k * step` is in bounds for each 
integer `k` with `0 <= k < max`.

Finally, determine `count`, the `count` of indices in the sequence:
* If there is not a stop specification or it doesn't include the count modifier `*`, set `count` to `max`.
* Otherwise, let `val` be the value given in the stop specification and:
  * If the `^` modifier is not present, set `count` to `val`.
  * Otherwise, set `count` to `max - val`.
  * If `count < 0`, set `count` to `0`.
  * If `count > max`, set `count` to `max`.

If `count` is zero, the index sequence is empty. Otherwise, the sequence consists of the indices:
```
start, start + step, start + 2 * step, ..., start + (count - 1) * step
```
The result of the slice includes the items associated with the indices in this sequence.

The algorithm is best illustrated with some examples. For these, assume `src` is a rank-one tensor of `I8` with 
values:
```
0 1 2 3 4 5
```
Then
```
src[:4]

src[4:]
```
result in
```
0 1 2 3

4 5
```
That is, these are the sub-tensors consisting of the first `4` items and the remaining items, respectively. If the
size of `src` were less than `4`, then the former would be the same as `src` and the latter would be empty.

Similarly,
```
src[:^4]

src[^4:]
```
result in
```
0 1

2 3 4 5
```
That is, these consist of all but the last `4` items and the last `4` items, respectively. If the size of src were less
than `4`, the former would be empty and the latter would be the same as `src`.

When the start value is greater than the stop value, the default step is `-1` instead of `+1`. For example,
```
src[1:4]

src[4:1]
```
are equivalent to
```
src[1:4:1]

src[4:1:-1]
```
and result in
```
1 2 3

4 3 2
```
To emphasize, an unspecified step is _not_ necessarily the same as step value of one. These two expressions
produce quite different results:
```
src[Range(6):3]

src[Range(6):3:1]
```
The former produces a sequence of rank-one tensors with values
```
0 1 2
1 2
2

4
5 4
```
where the blank line represents a rank-one tensor of size zero. The latter expression produces empty tensors
for the last three items since if the step is positive and `start >= stop`, the result is empty. Note that none of
these include the value at index `3`, namely `3`, since index `3` is the stop index and the stop index is never
included in the range.

In general, a step value of `-1` reverses items, so
```
src[::-1]
```
reverses the entire dimension producing
```
5 4 3 2 1 0
```

The **_count modifier_** `*` indicates that the stop specification value is directly affecting the count of
items rather than affecting the stop index value. When the start index is zero and the step is one, the count
and stop index are the same. That is, src[:*4]` is equivalent to `src[:4]`. However, when the start index is
not zero or the step is not one, specifying a count is quite different than specifying a stop index. For example,
with the same rank-one tensor as above, with item type text and values
```
0 1 2 3 4 5
```
the slices
```
src[1:3]

src[1:*3]
```
result in
```
1 2

1 2 3
```
Similarly, the slices
```
src[:3:2]

src[:*3:2]
```
result in
```
0 2

0 2 4
```
The count modifier `*` together with the `^` modifier reduces the count of items in the slice. For example:
```
src[::2]

src[:^1:2]

src[:^*1:2]
```
result in
```
0 2 4

0 2 4

0 2
```
The second expression reduces the stop index from its default `6` to `5`, but this has no effect on the size of the
resulting slice (since the step is two). The third expression directly reduces the count from its max `3` to `2`.

For tensors with rank larger than one, slicing and indexing are applied in each dimension independently. When
an index is provided, that dimension is _erased_ from the result. When a range is provided, the dimension is
kept, but is possibly smaller. For example, if `src` is a rank-two tensor with shape `(2, 3)`, item type `I8`,
and values:
```
0 1 2
3 4 5
```
the expression `src[:, 1]` results in a rank-one tensor with values `1 4`, the middle column. Similarly,
```
src[:, 1:]

src[::-1, :2]
```
result in rank-two tensors with values
```
1 2
4 5

3 4
0 1
```
In the former, the first column is dropped. In the latter, the rows are reversed and the last column is dropped.

When the right-most dimension should keep the full slice, it isn't necessary to specify a range for it. For
example, both `src[1, :]` and `src[1]` are equivalent and result in a rank-one tensor with shape `(3,)` and
values `3 4 5`, effectively extracting the second row.

In contrast, extracting a particular column must be written with two specifiers, for example, `src[:, 1]` results
in a rank-one tensor with shape `(2,)` and values `1 4`, effectively extracting the second column.

As noted, when an index is specified, that dimension is erased. To keep the dimension but set its size to one,
use the count modifier with count of one. For example,
```
src[1:*1]

src[:, 1:*1]
```
result in rank-two tensors, extracting the second row and second column, respectively. The resulting shapes
are `(1, 3)` and `(2, 1)` rather than producing rank-one tensors with shapes `(3,)` and `(2,)`.

When slicing a tensor, a range specifier may be specified via a tuple of values. The tuple may either contain 
three values indicating the start, stop, and step values (with `null` indicating that the value is "missing") or
contain five values indicating the start value, a logical value indicating whether the start value should be
subtracted from the dimension size, the stop value, a logical value indicating whether the stop value should be
subtracted from the dimension size, and the step value. For example, when `src` is a tensor of rank at least one,
the following are equivalent:
```
src[a:b:c]
src[(a, b, c)]
src[(a, false, b, false, c)]
With(r:(a, b, c), src[r])
```

These are also equivalent:
```
src[a:^b:c]
src[(a, false, b, true, c)]
With(r:(a, false, b, true, c), src[r])
```

Note that a tuple-encoded range cannot include **_count modifier_** functionality.

## Projection Operators

**_Projection operations_** generally take in one value and produce another. These operations accept a value on
the left, use either `->` or `+>` as an operator, and produce a value indicated on the right.

Operations that employ the `+>` syntax are called **_augmenting projections_**.

There are several distinct forms of projection.

### Function Projection

When the `->` operator is followed immediately by a function invocation, the left operand is used as the first
argument value for the function invocation. For example, `S->Count()` is equivalent to `Count(S)`.

This is a form of piping, akin to the pipe operator, with some significant differences. First, the operation on the
right must be a function invocation. Second, the precedence of this is much higher than the pipe operator. For
example,
```
3 + S->Count() | _ * 7
```
is equivalent to
```
(3 + Count(S)) * y
```
and _not_ to
```
Count(3 + S) * 7
```

Many functions support providing a name for the first argument. For example, when `X` is a sequence of a
numeric type, the two expressions
```
ForEach(x: X, x * 3)

ForEach(X as x, x * 3)
```
are equivalent. Both specify the name `x` to represent an item of the sequence `X`. When using function
projection, the latter form, using `as` is supported. That is, the following is equivalent to the above expressions:
```
X->ForEach(as x, x * 3)
```

Similar to the pipe operator, this form of projection is convenient when writing chains of operations, as in
```
Employees->TakeIf(Salary < 50000)->Sort(Years)->GroupBy(Years div 5)
```
In some Rexl hosts, formulas can use multiple lines, and this can be written:
```
Employees
  ->TakeIf(Salary < 50000)
  ->Sort(Years)
  ->GroupBy(Years div 5)
```
These are equivalent to each other, and both are equivalent to:
```
GroupBy(Sort(TakeIf(Employees, Salary < 50000), Years), Years div 5)
```
This full **_function syntax_** form requires reading **_inside out_**, with additional operations requiring editing on both
ends of the expression.

**_Note_**: This form of projection does not extend to optional, sequence, or tensor. However, the function itself 
_may_ extend. In all cases, function projection is equivalent to moving the left operand into the invocation as the
first argument.

When the left operand of `->` is of a type that has an associated namespace then the namespace may be
omitted from the function name. For example, if `x` is of a tensor type, with numeric item type, then
```
x->Tensor.Shape()

x->Tensor.Rank()

x->Tensor.ForEach(it * it)
```
may be abbreviated to
```
x->Shape()

x->Rank()

x->ForEach(it * it)
```
Similarly, if `name` is of text type and `names` is a sequence of text, then
```
name->Text.Len()

name->Text.Upper()

names->Text.Concat(", ")
```
may be abbreviated to
```
name->Len()

name->Upper()

names->Concat(", ")
```
As described in [Dot Operator](#dot-operator), some simple functions in namespaces may also be
used as **_properties_**. In particular, the examples above,
```
x->Tensor.Shape()

x->Tensor.Rank()

name->Text.Len()

name->Text.Upper()
```
may be abbreviated to
```
x.Shape

x.Rank

name.Len

name.Upper
```
respectively.

### Value Projection

Value projection is when the right operand of `->` is an expression enclosed in parentheses. For example,
```
3->(it * it)
```
produces the value `9`. Note that the value on the left can be referenced by the keyword `it`. This form of
projection extends to sequence on the left, so the expression
```
Range(4)->(it * it)
```
is shorthand for
```
ForEach(Range(4), it * it)
```
and produces the sequence
```
[ 0, 1, 4, 9 ]
```
When the left value is a record, the right expression can use fields of that record **_unqualified_**, that is, without
dotting from `it`. For example, the expression
```
{ A: 3, B: 5 }->(A * B)
```
produces the value `15`. This is equivalent to
```
{ A: 3, B: 5 }->(it.A * it.B)
```
As another example, if `Orders` is a table with numeric columns named `Amt` and `Price`, then
```
Orders->(Amt * Price)
```
is the sequence of gross proceeds from each order.

### Record Projection

Record projection is a shorthand form of value projection when the result is a record. For example, the value 
projection
```
3->({ A: it, B: it * it })
```
can be written without parentheses as
```
3->{ A: it, B: it * it }
```
to produce the record
```
{ A: 3, B: 9 }
```
Of course, this form extends to sequence, so the expression
```
Range(4)->{ A: it, B: it * it }
```
produces the table (sequence of records) equivalent to
```
[ { A: 0, B: 0 },
  { A: 1, B: 1 },
  { A: 2, B: 4 },
  { A: 3, B: 9 } ]
```
As with value projection, when the left value is itself a record, the right side can use fields of that record 
unqualified, that is, without dotting from `it`. For example, the expression
```
{ A: 3, B: 5 }->{ A, B, Sum: A + B, Prod: A * B, Pow: A^B }
```
produces a record equivalent to
```
{ A: 3, B: 5, Sum: 8, Prod: 15, Pow: 243 }
```
Note that we wrote just `A, B` as shorthand for `A: A, B: B`. Since the values were specified as identifiers, 
those identifiers were also used as field names.

Since the fields of the left record are included in the result, we can shorten this by using the
**_augmenting_** record projection operator to produce the same result:
```
{ A: 3, B: 5 }+>{ Sum: A + B, Prod: A * B, Pow: A^B }
```
If the left record contains fields that aren't wanted in the result, they can be dropped by using a `null` literal
on the right. That is,
```
{ A: 3, B: 5 }+>{ B: null, Sum: A + B, Prod: A * B, Pow: A^B }
```
produces
```
{ A: 3, Sum: 8, Prod: 15, Pow: 243 }
```
with the `B` field dropped.

To keep a field value but with a different name, declare a new field with the old field as value. For example, the
expressions
```
{ A: 3, B: 5 }->{ First: A, Sum: A + B }

{ A: 3, B: 5 }+>{ First: A, Sum: A + B }
```
produce the records
```
{ First: 3, Sum: 8 }

{ First: 3, B: 5, Sum: 8 }
```

Because the source field `A` was used as a field value, Rexl dropped `A` from the result of the augmenting operator.

Note that the declaration order of fields between the braces is not significant and has no effect on the value
produced. Fields of a record are not ordered. The user interface of the application hosting Rexl determines the
order in which fields are displayed.

### Tuple Projection

Like record projection, tuple projection is a shorthand form of value projection when the result is a tuple.
For example, the value projection
```
3->((it, it * it))
```
can be written without the outer parentheses as
```
3->(it, it * it)
```
to produce the tuple
```
(3, 9)
```
Of course, this form extends to sequence, so the expression
```
Range(4)->(it, it * it)
```
produces the sequence of tuples equivalent to
```
[ (0, 0), (1, 1), (2, 4), (3, 9) ]
```
When the left side is a tuple, the [tuple item properties](05-Functions.md#tuple-functions) `Item0`, `Item1`,
etc., may be used without dotting from `it`. For example, each of the following
```
(3, 5)->(it[0], it[1], it[0] + it[1], it[0] * it[1], it[0]^it[1])

(3, 5)->(Item0, Item1, Item0 + Item1, Item0 * Item1, Item0^Item1)
```
produces the tuple
```
(3, 5, 8, 15, 243)
```
Since the slots of the left tuple are included in the result, we can shorten this by using the **_augmenting_**
tuple projection operator to produce the same result:
```
(3, 5)+>(Item0 + Item1, Item0 * Item1, Item0^Item1)
```
When only a single value is to be appended, the trailing comma is not needed. That is, each of the following
```
(3, 5)+>(it[0]^it[1],)

(3, 5)+>(it[0]^it[1])

(3, 5)+>(Item0^Item1,)

(3, 5)+>(Item0^Item1)
```
produces the tuple
```
(3, 5, 243)
```

[_Back to Contents_](../RexlUserGuide.md)

[_Previous section - Extended Operators and Functions_](03-ExtendedOperatorsAndFunctions.md)

[_Next section - Functions_](05-Functions.md)
