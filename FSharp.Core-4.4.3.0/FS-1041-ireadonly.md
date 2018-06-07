# F# RFC FS-1041 - Implement IReadOnly* interfaces throughout FSharp.Core

The design suggestions [Implement IReadOnly* wherever possible in FSharp.Core](https://github.com/Microsoft/visualfsharp/issues/3999#issuecomment-346435080) and [Support IReadonlyDictionary<'Key,'Vale> on dict and Map types](https://github.com/fsharp/fslang-suggestions/issues/622) have been marked "approved in principle". This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] [Suggestion](https://github.com/Microsoft/visualfsharp/issues/3999#issuecomment-346435080)
* [x] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/238)
* [x] Implementation: [1](https://github.com/Microsoft/visualfsharp/pull/4014) and [2](https://github.com/Microsoft/visualfsharp/pull/3988).


# Summary
[summary]: #summary

Implement the IReadOnly interfaces on relevant F# collection typed throughout FSharp.Core.

# Motivation
[motivation]: #motivation

This mainly helps complete the picture on these types, because they are already readonly.

# Detailed design
[design]: #detailed-design

The following table details which type implements which interface.

|Type|`IReadonlyCollection`|`IReadonlyList`|`IReadonlyDictionary`|
|----|---------------------|---------------|---------------------|
|`Set`|**Yes**|No|No|
|`Map`|**Yes**|No|**Yes**|
|`List`|Already implemented|**Yes**|No|
|`dict` return value|**Yes**|No|**Yes**|

The implementation is very straightforward, as each type has all of the data required to implement each member in each interface.  In some cases, it is as simple as returning the existing `Count` property.

The `Array` type is a mutable collection, so it does not implement any of these interfaces.  The `Seq` type is an alias for `IEnumerable`, so it will not implement any of these interfaces.

# Drawbacks
[drawbacks]: #drawbacks

Not really a drawback, but this requires a new version of FSharp.Core, which means that the version of this binary that Visual Studio deploys will be different for a time.

# Alternatives
[alternatives]: #alternatives

N/A.

# Compatibility
[compatibility]: #compatibility

This is neither a breaking change nor an introduction of a brand-new construct, so there should be no issues with respect to compatibility.


# Unresolved questions
[unresolved]: #unresolved-questions

N/A.
