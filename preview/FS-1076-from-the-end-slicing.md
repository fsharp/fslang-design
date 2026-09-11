# F# RFC FS-1076 - From the end slicing and indexing for collections

> **Revision, September 2026.** This text replaces the 2019 design, which is in preview since F# 5 (feature `FromEndSlicing`). The 2019 text is in the file history (commit 96ad025).

The design suggestion [Allow negative indices in indexing and slicing like python](https://github.com/fsharp/fslang-suggestions/issues/358) has been marked "approved in principle". The range and index expressions below also cover suggestion [#1044](https://github.com/fsharp/fslang-suggestions/issues/1044), which is not approved.

- [x] [Suggestion #358](https://github.com/fsharp/fslang-suggestions/issues/358), approved in principle
- [ ] [Suggestion #1044](https://github.com/fsharp/fslang-suggestions/issues/1044), approval pending
- [x] [Implementation of the 2019 design](https://github.com/dotnet/fsharp/pull/7781) (preview); this revision needs new work
- [x] Discussion: [#472](https://github.com/fsharp/fslang-design/discussions/472), [#851](https://github.com/fsharp/fslang-design/pull/851)

Related: [FS-1351](https://github.com/fsharp/fslang-design/pull/849) (slicing protocols), [FS-1077](../FSharp-5.0/FS-1077-tolerant-slicing.md), [FS-1093](../FSharp-6.0/FS-1093-additional-conversions.md).

# Summary

`^e` denotes `System.Index(e, fromEnd = true)`, as in C#: `^1` is the last element. From-end positions work with `Index` indexers, countable types and the slicing protocols of FS-1351. Outside indexers, `a..b` and `^e` are values of type `System.Range` and `System.Index`.

```fsharp
let xs = [ 1..5 ]
xs[^1]                  // 5
xs[..^1]                // [1; 2; 3; 4]
(ResizeArray xs)[^1]    // 5: Count + Item(int)
let r = 1..^1           // System.Range
xs.Take(r)              // [2; 3; 4]: Enumerable.Take(source, Range)
```

# Motivation

The 2019 design predates `System.Index`. Its `^0` is the last element, so an F# `^1` is one element away from a C# `^1`. It needs a custom `GetReverseIndex` member on each type, so `ResizeArray[^1]` fails ([dotnet/fsharp#9425](https://github.com/dotnet/fsharp/issues/9425)), and it cannot pass `Index` or `Range` values to .NET APIs. It evaluates the receiver of `f()[^1]` twice ([dotnet/fsharp#12071](https://github.com/dotnet/fsharp/issues/12071), still reproduces). In [#472](https://github.com/fsharp/fslang-design/discussions/472) dsyme proposed that "`^e` *always* becomes `Index(e, true)`". This revision adopts that rule.

# Detailed design

## Meaning of `^e`

`^e` (`e : int`) is the position `len - e` of a receiver with length `len`. As an index or a slice start it is inclusive. As a slice end it is exclusive. An integer end `b` stays inclusive. Each F# form selects the same elements as its C# equivalent:

| F# | C# |
|---|---|
| `xs[^1]` | `xs[^1]` |
| `xs[a..b]` | `xs[a..(b + 1)]` |
| `xs[a..^b]` | `xs[a..^b]` |
| `xs[^a..]` | `xs[^a..]` |
| `xs[..^b]` | `xs[..^b]` |
| `xs[^a..^b]` | `xs[^a..^b]` |
| `xs[*]` | `xs[..]` |

Changes from the 2019 preview, on `xs = [1; 2; 3; 4; 5]`:

| Form | 2019 | Revision |
|---|---|---|
| `xs[^1]` | `4` | `5` |
| `xs[^0]` | `5` | out of range |
| `xs[^1..]` | `[4; 5]` | `[5]` |
| `xs[^2..^1]` | `[3; 4]` | `[4]` |
| `xs[..^1]` | `[1; 2; 3; 4]` | unchanged |

## Indexing

Under `FromEndSlicing`, `e[..., ^k, ...]` uses the first rule that applies, for get and for set:

| # | Receiver | `^k` at dimension `d` becomes |
|---|---|---|
| 0 | array of rank 1 to 4, or `string` | `r.GetLength(d) - k` |
| 1 | `Item` indexer, intrinsic or extension, with a `System.Index` parameter at that position | `Index(k, true)` |
| 2 | countable (intrinsic `int` `Length`, else `Count`) with an `int` indexer parameter | `r.Length - k` |
| 3 | member `GetReverseIndex: rank: int * offset: int -> int` | `r.GetReverseIndex(d, k)` |

The receiver `r` is evaluated once. `GetReverseIndex` now returns the offset of `^offset`, which is `length - offset`; the 2019 contract returned `length - offset - 1`. Out-of-range positions, including `xs[^0]`, are errors of the indexer.

## Slicing

From-end bounds are allowed in all FS-1351 protocols and in array and string slicing. For a `Range` indexer, `^k` becomes `Index(k, true)`. Elsewhere, `^k` becomes the start `len - k` or the inclusive end `len - k - 1`, and the integer rules of FS-1351 (FS-1077 for arrays and strings) then apply. `len` is `Length`, `Count` or `GetLength(d)`; a type without them uses `GetReverseIndex(d, k)` for `len - k`. A negative `k` throws `ArgumentOutOfRangeException` in every protocol, as the `Index` constructor does.

## Range and index expressions

Under the new feature `RangeIndexExpressions` (preview), a range `a..b`, `a..` or `..b` that is not an indexer argument, not in a list, array or sequence expression and not the source of a `for` loop has type `System.Range`. `^e` in the same positions has type `System.Index`. The bounds map as in FS-1351 protocol 1, but without clamping: `a` becomes `Index a`, `b` becomes `Index(b + 1)` saturated at `Int32.MaxValue`, `^k` becomes `Index(k, true)`, and an absent bound becomes `Index.Start` or `Index.End`. A negative bound throws in the `Index` constructor.

- If the expected type is known and is `seq<'T>`, a range is the sequence `seq { a..b }`. Any other known type except `Range` and `Index` gives the current error.
- A step range `a..s..b` is an error, as today.
- `^T.Member` with a dotted operand keeps its diagnostic FS3534 and its SRTP recovery.
- `System.Range` and `System.Index` must exist in the target framework.

```fsharp
let tail = ^5..
"hello world".AsSpan(tail)       // "world": a Range argument
[ 1..10 ].ElementAt(^1)          // 10: an Index argument
let ys : seq<int> = 1..3         // the sequence 1, 2, 3
```

## FSharp.Core

- An extension `member Item: index: Index -> 'T with get` on `List<'T>`, beside the FS-1351 extension `Item(range: Range)`, for netstandard2.1 and net.
- Both are extension members. Beside the intrinsic `Item: int -> 'T`, an intrinsic overload makes `let get (xs: 'T list) i = xs[i]` fail with FS0041 on every compiler; `[<OverloadResolutionPriority>]` prevents that only under `--langversion:preview`. With extensions, `i` is still inferred as `int`, and `xs[n]` with `n : int` still calls `Item(int)`.
- `GetReverseIndex` on lists, arrays and strings (all `[<Experimental>]`) gets the new contract and `[<EditorBrowsable(EditorBrowsableState.Never)>]`. The compiler no longer calls it for these types.

## Interactions

- **Quotations** show the elaboration, for example `Let(r, xs, Call(r, get_Item, [r.Length - 1]))`, or `NewObject(Index, ...)` for a bare `^e`. Quoted slices that show `GetReverseIndex` today change.
- **SRTP**: `^T` stays a type-parameter prefix in types and in `^T.Member`.
- **C#** cannot see the extension indexers on lists. `Index` and `Range` values pass between the languages unchanged.
- **Tooling**: hover on `^` shows `System.Index`; hover on a bare `..` shows `System.Range`.

# Changes to the F# spec

- §6.4.6 Lookup Expressions: add [Indexing](#indexing).
- §6.4.7 Slice Expressions: add [Slicing](#slicing).
- A new §6.4 subsection: add [Range and index expressions](#range-and-index-expressions).

# Drawbacks

- Code written for the 2019 preview changes meaning where `^` is an index or a slice start.
- Integer ends are inclusive and from-end ends are exclusive, because the `Index` value is exclusive.
- `1..6` as a value is `Range(1, 7)`; C# code and debuggers show the exclusive end.

# Alternatives

- **Keep `^0` as the last element**: then `^7 : Index` must be `Index(8, true)`, and every value passed to .NET is off by one.
- **Type-directed expressions only** (the form of #1044): `let r = 1..6` needs an annotation.
- **Exclusive integer ends**: breaks every existing F# slice.
- **Keep the old `GetReverseIndex` value and add 1 in the compiler**: the member disagrees with the language.

# Prior art

- C# 8 [ranges](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-8.0/ranges.md): `^e`, `Range` values, implicit `Index` support for countable types.
- Python negative indices (#358); range values in Kotlin, Rust and Swift.

# Compatibility

- `FromEndSlicing` has shipped only in preview, so no released language version changes.
- Third-party `GetReverseIndex` members that follow the 2019 contract are off by one until updated.
- Older compilers give FS3303 for `^` outside preview, FS0751 for a bare `1..6` and FS3534 for a bare `^7`, as today. The new FSharp.Core extensions do not change their inference. A new compiler with an older FSharp.Core uses rules 0 and 2 for arrays and lists and does not need `GetReverseIndex`.

# Interop

See [Interactions](#interactions).

# Pragmatics

## Diagnostics

| Number | Condition |
|---|---|
| new error | `xs[^k]` and no rule applies; the message names the rules |
| FS3350 | range or index expression with the feature off |
| new error | `System.Range` or `System.Index` is missing from the target framework |
| FS3303, FS3534 | unchanged |

## Tooling

See [Interactions](#interactions).

## Performance

One `Length` or `Count` read per from-end position: O(1) for arrays, strings and BCL collections, O(n) for F# lists, as today. `Index` and `Range` are structs.

## Scaling

Not applicable.

## Culture-aware formatting/parsing

Not applicable.

# Unresolved questions

- Promote together with FS-1351, not before it.
- Whether other expected types, such as `'T list` and `'T[]`, also select the sequence meaning of a range.
- `int64` and `nint` bounds, `NIndex` and `NRange`.
