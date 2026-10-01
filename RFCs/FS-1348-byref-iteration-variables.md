# F# RFC FS-1348 - `byref` and `inref` iteration variables in `for ... in` loops

The design suggestion [byref and inref for loops](https://github.com/fsharp/fslang-suggestions/issues/1453) has not been marked "approved in principle".

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1453)
- [ ] Approved in principle
- [ ] Implementation (not started)
- [x] [Discussion](https://github.com/fsharp/fslang-design/pull/845)

Related: [FS-1053 Span and byref-like structs](../FSharp-4.5/FS-1053-span.md).

# Summary

A `for ... in` loop variable with a `byref` or `inref` type annotation is bound to the address of each element, not to a copy. Sources are one-dimensional arrays, spans and enumerators whose `Current` returns a byref. The feature is `ByrefIterationVariables` (preview).

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

# Motivation

Today the loop variable is always a copy: the compiler dereferences the address that `Span<'T>.Item` or a byref `Current` returns. So `for p in span do p.X <- 1` fails with FS0256, `let mutable q = p` mutates only the copy, and each iteration over `Span<Big>` or `Big[]` copies a `Big`. The workarounds are an index loop, which needs random access, or the enumerator protocol by hand with `let item = &e.Current`. A library function cannot take an F# function over byrefs, even with `[<InlineIfLambda>]`. It can take only a delegate with a byref parameter, which costs an indirect call per element and cannot capture spans.

# Detailed design

## Form

The rules apply when the pattern of `for pat in expr do body` is `ident: ty` or `_: ty`, optionally in parentheses, and `ty` is a byref type (a *byref annotation*):

```fsharp
for item: _ byref in span do ...     // element type inferred
for (item: 'T inref) in span do ...
```

- F# infers a byref type for a binding only from an annotation or `&`, so an unannotated loop keeps its meaning.
- `outref<T>` is an error.
- Any other pattern with a byref annotation, such as `for (a, b): (int * int) byref in pairs`, is an error. Byrefs cannot be destructured.
- `T` must equal the element type of the source. Byref types are invariant, so `for x: obj byref in (names: string[])` is an error.

## Sources

| Source | `byref` | `inref` |
|---|---|---|
| one-dimensional array `T[]` | yes | yes |
| `Span<T>` | yes | yes |
| `ReadOnlySpan<T>` | no | yes |
| collection pattern, `Current: byref<T>` | yes | yes |
| collection pattern, `Current: inref<T>` | no | yes |

An `inref` variable over a writable source uses the existing implicit byref-to-inref conversion. The collection pattern is resolved as enumerable extraction resolves it today, with intrinsic or extension members. All other sources are errors, including multi-dimensional arrays, ranges and enumerators whose `Current` returns a value (lists, `string`, `seq<'T>`, `List<'T>`).

```fsharp
[<Struct>]
type RefEnumerator<'T> =
    val Items: 'T[]
    val mutable I: int
    new(items) = { Items = items; I = -1 }
    member e.MoveNext() = e.I <- e.I + 1; e.I < e.Items.Length
    member e.Current: byref<'T> = &e.Items[e.I]

type RefList<'T>(items: 'T[]) =
    member _.GetEnumerator() = RefEnumerator<'T>(items)

for x: int byref in RefList [| 1; 2 |] do x <- 0
```

## The variable

The variable is a byref-typed local, as if `let item = &<element>` were the first line of the body; the FS-1053 rules and byref safety analysis apply unchanged. `item` reads or writes the element. `&item` passes the reference to a `byref` or `inref` parameter, for example `ReadOnlySpan<int>(&item)`. A mutating struct member on a `byref` variable mutates the element in place; on an `inref` variable the existing defensive copy applies. The reference can outlive the iteration only where the existing rules let `let item = &<element>` escape. The compiler does not check that an enumerator's reference is used before the next `MoveNext`; as in C#, that is the enumerator's contract.

```fsharp
[<Struct>]
type Particle = { mutable X: float; mutable VX: float }

let step (ps: Particle[]) dt =
    for p: Particle byref in ps do
        p.X <- p.X + p.VX * dt
```

## Elaboration

Only the element binding changes: it takes the address instead of the value.

```fsharp
// arrays, Span, ReadOnlySpan
let s = expr
for i = 0 to s.Length - 1 do
    let item = &s[i]
    body

// collection pattern
let mutable e = expr.GetEnumerator()
try
    while e.MoveNext() do
        let item = &e.Current
        body
finally
    // Dispose, as today
```

For an `inref` variable over an array, `ldelema` gets the `readonly.` prefix, so it skips the array type check and does not throw on a covariant array. Today the compiler emits that prefix only when the element type is a type parameter.

## Sequence, list, array and computation expressions

A byref annotation on a `for` in these forms is an error:

```fsharp
[ for x: int byref in arr -> x ]        // error
task { for x: int byref in arr do () }  // error
```

These forms elaborate the loop variable to a lambda parameter (`Seq.collect`, a builder's `For`), and a byref cannot be one.

## Interactions

- **SRTP**: a trait call with the variable as receiver acts on the element, as on any byref local.
- **Quotations**: a quoted loop with a byref annotation is an error (FS0462), as for any byref local.
- **Type providers**: a provided type is checked as any other source type.

# Changes to the F# spec

- [§6.5.6 Sequence Iteration Expressions](https://fsharp.github.io/fslang-spec/expressions/#656-sequence-iteration-expressions): add the rules of [Detailed design](#detailed-design) as the *addressable sequence iteration*.
- [§14.9 Byref Safety Analysis](https://fsharp.github.io/fslang-spec/inference-procedures/#149-byref-safety-analysis): state that the iteration variable of an addressable sequence iteration is a byref-typed local.

# Drawbacks

- The shortest form, `x: _ byref`, is longer than C#'s `ref var x`.
- A `byref` variable over an array of a reference type checks the array type on each element. Over a `string[]` typed as `obj[]` it throws `ArrayTypeMismatchException`, where the by-value loop does not.
- Users can expect `for x: int byref in someList` to work.

# Alternatives

- **Address-of pattern `for &item in xs`**: no annotation, but new pattern syntax next to the `pat & pat` conjunction.
- **`for mutable item in xs`**: reads as a mutable copy.
- **Infer byref from use in the body**: the body would change the meaning of the header.
- **`for item in &span`**: the modifier belongs to the variable, not the source.

# Prior art

- **C#**: `foreach (ref V v in x)` and `foreach (ref readonly V v in x)` since C# 7.3, defined as `ref V v = ref e.Current;` (C# spec §13.9.5). `ref` over `ReadOnlySpan<T>` is CS8331, and `ref` or `ref readonly` over an array or a by-value `Current` is CS1510. This RFC matches these source and reference-kind rules except that it accepts arrays. C# 13 also accepts the form in async methods and iterators where the reference does not cross an `await` or `yield`; this RFC rejects it in all sequence and computation expressions.
- **Rust** (`for x in slice.iter_mut()`, `for x in &v`) and **C++** (`for (auto& x : xs)`, `const auto&`) iterate by reference.

# Compatibility

- Not a breaking change. Every use of the form is an error today (FS0001, or FS0412 when the source's element type is not yet inferred), and older compilers keep reporting it.
- Binaries contain nothing new: `ldelema`, `get_Item`, `get_Current` and byref locals.
- FSharp.Core does not change.

# Interop

The feature affects method bodies only. C# collections designed for `foreach (ref ...)`, such as `TensorSpan<T>` and struct enumerators with a `ref` `Current`, become iterable by reference.

# Pragmatics

## Diagnostics

| Number | Condition |
|---|---|
| new | the source is not addressable; the message names the accepted sources |
| new | `byref` over read-only elements; the message suggests `inref` |
| new | `outref` annotation |
| new | byref annotation in a sequence, list, array or computation expression |
| new | byref annotation on a pattern other than `ident` or `_` |
| FS3350 | the feature is off (replaces today's error) |
| FS0001 | the element type in the annotation differs from the source's |
| FS0257, FS0406, FS0412, FS0462, FS3209, FS3224, FS3228 and the other byref checks | unchanged |

All are errors.

## Tooling

Hover shows `val item: byref<int>`, as for any byref local. Debug points, navigation, rename, debugger display and error recovery do not change.

## Performance

New loops remove the per-element copy: arrays use `ldelema` instead of `ldelem`, and spans and enumerators skip the dereference.

## Scaling

Not applicable.

## Culture-aware formatting/parsing

Not applicable.

# Unresolved questions

- Accept arrays (this RFC, as requested in #1453; F# already lowers array loops to an index loop, so the address costs nothing) or match C#.
- `inref` over a by-value source as a reference to the loop's copy. This RFC rejects it, so that `inref` always means no copy, and to match C#.
- Multi-dimensional arrays through the array `Address` method.
- The address-of pattern as later sugar for this form.
- The feature name `ByrefIterationVariables` is a placeholder.
