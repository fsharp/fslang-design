# F# RFC FS-1353 - Parameterless struct constructors and struct initializers

The design suggestions [Allow parameterless constructors in structs](https://github.com/fsharp/fslang-suggestions/issues/362) and [Allow do bindings in structs](https://github.com/fsharp/fslang-suggestions/issues/1056) have been marked "approved in principle". This RFC covers both.

- [x] [Suggestion #362](https://github.com/fsharp/fslang-suggestions/issues/362)
- [x] [Suggestion #1056](https://github.com/fsharp/fslang-suggestions/issues/1056)
- [x] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

**C# reference:** [Parameterless struct constructors (C# 10)](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/parameterless-struct-constructors.md), [Auto-default structs (C# 11)](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/auto-default-structs.md)

# Summary

An F# struct type can declare a parameterless constructor, either as `new() = ...` or as a parameterless primary constructor. A struct type with a primary constructor can contain instance `let`, `do` and `member val` definitions. These are the F# counterpart of C# struct field initializers. `S()` calls a public parameterless constructor if the type has one, and otherwise zero-initializes, as in C#. For a struct type with primary-constructor initializers, `S()` never zero-initializes. The feature is gated behind `--langversion:preview`.

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

FS0870 calls this ban "a restriction imposed on all CLI languages". The CLI has no such restriction, and C# 10 allows them. FS0035 and FS0901 reject `do` and `let` because "the default constructor for structs would not execute these bindings". Zero-initialization skips every struct constructor, so this reason applies to all of them. C# 10 accepted the same trade-off for field initializers.

As a result:

- An F# struct cannot give `new S()` a meaning for C# callers, `Activator.CreateInstance` or `new()`-constrained generic code.
- Validation and derived values in a struct need `val` fields, explicit constructors and `then` blocks instead of primary-constructor code.
- F# rejects `S()` with FS0801 for an imported struct whose parameterless constructor is not public. C# zero-initializes it.

# Detailed design

## Explicit parameterless constructors

A struct type can declare `new() = ...`. The body follows the existing rules for struct constructors. It is an object initialization expression that assigns every field without `[<DefaultValue>]` (FS0764), or a call to another constructor of the type. Either form can be followed by `then`.

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

This applies to every struct type that can declare constructors, including `[<IsReadOnly>]` and `[<IsByRefLike>]` structs. Struct records and struct unions cannot declare constructors and do not change.

## Primary-constructor initializers

A struct type can have a parameterless primary constructor (FS0081 is removed). A struct type with a primary constructor can contain instance `let`, `let mutable`, `let rec` and `do` definitions ([§8.6.1.3](https://fsharp.github.io/fslang-spec/type-definitions/#8613-instance-function-and-value-definitions-in-primary-constructors)), and `member val` definitions (FS0901 and the struct form of FS0035 are removed). This RFC calls the `do` statements, value definitions and `member val` definitions *initializers*. Function definitions are not initializers.

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

The rules for classes apply, with these differences:

- Every instance value definition is an instance field, as every primary-constructor parameter of a struct already is. The set of fields does not depend on which values the members use. These fields behave as other fields for layout, generated equality, comparison and hashing, default-initialization checks (FS0688, `[<DefaultValue>]`), struct cycles, FS3225 in `[<IsReadOnly>]` structs, and byref-like rules.
- Additional constructors must call another constructor of the type ([§8.6.3](https://fsharp.github.io/fslang-spec/type-definitions/#863-additional-object-constructors-in-classes)). So each constructor call runs the initializers once, in declaration order.
- Closures cannot capture `this` (FS0406). Inside initializers, parameters and immutable value definitions are locals, so closures there can use them. Inside members they are fields of `this`, so closures there cannot, as for primary-constructor parameters today.
- The primary constructor cannot bind a self identifier (FS0658, unchanged). So initializers cannot call members.

## Accessibility

A parameterless struct constructor must be public, as in C# (CS8958). If the type has a signature, the signature must declare the constructor. Otherwise a new error is reported.

```fsharp
[<Struct>]
type S private () = // error
    member _.X = 1
```

Other code does not call a non-public parameterless struct constructor: `Activator.CreateInstance<S>()` throws `MissingMethodException`, and C# `new S()` zero-initializes.

## Meaning of `S()`

For a struct type `S`, `S()` and `new S()` resolve as follows ([§6.4.2](https://fsharp.github.io/fslang-spec/expressions/#642-object-construction-expressions)):

1. If `S` has a public parameterless constructor or has initializers, the candidates are the accessible declared constructors. If `S` has initializers and no candidate applies, a new error suggests `Unchecked.defaultof<S>`.
2. Otherwise zero-initialization is a candidate in addition to the declared constructors, as today.

A constructor whose parameters are all optional or `ParamArray` is not parameterless. Under rule 2, `S()` prefers zero-initialization to it, as today and as in C#. A non-public parameterless constructor of an imported type is never a candidate, as in C#. For such a type, `S()` zero-initializes instead of reporting FS0801.

F# metadata records whether a struct type has initializers, so rule 1 also applies to struct types from referenced F# assemblies.

## Zero-initialization

These ignore constructors and initializers, as today and as in C#: `Unchecked.defaultof<S>`, `Array.zeroCreate`, fields with `[<DefaultValue>]`, fields of type `S` in other structs, and omitted `[<Optional>]` arguments of type `S`.

## Generic code

Every struct type satisfies `'T : struct`. A struct type satisfies `'T : (new : unit -> 'T)` if it has a public parameterless constructor or if all its fields admit default initialization. Both rules are unchanged, and struct types with initializers are included, as in C#. `new 'T()` calls `Activator.CreateInstance<'T>()`. On .NET Core and later, this runs a public parameterless struct constructor. For .NET Framework, see the C# proposal. Member constraints cannot refer to constructors, so SRTP does not change.

## Quotations

`<@ S() @>` is `Expr.NewObject` when `S()` calls a constructor and `Expr.DefaultValue` when it zero-initializes. This is the current behaviour for C# structs.

# Changes to the F# spec

- [§8.8 Struct Type Definitions](https://fsharp.github.io/fslang-spec/type-definitions/#88-struct-type-definitions): remove "Structs that have primary constructors must accept at least one argument", "The fields in a struct may be mutable only if the struct does not have a primary constructor" and "Structs may not have “let” or “do” statements unless they are static". Add the rules in *Explicit parameterless constructors*, *Primary-constructor initializers* and *Accessibility*. Give the implicit default constructor only to struct types that have no public parameterless constructor and no initializers.
- [§6.4.2 Object Construction Expressions](https://fsharp.github.io/fslang-spec/expressions/#642-object-construction-expressions): replace "does not have a constructor method that takes zero arguments" with the rules in *Meaning of `S()`*.
- [§5.2.4 Default Constructor Constraints](https://fsharp.github.io/fslang-spec/types-and-type-constraints/#524-default-constructor-constraints): state the struct rule from *Generic code*, because §8.8 no longer gives every struct type an implicit default constructor.

# Drawbacks

- At a call site, `S()` can run code or zero-initialize, depending on the type. C# has the same property.
- Zero-initialized values skip constructors and initializers. Struct authors must still keep the zero value valid.
- `S()` still zero-initializes existing struct types whose explicit constructors have `then` blocks. A restriction would break existing code.
- A value definition that is used only during construction still takes space in every instance.

# Alternatives

- **Opt-in attribute for zero-initializing `S()`** ([dsyme on #1056](https://github.com/fsharp/fslang-suggestions/issues/1056#issuecomment-895235611)): `Unchecked.defaultof<S>` already states the intent.
- **Warning on `S()` for all struct types with constructors** (the C# "warning wave" idea): breaks builds that treat warnings as errors. It can be a separate, opt-in warning.
- **Non-public parameterless constructors** ([#362 comment](https://github.com/fsharp/fslang-suggestions/issues/362#issuecomment-894781609)): F# `S()` would run the constructor, but `new 'T()` and C# `new S()` would not (see *Accessibility*).
- **Class rule for value definitions** (a field only if a member uses it): layout and equality would change when a member body changes.
- **C# 11 auto-default structs**: not needed. An F# object initialization expression assigns every field, and a primary constructor assigns every field that it creates.
- **Constructors only**: leaves #1056 without a design. A parameterless primary constructor has no use without initializers.

# Prior art

C# 10 added parameterless struct constructors and struct field initializers. C# 11 made struct constructors zero-initialize the fields that they do not assign. This RFC follows C# 10 for declaration, accessibility, import and zero-initialization. Rule 1 for struct types with initializers has no C# counterpart.

# Compatibility

- **Breaking change?** Not for F# source: every new form was an error (FS0870, FS0081, FS0901, FS0035). One behaviour changes for binaries. If an imported struct has an `internal` parameterless constructor that is visible through `InternalsVisibleTo`, `S()` called it before and zero-initializes now, as in C#. No C# or F# compiler emits such a constructor.
- **Older compilers, new source**: the errors listed above.
- **Older compilers, new binaries**: `S()` calls a public parameterless constructor of an F# struct type. Current compilers already call an imported public zero-argument struct constructor, for example from C# 10. Older compilers do not know about initializers, so for such a type `S()` zero-initializes. The initializer flag must be stored in F# metadata so that older readers ignore it. The CompilerCompat tests must cover it.
- **FSharp.Core**: no change.

# Interop

- C# sees an ordinary public `.ctor()`. `new S()` calls it. `default(S)`, arrays and `Activator.CreateInstance` behave as for a C# struct.
- C# `new S()` zero-initializes a struct type that has initializers but no parameterless constructor. F# cannot prevent this.
- Imported types, including provided types, use the rules in *Meaning of `S()`*.

# Pragmatics

## Diagnostics

- New error: a parameterless struct constructor is not public.
- New error: `S()` on a struct type with initializers and no applicable constructor. The message suggests `Unchecked.defaultof<S>`, and a code fix applies it.
- With an older language version, the forms that FS0870, FS0081, FS0901 and FS0035 reject report the feature as not available in that version.

## Tooling

- Go to definition, find references and rename on `S()` go to the constructor when `S()` calls one, and to the type otherwise, as today.
- Tooltips, signature help and generated signatures show `new: unit -> S`.
- Breakpoints and stepping in struct initializers work as in class primary constructors.
- No FCS API change.

## Performance

No change for existing code. On a type with a parameterless constructor, `S()` is a call instead of `initobj`.

## Scaling

No new dimensions.

## Culture-aware formatting/parsing

None.

# Unresolved questions

1. Should `S()` ignore *non-public* imported parameterless constructors (as C# does), or only *inaccessible* ones (no behaviour change under `InternalsVisibleTo`)?
2. Should the new `S()` error for struct types with initializers be a warning?
3. Should a struct type with initializers and no parameterless constructor stop satisfying `'T : (new : unit -> 'T)`? C# accepts such types.
