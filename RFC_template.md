# F# RFC FS-NNNN - (Fill me in with a feature name)

NOTE: new sections have been added to this template! Please use this template rather than copying an existing RFC.

The design suggestion [FILL ME IN](https://github.com/fsharp/fslang-suggestions/issues/fill-me-in) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/FILL-ME-IN)
- [x] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

# Summary

One paragraph explanation of the feature.

# Motivation

Why are we doing this? What use cases does it support? What is the expected outcome?

# Detailed design

This is the bulk of the RFC. Explain the design in enough detail for somebody familiar
with the language to understand, and for somebody familiar with the compiler to implement.
This should get into specifics and corner-cases, and include examples of how the feature is used.

Example code:

```fsharp
let add x y = x + y
```

# Changes to the F# spec

List the necessary changes or additions to the [F# spec](https://fsharp.github.io/fslang-spec/).
For each change, mention the section numbers and the required change.
Here are two examples:
- [Class names as functions](https://github.com/fsharp/fslang-design/blob/main/FSharp-4.0/ClassNamesAsFunctionsDesignAndSpec.md#specification)
- [Scoped nowarn](https://github.com/fsharp/fslang-design/blob/main/RFCs/FS-1146-scoped-nowarn.md#detailed-specification)

# Drawbacks

Why should we *not* do this?

# Alternatives

What other designs have been considered? What is the impact of not doing this?

# Prior art

Has a similar feature been proposed or implemented for other programming languages (e.g., [C#](https://github.com/dotnet/csharplang), [OCaml](https://github.com/ocaml/RFCs), etc.)? Is there any wisdom to be gained from these proposals, designs, implementations, or from real-world usage?

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change?
* What happens when previous versions of the F# compiler encounter this design addition as source code?
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?

# Interop

* What happens when this feature is consumed by another .NET language?
* Are there any planned or proposed features for another .NET language (e.g., [C#](https://github.com/dotnet/csharplang)) that we would want this feature to interoperate with?

# Pragmatics

## Diagnostics

Please list the reasonable expectations for diagnostics for misuse of this feature.

## Tooling

Please list the reasonable expectations for tooling for this feature, including any of these:

* Debugging
  * Breakpoints/stepping
  * Expression evaluator
  * Data displays for locals and hover tips
* Auto-complete
* Tooltips
* Navigation and Go To Definition
* Error recovery (wrong, incomplete code)
* Colorization
* Brace/parenthesis matching

## Performance

Please list any notable concerns for impact on the performance of compilation and/or generated code

* For existing code
* For the new features

## Scaling

Please list the dimensions that describe the inputs for this new feature, e.g. "number of widgets" etc.  For each, estimate a reasonable upper bound for the expected size in human-written code and machine-generated code that the compiler will accept.

For example

* Expected maximum number of widgets in reasonable hand-written code: 100
* Expected reasonable upper bound for number of widgets accepted: 500

Testing should particularly check that compilation is linear (or log-linear or similar) along these dimensions.  If quadratic or worse this should ideally be noted in the RFC.

## Culture-aware formatting/parsing

Does the proposed RFC interact with culture-aware formatting and parsing of numbers, dates and currencies? For example, if the RFC includes plaintext outputs, are these outputs specified to be culture-invariant or current-culture.

# Unresolved questions

What parts of the design are still TBD?
