# F# RFC FS-1076 - From the end slicing for collections

This RFC covers the detailed proposal for this suggestion.

* [ ] Approved in principle
* [ ] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/358)
* [ ] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/FILL-ME-IN)
* [ ] Implementation: [In progress](https://github.com/Microsoft/visualfsharp/pull/FILL-ME-IN)


# Summary
[summary]: #summary

This RFC proposes the capability to slice collections with indices counted from the end. Using the `^i` syntax in slicing desugars to `myList.Length - i - 1`.

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

The `^` operator is currently used as a infix operator for:
- Power (2^2)
- Measure types
- Legacy string concat

It is used as a prefix operator for:
- Statically resolved types

For the from-the-end slicing, the `^` operator will be overloaded as a prefix operator. 

We will add new rules to the parser to support the following expressions for `optRange`:
```
^x..y
^x..
x..^y
..^y
^x..^y
```

## Typechecker

Currently in the typechecker, the slicing is handled in two ways (see `typechecker.fs: 6295`):

- For core collections, depending on the shape of the slicing call and the type of the collection, the appropriate `GetSlice` method implementation is picked and the supplied slicing indices are transformed into arguments to those methods.
- For third party collections, the compiler builds a generic `GetSlice` call, then does another round of typechecking to find the concrete implementation.


To support from-the-end slicing, logic can be added in these two code paths to check if the `^` symbol was prepended to any of the indices. If it is detected, the call to `GetSlice` with the index `^i` will be desugared to `myList.Length - i - 1`. The desugared indices will be piped to the existing `GetSlice` implementations.

# Drawbacks
[drawbacks]: #drawbacks

## Differing behavior with current inclusive-inclusive slicing compared to other languages

The current F# slicing behavior is front-inclusive and rear-inclusive, in contrast to front-inclusive and rear-exclusive for C# and Python. This means that if we choose to desugar `^i` to `myList.Length - i - 1`, then:

```
// Python
list[:-1]    // 1,2,3,4
list[-1:]    // 5

// F#
list.[..^1]  // 1,2,3,4 -- Same
list.[^1..]  // 4,5     -- Different
list.[^0..]  // 5       -- Different
```

Because of the difference in inclusivity, we can only match Python/C# behavior for either `list.[^1..]` or `list.[..^1]` but not both, unless the definition of `^i` varies based on the context of where it's placed. 

It is assumed that most users will use this syntax in the form of `list.[..^1]`, or "I want everything in this except for the last i elements."

# Alternatives
[alternatives]: #alternatives

- Using `-` instead of `^`: not possible since negative indexes can be valid
- Defining `^i` as `mylist.Length - i` without the `-1`. This would allow `list[-1:] == list.[^1..]` but would cause `list.[..^1]` to be different.
- Define `^i` to mean `mylist.Length - i` if used as the starting index of a slice and `mylist.Length - i - 1` if used as the end index. This would align the behavior completely with C#, but it would mean that the slicing would change from inclusive-inclusive to inclusive-exclusive if the `^` operator was used.

# Compatibility
[compatibility]: #compatibility

Please address all necessary compatibility questions:
* Is this a breaking change? **No**

## Third-party collections

We currently require third party collections to implement `<'T>.GetSlice` to support slicing syntax. We can additionally require third party collections to implement `<'T>.Length` if they wish to support from-the-end slicing. If they choose to do only the former, any `^i` indices in the slice will fail with runtime error trying to resolve `<'T>.Length`.

## Old versions of Core
- New compiler + new core:
    - Expected behavior
- New compiler + old core:
    - Same behavior as above, as the `^i` is desugared into a normal `GetSlice` call.
- Old compiler + new core:
    - Error on `^` at compile time.
- Old compiler + old core:
    - Error on `^` at compile time.

# Unresolved questions
[unresolved]: #unresolved-questions

Do we have actual data to back up the assumption that people use the `list.[..^1]` more than `list.[^1..]`?
