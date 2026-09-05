# F# RFC FS-1345 - Slicing syntax via `System.Range` indexers and `Slice` methods

The design suggestion [Allow slice syntax to use instance `Slice` method instead of requiring `GetSlice` method](https://github.com/fsharp/fslang-suggestions/issues/1317) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1317)
- [x] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/17377) (draft by @brianrourkeboll from July 2024; it predates this RFC, resolves the protocols in a different order and needs to be refreshed)
- [x] [Discussion](https://github.com/fsharp/fslang-design/pull/849)

**Related:** [FS-1077 Tolerant slicing](../FSharp-5.0/FS-1077-tolerant-slicing.md), [FS-1076 From-the-end slicing](../preview/FS-1076-from-the-end-slicing.md) (preview), [FS-1110 Index syntax](../FSharp-6.0/FS-1110-index-syntax.md), [FS-1111 Reference cell advisory messages](../FSharp-6.0/FS-1111-refcell-op-information-messages.md), suggestion [#1044](https://github.com/fsharp/fslang-suggestions/issues/1044) (`System.Index` and `System.Range` support).

# Summary

Slicing syntax `expr[a..b]` learns the two protocols that .NET and C# already use, in the order C# uses them:

1. an indexer taking `System.Range`, and
2. an instance method `Slice(start: int, length: int)` on a type with an `int` property `Length` or `Count`.

The existing F# protocol, `GetSlice`, remains and is tried last. `Span`, `ReadOnlySpan`, `Memory`, `ReadOnlyMemory`, `ArraySegment`, `List<T>` and `ImmutableArray` therefore slice out of the box, and a library type needs only one of the .NET shapes to be sliceable from both C# and F#. The compiler builds the `System.Range` value and computes the `Slice` length itself, so F# slices keep their end-inclusive meaning and, on the `Slice` path, their tolerance of out-of-range bounds.

FSharp.Core gives `list<'T>` a `System.Range` indexer, so list slicing takes the first path with no change in cost, and marks the F#-only `List<'T>.GetSlice` as deprecated through an informational diagnostic that never fails a build. The feature is gated by a new language feature, `SliceMethodSlicing`.

```fsharp
open System

let span = "abc123".AsSpan()
let s = span[1..3]                       // ReadOnlySpan<char> "bc1"   via Slice + Length
let memory = Memory<int>([| 1 .. 10 |])
let m = memory[2..]                      // Memory<int> of 3 .. 10     via Slice + Length
let resizeArray = ResizeArray [ 1 .. 10 ]
let r = resizeArray[..3]                 // List<int> [1; 2; 3; 4]     via List<T>.Slice + Count (.NET 8 and later)
let xs = [ 1 .. 10 ]
let l = xs[1..3]                         // [2; 3; 4]                  via FSharpList<T>.Item(Range)
let sub = matrix[0.., *]                 // a user type with Item(Range, Range)
```

# Motivation

Every one-dimensional collection that .NET has added since 2018 exposes `Slice(int, int)` together with `Length` or `Count`, and C# 8 turned that pair into the meaning of `x[a..b]`. F# recognises only its own `GetSlice` convention, so each of those types is unsliceable in F# until somebody writes an extension member for it. The [Slices page on Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/slices) documents exactly that recipe for `ArraySegment`, `Span` and `ReadOnlySpan`; its `Span` example computes `sp.Slice(s, e - s)`, which is end-exclusive, so code copied from the official documentation silently disagrees with every other F# slice. The suggestion's own workaround, an SRTP extension over `member Slice` and `member Length`, has the same off-by-one hazard and allocates two `option` values per slice.

Library authors who want both languages to slice their type must implement both conventions: `Slice` plus `Length` for C# and `GetSlice` for F#. Types whose slicing cannot be expressed with two integers, such as tensors and matrices, use a `System.Range` indexer in C#; F# cannot reach it through slicing syntax at all, even though `xs[Range(1, 4)]` already compiles as an ordinary indexer call.

FSharp.Core's own `List<'T>.GetSlice: int option * int option -> 'T list` is a compiler protocol that leaks into the public API of every F# list: it appears in completion, it is the only slicing member C# sees on `FSharpList<T>`, and it takes `FSharpOption` arguments that no C# caller wants. Giving the list a `System.Range` indexer lets both languages slice it natively and lets `GetSlice` retire.

# Detailed design

## Terminology

A **slice expression** is `e[arg1, ..., argN]` (or the legacy `e.[...]`) in which at least one argument is a **range argument**: `a..b`, `a..`, `..b` or `*` (§6.4.7). Every other argument is a **fixed index**. A bound is **plain** when it is not a from-end bound `^expr` (FS-1076). Throughout this RFC `Range` and `Index` mean `System.Range` and `System.Index`.

## Feature gate and scope

The rules below apply under the language feature `SliceMethodSlicing` (preview). They apply to a slice expression whose receiver has a nominal type other than an array or `string` and whose bounds are all plain. Arrays and strings keep their intrinsic elaboration to `OperatorIntrinsics.GetArraySlice*`/`GetStringSlice`. A slice expression with a from-end bound is elaborated exactly as today (`GetReverseIndex` plus `GetSlice`); extending the new paths to from-end bounds is left to a revision of FS-1076 (see [Unresolved questions](#unresolved-questions)).

## Resolution order

The first protocol whose requirements the receiver type satisfies is used; later ones are not consulted.

| Order | Protocol | Requirement on the receiver type `T` | Applies to |
|---|---|---|---|
| 1 | `Range` indexer | an accessible indexed property named `Item` (or the name given by `DefaultMemberAttribute`, §6.4.6), intrinsic or extension, with a getter of exactly N parameters where every parameter at a range-argument position has type `Range` | any N, get and set, fixed indices allowed |
| 2 | `Slice` method | an accessible instance method `Slice` with two `int` parameters, plus an accessible intrinsic property getter `Length` or `Count` of type `int` | N = 1, get only |
| 3 | `GetSlice` | as today | everything else |

With the feature off, only row 3 applies, as today.

The order is C#'s: a `Range` indexer is the type author's explicit statement of what a range means for the type, so it wins over the `Slice` pattern. `Slice` is placed before `GetSlice` by design choice, so that a .NET type's own slicing is used even when an F#-only `GetSlice` extension for it is in scope; the consequences are listed under [Compatibility](#compatibility).

## Protocol 1: `Range` indexer

Each range argument is translated to a `Range` value; fixed indices are passed unchanged. The result is an ordinary indexer lookup (§6.4.6), so overload resolution (§14.4) chooses among `Item` overloads, and `int` fixed indices convert to `Index` parameters through the usual `op_Implicit` rule.

| Range argument | `Range` value |
|---|---|
| `a..b` | `Range(Index(max a 0), Index(max (b + 1) 0))` |
| `a..` | `Range(Index(max a 0), Index.End)` |
| `..b` | `Range(Index.Start, Index(max (b + 1) 0))` |
| `*` | `Range.All` |

- `a` and `b` are checked with expected type `int`.
- The translation preserves the F# meaning of the syntax: `xs[1..3]` denotes elements 1, 2 and 3, hence `Range(1, 4)`. The indexer itself is end-exclusive like every `Range` consumer in .NET, so `fsList[1..3]` in C# keeps its C# meaning on the same member.
- `max _ 0` is applied because `Index` cannot represent a negative offset (its constructor throws), and because F# collections treat a negative bound as 0 (FS-1077). A bound that is negative after `+ 1` therefore yields an empty range rather than an exception.
- Everything else, including a start beyond the end or an end beyond the length, is the indexer's own behaviour, exactly as it is for `GetSlice` types today.
- Assignment `e[args] <- v` uses the indexer's setter when the property has one; otherwise it is elaborated to `SetSlice` as today.
- Evaluation order: the receiver, then each bound left to right, each exactly once; then the indexer call.

```fsharp
type Matrix(data: float[,]) =
    member _.Item
        with get (rows: Range, cols: Range) : Matrix = ...
        and set (rows: Range, cols: Range) (value: Matrix) = ...
    member _.Item with get (row: int, cols: Range) : float[] = ...

let m = Matrix(Array2D.zeroCreate 4 4)
let a = m[0..1, *]          // m.Item(Range(Index 0, Index 2), Range.All)
let b = m[2, 1..]           // m.Item(2, Range(Index 1, Index.End))
m[..1, ..1] <- a            // m.set_Item(Range(Index.Start, Index 2), Range(Index.Start, Index 2), a)
```

The `Range` and `Index` members the compiler uses are the constructor `Range(Index, Index)`, the property `Range.All`, the constructor `Index(int)` and the properties `Index.Start` and `Index.End`, taken from the assembly that defines the indexer's parameter type. A project that targets a framework without these types cannot declare such an indexer, and a project that uses a polyfill package for them is served by the polyfill.

## Protocol 2: `Slice` method with a countable property

Requirements, decided from the receiver type alone before any bound is checked:

- exactly one range argument, no fixed index, and the expression is not an assignment;
- an accessible instance method named `Slice`, intrinsic or an extension member in scope that is applicable to `T`, with a single parameter group of exactly two parameters whose types are `int` after erasing units of measure, none of which is optional, `ParamArray`, `byref` or `outref`, and no method type parameters; the return type is unconstrained;
- an accessible intrinsic instance property `Length` with a parameterless getter of type `int` after erasing units of measure; if there is none, `Count` under the same conditions. `Length` is preferred when both exist.

If more than one `Slice` overload satisfies the shape (for example `Slice(int, int)` and `Slice(int<m>, int<m>)`), the expression is an error (see [Diagnostics](#diagnostics)). Bounds are checked with the expected type of the chosen `Slice` parameters; the arithmetic below is performed on the underlying `int`.

The elaboration is tolerant in the sense of FS-1077 and matches `OperatorIntrinsics.GetArraySlice` for every combination of bounds. `min` and `max` denote integer comparisons emitted as conditionals; they do not refer to FSharp.Core functions.

| Slice expression | Elaboration |
|---|---|
| `e[a..b]` | `let r = e in let len = r.Length in let s = min (max a 0) len in r.Slice(s, max (min (b - s + 1) (len - s)) 0)` |
| `e[a..]` | `let r = e in let len = r.Length in let s = min (max a 0) len in r.Slice(s, len - s)` |
| `e[..b]` | `let r = e in let len = r.Length in r.Slice(0, max (min (b + 1) len) 0)` |
| `e[*]` | `let r = e in r.Slice(0, r.Length)` |

- `r`, `len` and `s` are compiler-generated locals; the receiver, each bound and `Length` are evaluated exactly once, in the order receiver, `a`, `b`, `Length`, `Slice`.
- A byref-like receiver such as `Span<T>` is held in a local like any other byref-like value.
- Results on a five-element receiver `1, 2, 3, 4, 5`, checked against today's array slicing:

| Bounds | `[1..3]` | `[2..]` | `[..1]` | `[*]` | `[-2..-1]` | `[0..100]` | `[5..2]` | `[-3..1]` | `[4..99]` | `[7..]` | `[..-1]` |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Elements | 2, 3, 4 | 3, 4, 5 | 1, 2 | all | empty | all | empty | 1, 2 | 5 | empty | empty |

Calling `Slice` directly keeps the method's own semantics: `span.Slice(5, -2)` throws, `span[5..2]` is empty. This is the same split arrays have today between `Array.sub` and `arr[5..2]`.

Types that qualify on .NET 8 and later: `Span<T>`, `ReadOnlySpan<T>`, `Memory<T>`, `ReadOnlyMemory<T>`, `ArraySegment<T>` (through `Count`), `System.Collections.Generic.List<T>` (through `Count`), `ImmutableArray<T>`. `ReadOnlySequence<T>` does not qualify because its `Length` is `int64`, which is also C#'s verdict.

## Protocol 3: `GetSlice`

Unchanged. It remains the only way to slice with non-integer bounds (`GetSlice(lb: char option, ub: char option)` is exercised by the compiler test suite), to slice in several dimensions without a `Range` indexer, to support `SetSlice` on types without an indexer setter, to slice types that have `Slice` but no countable property, and to use from-end bounds. Nothing in this RFC deprecates the convention for user-defined types.

## What a type author should now do

| Goal | Members to define |
|---|---|
| One-dimensional slicing from C# and F# | `Slice(start: int, length: int)` and `Length` (or `Count`) |
| Slicing with custom range semantics, several dimensions, or slice assignment from both languages | `Item` getter (and setter) taking `Range` parameters |
| F#-only slicing with non-integer bounds | `GetSlice` (and `SetSlice`) |

## FSharp.Core: `list<'T>`

`List<'T>` gains an indexer overload, compiled only for the target frameworks that define `System.Range` (the `netstandard2.1` and `net` builds, the same guard that `List.Create(ReadOnlySpan<'T>)` uses):

```fsharp
type List<'T> =
    ...
    /// <summary>Gets the elements of the list covered by the given range.</summary>
    member Item: range: Range -> 'T list with get
```

- The result is the same as today's `GetSlice` for every bound: a range whose start or end lies outside the list is clamped, and an empty range yields `[]`. A range with plain offsets is served in a single partial traversal, sharing the tail when the range reaches the end, exactly as `GetSlice` does now. `Length` is computed at most once, and only when `Start` or `End` is from-end, which slicing syntax never produces under this RFC; C# callers and explicit `Index(n, true)` values can.
- Under protocol 1, `xs[1..3]` on a list elaborates to this indexer; the compiler contains no special case for F# lists.
- On the `netstandard2.0` build, and for any compiler that does not implement this RFC, list slicing continues to use `GetSlice`.

`List<'T>.Slice(start, length)` is deliberately not added: with `Slice` before `GetSlice`, slicing syntax would then read the list's O(n) `Length` on every slice, and a length-based method beside an end-inclusive syntax invites the off-by-one mistake this RFC removes from `Span`. `Slice(Range)` is not added either: no language resolves that shape and no BCL type has it.

## FSharp.Core: deprecating `List<'T>.GetSlice`

`GetSlice` stays, for binary compatibility, for the `netstandard2.0` build and for older compilers. It is deprecated in three ways that never fail a build:

1. `[<EditorBrowsable(EditorBrowsableState.Never)>]`, which removes it from completion in F# and C# editors without affecting compilation.
2. An XML documentation remark stating that slicing syntax should be used instead.
3. A compiler informational diagnostic (severity *info*, see FS-1111) reported when source code explicitly names `GetSlice` on an F# list, gated by `SliceMethodSlicing`:

   ```
   info FS3918: The use of 'GetSlice' on F# lists is deprecated. Use slicing syntax 'list[start..finish]' instead.
   ```

   Lookups that the compiler synthesises for slicing syntax carry synthetic ranges and are exempt, so a project compiled for `netstandard2.0`, or with an older language version, sees no message for `xs[1..3]`. As with FS3370, `--warnon:3918` promotes the message to a warning and `--nowarn:3918` silences it; `--warnaserror` does not affect it unless the number is named.

`[<Obsolete>]` is not used: FS0044 is an ordinary warning and would break every project that builds with `TreatWarningsAsErrors`. `[<CompilerMessage>]` is not used because it always produces a warning.

## Quotations

`<@ span[1..3] @>` is not expressible because `Span` cannot appear in a quotation. For other receivers the quotation reflects the elaboration: protocol 1 gives `PropertyGet(Some xs, Item, [NewObject(Range, ...)])`, protocol 2 gives `Let(r, ..., Let(len, PropertyGet(r, Length), Let(s, ..., Call(r, Slice, [...]))))`. The shape of a quoted list slice therefore changes from `Call(GetSlice)` to `PropertyGet(Item)` on frameworks that have `System.Range`; quotation consumers that translate FSharp.Core members by name, such as Fable, need a matching entry.

# Changes to the F# spec

**§6.4.7 Slice Expressions.** Insert before the existing syntactic translation:

```diff
 Slice expressions are defined by syntactic translation:
 
+When the language feature `SliceMethodSlicing` is enabled, the type of `e1` is a nominal type other
+than an array or `string`, and no `sliceArg` contains a from-end bound, the following rules are tried
+in order and the first that applies is used:
+
+1. If the type of `e1` has an accessible indexed property, resolved by the name used for indexer lookup
+   (§6.4.6), whose getter has one parameter per `sliceArg` and whose parameters at the positions of
+   range arguments have type `System.Range`, then
+
+   `e1.[sliceArg1, ..., sliceArgN]` → `e1.get_Item(rarg1, ..., rargN)`
+   `e1.[sliceArg1, ..., sliceArgN] <- expr` → `e1.set_Item(rarg1, ..., rargN, expr)`  (if a setter exists)
+
+   where `*` → `System.Range.All`, `a..` → `System.Range(System.Index(max a 0), System.Index.End)`,
+   `..b` → `System.Range(System.Index.Start, System.Index(max (b + 1) 0))`,
+   `a..b` → `System.Range(System.Index(max a 0), System.Index(max (b + 1) 0))`, and `idx` → `idx`.
+
+2. If N = 1, the expression is not an assignment, the type of `e1` has an accessible instance method
+   `Slice` with exactly two parameters of type `int` (ignoring units of measure), and an accessible
+   intrinsic property `Length`, or otherwise `Count`, with a parameterless getter of type `int`, then
+   the expression is elaborated as follows, where `r`, `len` and `s` are fresh variables and `min`/`max`
+   are integer comparisons:
+
+   `e1.[a..b]` → `let r = e1 in let len = r.Length in let s = min (max a 0) len in r.Slice(s, max (min (b - s + 1) (len - s)) 0)`
+   `e1.[a..]`  → `let r = e1 in let len = r.Length in let s = min (max a 0) len in r.Slice(s, len - s)`
+   `e1.[..b]`  → `let r = e1 in let len = r.Length in r.Slice(0, max (min (b + 1) len) 0)`
+   `e1.[*]`    → `let r = e1 in r.Slice(0, r.Length)`
+
+Otherwise:
+
 `e1.[sliceArg1, ,,, sliceArgN]` → `e1.GetSlice(args1, ..., argsN)`
```

**§6.4.6 Lookup Expressions.** No change to the rules; the name resolved there (`Item` or the `DefaultMember` name) is the one rule 1 above uses.

**§14.4 Method Application Resolution.** No change; the indexer call produced by rule 1 and the `Slice` and `Length` member calls produced by rule 2 are resolved by the existing rules.

# Drawbacks

- **Three protocols instead of one.** The language now has to explain a precedence order. The order is C#'s, plus `GetSlice` last, and each protocol has a distinct, easily stated role (see the table under [What a type author should now do](#what-a-type-author-should-now-do)).
- **Behaviour changes for types that satisfy several protocols.** A type that has both a `GetSlice` and a `Range` indexer or `Slice` method changes elaboration under the new language version. The cases are enumerated under [Compatibility](#compatibility); the most likely one is a hand-written `GetSlice` extension for a BCL type, which becomes dead code and can be deleted.
- **Two semantics on one type.** On the `Slice` path, `span[5..2]` is empty while `span.Slice(5, -2)` throws. Arrays and strings already behave this way in F#; the alternative, strict lowering, is discussed below.
- **Tolerance differs between protocols.** The `Slice` path clamps every bound; the `Range` indexer path clamps only negative offsets and leaves the rest to the indexer, because the compiler cannot clamp an end it cannot compute without a length. This mirrors C#, where a `Range` indexer's semantics are the type's own.
- **One `Length` call per slice on the `Slice` path**, needed for tolerance. It is O(1) on every BCL type listed above.
- **Quoted list slices change shape** on frameworks with `System.Range`.

# Alternatives

- **`GetSlice` before `Slice`**, as in the draft implementation. It has no behaviour change for existing code, and the F# team may prefer it for that reason. It was not chosen because a stale or incorrect `GetSlice` extension in scope, including the one on Microsoft Learn, would forever shadow a type's own `Slice`, and because the author wants the .NET protocols to be the primary ones. Switching the order is a one-line change in the resolution table and in the implementation.
- **Strict elaboration on the `Slice` path**, `r.Slice(a, b - a + 1)` with no `Length` read for `e[a..b]`, giving the same exceptions as C#. Rejected for consistency with F# arrays, strings and lists (FS-1077) and because it would change the results of existing tolerant `GetSlice` extensions that the `Slice` path replaces.
- **Rejecting types that have a `Range` indexer** until `System.Range` is supported more broadly (the "detect, but do not support" option discussed on the suggestion). Rejected: the indexer is already callable explicitly today, and supporting it is what makes F# lists and multi-dimensional types work without special cases.
- **`List<'T>.Slice(start, length)`**, with or without a `GetListSlice` intrinsic in `OperatorIntrinsics`. Rejected as described under [FSharp.Core: `list<'T>`](#fsharpcore-listt).
- **`Slice` extension members for arrays and strings in FSharp.Core.** Nothing to gain: arrays and strings have no `GetSlice` members to retire and are elaborated intrinsically; C# has no `Slice` on them either.
- **`[<Obsolete>]` or `[<CompilerMessage>]` on `List<'T>.GetSlice`.** Both are warnings, so both break `TreatWarningsAsErrors` builds.
- **From-end bounds on the new paths**, as the draft implementation does with C#'s `^i = length - i` meaning. Deferred to a revision of FS-1076 so that a single document defines from-end indexing and slicing for `Index` indexers, `Range` indexers, `Slice` types and `GetSlice` types, with one meaning of `^i`.
- **Do nothing.** Every user keeps writing the extension member, or the SRTP workaround, per type.

# Prior art

- **C# 8 ranges** ([csharplang proposal](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-8.0/ranges.md)): a type is *countable* when it has an `int` `Length` or `Count` (`Length` preferred); the compiler provides an implicit `Range` indexer for countable types with an instance `Slice(int, int)` and no explicit `Range` indexer; an explicit `this[Range]` wins; extension methods are not considered; `string` is special-cased to `Substring` and arrays to `RuntimeHelpers.GetSubArray`; the receiver and bounds are evaluated once; out-of-range bounds throw. This RFC keeps the precedence and the countable rule, and differs in being end-inclusive, tolerant on the `Slice` path, accepting extension `Slice` members, and treating arrays and strings intrinsically as F# already does.
- **Python** slices are tolerant of out-of-range bounds; FS-1077 adopted that behaviour for F# collections and this RFC extends it to the `Slice` path.
- **FS-1076** introduced `^i` with `GetReverseIndex`; **FS-1077** the tolerant semantics; **FS-1110** the dotless `expr[...]` syntax; **FS-1111** the informational-message mechanism reused here for `GetSlice`.
- The name `Slice` follows the BCL, which in turn follows C++'s `std::slice`, as noted in the suggestion thread.

# Compatibility

**Is this a breaking change?** Not with the feature off. With the feature on, an existing program changes meaning only if a receiver type satisfies protocol 1 or 2 *and* has a `GetSlice` in scope:

| Existing situation | Under this RFC |
|---|---|
| A `GetSlice` extension for a BCL type copied from Microsoft Learn (end-exclusive) | the intrinsic `Slice` is used; `sp[0..3]` now has four elements instead of three |
| A tolerant, end-inclusive `GetSlice` extension for a BCL type | same results, through `Slice`; the extension is dead code |
| A `GetSlice` that returns a different type than `Slice` (a view versus a copy, or a lazy sequence) | the `Slice` result type is used; code depending on the old type fails to compile |
| A `GetSlice` with non-`int` bounds beside an unrelated `Slice(int, int)` | bounds must now be `int`; a non-`int` bound is a type error |
| A type with both a `Range` indexer and `GetSlice` | the indexer is used |

These are language-version gated; once the feature is promoted, they become breaking on an SDK upgrade for the affected code, whose fix is to delete the redundant extension or to write the call explicitly.

**Older compilers and the new source.** `span[1..3]` fails with FS0039 as today. A compiler that implements this RFC but has the feature off reports FS3350 (feature not available in this language version) when protocol 1 or 2 would have applied, instead of FS0039.

**Older compilers and the new FSharp.Core.** The `Range` indexer on `List<'T>` is an ordinary member; `xs[1..3]` continues to elaborate to `GetSlice`, which is retained, and `xs[Range(1, 4)]` can be called explicitly. Binary compatibility is preserved: nothing is removed or changed in signature.

**New compiler and an older FSharp.Core**, or a `netstandard2.0` target: `List<'T>` has no `Range` indexer, no `Slice`, so protocol 3 applies and lists slice as today with no diagnostic.

**Compiled binaries.** The new elaborations are ordinary calls to `get_Item`, `Slice`, `get_Length`/`get_Count` and the `Range`/`Index` constructors; no new FSharp.Core entry point is required by generated code.

# Interop

- **C#.** `FSharpList<T>` gains `this[Range]`, so `fsList[1..3]` and `fsList[^2..]` compile in C# with C# meaning, and `GetSlice` disappears from completion. Types written in F# with `Slice` plus `Length`, or with a `Range` indexer, are sliceable from C# without further work, and vice versa.
- **Future BCL shapes.** `System.Numerics.Tensors` types use `params ReadOnlySpan<NRange>` indexers and `NRange`/`NIndex` values; they are outside this RFC and, as noted on the suggestion thread, depend on suggestion [#1377](https://github.com/fsharp/fslang-suggestions/issues/1377) (interop with C# 13 `params` collections).
- **Visual Basic** has no range syntax; unaffected.

# Pragmatics

## Diagnostics

Numbers are provisional and are assigned at implementation time; 3916 is the first free number at the time of writing.

| Number | Kind | Text (abridged) | When |
|---|---|---|---|
| FS3916 | error | The type '%s' does not support slicing syntax. Slicing requires an indexer taking 'System.Range', a 'Slice' method taking two integers together with an integer 'Length' or 'Count' property, or a 'GetSlice' method. | no protocol applies (replaces the generic FS0039 for slice expressions) |
| FS3917 | error | The type '%s' has more than one 'Slice' method taking two integer parameters, so slicing syntax cannot choose between them. Call the method explicitly. | ambiguous `Slice` |
| FS3918 | info | The use of 'GetSlice' on F# lists is deprecated. Use slicing syntax 'list[start..finish]' instead. | explicit `List<'T>.GetSlice` reference |
| FS3350 | error | Feature 'slicing syntax via System.Range indexers and Slice methods' is not available in F# %s. | feature off, protocol 1 or 2 would apply |
| FS3303 | error | unchanged | from-end bound without `FromEndSlicing` |

Overload-resolution failures inside protocol 1 surface as the usual FS0001/FS0041/FS0501 messages naming `Item`.

## Tooling

- **Hover, go to definition, signature help.** The resolution of `Slice`, `Length`/`Count` or `Item` is recorded at the range of the `..` (or `*`) operator, so hovering the operator shows the member that the slice elaborates to and go-to-definition navigates to it. The existing recording for `GetSlice` is unchanged.
- **Colorization.** Unchanged. The semantic-classification pass keeps suppressing the whole-expression `GetSlice`/`SetSlice` resolution; the new paths record their resolution at the operator range only, so explicit `span.Slice(1, 2)` calls stay classified as method calls.
- **Completion.** `GetSlice` no longer appears on F# lists.
- **Debugging.** Stepping enters `Slice` or the indexer getter like any method call; the compiler-generated locals `r`, `len` and `s` are hidden from the locals window.
- **Error recovery, brace matching.** Unchanged; the syntax is unchanged.

## Performance

- **Compilation.** For a slice expression on a nominal type, two additional name-keyed member lookups (`Item` with a `Range` parameter, then `Slice` and `Length`/`Count`) before the existing `GetSlice` lookup. Both use the cached `InfoReader` tables; no measurable cost is expected.
- **Generated code.** Protocol 1: one `Range` construction (a struct) and one indexer call. Protocol 2: one `Length` call, at most six integer operations and one `Slice` call. Both replace today's `GetSlice` elaboration, which allocates two `option` objects per slice, so existing extension-based slicing gets faster. List slicing keeps its current cost.

## Scaling

- Range arguments per slice expression: hand-written code uses one to four; the compiler accepts as many as the indexer has parameters. Elaboration is linear in the number of arguments.

## Culture-aware formatting/parsing

Not applicable.

# Unresolved questions

- **From-end bounds on the new paths.** A follow-up revision of FS-1076 should define `xs[^i]` for `Index` indexers, `^i` inside ranges for `Range` indexers (as `Index(i + 1, fromEnd = true)` for a start and `Index(j, fromEnd = true)` for an end, keeping F#'s `^0` = last element), a length-based `^i` for `Slice` types, and the role of `GetReverseIndex`, in one place.
- **`Item(Index)` on `list<'T>`** and general `System.Index` support (suggestion #1044) are out of scope here.
- **Extension `Length`/`Count`.** This RFC follows C# in requiring an intrinsic property; allowing extension properties would let a type be made sliceable entirely from outside.
- **Promoting FS3918.** Whether, and when, the informational message becomes a warning, and whether a definition-site hint should suggest `Slice` to authors of one-dimensional `GetSlice` members.
- **`SetSlice` versus an indexer setter.** This RFC prefers the `Range` indexer setter when both exist, following the get side; the F# team may prefer `SetSlice` for compatibility.

# Appendix: implementation sketch (dotnet/fsharp)

File references are to `dotnet/fsharp` `main` at the time of writing (September 2026).

- **Language feature** — `src/Compiler/Facilities/LanguageFeatures.fs(i)`: `SliceMethodSlicing`, `previewVersion`; `src/Compiler/FSComp.txt`: `featureSliceMethodSlicing` and the three messages above.
- **Checker** — `src/Compiler/Checking/Expressions/CheckExpressions.fs`, `TcIndexingThen`: after the intrinsic array/string dispatch and before the hard-coded `"GetSlice"`/`"SetSlice"` lookups, when the feature is on, the receiver is a nominal non-array non-string type and `DecodeIndexArgs` reports no from-end bound: (1) look for `Item`/`DefaultMember` property getters through `AllPropInfosOfTypeInScope` whose parameter list matches the argument shape with `System.Range` at range positions (compared by full type name); build the `Range` arguments from the constructors and properties of the parameter's `Range` type and its `Index` type, then reuse the existing `DelayedDotLookup`/`DelayedApp` chain for `Item`; (2) otherwise, for a single range argument on a get, look for `Slice` through `AllMethInfosOfTypeInScope` filtered by shape and `IsExtensionMethCompatibleWithTy`, and `Length`/`Count` through `TryFindIntrinsicPropInfo`; emit the elaboration with `BuildMethodCall` and `mkCompGenLet`, comparisons through `mkCond`; (3) otherwise fall through to `GetSlice`, reporting FS3916 instead of FS0039 when the member is absent and FS3350 when the feature is off but (1) or (2) would have applied. The from-end rewrite in `ExpandIndexArgs` stays as it is; the new paths are skipped whenever it would fire.
- **Name-resolution sink** — record `Item.Property`/`Item.MethodGroup` for the chosen member at the `..` operator range (`SynExpr.IndexRange` carries it) so hover and go-to-definition work; `src/Compiler/Service/SemanticClassification.fs` is left unchanged.
- **Deprecation message** — in the member-application path that resolves an explicit `.GetSlice` on `FSharpList<_>` (`tyconRefEq` against `g.list_tcr_canon`, logical name `GetSlice`, non-synthetic range), report FS3918 with `informationalWarning` when the feature is on.
- **FSharp.Core** — `src/FSharp.Core/prim-types.fs`/`.fsi`, `List<'T>`: the `Item(range: Range)` getter under `#if NETSTANDARD2_1_OR_GREATER || NET`, implemented with the existing `PrivateListHelpers.sliceSkip`/`sliceTake`; `[<EditorBrowsable(EditorBrowsableState.Never)>]` and the doc remark on `GetSlice`; surface-area baselines `tests/FSharp.Core.UnitTests/FSharp.Core.SurfaceArea.netstandard21.*.bsl` gain the new getter.
- **Tests** — `tests/FSharp.Compiler.ComponentTests/Conformance/Expressions/SyntacticSugar/`: BCL types on the `Slice` path, `Count`-only types, `int64` `Length` rejected, extension `Slice`, ambiguous `Slice`, one- and two-dimensional `Range` indexers with and without setters, precedence (indexer over `Slice` over `GetSlice`), feature-off behaviour, evaluation order and single evaluation of the receiver and bounds, from-end bounds falling back to `GetSlice`; `tests/fsharp/Compiler/Language/SlicingQuotationTests.fs` list baselines; `tests/FSharp.Core.UnitTests` cases for `List<'T>.Item(Range)` including from-end `Index` values; a classification test that `span.Slice(1, 2)` stays a method.
- **Release notes and docs** — `docs/release-notes/.Language/preview.md`, `docs/release-notes/.FSharp.Core/<next>.md`, `docs/release-notes/.FSharp.Compiler.Service/<next>.md`; the Microsoft Learn "Slices" page should present `Slice` plus `Length` and `Range` indexers as the way to make a type sliceable, remove the end-exclusive `Span` extension, and keep `GetSlice` for non-integer bounds.
