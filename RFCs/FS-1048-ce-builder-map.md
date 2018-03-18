# F# RFC FS-1048 - Extend ComputationExpression builders with Map : m<'a> * ('a -> 'b) -> m<'b>

The design suggestion [extend ComputationExpression builders with Map : m<'a> * ('a -> 'b) -> m<'b>](https://github.com/fsharp/fslang-suggestions/issues/36) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [ ] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/36)
* [ ] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/258)
* [ ] Implementation: Not started


# Summary
[summary]: #summary

Computation expression builders would be allowed to implement a new method, `Map`, that the compiler will call in appropriate scenarios.

# Motivation
[motivation]: #motivation

Whenever a `let!` is followed by a `return`, the compiler currently must emit both a Bind and a Return. This could be optimized by allowing the compiler to replace these two calls with one; namely, Map.

For example, take the following code:

```fsharp
builder {
    let! x = f ()
    return g x
}
```

Currently, the compiler emits:

```fsharp
builder.Bind(f (), fun x -> builder.Return (g x))
```

This is wasteful.

# Detailed design
[design]: #detailed-design

Take the following code (repeated from [Motivation]):

```fsharp
builder {
    let! x = f ()
    return g x
}
```

if `builder` defines a `Map` method, this would now get desugared to:

```fsharp
builder.Map(f (), fun x -> g x)
```

Alternatively, if `builder` does not contain a `Map` method, the existing behavior should be preserved (emitting the usual `Bind` and `Return`).

Finally, existing CE builders should be modified to take advantage of this and implement `Map`.

# Drawbacks
[drawbacks]: #drawbacks

This introduces additional complexity into computation expressions (both from the language _and_ compiler sides).

# Compatibility
[compatibility]: #compatibility


This is not a breaking change, and is backwards-compatible with existing code.

* CE builders not implementing `Map` (and code using these builders) will be compiled in the same way as before.
* Previous F# compilers encountering `Map` will simply never emit calls it, always using `Bind` and `Return`.
* Previous F# compilers encountering this new construct in compiled binaries will not care -- it will be seen as just another method call.

# Unresolved Questions
[unresolved]: #unresolved-questions

*For more detailed discussions of the following questions and their proposed resolutions, please see the [RFC discussion](https://github.com/fsharp/fslang-design/issues/258).*

1. This document currently only specifies behavior in terms of `return`: should we allow this to be utilized by `yield` as well?
    * If so, should we do this by looking for CE methods `MapReturn` and `MapYield` instead of `Map`? Or are there other ways?
2. Should we apply this behavior to situations where the `let!` and `return` in question are not the last calls in the builder? For example, should we transform the following to `map` (and how would that work)?

    ```fsharp
    builder {
        let! x = f a
        return x
        let! y = g b
        return y }
    ```
