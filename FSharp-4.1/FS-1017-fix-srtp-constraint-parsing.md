# F# RFC FS-1017 - (Allow all inferrable SRTP constraints to be written)

The design suggestion [Allow all inferrable SRTP constraints to be written](https://fslang.uservoice.com/forums/245727-f-language/suggestions/7887270-allow-all-inferrable-srtp-constraints-to-be-writte) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] [User Voice Request](https://fslang.uservoice.com/forums/245727-f-language/suggestions/7887270-allow-all-inferrable-srtp-constraints-to-be-writte)
* [x] Details: [no separate discussion](https://github.com/fsharp/fslang-design/blob/master/FSharp-4.1/FS-1017-fix-srtp-constraint-parsing.md)
* [x] Implementation: [Completed](https://github.com/Microsoft/visualfsharp/pull/1278)


# Summary
[summary]: #summary

This RFC proposes to fix a minor nuisance parser issue which hobbles signature files and also results in some unintuitive messages from the compiler.

# Motivation
[motivation]: #motivation

The current SRTP constraint syntax has a quirk in which only type variables (i.e. `^a`) but not type names (e.g. `foo` in `type foo = | Hober of string`) are syntactically legal.  Despite this, F# will often print type names in inferred constraints and diagnostic messages--which the user cannot actually write himself!  This tiny change allows type names to also be written in SRTP constraints, making them simpler to write and eliminating a source of confusion.  Most importantly, this fixes the longstanding issue of bit-rotted support for signature files--it was possible to write F# code with types that could not be represented in signature files, preventing their use entirely in affected code.

# Detailed design
[design]: #detailed-design

This is effectively little more than a correction to the parser and AST.  The operation of the type checker remains the same.

# Drawbacks
[drawbacks]: #drawbacks

There do not seem to be any reasons *not* to do this.  The change does not break existing code and there is no added surface area for complexity or failure.

# Alternatives
[alternatives]: #alternatives

Since this is basically a bug fix, other designs have not been considered.  The outcome of not fixing the issue would be that signature files would remain effectively unusable, and users would continue to experience occasional diagnostics of dubious quality.

# Unresolved questions
[unresolved]: #unresolved-questions

The fix has already been prepared.
