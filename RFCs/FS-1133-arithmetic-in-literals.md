# F# RFC FS-1133 - Arithmetic in Literals

[The design suggestion](https://github.com/fsharp/fslang-suggestions/issues/539) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/539)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/14370)
- [ ] Design Review Meeting(s) with @dsyme and others invitees
- [ ] Discussion

# Summary

Allow the use of arithmetic operations on integers in literals as well as attributes. The operations shall be evaluated to constants at compile-time.

# Motivation

Currently, it is not easily possible to define literals whose values depend on other literals:

```fsharp
// proposed way of defining chains of literals
let [<Literal>] a = 1
let [<Literal>] b = a + 1
let [<Literal>] c = b + 1

// existing way of defining chains of literals
let [<Literal>] a = 1
let [<Literal>] b = 2
let [<Literal>] c = 3
```

The foremost drawback of the existing approach is lack of safety and poor ergonomics when defining and refactoring such chains of literals. Should we decide to change the value of `a`, we must not forget to manually adjust other literals that directly or transitively depend on `a`.

Another pain point is the inability to offload simple arithmetic to the compiler:

```fsharp
// proposed way of defining literal
let [<Literal>] bytesInMegabyte = 1024L * 1024L

// existing way of defining literal
let [<Literal>] bytesInMegabyte = 1_048_576L

// properties can also currently be used unless a literal is required
let bytesInMegabyte = 1024L * 1024L
```

# Detailed design

We allow the use of the following arithmetic operations on integers (`byte`, `sbyte`, `int16`, `uint16`, `int32`, `uint32`, `int64`, `uint64`) in literals and attributes:

- Addition
- Substraction
- Multiplication
- Division
- Modulo
- Bitwise AND
- Bitwise OR
- Bit shift left
- Bit shift right
- Unary plus

In addition to all of the operations above, we allow the use of the following arithmetic operations on signed integers (`sbyte`, `int16`, `int32`, `int64`) in literals and attributes:

- Unary minus (negation)

We allow the use of the following operations on booleans in literals and attributes:

- `not` (negation)
- Logical AND
- Logical OR

We allow the use of the following operations on `char` in literals and attributes:

- Addition
- Subtraction

We allow the use of the following operations on `float32` and `float` in literals and attributes:

- Addition
- Substraction
- Multiplication
- Division
- Modulo
- Unary plus
- Unary minus (negation)

An arbitrary number of operations should be supported, as long as each part of the expression comprises only the supported operators and operands:

```fsharp
let [<Literal>] someValue = 1

[<AttributeWithSomeEnum(enum ((someValue + 1 * 2) >>> 3))>]
do ()
```

Using an unsupported operation, operand of an unsupported type, or operand which is neither a literal nor a constant should produce the same compilation error as can be seen today:

```fsharp
let m = 2
let [<Literal>] x = 1 + m

let [<Literal>] y = 1 + System.DateTime.Now.Hour

let [<Literal>] f = 1. + 1.

// These should fail with:
// error FS0267: This is not a valid constant expression or custom attribute value
// error FS0837: This is not a valid constant expression
```

If the compile-time addition, subtraction or multiplication causes an arithmetic overflow, the compilation should fail:

```fsharp
let [<Literal>] x = System.Int32.MaxValue + 1

// error FS3177: This literal expression or attribute argument results in an arithmetic overflow.
```

# Drawbacks

None.

# Alternatives

What other designs have been considered?

None.

What is the impact of not doing this?

Missing out on some safety benefits.

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change?

  No.

* What happens when previous versions of the F# compiler encounter this design addition as source code?

  Code does not compile, just like today.

* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?

  Compiled binaries will contain evaluated constants, so business as usual.

* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?

  N/A.

# Unresolved questions

Allowing the same operations in enum definitions is a natural extension of the proposed changes, but can be worked on separately at a later point.
