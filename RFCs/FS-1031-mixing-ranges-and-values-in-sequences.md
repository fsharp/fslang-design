# F# RFC FS-1031 - Mixing ranges and values to construct sequences

The design suggestion [Mixing ranges and values to construct sequences](https://github.com/fsharp/fslang-suggestions/issues/1031) has been marked "approved in principle."

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1031)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/18670)
- [x] [Discussion](https://github.com/fsharp/fslang-design/discussions/803)

# Summary

Allow ranges to be directly mixed with individual values in sequence, list, and array expressions without requiring `yield!` for the range. This feature also automatically extends to custom computation expressions that implement the standard builder methods.

# Motivation

Currently, when constructing sequences that mix ranges with individual values, F# requires the use of `yield!` to include the range. This leads to verbose and less readable code for a common pattern.

This inconsistency is:
- **Unnecessarily verbose** for a common pattern in sequence construction
- **Inconsistent** with the intuitive expectation that ranges should compose naturally with other sequence elements
- **A source of friction** for developers who frequently work with sequences mixing ranges and values
- **Less readable** than the proposed syntax, particularly when multiple ranges and values are combined
- **Limiting for custom computation expressions** that could benefit from the same syntax simplification

# Detailed design

## Scenario: Sequence expressions

Sequence expressions can mix ranges and values directly:

```fsharp
// Before
let a = seq { yield! seq { 1..10 }; 19 }

// After  
let a = seq { 1..10; 19 }
```

## Scenario: List expressions

List expressions can mix ranges and values directly:

```fsharp
// Before
let b = [ -3; yield! [1..10] ]
let c = [ yield! [1..10]; 19; yield! [25..30] ]

// After
let b = [ -3; 1..10 ]
let c = [ 1..10; 19; 25..30 ]
```

## Scenario: Array expressions

Array expressions can mix ranges and values directly:

```fsharp
// Before
let d = [| -3; yield! [|1..10|]; 19 |]
let e = [| yield! [|1..5|]; 10; yield! [|15..20|] |]

// After
let d = [| -3; 1..10; 19 |]
let e = [| 1..5; 10; 15..20 |]
```

## Scenario: Complex expressions with multiple ranges

The feature supports any number of ranges and values in any order:

```fsharp
// Before
let complex = seq {
    0
    yield! seq { 1..5 }
    10
    yield! seq { 11..15 }
    20
    yield! seq { 21..25 }
}

// After
let complex = seq {
    0
    1..5
    10
    11..15
    20
    21..25
}
```

## Scenario: Step ranges

The feature also works with step ranges:

```fsharp
// Before
let stepped = [ yield! [0..2..10]; 15; yield! [20..5..40] ]

// After
let stepped = [ 0..2..10; 15; 20..5..40 ]
```

## Implementation Details

The implementation modifies the type checker to handle range expressions in sequence contexts by transforming them during the checking phase rather than requiring parser changes. The key components modified are:

### 1. CheckArrayOrListComputedExpressions.fs

- Added detection for range expressions (`SynExpr.IndexRange`) in list/array expressions
- Transforms mixed lists/arrays with ranges into sequence computation expressions
- For example, `[-3; 1..10; 19]` is transformed to `seq { yield -3; yield! 1..10; yield 19 }` during type checking
- The resulting sequence is then converted back to a list or array using `Seq.toList` or `Seq.toArray`

### 2. CheckSequenceExpressions.fs

- Extended to handle mixed ranges in sequence expressions directly
- Detects when a sequence contains range expressions and transforms them appropriately
- Builds the sequence body by converting ranges to `yield!` and values to `yield`

### 3. CheckComputationExpressions.fs

- Added support for computation expressions that contain ranges
- Checks if the builder supports the required methods (`Yield`, `YieldFrom`, `Combine`, `Delay`)
- Transforms sequential expressions with ranges into appropriate yields and yield-froms

## Benefits for Custom Computation Expressions

This feature automatically benefits any custom computation expression builder that implements the standard CE methods (`Yield`, `YieldFrom`, `Combine`, `Delay`). For example:

```fsharp
type MyBuilder() =
    member _.Yield(x) = [x]
    member _.YieldFrom(xs: seq<_>) = List.ofSeq xs
    member _.Combine(a, b) = a @ b
    member _.Delay(f) = f
    member _.Run(f) = f()

let mybuilder = MyBuilder()

// This now works automatically!
let result = mybuilder { 1; 2..5; 10 }  // [1; 2; 3; 4; 5; 10]
```

## Non-interference with Custom Range Operators

This feature only applies to the built-in range syntax and does not interfere with custom implementations of the range operator. If someone defines their own `(..)` operator:

```fsharp
open System.Collections
open System.Collections.Generic

type X(elements: X list) =
    member this.Elements = elements

    interface IEnumerable<X> with
        member this.GetEnumerator() =
            (this.Elements :> IEnumerable<X>).GetEnumerator()

        member this.GetEnumerator() : IEnumerator =
            (this.Elements :> IEnumerable).GetEnumerator()

    static member Combine(x1: X, x2: X) =
        X(x1.Elements @ x2.Elements)

let (..) a b = seq { X.Combine(a,b) }

let a = X([])
let b = X([])

let whatIsThis = seq { a..b }

let result1 = [ whatIsThis ;a ; b]
let result2 = [ yield! whatIsThis; a; b ]
```

The transformation happens at the syntax tree level for `SynExpr.IndexRange` nodes, which are only created by the built-in range syntax, not by custom operators. This ensures backward compatibility while enabling the new feature.

## Compatibility

* Is this a breaking change?
  * No. This change only allows syntax that was previously rejected by the compiler.
  
* What happens when previous versions of the F# compiler encounter this design addition as source code?
  * Older compiler versions will emit a syntax error when encountering ranges without `yield!` in sequence expressions.

* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
  * This is a purely syntactic change that doesn't affect the compiled output. Older compiler versions will be able to consume binaries without issue.

* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?
  * N/A - This is a syntactic change only.

# Pragmatics

## Diagnostics

The compiler provides clear error messages when:
- The feature is used without the preview language flag enabled
- Invalid range syntax is used  
- Type mismatches occur between range elements and other sequence elements

When the feature is not enabled, users see:
```
Feature 'Allow mixed ranges and values in sequence expressions, e.g. seq { 1..10; 20 }' is not available in F# 9.0. Please use language version 'PREVIEW' or greater.
```

## Tooling

Please list the reasonable expectations for tooling for this feature, including any of these:

* Debugging
  * Breakpoints/stepping
    * N/A.
  * Expression evaluator
    * N/A.
  * Data displays for locals and hover tips
    * N/A.
* Auto-complete
  * N/A.
* Tooltips
  * N/A.
* Navigation and go-to-definition
  * N/A.
* Error recovery (wrong, incomplete code)
  * N/A.
* Colorization
  * N/A.
* Brace/parenthesis matching
  * N/A.

## Performance

<!-- Please list any notable concerns for impact on the performance of compilation and/or generated code -->

* No performance or scaling impact is expected.

## Scaling

<!-- Please list the dimensions that describe the inputs for this new feature, e.g. "number of widgets" etc.  For each, estimate a reasonable upper bound for the expected size in human-written code and machine-generated code that the compiler will accept. -->

* N/A.

## Culture-aware formatting/parsing

Does the proposed RFC interact with culture-aware formatting and parsing of numbers, dates and currencies? For example, if the RFC includes plaintext outputs, are these outputs specified to be culture-invariant or current-culture.

* No.

# Unresolved questions

* None.