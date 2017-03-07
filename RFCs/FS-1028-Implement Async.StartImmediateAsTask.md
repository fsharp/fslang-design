# F# RFC FS-1028 - Implement Async.StartImmediateAsTask

The design suggestion, Implement `Async.StartImmediateAsTask` (https://github.com/fsharp/fslang-suggestions/issues/521) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/521)
* [x] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/153)
* [x] Implementation: implemented (https://github.com/Microsoft/visualfsharp/pull/2534)

# Summary
[summary]: #summary
Implement `Async.StartImmediateAsTask` function to the `Async` module in order to better interop with C# async-await keywords. 

# Motivation
[motivation]: #motivation

Current functions in the `Async` module does not allow starting a BCL Task "immediately". `Async.StartImmediate`, starts an async workflow immediately but returning unit, thus making it unconvertible to a Task, whereas `Async.StartAsTask` creates a new Task on a new thread. `Async.StartImmediateAsTask` would start a Task immediately however unlike the existing methods, this Task starts running within the current thread and also it can return a result which is wrapped into a Task.
This is the preferred way for interacting with the C# async-await keywords that expect you to return a "started" Task but does not expect you to create thread.
# Detailed design
[design]: #detailed-design

For this feature, we can make use of `Async.StartWithContinuations` function that can be wrapped inside a `TaskCompletionSource`.
Since `Async.StartWithContinuations` starts an async workflow immediately and also we are able to extract the result as a continuation.

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
If a synchronization context exists, the above code should return the same thread number. For applications like console, then thread 2, 3 and 4 will return the same id. 
So a possible output for the above code assuming it is running as a Console application is :

    1- 1
    2- 4
    3- 4
    4- 4

whereas using `StartAsTask` would yield the following:

    1- 3
    2- 4
    3- 4
    4- 3

Note that the main thread ID for both cases is 1.

So a draft implementation for this feature would be: 

```fsharp
  let StartImmediateAsTask<'T> (asyn : Async<'T>) =
       let ts = new TaskCompletionSource<'T>()
       let task = ts.Task
       Async.StartWithContinuations(
          asyn
          , (fun (k) -> ts.SetResult(k)), (fun exn -> ts.SetException(exn)), fun exn -> ts.SetCanceled())
      task
```

However the real implementation should be a member rather than a function, since there is a need for optional `CancellationToken` parameter.

# Drawbacks
[drawbacks]: #drawbacks

A possible drawback is that perhaps the `Async` module is getting too crowded with a lot of similar functions.

# Alternatives
[alternatives]: #alternatives

None.

# Unresolved questions
[unresolved]: #unresolved-questions

None.
