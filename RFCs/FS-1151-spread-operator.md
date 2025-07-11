# F# RFC FS-1151 - Spread operator

The design suggestion [Spread operator for F#](https://github.com/fsharp/fslang-suggestions/issues/1253) has been marked "approved in principle."

This RFC covers the detailed proposal for this suggestion.

- [x] [Spread operator for F#](https://github.com/fsharp/fslang-suggestions/issues/1253)
- [x] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/TODO)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/TODO)

# Summary

We add support in the following specific contexts for spread expressions of the form `...source`, wherein the symbol `...` prefixed to a `source` expression or type indicates that a set of values or members is to be copied or "spread" from the value or type represented by the source expression into the target context in some way.
