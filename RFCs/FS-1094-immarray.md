# F# RFC FS-1094 - `immarray` as FSharp.Core collection, an abbreviation for System.Collections.Immutable.ImmutableArray

The design suggestion [A normative immutable array type](https://github.com/fsharp/fslang-suggestions/issues/619) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [ ] Implementation TBD
- [ ] Design Review Meeting(s) with @dsyme and others invitees
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/528)

# Summary

A new collection type `'T immarray` will be added to FSharp.Core, and FSharp.Core will now depend on System.Collections.Immutable

# Motivation

Prior to this RFC F# programming has no strong opinion on an immutable array data structure in regular F# coding.
There are lots of use cases for this and it is easy enough to implement efficiently.

Fable and the Elmish design patterns are popularizing the use of immutable data for important model descriptions
more and more and we should be helping improve the situation for that kind of programming.

The main question is to how to make on immutable array type normative in F# coding

We have decided it is acceptable for FSharp.Core to take a dependency on `System.Collections.Immutable`. We still want an FSharp.Core
module making it look and feel like a normal F# collection.


# Detailed design

### Syntax additions

Expressions and patterns are extended with the syntax `[: ... :]` for block expressions and patterns.


### FSharp.Core additions

The additions follow a standard FSharp.Core collection design:

```fsharp
namespace FSharp.Collections

...

type 'T immarray = System.Collections.Immutable.ImmutableArray<'T>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module ImmArray =

    let append(l1 : 'T immarray) (l2 : 'T immarray) = l1.AddRange(l2)

    let empty<'T> = immarray<'T>.Empty
    ...
```

etc. with the standard functions and values.


### Structural equality, hashing and comparison

`System.Collections.Immutable.ImmutableArray` implements `IStructuralEquality` and `IStructuralComparison`, so
this means F# structural equality, hashing and comparison semantics are respected "all the way down".

TBD: examples and test cases


# Drawbacks

* `FSharp.Core` has a dependency on `System.Collections.Immutable.dll` and any transitive dependencies.  


# Alternatives

- Don't do this and maintain status quo

- Make a separate package `FSharp.Collections.Immutable.dll`

- Use a different name

# Compatibility

This is not a breaking change. The elaboration of existing code that passes type checking is not changed.

This doesn't extend the F# metadata format.

# Unresolved questions

* Full API design

* Consider the extension property `IsEmpty`.

