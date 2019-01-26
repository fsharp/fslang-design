# F# RFC FS-1067 - Async.Sequential

The design suggestion [Allow sequential processing for a sequence of asyncs](https://github.com/fsharp/fslang-suggestions/issues/706) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/706)
* [ ] Details:
* [ ] Implementation:


# Summary
[summary]: #summary

Add support for an equivalent of `Async.Parallel`, which we will call `Async.Sequential`, which enforces **sequential** processing of multiple workflows in a fork/join manner. The signature would be the same as `Async.Parallel` i.e. `Seq<Async<'T>> -> Async<'T []>`

# Motivation
[motivation]: #motivation

Sequential processing is useful in many cases where you need to, for example, throttle a number of asynchronous computations to a certain size. For example, you have 100 messages but can only send one at a time. Async.Parallel cannot achieve this sort of behaviour as it would send all of them together. In addition, combined with `Seq.chunkBySize`, you can control throttling at a specific batch size using `Parallel` for inner batches and `Sequential` for the overall control flow.

# Detailed design
[design]: #detailed-design

The key behavioural difference between this and the existing `Async.Parallel` is that this would enforce processing of multiple workflows in a **sequential** "queue", before returning all the aggregated results to the caller. Otherwise the signature would be the same as `Async.Parallel`.

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

## Possible implementations

There are several alternative implementations linked in [the source issue](https://github.com/fsharp/fslang-suggestions/issues/706) that are worth reading through, but a couple of possible starting points are below. No consideration has been given to error handling or cancellation tokens etc. at this point.

### 1. Using recursion
```fsharp
static member Sequential workflows =
    let rec sequential results (workflows:_ Async list) = async {
        match workflows with
        | [] ->
            return results |> List.rev
        | workflow :: workflows ->
            let! result = workflow
            return! sequential (result :: results) workflows }
    sequential [] (List.ofSeq workflows)
```

### 2. Using a mutable ResizeArray in a for loop
```fsharp

static member Sequential workflows =
    let results = ResizeArray(Seq.length workflows)
    async {
        for workflow in workflows do
            let! workflow = workflow
            results.Add workflow
            return results.ToArray() }
```
The second option is more concise and simpler to reason about, as well as the fact that it contains fewer GC operations (in my initial testing, operating over a million workflows which merely returned a number:

```
Recursion: Real: 00:00:00.894, CPU: 00:00:00.890, GC gen0: 30, gen1: 30, gen2: 0
Imperative: Real: 00:00:00.668, CPU: 00:00:00.671, GC gen0: 40, gen1: 0, gen2: 0
```

Note the lack of gen1 collections in the imperative implementation.

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

1. A full impplementation needs to be worked up.
2. Should it support batching? e.g. `workflows |> Async.Sequential 10 // execute 10 workflows at a time in parallel.` If so, perhaps a name like `Async.ParallelBatch` or `Async.ParallelChunkBySize` might be more appropriate (one could set the batch size to 1 to achieve sequential behaviour).


