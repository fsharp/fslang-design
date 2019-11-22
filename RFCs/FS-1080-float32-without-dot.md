# F# RFC FS-1080 - Float32 without dot

The design suggestion [Float32 literals without the numeric dot](https://github.com/fsharp/fslang-suggestions/issues/750) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion and some related design relaxations.

* [x] [Approved in principle](https://github.com/fsharp/fslang-suggestions/issues/750#issuecomment-507304042)
* [x] [Discussion](https://github.com/fsharp/fslang-design/issues/414)
* [x] [Implementation](https://github.com/dotnet/fsharp/pull/7839)


# Summary
[summary]: #summary

Currently, when declaring literals for `float32` (or `single`) dots are required. This is not consistent with F# `decimal` literals which do not require a dot. This RFC allows omission of the dot in such Float32 literals.

[Documentation on literals](https://docs.microsoft.com/en-us/dotnet/fsharp/language-reference/literals#literal-types)

## Code Examples

Currently this does not compile:

```fsharp
// FS1156: this is not a valid literal
let f = 750f
let g = 750F
let x = 42_000f
let y = 42_000F
```

After the change it will compile and will be equivalent to:

```fsharp
let f = 750.f
let g = 750.F
let x = 42_000f
let y = 42_000F
```

## Detailed Design

This is implemented by extending the lexer rules for Float32 literals that are suffixed with `f` or `F`. Literals for `double` (or `float`), that is, Float64 literals will continue to require a dot, as these literals can only be written without a suffix.

The production rules for `ieee32` in the [F# language specification](https://fsharp.org/specs/language-spec/) will change as follows:

Current (page 29):

```f#
token ieee32     =  
       | float [Ff]      For example, 3.0F or 3.0f 
       | xint 'lf'       For example, 0x00000000lf 
```

After this implementation:

```f#
token ieee32     =  
       | (float|int) [Ff]    For example, 3.1F or 3.1f , or 3f
       | xint 'lf'           For example, 0x00000000lf 
```


# Drawbacks
[drawbacks]: #drawbacks

None

# Alternatives
[alternatives]: #alternatives

The main alternative is "don't do this" and continue to require the dot. The following alternative ways of writing `float32` literals are available currently:

* use hexadecimal, octal or binary literals with the `lf` suffix: `0xBF77lf`
* include the `.` in the literal: `let x = 123.f`
* include the `E`, as with `let x = 123E0f`
* or convert using the function `float32` or `single`, but then it isn't a literal anymore.

# Compatibility
[compatibility]: #compatibility

This is a non-breaking change, as syntax that currently throws compile error FS1156 will now compile correctly.

# Unresolved questions
[unresolved]: #unresolved-questions

None
