# F# RFC FS-1052 - Allow `IsByRefLike`  attribute on structs ("ref structs") and respect it when present

.NET has a new attribute `IsByRefLike` that can be applied to struct types. It is used for the feature C# calls "ref structs".
This RFC deals with respecting this attribute in F#.

* [x] Approved in principle
* Discussion: https://github.com/fsharp/fslang-design/issues/287
* Implementation: [In Progress](https://github.com/Microsoft/visualfsharp/pull/4888)


# Summary
[summary]: #summary

.NET has a new attribute `IsByRefLike` that can be applied to struct types. It is used for the feature C# calls "ref structs".
This RFC deals with respecting this attribute in F#. This RFC proposes to

* Test adding the `IsByRefLike` attribute to structs in F# code
* Respect the `IsByRefLike` attribute when it occurs in .NET libraries

There are already ByRefLike types in F# - just a fixed collection of them.  We basically need to make this set of types extensible through the atttribute.

The C# feature is described here:
* https://blogs.msdn.microsoft.com/mazhou/2018/03/02/c-7-series-part-9-ref-structs/

The `Span` type is labelled `IsByRefLike`.

# Motivation
[motivation]: #motivation

Parity with .NET/C#, better codegen.


# Detailed design
[design]: #detailed-design

Here is an example of what declaring a ref struct would look like in F#:
```fsharp
open System.Runtime.CompilerServices

[<IsByRefLike>]
type S(count1: int, count2: int) = 
    member x.Count1 = count1
    member x.Count2 = count2
```

We could potentially give an alias for the attribute.


Separately, C# attaches the following `Obsolete` attribute to these types, and presumably has special code to ignore it:
```
Types with embedded references are not supported in this version of your compiler
```
We would have to also ignore this attribute.


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

* An implementation needs to be done, it will flush out the issues

