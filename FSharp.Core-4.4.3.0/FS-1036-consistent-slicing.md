# F# RFC FS-1036 - Consistent Slicing

The design suggestion [Consistent Slicing](https://github.com/Microsoft/visualfsharp/issues/2643) has been approved for RFC [here](https://github.com/Microsoft/visualfsharp/issues/3474).
This RFC covers the detailed proposal for this suggestion.

* [ ] Approved in principle
* [ ] [Suggestion](https://github.com/Microsoft/visualfsharp/issues/2643)
* [ ] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/217)
* [ ] Implementation: [Completed PR #1](https://github.com/Microsoft/visualfsharp/pull/3475) | [In Progress PR #2](https://github.com/dotnet/fsharp/pull/7541)


# Summary
[summary]: #summary
Currently, if `x` is a list, `x.[..j]` gives an out of bounds exception when j<0, while `x.[0..j]` gives `[]`.
This proposes changing `x.[..j]` to give the same output as `x.[0..j]`, and `x.[i..]` to give the same output as `x.[i..(x.Length-1)]`

In addition, there is some inconsistency in the out-of-bounds behavior for slicing these collections as well. There is an opportunity here to make the out-of-bounds slicing behavior more consistent and predictable for users.

The same logic also applies to lists, strings, and arrays, including multidimensional arrays.


# Motivation
[motivation]: #motivation


## `l.[0..j]` vs `l.[..j]`
If `l` is a list, `l.[0..j]` takes the range x:i≤x≤y and look up those elements, forming a new list. So when i>j the result is `[]`, and when i≤j and i or j are not in bounds, there is an error.

For consistency, `l.[..j]` should behave the same way, but it doesn't. When j<0, `l.[..j]` fails.

This includes the critical case `l.[.. -1]`, the natural base case, which should give `[]`.

Currently it is very hard to express in words what `x.[..j]` and `x.[i..]` currently mean. They do not correspond to a clear or useful concept.

The same issues apply to `l.[i..]`, which fails when i≥l.Length, including the critical case when i=l.Length.

The flaw in `l.[..j]` generates **unexpected runtime errors**. E.g.

- If n is a position in the string s, you would expect `s.[..(n-1)]` to be the substring prior to n but this is not true at the moment (for n=0).
- You want to split a list l into the first n elements and the remaining elements. You write: `let a,b = l.[..(n-1)], l.[n..]`. This code unexpectedly fails when n=0 and and when n=l.Length.

## `l.[(-1)..0]` vs `l.[0..(-1)]`

In our current impmentation of slicing, when an out-of-bounds index is given for the beginning or end of the slice, the resultant behavior can differ depending on the case.

For example, `l.[(-1)..0]` throws an error, because -1 is not a valid index in the list. Based on this, one may expect `l.[0..(-1)]` to also throw an error. However, the latter slice completes successfully and returns `[]`.

To illustrate this, we provide a more detailed examination of F#, C#, and Python slicing behaviors:


Assuming we have a list `L` with lower bound `a` and upper bound `b`, then:
`L = { L[a], L[a+1] ... L[b-1], L[b] } `

Our current slicing behavior in F# looks like:

`L.[x..y]` | x < a | a <= x <= b | x > b
--------|-------|---------------|-------
y < a | if x > y [] else Error | [] | []
a <= y <= b | Error | if x > y [] else `{ L[x] .. L[y] }` | []
y > b | Error | Error | if x > y [] else Error


The current behavior in C# .NET preview is:

`L[x..y]` | x < a | a <= x <= b+1 | x > b+1
-------|--------|------------------|------
y < a |  Error | Error | Error
a <= y <= b+1 | Error | `{ L[x] .. L[y-1] }` | Error
y > b+1 | Error | Error | Error

The current behavior in Python is:

`L[x:y]`  | a <= x <= b+1 | x > b+1
-----------------|------------------|------
a <= y <= b+1 | if x > y [] else `{ L[x] .. L[y-1] }` | []
y > b+1 |  `{ L[x] .. L[b] }` | []

(Note: We can't have x < a and y < a cases because Python negative indices mean something else)

---

As in the example above the slicing behavior in F# is a lot more confusing than C# or Python. Sometimes when indexes are out of bounds you get [], sometimes you get Error, and sometimes you get either. 

C# disallows any out of bound indexes and Python just takes `L[x:y] = L[x:min(y, b)]` and `[]` if the bounds don't make sense. This is much easier to create a mental model of, especially to new users.

# Detailed design
[design]: #detailed-design

## [COMPLETED] `l.[0..j]` vs `l.[..j]`

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

## `l.[(-1)..0]` vs `l.[0..(-1)]`

This is the proposed out-of-bound slicing behavior for F#:

`L.[x..y]` | x < a | a <= x <= b | x > b
--------|-------|---------------|-------
y < a | [] | [] | []
a <= y <= b |  `{ L[a] .. L[y] }` |  if x > y [] else `{ L[x] .. L[y] }` | []
y > b | ` { L[a] .. L[b] } ` | ` { L[x] .. L[b] }` | []

This is pretty much equivalent to `L.[x..y] = L.[max(x, a)..min(y, b)]` and [] otherwise. The slicing behavior would be consistent with Python.

Using the example above, we'd end up with:


| `j'           | l.[0..j]      | l.[..j] | l.[j..2]  | l.[j..] |
| ------------- | ------------- | ------- | ------    | ------- |
| ≤-1           | []            | []     | [0;1;2]     | [0;1;2]   |
| 0             | [0]           | [0]     | [0;1;2]   | [0;1;2] |
| 1             | [0;1]         | [0;1]   | [1;2]     | [1;2]   |
| 2             | [0;1;2]       | [0;1;2] | [2]       | [2]     |
| ≥3            | [0;1;2]         | [0;1;2]   | []        | [] |

Why?
- The legacy behavior has been `L.[0..(-1)] = []`, so we don't want to change that to Error all of a sudden. 
- This would work with concatenation and recursive cases where we need a base case of `l.[0..(-1)] = []` (see @charlesroddie's example in discussion).
- For people who are doing calculations on arrays of numbers (perhaps ML workloads) your life would be easier as you wouldn't have to explicitly check for bounds.
- This would bring F# more in line with python and make it more accessible for python users.
- It's much more clearer and consistent.

# Drawbacks
[drawbacks]: #drawbacks

- We'd change the behavior for cases like `L.[0..99999]` and `L.[-1..0]` where they would become valid. If users are catching that exception and performing logic based on that we'd break their code (they shouldn't be doing so anyways). 
- If you accidentally access an out-of-bounds slice the compiler's not going to warn you about it.

# Alternatives
[alternatives]: #alternatives

- Keep existing behavior for out-of-bounds slicing
- Follow the C# behavior and throw errors.

# Unresolved questions
[unresolved]: #unresolved-questions

None; ~~these changes are straightforward.~~
