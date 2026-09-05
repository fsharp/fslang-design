# F# RFC FS-1342 - `fst` and `snd` for struct tuples and larger tuples

The design suggestion [Add List.chooseV, Seq.tryPickV, etc. for ValueOption](https://github.com/fsharp/fslang-suggestions/issues/739) has been marked "approved in principle". Its discussion thread grew a request for struct-tuple accessors, and the F# team proposed an overload-based design in [this comment](https://github.com/fsharp/fslang-suggestions/issues/739#issuecomment-5357571363). This RFC covers that proposal only; the `ValueOption` collection functions of the suggestion are a separate RFC.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/739) (the `fst`/`snd` side thread, starting at [this comment](https://github.com/fsharp/fslang-suggestions/issues/739#issuecomment-5302753208))
- [x] Approved in principle (parent suggestion; the accessor design was endorsed by the F# team in the thread)
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

**Depends on:** [FS-1338 OverloadResolutionPriorityAttribute support](FS-1338-OverloadResolutionPriorityAttribute.md) (preview).

# Summary

`fst` and `snd` become overloaded static members of a new FSharp.Core type, `Microsoft.FSharp.Core.TupleAccessors`, with one overload per tuple kind (reference or struct) and per arity from 2 to 7. The reference-pair overload carries `[<OverloadResolutionPriority(1)>]`, so a use whose argument type is not yet known still infers `'a * 'b`, exactly as today.

The type is **not** auto-opened by FSharp.Core. Under a new preview language feature, `StructTupleAccessors`, the compiler opens it implicitly after FSharp.Core's own auto-opens, and generalises the "single named item" argument-tupling rule so that `fst (a, b)` keeps compiling against an overloaded method group. `Operators.fst` and `Operators.snd` are unchanged and remain reachable qualified.

```fsharp
fst (1, "a")                       // 1        reference pair, as today
fst (struct (1, "a"))              // 1        struct pair
snd (struct (1, "a", 3.0))         // "a"      struct triple
[ struct (2, "b"); struct (1, "a") ] |> List.sortBy fst
let f x = fst x                    // val f: 'a * 'b -> 'a   (unchanged inference)
Operators.fst (1, 2)               // still available
```

# Motivation

Struct tuples have been in the language since F# 4.1, and FSharp.Core has since been given `ValueOption` parity ([FS-1065](https://github.com/fsharp/fslang-design/blob/main/FSharp.Core-4.6.0/FS-1065-valueoption-parity.md), [FS-1065.1](FS-1065.1-option-voption-conversion-functions.md)) and struct-tuple `Item1`/`Item2` access ([FS-1064](FS-1064-struct-tuple-equivalence.md)). The two most used tuple functions, `fst` and `snd`, still accept only reference pairs. Code that adopts struct tuples for allocation reasons has to write `fun struct (a, _) -> a` at every use, or define private `fstv`/`sndv` helpers per project. The thread on #739 asked for `fstv`/`sndv`; the F# team preferred a single pair of names that work on both tuple kinds, and identified `OverloadResolutionPriorityAttribute` as the mechanism that keeps type inference stable.

Extending the overloads to arities 3 to 7 makes `fst`/`snd` usable on any tuple whose first or second component is wanted, which today needs an explicit pattern.

# Detailed design

## FSharp.Core

A new type in namespace `Microsoft.FSharp.Core`, declared after `module Operators`:

```fsharp
[<AbstractClass; Sealed>]
type TupleAccessors =
    [<OverloadResolutionPriority(1); CompiledName("Fst")>]
    static member inline fst: tuple: ('T1 * 'T2) -> 'T1
    [<CompiledName("Fst")>]
    static member inline fst: tuple: struct ('T1 * 'T2) -> 'T1
    [<CompiledName("Fst")>]
    static member inline fst: tuple: ('T1 * 'T2 * 'T3) -> 'T1
    [<CompiledName("Fst")>]
    static member inline fst: tuple: struct ('T1 * 'T2 * 'T3) -> 'T1
    // ... reference and struct overloads up to arity 7 ...

    [<OverloadResolutionPriority(1); CompiledName("Snd")>]
    static member inline snd: tuple: ('T1 * 'T2) -> 'T2
    [<CompiledName("Snd")>]
    static member inline snd: tuple: struct ('T1 * 'T2) -> 'T2
    // ... reference and struct overloads up to arity 7 ...
```

- 24 members in total: two names × six arities × two tuple kinds. Arity 7 is the largest arity that `System.Tuple` and `System.ValueTuple` represent without an `TRest` nesting.
- Only the **reference pair** overload of each name carries `[<OverloadResolutionPriority(1)>]`. All other overloads have the default priority 0.
- Every member is `inline`, so the generated IL is the same tuple field load that `Operators.fst` produces today.
- The type is **not** marked `[<AutoOpen>]`. See [Compatibility](#compatibility) for why.
- `Operators.fst` and `Operators.snd` are left exactly as they are, for binary compatibility and for older compilers.
- `System.Runtime.CompilerServices.OverloadResolutionPriorityAttribute` exists only in .NET 9 and later. FSharp.Core targets `netstandard2.0`, `netstandard2.1` and a `net` TFM, so the two `netstandard` builds carry an `internal` polyfill of the attribute, in the same way FSharp.Core already polyfills `CollectionBuilderAttribute` and `ScopedRefAttribute`. The compiler recognises the attribute by its full name on both IL and F# metadata, so the polyfill is honoured.
- The signature file must repeat `[<OverloadResolutionPriority(1)>]`: consumers of FSharp.Core see the attributes of the `.fsi`, and the compiler does not currently enforce that this attribute matches between `.fs` and `.fsi`.

## Compiler: implicit opening of `TupleAccessors`

When the `StructTupleAccessors` feature is enabled and the referenced FSharp.Core defines `Microsoft.FSharp.Core.TupleAccessors`, the compiler adds the static content of that type to the initial name-resolution environment, immediately after it has applied FSharp.Core's assembly-level `AutoOpen` attributes. The effect is the same as an `open type Microsoft.FSharp.Core.TupleAccessors` placed before the first line of every file.

Consequences:

- Unqualified `fst` and `snd` resolve to the overloaded method group, which shadows `Operators.fst`/`snd` because it is added later.
- User definitions, `open` declarations and `open type` declarations come later still, so they shadow the accessors as they shadow `Operators.fst` today.
- If FSharp.Core does not define the type (an older FSharp.Core with a newer compiler), nothing is opened and `fst`/`snd` resolve to `Operators` as before.
- Compiling FSharp.Core itself is unaffected; its own uses of `fst` bind to `Operators.fst`.

## Compiler: tupled argument for an overloaded group of unary methods

F# lets a syntactic tuple be passed to a method that takes a single parameter, so `T.M(1, 2)` compiles against `static member M((a, b))`. Today that rule fires only when the method group has exactly one accessible candidate. With two or more overloads of `fst`, `fst (1, 2)` is checked as a call with two arguments and fails with FS0503:

```
error FS0503: A member or object constructor 'fst' taking 2 arguments is not accessible
from this code location. All accessible versions of method 'fst' take 1 arguments.
```

Under `StructTupleAccessors` the rule is generalised. When all of the following hold, the syntactic tuple is treated as a single argument, and ordinary overload resolution then selects among the candidates by the tuple's arity and struct-ness:

1. the method group has two or more accessible candidates;
2. every candidate has exactly one curried parameter group containing exactly one parameter, and that parameter is "simple" (not `ParamArray`, not `out`, not optional, no caller-info attribute);
3. the call supplies a single curried group of two or more unnamed arguments and no named arguments;
4. the call is not an indexed property setter.

The rule only applies to calls that are an arity mismatch against every candidate today, so it turns an FS0503 into a resolution and cannot change the meaning of a program that already compiles. It is a general rule, not special-cased to `fst`/`snd`; any user type with several single-tuple-parameter overloads benefits.

The same path covers quotations: `<@ fst (1, 2) @>` type-checks like the plain call.

## Resolution table

With the feature on, the reference FSharp.Core, and the probe results from a compiler build that includes FS-1338:

| Expression | Resolves to | Note |
|---|---|---|
| `fst (1, "a")` | ref pair | via the generalised tupling rule |
| `fst t` where `t: int * string` | ref pair | ordinary resolution |
| `fst (struct (1, "a"))`, `fst s` where `s: struct (int * string)` | struct pair | ordinary resolution |
| `fst (1, 2, 3)` | ref triple | new: today a type error |
| `snd (struct (1, 2, 3, 4))` | struct 4-tuple | new |
| `let f x = fst x` | ref pair, `'a * 'b -> 'a` | argument type unknown: ORP pre-filter keeps only the priority-1 candidate |
| `let g = fst` | ref pair | same |
| `[ struct (1, 2) ] \|> List.map fst` | struct pair | the list type is known before `fst` is checked |
| `List.map fst [ struct (1, 2) ]` | ref pair, then a type error | the method group is resolved before the list is checked. Same left-to-right limitation as any overloaded method used first-class; write the pipeline form or annotate |
| `fst "s"` | error FS0041 listing the candidates | today an FS0001 "expected a tuple" |
| `Operators.fst (1, 2)` | `Operators.fst` | unchanged |
| `let fst x = 42 in fst (1, 2)` | user `fst` | user definitions shadow |

## Quotations

`<@ fst t @>` produces `Call(None, TupleAccessors.Fst, [t])` (with the overload matching `t`) instead of `Call(None, Operators.Fst, [t])`. FSharp.Core's own query translation does not special-case `Operators.Fst`/`Snd`, so `query { }` and `LeafExpressionConverter` are unaffected. Third-party quotation consumers that match on `Operators.Fst` by `MethodInfo` or by name will see the new member for code compiled with the feature on; see [Drawbacks](#drawbacks).

# Changes to the F# spec

Both changes are gated by the `StructTupleAccessors` language feature.

**§14.1 Name Resolution, initial environment.** After the assembly-level `AutoOpen` attributes of FSharp.Core are applied to the initial environment, if FSharp.Core defines the type `Microsoft.FSharp.Core.TupleAccessors`, the static content of that type is added to the environment as if by `open type Microsoft.FSharp.Core.TupleAccessors`.

**§14.4 Method Application Resolution, "single named item" rule.** The existing text applies when "there is a single accessible method taking one argument" and the call supplies several unnamed arguments; the arguments are then tupled into one. Add:

```diff
  If the method group has exactly one accessible candidate, that candidate has a single parameter,
  and the call supplies several unnamed arguments, the arguments are combined into a single tuple
  argument.
+ If the method group has two or more accessible candidates, every candidate has exactly one
+ curried parameter group of exactly one simple parameter (not a ParamArray, out, optional or
+ caller-info parameter), the call supplies a single curried group of two or more unnamed arguments
+ and no named arguments, and the call is not an indexed property setter, the arguments are likewise
+ combined into a single tuple argument before overload resolution.
```

# Drawbacks

- **Tooling noise.** Hovering `fst` shows a method group with 12 overloads instead of one function signature, and completion lists the group. Errors for a wrong argument (`fst "s"`) become an FS0041 candidate list instead of the short "expected a tuple" mismatch.
- **Semantic widening.** `fst (1, 2, 3)` is a type error today and compiles under the feature. Code that relied on `fst` to reject non-pairs loses that check.
- **First-class use with an unknown argument type is silently the reference pair.** `List.map fst xs` where `xs` is checked after `fst` picks the reference overload, then fails to unify if `xs` holds struct tuples. This matches how every overloaded method behaves when used first-class, but `fst` is far more common in that position than typical methods.
- **Quotation shape.** Libraries that translate quotations and match `Operators.Fst`/`Operators.Snd` (Fable's replacement tables, some LINQ providers) see `TupleAccessors.Fst`/`Snd` for code compiled with the feature on and need a matching entry. Fable compiles FSharp.Core by name, so this needs a coordinated addition there.
- **Dependency on ORP.** Without the ORP pre-filter, `let f x = fst x` is ambiguous (FS0041). The feature must therefore ship no earlier than `OverloadResolutionPriority` and must never be promoted to a numbered language version ahead of it.

# Alternatives

- **`fstv` / `sndv` as plain functions in `Operators`.** Zero compiler work, no inference or tooling changes, no quotation impact, and no dependency on FS-1338. It does not unify the two tuple kinds under one name and adds vocabulary; the F# team preferred the overload design in the thread. It remains the fallback if the overload design is rejected.
- **A literal `[<AutoOpen>]` type in FSharp.Core**, as sketched in the thread. `[<AutoOpen>]` on types has been honoured by every compiler since F# 5, so the new FSharp.Core would take effect on every compiler and language version that references it. Measured on a compiler build with FS-1338: with `--langversion:9.0` (ORP off) `let f x = fst x` becomes FS0041, and on any compiler without the generalised tupling rule `fst (1, 2)` becomes FS0503. Rejected: it makes a NuGet upgrade of FSharp.Core a source-breaking change.
- **Making the ORP pre-filter and the tupling rule unconditional** so the `[<AutoOpen>]` variant works on all language versions. Still breaks older compilers that pick up the new FSharp.Core package, and changes FS-1338's agreed gating.
- **An SRTP-based `fst`** constrained on `Item1`. Kills the inference of `fst`'s argument type (`let f x = fst x` would carry an unresolved constraint instead of `'a * 'b -> 'a`) and would need F# tuple types to solve member constraints against `Item1`, which they do not do today. Rejected.
- **Do nothing.** Users keep writing `fun struct (a, _) -> a`.

# Prior art

- OCaml and Haskell define `fst`/`snd` for pairs only; neither has struct tuples.
- C# exposes `Item1`/`Item2` on both `System.Tuple` and `System.ValueTuple`; F# exposes them too since [FS-1064](FS-1064-struct-tuple-equivalence.md), with a warning steering users to pattern matching.
- C# 13 introduced `OverloadResolutionPriorityAttribute` for exactly this situation, adding a preferred overload beside an existing one without breaking inference at call sites; [FS-1338](FS-1338-OverloadResolutionPriorityAttribute.md) brings it to F#.
- FSharp.Core's `TaskBuilderExtensions.LowPriority`/`HighPriority` modules and `QueryRunExtensions` use ordered `AutoOpen` modules to layer overloads; this RFC uses the same layering idea through the compiler-controlled implicit open.

# Compatibility

- **Binary.** Not breaking. `Operators.Fst`/`Snd` are retained; the new type is additive.
- **Source, older compiler with the new FSharp.Core.** The type is present but never opened, so nothing changes. Explicit `TupleAccessors.fst (struct (1, 2))` works on any compiler that can consume the assembly.
- **Source, new compiler below the feature's language version.** Unchanged: `fst`/`snd` bind to `Operators`.
- **Source, new compiler with the feature on and an older FSharp.Core.** Unchanged: the implicit open is skipped when the type is absent.
- **Source, feature on.** Every program that compiled before still compiles with the same meaning, except quotation consumers that inspect `Operators.Fst`/`Snd` (see Drawbacks) and code that depended on `fst` rejecting triples.
- **FSharp.Core with older compilers in general.** Adding an `internal` attribute type and a public static class is the same kind of addition FSharp.Core has made before.

# Interop

- **Consumed from C#.** `TupleAccessors.Fst(...)`/`Snd(...)` are ordinary static overloads on `System.Tuple<...>` and `System.ValueTuple<...>`; C# 13 honours the priority attribute on the `net` build and ignores the polyfilled one on `netstandard` (Roslyn matches the attribute by name; no behavioural change either way, since C# resolves the struct/reference overloads by exact type).
- **Related features.** None planned in C#. The design is the F# use of C# 13's overload-priority mechanism.

# Pragmatics

## Diagnostics

No new diagnostics. Misuse produces the existing FS0041 (ambiguous or no matching overload) with the candidate list, or FS0001 once an overload is chosen. FS0503 for `fst (1, 2)` disappears under the feature.

## Tooling

- Tooltips show the overload group; signature help lists the overloads; go-to-definition lands on the `TupleAccessors` member in FSharp.Core's signature.
- Debugging, breakpoints and stepping are unaffected: the members are `inline` and compile to the same tuple field loads as today.
- Colorization, brace matching, error recovery: unaffected.

## Performance

- Generated code: identical to today for pairs; for larger tuples, one field load.
- Compilation: the implicit open adds 24 members to the initial environment once per compilation. Overload resolution for `fst`/`snd` now runs over 12 candidates instead of binding a value; the ORP pre-filter reduces the inference-only case to one candidate before betterness. The generalised tupling rule adds one `GetParamAttribs` call per candidate only when a call has two or more arguments against a group of two or more candidates.

## Scaling

- Tuple arities covered: 2 to 7 (fixed set).
- Overloads per name: 12; candidates per resolution: at most 12.

## Culture-aware formatting/parsing

Not applicable.

# Unresolved questions

- **Arities above 7.** F# tuple syntax covers them through `Rest` nesting; adding overloads for them is mechanical but grows the group. Decide whether to stop at 7 (this RFC) or extend.
- **Further accessors.** Whether `thd3`-style accessors, or a `ValueTuple` module, should follow (raised in the thread by @bartelink).
- **Feature promotion.** `StructTupleAccessors` must move to a numbered language version together with `OverloadResolutionPriority`, never before it.
- **Fable and quotation-consuming libraries.** Coordination so that `TupleAccessors.Fst`/`Snd` are recognised where `Operators.Fst`/`Snd` are today.
- **Signature conformance.** Whether the compiler should start enforcing that `OverloadResolutionPriorityAttribute` matches between `.fs` and `.fsi` (it enforces this for other compiler-semantic attributes via FS3888).

# Appendix: implementation sketch (dotnet/fsharp)

For reviewers who want to see the compiler-side footprint; file references are to `dotnet/fsharp` `main` at the time of writing.

- **FSharp.Core** — `src/FSharp.Core/prim-types.fs`/`.fsi`: `OverloadResolutionPriorityAttribute` polyfill under `#if !NET9_0_OR_GREATER` next to the existing `CollectionBuilderAttribute` polyfill; `TupleAccessors` after `module Operators`; XML docs with `<example id>` for the pair overloads. Surface-area baselines under `tests/FSharp.Core.UnitTests/FSharp.Core.SurfaceArea.*.bsl` updated; new `TupleAccessorsTests.fs` calling the members qualified.
- **Language feature** — `src/Compiler/Facilities/LanguageFeatures.fs(i)`: `StructTupleAccessors`, `previewVersion`; `FSComp.txt`: `featureStructTupleAccessors`.
- **Implicit open** — `src/Compiler/TypedTree/TcGlobals.fs`: a `mk_MFCore_tcref fslibCcu "TupleAccessors"` slot; `src/Compiler/Checking/CheckDeclarations.fs` `AddCcuToTcEnv`: after folding the assembly-level auto-opens of FSharp.Core, when the feature is on and the slot dereferences, `AddTypeContentsToNameEnv` for the type. `AddCcuToTcEnv` runs once per compilation and seeds every file's environment, so user code layers over it.
- **Tupling rule** — `src/Compiler/Checking/Expressions/CheckExpressions.fs` `TcMethodApplication_SplitSynArguments`: keep the single-candidate case; add the multi-candidate case described above (all candidates `[[simpleArg]]`, no named args, one curried group with two or more unnamed args, not a property), reusing the existing re-tupling of the syntactic arguments.
- **Tests** — `tests/FSharp.Compiler.ComponentTests/Conformance/TupleAccessors/`: the resolution table above as `eval`/`typecheck` cases, feature-off behaviour, user shadowing, quotation shape, and the tupling rule on a user-defined type with and without the feature.
- **Release notes** — `docs/release-notes/.Language/preview.md` and `docs/release-notes/.FSharp.Core/<next>.md`.
