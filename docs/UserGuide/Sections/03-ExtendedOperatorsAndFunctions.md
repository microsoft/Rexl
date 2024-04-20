# Extended Operators and Functions

* [Extending to Optional](#extending-to-optional)
* [Extending to Sequence](#extending-to-sequence)
* [Extending to Tensor](#extending-to-tensor)
* [Extending to Multiple](#extending-to-multiple)

Many operators and functions of Rexl are defined on relatively simple types and then **_extended_** in a standard 
way to apply to more complex types. There are three kinds of extending, namely **_optional_**, **_sequence_**, and 
**_tensor_**. These are detailed in the following sections.

## Extending to Optional

A **_required_** type is one that does not contain the special value `null`. Each such type has a corresponding 
**_optional_** type that includes `null`. Many operators and functions are defined for certain required types and 
automatically extend to the corresponding optional types. For example, the numeric addition operator is 
defined for numeric values. When applied to optional numeric types, the result is `null` if any operand is `null`
and is the normal numeric result otherwise. Similarly, the `Sqrt` function computes the square root of a 
numeric value. When applied to an optional numeric type, the result is `null` if the argument is `null` and is the 
normal numeric result otherwise.

More precisely, when `X` and `Y` are of optional numeric type, the expressions:
```
X + Y

Sqrt(X)
```
are shorthand for these invocations of the [`Guard`](05-Functions.md#with-and-guard) function:
```
Guard(x:X, y:Y, x + y)

Guard(x:X, Sqrt(x))
```
To summarize this behavior, we say that the addition operator and `Sqrt` function **_extend to optional_**.
In general, **_extension to optional_** results in automatic application of the
[`Guard`](05-Functions.md#with-and-guard) function.

Many Rexl operators and functions extend to optional.

Note that unnecessary use of optional types incurs additional computational cost, so data sources (such as 
imported SQL tables and parquet files) should be constructed to use required types when possible.

## Extending to Sequence

When `x` and `y` are numeric, `x + y` is the sum of the two operands, as expected. When `X` is a sequence of 
numeric values, `X + y` is also a numeric sequence with each item of the result being the sum of the 
corresponding item in `X` with `y`. More precisely, `X + y` is shorthand for this invocation of the
[`ForEach`](05-Functions.md#foreach-functions) function:
```
ForEach(x:X, x + y)
```
When `Y` is also a numeric sequence, `X + Y` is a numeric sequence with each item being the sum of the 
corresponding items of `X` and `Y`. More precisely, `X + Y` is shorthand for:
```
ForEach(x:X, y:Y, x + y)
```
Similarly, `Sqrt(X)` is shorthand for:
```
ForEach(x:X, Sqrt(x))
```
To summarize this behavior, we say that the addition operator and `Sqrt` function **_extend to sequence_**.
In general, **_extension to sequence_** results in automatic application of the
[`ForEach`](05-Functions.md#foreach-functions) function.

Extending to sequence applies to multiple levels of sequence, so if `XX` is a sequence of sequence of numeric 
values, then `XX + y` and `Sqrt(XX)` are shorthand for:
```
ForEach(X:XX, ForEach(x:X, x + y))

ForEach(X:XX, ForEach(x:X, Sqrt(x)))
```
Many Rexl operators and functions extend to sequence. A notable special case is the
[**_dot_**](04-Operators.md#dot-operator) operator. When `R` is a record with a field named `F`, the expression
`R.F` produces the value of that field in the record. When `T` is a table with a column `F`, the expression `T.F`
is shorthand for:
```
ForEach(r:T, r.F)
```
The type of `T.F` is a sequence of the type of the field/column `F`. For example, if `Employees` is a table (sequence 
of records) with column `Name` of type text and column `Age` of type `I8`, then `Employees.Name` is a 
sequence of text and `Employees.Age` is a sequence of `I8`. Then the expression
```
Employees.Age->Mean()
```
computes the mean (average) of the employee ages. Similarly,
```
Employees.Name->Distinct()
```
results in a sequence containing the distinct employee names, with no repeats.

## Extending to Tensor

Extending to tensor is similar to extending to sequence. When `X` and `Y` are tensors with numeric item type, the 
expressions:
```
X + Y

Sqrt(X)
```
are shorthand for these invocations of the [`Tensor.ForEach`](05-Functions.md#tensorforeach) function:
```
Tensor.ForEach(x:X, y:Y, x + y)

Tensor.ForEach(x:X, Sqrt(x))
```
In general, extension to tensor results in automatic application of the
[`Tensor.ForEach`](05-Functions.md#tensorforeach) function.

Many Rexl operators and functions extend to tensor.

## Extending to Multiple

Many operators and functions support multiple kinds of extending. For example, the addition operator
supports all three kinds of extending, to optional, to sequence, and to tensor. Consequently
a single application of the addition operator may involve multiple kinds.

For example, if `XX` is a **_sequence_** of **_tensor_** with **_optional_** numeric item type, then
```
XX + 3
```
is shorthand for
```
ForEach(X:XX, Tensor.ForEach(qx:X, Guard(rx:qx, rx + 3)))
```
Similarly, if `YY` is a **_sequence_** of **_optional_** **_tensor_** of numeric type,
```
YY + 3
```
is shorthand for
```
ForEach(QY:YY, Guard(RY:QY, Tensor.ForEach(y:RY, y + 3)))
```
Furthermore, if `ZZ` is a **_sequence_** of **_optional_** **_tensor_** of **_optional_** numeric type,
```
ZZ + 3
```
is shorthand for
```
ForEach(QZ:ZZ, Guard(RZ:QZ, Tensor.ForEach(qz:RZ, Guard(rz:qz, rz + 3))))
```

In summary, automatic **_extending_** greatly simplifies authoring of Rexl expressions.

[_Back to Contents_](../RexlUserGuide.md)

[_Previous section - Types and Values_](02-TypesAndValues.md)

[_Next section - Operators_](04-Operators.md)
