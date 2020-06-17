# F# RFC FS-1076 - From the end slicing and indexing for collections

This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/358)
* [x] [Implementation](https://github.com/dotnet/fsharp/pull/7781) (merged)
* [Discussion](https://github.com/fsharp/fslang-design/issues/472)

# Summary
[summary]: #summary

This RFC proposes the capability to slice and index collections with indices counted from the end. Using the `^i` syntax in slicing and indexing desugars to `collection.GetReverseIndex(i)`.

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

From-the-end slicing and indexing would allow easier operations on arrays. Currently in Python one can specify a negative index, like `list.[:-1]` to obtain a slice of the list without the last element. This feature is often used in scientific and mathematical computation. Adding this feature would make F# more accessible for those uses.

# Detailed design
[design]: #detailed-design

## Parsing

The `^` operator is currently used as a infix operator for:
- Power (2^2)
- Measure types
- Legacy string concat

It is used as a prefix operator for:
- Statically resolved types

For the from-the-end slicing and indexing, the `^` operator will be overloaded as a prefix operator. 

We will add new rules to the parser to support the following expressions for `optRange`:
```
^x..y
^x..
x..^y
..^y
^x..^y
^x
```

Using `^` outside of the square brackets will not mean anything, as the parsing rule that handles it only exists inside `optRange`.

In addition, because of the way `..` is handled currently in the lexer, to correctly parse `..^`, we would have to add it to the list of reserved symbolic operators. This would break any users currently using `..^` as a custom operator.

## Typechecker

Currently in the typechecker, the slicing is handled in two ways (see `typechecker.fs: 6295`):

- For core collections, depending on the shape of the slicing call and the type of the collection, the appropriate `GetSlice` method implementation is picked and the supplied slicing indices are transformed into arguments to those methods.
- For third party collections, the compiler builds a generic `GetSlice` call, then does another round of typechecking to find the concrete implementation.


To support from-the-end slicing, logic can be added in these two code paths to check if the `^` symbol was prepended to any of the indices. If it is detected, the call to `GetSlice` with the index `^i` will be desugared to `myCollection.GetReverseIndex(i)`. The desugared indices will be piped to the existing `GetSlice` implementations.

For indexing, currently `.[i]` is desugared to `.Item(i)`. After this change, `.[^i]` would be desugared to `.Item(GetReverseIndex(i))`.

For all the core collections, the provided implementation of `GetReverseIndex(i)` would be `collection.Length - i - 1` (See below for rationale of -1).

For third party collections, a `GetReverseIndex` method would need to exist for the `^` to work correctly. If `GetSlice` is implemented and `GetReverseIndex` is not, regular slicing would function as expected, but when `^` is present in the slice expression there would be an error thrown at compile time that looks like `GetReverseIndex is not defined`.

A `GetReverseIndex` is not implemented by default for third party collections because we do not know if the third party collection has a concept of `collection.Length`. 

# Drawbacks
[drawbacks]: #drawbacks

## Differing behavior with current inclusive-inclusive slicing compared to other languages

The current F# slicing behavior is front-inclusive and rear-inclusive, in contrast to front-inclusive and rear-exclusive for C# and Python. This means that if we choose to implement `GetReverseIndex(i)` as `myList.Length - i - 1`, then:

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

## Third party collections could implement reverse slicing in an inconsistent way

Because `GetReverseIndex` needs to be implemented by third party collections, they could implement it in a way that is inconsistent with the proposed behavior in Core. For example, if a third party author decides to implement `GetReverseIndex(i)` as `arr.Length - i` without the `-1`, this would result in different behavior compared to core collections.

If a user is using the third party collection alongside core collections, this would be very confusing as `collection.[..^1]` could return different elements even if the two collections contain the same items.

# Alternatives
[alternatives]: #alternatives

- Using `-` instead of `^`: not possible since negative indexes can be valid
- Defining `^i` as `mylist.Length - i` without the `-1`. This would allow `list[-1:] == list.[^1..]` but would cause `list.[..^1]` to be different.
- Define `^i` to mean `mylist.Length - i` if used as the starting index of a slice and `mylist.Length - i - 1` if used as the end index. This would align the behavior completely with C#, but it would mean that the slicing would change from inclusive-inclusive to inclusive-exclusive if the `^` operator was used.
- Providing a default `GetReverseIndex` for third party collections.

# Compatibility
[compatibility]: #compatibility

Please address all necessary compatibility questions:
* Is this a breaking change? **Yes**

This would break any code that defines `..^` as a custom operator.

## Third-party collections

We currently require third party collections to implement `<'T>.GetSlice` to support slicing syntax and `<'T>.Item` for indexing. We can additionally require third party collections to implement `<'T>.GetReverseIndex` if they wish to support from-the-end indexing and slicing. If they choose to do implement only `GetSlice`/`Item`, any `^i` indices will fail with a compile time error.

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
