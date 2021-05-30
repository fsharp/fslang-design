# F# RFC FS-1103 - `chooseType` function for collection modules

The design suggestion [Enumerable.OfType<TResult> equivalent for List, Array and Seq modules](https://github.com/fsharp/fslang-suggestions/issues/527) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/527)
- [x] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [ ] Design Review Meeting(s) with @dsyme and others invitees
- [Discussion](https://github.com/fsharp/fslang-design/discussions/581)

# Summary

A function called `chooseType` will be added to `List`, `Array`, and `Seq` modules.

# Motivation

There is currently no equivalent of `Enumerable.OfType<TResult>` in the F# collection modules. Users have to define
this function [themselves](https://stackoverflow.com/q/2521254/5429648).

# Detailed design

The implementation of this function will copy `choose` but replacing the option with a function that does a type match.

The type signature will be:
```fs
val chooseType : source:'T list -> seq<'U>
val chooseType : source:'T[] -> seq<'U>
val chooseType : source:IEnumerable -> seq<'T> // Follows Seq.cast's signature
```

Example code:

```fsharp
let seqOfInts = [box 1; box 2] |> Seq.chooseType<int>
```

# Drawbacks

[From @dsyme](https://github.com/fsharp/fslang-suggestions/issues/527#issuecomment-521980214):
> Historically we haven't particularly emphasized runtime-type-based classification/filtering/querying
> as a technique in FSharp.Core. But it's entirely consistent with the rest of F# to do so.

# Alternatives

- Using the name `ofType`. Although this will be consistent with LINQ, this name is inconsistent with
the rest of F# that has a convention of functions starting with `of` being a creation function from another type.
Since this functions acts similarly to `choose` where the chooser chooses based on type matching, the name `chooseType` is used here.

- Not doing this. Users will continue to define their own functions.

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change? No.
* What happens when previous versions of the F# compiler encounter this design addition as source code? Works as usual.
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries? Works as usual.
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct? Works as usual.


# Unresolved questions

None.
