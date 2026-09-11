# F# RFC FS-1352 - Interpolated strings in constant expressions

The design suggestion [Support (a subset of) interpolated strings in Attribute parameters](https://github.com/fsharp/fslang-suggestions/issues/1347) is **not** marked "approved in principle". This RFC gives a concrete design for that decision.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1347)
- [ ] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

# Summary

An interpolated string is a constant expression when each hole inserts a non-null constant `string` unchanged. The compiler evaluates it to the text that the same expression gives at run time. `[<Literal>]` values, attribute arguments and type provider static arguments can then use interpolation, as they already use `+`.

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

1. The hole has no .NET alignment (`{e,8}`) and no .NET format (`{e:N2}`).
2. The hole has no printf format, or only a plain `%s` (no flags, width or precision).
3. The hole expression is a constant expression of type `string`, and its value is not `null`.

The rules apply to all interpolated string forms: `$"..."`, `$@"..."`, `$"""..."""` and `$$"""..."""`. A constant interpolated string has type `string` and is a constant expression. So it can be an operand of `+` or a hole of another constant interpolated string. Interpolated strings without holes are constant expressions already and do not change.

## Value

The value is the concatenation of the text fragments and the hole values, in source order. The text fragments use the run-time escape rules (`{{`, `}}` and `%%`, or their `$$` forms). So the value is the one that the same expression gives at run time:

```fsharp
[<Literal>]
let Items = $"{Root}/items"          // "api/v2/items"

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

Outside these contexts, interpolated strings do not change.

## Rejected holes

```fsharp
[<Literal>]
let Version = 2
[<Literal>]
let NoValue: string = null
let dir = "data"

[<Literal>] let A = $"v{Version}"     // rule 3: type int
[<Literal>] let B = $"{Root,10}"      // rule 1: alignment
[<Literal>] let C = $"%-8s{Root}"     // rule 2: flags and width
[<Literal>] let D = $"{dir}/x"        // rule 3: not a constant
[<Literal>] let E = $"{NoValue}/x"    // rule 3: null, as for NoValue + "/x" today
```

# Changes to the F# spec

[Literal Definitions in Modules](https://github.com/fsharp/fslang-spec/blob/main/spec/namespaces-and-modules.md#literal-definitions-in-modules) defines *literal constant expression*. Add one alternative:

> — OR —
>
> - An interpolated string whose holes are non-null literal constant expressions of type `string`, without alignment, without .NET format, and without a printf format other than a plain `%s`. Its value is the concatenation of its text fragments and hole values.

[Custom attribute](https://github.com/fsharp/fslang-spec/blob/main/spec/custom-attributes-and-reflection.md#custom-attributes) arguments and [provided type](https://github.com/fsharp/fslang-spec/blob/main/spec/provided-types.md) static parameters use this definition, so they do not change.

# Drawbacks

- The subset is narrower than general interpolation. Some holes, such as an `int` literal, still fail, and the reason is not obvious.
- There are two ways to write the same constant string.

# Alternatives

- Do nothing: `+` stays the only way.
- Attribute arguments only, as the suggestion title says. Rejected: attribute arguments, `[<Literal>]` values and static arguments share one definition of constant expression.
- More hole types: see [Unresolved questions](#unresolved-questions).

# Prior art

C# 10 [constant interpolated strings](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/constant_interpolated_strings.md) ([champion issue](https://github.com/dotnet/csharplang/issues/2951)) use the same subset: each hole is a constant `string` expression without alignment or format. C# also rejects a `null` constant hole (CS0133). Rule 2 is F#-only, because C# has no printf formats.

In C#, holes of other types format with the current culture, so their text is not known at compile time. F# formats holes with the invariant culture (`$"{1.5}"` gives `1.5` under `de-DE`), so in F# the restriction is a scope choice.

# Compatibility

- Not a breaking change: each new form gave FS0267 before.
- An older compiler reports FS0267 for the new forms. A newer compiler with an older `--langversion` reports that the feature needs a newer language version.
- Binaries contain only the evaluated string: a literal field, an attribute blob, or a `Const.String` in F# metadata. Older compilers read them as before. The metadata format does not change.
- No FSharp.Core change.

# Interop

Other .NET languages see an ordinary `const string` field or attribute value.

# Pragmatics

## Diagnostics

Report the error on the first hole that breaks a rule, not on the whole string, and state the rules. For example: *"This interpolated string must be a constant, so each hole must be a non-null constant expression of type 'string' without alignment or format specifiers."*

## Tooling

- The parse tree does not change. Holes are type-checked as today, so navigation, find references, rename and colorization in holes do not change.
- `FSharpMemberOrFunctionOrValue.LiteralValue` and tooltips show the evaluated string.
- In quotations, a use of such a literal is a `Value` node, as for other literals.
- Debugging: no change, because the value is a constant.

## Performance

Evaluation is linear in the number of fragments and holes. Existing code is not affected. The RFC does not require folding constant interpolated strings outside constant contexts; an implementation can do it as an optimization.

## Scaling

- Holes in one string: about 10 in hand-written code; the compiler accepts any number, in linear time.
- Interpolated strings nested in holes: about 2 in hand-written code; the existing expression-depth limits apply.

## Culture-aware formatting/parsing

None. Holes insert strings unchanged, so the value does not depend on culture or on the .NET version.

# Unresolved questions

1. Allow holes of integral types, `char` and `bool` without formatting, for example `$"api/v{Version}"` with `[<Literal>] let Version = 2`? Their F# text does not depend on culture (see [Prior art](#prior-art)), so the value is known at compile time. This RFC excludes them for C# parity and to keep one text rule. `float` and `float32` stay excluded in any case: their text depends on the .NET version (shortest round-trip formatting since .NET Core 3.0). Printf formats add their own rules: `$"%b{true}"` gives `true`, but `$"{true}"` gives `True`.
