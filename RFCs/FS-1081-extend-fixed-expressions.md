# F# RFC FS-1081 - Extend fixed expressions to support byref types and types implementing GetPinnableReference()


The design suggestion [Extend fixed expressions to support more types](https://github.com/fsharp/fslang-suggestions/issues/761) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [ ] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/fill-me-in)
* [ ] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/FILL-ME-IN)
* [ ] Implementation: [In progress](https://github.com/Microsoft/visualfsharp/pull/FILL-ME-IN)


# Summary
[summary]: #summary

The `fixed` expression should be extended `byref<'a>` types and any type that implements the C#-style [`GetPinnableReference()`](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-7.3/pattern-based-fixed) pattern.

# Motivation
[motivation]: #motivation

It should be possible to pin and take native references of types like `Span<'T>` and `Memory<'T>` via `fixed` statements, as this is useful especially in native interop scenarios. At the moment, there are no workarounds in pure F#: you can do this in C#, but it's impossible in F#. The best you can do is write that part of your code in C#.

# Detailed design
[design]: #detailed-design

This section of the RFC needs more detail and specific examples. Please feel free to contribute by editing this file and submitting a PR to fsharp/fslang-design.

This will require both type-checker and codegen changes. Statements in the following form: `use ptr = fixed expr` now need to successfully type-check for any `expr` where its type is a `byref<'a>` type or something that implements `GetPinnableReference()`. The code generator will need to generate code that pins these references and takes their addresses. For reference, here is the C# proposal: https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.3/pattern-based-fixed.md.

# Drawbacks
[drawbacks]: #drawbacks

This feature would introduce more special rules into the language.

# Alternatives
[alternatives]: #alternatives

- Don't do this, providing no alternative for those who want to use Span<'T> and friends with native code from F#
- Only extend `fixed` to support `ref` types
- Only extend `fixed` to support `GetPinnableReference()`
  
# Compatibility
[compatibility]: #compatibility

Please address all necessary compatibility questions:
* Is this a breaking change?
* What happens when previous versions of the F# compiler encounter this design addition as source code?
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?


# Unresolved questions
[unresolved]: #unresolved-questions

None
