# F# RFC FS-1025 - Improve record type inference

The design suggestion [FILL ME IN](https://fslang.uservoice.com/forums/245727-f-language/suggestions/7138324-record-type-inference-suggestion) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] [User Voice Request](https://fslang.uservoice.com/forums/245727-f-language/suggestions/7138324-record-type-inference-suggestion)
* [ ] Details: To be discussed.
* [ ] Implementation: Not started.


# Summary
[summary]: #summary

Record type and field resolution is a bit too eager to declare failure:

```fsharp
type A = { a: int }
type B = { b: int }
type AB = { a: int; b: int }

let p = { a=1 } // compiler error: no assignment given for field 'b' of type 'AB'
let q = { b=2 } // compiler error: no assignment given for field 'a' of type 'AB'
```

Note that if the record `AB` is declared first, then type + argument name type inference does work.

The aim is to make the above program work without any type annotations on the fields (`{ B.b = 2 }`).

# Motivation
[motivation]: #motivation

The current behavior can be somewhat mentally jarring. It doesn't feel nice to have to either add the type name to the fields, rename fields or change the order of declaration (if even possible) to disambiguate things that really don't look ambiguous.

# Detailed design
[design]: #detailed-design

Detailed design to be determined.

# Drawbacks
[drawbacks]: #drawbacks

No significant drawbacks have been identified so far.

# Alternatives
[alternatives]: #alternatives

Since there isn't a detailed design yet, no other alternatives have been considered so far.

# Unresolved questions
[unresolved]: #unresolved-questions

No unresolved questions so far.
