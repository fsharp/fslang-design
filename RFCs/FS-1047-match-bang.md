# F# RFC FS-NNNN - (Fill me in with a feature name)

The design suggestion [FILL ME IN](https://github.com/fsharp/fslang-suggestions/issues/572) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [ ] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/572)
* [ ] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/FILL-ME-IN)
* [ ] Implementation: [In progress](https://github.com/Microsoft/visualfsharp/pull/FILL-ME-IN)


# Summary
[summary]: #summary

There should be a syntatic sugar, called `match!`, that is equivalent to `let!` followed by `match`.

# Motivation
[motivation]: #motivation

An oft-repeated idiom in monadic code (especially `async`s) entails binding a monadic expression, then immediately pattern matching over the result. This happens over and over. For example:

    async {
      let! x = myAsyncFunction ()
      match x with
      | ... }
      

# Detailed design
[design]: #detailed-design

This is the bulk of the RFC. Explain the design in enough detail for somebody familiar
with the language to understand, and for somebody familiar with the compiler to implement.
This should get into specifics and corner-cases, and include examples of how the feature is used.

`match!` should a simple addition to the F# compiler/parser, and should be translated internally to an equivalent `let!` and `match`. Meaning, this code:

    async {
      let! x = myAsyncFunction ()
      match x with
      | Some x -> printfn "%A" x
      | None -> printfn "Function returned None!" }
      
will be compiled as if insead written as the following:

    async {
      match! myAsyncFunction () with
      | Some x -> printfn "%A" x
      | None -> printfn "Function returned None!" }

Example code:

```fsharp
let add x y = x + y
```

# Drawbacks
[drawbacks]: #drawbacks

Additional complexity in the language and compiler.

# Alternatives
[alternatives]: #alternatives

Don't add this, and programmers continue combining `let!` and `match`.

# Compatibility
[compatibility]: #compatibility

Please address all necessary compatibility questions:
* Is this a breaking change?
* What happens when previous versions of the F# compiler encounter this design addition as source code?
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?

This is a completely backwards compatible change, and will only require changes to the F# compiler, not the library. No exisiting code will differ in functionality. Previous versions of the F# compiler that do not implement this syntax emit a compiler error when encountering an instance of it. Additionally, since this is syntactic sugar for existing constructs, compiled binaries using `match!` will be compatible with previous F# compilers and tools.

# Unresolved questions
[unresolved]: #unresolved-questions

N/A.
