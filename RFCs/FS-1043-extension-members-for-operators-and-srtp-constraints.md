# F# RFC FS-1043 - Extension members should be available to solve operator trait constraints

These design suggestions:
* https://github.com/fsharp/fslang-suggestions/issues/230
* https://github.com/fsharp/fslang-suggestions/issues/29

have been marked "approved in principle". This RFC covers the detailed proposal for these

* [x] Approved in principle
* [ ] Implementation: [In progress](https://github.com/dotnet/fsharp/pull/6805)


# Summary
[summary]: #summary

Extension methods are previously ignored by SRTP constraint resolution.  This RFC mean they are taken into account.

For example, consider
```fsharp

type System.String with
    static member ( * ) (foo, n: int) = String.replicate n foo

let r4 = "r" * 4
let spaces n = " " * n
```
Prior to this RFC the result is:
```
foo.fs(2,21): warning FS1215: Extension members cannot provide operator overloads.  Consider defining the operator as part of the type definition instead.
foo.fs(4,16): error FS0001: The type 'int' does not match the type 'string'
```

# Motivation
[motivation]: #motivation

It is reasonable to use extension methods to retrofit operators and other semantics on to existing types. This "completes the picture" with everything allowed by extension methods in a natural way.


# Detailed design
[design]: #detailed-design

The main technical challenges are

- the precise specification of scoping, operator resolution and SRTP constraints

- backwards compat


# Drawbacks
[drawbacks]: #drawbacks

* This slightly strengthens the "type-class"-like capabilities of SRTP resolution. This means that people may increasingly use SRTP code as a way to write generic, reusable code rather than passing parameters explicitly.  While this is reasonable for generic arithmetic code, it has many downsides when applied to other things.

# Alternatives
[alternatives]: #alternatives

1. Don't do it

# Compatibility
[compatibility]: #compatibility

TBD (there are important questions to look at here)

# Unresolved questions
[unresolved]: #unresolved-questions

TBD
