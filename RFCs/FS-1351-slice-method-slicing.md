# F# RFC FS-1351 - Slicing syntax via `System.Range` indexers and `Slice` methods

The design suggestion [Allow slice syntax to use instance `Slice` method instead of requiring `GetSlice` method](https://github.com/fsharp/fslang-suggestions/issues/1317) has been marked "approved in principle".

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1317)
- [x] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/17377) (draft; predates this RFC)
- [x] [Discussion](https://github.com/fsharp/fslang-design/pull/849)

Related: [FS-1077 Tolerant slicing](../FSharp-5.0/FS-1077-tolerant-slicing.md), [FS-1076 revision](https://github.com/fsharp/fslang-design/pull/851) (from-end bounds).

# Summary

Slicing syntax `e[a..b]` uses the .NET slicing protocols in C# order: an indexer that takes `System.Range`, then `Slice(int, int)` on a type with an `int` `Length` or `Count`, then the existing `GetSlice`. F# slices stay end-inclusive and tolerant. FSharp.Core deprecates `List<'T>.GetSlice`.

```fsharp
let s = "abc123".AsSpan()[1..3]   // "bc1": Slice + Length
let sub = matrix[0..1, *]         // a type with Item(Range, Range)
let l = [ 1..10 ][1..3]           // [2; 3; 4]: FSharp.Core extension Item(Range)
```

# Motivation

`Span`, `ReadOnlySpan`, `Memory`, `ArraySegment`, `List<T>` and `ImmutableArray` expose `Slice(int, int)` with `Length` or `Count`, and C# 8 slices them with `x[a..b]`. F# slices them only after the user writes a `GetSlice` extension for each type. The [Learn example](https://learn.microsoft.com/dotnet/fsharp/language-reference/slices) of such an extension is end-exclusive (`sp.Slice(s, e - s)`), so it disagrees with all other F# slices. Types that need more than two integers, such as matrices and tensors, use a `Range` indexer, which F# slicing syntax cannot reach. `List<'T>.GetSlice` exposes F# `option` parameters to C#.

# Detailed design

## Resolution

The rules apply under the language feature `SliceMethodSlicing` (preview) to a slice expression whose receiver has a nominal type other than an array or `string`, and whose bounds contain no from-end `^`. Arrays, strings and from-end bounds keep their current elaboration. The first protocol that the receiver type satisfies applies:

| # | Protocol | Requirement | Scope |
|---|---|---|---|
| 1 | `Range` indexer | accessible `Item` (or `DefaultMember`) indexer, intrinsic or extension, with one parameter per argument and type `System.Range` at each range position | any rank, get and set |
| 2 | `Slice` | accessible instance `Slice`, intrinsic or extension, with exactly two `int` parameters (units of measure erased) and no optional, `ParamArray` or byref parameter; and an accessible intrinsic `int` property `Length`, else `Count` | one range argument, get only |
| 3 | `GetSlice` | unchanged | all other cases |

The compiler selects the protocol from the receiver type before it checks the bounds. It then checks each bound against the parameter type (`int`, with the units of measure of `Slice` if any). If more than one `Slice` overload qualifies, it reports an error. An assignment `e[...] <- v` uses the setter of protocol 1 if it exists, else `SetSlice`.

## Bounds

The compiler evaluates the receiver `r`, then `a`, then `b`, then `len = r.Length` (protocol 2 only), each once. Then it calls the member:

```fsharp
// Protocol 1: one Range per range argument; `*` is Range.All
start = if a is absent then Index.Start else Index(max a 0)
end   = if b is absent then Index.End   else Index(if b < 0 then 0 elif b = Int32.MaxValue then b else b + 1)
r.Item(..., Range(start, end), ...)

// Protocol 2
s = if a is absent then 0   else min (max a 0) len
x = if b is absent then len elif b < s then s elif b >= len then len else b + 1
r.Slice(s, x - s)
```

No step can overflow. Protocol 2 selects the same elements as array slicing for all bounds, and it returns an empty result for `[3..Int32.MinValue]`, where array slicing throws today ([dotnet/fsharp#20530](https://github.com/dotnet/fsharp/issues/20530)). Protocol 1 clamps only the negative offsets that `Index` cannot hold; the indexer handles all other out-of-range bounds. A direct call keeps the member's own checks: `span.Slice(5, -2)` throws, but `span[5..2]` is empty.

## FSharp.Core

- An `[<AutoOpen>]` module adds the extension `member Item: range: Range -> 'T list with get` to `List<'T>` for the target frameworks that define `System.Range` (netstandard2.1 and net). It returns the same elements as `GetSlice` for all bounds and reads `Length` only for from-end `Index` values. List slicing then uses protocol 1. On netstandard2.0 it uses `GetSlice`.
- The member is an extension, not an intrinsic overload. Beside the intrinsic `Item: int -> 'T`, an intrinsic overload makes `let get (xs: 'T list) i = xs[i]` fail with FS0041 on every compiler; `[<OverloadResolutionPriority>]` prevents that only under `--langversion:preview`. With an extension, `i` is still inferred as `int`.
- `List<'T>.GetSlice` stays for binary compatibility. It gets `[<EditorBrowsable(EditorBrowsableState.Never)>]`, a documentation remark and, under the feature, info FS3918 on explicit use. Compiler-generated calls do not report FS3918. `[<Obsolete>]` is not used, because FS0044 fails builds that use `TreatWarningsAsErrors`.

## Interactions

- **Quotations** show the elaboration: `PropertyGet(Item, [Range])`, or `Let` bindings with `PropertyGet(Length)` and `Call(Slice)`. A quoted list slice changes from `Call(GetSlice)` to `PropertyGet(Item)`; quotation translators such as Fable need the new shape.
- **Byref-like receivers** such as `Span<'T>` are held in a local, as any byref-like value is. They cannot be quoted.
- **Type providers**: provided types use the same protocols.
- **SRTP**: an `inline` `GetSlice` extension over `Slice` and `Length`, the workaround in #1317, is no longer reached for types that have `Slice`.
- **C#** ignores extension members, so C# cannot slice F# lists. A type with `Slice` or a `Range` indexer slices the same elements in both languages, apart from F#'s inclusive end.
- **Tooling**: the checker records the selected member at the `..` range for hover and go-to-definition. An explicit `span.Slice(1, 2)` call stays classified as a method.

# Changes to the F# spec

- §6.4.7 Slice Expressions: before the `GetSlice` translation, add the rules of [Resolution](#resolution) and [Bounds](#bounds).
- §6.4.6 Lookup Expressions: no change.

# Drawbacks

- Three protocols with a precedence order.
- A type that has both `GetSlice` and `Slice` or a `Range` indexer changes behaviour when the feature is on (see [Compatibility](#compatibility)).
- `span[5..2]` is empty but `span.Slice(5, -2)` throws. Arrays have the same split today.

# Alternatives

- **`GetSlice` before `Slice`**, as in the draft implementation: no behaviour change, but a stale or wrong `GetSlice` extension hides the type's own `Slice` permanently.
- **Strict protocol 2** (`r.Slice(a, b - a + 1)`, no `Length` read): the same exceptions as C#, but different results from F# arrays and lists.
- **Intrinsic list members** break inference, as shown above. A compiler intrinsic for list slicing instead gives no member for `Range` values.
- **`[<Obsolete>]` on `GetSlice`** breaks `TreatWarningsAsErrors` builds.

# Prior art

- C# 8 [ranges](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-8.0/ranges.md): the same precedence and countable rule. C# ends are exclusive, bounds are strict, and extension members are ignored.
- FS-1077 defines the tolerant semantics that protocol 2 keeps.

# Compatibility

With the feature off, nothing changes. With the feature on, a program changes only if a receiver satisfies protocol 1 or 2 and also has a `GetSlice` in scope:

| Existing code | New behaviour |
|---|---|
| end-exclusive `GetSlice` extension from Learn | `Slice` is used; `sp[0..3]` has 4 elements, not 3 |
| `GetSlice` that returns another type than `Slice` | the `Slice` result type is used |
| `GetSlice` with non-`int` bounds beside `Slice(int, int)` | non-`int` bounds are type errors |
| `GetSlice` beside a `Range` indexer | the indexer is used |

The fix is to delete the redundant extension or to call it explicitly. These changes become breaking when the feature is promoted.

Older compilers use `GetSlice` as today, and the new FSharp.Core extension does not change their inference. A new compiler with an older FSharp.Core, or on netstandard2.0, slices lists with `GetSlice`. Generated code calls only `get_Item`, `Slice`, `get_Length` or `get_Count`, and the `Range` and `Index` constructors.

# Interop

See [Interactions](#interactions). Tensor types use `NRange` and `params ReadOnlySpan<NRange>`; they need suggestion [#1377](https://github.com/fsharp/fslang-suggestions/issues/1377).

# Pragmatics

## Diagnostics

| Number | Kind | Condition |
|---|---|---|
| FS3916 | error | no protocol applies; the message names all three (replaces FS0039 for slices) |
| FS3917 | error | more than one `Slice(int, int)` overload qualifies |
| FS3918 | info | explicit use of `List<'T>.GetSlice` |

With the feature off, FS3350 replaces FS0039 where protocol 1 or 2 would apply. The numbers are provisional.

## Tooling

See [Interactions](#interactions).

## Performance

Protocol 1 builds one `Range` struct. Protocol 2 reads `Length` once and does at most four comparisons. Both avoid the two `option` allocations of `GetSlice`. List slicing keeps its cost.

## Scaling

Not applicable.

## Culture-aware formatting/parsing

Not applicable.

# Unresolved questions

- From-end bounds on protocols 1 and 2: the FS-1076 revision ([#851](https://github.com/fsharp/fslang-design/pull/851)).
- Extension `Length` and `Count` (C# requires intrinsic properties).
- When to promote FS3918 to a warning.
