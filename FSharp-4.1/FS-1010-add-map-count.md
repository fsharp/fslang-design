# F# RFC FS-1010 - Add Map.count

The design suggestion [Add Map.count](https://fslang.uservoice.com/forums/245727-f-language/suggestions/12880398-add-map-count) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Details: [Discussion Completed](https://github.com/fsharp/FSharpLangDesign/issues/78)
* [x] Implementation: [Completed](https://github.com/dotnet/fsharp/pull/1007)


# Summary
[summary]: #summary

Add ``Map.count``.  

# Motivation
[motivation]: #motivation

To align with ``Set.count``

# Detailed design
[design]: #detailed-design

Very simple design, no need to go into detail here.

# Drawbacks
[drawbacks]: #drawbacks

Note that the instance property ``.Count`` is already available.  The main drawback is that it gives two ways to do the same thing.

# Alternatives
[alternatives]: #alternatives

The alternative is to keep things as they are.

# Unresolved questions
[unresolved]: #unresolved-questions

None
