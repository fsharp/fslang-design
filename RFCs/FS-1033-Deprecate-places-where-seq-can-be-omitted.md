# F# RFC FS-1033 - Deprecate places where seq can be omitted.

The design suggestion [Deprecate places where seq can be omitted](https://github.com/fsharp/fslang-suggestions/issues/1033) has been marked "approved in principle."

This RFC covers the detailed proposal for this suggestion.

- [x] [Deprecate places where seq can be omitted](https://github.com/fsharp/fslang-suggestions/issues/1033)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/17772)
- [x] [Discussion](https://github.com/fsharp/fslang-design/discussions/788)

# Summary

Deprecate places where seq can be omitted.

# Motivation

- Is confusing that we don't need to use `seq` in `{ start..finish }` but you need to do `seq` in `{ x; y }` e.g.

```fsharp
  { 1..10 } // No error
  { 1..3..10 } // No error
  { 1;10 } // Error: Invalid record, sequence or computation expression. Sequence expressions should be of the form 'seq { ... }'
```

- Having only one way to create a sequence will make the language more consistent and easier to understand.

- Preventing the overload of `..` `op_Range` and `.. ..` `op_RangeStep` operators. Which can lead to confusing code like:
```fsharp
let (..) _ _ = "Im an op_Range operator"

let x = { 1..10 } // Here the index range operator is being overridden and `x` will be a string instead of a sequence.

let (.. ..) _ _ = "Im an op_RangeStep operator"

let y = { 1..10..20 } // Here the index range operator is being overridden and `y` will be a string instead of a sequence.
```

- The FSharp-Spec-4.1 already mentions that the omission of the `seq` keyword is not recommended and that it might be deprecated (§6.3.12).

# Detailed design

<!-- This is the bulk of the RFC. Explain the design in enough detail for somebody familiar
with the language to understand, and for somebody familiar with the compiler to implement.
This should get into specifics and corner-cases, and include examples of how the feature is used.
 -->

- `{ start..finish }` will be deprecated and replaced by `seq { start..finish }`.
- `{ start..finish..step }` will be deprecated and replaced by `seq { start..finish..step }`.
- `{ x; y }` is already not allowed and will continue to raise an error.

- All of these constructs are currently parsed as `SynExpr.ComputationExpr` with `hasSeqBuilder = false`

```fsharp
{ start..finish } 

SynExpr.ComputationExpr(
    hasSeqBuilder = false,
    expr = SynExpr.IndexRange(...)
    ...
)

{ start..finish..step }

SynExpr.ComputationExpr(
    hasSeqBuilder = false,
    expr = SynExpr.IndexRange(...)
    ...
)

{ x; y }

SynExpr.ComputationExpr(
    hasSeqBuilder = false,
    expr = SynExpr.Sequential(...)
    ...
)
```

- We will need to update `TcExprUndelayed` in [CheckExpressions.fs](https://github.com/dotnet/fsharp/blob/b187b806f713e05f0b478ba78dfd7140087f436d/src/Compiler/Checking/Expressions/CheckExpressions.fs#L5723) to raise a warning when it finds a `SynExpr.ComputationExpr` with `hasSeqBuilder = false` and `expr` being `SynExpr.IndexRange` or `SynExpr.Sequential`.
- A new warning will be added to the compiler to warn about the deprecated constructs.
- FS3873, "This construct is deprecated. Sequence expressions should be of the form seq {...}"

## Before

To create a sequence, you can use `{ start..finish }` or `{ start..finish..step }`.

```fsharp
{ 1..10 }

{ 1..5..10 }

[| { 1..10 } |]

[| { 1..5..10 } |]

[| yield { 1..10 } |]

[ { 1..10 } ]

[ { 1..10..10 } ]

[ yield { 1..10 } ]

[ yield { 1..10..20 } ]

ResizeArray({ 1..10 })

ResizeArray({ 1..10..20 })

[ for x in { start..finish } -> x ]

[| for x in { start..finish } -> x |]

for x in { 1..10 }  do ()

for x in { 1..5..10 } do ()

set { 1..6 }

Seq.length { 1..8 }

Seq.map3 funcInt { 1..8 } { 2..9 } { 3..10 }

Seq.splitInto 4 { 1..5 } |> verify { 1.. 10 }

seq [ {1..4}; {5..7}; {8..10} ]

Seq.allPairs { 1..7 } Seq.empty

Seq.allPairs Seq.empty { 1..7 }

[| yield! {1..100}
   yield! {1..100} |]
   
let (..) _ _ = "Im an index range operator"

let x = { 1..10 }
```

## After

A new warning will be raised when using `{ start..finish }` or `{ start..finish..step }` to create a sequence.

```fsharp
{ 1..10 } // FS3873, "This construct is deprecated. Sequence expressions should be of the form seq {...}"

{ 1..5..10 } // FS3873, "This construct is deprecated. Sequence expressions should be of the form seq {...}"

[| { 1..10 } |] // FS3873, "This construct is deprecated. Sequence expressions should be of the form seq {...}"

[| { 1..5..10 } |] // FS3873, "This construct is deprecated. Sequence expressions should be of the form seq {...}"

[| yield { 1..10 } |] // FS3873, "This construct is deprecated. Sequence expressions should be of the form seq {...}"

[ { 1..10 } ] // FS3873, "This construct is deprecated. Sequence expressions should be of the form seq {...}"

[ { 1..10..10 } ] // FS3873, "This construct is deprecated. Sequence expressions should be of the form seq {...}"

[ yield { 1..10 } ] // FS3873, "This construct is deprecated. Sequence expressions should be of the form seq {...}"

[ yield { 1..10..20 } ] // FS3873, "This construct is deprecated. Sequence expressions should be of the form seq {...}"

ResizeArray({ 1..10 }) // FS3873, "This construct is deprecated. Sequence expressions should be of the form seq {...}"

ResizeArray({ 1..10..20 }) // FS3873, "This construct is deprecated. Sequence expressions should be of the form seq {...}"

[ for x in { start..finish } -> x ] // FS3873, "This construct is deprecated. Sequence expressions should be of the form seq {...}"

[| for x in { start..finish } -> x |] // FS3873, "This construct is deprecated. Sequence expressions should be of the form seq {...}"

for x in { 1..10 }  do () // FS3873, "This construct is deprecated. Sequence expressions should be of the form seq {...}"

for x in { 1..5..10 } do () // FS3873, "This construct is deprecated. Sequence expressions should be of the form seq {...}"

set { 1..6 } // FS3873, "This construct is deprecated. Sequence expressions should be of the form seq {...}"

Seq.length { 1..8 } // FS3873, "This construct is deprecated. Sequence expressions should be of the form seq {...}"

Seq.map3 funcInt { 1..8 } { 2..9 } { 3..10 } // FS3873, "This construct is deprecated. Sequence expressions should be of the form seq {...}"

Seq.splitInto 4 { 1..5 } |> verify { 1.. 10 } // FS3873, "This construct is deprecated. Sequence expressions should be of the form seq {...}"

seq [ {1..4}; {5..7}; {8..10} ] // FS3873, "This construct is deprecated. Sequence expressions should be of the form seq {...}"

Seq.allPairs { 1..7 } Seq.empty // FS3873, "This construct is deprecated. Sequence expressions should be of the form seq {...}"

Seq.allPairs Seq.empty { 1..7 } // FS3873, "This construct is deprecated. Sequence expressions should be of the form seq {...}"

[| yield! {1..100} // FS3873, "This construct is deprecated. Sequence expressions should be of the form seq {...}"
   yield! {1..100} |] // FS3873, "This construct is deprecated. Sequence expressions should be of the form seq {...}"
   
let (..) _ _ = "Im an index range operator"

let x = { 1..10 } // FS3873, "This construct is deprecated. Sequence expressions should be of the form seq {...}"
```

To create a sequence, you need to use `seq { start..finish }` or `seq { start..finish..step }`.

```fsharp
seq { 1..10 }

seq { 1..5..10 }

[| seq { 1..10 } |]

[| seq { 1..5..10 } |]

[| yield seq { 1..10 } |]

[ seq { 1..10 } ]

[ seq { 1..10..10 } ]

[ yield seq { 1..10 } ]

[ yield seq { 1..10..20 } ]

ResizeArray(seq { 1..10 })

ResizeArray(seq { 1..10..20 })

[ for x in seq { start..finish } -> x ]

[| for x in seq { start..finish } -> x |]

for x in seq { 1..10 }  do ()

for x in seq { 1..5..10 } do ()

set { 1..6 }

Seq.length (seq { 1..8 })

Seq.map3 funcInt (seq { 1..8 }) (seq { 2..9 }) (seq { 3..10 })

Seq.splitInto 4 (seq { 1..5 }) |> verify (seq { 1.. 10 })

seq [ seq {1..4}; seq {5..7}; seq {8..10} ]

Seq.allPairs (seq { 1..7 }) Seq.empty

Seq.allPairs Seq.empty (seq { 1..7 })

[| yield! seq {1..100}
   yield! seq {1..100} |]
   
let (..) _ _ = "Im an index range operator"

let x = seq { 1..10 }
```

Please address all necessary compatibility questions:

* Is this a breaking change?
  * No. As the current construct `{ x;y }` is already not allowed, this change will only raise a warning when using `{ start..finish }` or `{ start..finish..step }`

* What happens when previous versions of the F# compiler encounter this design addition as source code?
  * Older compiler versions will still emit an error when they encounter `{ x;y }`, but will not raise a warning when they encounter `{ start..finish }` or `{ start..finish..step }`.

* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
  * Older compiler versions will be able to consume the compiled result of this feature without issue.

* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?
  * N/A.

# Pragmatics

## Diagnostics

<!-- Please list the reasonable expectations for diagnostics for misuse of this feature. -->

- FS3873, "This construct is deprecated. Sequence expressions should be of the form seq {...}"

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
