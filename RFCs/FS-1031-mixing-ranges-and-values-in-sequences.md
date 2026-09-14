# F# RFC FS-1031 - Mixing ranges and values to construct sequences

The design suggestion [Mixing ranges and values to construct sequences](https://github.com/fsharp/fslang-suggestions/issues/1031) has been marked "approved in principle."

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1031)
- [x] Approved in principle
- [ ] Implementation: [dotnet/fsharp#18670](https://github.com/dotnet/fsharp/pull/18670) was a prototype; it was closed without merging on 2025-09-25 and a new implementation is required
- [x] [Discussion](https://github.com/fsharp/fslang-design/discussions/803)

> **Revision history.** This RFC was first merged in August 2025 ([fslang-design#804](https://github.com/fsharp/fslang-design/pull/804)). It was amended in September 2026 to record the decisions reached in the discussion thread and during review of the prototype, and to add the sections of the current RFC template. Places where the amended design deliberately differs from the prototype are marked **Differs from the prototype**.

# Summary

In list, array and sequence expressions, and in computation expressions whose builders support `yield!`, a range expression `e1 .. e2` or `e1 .. e2 .. e3` may be written directly as an element. It is spliced into the result exactly as `yield! (e1 .. e2)` would be. The explicit form `yield! e1 .. e2` is also permitted.

```fsharp
let a = seq { 1..10; 19 }
let b = [ -3; 1..10 ]
let c = [| -3; 1..10; 19 |]
```

# Motivation

Today a range can only be spliced into a larger collection expression through `yield!` of an already-constructed collection:

```fsharp
let a = seq { yield! seq { 1..10 }; 19 }
let b = [ -3; yield! [ 1..10 ] ]
let c = [| -3; yield! [| 1..10 |]; 19 |]
```

This is

- **Verbose**: `yield! seq { ... }` or `yield! [ ... ]` is scaffolding around the only interesting part, the range.
- **Inconsistent**: `[ 1..10 ]` is already a range splice when the range is the only element. Adding a second element forces the author to rewrite the range in a different form.
- **Wasteful**: for `[ ... ]` and `[| ... |]` the inner collection is materialised only to be copied into the outer one.

The pattern is common in real code. The following examples were found in public repositories.

Base64 alphabet (WebSharper, `src/compiler/WebSharper.Core.JavaScript/Writer.fs`):

```fsharp
// Before
let base64Digits =
    lazy [|
        yield! seq { 'A' .. 'Z' }
        yield! seq { 'a' .. 'z' }
        yield! seq { '0' .. '9' }
        yield '+'
        yield '/'
    |]

// After
let base64Digits =
    lazy [|
        'A' .. 'Z'
        'a' .. 'z'
        '0' .. '9'
        '+'
        '/'
    |]
```

Character tables in the F# compiler's own lexer generator (`buildtools/fslex/fslexast.fs` in dotnet/fsharp, lightly reformatted):

```fsharp
// Before
Set.ofList [ yield! seq { char 0 .. char (numLowUnicodeChars - 1) }
             yield! GetSpecificUnicodeChars() ]

// After
Set.ofList [ char 0 .. char (numLowUnicodeChars - 1)
             yield! GetSpecificUnicodeChars() ]
```

Test data made of disjoint intervals (Brotli-Builder, `UnitTests/Brotli/TestElements.fs`):

```fsharp
// Before
let values = seq {
    yield! seq { 0..65536 } // all values that fit into 0-4 nibbles
    yield 65537             // 5 nibbles
    yield 1048576           // 5 nibbles
}
let insertRange = seq { yield! seq { 2114..2200 }; yield! seq { 6100..6209 } }

// After
let values = seq {
    0..65536 // all values that fit into 0-4 nibbles
    65537    // 5 nibbles
    1048576  // 5 nibbles
}
let insertRange = seq { 2114..2200; 6100..6209 }
```

Row bookkeeping in a game board (Avalonia.FuncUI Tetris example, `Tetris.Core.fs`, abbreviated):

```fsharp
// Before
[ yield! [ config.height - 1 .. -1 .. config.height - 3 ]
  for y in config.height - 4 .. -1 .. 0 do
      ...
      yield y ]

// After
[ config.height - 1 .. -1 .. config.height - 3
  for y in config.height - 4 .. -1 .. 0 do
      ...
      yield y ]
```

Other typical uses are sets of codes with holes (`[ 200..299; 304 ]`), port lists (`[ 22; 80; 443; 8000..8100 ]`), character classes in hand-written lexers, and year or version ranges with gaps.

# Detailed design

## Terminology

A *range expression* is the syntactic form `e1 .. e2` or `e1 .. e2 .. e3` **without enclosing parentheses**. `(e1 .. e2)` is not a range expression in this sense; see [Rule 5](#rule-5-parenthesised-ranges).

A *collection expression* below means a list expression `[ ... ]`, an array expression `[| ... |]`, a sequence expression `seq { ... }`, or a computation expression `builder { ... }`.

## Rule 1: a range expression in element position is a splice

Wherever a collection expression currently accepts a plain expression `e` as an element, that is, wherever the compiler would consider inserting an implicit `yield e` (see [FS-1069 Implicit yields](../FSharp-4.7/FS-1069-implicit-yields.md)), a range expression is also accepted and is treated as `yield! (e1 .. e2)`:

```fsharp
[ -3; 1..10; 19 ]          // = [ yield -3; yield! (1..10); yield 19 ]
[| 0; 2..2..10; 15 |]      // = [| yield 0; yield! (2..2..10); yield 15 |]
seq { 'a'..'z'; '_' }      // = seq { yield! ('a'..'z'); yield '_' }
```

This applies uniformly to every position where an element may appear, in particular inside branches and loops, mirroring how implicit `yield` already behaves for plain values:

```fsharp
let f p = [ if p then 1 else 1..10 ]                   // if/else: both branches are elements
let g x = [ match x with Some n -> 1..n | None -> 0 ]  // match arms
let h xs = [ for x in xs do x..x+2 ]                   // for body
let k () = [ let lo = 1 in lo..5; 10 ]                 // let body
let m xs = [ 0; for x in xs do if x > 0 then x..x*2 ]  // nested
```

Elements are separated by `;` or by a newline, as today:

```fsharp
let d =
    [ 1..3
      10
      20..2..30 ]
```

**Differs from the prototype.** The prototype only rewrote ranges at the top level of an element list and inside `if`/`match` branches; ranges inside `for`, `while`, `try`, `let` and `use` bodies were still rejected. The rule above requires the splice to be recognised everywhere an element may appear.

## Rule 2: range splices do not participate in the implicit-yield decision

FS-1069 inserts implicit `yield`s only when a collection expression contains no explicit `yield` or `yield!`; otherwise a plain expression in element position is a statement whose value is discarded (warning FS3221). A range expression in element position is **always** a splice, and it is **not** counted as an explicit `yield` when deciding whether other plain expressions are implicitly yielded:

```fsharp
[ 1; 2..3 ]             // yield-free: 1 is yielded, 2..3 is spliced        → [1; 2; 3]
[ yield 1; 2..3 ]       // not yield-free: 2..3 is still spliced             → [1; 2; 3]
[ yield 1; 2..3; 4 ]    // 4 is a discarded statement, warning FS3221        → [1; 2; 3]
[ 1..3; yield! xs ]     // a range splice next to an explicit yield!         → 1, 2, 3, xs...
```

Rationale: a range has no side effects and its value can never be meaningfully discarded, so treating a bare range as a statement would only ever produce a warning and a silently missing slice of data. Treating it as a splice is always what the author meant. Keeping the existing rule for plain values avoids changing the meaning of any program that compiles today.

**Differs from the prototype.** When a top-level element list contained both a range and an explicit `yield!`, the prototype rewrote *every* plain element as `yield`, so `[ yield! xs; 1..3; 4 ]` yielded `4` whereas `[ yield! xs; 4 ]` did not. Rule 2 keeps plain values under the existing rule.

This also answers the first question raised in the discussion: `[ if p then 1 else 1..10 ]` is accepted (both branches are elements of a yield-free expression), and `[ if p then yield 1 else 1..10 ]` is accepted as well (the range is spliced, the `yield` is explicit).

## Rule 3: explicit `yield!` of a range

`yield! e1 .. e2` and `yield! e1 .. e2 .. e3` are accepted wherever `yield!` is accepted, with the same meaning as the implicit splice:

```fsharp
[ yield! 1..10 ]                                    // = [ 1..10 ]
seq { if p then yield! 1..10 else yield 0 }
[ match x with Some n -> yield! 1..n | None -> () ]
```

This is the form to reach for when a splice sits in a position where an implicit form reads badly, or purely for emphasis. It was agreed in the discussion and is supported by the prototype.

## Rule 4: `yield e1 .. e2` and `return e1 .. e2` are errors

A range expression is not a value. `yield 1..10` and `return 1..10` are rejected with a dedicated diagnostic rather than being interpreted as "yield the sequence produced by the range as a single element":

```
error FSxxxx: A range expression cannot be used as a single value here. Use 'yield! 1..10' to splice the range into the collection, or '[ 1..10 ]' / 'seq { 1..10 }' to use it as one value.
```

Rationale: outside element position a range expression has no meaning in F# today (`let r = 1..10` is an error), and this RFC should not create one. Keeping `yield 1..10` an error leaves open the possibility of giving range expressions a first-class meaning later (see [Unresolved questions](#unresolved-questions)).

**Differs from the prototype.** The prototype rewrote `yield 1..10` to `yield ((..) 1 10)`, i.e. it yielded the whole range as one `seq<int>` element, which was then usually a type error against the element type.

## Rule 5: parenthesised ranges

`( e1 .. e2 )` is not a range expression and is not a splice. It remains an error, exactly as today:

```fsharp
[ 1; (2..5) ]   // error FS0751, unchanged
```

Rationale: this reserves the parenthesised form. Should F# ever give `e1 .. e2` a first-class meaning (for example a `System.Range`-like value, a question raised in the suggestion thread in connection with C#'s `var r = 1..10;`), the parenthesised form is available for it without conflicting with the splice meaning fixed by this RFC.

## Rule 6: the `(..)` and `(.. ..)` operators in scope are used

The splice `e1 .. e2` elaborates to `yield! ((..) e1 e2)`, and `e1 .. e2 .. e3` to `yield! ((.. ..) e1 e2 e3)`, where `(..)` and `(.. ..)` are resolved by ordinary name resolution at the point of the splice. This is exactly how `[ e1 .. e2 ]`, `seq { e1 .. e2 }` and `for x in e1 .. e2 do ...` already resolve the operators today.

Consequently:

- Any element type for which `(..)` is defined works: `int`, `int64`, `char`, `bigint`, `float`, `decimal`, values with units of measure, and so on. Steps may be negative; an empty range such as `5..4` contributes no elements.
- A user-defined `(..)` in scope participates in the splice. Given the example from the suggestion thread:

  ```fsharp
  type X(elements: X list) =
      member _.Elements = elements
      interface IEnumerable<X> with
          member this.GetEnumerator() = (this.Elements :> IEnumerable<X>).GetEnumerator()
          member this.GetEnumerator() : IEnumerator = (this.Elements :> IEnumerable).GetEnumerator()
      static member Combine(x1: X, x2: X) = X(x1.Elements @ x2.Elements)

  let (..) a b = seq { X.Combine(a, b) }
  ```

  `[ a..b; c ]` means `[ yield! ((..) a b); yield c ]`, which type-checks because the custom `(..)` returns `seq<X>` and `c : X`. This is not a breaking change: `[ a..b; c ]` is an error in every shipped compiler. A splice is well-typed whenever the operator returns `seq<'T>` (or a subtype) for element type `'T`; otherwise the usual type error is reported at the range.

The previous revision of this RFC stated that the feature "does not interfere with custom implementations of the range operator" because the rewrite is syntactic. That was misleading: the rewrite *is* syntactic, but the operator it calls is whichever `(..)` is in scope, so custom operators are honoured, not bypassed. This is the same behaviour that `[ e1 .. e2 ]` has always had.

## Rule 7: computation expressions

For a computation expression `b { ... }` the splice is translated with the standard rule for `yield!`:

```
T(e1 .. e2, V, C, q)        = C(b.YieldFrom(src((..) e1 e2)))
T(e1 .. e2 .. e3, V, C, q)  = C(b.YieldFrom(src((.. ..) e1 e2 e3)))
```

The builder must define `YieldFrom`; if it does not, the standard error is reported:

```
error FS0708: This control construct may only be used if the computation expression builder defines a 'YieldFrom' method
```

Sequencing a splice with other elements requires `Combine` and `Delay`, as for any other multi-statement computation expression. No new builder method is introduced.

```fsharp
type ArrayBuilder() =
    member _.Yield(x) = [| x |]
    member _.YieldFrom(xs: seq<_>) = Seq.toArray xs
    member _.Zero() = [||]
    member _.Combine(a: int array, b: unit -> int array) = Array.append a (b ())
    member _.Delay(f) = f
    member _.Run(f) = f ()

let array = ArrayBuilder()
let xs = array { -3; 1..10; 19 }   // [| -3; 1; 2; 3; 4; 5; 6; 7; 8; 9; 10; 19 |]
```

**Differs from the prototype.** When the builder lacked `YieldFrom`, the prototype fell back to `b.Yield(range)`, silently yielding the whole range as one element. That fallback is dropped: a builder that cannot splice gets an error, the same as for an explicit `yield!`.

Whether the translation should instead, or additionally, go through `For`, is listed under [Unresolved questions](#unresolved-questions).

## Rule 8: forms that are out of scope

- The builder-less form `{ 1..10; 19 }` is not supported. Omitting `seq` is deprecated ([FS-1033](../FSharp-10.0/FS-1033-Deprecate-places-where-seq-can-be-omitted.md)) and the plain `{ e1 .. e2 }` form keeps its current elaboration.
- The existing single-range forms `[ e1 .. e2 ]`, `[| e1 .. e2 |]` and `seq { e1 .. e2 }` keep their current, direct elaboration; this RFC does not change their meaning or code generation.
- Slicing syntax `xs[a..b]` and `xs.[a..b]` is unaffected; a range expression is only a splice in element position of a collection expression.

## Constant array literals

Arrays whose elements are all `byte` or `uint16` constants are compiled to a data blob. A mixed literal such as `[| 0uy; 1uy..3uy |]` is not all-constant and takes the ordinary comprehension path. Implementations may fold a constant integral range into the blob but are not required to.

# Changes to the F# spec

References are to [`spec/expressions.md`](https://github.com/fsharp/fslang-spec/blob/main/spec/expressions.md) of the F# language specification.

1. **[Computation expressions](https://github.com/fsharp/fslang-spec/blob/main/spec/expressions.md#computation-expressions).** Add a production to `comp-expr` and allow `range-expr` as the operand of `yield!`:

   ```fsgrammar
   comp-expr :=
       ...
       yield! expr                     -- yield computation
       yield! range-expr               -- yield range (new)
       ...
       range-expr                      -- range splice, implicit yield! (new)
       expr
   ```

   and add the translation rules

   ```
   T(e1 .. e2, V, C, q)               = C(b.YieldFrom(src((..) e1 e2)))
   T(e1 .. e2 .. e3, V, C, q)         = C(b.YieldFrom(src((.. ..) e1 e2 e3)))
   T(yield! e1 .. e2, V, C, q)        = C(b.YieldFrom(src((..) e1 e2)))
   T(yield! e1 .. e2 .. e3, V, C, q)  = C(b.YieldFrom(src((.. ..) e1 e2 e3)))
   ```

2. **[Sequence expressions](https://github.com/fsharp/fslang-spec/blob/main/spec/expressions.md#sequence-expressions).** No change beyond the `comp-expr` production; the `SeqBuilder` model already has `YieldFrom`.

3. **[Range expressions](https://github.com/fsharp/fslang-spec/blob/main/spec/expressions.md#range-expressions).** Add: "A range expression may also appear as an element of a list, array, sequence or computation expression, where it denotes the splice of the generated sequence into the enclosing expression (see Computation expressions). A parenthesised range `( e1 .. e2 )` is not a range expression."

4. **[Lists via sequence expressions](https://github.com/fsharp/fslang-spec/blob/main/spec/expressions.md#lists-via-sequence-expressions)** and **[Arrays sequence expressions](https://github.com/fsharp/fslang-spec/blob/main/spec/expressions.md#arrays-sequence-expressions).** Add examples `[ e; e1 .. e2 ]` and `[| e; e1 .. e2 |]`; the elaboration to `Seq.toList (seq { cexpr })` and `Seq.toArray (seq { cexpr })` is unchanged.

5. **Implicit yields ([FS-1069](../FSharp-4.7/FS-1069-implicit-yields.md)).** State that a `range-expr` in element position is neither an explicit `yield` nor a statement for the purposes of the yield-free rule (Rule 2).

# Drawbacks

- One more implicit rule in collection expressions, on top of implicit `yield`.
- The unparenthesised `e1 .. e2` in element position permanently means "splice". A future first-class range type must use another spelling in that position; Rule 5 keeps `(e1 .. e2)` free for it.
- `[ yield 1; 2..3; 4 ]` splices the range but discards `4`. This follows from keeping the existing FS-1069 rule for plain values, but may surprise readers who see the range and the value as the same kind of thing.

# Alternatives

- **Splice only at the top level of the element list**, requiring explicit `yield!` inside `if`, `match` and `for`. Proposed in the discussion as a way to sidestep questions about nesting. Rejected: `[ if p then 1 ]` already inserts an implicit `yield` inside the branch, and having ranges behave differently from values in the same position would be a new inconsistency.
- **Treat bare ranges exactly like bare values**, i.e. splice only in yield-free expressions and otherwise treat the range as a discarded statement. Rejected: a discarded range is never intentional, and `[ 1..3; yield! xs ]` is one of the main motivating shapes.
- **Fall back to `Yield` when the builder has no `YieldFrom`** (prototype behaviour). Rejected: it silently turns a splice into a single element.
- **A general spread operator** (`...e`, [fslang-suggestions#1253](https://github.com/fsharp/fslang-suggestions/issues/1253)) would cover splicing any sequence, not only ranges. This RFC is a narrow syntactic convenience that does not conflict with it: `[ 1; 2; 3..5 ]` is defined as `[ 1; 2; yield! [ 3..5 ] ]`, and a spread design may independently define `[ 1; 2; ...[ 3..5 ] ]` to mean the same thing.
- **Do nothing.** `yield! [ e1 .. e2 ]` remains available; the cost is the verbosity and the intermediate collection described under Motivation.

# Prior art

- **Ruby**: a range can be splatted into an array literal: `[-3, *1..10, 19]`.
- **Python**: PEP 448 unpacking: `[-3, *range(1, 11), 19]`.
- **C# 12** collection expressions have a spread element: `int[] xs = [-3, ..Enumerable.Range(1, 10), 19];`.
- **Haskell**: `[-3] ++ [1..10] ++ [19]`.
- **Kotlin**: `listOf(-3) + (1..10) + 19`.

F# is unusual in already having a dedicated range syntax inside collection brackets; this RFC lets that syntax compose with other elements instead of only standing alone.

# Compatibility

* **Is this a breaking change?** No. Every form this RFC makes legal is rejected by all shipped compilers with error FS0751 ("Incomplete expression or invalid use of indexer syntax"). No program that compiles today changes meaning.
* **Previous compilers on new source code.** The parser already accepts the syntax; the type checker reports FS0751 at the range. A new compiler with an older `--langversion` reports FS3350 ("Feature '...' is not available in F# x.y. Please use language version ... or greater") at the range instead.

  Diagnostics for code that does *not* use the feature must not change under any language version. The prototype changed the expected output of an existing test (`tests/fsharp/typecheck/sigs/version46/neg24.bsl`, where an FS0793 error for `seq { (if true then 1 else 2) }` disappeared); an implementation must avoid such side effects.
* **Previous compilers on compiled binaries.** No effect: the feature is entirely a compile-time elaboration.
* **FSharp.Core.** No changes; the existing `(..)` and `(.. ..)` operators are used.

# Interop

None. The feature changes no emitted metadata or public surface; other .NET languages see ordinary lists, arrays and sequences.

# Pragmatics

## Diagnostics

- Feature not enabled: FS3350 at the range, once per range.
- `yield e1 .. e2` or `return e1 .. e2`: a dedicated error pointing at `yield!` and at `[ e1 .. e2 ]` / `seq { e1 .. e2 }` for the single-value reading (Rule 4).
- Element type mismatch between a range and other elements: the usual FS0001 at the range, as for `yield! [ e1 .. e2 ]` today.
- Builder without `YieldFrom`: FS0708 naming `YieldFrom` (Rule 7).
- A plain value discarded next to a range splice in a non-yield-free expression (`[ yield! xs; 1..3; 4 ]`): warning FS3221, unchanged.

## Tooling

- **Parsing and error recovery.** No parser change; the syntax tree already contains the range node, so colorization, brace matching, formatting and outlining are unaffected, including when the feature is disabled.
- **Tooltips and navigation.** Hovering `..` in a splice resolves to the `(..)` or `(.. ..)` operator, as it does for `[ e1 .. e2 ]` today. Go To Definition on a custom operator works the same way.
- **Debugging.** A splice gets the same debug points as `yield!`: one per element expression, stepping into the range operator if it is user-defined. Nothing new for the expression evaluator or locals display.
- **Auto-complete.** No change.

## Performance

- **Existing code.** None: the new rule only triggers on syntax that is currently an error, and the check is a syntactic test on element expressions.
- **New code.** Observable semantics are those of `yield! ((..) e1 e2)`. An implementation should not make a mixed literal slower than the `yield! [ e1 .. e2 ]` spelling it replaces, and should make integral splices as fast as `[ for x in e1 .. e2 -> x ]`, which since F# 9 is lowered to a counted loop. Elaborating the splice as `for x in e1 .. e2 do yield x` inside list, array and sequence expressions achieves this by reusing the existing lowering of integral `for` loops; for the built-in collections this is an implementation detail with no observable difference. For user-defined builders the choice is observable; see Unresolved questions.

## Scaling

The relevant dimension is the number of elements (values and ranges) in one collection expression. Human-written code rarely exceeds a few dozen; generated code can reach thousands, the same bound as list literals today. Processing must remain linear in the number of elements; the prototype's rewrite was made tail-recursive during review for that reason.

## Culture-aware formatting/parsing

No interaction.

# Unresolved questions

1. **Builder translation: `YieldFrom` or `For`?** This RFC translates a splice through `YieldFrom`, matching the "implicit `yield!`" mental model and the existing `yield!` rule. An alternative is `b.For((..) e1 e2, fun x -> b.Yield x)`, which needs `For` and `Yield` (more commonly implemented than `YieldFrom`), matches how the built-in collections should be lowered for performance, and would compose with the `Range` builder method proposed in [fslang-suggestions#1116](https://github.com/fsharp/fslang-suggestions/issues/1116). A third option is `YieldFrom` when present, otherwise `For` plus `Yield`. The choice is observable for user-defined builders and should be settled before implementation.
2. **Interaction with a spread operator** ([fslang-suggestions#1253](https://github.com/fsharp/fslang-suggestions/issues/1253)). Should `...e1..e2` be accepted (as `...(e1..e2)`) or rejected as redundant? This is for the spread RFC to decide; nothing in this RFC prevents either answer.
3. **First-class ranges.** Rule 5 keeps `(e1 .. e2)` available if F# ever gives ranges a value meaning (compare C#'s `System.Range`). Is that sufficient, or should this RFC reserve anything else?
4. **Diagnostic text** for Rule 4, and whether FS3221 should mention `yield!` when the discarded expression sits next to a range splice.
