# F# RFC FS-1330 - Support tailcalls in computation expressions

The design suggestion [Computation expressions should support syntax desugaring for return!/yield! in tailcall positions](https://github.com/fsharp/fslang-suggestions/issues/1006) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1006)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/18804)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

# Summary

Augment computation expression de-sugaring to desugar `return!`/`yield!` to `ReturnFromFinal`/`YieldFromFinal` when they occur at the natural tailcall position if the method is present on the computation expression builder. Likewise `do!` in the final position translate to `ReturnFromFinal` if present.

# Motivation

Allow CE builders to support tailcalls easier by detecting tailcalls during checking. 

# Detailed design

If a computation expression builder provides a `YieldFromFinal` method
```fsharp
    // The implementation of `yield!`
    member inline _.YieldFrom (other: Coroutine) : CoroutineCode = 
        ResumableCode.While((fun () -> not other.IsCompleted), CoroutineCode(fun sm -> 
            other.MoveNext()
            let __stack_other_fin = other.IsCompleted
            if not __stack_other_fin then
                // This will yield with __stack_yield_fin = false
                // This will resume with __stack_yield_fin = true
                let __stack_yield_fin = ResumableCode.Yield().Invoke(&sm)
                __stack_yield_fin
            else
               printfn "done YieldFrom"
               yieldFromCount <- yieldFromCount + 1
               true))

    // The implementation of `yield!`, non-standard for tailcalls
    member inline _.YieldFromFinal (other: Coroutine) : CoroutineCode =
        ResumableCode<_,_>(fun sm ->
            sm.Data.TailcallTarget <- Some other
            false)
```
it will be picked instead of `YieldFrom` when `yield!` is in a tail-call position:
```fsharp
let testTailcallTiny () = 
    coroutine {
        printfn "in testTailcallTiny"
        yield! t1() // will desugar to YieldFromFinal
    }

let testNonTailcall () = 
    coroutine {
        try 
            yield! t1() // will desugar to YieldFrom
        finally ()
    }
```

If a `do!` is in a tail-call position it will get translated to `ReturnFromFinal` or to `YieldFromFinal` if the former is not defined.

# Drawbacks

Why should we *not* do this?

# Alternatives

What other designs have been considered?

What is the impact of not doing this?
> CE Builders have no easy way to detect tail-calls.

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change?
> Possibly, if a builder already defines `ReturnFromFinal`/`YieldFromFinal` methods.

* What happens when previous versions of the F# compiler encounter this design addition as source code?
> CE authors should be aware that older compilers (older SDKs) will not call the FromFinal methods.
The CE should work in a progressive enhancement kind of way - making sure it stays correct even when the FromFinal is not called.

* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
>N/A

* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?
>N/A

# Pragmatics

## Diagnostics

Please list the reasonable expectations for diagnostics for misuse of this feature.

## Tooling

>N/A

## Performance

Please list any notable concerns for impact on the performance of compilation and/or generated code

>N/A

## Scaling

>N/A

## Culture-aware formatting/parsing

>N/A

# Unresolved questions

What parts of the design are still TBD?
>Naming?
