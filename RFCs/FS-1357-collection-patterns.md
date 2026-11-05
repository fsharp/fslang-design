# F# RFC FS-1357 - Collection patterns

The design suggestion [Collection patterns](https://github.com/fsharp/fslang-suggestions/issues/1478) is not yet approved in principle. This RFC is a draft for discussion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1478)
- [ ] Approved in principle
- [ ] Implementation
- [ ] Discussion

# Summary

List and array patterns apply to other collection types when the compiler knows the type of the scrutinee. A type with `IsEmpty`, `Head` and `Tail` matches `[]`, `[a; b]` and `x :: xs`. A type with `Length` or `Count` and an `int` indexer matches `[| |]` and `[| a; b |]`. A new spread element `...pat` in a bracket pattern binds the remaining elements as a slice. The patterns compile to member calls and integer tests. The compiled code has no new node and no new metadata.

# Motivation

Code that matches an `ImmutableArray<'T>`, a `ReadOnlySpan<char>` or a lazy list needs an active pattern for each type. An active pattern cannot return a `Span` or a `ReadOnlySpan`, because `option`, `voption` and `Choice` are not byref-like (error FS3301). It allocates its result unless it is a struct partial pattern. [FS-1094](FS-1094-immarray.md) leaves the patterns of `immarray` to this extension (unresolved question 4). Suggestion [#619](https://github.com/fsharp/fslang-suggestions/issues/619) names "list pattern syntax against indexable data-structures" as the condition for matching without a copy. [FS-1342](https://github.com/fsharp/fslang-design/pull/838) gives the construction side, target-typed `[ ]`, and does not change patterns.

```fsharp
let rec sum (xs: ReadOnlySpan<int>) =
    match xs with
    | [| |] -> 0
    | [| x; ...rest |] -> x + sum rest      // rest: ReadOnlySpan<int>, no copy
```

# Detailed design

## Syntax

The list and array pattern grammar gets a spread element:

```
pat          := ... | [ pat-elements ] | [| pat-elements |]
pat-elements := pat-element ; ... ; pat-element
pat-element  := pat | ... pat
```

A bracket pattern MUST contain at most one spread element. The token `...` is `DOT_DOT_DOT` of [FS-1151](FS-1151-record-spreads.md). A spread element is valid only inside `[ ]` and `[| |]`.

## Type-directed resolution

`T` is the type of the scrutinee after inference up to the pattern.

1. `T` is an inference variable: the pattern keeps its current meaning. `[ ]` and `::` unify `T` with `'a list`, and `[| |]` unifies `T` with `'a[]`.
2. `T` is `'a list`: `[ ]` and `::` compile as today. `[| |]` is an error, as today.
3. `T` is `'a[]`: `[| |]` compiles as today, with the spread rule of protocol 2.
4. Otherwise: `::` uses protocol 1. `[| |]` uses protocol 2. `[ ]` uses protocol 1 when `T` has it, and protocol 2 otherwise.

A type with both protocols gets protocol 1 for `[ ]` and `::`, and protocol 2 for `[| |]`. The resolution does not depend on the other clauses of the match. A pattern in `function` or `fun` gets an inference variable unless an annotation or the context gives the type.

## Protocol 1: head and tail

`T` has protocol 1 when it has the instance or extension properties `IsEmpty: bool`, `Head: 'E` and `Tail: T2`.

| Pattern | Test | Sub-patterns |
| --- | --- | --- |
| `[]` | `x.IsEmpty` | none |
| `p :: ps` | `not x.IsEmpty` | `p` on `x.Head`, `ps` on `x.Tail` |
| `[p1; …; pn]` | as `p1 :: … :: pn :: []` | each tail level uses the protocol of its own type |

`T2` MAY differ from `T`. `LazyList<'T>` (FSharpx) forces one cell per level:

```fsharp
let rec takeWhilePositive (xs: LazyList<int>) =
    match xs with
    | x :: rest when x > 0 -> x :: takeWhilePositive rest
    | _ -> []
```

## Protocol 2: length and index

`T` has protocol 2 when it has an instance property `Length: int` or `Count: int` and an `int` indexer. `Length` wins when both properties exist. The indexer is the property that F# resolves for `x[i]`: `Item`, or the property that `DefaultMemberAttribute` names (`string.Chars`).

For `n` element patterns before a spread, `j` element patterns after it, and the spread `ps`:

| Pattern | Test | Sub-patterns |
| --- | --- | --- |
| `[| p0; …; pn-1 |]` | `x.Length = n` | `pi` on `x[i]` |
| `[| p0; …; pn-1; ...ps; q0; …; qj-1 |]` | `x.Length >= n + j` | `pi` on `x[i]`, `qi` on `x[x.Length - j + i]`, `ps` on the slice at `n` of length `x.Length - n - j` |

The compiler evaluates `x.Length` once for the exact-length tests and once for the spread tests of one scrutinee.

```fsharp
let parse (s: string) =
    match s with
    | [| '-'; ...digits |] -> -(Int32.Parse digits)   // digits: ReadOnlySpan<char>
    | _ -> Int32.Parse s
```

### The slice of a spread

The slice expression is the first rule that applies:

1. `T` is `Span<'E>` or `ReadOnlySpan<'E>`: `x.Slice(start, length)`.
2. `T` is `'E[]`: `Span<'E>(x, start, length)`. `T` is `string`: `MemoryExtensions.AsSpan(x, start, length)`. `T` has an instance method `AsSpan: unit -> Span<'E>` or `AsSpan: unit -> ReadOnlySpan<'E>`: `x.AsSpan().Slice(start, length)`.
3. `T` has an instance or extension method `Slice: int * int -> 'S`: `x.Slice(start, length)`.
4. `T` has a `GetSlice` method that F# accepts for `x[a..b]`: `x[start .. start + length - 1]`.
5. Otherwise a spread element is an error. The patterns without a spread stay valid.

The slice has the return type of that member, and the pattern adds no cost to that member. Rule 2 copies nothing. `ImmutableArray<'T>.Slice` and `List<'T>.Slice` copy; `ImmutableArray<'T>` has `AsSpan`, so rule 2 applies to it. The compiler MUST NOT search extension methods for `AsSpan`, so the slice type does not depend on `open` declarations. Rule 2 for arrays and strings needs `System.Span` in the target framework.

## Null

A collection pattern reads a member of the scrutinee. A null scrutinee throws `NullReferenceException`, as `x.Length` does. With nullness checking, a scrutinee of type `T | null` gets the nullness warning of a member access. A `null` pattern before the collection patterns removes the warning.

## Exhaustiveness and redundancy

- Protocol 1: `[]` and `_ :: _` together are complete, because the test is a boolean.
- Protocol 2: the compiler treats an exact length as an integer constant and a spread pattern as a `Length >= n` test. `[| |]` and `[| _; ..._ |]` together are complete. A counterexample for a missing length prints as an array pattern of unknowns, for example `[|_; _|]`.
- The compiler MAY not report warning FS0026 for a clause that only an exact-length test and a spread test together make unreachable. Example: the third clause of `[| |] | [| a; ...r |] | [| x; y |]`.

## Byref-like slices

A slice of rule 1 or 2 is byref-like. The bound variable is a local of the match target. The existing rules for byref-like locals apply: no capture by a closure, no storage in a field, no escape by return. So a `match` in a `seq`, `async` or `task` expression cannot capture the slice in its target.

## Lowering

Each collection pattern lowers to a `let` of the test value (`x.IsEmpty`, `x.Length`) and a switch on a boolean or integer constant. The elements come from member calls, the spread from one slice call. The lowering adds no typed-tree node, decision-tree test or FSharp.Core type. A quotation of a `match` with collection patterns SHOULD contain those calls and tests.

# Changes to the F# spec

- Patterns chapter, list and array patterns: add the spread element to the grammar; add the type-directed resolution and protocols 1 and 2 with the slice rules.
- Patterns chapter, exhaustiveness: add the completeness rules of this RFC.
- Byref-like types (FS-1053): a slice of rule 1 or 2 is a byref-like local.

# Drawbacks

- The meaning of `[ ]` and `[| |]` depends on the known type. In `function` and `fun` the pattern is the first mention of the type, so the list and array defaults apply there; a protocol applies only when an annotation or the context gives the type first.
- The rest of an array has type `Span<'T>`, not `'T[]`. A recursive function over the rest takes a span.
- Three name-based protocols (`IsEmpty`/`Head`/`Tail`, `Length`/indexer, slice members), as `GetEnumerator` and `GetSlice` today.
- A type whose `Slice` copies makes `...rest` copy.
- The pattern-match compiler gets a new path kind for member-based tests.

# Alternatives

- Active patterns, the status quo: one per type, no byref-like result, an allocation per match, no completeness for partial patterns.
- `x :: xs` on indexable types ([#1088](https://github.com/fsharp/fslang-suggestions/issues/1088)): `xs` is a copy, or a span of another type than `x`. The suggestion excludes it.
- A rest that copies, with the type of the scrutinee (C#, `RuntimeHelpers.GetSubArray`): O(n) per level, so a recursion over the rest is O(n²).
- A new bracket form, `[: … :]` of the earlier FS-1094 text: a third syntax for one type, withdrawn in [discussion #528](https://github.com/fsharp/fslang-design/discussions/528).
- Byref-like results for active patterns: gives spans, but keeps one pattern per type and no completeness.
- "Null never matches" (C#): three outcomes per test, so `[] | _ :: _` becomes incomplete.

# Prior art

- [C# 11 list patterns](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/list-patterns.md): `Length` or `Count` and an `int` or `Index` indexer; a slice through a `Range` indexer or `Slice(int, int)`; one `..` at any position; `null` does not match; the slice of an array is a copy.
- [Rust slice patterns](https://doc.rust-lang.org/reference/patterns.html#slice-patterns) `[first, rest @ ..]` on `&[T]`: `rest` is a sub-slice without a copy, as the spans of this RFC.
- Scala `unapplySeq`: one extractor method per type, the active-pattern approach.
- F# `for … in` (`GetEnumerator`), slicing (`GetSlice`) and `use` (`Dispose`) resolve members by name.

# Compatibility

- Not a breaking change: a program that the feature accepts is a type error today, and a program that type-checks today keeps its elaboration.
- A previous compiler reports a syntax error on `...` in a pattern, and a type error on a collection pattern for a type that is not a list or an array.
- Compiled binaries contain member calls, `let` bindings and constant switches. A previous compiler consumes them, also in inline functions.
- FSharp.Core does not change.

# Interop

- C# sees ordinary member calls. The protocols accept members from any .NET language. `ImmutableArray<T>`, `ArraySegment<T>`, `Span<T>`, `ReadOnlySpan<T>`, `string` and `IReadOnlyList<T>` qualify without a change.
- C# list patterns on F# types do not change.

# Pragmatics

## Diagnostics

New errors:

- `T` has no `IsEmpty`, `Head` and `Tail`, and no `Length` or `Count` with an indexer. The message names the missing members.
- A spread element on a type without a slice member (rule 5 of the slice rules).
- More than one spread element in a bracket pattern.
- A spread element outside `[ ]` and `[| |]`.

Existing diagnostics apply: byref-like escape errors, nullness warnings, and FS0025 with a counterexample.

## Tooling

- FSharp.Compiler.Service reports the resolved `IsEmpty`, `Head`, `Tail`, `Length`, indexer and slice members as symbol uses at the pattern, as for `GetSlice` today. Hover on a spread variable shows the slice type. Go to Definition on `...` goes to the slice member.
- Debugging: the bound variables are locals; breakpoints and stepping work as for other patterns.
- Colorization and brace matching: `...` is an existing token, and the bracket forms exist.

## Performance

- Existing code: no change. The new resolution runs only for a scrutinee that is not a list or an array.
- New patterns: `Length` twice per scrutinee at most, one indexer call per element pattern, one slice call per spread, no allocation unless the member allocates. Protocol 1 evaluates `IsEmpty`, `Head` and `Tail` once per level.
- Compilation: linear in the number of clauses and element patterns.

## Scaling

- Element patterns in one bracket pattern: hand-written up to 10; accepted up to 1 000, as for array patterns today.
- Clauses with collection patterns in one match: hand-written up to 50; accepted up to 1 000.

## Culture-aware formatting/parsing

None. `Length` and indices are integers; the feature parses and formats no text.

# Unresolved questions

1. A spread in any position, or only as the last element.
2. Extension members for `Length`, `Count` and the indexer. Protocol 1 and the slice rules accept extensions.
3. `Length: int64` and `nativeint`.
4. `[ ]` on protocol 2, or only `[| |]`.
5. `null`: member semantics (this text) or the C# rule.
