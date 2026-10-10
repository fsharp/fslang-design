# F# RFC FS-1352 - Interpolated strings in constant expressions

The design suggestion [Support (a subset of) interpolated strings in Attribute parameters](https://github.com/fsharp/fslang-suggestions/issues/1347) has been marked "approved in principle". This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1347)
- [x] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/20753), [amendment](https://github.com/dotnet/fsharp/pull/20754)
- [x] [Discussion](https://github.com/fsharp/fslang-design/discussions/854)

# Summary

An interpolated string is a constant expression when each hole is a non-null constant string, integer, decimal, character or Boolean, with no alignment and no format specifier other than `%s`. The compiler evaluates it to the text that the same expression gives at run time. `[<Literal>]` values, attribute arguments and type provider static arguments can then use interpolation, as they already use `+`.

# Motivation

F# accepts `+` concatenation of string constants, and interpolated strings without holes, as constant expressions. It rejects the same text written with holes:

```fsharp
[<Literal>]
let Root = "api/v2"

[<Literal>]
let Items = Root + "/items"     // OK
[<Literal>]
let Items2 = $"{Root}/items"    // error FS0267: This is not a valid constant expression or custom attribute value

[<Obsolete($"Use {nameof getItemV2} instead")>]    // error FS0267
let getItem id = getItemV2 id
```

The `[<Obsolete>]` message is the use case of the suggestion. `+` is harder to read when the text has several holes.

# Detailed design

## Constant interpolated strings

An interpolated string is a *constant interpolated string* when each hole obeys all of these rules:

1. The hole has no alignment (`{e,8}`) and no format specifier (`{e:N2}`, `%d{e}`), except `%s` on a `string` hole.
2. The hole expression is a constant expression of type `string`, `decimal`, `char`, `bool` or an integer type: `sbyte`, `byte`, `int16`, `uint16`, `int32`, `uint32`, `int64` or `uint64`. A `string` value is not `null`.

The rules apply to all interpolated string forms: `$"..."`, `$@"..."`, `$"""..."""` and `$$"""..."""`. A constant interpolated string has type `string` and is a constant expression. So it can be an operand of `+` or a hole of another constant interpolated string. Interpolated strings without holes are constant expressions already and do not change.

## Value

The value is the concatenation of the text fragments and the hole texts, in source order. The text fragments use the run-time escape rules (`{{`, `}}` and `%%`, or their `$$` forms). A `string` hole contributes its value. Any other hole contributes the text that `string` returns for it, which does not depend on culture: the digits with a leading `-` if negative (a decimal keeps its scale, `1.50m` gives `1.50`), the character, or `True` or `False`. So the value is the one that the same expression gives at run time:

```fsharp
[<Literal>]
let Items = $"{Root}/items"          // "api/v2/items"

[<Literal>]
let ApiVersion = 2

[<Literal>]
let Versioned = $"api/v{ApiVersion}{'-'}{true}"   // "api/v2-True"

[<Literal>]
let ItemById = $"{Items}/{{id}}"     // "api/v2/items/{id}", a route template

[<Literal>]
let CountFormat = $"{Items}: %%d"    // "api/v2/items: %d", usable as a printf format
```

## Contexts

A constant interpolated string is valid where F# requires a constant expression:

- `[<Literal>]` values, in implementation and signature files.
- Attribute arguments, named arguments and array elements included.
- Type provider static arguments, after `const` or through a `[<Literal>]` value.

```fsharp
type Prices = CsvProvider<const ($"{__SOURCE_DIRECTORY__}/data/prices.csv")>
```

Outside these contexts, interpolated strings do not change. In particular, quotations keep their current shape.

## Rejected holes

```fsharp
[<Literal>]
let NoValue: string = null
let dir = "data"

[<Literal>] let A = $"{1.5}"                // rule 2: type float
[<Literal>] let B = $"{Root,10}"            // rule 1: alignment
[<Literal>] let C = $"%d{ApiVersion}"       // rule 1: format specifier
[<Literal>] let D = $"{dir}/x"              // rule 2: not a constant
[<Literal>] let E = $"{NoValue}/x"          // rule 2: null, as for NoValue + "/x" today
[<Literal>] let F = $"{DayOfWeek.Monday}"   // rule 2: enum type
```

# Changes to the F# spec

[Literal Definitions in Modules](https://github.com/fsharp/fslang-spec/blob/main/spec/namespaces-and-modules.md#literal-definitions-in-modules) defines *literal constant expression*. Add one alternative:

> — OR —
>
> - An interpolated string whose holes are literal constant expressions of type `string` (not `null`), `decimal`, `char`, `bool` or an integer type, with no alignment and no format specifier other than `%s` on a `string` hole. Its value is the concatenation of its text fragments and hole texts.

[Custom attribute](https://github.com/fsharp/fslang-spec/blob/main/spec/custom-attributes-and-reflection.md#custom-attributes) arguments and [provided type](https://github.com/fsharp/fslang-spec/blob/main/spec/provided-types.md) static parameters use this definition, so they do not change.

# Drawbacks

- The subset is narrower than general interpolation. Some holes, such as a `float` literal, still fail, and the reason is not obvious.
- There are two ways to write the same constant string.

# Alternatives

- Do nothing: `+` stays the only way.
- Attribute arguments only, as the suggestion title says. Rejected: attribute arguments, `[<Literal>]` values and static arguments share one definition of constant expression.
- Strict parity with C#: `string` holes only, no `%s`. Rejected in [discussion #854](https://github.com/fsharp/fslang-design/discussions/854): F# format specifiers already differ from C#, and F# formats integers, decimals, characters and Booleans with the invariant culture, so their text is known at compile time.
- `float` and `float32` holes. Rejected: their text depends on the runtime that runs the compiler (`-0.0` is `0` on .NET Framework and `-0` on .NET), so one source would give different constants.

# Prior art

C# 10 [constant interpolated strings](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/constant_interpolated_strings.md) ([champion issue](https://github.com/dotnet/csharplang/issues/2951)) accept constant `string` holes without alignment or format specifiers, and reject a `null` constant hole (CS0133). F# accepts these holes too, and also integer, `decimal`, `char`, `bool` and `%s` holes. C# cannot fold numeric holes: it formats them with the current culture.

C# also evaluates such strings at compile time outside constant contexts. This RFC does not require that, as F# does not require it for `+`.

# Compatibility

- Not a breaking change: each new form gave FS0267 before.
- An older compiler reports FS0267 for the new forms. A newer compiler with an older `--langversion` reports that the feature needs a newer language version.
- Binaries contain only the evaluated string: a literal field, an attribute blob, or a `Const.String` in F# metadata. Older compilers read them as before. The metadata format does not change.
- No FSharp.Core change.

# Interop

Other .NET languages see an ordinary `const string` field or attribute value.

# Pragmatics

## Diagnostics

Report the error on the first hole that breaks a rule, not on the whole string, and state the rules. For example: *"This interpolated string is a constant, so each hole must be a non-null constant string, integer, decimal, character or Boolean, with no alignment and no format specifier other than '%s' for a string."*

## Tooling

- The parse tree does not change. Holes are type-checked as today, so navigation, find references, rename and colorization in holes do not change.
- `FSharpMemberOrFunctionOrValue.LiteralValue` and tooltips show the evaluated string.
- In quotations, a use of such a literal is a `Value` node, as for other literals.
- Debugging: no change, because the value is a constant.

## Performance

Evaluation is linear in the number of fragments and holes. Existing code is not affected. An implementation can also fold constant interpolated strings outside constant contexts, as an optimization.

## Scaling

- Holes in one string: about 10 in hand-written code; the compiler accepts any number, in linear time.
- Interpolated strings nested in holes: about 2 in hand-written code; the existing expression-depth limits apply.

## Culture-aware formatting/parsing

None. Integer, `decimal`, `char` and `bool` holes use culture-invariant text, which is the same on every .NET runtime. `float` holes are excluded because theirs is not.

# Unresolved questions

None.
