# F# RFC FS-1067 - Async.Sequential and maximum degree of parallelism

The design suggestion [Allow sequential processing for a sequence of asyncs](https://github.com/fsharp/fslang-suggestions/issues/706) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

Additionally, it covers the design of a `maxDegreeOfParallelism` optional parameter in an overload to `Async.Parallel`.


# Summary
[summary]: #summary

Add support for an equivalent of `Async.Parallel`, which we will call `Async.Sequential`, which enforces **sequential** processing of multiple workflows in a fork/join manner. The signature would be the same as `Async.Parallel` i.e. `seq<Async<'T>> -> Async<'T []>`.

Additionally, add an overload to `Async.Parallel` that lets you tune the degree of parallelism: `seq<Async<'T>> -> ?maxDegreeOfParallelism: int -> Async<'T []>`.

# Motivation
[motivation]: #motivation

Sequential processing is useful in many cases where you need to, for example, throttle a number of asynchronous computations to a certain size. For example, you have 100 messages but can only send one at a time. Async.Parallel cannot achieve this sort of behavior as it would send all of them together. In addition, combined with `Seq.chunkBySize`, you can control throttling at a specific batch size using `Parallel` for inner batches and `Sequential` for the overall control flow.

Additionally, it is useful to fine-tune the degree of parallelism that you would like to process async computations with. This number is typically an integer that represents the number of cores on a machine, but it could be a different value.

# Detailed design: `Async.Sequential`
[design]: #detailed-design

The key behavioral difference between this and the existing `Async.Parallel` is that this would enforce processing of multiple workflows in a **sequential** "queue", before returning all the aggregated results to the caller. Otherwise the signature would be the same as `Async.Parallel`.

## Code samples
Example code:

```fsharp
// 1. example collection of workflows
let workflows = [| for x in 1 .. 10 -> async { printfn "Working on %d" x } |]

// 2. using Async.Parallel
workflows |> Async.Parallel |> Async.RunSynchronously |> ignore

(*
Working on 1Working on Working on 5
Working on 6Working on Working on 8
Working on Working on Working on 7

Working on 9
3
10
4
2

*)

// 3. using Async.Sequential
workflows |> Async.Sequential |> Async.RunSynchronously |> ignore

(*
Working on 1
Working on 2
Working on 3
Working on 4
Working on 5
Working on 6
Working on 7
Working on 8
Working on 9
Working on 10
*)
```

## Detailed design: `maxDegreeOfParallelism`

An additional overload for `Async.Parallel` is added: `seq<Async<'T>> -> ?maxDegreeOfParallelism: int -> Async<'T []>`

A key difference between these two overloads is that when the first `Parallel` is called, all async computations are queued for execution rather than immediately executed. If the overload with `maxDegreeOfParallelism` is called, and `maxDegreeOfParallelism` is specified as a non-negative number, that many computations are started rather than queued for scheduling.

## Relationship between `Sequential` and `maxDegreeOfParallelism`

`Async.Sequential` is implemented as a call to `Async.Parallel(tasks, ?maxDegreeOfParallelism=1)`. This forces sequential processing of async computations but also allows for more flexibility with advanced scenarios.

# Drawbacks
[drawbacks]: #drawbacks

1. It's another way to perform multiple asynchronous computations in a batch.
2. Some guidance and documentation will need to be provided to instruct users when to use this new variant.
3. It increases the surface area of FSharp.Core.

# Alternatives
[alternatives]: #alternatives

People are evidently already coming up with hand-rolled solutions for this. On the one hand, that shows that it's doable and people can survive without it in FSharp.Core. On the other hand, it suggests that its not an uncommon requirement and one that should be provided in FSharp.Core with an optimal implementation.

# Compatibility
[compatibility]: #compatibility

* This is a non-breaking change.
* It should be backwards compatible with older versions of the F# compiler, assuming that the implementation does not use newer F# features.

# Unresolved questions
[unresolved]: #unresolved-questions

1. A full implementation needs to be worked up.
2. Should it support batching? e.g. `workflows |> Async.Sequential 10 // execute 10 workflows at a time in parallel.` If so, perhaps a name like `Async.ParallelBatch` or `Async.ParallelChunkBySize` might be more appropriate (one could set the batch size to 1 to achieve sequential behavior).