# F# RFC FS-1046 - Wildcard self identifiers

The design suggestion [Wildcard self identifiers](https://github.com/fsharp/fslang-suggestions/issues/333) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/333)
* [x] Details
* [x] Implementation: [Complete](https://github.com/dotnet/fsharp/pull/6829)


# Summary
[summary]: #summary

Allow single underscore character as ignored self identifier.

# Motivation
[motivation]: #motivation

Makes underscore semantic more consistent across the language.

# Detailed design
[design]: #detailed-design

Current workaround for ignoring self identifier in instance members is to use double underscores:

```fsharp
type C() = 
   member __.P = 1
```

Suggestion is to enhance parse to allow single underscore::

```fsharp
type C() = 
   member _.P = 1
```

Currently above results in syntax error:
> Unexpected symbol '.' in member definition. Expected 'with', '=' or other token."

# Drawbacks
[drawbacks]: #drawbacks

Additional complexity in parsing rules.

# Alternatives
[alternatives]: #alternatives

Continue using double underscore workaround.

# Compatibility
[compatibility]: #compatibility

No compatibility issues identified.

# Unresolved questions
[unresolved]: #unresolved-questions

None.
