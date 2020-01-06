# F# RFC FS-1081 - Extend fixed expressions to support ref types and types implementing GetPinnableReference()


The design suggestion [Extend fixed expressions to support more types](https://github.com/fsharp/fslang-suggestions/issues/761) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [ ] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/fill-me-in)
* [ ] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/FILL-ME-IN)
* [ ] Implementation: [In progress](https://github.com/Microsoft/visualfsharp/pull/FILL-ME-IN)


# Summary
[summary]: #summary

The `fixed` expression should be extended `ref` types and any type that implements the C#-style [`GetPinnableReference()`](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-7.3/pattern-based-fixed) pattern.

# Motivation
[motivation]: #motivation

It should be possible to pin and take native references of types like `Span<T>` and `Memory<T>` via `fixed` statements, as this is useful especially in native interop scenarios. At the moment, there are no workarounds in pure F#: you can do this in C#, but it's impossible in F#. The best you can do is write that part of your code in C#.

# Detailed design
[design]: #detailed-design

This is the bulk of the RFC. Explain the design in enough detail for somebody familiar
with the language to understand, and for somebody familiar with the compiler to implement.
This should get into specifics and corner-cases, and include examples of how the feature is used.

Example code:

```fsharp
let add x y = x + y
```

# Drawbacks
[drawbacks]: #drawbacks

This feature would introduce more special rules into the language.

# Alternatives
[alternatives]: #alternatives

- Don't do this, providing no alternative for those who want to use Spans<T> and friends with native code from F#
- Only extend `fixed` to support `GetPinnableReference()`
- Only extend `fixed` to support `ref` types
  
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
