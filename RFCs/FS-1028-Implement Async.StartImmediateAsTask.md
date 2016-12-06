# F# RFC FS-1028 - Implement Async.StartImmediateAsTask

The design suggestion Implement Async.StartImmediateAsTask (https://github.com/fsharp/fslang-suggestions/issues/521) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/521)
* [ ] Details: [under discussion]
* [ ] Implementation: [In progress]


# Summary
[summary]: #summary
Implement Async.StartImmediateAsTask function to Async module in order to better interop with C# async-await. 

# Motivation
[motivation]: #motivation

Current functions in async method does not allow starting a task immediately. Async.StartImmediate, only starts the async workflow without returning a value,
where as Async.StartAsTask creates a new task on a new thread. Async.StartImmediateAsTask starts a task imeddiately however this task start running immediately.
This is the preferred solution for interacting existing C# async-await structures that expect you to return a Task.

# Detailed design
[design]: #detailed-design

To implement this feature, we can make use of Async.StartWithContinuations function, that can be wrapped inside a TaskCompletionSource.
Since Async.StartWithContinuations start immediately and we are able to extract the result as a continuation.

Example code:

```fsharp
 async {
        printfn "1- %A" (Thread.CurrentThread.ManagedThreadId)
        do! Async.Sleep 1000
        printfn "2- %A" (Thread.CurrentThread.ManagedThreadId)
        do! Async.Sleep 1000
        printfn "3- %A" (Thread.CurrentThread.ManagedThreadId)
        do! Async.Sleep 1000
        printfn "4- %A" (Thread.CurrentThread.ManagedThreadId)
        return 1
    }
 

```
If a synchronization context exists, the above code should return the same thread number. For applications like console, then thread 2 and 3 will return the same id. 
So a possible output for above code assuming running as Console application is :
1- 1
2- 4
3- 4
4- 4

Where as using StartAsTask would yield this:
1- 3
2- 4
3- 4
4- 3

Note that the main thread ID for both cases is 1.

So a draft implementation for this feature would be 

```fsharp
  let StartImmediateAsTask<'T> (asyn : Async<'T>) =
       let ts = new TaskCompletionSource<'T>()
       let task = ts.Task
       Async.StartWithContinuations(
          asyn
          , (fun (k) -> ts.SetResult(k)), (fun exn -> ts.SetException(exn)), fun exn -> ts.SetCanceled())
      task
```

However real implementation should be a member since there is a need for optional CancellationToken parameter.

# Drawbacks
[drawbacks]: #drawbacks

A possible drawback is that perhaps Async module is getting to crowded with a lot of similar functions.

# Alternatives
[alternatives]: #alternatives

None.

# Unresolved questions
[unresolved]: #unresolved-questions

None.
