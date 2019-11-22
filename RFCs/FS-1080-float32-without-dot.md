# F# RFC FS-1080 - Float32 without dot

The design suggestion [Float32 literals without the numeric dot](https://github.com/fsharp/fslang-suggestions/issues/750) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion and some related design relaxations.

* [x] Approved in principle
* [x] [Discussion](https://github.com/fsharp/fslang-design/issues/414)
* [x] [Implementation](https://github.com/dotnet/fsharp/pull/7839)


# Summary
[summary]: #summary

ATM Float32 dots are required in Float32 literals. This is not consistent with F# decimal literals which do not require the dot. This RFC allows omission of the dot in Float32 literals.

## Code Examples

Currently this does not compile:

```fsharp
let f = 750f
```

After the change it will compile and will be equivalent to:

```fsharp
let f = 750.f
```

## Detailed Design

This is implemented by extending the lexer rules for Float32 literals.

# Drawbacks
[drawbacks]: #drawbacks

None

# Alternatives
[alternatives]: #alternatives

The main alternative is "don't do this" and continue to require the dot.

# Compatibility
[compatibility]: #compatibility

This is a non-breaking change.

# Unresolved questions
[unresolved]: #unresolved-questions

None
