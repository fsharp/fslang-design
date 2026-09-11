# F# RFC FS-1353 - Parameterless struct constructors and struct initializers

This RFC covers the suggestions [#362](https://github.com/fsharp/fslang-suggestions/issues/362) and [#1056](https://github.com/fsharp/fslang-suggestions/issues/1056), both marked "approved in principle".

- [x] [Suggestion #362](https://github.com/fsharp/fslang-suggestions/issues/362)
- [x] [Suggestion #1056](https://github.com/fsharp/fslang-suggestions/issues/1056)
- [x] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

**C# reference:** [Parameterless struct constructors (C# 10)](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/parameterless-struct-constructors.md), [Auto-default structs (C# 11)](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/auto-default-structs.md)

# Summary

Struct types can declare a public parameterless constructor, as `new() = ...` or `type S() = ...`. Primary-constructor structs can contain instance `let`, `do` and `member val` definitions, the F# counterpart of C# struct field initializers. As in C#, `S()` calls a public parameterless constructor and otherwise zero-initializes. Unlike C#, `S()` never zero-initializes a struct with such initializers. Gated behind `--langversion:preview`.

# Motivation

These are errors today:

```fsharp
[<Struct>]
type Options =
    val Retries: int
    new() = { Retries = 3 } // FS0870

[<Struct>]
type Ratio(n: int, d: int) =
    do if d = 0 then invalidArg (nameof d) "zero denominator" // FS0035
    member _.N = n
    member _.D = d
```

FS0870 calls this ban "a restriction imposed on all CLI languages". The CLI has no such restriction, and C# 10 allows these constructors. FS0035 and FS0901 say that the default constructor "would not execute these bindings". Zero-initialization skips every struct constructor, and C# 10 accepted this for field initializers.

- F# structs cannot define `new S()` for C#, `Activator.CreateInstance` or `new()`-constrained code.
- Validation and derived values need `val` fields, explicit constructors and `then` blocks.
- `S()` fails with FS0801 on an imported struct with a non-public parameterless constructor. C# zero-initializes it.

# Detailed design

## Explicit parameterless constructors

A struct type can declare `new() = ...`. The existing rules for struct constructors apply: an object initialization expression that assigns every field without `[<DefaultValue>]` (FS0764), or a call to another constructor, optionally followed by `then`. This includes `[<IsReadOnly>]` and `[<IsByRefLike>]` structs. Struct records and unions cannot declare constructors.

```fsharp
[<Struct>]
type Options =
    val Retries: int
    val DelayMs: int
    new() = { Retries = 3; DelayMs = 100 }

[<Struct>]
type Complex(r: float, i: float) =
    new() = Complex(1.0, 0.0)
    member _.R = r
    member _.I = i
```

## Primary-constructor initializers

FS0081, FS0901 and the struct form of FS0035 are removed. A struct can have a parameterless primary constructor. A primary-constructor struct can contain instance `let`, `let mutable`, `let rec` and `do` definitions ([§8.6.1.3](https://fsharp.github.io/fslang-spec/type-definitions/#8613-instance-function-and-value-definitions-in-primary-constructors)) and `member val` definitions. Below, *initializers* are the `do`, value and `member val` definitions, not function definitions.

```fsharp
[<Struct>]
type Counter() =
    let mutable count = 0
    member _.Count = count
    member _.Increment() = count <- count + 1

[<Struct>]
type Ratio(n: int, d: int) =
    do if d = 0 then invalidArg (nameof d) "zero denominator"
    let sign = if d < 0 then -1 else 1
    member _.N = sign * n
    member _.D = sign * d
```

The rules for classes apply, except:

- Every instance value definition is a field, whether or not members use it, as every struct primary-constructor parameter already is. These fields count as other fields for layout, generated equality, comparison and hashing, default-initialization checks (FS0688, `[<DefaultValue>]`), struct cycles, FS3225 and byref-like rules.
- Closures cannot capture `this` (FS0406). In initializers, parameters and immutable values are locals and can be captured. In members they are fields, as parameters are today.
- No self identifier (FS0658), so initializers cannot call members.

Additional constructors must call another constructor ([§8.6.3](https://fsharp.github.io/fslang-spec/type-definitions/#863-additional-object-constructors-in-classes)), so each constructor call runs the initializers once, in order.

## Accessibility

A parameterless struct constructor must be public, also in the signature file (new error), as in C# (CS8958). For a non-public one, `Activator.CreateInstance<S>()` throws `MissingMethodException` and C# `new S()` zero-initializes.

## Meaning of `S()`

For a struct type `S`, `S()` and `new S()` resolve as follows ([§6.4.2](https://fsharp.github.io/fslang-spec/expressions/#642-object-construction-expressions)):

1. If `S` has a public parameterless constructor or initializers, the candidates are the accessible declared constructors. If `S` has initializers and no candidate applies, a new error suggests `Unchecked.defaultof<S>`.
2. Otherwise zero-initialization is also a candidate, as today.

A constructor with only optional or `ParamArray` parameters is not parameterless. Under rule 2, `S()` prefers zero-initialization to it, as today and in C#. A non-public parameterless constructor of an imported type is never a candidate, as in C#, so `S()` zero-initializes instead of reporting FS0801. F# metadata records whether a struct has initializers, so rule 1 works across assemblies.

`<@ S() @>` is `Expr.NewObject` when `S()` calls a constructor and `Expr.DefaultValue` otherwise, as for C# structs today.

`Unchecked.defaultof`, `Array.zeroCreate`, `[<DefaultValue>]` fields, fields of struct type in other structs and omitted `[<Optional>]` arguments still zero-initialize, as in C#.

## Generic code

`'T : struct` and `'T : (new : unit -> 'T)` do not change, also for structs with initializers, as in C#. A struct satisfies the second if it has a public parameterless constructor or all its fields admit default initialization. `new 'T()` calls `Activator.CreateInstance<'T>()`, which runs a public parameterless struct constructor on .NET Core and later. For .NET Framework, see the C# proposal. SRTP cannot refer to constructors.

# Changes to the F# spec

- [§8.8](https://fsharp.github.io/fslang-spec/type-definitions/#88-struct-type-definitions): remove the rules that struct primary constructors take arguments, that fields of primary-constructor structs are immutable and that structs have no instance `let` or `do`. Add *Explicit parameterless constructors*, *Primary-constructor initializers* and *Accessibility*. Only structs with no public parameterless constructor and no initializers get the implicit default constructor.
- [§6.4.2](https://fsharp.github.io/fslang-spec/expressions/#642-object-construction-expressions): replace the zero-argument struct rule with *Meaning of `S()`*.
- [§5.2.4](https://fsharp.github.io/fslang-spec/types-and-type-constraints/#524-default-constructor-constraints): add the struct rule from *Generic code*.

# Drawbacks

- At a call site, `S()` can run code or zero-initialize, depending on the type, as in C#.
- Zero-initialized values skip constructors and initializers. The zero value must stay valid.
- Existing structs with `then` blocks still allow zero-initializing `S()`. Changing this would break code.
- A value used only during construction still takes space in each instance.

# Alternatives

- **Attribute to allow zero-initializing `S()`** ([dsyme on #1056](https://github.com/fsharp/fslang-suggestions/issues/1056#issuecomment-895235611)): `Unchecked.defaultof<S>` already says this.
- **Warn on `S()` for every struct with constructors** (C# "warning wave"): breaks warnings-as-errors builds. Possible later as an opt-in warning.
- **Non-public parameterless constructors** ([#362 comment](https://github.com/fsharp/fslang-suggestions/issues/362#issuecomment-894781609)): `S()`, `new 'T()` and C# `new S()` would disagree.
- **Class rule for values** (field only if a member uses it): layout and equality would depend on member bodies.
- **C# 11 auto-default structs**: not needed. Object initialization expressions and primary constructors assign every field.
- **Constructors only**: leaves #1056 open, and `type S() =` is useless without initializers.

# Prior art

C# 10 added parameterless struct constructors and field initializers. C# 11 added auto-default structs. This RFC follows C# 10. Rule 1 for initializers has no C# counterpart.

# Compatibility

- **Breaking?** Not for source: all new forms were errors. For binaries, `S()` on an imported struct with an `internal` parameterless constructor visible through `InternalsVisibleTo` called it. Now `S()` zero-initializes, as in C#. No C# or F# compiler emits such constructors.
- **Older compilers, new source**: the errors above.
- **Older compilers, new binaries**: `S()` calls a public parameterless F# struct constructor, as it already does for C# 10 structs. It zero-initializes structs with initializers. Older readers must ignore the new metadata flag. CompilerCompat tests must cover it.
- **FSharp.Core**: no change.

# Interop

C# sees a normal public `.ctor()`: `new S()` calls it, and `default(S)`, arrays and `Activator.CreateInstance` behave as for C# structs. C# `new S()` zero-initializes a struct with initializers and no parameterless constructor. Imported and provided types follow *Meaning of `S()`*.

# Pragmatics

## Diagnostics

- New error: a non-public parameterless struct constructor.
- New error: `S()` on a struct with initializers and no applicable constructor, with a code fix to `Unchecked.defaultof<S>`.
- Older language versions report the new forms as unavailable.

## Tooling

Navigation, tooltips and generated signatures treat the constructor as any other (`new: unit -> S`). Stepping through initializers works as in classes. No FCS API change.

## Performance

No change for existing code. With a parameterless constructor, `S()` is a call, not `initobj`.

## Scaling

None.

## Culture-aware formatting/parsing

None.

# Unresolved questions

1. Ignore *non-public* imported parameterless constructors (C#), or only *inaccessible* ones (no change under `InternalsVisibleTo`)?
2. Make the new `S()` error a warning?
3. Should a struct with initializers and no parameterless constructor fail `'T : (new : unit -> 'T)`? C# accepts it.
