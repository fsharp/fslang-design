# F# RFC FS-1049 - Nested Record Field Copy and Update Expression

The design suggestion [Support first-class lensing / lenses](https://github.com/fsharp/fslang-suggestions/issues/379)
This RFC covers the detailed proposal for this suggestion.

* [ ] Approved in principle
* [ ] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/379)
* [ ] Details: [under discussion](https://github.com/fsharp/fslang-suggestions/issues/379)
* [ ] Implementation: [WIP](https://github.com/Microsoft/visualfsharp/pull/4511)


# Summary
[summary]: #summary

Enable updating nested record field with "with" syntax.

# Motivation
[motivation]: #motivation

This improves readability. In cases where need to update the value of a nested field in record.

For example, take the following code:

```fsharp
type Street = { N: string }
type Address = { S: Street }
type Person = { A: Address; Age: int }
​
let person = { A = { S = { N = "Street 1" } }; Age = 30 }
​
let anotherPerson = { person with A = { person.A with S = { person.A.S with N = person.A.S.N + ", k.2" } } }
​
let anotherPerson1 = { person with A.S.N = person.A.S.N + ", k.2" }
```

# Detailed design
[design]: #detailed-design

Carefully transform the ast in TypeChecker.fs to expand to the current form
Need to consider in some cases
1. Handle the possible ambiguity of specifing type name vs field name
2. Group nested field from the same parent
3. All nested field parts need to be decleard on  record type
4. Collaborate with anonymous records feature
5. Check IntelliSense
6. Investigate other language features that should support nested paths like named arguments
7. Check no same nested field update

# Drawbacks
[drawbacks]: #drawbacks

Additional complexity in the compiler.

# Compatibility
[compatibility]: #compatibility

This is not a breaking change, and is backwards-compatible with existing code.

# Open Questoions

Q: What about indexers, e.g.
```
let anotherPerson1 = { person with A.[3].N = person.A.[3].N + ", k.2" }
```
A:Need to look at it 

Q: What happens to cases where A.S is mentioned twice?

```
 { person with A.S.N = person.A.S.N + ", k.2"; A.S.M = person.A.S.M + ", k.3"  }
```

A: Already implemented will compile to

```
 { person with A = { person.A with S = { person.A.S with N = person.A.S.N + ", k.2"; M = person.A.S.M + ", k.3"  }
```

Q: What about similar features in the language that name fields, especially mutating property setters

```
Person(A.C.N = 3, A.C.M = 4)
```

A: Need to look at it after we finish with the records

# Alternatives

- Do not implement this feature
