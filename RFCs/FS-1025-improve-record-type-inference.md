# F# RFC FS-1025 - Improve record type inference

The design suggestion [Improve record type inference](https://github.com/fsharp/fslang-suggestions/issues/415) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] [User Voice Request](https://github.com/fsharp/fslang-suggestions/issues/415)
* [x] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/163)
* [x] Implementation: [completed](https://github.com/Microsoft/visualfsharp/pull/1771)


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

When resolving a record type, the current behavior of the compiler is to find for each field of the record type, all the types in scope that have that field. Then it takes the intersection of those types for all the fields, and if a single matching type is found, that type is picked. Otherwise, the first type in the list is blindly chosen, and an error can ensue.

The change here is that if there are multiple matches, the compiler now picks the record type in scope that has the same number of fields defined as the given record type.

In the example above, for `p`, two possible record types are in scope with an `a` field: `A` and `AB`. Before this change, this ambiguity would just lead to the last type being picked, `AB` which leads to an error. With the new behavior, `A` is picked instead because it has the same number of fields as the instantiation `p`, and there is no error.

# Drawbacks
[drawbacks]: #drawbacks

No significant drawbacks have been identified so far.

# Alternatives
[alternatives]: #alternatives

No other alternatives have been considered so far.

# Unresolved questions
[unresolved]: #unresolved-questions

No unresolved questions so far.
