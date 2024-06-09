# F# RFC FS-1011 - Warn when recursive function is not tail-recursive

The design suggestion [Enable a compiler-warning when a recursive algorithm is not tail-recursive][UserVoice] has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] [Suggestion][Suggestion]
* [x] Details: [under discussion][Details]
* [x] [Implementation][Implementation]

  [Suggestion]:https://github.com/fsharp/fslang-suggestions/issues/721
  [Details]:https://github.com/fsharp/fslang-design/discussions/82
  [Implementation]:https://github.com/dotnet/fsharp/pull/15503

# Summary
[summary]: #summary

Add an TailRecursiveAttribute to enable a compiler-warning when a recursive algorithm is not tail-recursive. This should ideally also cover recursive seq { .. } and async { ... } expressions.

# Motivation
[motivation]: #motivation

Recursive functions which are not properly tail-recursive can lead to unexpected `StackOverflowException`s at runtime. By issuing a warning for recursive functions which are not tail-recursive, developers receive feedback to allow them to address such potential issues.

# Detailed design
[design]: #detailed-design

Recursive functions which require tail-recursion would apply a `[<TailCall>]` attribute to the recursive function.

All uses of an attributed function within its recursive scope would need to be in tail position. Thus passing the function as a higher-order argument would not be allowed. This can potentially limit the utility of such a function.

```fsharp
[<TailCall>] 
let rec f x = .... f (x-1)

[<TailCall>] 
let rec f x = .seq { ... yield! f (x-1) }

[<TailCall>] 
let rec g x = .async { ... return g (x-1) }
```

Example code, adapted from [Jack Pappas' Gist](https://gist.github.com/jack-pappas/9860949):

```fsharp
let foo x =
    printfn "Foo: %x" x

[<TailCall>]
let rec bar x =
    match x with
    | 0 ->
        foo x           // OK: non-tail-recursive call to a function which doesn't share the current stack frame (i.e., 'bar' or 'baz').
        printfn "Zero"
        
    | 1 ->
        bar (x - 1)     // Warning: this call is not tail-recursive
        printfn "Uno"
        baz x           // OK: tail-recursive call.

    | x ->
        printfn "0x%08x" x
        bar (x - 1)     // OK: tail-recursive call.
        
and [<TailCall>] baz x =
    printfn "Baz!"
    bar (x - 1)         // OK: tail-recursive call.
```

# Drawbacks
[drawbacks]: #drawbacks

* Attributed functions would not be able to be used as non-tailcalls or higher-order functions within their frame.
* Using `module rec` or `namespace rec` or object programming creates large recursive scopes, making the use of this attribute pretty much impossible. Thus you can't have both these good things and must make an unstable choice between them

# Alternatives
[alternatives]: #alternatives

* Use the reserved `tailcall` keyword rather than an attribute
* Apply the inspection to all recursive functions

# Unresolved questions
[unresolved]: #unresolved-questions

* What warning level should the warning be issued at?
