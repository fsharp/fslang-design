# F# RFC FS-1047 - (match! syntactic sugar in computational expressions)

The design suggestion [match! syntactic sugar](https://github.com/fsharp/fslang-suggestions/issues/572) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/572)
* [x] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/255)
* [x] Implementation: [Complete](https://github.com/Microsoft/visualfsharp/pull/4427)

# Summary
[summary]: #summary

There should be a syntactic sugar, called `match!`, that is equivalent to `let!` followed by `match`.

# Motivation
[motivation]: #motivation

An oft-repeated idiom in monadic code (especially `async` workflows) entails binding a monadic expression, then immediately pattern matching over the result. This happens over and over. For example:

```fsharp
async {
    let! x = myAsyncFunction ()
    match x with
    | Case1 -> ...
    | Case2 -> ...
}
```

# Detailed design
[design]: #detailed-design

`match!` is a simple addition to the F# parser, and should be treated as an equivalent `let!` and `match`. Meaning, this code:

```fsharp
async {
    match! myAsyncFunction() with
    | Some x -> printfn "%A" x
    | None -> printfn "Function returned None!"
}
```
      
Would be compiled as if it were written as follows:

```fsharp
async {
    let! x = myAsyncFunction ()
    match x with
    | Some x -> printfn "%A" x
    | None -> printfn "Function returned None!"
}
```

# Drawbacks
[drawbacks]: #drawbacks

Additional complexity in the language and compiler.

# Alternatives
[alternatives]: #alternatives

The alternative is to refrain from adding this, where programmers will continue combining `let!` and `match`.

# Compatibility
[compatibility]: #compatibility

This is a completely backwards compatible change, and will only require changes to the F# compiler, not the library. No exisiting code will differ in functionality. Previous versions of the F# compiler that do not implement this syntax emit a compiler error when encountering an instance of it. Additionally, since this is syntactic sugar for existing constructs, compiled binaries using `match!` will be compatible with previous F# compilers and tools.

# Unresolved questions
[unresolved]: #unresolved-questions

None.
