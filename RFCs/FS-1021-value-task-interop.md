# F# RFC FS-1021 - Support Interop with ValueTask in Async Type

There is no UserVoice feature for this RFC.  This was created after a short discussion with [dsyme](github.com/dsyme) about and upcoming feature in Roslyn.

* [x] Approved in principle
* [ ] [User Voice Request](https://fslang.uservoice.com/FILL-ME-IN)
* [ ] Details: [under discussion](https://github.com/fsharp/FSharpLangDesign/issues/FILL-ME-IN)
* [ ] Implementation: [In progress](https://github.com/Microsoft/visualfsharp/pull/FILL-ME-IN)


# Summary
[summary]: #summary

A big feature has been added to Roslyn to support [Task-like returns for async methods](https://github.com/dotnet/roslyn/pull/12518).  This no longer ties async code in C# or VB to the `Task` and `Task<T>` objects.  This brings a number of benefits, namely the ability to write async methods which return `ValueTask<T>` which can provide significant performance benefits in some scenarios.

We should support interop with `ValueTask<T>` in the `Async` type to support consuming code which uses this feature via two functions: `Async.AwaitValueTask` and `Async.StartAsValueTask`.

In the long term, we may wish to consider augmenting async programming so that we can support arbitrary Task-like returns rather than having to add new conversation functions in the Async type for each type.

There is more information about Task-like returns for async methods available in the [Arbitrary Async Returns](https://github.com/ljw1004/roslyn/blob/features/async-return/docs/specs/feature%20-%20arbitrary%20async%20returns.md) document.

# Motivation
[motivation]: #motivation

When the Roslyn feature is shipped, we anticipate a lot of code getting updates to use `ValueTask<T>` for peformance benefits.  Adding the ability to await and start ValueTasks from an async workflow fills a hole in interop scenarios.

# Detailed design
[design]: #detailed-design

The design here is quite simple - Three functions on the Async module with the following signatures:

```fsharp
static member AwaitValueTask: task: ValueTask<'T> -> Async<'T>

static member StartAsValueTask : computation:Async<'T> * ?taskCreationOptions:TaskCreationOptions * ?cancellationToken:CancellationToken -> ValueTask<'T>

static member StartChildAsvalueTask : computation:Async<'T> * ?taskCreationOptions:TaskCreationOptions -> Async<ValueTask<'T>>
```

Interoperating would then look just like `AwaitTask` and `StartAsTask`:

```fsharp
open LibraryWithValueTask

let f x = async {
    let! res = LibraryWithValueTask.GetAsync(x) // this method returns ValueTask<T> 
               |> Async.AwaitValueTask
    ... 
}

let g = async { ... }


let valueTask = Async.StartAsValueTask(g)
// pass the ValueTask<T> to something
```

# Drawbacks
[drawbacks]: #drawbacks

In the future, we may want to have a design in place for properly handling Task-like returns from async methods that doesn't involve adding more static members to the `Async` type.  Adding these members now may complicate that and could result in methods which are present for backwards compatibility, but are classified as "don't use this".

# Alternatives
[alternatives]: #alternatives

The alternative here is designing full support for Task-like return types on async methods.  There are a number of ways this would go, but at the bare minimum it would involve a interop story that's better than just adding static members to the `Async` type.

# Unresolved questions
[unresolved]: #unresolved-questions

* Are `ValueTask<T>`s capable of being created with `TaskCompletionOption`s?  If not, then `StartAsValueTask` and `StartChildAsvalueTask` wouldn't need the parameter with those options.

Any further unresolved questions are more in line with how we support general Task-like returns.  This would be a future RFC.