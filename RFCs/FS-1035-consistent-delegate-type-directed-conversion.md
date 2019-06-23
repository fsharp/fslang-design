# F# RFC FS-1035 - Consistent type directed conversion from functions to .NET delegates

The design suggestion [Stronger type directed conversion from functions to .Net delegates](https://github.com/fsharp/fslang-suggestions/issues/248) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/248)
* [ ] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/210)
* [ ] Implementation: not started


# Summary
[summary]: #summary

Currently type directed conversion from functions to .NET delegates only works if called method have overloads.
It will make type directed conversion more consistent and predictable if it behaves the same
regardless whether method have overloads or not.

# Motivation
[motivation]: #motivation

It is very confusing to see a compilation error when type directed conversion didn't worked because method lacked unrelated overload,
e.g. the following would fail:
```F#
type Test() =
    member this.Test(action : System.Func<int, int>) = action.Invoke(1)

let test x = x + 1
Test().Test (test) // This expression was expected to have type System.Func<int,int> but here has type int -> int
```

But adding unrelated overload to that method enables type directed conversion hence making code compileable:
```F#
type Test() =
    member this.Test(x) = x + 1 |> ignore
    member this.Test(action : System.Func<int, int>) = action.Invoke(1)

let test x = x + 1
Test().Test (test)
```

# Detailed design
[design]: #detailed-design

Type directed conversion extends to happen even if a method does not have overloads.
From the user perspective nothing changes except that functions can be passed as delegates in more scenarious.

# Drawbacks
[drawbacks]: #drawbacks

TBD

# Alternatives
[alternatives]: #alternatives

## Don't do it
As a workaround, wrapping function into a lamdba works in all cases, e.g.
```F#
let test x = x + 1
Test().Test (fun x -> test x)
```

# Unresolved questions
[unresolved]: #unresolved-questions

TBD
