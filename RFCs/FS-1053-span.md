# F# RFC FS-1053 - Add support for `Span`

.NET has a new feature `Span`. This RFC adds support for this in F#.


* [x] Approved in principle
* Discussion: TBD
* Implementation: [In progress](https://github.com/Microsoft/visualfsharp/pull/4888)

# Summary
[summary]: #summary

.NET has a new feature `Span`. This RFC adds support for this in F#.

Span is actually built from other features, covered by these RFCs
* https://github.com/fsharp/fslang-design/blob/master/RFCs/FS-1051-readonly-struct-attribute.md
* https://github.com/fsharp/fslang-design/blob/master/RFCs/FS-1052-isbyreflike-on-structs.md

This RFC will cover any extra work needed to get realistic support for `Span`.

# Motivation
[motivation]: #motivation

Parity with .NET/C#, better codegen, performance, interop.

# Detailed design
[design]: #detailed-design

TBD

# Drawbacks
[drawbacks]: #drawbacks

None

# Alternatives
[alternatives]: #alternatives

None

# Compatibility
[compatibility]: #compatibility

TBD

# Unresolved questions
[unresolved]: #unresolved-questions

* An implementation needs to be done, it will fluch out the issues
