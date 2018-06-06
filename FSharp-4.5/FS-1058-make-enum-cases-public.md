# F# RFC FS-1057 - Make Enum cases public

This RFC documents the change [Make enum cases public](https://github.com/Microsoft/visualfsharp/pull/5002).

The goal is to align our code generation for enumerations with C# by making them emit as public.

* [x] Approved in principle
* [x] Implementation: [Complete](https://github.com/Microsoft/visualfsharp/pull/5002)
* [x] Discussion: https://github.com/Microsoft/visualfsharp/pull/5002

# Summary
[summary]: #summary

F# enumeration generation will be public to align with how C# does it.

# Motivation
[motivation]: #motivation

The motivation for this is twofold:

1. Align with C# for the sake of consistency with the rest of the .NET platform.
2. Allow for more predictable behavior when analyzing logs from profiling F# code.

The latter case was discovered in recent work to add more logging capabilities to the F# compiler and tools. When analyzing logs, the _value_ of the enumeration (rather than the label) is emitted, making it more difficult to analyze logs unless those enumerations are also marked as public.

# Detailed design

Code gen enumeration cases to be public under all circumstances.

# Drawbacks
[drawbacks]: #drawbacks

This can break people who, in reflection code, depend on the existing behavior.

# Alternatives
[alternatives]: #alternatives

Do nothing.

# Compatibility
[compatibility]: #compatibility

This does not break code unless it uses reflection _and_ depends on existing enum case code generation in that reflection code.

# Unresolved questions
[unresolved]: #unresolved-questions

None.