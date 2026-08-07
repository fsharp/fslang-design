# F# RFC FS-1142 - Extended numeric literal

The design suggestion [Underscores in numeric literals after prefix and before suffix](https://github.com/fsharp/fslang-suggestions/issues/718), [Extend custom numeric types to support floating point literals](https://github.com/fsharp/fslang-suggestions/issues/445) and [Hexadecimal, octal and binary custom numeric literals](https://github.com/fsharp/fslang-suggestions/issues/754) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] Suggestion: [Underscores in numeric literals after prefix and before suffix](https://github.com/fsharp/fslang-suggestions/issues/718), [Extend custom numeric types to support floating point literals](https://github.com/fsharp/fslang-suggestions/issues/445) and [Hexadecimal, octal and binary custom numeric literals](https://github.com/fsharp/fslang-suggestions/issues/754)
- [x] Approved in principle
- [ ] [Implementation (in progress)](https://github.com/dotnet/fsharp/pull/17242)
- [ ] Design Review Meeting(s) with @dsyme and others invitees
- [Discussion](https://github.com/fsharp/fslang-design/discussions/769)

# Summary

This RFC will allow the following things:

- Underscores in numeric literals after prefix and before suffix like `0x_1` or `1_u` or mixed them up like `0x_1_u`.
- Hexadecimal, octal, binary and floating point custom numeric literals like `0x1I` or `1.0G`

# Motivation

Make the language more consistent and easier to read. Enhance the custom numeric literals feature by supporting not only integers but also floats.

# Detailed design

1. Underscores can now be placed after prefix (`0x`, `0o`, `0b`) and before suffix (eg. `UL`, `y`, `m`, `f`). These used to be [forbidden](https://github.com/fsharp/fslang-design/blob/main/FSharp-4.1/FS-1005-underscores-in-numeric-literals.md#detailed-design) but would be allowed after this RFC is implemented.

### Examples

    ```fsharp
    let pi1 = 3_.1415F      // Invalid cannot put underscores adjacent to a decimal point
    let pi2 = 3._1415F      // Invalid cannot put underscores adjacent to a decimal point
    let socialSecurityNumber1 = 999_99_9999_L         // OK

    let x1 = _52              // This is an identifier, not a numeric literal
    let x2 = 5_2              // OK (integer literal)
    let x3 = 52_              // Invalid cannot put underscores at the end of a literal
    let x4 = 5_______2        // OK (integer literal)

    let x5 = 0_x52            // Invalid cannot put underscores in the 0x radix prefix
    let x6 = 0x_52            // OK
    let x7 = 0x5_2            // OK
    let x8 = 0x52_            // Invalid cannot put underscores at the end of a number

    // Leading zeroes may have underscores
    let x9 = 0_52             // OK (integer literal)
    let x10 = 0000_005_2      // OK (integer literal)
    let x11 = 052_            // Invalid cannot put underscores at the end of a numeric literal

    // Octal literals
    let x12 = 0_o52            // Invalid cannot put underscores in the 0o radix prefix
    let x13 = 0o_52            // OK (octal literal)
    let x14 = 0o5_2            // OK (octal literal)
    let x15 = 0o52_            // Invalid cannot put underscores at the end of a numeric literal
    ```

2. Allow number prefix (`0x`, `0o`, `0b`) before integer custom numeric integer literals. This follows the same syntactic rules as current integer literals.
  
    ```fsharp
    let x1 = 0x123I        // big int (291)
    let x2 = 0o123I        // big int (83)
    let x3 = 0b1010I       // big int (10)
    ```

    The number prefix **will not be removed** from the string sended to the `FromString` of the numeric literal module. This may be a break change.

    ```fsharp
    module NumericLiteralG =
      let FromString s = s

    let x4 = 0x999999999999999999G    // FromString will receive the string "0x999999999999999999"
    ```

3. Allow floating point custom numeric literals. 

    This will require the numeric literal module contains two new functions:

    ```fsharp
    val FromFloat: float -> 'CustomNumber
    val FromFloatString: string -> 'CustomNumber
    ```

    According to [this comment](https://github.com/fsharp/fslang-suggestions/issues/445#issuecomment-596902041),

    - When the literal has <= 15 significant figures[^1] and its exponent is within -300 to 300, the compiler will call `FromFloat` with the parsed `float` number
    - Or the compiler will call `FromFloatString` with the original string

    [^1]: The first none zero figure to last none zero figure, without the dot. Can be match by the Regex:

        ```regex
        ^-?0*(?<number>\d+\.?\d*?)0*(?:$|[eE][+-]?(?<exp>\d+))
        ```

    ```fsharp
    module NumericLiteralG =
      let FromFloat (s: float) = s
      let FromFloatString (s: string) = s

    let x1 = 123.456e-10G    // x1: float = 1.23456e-08
    let x2 = 123.456789123456789123e-10G    // x2: string = "123.456789123456789123e-10"
    ```

# Drawbacks

- Custom numeric literals module might harder to write.

- May introduce break change.

# Alternatives

- For number prefix (`0x`, `0o`, `0b`) before integer custom numeric literals, we might introduce a new `FromIntegerString` to avoid the break change.
- Or firstly parse it to `bigint` then `ToString` to obtain a literal without prefix.

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change?
Maybe

* What happens when previous versions of the F# compiler encounter this design addition as source code?
Can write numeric literal module with new functions in the source code, but cannot use these new numeric literal grammar.

* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
Cannot use the new numeric literal grammar.

# Pragmatics

## Tooling

Please list the reasonable expectations for tooling for this feature, including any of these:

* Colorization

  Might need to change the color schema of the numeric literals.

# Unresolved questions

- Should we introduce a new `FromIntegerString` or use any way to remove number prefix from custom integer literal string passed to `FromString`?
