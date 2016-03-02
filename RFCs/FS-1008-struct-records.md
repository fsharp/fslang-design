# F# RFC FS-0008 - Struct Records

The design suggestion [Record types can be marked with the Struct attribute](https://fslang.uservoice.com/forums/245727-f-language/suggestions/6547517-record-types-can-be-marked-with-the-struct-attribu) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] [Approved in principle](https://fslang.uservoice.com/forums/245727-f-language/suggestions/6547517-record-types-can-be-marked-with-the-struct-attribu)
* [x] Details: [under discussion](https://github.com/Microsoft/visualfsharp/pull/620)
* [x] Implementation: [Almost complete](https://github.com/Microsoft/visualfsharp/pull/620)


# Summary
[summary]: #summary

Record types should be able to be marked as a struct, effectively making the record have the semantics of value types.

# Motivation
[motivation]: #motivation

Performance.

# Detailed design
[design]: #detailed-design

How to use:
```fsharp
type Vector3 = { X: float; Y: float; Z: float }
```
Put the `StructAttribute` on the type.
```fsharp
[<Struct>]
type Vector3 = { X: float; Y: float; Z: float }
```

Key differences in struct records:
- Any fields marked as mutable can't be mutated if the corresponding struct instance itself is not marked as mutable. This is how structs work in general for F#.
- You cannot have cyclic references to the same type being defined. ex: ```type T = { X: T }```
- You also cannot call the default ctor, like you could with normal F# structs.
- When marked with the `CLIMutableAttribute`, it will not create a default ctor, because structs implicitly have one, though you can't call it from F#.

# Drawbacks
[drawbacks]: #drawbacks

Few if any.

# Alternatives, Unresolved questions
[unresolved]: #unresolved-questions

See https://github.com/Microsoft/visualfsharp/pull/620 for remaining issues.
