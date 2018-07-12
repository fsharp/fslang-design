# F# RFC FS-1036 - Consistent Slicing

The design suggestion [Consistent Slicing](https://github.com/Microsoft/visualfsharp/issues/2643) has been approved for RFC [here](https://github.com/Microsoft/visualfsharp/issues/3474).
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [ ] [Suggestion](https://github.com/Microsoft/visualfsharp/issues/2643)
* [ ] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/217)
* [ ] Implementation: [Complete](https://github.com/Microsoft/visualfsharp/pull/3475)


# Summary
[summary]: #summary
Currently, if `x` is a list, `x.[..j]` gives an out of bounds exception when j<0, while `x.[0..j]` gives `[]`.
This proposes changing `x.[..j]` to give the same output as `x.[0..j]`, and `x.[i..]` to give the same output as `x.[i..(x.Length-1)]`

The same logic also applies to lists, strings, and arrays, including multidimensional arrays.


# Motivation
[motivation]: #motivation

If `l` is a list, `l.[0..j]` takes the range x:i≤x≤y and look up those elements, forming a new list. So when i>j the result is `[]`, and when i≤j and i or j are not in bounds, there is an error.

For consistency, `l.[..j]` should behave the same way, but it doesn't. When j<0, `l.[..j]` fails.

This includes the critical case `l.[.. -1]`, the natural base case, which should give `[]`.

Currently it is very hard to express in words what `x.[..j]` and `x.[i..]` currently mean. They do not correspond to a clear or useful concept.

The same issues apply to `l.[i..]`, which fails when i≥l.Length, including the critical case when i=l.Length.

The flaw in `l.[..j]` generates **unexpected runtime errors**. E.g.

- If n is a position in the string s, you would expect `s.[..(n-1)]` to be the substring prior to n but this is not true at the moment (for n=0).
- You want to split a list l into the first n elements and the remaining elements. You write: `let a,b = l.[..(n-1)], l.[n..]`. This code unexpectedly fails when n=0 and and when n=l.Length.


# Detailed design
[design]: #detailed-design

When `l=[0;1;2]` the current behavior is 

| `j'           | l.[0..j]      | l.[..j] | l.[j..2]  | l.[j..] |
| ------------- | ------------- | ------- | ------    | ------- |
| ≤-1           | []            | **error** | error     | error   |
| 0             | [0]           | [0]     | [0;1;2]   | [0;1;2] |
| 1             | [0;1]         | [0;1]   | [1;2]     | [1;2]   |
| 2             | [0;1;2]       | [0;1;2] | [2]       | [2]     |
| ≥3            | error         | error   | []        | **error** |

The bold errors should be changed to [] for consistency.

`l.[..j]` should give:
[] if j < 0
the first j+1 elements of l when -1 ≤ j ≤ l.Length-1
error when l.Length≤j

Similarly `l.[i..]` should give:
error when i < 0
the last (l.Length-i) elements of l when 0 ≤ i ≤ l.Length
[] when l.Length ≤ i

# Drawbacks
[drawbacks]: #drawbacks

None. It would be very hard for this to break any code.

# Alternatives
[alternatives]: #alternatives

None

# Unresolved questions
[unresolved]: #unresolved-questions

None; these changes are straightforward.
