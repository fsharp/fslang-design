# F# RFC FS-1058 - Improve async stack traces

This RFC documents the change [Improve async stack traces](https://github.com/Microsoft/visualfsharp/pull/4867).

The high-level goal is to make stack traces for exceptions which occur in an asynchronous workflow more comprehensible.

* [x] Approved in principle
* [x] Implementation: [Complete](https://github.com/Microsoft/visualfsharp/pull/4867)
* [x] Discussion: https://github.com/Microsoft/visualfsharp/pull/4867

# Summary
[summary]: #summary

Debugging computationally embedded languages gives bad stack traces. The most common case where this comes up is with `async { }` computation expressions. We wish to make these stack traces better; i.e., easier for developers to diagnose when exceptions are thrown.

# Motivation
[motivation]: #motivation

There are two major problems today:

## Problem One - losing stack traces for computationally embedded languages

There is a fundamental problem with debugging for pretty much all "computationally embedded DSLs" which delay computation. Many basic DSLs suffer this problem, both in functional and object programming.

For our purposes, "Computationally embedded DSLs" are ones where:

1. The user code creates a composition of objects using helper calls from a library (e.g. `async { ... }`)
2. The user code then executes the computation implied by that collection of objects (e.g. calls `Async.RunSynchronously`).

The process of execution may dynamically create more computation objects along the way during phase2, though that's kind of irrelevant.

A very, very simple example of such a "computationally embedded language" is below. This DSL has lousy debugging in F#:

```fsharp
module DSL = 
    type Computation<'T> = C of (unit -> 'T)

    let bind (C f) k = C(fun () -> let (C f2) = k (f ()) in f2())

    let ret x = C (fun () -> x)

    let add c1 c2 = bind c1 (fun v1 -> bind c2 (fun v2 -> ret (v1 + v2)))

    let run (C f) = f ()
```

which lets you write things like:

```fsharp
open DSL

let f1() = ret 6
let f2() = ret 7
let f3() = add (f1()) (f2())

f3() |> run
```

Now the "stack" when the `ret 6` computation of `f1()` is actually "invoked" (i.e. when it gets invoked as part of `f3() |> run`) is bad. You can see the bad stack by augmenting the example as follows:

```fsharp
type Computation<'T> = C of (unit -> 'T)

let delay f = bind (ret ()) f
let stack msg c1 = 
    delay (fun () -> 
        System.Console.WriteLine("----{0}-----\n{1}\n----------", msg, System.Diagnostics.StackTrace(true).ToString())
        c1)
```

Here is the stack:

```
----f1-----
   at A.stack@12.Invoke(Unit unitVar0) in C:\GitHub\dsyme\visualfsharp\a.fs:line 12
   at A.bind@5.Invoke(Unit unitVar0) in C:\GitHub\dsyme\visualfsharp\a.fs:line 5
   at A.bind@5.Invoke(Unit unitVar0) in C:\GitHub\dsyme\visualfsharp\a.fs:line 5
   at A.run[a](Computation`1 _arg1) in C:\GitHub\dsyme\visualfsharp\a.fs:line 13
   at A.main(String[] argv) in C:\GitHub\dsyme\visualfsharp\a.fs:line 22
----------
```

Barely any user code is on the stack at all, and the line numbers are all closures in the implementation of the DSL.

It is also important to note that C# async and other implementations of Promises/Tasks/"Hot Tasks" don't suffer this problem since they are not "delayef". But most F# DSLs are "delayed". For `async { }`, it is F#'s use of "cold tasks" that require explicit starting that causes the issue.

## Problem Two - losing tack traces for exceptions

There is a second fundamental problem with .NET stack traces for any computationally embedded DSL that may throw .NET exceptions. This is based on [this problem](https://stackoverflow.com/questions/5301535/exception-call-stack-truncated-without-any-re-throwing), where only a limited number of stack frames are included in the `.StackTrace` property of a .NET exception when it is thrown, up to the first catch handler.

This is a serious and surprising limitation in the CLR. For example it’s a very common pattern in functional programming to quickly turn exceptions into data. For example, consider basic patterns like the [Railway Oriented programming](https://fsharpforfunandprofit.com/rop/) pattern. This might have some code like this:

```fsharp
/// A type very similar to this is in the FSharp.Core library
type Result<'T> = 
   | Ok of 'T 
   | Error of System.Exception

let embed f = 
    try Ok(f()) 
    with exn -> Error exn

let run f = f()

run (embed (fun () -> failwith "fail"))
```

Here the `StackTrace` data for exceptions carried by the `Error` data will only carry a partial stack trace containing one stack frame. See the link above for why. The CLR only adds stack frames up to the first `try ... with` handler.

There are C# equivalents to such patterns, especially with C# becoming more expression-oriented. They suffer the same problem.

# Detailed design

Possible solutions to these problems:

## Three possible solutions to Problem One

1. Keep causal stacks in objects and reinstate them when the behavior of the objects is activated.

    * This could be done if there was a way to do some kind of `[<CaptureCallerStackToken>]` and pass it down. This can be simulated by [caputring a `System.Diagnostics.StackTrace`](https://gist.github.com/dsyme/fb5c70ce6b16ac3047b8ceae057ccccb) for each and every computational object created and passing them down.

    * This could give very nice stack traces, but you would need to be able to hijack the normal debugging mechanisms and say, "hey, this is the real causal stack". This can be done for **exception** stack traces by hacking the internals of a .NET exception object (see [this blog post](https://eiriktsarpalis.wordpress.com/2015/12/27/reconciling-stacktraces-with-computation-expressions/)).

    * This is expensive. We cannot normally afford to keep stacks from the creation of objects in the actual object closures.

    * In some sense, this is what C# async debugging is doing. However, we don't fully understand how they accomplish this and will need to reach out to them to understand more.

    * It is also roughly what is done for rethrow exceptions using `ExceptionDispatchInfo`. However, this may not be able to be done for stack traces at breakpoints.

2. Pass source/line number information into the process of constructing the computation objects. This was done by Eirik Tsarpalism [here](https://eiriktsarpalis.wordpress.com/2015/12/27/reconciling-stacktraces-with-computation-expressions/) in order to get better exception stack traces. However, this feels expensive - or at least not cost-free - and also requires changing API surface area. Also, it is only helpful for stack traces produced for exceptions.

3. The third option is to "inline" the key parts of the computational language (and its composition operations) so that the key closures which implement the composition become user code. This relies on the fact that when the F# compiler inlines code, it re-tags closures contained in that code with the source information of the call site.

Here is an example of how to play this game with the very single computational language above:

```fsharp
type Computation<'T> = C of (unit -> 'T)

