# F# RFC FS-1076 - From the end slicing for collections

The design suggestion [FILL ME IN](https://github.com/fsharp/fslang-suggestions/issues/fill-me-in) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [ ] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/fill-me-in)
* [ ] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/FILL-ME-IN)
* [ ] Implementation: [In progress](https://github.com/Microsoft/visualfsharp/pull/FILL-ME-IN)


# Summary
[summary]: #summary

This RFC proposes the capability to slice collections with indices counted from the end. Using the `^i` syntax in slicing is equivalent to `(List.length myList) - i`.

e.g.
```
let list = [1;2;3;4;5]

list.[..^0]   // 1,2,3,4,5
list.[..^1]   // 1,2,3,4
list.[0..^1]  // 1,2,3,4
list.[^1..]   // 4,5
list.[^0..]   // 5
list.[^2..^1] // 3,4
```

# Motivation
[motivation]: #motivation

From-the-end slicing would allow easier operations on arrays. Currently in Python one can specify a negative index, like `list.[:-1]` to obtain a slice of the list without the last element. This feature is often used in scientific and mathematical computation. Adding this feature would make F# more accessible for those uses.

# Detailed design
[design]: #detailed-design

## Parsing

We add new rules to the parser to support the following expressions
```
^x..y
^x..
x..^y
..^y
^x..^y
```

## Typechecker



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

# Compatibility
[compatibility]: #compatibility

Please address all necessary compatibility questions:
* Is this a breaking change?
* What happens when previous versions of the F# compiler encounter this design addition as source code?
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?


# Unresolved questions
[unresolved]: #unresolved-questions

What parts of the design are still TBD?

