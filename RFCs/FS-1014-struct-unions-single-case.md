# F# RFC FS-0014 - Struct unions (single case)

The design suggestion [Allow single case unions to be compiled as structs](https://fslang.uservoice.com/forums/245727-f-language/suggestions/6147144-allow-single-case-unions-to-be-compiled-as-structs) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] [User Voice Request](https://fslang.uservoice.com/forums/245727-f-language/suggestions/6147144-allow-single-case-unions-to-be-compiled-as-structs)
* [ ] Details: [under discussion](https://github.com/fsharp/FSharpLangDesign/issues/FILL-ME-IN)
* [ ] Implementation: [In progress](https://github.com/Microsoft/visualfsharp/pull/FILL-ME-IN)


# Summary
[summary]: #summary

See [struct records](https://github.com/fsharp/FSharpLangDesign/blob/master/RFCs/FS-1008-struct-records.md):

Like record types, single case union types should be able to be marked as a struct,
effectively making the union type have the semantics of value types.


# Motivation
[motivation]: #motivation

Enable better performance in some situations via a simple attribute addition.


# Detailed design
[design]: #detailed-design

How to use:

```fsharp
[<Struct>]
type UnionExample = U of int * int * bool
```

Key differences in struct records:

You cannot have cyclic references to the same type being defined. ex: type T = U of T

You also cannot call the default ctor, like you could with normal F# structs.


# Drawbacks
[drawbacks]: #drawbacks

The same as [struct records](https://github.com/fsharp/FSharpLangDesign/blob/master/RFCs/FS-1008-struct-records.md):
* People may not understand when to use the attribute, and, like inline, use it inappropriately, giving worse performance.
* People may "fiddle around" applying the attribute when performance is OK or performance gains are more likely to come via other routes
* It's one more trick for F# programmers to learn

# Alternatives
[alternatives]: #alternatives

TBD: What other designs have been considered? What is the impact of not doing this?

# Unresolved questions
[unresolved]: #unresolved-questions

TBD: What parts of the design are still TBD?
