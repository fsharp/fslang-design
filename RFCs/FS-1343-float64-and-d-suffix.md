# F# RFC FS-1343 - `float64` abbreviation and `d`/`D` literal suffix

This RFC is the focused successor to section **e** of [#800](https://github.com/fsharp/fslang-design/pull/800), as requested by its [design review](https://github.com/fsharp/fslang-design/pull/800#issuecomment-3852354596).

- [x] Approved in principle
- [ ] Implementation
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

# Summary

Add `float64` as a transparent abbreviation for `System.Double`, and add `d`/`D` suffixes for decimal-form `System.Double` literals.

```fsharp
let x: float64 = 1d
let y = 1.25D

[<Measure>]
type s

let elapsed: float64<s> = 0.5d<s>
```

Unsuffixed floating-point literals keep their current type and behavior.

# Motivation

F# provides fixed-width names such as `int32`, `int64`, and `float32`, but no `float64`. It also provides explicit `f`/`F` and `m`/`M` suffixes for `float32` and `decimal`, but no suffix that explicitly selects binary64. The additions make generated code and representation-oriented APIs more regular without changing inference.

# Detailed design

## Type abbreviation

FSharp.Core adds:

```fsharp
type float64 = System.Double

[<MeasureAnnotatedAbbreviation>]
type float64<[<Measure>] 'Measure> = float<'Measure>
```

These are transparent abbreviations. They add no type identity, conversion, operator, equality rule, or runtime representation. Existing tooling may continue to display `float` as the canonical inferred name.

## Literal suffix

The lexical grammar adds:

```fsgrammar
token ieee64-suffixed =
    | (float | int) [Dd]
```

The token has type `System.Double` and reuses the parsing, rounding, overflow, constant, pattern, quotation, code-generation, and units-of-measure rules for existing `ieee64` literals.

```fsharp
0d
1.5D
6.022e23d
12d<kg>
```

The existing hexadecimal bit-pattern form remains `xint 'LF'`. Since `d` and `D` are hexadecimal digits, `0x1d` and `0x1D` remain integer literals with value 29; this RFC adds no `0x...d` form.

# Changes to the F# spec

- **Lexical analysis / numeric literals:** add `ieee64-suffixed` as above and give it the same semantics as `ieee64`.
- **Simple constant expressions:** include the new token wherever an existing `float` literal is accepted.
- **FSharp.Core basic types:** list `float64` with `float` and `double` as abbreviations for `System.Double`, including the measured form.

# Drawbacks

F# gains a third source name for `System.Double`, so projects may choose different style conventions. The suffix is also redundant for floating-shaped literals because `1.0` already defaults to `float`; its value is explicitness and regularity.

# Alternatives

- Add only `float64`: fixes type-name symmetry but not literal symmetry.
- Add only `d`/`D`: fixes literal spelling but not the fixed-width type family.
- Use `f64`: introduces a new multi-character suffix convention instead of the familiar .NET `d`/`D`.
- Make `float64` the canonical displayed name: creates broad textual churn without semantic benefit.

# Prior art

C# and Java use `d`/`D` for binary64 literals. Rust uses the fixed-width name and suffix `f64`. F# already has transparent primitive aliases such as `int`/`int32`, `float32`/`single`, and `float`/`double`.

# Compatibility

Neither addition reinterprets an existing token or type. Older compilers reject source using the new spelling, while produced assemblies use the existing binary64 representation. Adding the automatically available name `float64` has the normal possibility of a source name collision; existing qualification and shadowing rules apply. The feature should initially be gated by the corresponding preview language version.

# Interop

No special interop handling is required.

# Pragmatics

## Diagnostics and tooling

Malformed or out-of-range `d`/`D` literals use the existing binary64 diagnostics. Syntax highlighting treats the suffix as part of the literal; completion includes `float64`; hover follows the tool's existing alias-display policy.

## Performance and scaling

The alias and suffix reuse existing binary64 typing and emission. They add no runtime work and no new inference or overload-resolution path.

## Culture-aware formatting/parsing

Literal parsing remains culture-invariant and continues to use `.` and `e`/`E`.

# Unresolved questions

None.
