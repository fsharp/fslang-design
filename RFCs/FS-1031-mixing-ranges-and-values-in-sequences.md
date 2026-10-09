# F# RFC FS-1031 - Mixing ranges and values to construct sequences

The design suggestion [Mixing ranges and values to construct sequences](https://github.com/fsharp/fslang-suggestions/issues/1031) has been marked "approved in principle."

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1031)
- [x] Approved in principle
- [ ] Implementation: the prototype [dotnet/fsharp#18670](https://github.com/dotnet/fsharp/pull/18670) was closed without merging
- [x] Discussion: [#803](https://github.com/fsharp/fslang-design/discussions/803), [#850](https://github.com/fsharp/fslang-design/pull/850)

# Summary

Allow ranges to be directly mixed with individual values in sequence, list, and array expressions without requiring `yield!` for the range. This feature also automatically extends to custom computation expressions whose builders implement `YieldFrom`.

# Motivation

Today a range mixed with other values needs `yield!` of a nested collection, such as `yield! [ 1..10 ]`, or a `for` loop. `yield! 1..10` is an error. This leads to verbose and less readable code for a common pattern.

`[ 1..10 ]` alone is already a splice. A second element forces a rewrite, and the inner list or array is built only to be copied.

# Detailed design

The keywords MUST, MUST NOT, SHOULD and MAY are used as in [RFC 2119](https://www.rfc-editor.org/info/rfc2119/).

```fsharp
// Before
let a = seq { yield! seq { 1..10 }; 19 }
let c = [ yield! [1..10]; 19; yield! [25..30] ]
let d = [| -3; yield! [|1..10|]; 19 |]
let stepped = [ yield! [0..2..10]; 15; yield! [20..5..40] ]

// After
let a = seq { 1..10; 19 }
let c = [ 1..10; 19; 25..30 ]
let d = [| -3; 1..10; 19 |]
let stepped = [ 0..2..10; 15; 20..5..40 ]
```

## Rules

A *range expression* is `e1..e2` or `e1..e2..e3`.

1. **Splice.** In element position, a range expression means `yield! ((..) e1 e2)` or `yield! ((.. ..) e1 e2 e3)`. Element positions are the body of a list, array, sequence or computation expression and, inside it, the bodies of `if`, `match`, `for`, `while`, `try`, `let`, `use` and their `!` forms. They do not depend on the builder methods:

   ```fsharp
   let f p = [ if p then 1 else 1..10 ]
   let h xs = [ for x in xs do x..x+2 ]
   ```

2. **Implicit yields.** Like `yield!`, a range expression is not an explicit `yield` for the activation rule of [FS-1069](../FSharp-4.7/FS-1069-implicit-yields.md). It is a splice whether implicit yields are active or not. Plain values keep the FS-1069 rule:

   ```fsharp
   [ yield! xs; 2..3; 4 ]   // xs, then 2; 3; 4
   [ yield 1; 2..3; 4 ]     // [1; 2; 3]; 4 is discarded with warning FS3221
   ```

3. **Explicit splice.** `yield! e1..e2` and `yield! e1..e2..e3` are allowed wherever `yield!` is allowed, with the same meaning.

4. **No single value.** `yield e1..e2`, `for x in xs -> e1..e2` (`->` means `yield`) and, in a computation expression, `return e1..e2` are errors with a dedicated diagnostic. In list, array and sequence expressions `return` keeps error FS0635. A range is not a value today; `let r = 1..10` is an error. `return! e1..e2` stays an error.

5. **Parentheses.** `(e1..e2)` is not a range expression. `[ 1; (2..5) ]` stays an error. This keeps the form free for a first-class range value, such as `System.Range` in the [FS-1076 revision (#851)](https://github.com/fsharp/fslang-design/pull/851), which leaves the positions of rules 1 to 4 to this RFC.

6. **Operators in scope.** `(..)` and `(.. ..)` are resolved by name resolution at the splice, as for `[ e1..e2 ]` and `for x in e1..e2` today. A user-defined operator is used:

   ```fsharp
   let (..) (a: int) (b: int) = seq { a; b }
   [ 0; 1..3 ]   // [0; 1; 3]; today [ 1..3 ] already gives [1; 3]
   ```

7. **Computation expressions.** The translation is the existing `yield!` rule:

   ```text
   T(e1..e2, V, C, q)      = C(b.YieldFrom(src((..) e1 e2)))
   T(e1..e2..e3, V, C, q)  = C(b.YieldFrom(src((.. ..) e1 e2 e3)))
   ```

   In tail position, `YieldFromFinal` is used when the builder defines it, as for `yield!`. A builder with neither method gets error FS0708. Sequencing needs `Combine` and `Delay`, as today. This also applies to `b { e1..e2 }` alone, which is an error today.

8. **Out of scope.** The builder-less form `{ 1..10; 19 }` ([FS-1033](../FSharp-10.0/FS-1033-Deprecate-places-where-seq-can-be-omitted.md)). The single-range forms `[ e1..e2 ]`, `[| e1..e2 |]` and `seq { e1..e2 }` keep their elaboration. Slicing `xs[a..b]` is unaffected.

## Benefits for Custom Computation Expressions

```fsharp
type MyBuilder() =
    member _.Yield(x) = [x]
    member _.YieldFrom(xs: seq<_>) = List.ofSeq xs
    member _.Zero() = []
    member _.Combine(a, b) = a @ b ()
    member _.Delay(f) = f
    member _.Run(f) = f()

let mybuilder = MyBuilder()

let result = mybuilder { 1; 2..5; 10 }  // [1; 2; 3; 4; 5; 10]
```

## Interactions

- **Quotations** show the `yield!` elaboration: a call to `op_Range` or `op_RangeStep` inside the sequence.
- **SRTP**: `(..)` is an inline FSharp.Core operator. A splice in `inline` code adds its constraints, as `[ a..b ]` does.
- **Type providers, structs, C#**: no interaction. The feature emits ordinary lists, arrays, sequences and builder calls.

# Changes to the F# spec

- §6.3.10 Computation Expressions: add `range-expr` and `yield! range-expr` to `comp-expr`, with the translation of rule 7.
- §6.3.12 Range Expressions: a range expression may also be an element of a list, array, sequence or computation expression (rules 1 and 5).
- §6.3.13 Lists via Sequence Expressions and §6.3.14 Arrays Sequence Expressions: add examples `[ e; e1..e2 ]` and `[| e; e1..e2 |]`. The elaboration through `Seq.toList` and `Seq.toArray` does not change.
- Implicit yields (FS-1069): a range expression is not an explicit `yield` (rule 2).

# Drawbacks

- One more implicit rule in collection expressions.
- `e1..e2` in element position permanently means "splice".
- A plain value next to a range can still be discarded (rule 2).

# Alternatives

- **Splice only at the top level**, with explicit `yield!` inside `if`, `match` and `for`. Ranges would then behave differently from values in the same position.
- **Treat ranges like plain values.** Then `[ yield 1; 2..3 ]` discards the range with warning FS3221. A discarded range is never intentional.
- **Fall back to `Yield`** when the builder has no `YieldFrom` (the prototype). This silently yields the range as one element.
- **Translate through `For` and `Yield`** in computation expressions. This composes with the `Range` builder method of [fslang-suggestions#1116](https://github.com/fsharp/fslang-suggestions/issues/1116), but a splice would then differ from `yield!` of the same range (rule 3).
- **A general spread operator** ([fslang-suggestions#1253](https://github.com/fsharp/fslang-suggestions/issues/1253)). It covers any sequence and does not conflict with this RFC.

# Prior art

Ruby `[-3, *1..10, 19]`, Python `[-3, *range(1, 11), 19]`, C# 12 `[-3, ..Enumerable.Range(1, 10), 19]`.

# Compatibility

* Is this a breaking change?
  * No. This change only allows syntax that was previously rejected by the compiler.
  * Diagnostics for code that does not use the feature MUST NOT change under any language version.
  
* What happens when previous versions of the F# compiler encounter this design addition as source code?
  * Compilers before F# 6 emit a syntax error. F# 6 and later report an error at the range, FS0751 in most positions. A new compiler with an older `--langversion` reports FS3350 once per range for the new forms, and keeps today's error for the rule 4 forms.

* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
  * This is a purely syntactic change that doesn't affect the compiled output. Older compiler versions will be able to consume binaries without issue.

* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?
  * N/A - This is a syntactic change only.

# Interop

See [Interactions](#interactions).

# Pragmatics

## Diagnostics

- `yield e1..e2`, `return e1..e2`, `-> e1..e2`: a new error (rule 4).
- Element type mismatch: FS0001 at the range.

## Tooling

The syntax tree already has the range node, so parsing, error recovery, colorization and brace matching do not change. Hover and Go To Definition on `..` resolve to the operator, as for `[ e1..e2 ]`. A splice has the debug points of `yield!`.

## Performance

* No impact on existing code.
* A mixed literal MUST NOT be slower than the `yield! [ e1..e2 ]` it replaces. Integral splices in list, array and sequence expressions SHOULD be as fast as `[ for x in e1..e2 -> x ]`. An implementation MAY lower them to that counted loop after type checking; quotations and debug points MUST keep the `yield!` form.

## Scaling

The dimension is the number of elements in one collection expression. Human-written code has a few dozen; generated code can have thousands, as for list literals today. Checking MUST stay linear in it.

## Culture-aware formatting/parsing

Not applicable.

# Unresolved questions

* **Spread operator** ([#1253](https://github.com/fsharp/fslang-suggestions/issues/1253)): whether `...e1..e2` is accepted or redundant.
* **Diagnostic text** for rule 4, and whether FS3221 should mention `yield!` next to a range splice.
