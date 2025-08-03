# F# RFC FS-1330 - Support tailcalls in computation expressions

The design suggestion [Computation expressions should support syntax desugaring for return!/yield! in tailcall positions](https://github.com/fsharp/fslang-suggestions/issues/1006) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1006)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/18804)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

# Summary

Augment computation expression de-sugaring to desugar `return!`/`yield!` to `ReturnFromFinal`/`YieldFromFinal` when they occur at the natural tailcall position if the method is present on the computation expression builder. Likewise `do!` in the final position translate to `ReturnFromFinal` if present.

# Motivation

Allow CE builders to support tailcalls easier by detecting tailcalls during checking. 

# Detailed design

Naming TBD

See example usage in coroutines.fsx (TODO: add link here)

# Drawbacks

Why should we *not* do this?

# Alternatives

What other designs have been considered? What is the impact of not doing this?

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change?
> Possibly, if a builder already defines ReturnFromFinal`/`YieldFromFinal` methods.

* What happens when previous versions of the F# compiler encounter this design addition as source code?
>N/A

* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
>N/A

* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?
>N/A

# Pragmatics

## Diagnostics

Please list the reasonable expectations for diagnostics for misuse of this feature.

## Tooling

>N/A

## Performance

Please list any notable concerns for impact on the performance of compilation and/or generated code

>N/A

## Scaling

>N/A

## Culture-aware formatting/parsing

>N/A

# Unresolved questions

What parts of the design are still TBD?
>Naming?
