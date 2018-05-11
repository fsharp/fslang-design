# F# RFC FS-1051 - Add readonly struct attribute automatically and use IsReadOnly attribute

.NET has a new attribute `IsReadOnly` that can be applied to struct types and other things.
This RFC deals with its interaction with struct types.

* [x] Approved in principle
* [ ] [Discussion](https://github.com/fsharp/fslang-design/issues/FILL-ME-IN)
* [ ] Implementation: [In progress](https://github.com/Microsoft/visualfsharp/pull/FILL-ME-IN)


# Summary
[summary]: #summary

.NET has a new attribute `IsReadOnly` that can b applied to struct types and other things. This
is now starting to appear in .NET libraries and C# code.

F# structs are normally readonly, it is quite hard to write a mutable struct.

* Knowing a struct is readonly gives more efficient code and fewer warnings.

* The key part of the F# compiler code is [CanTakeAddressOfImmutableVal](https://github.com/Microsoft/visualfsharp/blob/16dd8f40fd79d46aa832c0a2417a9fd4dfc8327c/src/fsharp/TastOps.fs#L5582)

* The C# 7.2 feature is described here: https://blogs.msdn.microsoft.com/mazhou/2017/11/21/c-7-series-part-6-read-only-structs/

* A related issue is https://github.com/Microsoft/visualfsharp/pull/4576 which deals with oddities in the warnings about struct mutation.

This RFC proposes to

* Add the IsReadOnly attribute automatically when an F# struct is inferred to be readonly
* Make use of the presence of inference of the IsReadOnly attribute to improve F# codegen and reduce the number of warnings being given in F# code.

# Motivation
[motivation]: #motivation

Parity with .NET/C#, better codegen.


# Detailed design
[design]: #detailed-design

Here is an example of a typical readonly struct in F#:
```fsharp
type S(count1: int, count2: int) = 
    member x.Count1 = count1
    member x.Count2 = count2
```

# Drawbacks
[drawbacks]: #drawbacks

None

# Alternatives
[alternatives]: #alternatives

None

# Compatibility
[compatibility]: #compatibility

TBD

# Unresolved questions
[unresolved]: #unresolved-questions

* An implementation needs to be done, it will fluch out the issues
