# F# RFC FS-1342 - `fst` and `snd` for struct tuples and larger tuples

The design suggestion [Add List.chooseV, Seq.tryPickV, etc. for ValueOption](https://github.com/fsharp/fslang-suggestions/issues/739) has been marked "approved in principle". This RFC covers the `fst`/`snd` overloads that the F# team sketched in [this comment](https://github.com/fsharp/fslang-suggestions/issues/739#issuecomment-5357571363), with a compiler-controlled open instead of `[<AutoOpen>]` (see [Alternatives](#alternatives)). The `ValueOption` collection functions are out of scope.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/739) (the `fst`/`snd` thread starts at [this comment](https://github.com/fsharp/fslang-suggestions/issues/739#issuecomment-5302753208))
- [x] Approved in principle (the parent suggestion)
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [x] [Discussion](https://github.com/fsharp/fslang-design/pull/844)

Depends on: [FS-1338 OverloadResolutionPriorityAttribute (ORP) support](FS-1338-OverloadResolutionPriorityAttribute.md) (F# 11.2).

# Summary

`fst` and `snd` become overloaded static members of a new FSharp.Core type `TupleAccessors`, for reference and struct tuples of arity 2 to 7. Under the preview feature `StructTupleAccessors`, the compiler opens this type implicitly, and a syntactic tuple argument can select among overloads that each take one parameter. When the argument type is unknown, `fst` and `snd` still infer `'a * 'b`.

```fsharp
fst (struct (1, "a"))                                     // 1
snd (struct (1, "a", 3.0))                                // "a"
[ struct (2, "b"); struct (1, "a") ] |> List.sortBy fst
```

# Motivation

FSharp.Core has `ValueOption` parity ([FS-1065](https://github.com/fsharp/fslang-design/blob/main/FSharp.Core-4.6.0/FS-1065-valueoption-parity.md)), and F# has struct tuple `Item1`/`Item2` access ([FS-1064](FS-1064-struct-tuple-equivalence.md)), but `fst` and `snd` accept only reference pairs. Code that uses struct tuples to avoid allocation writes `fun struct (a, _) -> a` or private `fstv`/`sndv` helpers. Arities 3 to 7 remove the pattern match that the first or second element of a larger tuple needs today.

# Detailed design

## FSharp.Core

```fsharp
namespace Microsoft.FSharp.Core

[<AbstractClass; Sealed>]
type TupleAccessors =
    [<OverloadResolutionPriority(1); CompiledName("Fst")>]
    static member inline fst: tuple: ('T1 * 'T2) -> 'T1
    [<CompiledName("Fst")>]
    static member inline fst: tuple: struct ('T1 * 'T2) -> 'T1
    [<CompiledName("Fst")>]
    static member inline fst: tuple: ('T1 * 'T2 * 'T3) -> 'T1
    // ... reference and struct overloads (priority 0) up to arity 7, and the same 12 overloads of snd
```

- Arity 7 is the largest arity that `System.Tuple` and `System.ValueTuple` represent without `TRest`.
- The members are `inline` and compile to a tuple field load, as `Operators.fst` does.
- On `netstandard2.0` and `netstandard2.1`, FSharp.Core defines an `internal` polyfill of `System.Runtime.CompilerServices.OverloadResolutionPriorityAttribute`. The compiler recognises the attribute by its full name in IL and in F# metadata, including the polyfill defined in FSharp.Core, so the priority applies to every FSharp.Core build.

## Implicit open

If `StructTupleAccessors` is enabled and the referenced FSharp.Core defines `Microsoft.FSharp.Core.TupleAccessors`, the compiler adds the static content of the type to the initial environment immediately after the assembly-level `AutoOpen` attributes of FSharp.Core. Thus unqualified `fst` and `snd` shadow `Operators.fst` and `Operators.snd`. The `AutoOpen` attributes of assemblies added later, user definitions and `open` declarations shadow the accessors. An explicit `open FSharp.Core` reopens `Operators`, so `fst` and `snd` in that file bind to `Operators` again.

## Tupled argument for overloaded single-parameter methods

Today the unnamed arguments of a method call become one tuple argument only if the method group has exactly one accessible candidate, with one parameter. Against two or more candidates, `fst (1, 2)` is a call with two arguments and fails with FS0503.

Under `StructTupleAccessors`, the arguments also become one tuple argument, before overload resolution, if all of these conditions are true:

1. The method group has two or more accessible candidates.
2. Each candidate has one curried group of one parameter, and the parameter is not `ParamArray`, `out`, optional or caller-info.
3. The call has one curried group of two or more unnamed arguments, and no named arguments.
4. The call is not an indexed property setter.

Overload resolution then selects by the arity and the kind of the tuple. The rule applies to all method groups, not only `fst` and `snd`.

## Resolution

| Expression | Result |
|---|---|
| `fst (1, "a")` | reference pair, by the tupled-argument rule |
| `fst (1, 2, 3)` | reference triple; today a type error |
| `let f x = fst x` | reference pair, `'a * 'b -> 'a`: for an unknown argument type, ORP keeps only priority 1 |
| `xs \|> List.map fst`, `xs: struct (int * int) list` | struct pair |
| `List.map fst xs`, the same `xs` | reference pair, then a type error: `fst` is checked before `xs`, as any first-class overloaded method |
| `fst "s"` | FS0041 with the candidate list; today FS0001 |

## Interactions

- **Quotations**: `<@ fst t @>` is `Call(None, TupleAccessors.Fst, [t])`, not `Call(None, Operators.Fst, [t])`. `query { }` and `LeafExpressionConverter` do not special-case `Operators.Fst`, so they do not change. LINQ providers that match `Operators.Fst` need the new members.
- **SRTP**: no interaction. **Type providers**: provided method groups follow the tupled-argument rule like any other.
- **C#** sees static overloads `TupleAccessors.Fst`/`Snd`. They differ by exact tuple type, so the priority does not change the C# result.
- **Tooling**: hover and signature help show 12 overloads, semantic classification shows `fst` as a method, and go to definition goes to `TupleAccessors`. FSharp.Compiler.Service reports unqualified `fst` as a `TupleAccessors` member, so consumers that match `Operators.fst`, such as analyzers and Fable's replacement tables, need an update.

# Changes to the F# spec

- §12 Program Structure and Execution, step 1 (initial environment): after the `AutoOpen` attributes of FSharp.Core, add [Implicit open](#implicit-open).
- §14.4 Method Application Resolution: beside the single-candidate tupling rule, add [the multi-candidate rule](#tupled-argument-for-overloaded-single-parameter-methods).

# Drawbacks

- `fst` accepts tuples of arity 3 to 7, and tooling shows a method group instead of a function signature.
- `fst` is used as a first-class value more often than most methods, so the `List.map fst xs` limitation of [Resolution](#resolution) is more visible.

# Alternatives

- **`fstv`/`sndv` in `Operators`**: no compiler change and no dependency on FS-1338, but more names and no single name for both tuple kinds. This is the fallback if this design is rejected.
- **`[<AutoOpen>]` on `TupleAccessors`**, as in the thread: compilers since F# 5 honour `[<AutoOpen>]` on F# types, so the new FSharp.Core changes all language versions. With `--langversion:9.0` (no ORP), `let f x = fst x` fails with FS0041, and compilers without the [tupled-argument rule](#tupled-argument-for-overloaded-single-parameter-methods) reject `fst (1, 2)`. Rejected: an upgrade of the FSharp.Core package breaks source.
- **A version-gated `[<AutoOpenIfLangVersion>]` attribute**, raised in the thread: general, but adds a public attribute and a new `AutoOpen` rule for one type. The implicit open has the same effect without new surface.
- **ORP and the tupled-argument rule on all language versions**: older compilers still break, and the gating of FS-1338 changes.
- **An SRTP `fst` on `Item1`**: `let f x = fst x` gets an unresolved constraint, and struct tuple types do not solve member constraints on `Item1` (FS0001).

# Prior art

- OCaml and Haskell define `fst` and `snd` for pairs only.
- C# 13 `OverloadResolutionPriorityAttribute` adds a preferred overload beside an existing one without breaking call sites.

# Compatibility

- **Binary**: additive. `Operators.Fst` and `Operators.Snd` do not change.
- **Feature off, an older compiler, or an older FSharp.Core**: no change. `fst` and `snd` bind to `Operators`. A compiler that can consume the new FSharp.Core can call `TupleAccessors.fst (struct (1, 2))` qualified.
- **Feature on**: the tupled-argument rule applies only to calls that fail today. The implicit open makes `fst` and `snd` a method group, which changes these programs:
  - Quotations change shape (see [Interactions](#interactions)).
  - A tuple element `id = expr` becomes a named argument: `snd (2, a = 1)` fails (FS0508).
  - The expected type no longer flows into the argument: `let g: string -> int = fst ((fun x -> x.Length), 0)` fails (FS0072).

# Interop

See [Interactions](#interactions). No related C# feature is planned.

# Pragmatics

## Diagnostics

No new diagnostics. Under the feature, a call with two or more arguments to a group of single-parameter overloads, such as `Math.Abs(1, 2)`, reports FS0041 for a tuple argument instead of FS0503.

## Tooling

See [Interactions](#interactions).

## Performance

Each unqualified `fst` or `snd` runs overload resolution over at most 12 candidates instead of binding a value.

## Scaling

Not applicable.

## Culture-aware formatting/parsing

Not applicable.

# Unresolved questions

- Arities above 7.
- Further accessors, such as `thd3`, or a `ValueTuple` module (raised by @bartelink in the thread).
