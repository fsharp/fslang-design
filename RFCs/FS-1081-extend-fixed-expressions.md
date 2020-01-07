# F# RFC FS-1081 - Extend fixed expressions to support byref types and types implementing GetPinnableReference()


The design suggestion [Extend fixed expressions to support more types](https://github.com/fsharp/fslang-suggestions/issues/761) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [ ] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/761)
* [ ] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/421)
* [ ] Implementation: [Not started]


# Summary
[summary]: #summary

The `fixed` expression should be extended to support `byref<'a>` types and any type that implements the C#-style [`GetPinnableReference()`](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-7.3/pattern-based-fixed) pattern.

# Motivation
[motivation]: #motivation

It should be possible to pin and take native references of types like `Span<'T>` and `Memory<'T>` via `fixed` statements, as this is useful especially in native interop scenarios. At the moment, there are no workarounds in pure F#: you can do this in C#, but it's impossible in F#. The best you can do is write that part of your code in C#.

# Detailed design
[design]: #detailed-design

Currently, statements of the following form: `use ptr = fixed expr` are allowed when `expr` is one of:
* Array
* String
* Address of an array element
* Address of a field

FS-1081 adds the following to the list of allowed types for `expr`:
* byref
* any 'a when 'a implements `GetPinnableReference() : unit -> byref<'t>`

The code generator will need to generate code that pins these references and takes their addresses. For reference, here is the C# proposal: https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.3/pattern-based-fixed.md.

# Drawbacks
[drawbacks]: #drawbacks

This feature would introduce more special rules into the language.

# Alternatives
[alternatives]: #alternatives

- Don't do this, providing no alternative for those who want to use Span<'T> and friends with native code from F#
- Only extend `fixed` to support `byref<'a>` types
- Only extend `fixed` to support `GetPinnableReference()`
  
# Compatibility
[compatibility]: #compatibility

Please address all necessary compatibility questions:
* Is this a breaking change?
  * No.
* What happens when previous versions of the F# compiler encounter this design addition as source code?
  * A pre-FS1081 F# compiler will generate a compiler error.
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
  * A pre-FS1081 F# compiler will not encounter any issues because any differences in codegen that this design addition creates will be contained to method/function body IL, and will not affect the public interface of user code.
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?
  * N/A - no changes to FSharp.Core are prescribed by FS-1081.

# Unresolved questions
[unresolved]: #unresolved-questions

None
