# F# RFC FS-1038 - Evaluate generalizable values once

The design suggestion [Evaluate generalizable values once](https://github.com/fsharp/fslang-suggestions/issues/602) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [ ] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/602)
* [ ] Details: [under discussion](https://github.com/fsharp/FSharpLangDesign/issues/FILL-ME-IN)
* [ ] Implementation: [In progress](https://github.com/Microsoft/visualfsharp/pull/FILL-ME-IN)


# Summary
[summary]: #summary

As of VS2017.3 / F# 4.1, ``let``-bound values decorated with ``[<GeneralizableValue>]`` aren't actually values; they're compiled as
methods, and the bound expression is re-evaluated each time the "value" is used. This RFC proposes changing the compiled
representation of these values so the bound expression is evaluated only once as with other ``let``-bound values.

# Motivation
[motivation]: #motivation

Why are we doing this? What use cases does it support? What is the expected outcome?

# Detailed design
[design]: #detailed-design

This is the bulk of the RFC. Explain the design in enough detail for somebody familiar
with the language to understand, and for somebody familiar with the compiler to implement.
This should get into specifics and corner-cases, and include examples of how the feature is used.

Example code:

```fsharp
let add x y = x + y
```

# Drawbacks
[drawbacks]: #drawbacks

Why should we *not* do this?

# Alternatives
[alternatives]: #alternatives

What other designs have been considered? What is the impact of not doing this?

# Unresolved questions
[unresolved]: #unresolved-questions

What parts of the design are still TBD?

