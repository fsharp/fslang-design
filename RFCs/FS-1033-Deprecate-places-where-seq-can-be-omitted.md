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
// TODO: Add motivation.

```fsharp
{ start..finish }

{ start..step..finish }

```

# Detailed design

<!-- This is the bulk of the RFC. Explain the design in enough detail for somebody familiar
with the language to understand, and for somebody familiar with the compiler to implement.
This should get into specifics and corner-cases, and include examples of how the feature is used.
 -->

// TODO: Add detailed design.

## Before

To create an object expression without overrides, the user has to override a member, even if it is not necessary.

```fsharp
// TODO
```

## After

We won't need to use any workaround to use classes(abstract or non-abstract) in object expressions.

```fsharp
// TODO
```

Please address all necessary compatibility questions:

* Is this a breaking change?
  * No.
  
* What happens when previous versions of the F# compiler encounter this design addition as source code?
  * Older compiler versions will still emit an error when they encounter this design addition as source code.

* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
  * Older compiler versions will be able to consume the compiled result of this feature without issue.

* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?
  * N/A.

# Pragmatics

## Diagnostics

<!-- Please list the reasonable expectations for diagnostics for misuse of this feature. -->
  N/A.

We continue to emit an error message when not all abstract members are implemented:

```fsharp
// TODO
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
