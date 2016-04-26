# F# RFC FS-1012 - Support for caller info argument attributes (CallerLineNumber, CallerFileName, CallerMemberName)

The design suggestion [F# compiler should support CallerLineNumber, CallerFilePath etc](https://fslang.uservoice.com/forums/245727-f-language/suggestions/8899330-f-compiler-should-support-callerlinenumber-calle) has been marked "planned".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] Details: [under discussion](https://github.com/fsharp/FSharpLangDesign/issues/84)
* [x] Implementation: [In progress](https://github.com/Microsoft/visualfsharp/issues/1114)


# Summary
[summary]: #summary

This RFC describes F# support for the emerging .NET standard for compile-time treatment of method arguments tagged with one of
the following attributes from the `System.Runtime.CompilerServices` namespace:

  - `CallerLineNumberAttribute`
    - An integer optional argument with this attribute will be given a runtime value matching the line number of the source of the callsite
  - `CallerFilePathAttribute`
    - A string optional argument with this attribute will be given a runtime value matching the absolute file path of source of the callsite
  - `CallerMemberNameAttribute`
    - A string optional argument with this attribute will be given a runtime value matching the unqualified name of the enclosing member of the callsite

[Brief description and motivation on MSDN.](https://msdn.microsoft.com/en-us/library/hh534540.aspx)

# Motivation
[motivation]: #motivation

These attributes are useful for diagnostic and logging purposes, among others. They provide a way to obtain stack-trace or symbol-like source
information in a lightweight way at runtime, perhaps for inclusion in a log line. They also help developers with patterns like `INotifyPropertyChanged`,
providing a way to track member or property names as strings without hard-coded literals.

# Detailed design
[design]: #detailed-design

TBD

# Drawbacks
[drawbacks]: #drawbacks

The major drawback is that added complexity this brings to the rules of the language.

# Alternatives
[alternatives]: #alternatives

Some alternatives are:

- Implement an F#-specific version of this feature.  This is rejected because it is better to conform to .NET standards rather than be F#-specific for this feature.

- Do not implement this feature.  This is rejected because it is better to conform to .NET standards for this topic.


# Unresolved questions
[unresolved]: #unresolved-questions

- What happens with first-class uses of attributed methods?
- Does the use of an attribute change the way type inference and method selection works for a method?
