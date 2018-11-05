# F# RFC FS-1066 - Add tryExactlyOne to array, list and seq

The design suggestion [Add tryExactlyOne to array, list and seq](https://github.com/fsharp/fslang-suggestions/issues/137) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/137)
* [x] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/344)
* [x] Implementation: [In progress](https://github.com/Microsoft/visualfsharp/pull/5804)


# Summary
[summary]: #summary

Add function `tryExactlyOne` working like `exactlyOne`, but notthrowing and returning `option` type to `array`, `list` and `seq` modules.

# Motivation
[motivation]: #motivation

We want to increase the consistency of `FSharp.Core`. `tryExactlyOne` function is the missing safe (nonthrowing) deconstructor for `singleton` function and the safe alternative for `exactlyOne`. We expect to see users replacing their ad hoc implementations of `tryExactlyOne` and using it for their new use cases.

# Detailed design
[design]: #detailed-design

The functions should copy the behavior of `exactlyOne`, but instead of throwing they should return `None`. The null check and `ArgumentNullException` throw for `array` and `seq` should not be replaced by `None` as calling the function with `null` is considered exceptional and undesired. 

Example code:

```fsharp
List.tryExactlyOne []
// None
List.tryExactlyOne [1]
// Some 1
List.tryExactlyOne [1; 2]
// None

Array.tryExactlyOne null
// ArgumentNullException
Array.tryExactlyOne [||]
// None
Array.tryExactlyOne [|1|]
// Some 1
Array.tryExactlyOne [|1; 2|]
// None

Seq.tryExactlyOne null
// ArgumentNullException
Seq.tryExactlyOne (Seq.ofList [])
// None
Seq.tryExactlyOne (Seq.ofList [1])
// Some 1
Seq.tryExactlyOne (Seq.ofList [1; 2])
// None
```

# Drawbacks
[drawbacks]: #drawbacks

* Extending api surface.
* Confusion of C# developers expecting the function to throw (as the similar function from LINQ throws).

# Alternatives
[alternatives]: #alternatives

* Implementing this function with throwing for more than one element. Not choosing this path means that this function won't be a direct replacement for `System.Linq.IEnumerable.SingleOrDefault`.
* Implementing this function with `None` for `null` seqs and arrays. This would introduce inconsistency with other array and seq functions as they all throw on null.

# Compatibility
[compatibility]: #compatibility

Please address all necessary compatibility questions:
* Is this a breaking change?

No.

* What happens when previous versions of the F# compiler encounter this design addition as source code?

The same as if an older F# compiler encounters the `ValueOption` type. This is primarily just additional functions.

* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?

The same behavior as before, since this is binary-compatible.

* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?

Since this is binary-compatible, no change other than seeing new functions occurs.


# Unresolved questions
[unresolved]: #unresolved-questions

N/A

