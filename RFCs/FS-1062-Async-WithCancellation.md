# F# RFC FS-1062 - (Add Async.WithCancellation)

The design suggestion [Add Async.withCancellation](https://github.com/fsharp/fslang-suggestions/issues/685) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/685)
* [ ] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/FILL-ME-IN)
* [ ] Implementation: [In progress](https://github.com/dotnet/fsharp/pull/5892)


# Summary
[summary]: #summary

This feature would add `Async.WithCancellation`.  

# Motivation
[motivation]: #motivation

The motivation behind this RFC is there currently is no built in way to pass in external cancellation tokens. This would allow using outside CancellationToken, such as the [RequestAborted](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.httpcontext.requestaborted?view=aspnetcore-2.1) from `HttpContext`.  The problem with [Async.Start](https://msdn.microsoft.com/en-us/visualfsharpdocs/conceptual/async.start-method-%5Bfsharp%5D?f=255&MSPPError=-2147217396) is it does not provide a way to return a value. The code below is not difficult to implement but should be built into the standard library for consumers to use easily. 

# Detailed design
[design]: #detailed-design

The existing way of approaching this problem in F# is provided here: 

https://gist.github.com/eulerfx/c41d50c6fba8e88cf16a21ed7c3c14bd#file-async-withcancellation-fs

Inlined:

```fsharp
let withCancellation (ct:CancellationToken) (a:Async<'a>) : Async<'a> = async {
  let! ct2 = Async.CancellationToken
  use cts = CancellationTokenSource.CreateLinkedTokenSource (ct, ct2)
  let tcs = new TaskCompletionSource<'a>()
  use _reg = cts.Token.Register (fun () -> tcs.TrySetCanceled() |> ignore)
  let a = async {
    try
      let! a = a
      tcs.TrySetResult a |> ignore
    with ex ->
      tcs.TrySetException ex |> ignore }
  Async.Start (a, cts.Token)
  return! tcs.Task |> Async.AwaitTask }
```

Since all methods on `Async` are implemented as `static members`, that pattern should continue to be used with the proposed signature. 

```fsharp
/// <summary>Creates a new async computation that allows cancellation with a provided CancellationToken.</summary>
///
/// <param name="computation">The computation to run. </param>
/// <param name="cancellationToken">The CancellationToken to be associated with the computation.</param>

static member WithCancellation(computation : Async<'a>, cancellationToken : CancellationToken) = async {
  let! ct2 = Async.CancellationToken
  use cts = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken, ct2)
  let tcs = new TaskCompletionSource<'a>()
  use _reg = cts.Token.Register (fun () -> tcs.TrySetCanceled() |> ignore)
  let a = async {
    try
      let! a = computation
      tcs.TrySetResult a |> ignore
    with ex ->
      tcs.TrySetException ex |> ignore }
  Async.Start (a, cts.Token)
  return! tcs.Task |> Async.AwaitTask }
```

with the same body as shown above.



# Drawbacks
[drawbacks]: #drawbacks

There is a drawback to adding more members like this, as it does move more towards having a paradox of choice.

# Alternatives
[alternatives]: #alternatives

We may not want to rely entirely on `TaskCompletionSource<'a>` as it might not be best to context switch between `Async` and `Task`.  It might be worth while exploring creating something like `AsyncCompletionTask<'a>` with a reference implementation in [FSharp.Control.FusionTasks](https://github.com/kekyo/FSharp.Control.FusionTasks/blob/99e3cea2c5121ce00ea6e4c4750103c29a4b586a/FSharp.Control.FusionTasks/Infrastructures.fs#L188).  

# Compatibility
[compatibility]: #compatibility

Please address all necessary compatibility questions:
* Is this a breaking change? 

No

* What happens when previous versions of the F# compiler encounter this design addition as source code?

N/A

* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?

It will be binary compatible.

* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?

It should not be an issue.



# Unresolved questions
[unresolved]: #unresolved-questions

What parts of the design are still TBD?

None

