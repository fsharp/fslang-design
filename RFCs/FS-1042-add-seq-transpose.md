# F# RFC FS-1042 - (Add Transpose to Seq, List and Array)

The design suggestion [Seq.transpose](https://github.com/fsharp/fslang-suggestions/issues/106) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [X] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/106)
* [X] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/236)
* [X] Implementation: [In progress](https://github.com/Microsoft/visualfsharp/pull/4020)


# Summary
[summary]: #summary

Add transpose methods to Seq, List and Array modules which swap rows and columns in the source.

# Motivation
[motivation]: #motivation

This is a standard operation on matrices (or lists of lists) which could be a useful addition to the core library.

# Detailed design
[design]: #detailed-design

Given a sequence of m collections of length n, transpose returns a collection of n collections of length m, where result.[i].[j] = source.[j].[i], for 0 <= i < n and 0 <= j < m

There is a choice of signatures here:

1. `M(M('T)) -> M(M('T))`
3. `seq<#seq<'T>> -> M(M('T))`
2. `seq<M('T)> -> M(M('T))`

Option 1:

```fsharp
module Array = val transpose: array:'T[][]] -> 'T[][]
module List = val transpose: lists:'T list list -> 'T list sist
module Seq = val transpose: source:seq<seq<'T> -> seq<seq<'T>>
```

Option 2:

```fsharp
module Array = val transpose: arrays:seq<#seq<'T>> -> 'T[][]
module List = val transpose: lists:seq<#seq<'T>> -> 'T list sist
module Seq = val transpose: source:seq<#seq<'T> -> seq<seq<'T>>
```

Option 3:

```fsharp
module Array = val transpose: arrays:seq<'T[]> -> 'T[][]
module List = val transpose: lists:seq<'T list> -> 'T list sist
module Seq = val transpose: source:seq<#seq<'T> -> seq<seq<'T>>
```

It is proposed to go with option 3 here.

Example code:

```fsharp
let t = Array.transpose <| seq [ [|1..3|]; [|4..6|] ]
// t should be [| [|1;4|]; [|2;5|]; [|3;6|] |]
```

Corner cases:
* Similar to other methods in F# Core (e.g. zip), transpose should fail if given a jagged array or list (e.g. [[1..3]; [1..2]]) whereas transpose on Seq should not fail if the inner sequences are of different lengths

* Given an input of m empty collections, transpose should return an empty collection. (m x 0 -> 0 x m)

# Drawbacks
[drawbacks]: #drawbacks

Like any addition to the F# Core API, we need to make sure that it is justified and useful.

Also, this PR, if accepted, would mean extra work to merge in the PR which is fundamentally changing how Seq is implemented in F#.

# Alternatives
[alternatives]: #alternatives

Require programmers to implement this themselves in their own programs.

# Compatibility
[compatibility]: #compatibility

As this is just an addition to F# Core and does not require nany new syntax, it is not a breaking change to the compiler


# Unresolved questions
[unresolved]: #unresolved-questions

None

