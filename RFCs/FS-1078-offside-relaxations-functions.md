# F# RFC FS-1078 - Offside relaxations for functions

The design suggestion [Allow undentation for constructors](https://github.com/fsharp/fslang-suggestions/issues/724) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion and some related design relaxations.

* [x] Approved in principle
* [ ] Discussion
* [x] [Implementation](https://github.com/Microsoft/visualfsharp/pull/6314)


# Summary
[summary]: #summary

F#'s indentation rules are overly stringent for the argument lists of functions. This RFC relaxes the rules for these cases by adding some permitted "undentations", much like what was done for static members and constructors in F# 4.7.

## Code Examples

Currently this gives the indentation warning `FS0058 Possible incorrect indentation: this token is offside...`:

```fsharp
let f(a:int,
    b:int, c:int, // Warning today, 'b' needs to be aligned with 'a'
    d:int) = ...
```

In this case, `a` sets the offside line and `b` needs to be aligned with `a` to avoid the warning.

Allowing "undentation" in these three cases would remove the warning.

It would also allow:

```fsharp
let f(
    a:int, // Warning today, 'a' needs to be aligned with or after '('
    b:int, c:int) = ...
```

## Detailed Design

Function definitions should be added to the list of permitted "undentations" in the F# language spec.

In the language of the spec, the "undentation" is permitted from the bracket starting a sequence of arguments in a definition, but the block must not "undent" past other offside lines.

# Drawbacks
[drawbacks]: #drawbacks

None

# Alternatives
[alternatives]: #alternatives

The main alternative is "don't do this" and continue to require indentation.

# Compatibility
[compatibility]: #compatibility

This is a non-breaking change.

# Unresolved questions
[unresolved]: #unresolved-questions

None
