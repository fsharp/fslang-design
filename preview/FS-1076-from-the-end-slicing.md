# F# RFC FS-1076 - From the end slicing and indexing for collections

> **Revision, September 2026.** This text replaces the 2019 design, which has been in preview since F# 5 (`--langversion:preview`, feature `FromEndSlicing`) and is still listed in the compiler as unfinished. The revision keeps the syntax and changes three things: `^i` takes the .NET meaning of `System.Index` (`^1` is the last element); from-end positions resolve through `System.Index`/`System.Range` indexers and the countable pattern before the F#-only `GetReverseIndex`; and `a..b` and `^i` become ordinary expressions of type `System.Range` and `System.Index`. The 2019 text is in the file history (commit 96ad025).

The design suggestion [Allow negative indices in indexing and slicing like python](https://github.com/fsharp/fslang-suggestions/issues/358) has been marked "approved in principle". Suggestion [Better interop with `System.Index` and `System.Range`](https://github.com/fsharp/fslang-suggestions/issues/1044) asks for the bare range and index expressions covered by this revision; it is open and not yet approved in principle, and the "coherent spec" requested there is what this document tries to be.

- [x] [Suggestion #358](https://github.com/fsharp/fslang-suggestions/issues/358), approved in principle
- [ ] [Suggestion #1044](https://github.com/fsharp/fslang-suggestions/issues/1044), covered here, approval pending
- [x] [Implementation of the 2019 design](https://github.com/dotnet/fsharp/pull/7781) (merged, preview); the revision needs a follow-up implementation
- [x] [Discussion](https://github.com/fsharp/fslang-design/discussions/472) (2019–2021) and the pull request of this revision (FILL-ME-IN)

**Related:** [FS-1351 Slicing syntax via `System.Range` indexers and `Slice` methods](https://github.com/fsharp/fslang-design/pull/849), [FS-1077 Tolerant slicing](../FSharp-5.0/FS-1077-tolerant-slicing.md), [FS-1110 Index syntax](../FSharp-6.0/FS-1110-index-syntax.md), [FS-1093 Additional type-directed conversions](../FSharp-6.0/FS-1093-additional-conversions.md), the [C# 8 ranges proposal](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-8.0/ranges.md).

# Summary

`^i` in an index or a slice bound denotes `System.Index(i, fromEnd = true)`: the position `i` elements before the end. `xs[^1]` is the last element, `xs[..^1]` drops the last element, `xs[^2..]` is the last two, exactly as in C#. Integer bounds keep F#'s end-inclusive meaning; a from-end end bound is exclusive, because that is what the `Index` value means.

`xs[^i]` resolves, in order, to an indexer taking `System.Index`, to `Item(Length - i)` on a countable type, or to the F#-only `GetReverseIndex` protocol. From-end slice bounds feed the three slicing protocols of FS-1351 (`Range` indexer, `Slice` with `Length`/`Count`, `GetSlice`) and the intrinsic array and string slicers. The receiver is evaluated once.

Outside indexers, list and sequence expressions and `for` loops, `a..b`, `a..`, `..b`, `a..^b` and their combinations are expressions of type `System.Range`, and `^i` is an expression of type `System.Index`. `1..6` is the range that includes 6, so it is C#'s `1..7`.

Two preview language features: the existing `FromEndSlicing` for everything inside brackets, and a new `RangeIndexExpressions` for the bare expressions.

```fsharp
open System
open System.Linq

let xs = [ 1 .. 5 ]
let last = xs[^1]                    // 5
let init = xs[..^1]                  // [1; 2; 3; 4]
let tail2 = xs[^2..]                 // [4; 5]
let inner = xs[1..^1]                // [2; 3; 4]
let ra = ResizeArray xs
let raLast = ra[^1]                  // 5, through Count and Item(int)
let span = "abc123".AsSpan()
let digits = span[^3..]              // "123", through Slice and Length (FS-1351)

let range = 1..6                     // System.Range, 1..7 in C# terms
let fromEnd = ^1                     // System.Index
let sub = xs.Take(1..^1)             // Enumerable.Take(source, Range): [2; 3; 4]
let arrTail = [| 1 .. 5 |].AsSpan(^2..)   // MemoryExtensions.AsSpan(array, Range)
```

# Motivation

The 2019 design predates .NET Core 3.0, which introduced `System.Index` and `System.Range` and gave C# the `^i` and `a..b` operators over them. It chose `^0` for the last element and `Length - i - 1` arithmetic through a bespoke `GetReverseIndex` member, so an F# `^1` is one position away from a C# `^1`, no BCL indexer taking `Index` or `Range` can be reached, and every type needs an F#-specific extension before `^` works on it (`ResizeArray[^1]` fails today, dotnet/fsharp#9425). The design thread [#472](https://github.com/fsharp/fslang-design/discussions/472) already proposed in 2020 that "`^e` *always* becomes `Index(e, true)`"; this revision adopts that.

FS-1351 makes `xs[a..b]` reach `Range` indexers and `Slice` methods; from-end bounds are the missing half. With C# semantics the translation is direct: `^i` is the `Index` the indexer expects, and the `Slice` length follows from `Length - i`.

`System.Range` and `System.Index` are also ordinary argument types in the BCL: `Enumerable.Take(source, Range)`, `Enumerable.ElementAt(source, Index)`, `MemoryExtensions.AsSpan(array, Range)`, `RuntimeHelpers.GetSubArray(array, Range)`, `Tensor` slicing. F# can construct the values only by hand (`Range(Index 1, Index(1, true))`), even though the parser already accepts `1..^1` and `^1` in every expression position and only the checker rejects them (FS0751, FS3534). Making them expressions is the smallest possible change and matches dsyme's suggestion on #1044 that "`a..b` be valid and usable as `System.Range`".

Finally, the current implementation evaluates the receiver of `f()[^1..]` twice (dotnet/fsharp#12071); the revision specifies single evaluation.

# Detailed design

## Terminology

An **index expression** is `e[i1, ..., iN]` (or `e.[...]`) in which every argument is a single value; a **slice expression** has at least one range argument `a..b`, `a..`, `..b` or `*` (§6.4.7, FS-1351). A **from-end bound** is `^expr`. A **countable** type has an accessible intrinsic instance property `Length`, else `Count`, with a parameterless getter of type `int` (`Length` preferred), as in FS-1351 and C#.

## The meaning of `^e`

`^e`, with `e : int`, denotes `System.Index(e, fromEnd = true)`: the position `e` elements before the end of the receiver. On a receiver of length `len` its offset is `len - e`. Consequences:

- `xs[^1]` is the last element; `xs[^0]` is the end itself and is out of range as an index, as in C#.
- As a slice **start**, `^e` starts at offset `len - e` (inclusive), so `xs[^2..]` is the last two elements.
- As a slice **end**, `^e` ends at offset `len - e` **exclusive**, so `xs[..^1]` drops the last element and `xs[^2..^1]` is the second-to-last element alone. An integer end `b` remains inclusive, so `xs[1..3]` is still three elements.

Equivalence with C#:

| F# | C# |
|---|---|
| `xs[^1]` | `xs[^1]` |
| `xs[a..b]` | `xs[a..(b + 1)]` |
| `xs[a..^b]` | `xs[a..^b]` |
| `xs[^a..]` | `xs[^a..]` |
| `xs[..^b]` | `xs[..^b]` |
| `xs[^a..^b]` | `xs[^a..^b]` |
| `xs[*]` | `xs[..]` |

What changes against the 2019 design, on `xs = [1; 2; 3; 4; 5]` (verified with the current preview compiler and a reference implementation of this revision):

| Form | 2019 design (preview today) | This revision (= C#) |
|---|---|---|
| `xs[^1]` | `4` | `5` |
| `xs[^0]` | `5` | out of range |
| `xs[^1..]` | `[4; 5]` | `[5]` |
| `xs[^2..]` | `[3; 4; 5]` | `[4; 5]` |
| `xs[^0..]` | `[5]` | `[]` |
| `xs[..^1]` | `[1; 2; 3; 4]` | `[1; 2; 3; 4]` |
| `xs[..^0]` | all | all |
| `xs[0..^1]` | `[1; 2; 3; 4]` | `[1; 2; 3; 4]` |
| `xs[^2..^1]` | `[3; 4]` | `[4]` |
| `xs[^3..^1]` | `[2; 3; 4]` | `[3; 4]` |
| `xs[1..3]` | `[2; 3; 4]` | `[2; 3; 4]` |

The end-side forms, which the 2019 text expected to be the common ones (`xs[..^1]`), are unchanged; the start-side and single-index forms move by one to agree with C#.

## Indexing with a from-end position

Under `FromEndSlicing`, `e[i1, ..., iN]` where at least one argument is a from-end bound `^k` is elaborated by the first applicable rule, for get and for set alike:

| Order | Requirement on the receiver type | Elaboration |
|---|---|---|
| 0 | array of rank 1–4, or `string` | intrinsic: `^k` at dimension `d` becomes `GetLength(d) - k` (`Length - k` for rank 1 and strings) |
| 1 | an accessible `Item` (or `DefaultMember`) getter/setter, intrinsic or extension, whose parameter at that position has type `System.Index` | `^k` becomes `Index(k, true)`; ordinary indexer lookup |
| 2 | countable, with an `Item` getter/setter taking `int` at that position | `let r = e in r.Item(..., r.Length - k, ...)` |
| 3 | an accessible `GetReverseIndex: rank: int * offset: int -> int` | `let r = e in r.Item(..., r.GetReverseIndex(d, k), ...)` |

- Rule 1 is C#'s explicit `Index` indexer; rule 2 is C#'s implicit `Index` support, and it is what makes `ResizeArray`, `ImmutableArray`, `Span`, `Memory` and `StringBuilder` work with no F#-specific code. Rule 3 is the F#-only protocol kept for types that are neither countable nor `Index`-aware.
- The contract of `GetReverseIndex` changes: it returns **the offset of `^offset`**, `length - offset`, in the given dimension, the same value `Index.GetOffset` would compute. The 2019 contract returned `length - offset - 1`.
- The receiver `e` is bound to a compiler-generated local and evaluated once. Today `(f())[^1]` calls `f` twice.
- Out-of-range offsets, including `^0` as an index, are the indexer's own business; nothing is clamped.
- Mixed arguments follow the same rules per position: `m[^1, 2]` on a 2-D array is `m[m.GetLength(0) - 1, 2]`.

F# lists are countable (`Length`) and have `Item(int)`, so `xs[^1]` needs no `GetReverseIndex`; FSharp.Core additionally gives lists an `Index` indexer (below) so that C# callers and rule 1 share one member.

## Slicing with from-end bounds

FS-1351 defines three slicing protocols and admits only plain bounds. This revision admits from-end bounds on all of them and on the intrinsic array and string slicers. The receiver is evaluated once; `len` denotes its length in the relevant dimension.

| Protocol (FS-1351) | Start bound | End bound (exclusive offset `x`) |
|---|---|---|
| `Range` indexer | `a` → `Index(max a 0)`; `^a` → `Index(a, true)`; absent → `Index.Start` | `b` → `Index(max (b + 1) 0)`; `^b` → `Index(b, true)`; absent → `Index.End` |
| `Slice` + countable | `a` → `s = min (max a 0) len`; `^a` → `s = min (max (len - a) 0) len`; absent → `s = 0` | `b` → `x = b + 1`; `^b` → `x = len - b`; absent → `x = len`; then `Slice(s, max (min (x - s) (len - s)) 0)` |
| `GetSlice` | `a` → `Some a`; `^a` → `Some (len - a)`; absent → `None` | `b` → `Some b`; `^b` → `Some (len - b - 1)`; absent → `None` |
| arrays, strings (intrinsic) | as `GetSlice`, with `len` from `Length`/`GetLength(d)` | as `GetSlice` |

- On the `GetSlice` row, `len` is `Length`/`Count` when the type is countable; otherwise `^a` is `GetReverseIndex(d, a)` and `^b` is `GetReverseIndex(d, b) - 1`. `GetSlice` keeps its inclusive `int option` convention, so a from-end end is converted to its inclusive equivalent.
- The `Range` indexer row needs no arithmetic and no `Length`: the indexer receives the very `Range` a C# caller would pass. FSharp.Core's `List<'T>.Item(range: Range)` from FS-1351 computes `Length` once only when a bound is from-end.
- The tolerance rules of FS-1077 and FS-1351 are unchanged: the `Slice` row clamps, the other rows leave out-of-range offsets to the callee.
- Set-slices (`e[a..^b] <- v`) use the same bound translation with the `Range` indexer setter or `SetSlice`.

## Bare range and index expressions

Under the new feature `RangeIndexExpressions`, a range expression that is not an indexer argument, not the body of a list, array or sequence expression and not the source of a `for` loop is an expression of type `System.Range`, and `^e` outside an indexer is an expression of type `System.Index`. The parser already produces `SynExpr.IndexRange` and `SynExpr.IndexFromEnd` for these forms in every expression position; only the checker changes.

| Expression | Value |
|---|---|
| `a..b` | `Range(Index a, Index(b + 1))` |
| `a..` | `Range(Index a, Index.End)` |
| `..b` | `Range(Index.Start, Index(b + 1))` |
| `a..^b` | `Range(Index a, Index(b, true))` |
| `^a..b` | `Range(Index(a, true), Index(b + 1))` |
| `^a..` | `Range(Index(a, true), Index.End)` |
| `..^b` | `Range(Index.Start, Index(b, true))` |
| `^a..^b` | `Range(Index(a, true), Index(b, true))` |
| `^e` | `Index(e, true)` |

- Bounds are checked with expected type `int`. The integer end is inclusive, as everywhere else in F#: `1..6` is the six elements 1 to 6, hence `Range(1, 7)`. C# code that receives the value sees `1..7`.
- Nothing is clamped: a negative bound throws `ArgumentOutOfRangeException` from the `Index` constructor at run time, as in C#. Clamping belongs to the slicing translation of FS-1351, not to a value.
- **Expected type.** If the expression is checked against a known expected type other than `System.Range`, the pre-existing meaning applies where one exists: with expected type `seq<'T>` a range expression is the range sequence, as if written `seq { a..b }`; with any other expected type the expression is an error, as today (FS0751). With an unknown expected type, or `System.Range`, it is a `Range`. `^e` is an `Index` unless the expected type is known and is not `System.Index`, in which case it is an error as today. Plain integers already convert to `Index` where one is expected through the `op_Implicit` rule of FS-1093.
- `[a..b]`, `[| a..b |]`, `seq { a..b }`, `{ a..b }`, `for i in a..b do` and `xs[a..b]` are unaffected: those contexts consume the syntax before this rule applies. `for i in ^3..^1 do` remains an error.
- `^T.Member` in expression position keeps its current diagnostic (FS3534, "use `'T.Member`") and recovery: when the operand of `^` is a dotted long identifier the expression is treated as a mistaken static-constraint invocation as today; any other operand is an `Index` expression.
- `System.Range` and `System.Index` must be present in the target framework (netstandard2.1, .NET Core 3.0 and later, or a polyfill assembly); otherwise the expression reports that the type is not available.

Examples:

```fsharp
let r = 2..^1                                  // System.Range
let firstTwo = 0..1
let text = "hello world"
text.AsSpan(^5..)                              // "world"
[| 1 .. 10 |].AsSpan(firstTwo).ToArray()       // [| 1; 2 |]
[ 1 .. 10 ].ElementAt(^1)                      // 10
```

## FSharp.Core

- `List<'T>` gains `member Item: index: Index -> 'T with get`, compiled for the frameworks that define `System.Index` (the `netstandard2.1` and `net` builds, the guard used by `List.Create(ReadOnlySpan<'T>)` and by FS-1351's `Item(range: Range)`). A from-end index walks the list twice (`Length`, then `Item`), as today's `GetReverseIndex` path does.
- `GetReverseIndex` on `List<'T>`, on arrays of rank 1 to 4 and on `string` (all `[<Experimental>]`, preview-only) is redefined to the new contract, `length - offset` in the given dimension. The compiler no longer emits calls to them for these types (they are countable); they stay for source compatibility of explicit calls and are hidden from completion with `[<EditorBrowsable(EditorBrowsableState.Never)>]`.
- Nothing else. `ResizeArray`, `Span`, `Memory`, `ArraySegment`, `ImmutableArray` and `StringBuilder` become from-end indexable through the countable rule; dotnet/fsharp#9425 is answered without an extension.

## Quotations

`<@ xs[^1] @>` on a countable receiver yields `Let(r, xs, Call(r, get_Item, [Call(op_Subtraction, [PropertyGet(r, Length); 1])]))`; on an `Index` indexer, `Call(r, get_Item, [NewObject(Index, [1; true])])`. Bare expressions quote as `NewObject(Range, ...)`/`NewObject(Index, ...)`. The array baselines in `tests/fsharp/Compiler/Language/SlicingQuotationTests.fs`, which today show `GetReverseIndex` calls, change accordingly.

# Changes to the F# spec

**§6.4.6 Lookup Expressions.** Add, before the syntactic translation `e1.[eargs] → e1.get_Item(eargs)`:

```diff
+When the language feature `FromEndSlicing` is enabled, an argument of the form `^e` denotes the
+position `e` elements before the end of the receiver. It is elaborated by the first applicable rule:
+arrays and strings use `GetLength(d) - e`; a type with an indexer parameter of type `System.Index` at
+that position receives `System.Index(e, true)`; a type with an accessible intrinsic `int` property
+`Length` (else `Count`) and an `int` indexer parameter receives `r.Length - e`, where `r` is the receiver
+bound once to a fresh variable; otherwise the type must provide `GetReverseIndex: int * int -> int`
+and receives `r.GetReverseIndex(d, e)`, whose result is the offset of `^e` in dimension `d`.
```

**§6.4.7 Slice Expressions.** After the FS-1351 rules, add:

```diff
+A from-end bound `^e` in a slice argument is translated per protocol: for a `System.Range` indexer,
+to `System.Index(e, true)` at either position; for the `Slice` elaboration, to the start offset
+`len - e` or the exclusive end offset `len - e`; for `GetSlice` and for the intrinsic array and
+string slicers, to `Some (len - e)` as a start and `Some (len - e - 1)` as an end, where `len` is the
+receiver's `Length`/`Count`/`GetLength(d)` when the type is countable and `GetReverseIndex(d, e)`
+(with `- 1` for an end) otherwise. The receiver is evaluated once.
```

**§6.4, new subsection "Range and index expressions".**

```diff
+When the language feature `RangeIndexExpressions` is enabled, a range expression `e1..e2`, `e1..`,
+`..e2` (with either bound optionally of the form `^e`) that does not occur as an indexer argument, as
+the body of a list, array or sequence expression, or as the source of a `for` loop, is an expression
+of type `System.Range`; `^e` in the same positions is an expression of type `System.Index`. The
+bounds have type `int`. `e1..e2` denotes `System.Range(System.Index e1, System.Index(e2 + 1))`; a
+bound `^e` denotes `System.Index(e, true)`; a missing start denotes `System.Index.Start` and a missing
+end `System.Index.End`. If the expected type of the expression is known and is `seq<T>`, the
+expression is instead the sequence expression `seq { e1..e2 }`.
```

# Drawbacks

- **Changed meaning inside a preview feature.** Code written against the 2019 preview that uses `^i` as a single index or as a slice start moves by one element. The feature has never shipped outside `--langversion:preview`, and the end-side forms are unchanged, but the change must be called out in release notes.
- **Asymmetry.** Integer bounds are inclusive and from-end end bounds are exclusive. C# has the same two behaviours (`a..b` exclusive there); F# keeps its inclusive integers because changing them is not an option. The table above shows the practical effect is that both languages select the same elements for every from-end form.
- **`1..6` as a value is `1..7` in C#.** Anyone reading the `Range` value in C# or in a debugger sees the exclusive end. This is inherent in keeping F# ranges inclusive.
- **A third protocol for from-end indexing** (`GetReverseIndex`) survives, although only non-countable, non-`Index`-aware types need it.
- **Bare `^e` overlaps visually with SRTP `^T`**; the two never occur in the same position, and the existing diagnostic for `^T.Member` is preserved.

# Alternatives

- **Keep `^0` as the last element**, the 2019 design. Rejected: `^7 : Index` would have to be `Index(8, true)`, every value handed to a .NET `Index` or `Range` parameter would be off by one against the C# reading of the same source, and the `Range` indexer path of FS-1351 would need arithmetic instead of passing the `Index` through.
- **Context-dependent `^i`** (`len - i` as a start, `len - i - 1` as an end), the alternative the 2019 text listed. This is what the revision does in effect, expressed as a single definition, `Index(i, true)`, whose offset is the same in both positions and whose end position is exclusive.
- **Make integer bounds exclusive too**, full C# semantics. Breaking for every F# slice ever written; not considered.
- **Type-directed bare expressions only** (the form of #1044: `Range` only under an annotation or a known parameter type). Rejected in favour of a default type, so that `let r = 1..6` works and inference does not decide between `Range` and `seq<int>`; the `seq<'T>` case is kept for the expected-type situation where the sequence meaning already existed.
- **A `Range`-constructing function in FSharp.Core** instead of syntax. Possible today (`Range(Index a, Index(b + 1))`), and the reason the syntax is worth having.
- **Redefine `GetReverseIndex` to keep its old value and add `+ 1` in the compiler.** Rejected: the member's documented meaning would then differ from the language's meaning of `^i`.
- **Do nothing.** From-end slicing stays a preview feature that disagrees with C# by one and cannot reach `Index`/`Range` indexers.

# Prior art

- **C# 8** ([ranges proposal](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-8.0/ranges.md)): `^e` is `Index(e, fromEnd: true)`; `a..b` is a `Range` value usable anywhere; implicit `Index` support for countable types with an `int` indexer (`^e` → `Length - e`); explicit `this[Index]`/`this[Range]` win; `..` bounds are exclusive.
- **Python** negative indices (`xs[-1]`, `xs[:-1]`), the model of suggestion #358; exclusive ends.
- **Rust** (`a..b`, `a..=b`), **Swift** (`a..<b`, `a...b`), **Kotlin** (`a..b`, `until`) all have range values as first-class expressions; Kotlin's are inclusive like F#'s.
- **FS-1077** tolerant slicing; **FS-1351** slicing protocols; **FS-1093** `op_Implicit` conversions (`int → Index`); design thread **#472**.

# Compatibility

- **Is this a breaking change?** Not for any shipped language version: `FromEndSlicing` has only ever been available under `--langversion:preview`. Within the preview, the forms in the "What changes" table change value, and third-party `GetReverseIndex` implementations written to the 2019 contract are off by one until updated; both are documented in the release notes.
- **Older compilers and the new source.** `^` outside preview: FS3303 as today. Bare `1..6`: FS0751; bare `^7`: FS3534.
- **Older compilers and the new FSharp.Core.** `Item(Index)` is an ordinary indexer, callable as `xs[Index(1, true)]`; the redefined `GetReverseIndex` is only reached by an older compiler's preview `^` elaboration, whose results then shift by one. Preview-only.
- **New compiler and an older FSharp.Core**, or a `netstandard2.0` target. Lists and arrays are countable, so the compiler never depends on FSharp.Core's `GetReverseIndex` and the results are those of this revision; `Item(Index)` is simply absent.
- **Quotations** change shape as described above.
- **FS-1351 interaction.** FS-1351 excludes from-end bounds from its new paths; this revision lifts that exclusion. The two RFCs are meant to be promoted together.

# Interop

- **C#.** `FSharpList<T>` gains `this[Index]` beside FS-1351's `this[Range]`, so `fsList[^1]` and `fsList[1..^1]` compile in C#. F# types with `Index` or `Range` indexers are used by F# slicing syntax with the same values C# would pass.
- **Values.** A bare F# `^7` is C#'s `^7`; a bare F# `a..b` is C#'s `a..(b + 1)`; `a..^b`, `^a..`, `..^b` are identical in both.
- **Tensors.** `NIndex`/`NRange` and `params ReadOnlySpan<NRange>` indexers remain out of scope (see FS-1351, suggestion #1377).
- **Visual Basic** is unaffected.

# Pragmatics

## Diagnostics

| Number | Kind | Text (abridged) | When |
|---|---|---|---|
| FS3303 | error | The 'from the end slicing' feature requires language version 'preview'. | unchanged |
| new | error | The type '%s' does not support from-end indexing. It requires an indexer taking 'System.Index', an integer 'Length' or 'Count' property together with an integer indexer, or a 'GetReverseIndex' method. | `xs[^e]` with no protocol (replaces FS0039 naming `GetReverseIndex`) |
| FS3350 | error | Feature 'range and index expressions' is not available in F# %s. | bare `a..b`/`^e` with the feature off (replaces FS0751/FS3534 in that position) |
| new | error | The type 'System.Range'/'System.Index' is not available in the target framework. | bare expression on a framework without the types |
| FS3534 | error | unchanged | `^T.Member` |

## Tooling

- Hover on `^` shows `System.Index`; hover on `..` in a bare expression shows `System.Range`; inside an indexer the FS-1351 recording at the operator range applies.
- Completion, colorization, brace matching: unchanged.
- Debugging: stepping enters the indexer or `Slice`; compiler-generated receiver and length locals are hidden.

## Performance

- One `Length`/`Count` read per from-end position on the countable, `Slice` and `GetSlice` rows; O(1) on arrays, strings and the BCL types, O(n) on F# lists as today. No allocation: `Index` and `Range` are structs, and the `option` values of the `GetSlice` row are the same as today.
- Compilation: one additional member lookup (`Item(Index)`) per from-end index expression; negligible.

## Scaling

- From-end bounds per expression: at most one per dimension; elaboration is linear in the number of arguments.

## Culture-aware formatting/parsing

Not applicable.

# Unresolved questions

- **Promotion.** `FromEndSlicing` and `RangeIndexExpressions` should be promoted to a numbered language version together with FS-1351, never before it.
- **Other expected types for bare ranges.** Whether `'T list` or `'T[]` expected types should also select the sequence meaning, or whether `seq<'T>` should be dropped so that a bare range is always a `Range`.
- **Wider bound types.** `int64`/`nint` bounds, `NIndex`/`NRange` for tensors, and `Length` properties of type `int64` (`ReadOnlySequence<T>`).
- **Multi-dimensional arrays and `Index`.** Whether `Array2D` and friends should accept `Index` arguments explicitly, beyond the intrinsic `^e` support.
- **Removing `GetReverseIndex` from FSharp.Core** once the compiler no longer emits it, given that the members are `[<Experimental>]`.

# Appendix: implementation sketch (dotnet/fsharp)

File references are to `dotnet/fsharp` `main` in September 2026.

- **Language features** — `src/Compiler/Facilities/LanguageFeatures.fs(i)`: keep `FromEndSlicing` (`previewVersion`), add `RangeIndexExpressions` (`previewVersion`); `src/Compiler/FSComp.txt`: `featureRangeIndexExpressions` and the new messages.
- **Checker, bare expressions** — `src/Compiler/Checking/Expressions/CheckExpressions.fs`, `TcExprUndelayed`: replace the `SynExpr.IndexRange` arm that raises FS0751 (~6271) and the `SynExpr.IndexFromEnd` arms that raise FS3534 (~5734, ~6264) with the `Range`/`Index` construction when the feature is on and the expected type permits; keep the `adjustHatPrefixToTyparLookup` recovery for dotted operands. `System.Range` and `System.Index` are resolved by name through `TcGlobals` (`tryFindSysTyconRef`), as FS-1351 does for its indexer path.
- **Checker, indexing and slicing** — `ExpandIndexArgs` (~6793-6837) no longer rewrites `^e` to `GetReverseIndex` syntactically; instead `TcIndexingThen` (~6844) receives the decoded from-end flags, spills the receiver with `mkCompGenLet`, and applies the per-protocol translations above, sharing the countable lookup (`TryFindIntrinsicPropInfo` for `Length`/`Count`) and the `Index`/`Range` construction with the FS-1351 paths. Arrays and strings compute `GetLength(d)`/`Length` inline.
- **FSharp.Core** — `src/FSharp.Core/prim-types.fs(i)`: `List<'T>.Item(index: Index)` under `#if NETSTANDARD2_1_OR_GREATER || NET`; `GetReverseIndex` bodies on `List<'T>` (`prim-types.fs:4372`) and in `ArrayExtensions` (`prim-types.fs:7264-7308`) changed to `length - offset`, plus `EditorBrowsable(Never)`; surface-area baselines under `tests/FSharp.Core.UnitTests/` updated for the new getter.
- **Tests** — `tests/fsharp/Compiler/Language/CustomCollectionTests.fs` (protocol order: `Index` indexer, countable, `GetReverseIndex`; the receiver-once guarantee), `tests/fsharp/Compiler/Language/SlicingQuotationTests.fs` (new baselines), `tests/FSharp.Compiler.ComponentTests/Conformance/Expressions/SyntacticSugar/` (the equivalence table on lists, arrays, strings, `ResizeArray`, `Span`, a `Range`-indexer type; bare expressions with and without expected types; `^T.Member` still FS3534; feature-off diagnostics), FSharp.Core unit tests for `Item(Index)` and the redefined `GetReverseIndex`.
- **Release notes** — `docs/release-notes/.Language/preview.md` (changed preview semantics, new feature), `docs/release-notes/.FSharp.Core/<next>.md`.
