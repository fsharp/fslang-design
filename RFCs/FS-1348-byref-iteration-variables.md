# F# RFC FS-1348 - `byref` and `inref` iteration variables in `for ... in` loops

The design suggestion [byref and inref for loops](https://github.com/fsharp/fslang-suggestions/issues/1453) is open and has not yet been marked "approved in principle". This RFC is written ahead of that decision so that the design can be reviewed in concrete form.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1453)
- [ ] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

**Related:** [FS-1053 Span and byref-like structs](https://github.com/fsharp/fslang-design/blob/main/FSharp-4.5/FS-1053-span.md) (F# 4.5) introduced `inref`, byref returns and their implicit dereference. This RFC builds on those rules and changes none of them.

# Summary

A `for pat in expr do body` loop may declare its iteration variable with a `byref` or `inref` type annotation:

```fsharp
let zero (span: Span<int>) =
    for item: int byref in span do
        item <- 0

let sum (span: ReadOnlySpan<int>) =
    let mutable acc = 0
    for item: int inref in span do
        acc <- acc + item
    acc
```

The variable is then bound to the **address** of each element instead of to a copy of it, and follows the existing rules for byref-typed locals. The form is accepted when the elements can be addressed without copying: one-dimensional arrays, `Span<'T>`, `ReadOnlySpan<'T>`, and any collection whose enumerator's `Current` property returns `byref<'T>` or `inref<'T>`. It is rejected everywhere else, and inside sequence, list, array and computation expressions.

This is the F# counterpart of C# 7.3's `foreach (ref V v in x)` and `foreach (ref readonly V v in x)`, extended to arrays. It is gated by a new language feature, `ByrefIterationVariables`, and needs no FSharp.Core change.

# Motivation

F# reads by-reference collections through the same `for ... in` loop as any other collection, but always by value. For `Span<'T>`, `ReadOnlySpan<'T>` and any enumerator with a byref-returning `Current`, the compiler takes the address the collection hands it and immediately dereferences it (the "implicitly dereference byref" step of FS-1053). The loop variable is a copy, so the two things these collections exist for are unavailable in a `for` loop:

- **Writing through the reference.** `for p in span do p.X <- 1` is rejected with FS0256 ("a value must be mutable"), and the workaround `let mutable q = p in q.X <- 1` compiles and silently mutates the copy.
- **Avoiding the copy.** Every iteration over a `Span<Big>` or `Big[]` copies a `Big`; for large structs that is the cost the span was meant to remove.

What users write today:

```fsharp
// 1. An index loop. Needs random access, so it does not apply to enumerator-only collections,
//    and it evaluates the indexer twice when reading and writing.
let zero (span: Span<int>) =
    for i = 0 to span.Length - 1 do
        span[i] <- 0

// 2. The enumerator protocol by hand. Applies everywhere but loses the `for` shape:
//    no pattern, no `for`/`in` debug points, an easy-to-forget `mutable` on the enumerator,
//    and the address-taking `&` that this RFC makes implicit.
let zero (span: Span<int>) =
    let mutable e = span.GetEnumerator()
    while e.MoveNext() do
        let item = &e.Current
        item <- 0
```

A library-level alternative does not exist: byref types cannot appear in function types, so there can be no `Span.iterByRef (fun (x: byref<int>) -> ...)`.

C# has had `foreach (ref var x in span)` since 7.3, and .NET APIs are increasingly designed around it (`Span<T>`, `ReadOnlySpan<T>`, `TensorSpan<T>`, entity-component libraries, and any hand-written struct enumerator with a `ref` `Current`). This RFC gives F# the same expressiveness with no new syntax: the type annotation already parses, and today fails with a type mismatch.

# Detailed design

## Syntax

There is no grammar change. The feature applies when the pattern of a `for ... in ... do` loop is a single identifier, or a wildcard, with a type annotation whose type is a byref type:

```fsharp
for item: int byref in span do ...
for item: byref<int> in span do ...
for item: _ byref in span do ...        // element type inferred
for (item: 'T inref) in span do ...     // parentheses are allowed
for _: int byref in span do ...         // accepted, the variable is unused
```

The annotation is required. Byref-ness cannot be inferred from use, and F# never infers a byref type for a binding without an annotation or an explicit `&`, so the loop variable of an unannotated `for` keeps its by-value meaning.

The arrow form `for pat in expr -> expr` is a sequence-expression form and is covered by the exclusion below.

## Sources that can be iterated by reference

The loop is accepted when `expr` is one of the following (an *addressable* source):

| Source | Element address | Variable may be |
|---|---|---|
| one-dimensional array `T[]` | `&arr[i]` | `byref`, `inref` |
| `System.Span<T>` | `&span[i]` (`Item` returns `byref<T>`) | `byref`, `inref` |
| `System.ReadOnlySpan<T>` | `&span[i]` (`Item` returns `inref<T>`) | `inref` only |
| collection-pattern type whose `Current` returns `byref<T>` | `&e.Current` | `byref`, `inref` |
| collection-pattern type whose `Current` returns `inref<T>` | `&e.Current` | `inref` only |

Everything else is rejected with a new diagnostic: `'T list`, `string`, `seq<'T>`, `IEnumerable`, multi-dimensional arrays, integer ranges `a .. b` and `a .. s .. b`, and any enumerator whose `Current` returns by value (for example `List<'T>`, `ImmutableArray<'T>`, `Dictionary<_, _>`).

"Collection-pattern type" is the type already accepted by enumerable extraction (an accessible zero-argument `GetEnumerator` whose result has `MoveNext: unit -> bool` and a `Current` property, including extension members), in the same order of resolution as today: the static type first, then `seq<'T>`, then `IEnumerable`. `Span<'T>` and `ReadOnlySpan<'T>` already satisfy it through their nested `Enumerator` types, whose `Current` returns `ref T` and `ref readonly T`; they are listed separately because the compiler iterates them by index rather than through the enumerator, and the result must not depend on that optimisation.

The rule for the variable's kind is: the variable may be less permissive than the source, never more. A writable source (`byref<T>` elements) can be bound to a `byref` or an `inref` variable; a read-only source (`inref<T>` elements) only to an `inref` variable. Binding a `byref<T>` address to an `inref<T>` variable is the existing implicit byref-to-inref conversion (`let r: int inref = &arr[0]` compiles today).

`outref` is never accepted as an iteration variable type.

## Element type

The element type named in the annotation must equal the source's element type. Byref types are invariant, so no subsumption is applied: `for x: obj byref in (names: string[])` is an error. `_` and type parameters are permitted, so the form works in generic code:

```fsharp
let inline clear (span: Span<'T>) =
    for item: 'T byref in span do
        item <- Unchecked.defaultof<'T>
```

## Semantics of the variable

The iteration variable is an ordinary byref-typed local, exactly as if it had been introduced by `let item = &<element>` at the top of the body. All rules of FS-1053 and of byref safety analysis apply unchanged:

- Using `item` as an expression reads the element. `item <- v` writes through the reference; on an `inref` variable it is rejected with FS3224 ("the byref pointer is readonly").
- `&item` denotes the reference itself, for passing to a `byref`/`inref` parameter or constructor (`ReadOnlySpan<int>(&item)` in the suggestion). Passing `item` without `&` passes the value, as for every byref local today.
- Calling a mutating struct member on a `byref` variable mutates the element in place. On an `inref` variable the existing defensive-copy rule applies.
- The variable cannot be captured by a closure (FS0406), cannot escape the loop body, cannot be stored in a field, ref cell, tuple or any other heap-allocated value, and cannot instantiate a type parameter (FS0412). Quotations reject it as they reject every byref local (FS0462).
- The reference is valid for the duration of one iteration of the body. For enumerator sources this matches C#: the reference returned by `Current` is used before the next `MoveNext`.

Nothing about the lifetime of the source changes. A `byref` into a `Span<'T>` is subject to the same escape rules as `let item = &span[i]` today.

## Not allowed in sequence, list, array and computation expressions

```fsharp
seq { for x: int byref in span do yield x }          // error
[ for x: int byref in arr -> x ]                       // error
[| for x: int byref in arr do yield x |]               // error
task { for x: int byref in arr do () }                 // error
builder { for x: int byref in arr do () }              // error (any CE `For`)
```

In those forms the loop elaborates to a call (`Seq.collect`, `Seq.iter`, or the builder's `For` method) whose lambda parameter is the loop variable. A byref cannot be a lambda parameter or a generic instantiation, so the form cannot be given a meaning. It is rejected with a dedicated diagnostic rather than with the type errors those calls would otherwise produce.

## Pattern restriction

Only the identifier and wildcard shapes above are accepted. Any other pattern with a byref annotation, such as `for (a, b): (int * int) byref in pairs`, is rejected with a dedicated diagnostic. Byrefs cannot be destructured today, and a byref-typed tuple or record element cannot exist, so no expressiveness is lost.

## Elaboration

Each case differs from today's elaboration in one line: the element binding takes an address instead of a value.

**Arrays.**

```fsharp
let arr = expr
for idx = 0 to arr.Length - 1 do
    let item = &arr[idx]          // byref<T>, or inref<T> for an inref variable
    body
```

**`Span<'T>` and `ReadOnlySpan<'T>`.** The compiler already iterates these by index and dereferences the address returned by `Item`; the address is now kept.

```fsharp
let span = expr
for idx = 0 to span.Length - 1 do
    let item = &span[idx]         // Span: byref<T>; ReadOnlySpan: inref<T>
    body
```

**Collection pattern.**

```fsharp
let inputSequence = expr
let mutable enumerator = inputSequence.GetEnumerator()
try
    while enumerator.MoveNext() do
        let item = &enumerator.Current    // no implicit dereference
        body
finally
    // Dispose, as today
```

Debug points are unchanged: the `for` keyword, `in`, and the body keep the positions they have for a by-value loop.

## Worked examples

```fsharp
open System

[<Struct>]
type Particle = { mutable X: float; mutable Y: float; mutable VX: float; mutable VY: float }

// In-place update over an array: no copy in, no copy out.
let step (particles: Particle[]) (dt: float) =
    for p: Particle byref in particles do
        p.X <- p.X + p.VX * dt
        p.Y <- p.Y + p.VY * dt

// Read-only traversal of large structs without copying them.
let kineticEnergy (particles: ReadOnlySpan<Particle>) =
    let mutable e = 0.0
    for p: Particle inref in particles do
        e <- e + 0.5 * (p.VX * p.VX + p.VY * p.VY)
    e

// Passing the reference on.
let normalise (v: float byref) = v <- v / 10.0

let normaliseAll (values: Span<float>) =
    for v: float byref in values do
        normalise &v

// A user-defined struct enumerator with a byref-returning Current.
[<Struct>]
type ChunkEnumerator<'T> =
    val mutable private items: 'T[]
    val mutable private index: int
    new(items) = { items = items; index = -1 }
    member e.MoveNext() = e.index <- e.index + 1; e.index < e.items.Length
    member e.Current: byref<'T> = &e.items[e.index]

type Chunk<'T>(items: 'T[]) =
    member _.GetEnumerator() = ChunkEnumerator<'T>(items)

let fill (chunk: Chunk<int>) value =
    for item: int byref in chunk do
        item <- value

// Nested loops: a read-only reference to each row, then byref cells of that row.
let scale (rows: Memory<Particle>[]) (k: float) =
    for row: Memory<Particle> inref in rows do
        for p: Particle byref in row.Span do
            p.X <- p.X * k
            p.Y <- p.Y * k
```

Rejected:

```fsharp
for x: int byref in (ro: ReadOnlySpan<int>) do ()     // error: read-only elements, use inref
for x: int byref in [ 1; 2; 3 ] do ()                  // error: list elements cannot be addressed
for x: int inref in (xs: List<int>) do ()              // error: Current returns by value
for x: int byref in 1 .. 10 do ()                      // error: range
for x: int outref in arr do ()                         // error: outref
for x: int byref in arr do async { return x } |> ignore   // FS0406: captured by a closure (existing)
```

## Language feature

The feature is `LanguageFeature.ByrefIterationVariables`, initially in `preview`. When the feature is off and the pattern carries a byref annotation, the compiler reports the standard "feature not available in this language version" error (FS3350) instead of today's FS0001 type mismatch. Both are errors, so no program changes meaning.

# Changes to the F# spec

**[Sequence Iteration Expressions](https://fsharp.github.io/fslang-spec/expressions/#sequence-iteration-expressions)** in `expressions.md`. After the paragraph that describes enumerable extraction ("The type of `pat` is the same as the return type of the Current property on the enumerator value ..."), add:

> If `pat` has the form `ident : ty` or `_ : ty`, optionally parenthesised, and `ty` is a byref type `byref<ty2>` or `inref<ty2>`, the expression is an *addressable sequence iteration*. `expr1` must then be one of: a single-dimensional array type `ty2[]`; `System.Span<ty2>`; `System.ReadOnlySpan<ty2>`; or a type that satisfies the collection pattern and whose `Current` property has return type `byref<ty2>` or `inref<ty2>`. If `ty` is `byref<ty2>`, the source must additionally yield writable references: an array, `System.Span<ty2>`, or a `Current` property of type `byref<ty2>`. `ty` may not be `outref<_>`.
>
> The elaboration is as above, except that `ident` is bound to the address of the current element rather than to its value: `&v.Current` for the collection pattern, `&arr.[i]` for an array, and `&span.[i]` for a span. `ident` is a byref-typed local value and is subject to [byref safety analysis](https://fsharp.github.io/fslang-spec/inference-procedures/#byref-safety-analysis). Where the element address has type `byref<ty2>` and `ty` is `inref<ty2>`, the binding applies the implicit conversion from `byref<ty2>` to `inref<ty2>`.
>
> An addressable sequence iteration may not appear as a `for` in a sequence expression, list or array expression, or computation expression, and its pattern may not have any other form. `expr1` may not be a range expression.

**[Byref Safety Analysis](https://fsharp.github.io/fslang-spec/inference-procedures/#byref-safety-analysis)** in `inference-procedures.md`. Add to the list of byref-typed local values: "the iteration variable of an addressable sequence iteration ([Sequence Iteration Expressions](https://fsharp.github.io/fslang-spec/expressions/#sequence-iteration-expressions))".

# Drawbacks

- **Annotation weight.** C# writes `ref var x`; F# has no `var`, so the shortest form is `x: _ byref`. An address-of pattern (`for &x in xs`) would be lighter; see Alternatives.
- **Three compiler paths.** Arrays, spans and the enumerator pattern each take the address differently, and the sequence/computation-expression checkers need a matching rejection. The change is contained in `TcForEachExpr` and the three `SynExpr.ForEach` handlers, but it is not a single-site change.
- **Beyond C# for arrays.** C# rejects `foreach (ref var x in array)` because its spec routes arrays through `IEnumerable<T>`. F# already lowers array loops to an index loop with `ldelem`, so the address is available at no cost, and the suggestion thread asked for it. The cost is one more difference from C# to document.
- **Covariance check on reference-type arrays.** `ldelema` on a `T[]` whose runtime element type may be more derived performs a type check per element. It applies only to `byref` variables over reference-type element arrays, which is the least useful combination (the only in-place operation is replacing the reference). For `inref` variables this RFC proposes emitting the `readonly.` prefix, which skips the check; today the compiler emits it for `&arr[i]` only when the element type is a type parameter.
- **Expectation gap.** Users may try `for x: int byref in someList` and meet an error. The diagnostic names the accepted sources.

# Alternatives

- **An address-of pattern, `for &item in span do`.** No annotation, and the element type stays inferred. It needs new pattern syntax, and `&` already has a pattern meaning (`pat & pat` conjunction) that would sit next to a prefix `&`. It can be layered on later as sugar for the annotated form without changing anything in this RFC, so it is deferred.
- **`for mutable item in span do`.** Reads as "a mutable copy", which is precisely the semantics to avoid.
- **Inferring byref from use** (for example from `item <- v` in the body). F# never infers byref types for bindings; doing so here would make the body change the meaning of the header.
- **A modifier in the enumerable position, `for item in &span do`.** Puts the modifier on the wrong side: it is the variable, not the collection, that changes.
- **Library functions.** Impossible, as byref types cannot appear in function types (`byref<int> -> unit` is rejected), and `[<InlineIfLambda>]` does not lift that restriction.
- **Do nothing.** Users keep writing the enumerator protocol by hand.

# Prior art

- **C# 7.3** (`IDS_FeatureRefForEach` in Roslyn): `foreach (ref V v in x)` and `foreach (ref readonly V v in x)`. The C# specification (§13.9.5) defines the form as equivalent to `ref V v = ref e.Current;` and requires `Current` to be a ref return. Measured with the current Roslyn:

  | C# | Result |
  |---|---|
  | `foreach (ref int x in span)` | accepted |
  | `foreach (ref readonly int x in span)` | accepted |
  | `foreach (ref readonly int x in readOnlySpan)` | accepted |
  | `foreach (ref int x in readOnlySpan)` | CS8331 (readonly variable) |
  | `foreach (ref int x in array)` | CS1510 |
  | `foreach (ref readonly int x in array)` | CS1510 |
  | `foreach (ref int x in list)` | CS1510 |

  This RFC matches every row except the two array rows, which it accepts.
- **Rust**: `for x in slice.iter_mut()` and `for x in &mut vec` bind `&mut T`; `for x in &vec` binds `&T`. The by-reference form is the default idiom and the by-value form the exception.
- **C++**: range-`for` with `auto&` and `const auto&`.
- **F# itself**: FS-1053 introduced `inref`, byref returns and the rule that a byref return is dereferenced unless the caller writes `&`. This RFC applies the same rule to the element binding of a loop, with the annotation playing the role of `&`.

# Compatibility

- **Not a breaking change.** Every program that uses the new form fails today with FS0001 ("expected `int` but here has type `byref<int>`"). No program that compiles today changes meaning; the unannotated loop is untouched.
- **Older compilers, new source.** FS0001, as today.
- **Older compilers, new binaries.** Nothing new appears in metadata. The generated IL uses `ldelema`, `get_Item`/`get_Current` calls and byref locals, all of which every consumer already understands.
- **FSharp.Core.** No change.
- **Language version below the feature.** FS3350, see above.

# Interop

- **Consumed from other languages.** No surface change; the feature affects method bodies only.
- **C#.** Same kinds (`ref` ≙ `byref`, `ref readonly` ≙ `inref`), same rule that the variable may not be more permissive than the source, same set of enumerator sources, plus arrays. Any C# collection written for `foreach (ref ...)` becomes iterable by reference from F#, including `Span<T>`, `ReadOnlySpan<T>`, `TensorSpan<T>` and hand-written struct enumerators.

# Pragmatics

## Diagnostics

New errors (numbers to be assigned at implementation time):

| Condition | Message (draft) |
|---|---|
| Source is not addressable (list, string, seq, range, by-value `Current`, ...) | The iteration variable '%s' has the byref type '%s', but the elements of '%s' cannot be addressed. byref and inref iteration variables are only supported for arrays, `Span<'T>`, `ReadOnlySpan<'T>` and collections whose enumerator's 'Current' property returns a byref. |
| `byref` variable over read-only elements (`ReadOnlySpan<'T>`, `inref` `Current`) | The iteration variable '%s' is declared as 'byref', but the elements of '%s' are read-only. Declare it as 'inref' instead. |
| `outref` annotation | 'outref' cannot be used as the type of an iteration variable. |
| Byref annotation in a sequence, list, array or computation expression | byref and inref iteration variables cannot be used in sequence expressions, list or array expressions, or computation expressions. |
| Byref annotation on any other pattern shape | A byref or inref iteration variable must be a single identifier or wildcard with a type annotation. |
| Feature off | FS3350, the standard feature-gating error. |

Existing diagnostics that continue to apply to the variable: FS0406 (captured by a closure), FS0412 (byref in a type instantiation), FS0462 (byref in a quotation), FS3224 (write through `inref`), FS3228 and the other escape checks, FS0001 for an element-type mismatch in the annotation.

## Tooling

- **Tooltips and completion** show `val item: byref<int>` / `val item: inref<int>`, as for any byref local. No new tooling code.
- **Go to definition, find references, rename** treat the variable as any other loop variable.
- **Debugging.** Byref locals are already displayed by the debugger through the referenced value; breakpoints and stepping use the same debug points as a by-value loop.
- **Error recovery.** The annotation is already parsed; a wrong or incomplete source expression recovers exactly as today.
- **Colorization, brace matching.** Unaffected.

## Performance

- **Compilation.** One additional check of the pattern's annotation per `for ... in` loop. Negligible.
- **Generated code, existing loops.** Unchanged.
- **Generated code, new loops.** Removes the per-element copy: arrays go from `ldelem` (a `ldobj`-sized copy for structs) to `ldelema`; spans skip the dereference of the `Item` result; enumerators skip the dereference of `Current`. This is the same code the existing `let item = &arr[i]` / `&span[i]` / `&e.Current` produce, with one proposed addition: for an `inref` variable over an array the `ldelema` carries the `readonly.` prefix, which avoids the array-covariance check on reference-type elements. The compiler already uses that prefix for `&arr[i]` when the element type is a type parameter, so no new code-generation path is needed.

## Scaling

No new dimension. Loops nest as before, and the feature adds one binding per loop.

## Culture-aware formatting/parsing

Not applicable.

# Unresolved questions

- **Arrays.** Whether to accept arrays (this RFC: yes, as requested in the suggestion thread) or to match C# exactly.
- **`inref` over by-value sources.** Whether `for x: T inref in list` should be accepted as "a read-only reference to the loop's own copy". This RFC says no, to keep the promise that `inref` means no copy was made, and to match C#.
- **Multi-dimensional arrays.** `T[,]` is enumerated through `IEnumerable` today (elements boxed to `obj`) and is excluded. Support through the array `Address` method could be added later.
- **Address-of pattern.** Whether `for &item in xs do` should follow as sugar for the annotated form.
- **Feature name.** `ByrefIterationVariables` is a placeholder.

# Appendix: implementation sketch (dotnet/fsharp)

For reviewers who want to see the compiler-side footprint; file references are to `dotnet/fsharp` `main` at the time of writing.

- **Language feature** — `src/Compiler/Facilities/LanguageFeatures.fs(i)`: `ByrefIterationVariables`, `previewVersion`; `FSComp.txt`: `featureByrefIterationVariables` and the five new errors above.
- **Detecting the annotation** — `src/Compiler/Checking/Expressions/CheckExpressions.fs`, `TcForEachExpr`: before choosing the iteration technique, inspect `synPat`. If it is `SynPat.Typed(SynPat.Named | SynPat.Wild, synTy, _)` (optionally inside `SynPat.Paren`) and `synTy` checks to a byref type, record the requested kind (`byref`/`inref`/`outref`) and the requested element type (`destByrefTy`), check the feature, and reject `outref`. If the annotated type is a byref but the pattern has any other shape, report the pattern error.
- **Arrays** — the `isArray1DTy` branch: replace `mkLdelem` with `mkArrayElemAddress g (readonly, (if readonly then ILReadonly.ReadonlyAddress else ILReadonly.NormalAddress), false, ILArrayShape.SingleDimensional, elemTy, [arrExpr; idxExpr], mIn)` and type the element variable as `mkByrefTyWithFlag g readonly elemTy`. This is the call `mkExprAddrOfExprAux` makes for `&arr[i]`, except that `mkExprAddrOfExprAux` selects `ReadonlyAddress` only for type-parameter element types (`useReadonlyForGenericArrayAddress`), because it cannot know how the address will be used; here the `inref` annotation makes it known.
- **Spans** — the `tryGetOptimizeSpanMethods` branch: bind the element variable to the `get_Item` call directly instead of to `mkAddrGet` of the `addr` temporary. Reject a `byref` request when `isReadOnlySpan`. When an `inref` variable is requested over `Span<'T>`, apply the byref-to-inref coercion the checker uses for `let r: inref<_> = &e`.
- **Collection pattern** — `AnalyzeArbitraryExprAsEnumerable`: add a parameter (or a second return value) that keeps `currentExpr` undereferenced and reports the raw `Current` type. In `TcForEachExpr`, when a byref variable is requested: if the raw type is not a byref, report "not addressable"; if it is `inref` (`isInByrefTy`) and `byref` was requested, report "read-only"; otherwise bind the variable to the undereferenced `currentExpr`.
- **Ranges** — the `range_op_vref` branch: report "not addressable" when a byref variable is requested.
- **Pattern binding** — `TcMatchPattern` is called with the byref-typed element type; the existing `TPat_as(_, PatternValBinding(v, _), _)` shortcut binds `v` directly, and `CompilePatternForMatch` on the remaining wildcard must not introduce a byref temporary. Verify with the wildcard form `for _: int byref in arr`.
- **Sequence and computation expressions** — `CheckSequenceExpressions.fs` (line ~45), `CheckComputationExpressions.fs` (`SynExpr.ForEach` at ~638 and ~1406), and the computed-collection path: when the pattern carries a byref annotation, report the dedicated error before translating the loop.
- **Byref checks** — `PostInferenceChecks.fs` should need no change: the variable is a `let`-bound byref local inside a loop, a shape it already accepts (including under the `try/finally` that wraps the enumerator loop). Confirm with tests that an `inref` variable rejects writes and a closure capture is rejected.
- **Tests** — `tests/FSharp.Compiler.ComponentTests/Conformance/Expressions/ControlFlowExpressions/SequenceIteration/`: each row of the source table as `typecheck`/`eval` cases (arrays of structs, `Span`, `ReadOnlySpan`, custom struct enumerator with `byref` and with `inref` `Current`, generic `inline` use, `&item` passed on, nested loops); every rejected form above; feature-off behaviour; IL baselines showing `ldelema` with and without `readonly.` and the absence of the `ldobj` copy.
- **Release notes** — `docs/release-notes/.Language/preview.md`.