[<System.Diagnostics.DebuggerHidden>]
let apply (C f) = f()
let inline bind f k = C(fun () -> let f2 = k (apply f) in apply f2)
let inline ret x = C (fun () -> x)
let inline add c1 c2 = bind c1 (fun v1 -> bind c2 (fun v2 -> ret (v1 + v2)))
let inline stack msg c1 = bind (ret ()) (fun () -> System.Console.WriteLine("----{0}-----\n{1}\n----------", msg, System.Diagnostics.StackTrace(true).ToString()); c1)
let run c = apply c
```

The rules applied are:

1. Inline the mechanisms of composition.
2. Expose any necessary "runtime" functions such as `apply` and mark them `DebuggerHidden`.

```
----f1-----
   at A.f1@16-3.Invoke(Unit unitVar0) in <your-user-code-path>\a.fs:line 16
   at A.f3@18-4.Invoke(Unit unitVar0) in <your-user-code-path>\a.fs:line 18
   at A.main(String[] argv) in <your-user-code-path>\a.fs:line 23
```

This is not perfect. We see the generated closure names. But at least the line numbers are right and names are `A.f1` and `A.f3` appear. This is a significant improvement.

## Possible solutions to Problem Two

One way to improve the exception stacks for losing stack traces is to use a "trampoline" to run the asynchronous parts of computations. When an exceltion happens, the exception continuation (or other information require to continue execution) is written into the trampoline.

This technique works for async because we have a trampoline available.

Note that the stack trace is lost when **rethrowing** and exception. Sadly, this means that it is lost when an implicit rethrow happens, such as here:

```fsharp
try
   ...
with
| :? ArgumentException as e -> ...
```

You are encouraged to do something like this pattern to keep good `InnerException` information:

```fsharp
try
   ...
with
| :? ArgumentException as e -> ...
| otherExn -> raise (AggregateException(..., otherExn))
```

## Summary of the solution for now

1. The Bind composition operation in `AsyncBuilder` is marked `inline`, as are a few other methods.
2. An extra level of underlying async execution machinery is exposed in the API of FSharp.Core. This surface area is marked as **Compiler use only**, but becomes part of the long-term binary compatible API for FSharp.Core. This allows key closures to be inlined into user code while preventing a full re-implementation of how Async is implemented.

This solution has the following caveats:

1. This only improves debug user code that is compiled with tailcalls off.
2. This improves the debugging experience for **first throw of exceptions** and the **synchronous** parts of asynchrounous code.
3. The stack is still lost if:

    * The "trampoline" has been used, which happens every 300 executions of a bind on a stack of when any exception is raised (even if it is caught).
    * An exception is rethrown.

# Drawbacks
[drawbacks]: #drawbacks

Although this can dramatically improve the diagnostics of F# async code, it is not a blanket solution to all problems here. You can still lose stack traces, and this will not be usable with code in release mode with tailcalls turned on. This also introduces types into the API surface area of FSharp.Core that people could mistakenly use (even though they are marked in a way to discourage their use).

# Alternatives
[alternatives]: #alternatives

Do nothing.

# Compatibility
[compatibility]: #compatibility

This is binary and source compatible.

# Unresolved questions
[unresolved]: #unresolved-questions

None.
