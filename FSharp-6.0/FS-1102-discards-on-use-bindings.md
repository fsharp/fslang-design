# F# RFC FS-1102 - Discards on use bindings

The design suggestion [Allow underscore in use bindings](https://github.com/fsharp/fslang-suggestions/issues/881) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] Approved in principle
- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/881)
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/11630)
- [x] ~~Design Review Meeting(s) with @dsyme and others invitees~~ Not needed
- [Discussion](https://github.com/fsharp/fslang-design/discussions/578)

# Summary

The left-hand-side receiver of `use` bindings will allow discards.

# Motivation

F# 4.7 implemented [wildcard self identifiers](https://github.com/fsharp/fslang-suggestions/issues/333) as two underscores were frequently
used in member definitions to denote an ignored "self" identifier and this seemed like a hack given that the language already provided a wildcard
pattern that represents an unused value. However, this same scenario still exists for `use` bindings.

# Detailed design

Just allow the underscore to be specified to the left to indicate that the value is not actually used.

# Drawbacks

This introduces more complexity to the language.

# Alternatives

- Not doing this. Workarounds will have to be used for ignoring results from `use` bindings like
```fs
use __ = ...
__ |> ignore // Mutes the unused variable warning
```

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change? No.
* What happens when previous versions of the F# compiler encounter this design addition as source code? Error as usual.
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries? The code will work.
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct? This is not a change to FSharp.Core.

# Unresolved questions

None.
