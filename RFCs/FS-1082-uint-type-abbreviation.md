# FS 1082 - `uint` Type Abbreviation in FSharp.Core

The design suggestion [Add `uint` type abbreviation to FSharp.Core](https://github.com/fsharp/fslang-suggestions/issues/818) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/818)
* [x] [Discussion](https://github.com/fsharp/fslang-design/issues/423)
* [x] [Implementation](https://github.com/dotnet/fsharp/pull/8185)

# Summary

Add the `uint` type abbreviation to FSharp.Core to align with the `int` type abbreviation.

# Motivation

The following three type abbreviations exist in FSharp.Core today:

* `int32`
* `int`
* `uint32`

The `int` abbreviation is equivalent to `int32`. However, there is no corresponding `uint` abbreviation. This feels like an omission more than a deliberate decision. In fact, the F# compiler already assumes that the abbreviation exists in [formatting](https://github.com/dotnet/fsharp/blob/master/src/utils/sformat.fs#L147) and [an error message](https://github.com/dotnet/fsharp/blob/master/src/fsharp/FSComp.txt#L790). Because of this, the type abbreviation should be added.

# Detailed design

The addition is trivial:

```diff
type uint = uint32
```

This would allow F# programmers to write code like this:

```fsharp
let f (x: uint) = ()
```

Formatting with `%u` should also work because this is an abbreviation for the same type as `uint32`.

# Drawbacks

None.

# Alternatives

Do nothing.

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change?

No.

* What happens when previous versions of the F# compiler encounter this design addition as source code?

They fail to compile, unless that type abbreviation is already defined in their source code.

* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?

They continue working.

* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?

They continue working.

# Unresolved questions

None.
