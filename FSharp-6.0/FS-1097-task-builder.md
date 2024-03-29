# F# RFC FS-1097 - Task builder

The design suggestion [Native support for task { ... } ](https://github.com/fsharp/fslang-suggestions/issues/581) has been marked "approved in principle". This RFC covers the detailed proposal for this suggestion.

- [x] Approved in principle
- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/581)
- [x] [RFC Discussion](https://github.com/fsharp/fslang-design/discussions/574)
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/6811)

# Summary

We add a `task { .. }` builder to the F# standard library, implemented using [FS-1087 resumable code](https://github.com/fsharp/fslang-design/blob/master/FSharp-6.0/FS-1087-resumable-code.md).

The design is heavily influenced by [TaskBuilder.fs](https://github.com/rspeele/TaskBuilder.fs/) and [Ply](https://github.com/crowded/ply) which effectively
formed part of prototypes for this RFC (thank you!!!!)

# Motivation

`task { ... }` support is needed in F# with good quality, low-allocation generated code.

# Detailed design

We add support for tasks along the lines of `TaskBuilder.fs`.  This supports

* The standard computation expression features for imperative code (`Delay`, `Run`, `While`, `For`, `TryWith`, `TryFinally`, `Return`)

* `let!` and `return!` on task values

* `let!` and `return!` on async values

* `let!` and `return!` "task-like" values (supporting `task.GetAwaiter`, `awaiter.IsCompleted` and `awaiter.GetResult` members)

* `use` on both `IDisposable` and `IAsyncDisposable` resources

* `backgroundTask { ... }` to escape the UI thread synchronization context

For example, a simple task:
```fsharp
        task {
            return 1
        }
```
Binding to `Task<_>` values:
```fsharp
    task {
        let! x = Task.FromResult(1)
        return 1 + x
    }
```
Binding to (non-generic) `Task` values:
```fsharp
    task {
        let! x = Task.Delay(1)
        return 1 + x
    }
```
Binding to (task-like) `YieldAwaitable` values:
```fsharp
    task {
        let! x = Task.Yield()
        return 1 + x
    }
```
Binding to (non-generic) F# `Async` values:
```fsharp
    task {
        do! Async.Sleep(1)
        return 1
    }
```
Nested tasks:
```fsharp
    task {
        let! x = task { return 1 }
        return x
    }
```
Try/with in tasks:
```fsharp
    task {
        try 
           return 1
        with e -> 
           return 2
    }
```
Tasks are executed immediately to their first await point. For example:
```fsharp
    let mutable x = 0
    let t =
        task {
            x <- x + 1
            do! Task.Delay(50000)
            x <- x + 1
        }
    printfn "x = %d" x // prints "x = 1"
```
Background tasks are specified using `backgroundTask { ... }`.  
```fsharp
backgroundTask { return doSomethingLong() }
```

These ignore any SynchronizationContext - if
they are started on a thread with non-null `SynchronizationContext.Current`, they switch to a background thread
in the thread pool using `Task.Run`.  

### Binding to Task-like values

Binding to task-like values like `YieldAwaitable` is via SRTP constraints characterising the pattern:

```fsharp
  member Bind...
     when  ^TaskLike: (member GetAwaiter:  unit ->  ^Awaiter)
     and ^Awaiter :> ICriticalNotifyCompletion
     and ^Awaiter: (member get_IsCompleted:  unit -> bool)
     and ^Awaiter: (member GetResult:  unit ->  ^TResult1) 
```

e.g.

Binding to (task-like) `YieldAwaitable` values:
```fsharp
    task {
        let! x = Task.Yield()
        return 1 + x
    }
```

See the library entries below.

### ValueTask and other bindable values

For `task { ... }`, the constructs  `let!` and `return!` can bind to any `Task` or `ValueTask` or `Async` corresponding
to the appropriate design pattern. See the SRTP constraints for `Bind` and `ReturnFrom` in the design below.

There is no direct way to produce a `ValueTask` using the `task { ... }` builder.  

### Producing non-generic Task values

In this RFC there is no specific support for a `unitTask { ... }`, instead you should use `task { ... } :> Task` or define `Task.Ignore`:

```fsharp
module Task =
    let Ignore(t: Task<unit>) = (t :> Task)
```

### Producing ValueTask values

In this RFC there is no specific support for producing `ValueTask<_>` or `ValueTask` struct values.  Instead
use

    task { ... } |> ValueTask<_>
    
which produces a `ValueTask<_>` of the right type.  This incurs an allocation. 


A `vtask { ... }` is definable as a user-library using resumable code,  replicating all of tasks.fs with a different state machine type  

> NOTE: It would in theory have been possible to engineer `tasks.fs` using ValueTask production at the core.  
> from the outside, but this uses AsyncValueTaskMethodBuilder, which is only in netstandard2.1,
> and this would mean no task support on .NET Framework.  

### IAsyncDisposable

If `netstandard2.1` FSharp.Core.dll or higher is referenced, then the following method is available directly on the TaskBuilder type:

```fsharp
type TaskBuilderBase =
     ...
     member inline Using<'Resource, 'TOverall, 'T when 'Resource :> IAsyncDisposable> : resource: 'Resource * body: ('Resource -> TaskCode<'TOverall, 'T>) -> TaskCode<'TOverall, 'T>
     ...
```

A second method is provided for IDisposable as an extension method, which acts as a low-priority method overload. This means the IAsyncDisposable
overload is preferred over the IDisposable overload for types that support both interfaces.
```fsharp
module TaskHelpers = 
    type TaskBuilderBase with
        /// Low-priority method overload for 'Disposable', the 'IAsyncDisposable' is preferred if it is a feasible candidate
        member inline Using: resource: 'Resource * body: ('Resource -> TaskCode<'TOverall, 'T>) -> TaskCode<'TOverall, 'T> when 'Resource :> IDisposable
```

This allows `use` and `use!` to bind to `IAsyncDisposable` if present, otherwise `IDisposable`, e.g. 

```fsharp
let f () =
    let mutable disposed = 0
    let t = 
        task {
            use d = 
                { new IAsyncDisposable with 
                    member __.DisposeAsync() = 
                        task { 
                            disposed <- disposed + 1 
                            printfn $"in disposal, disposed = {disposed}"
                            do! Task.Delay(10)
                            disposed <- disposed + 1 
                            printfn $"after disposal, disposed = {disposed}"
                        }
                        |> ValueTask 
                }
            printfn $"in using, disposed = {disposed}"
            do! Task.Delay(10)
         }

    printfn $"outside using, disposed = {disposed}"
    t.Wait()
    printfn $"after full disposal, disposed = {disposed}"

f()
```
outputs:
```
in using, disposed = 0
outside using, disposed = 0
in disposal, disposed = 1
after disposal, disposed = 2
after full disposal, disposed = 2
```

### Background tasks and synchronization context

By default, tasks written using `task { .. }` are, like F# async, "context aware" and schedule continuations to the `SynchronizationContext.Current`
if present.  This allows tasks to serve as cooperatively scheduled interleaved agents executing on a user interface thread.

In practice, it is often intended that tasks be "free threaded" in the .NET thread pool, including their initial execution.
This can be done using `backgroundTask { ... }`:

```fsharp
backgroundTask {
       ...
}    
```

A `backgroundTask { ... }` ignores any SynchronizationContext.Current in the following sense: if
on a thread with non-null `SynchronizationContext.Current`, it switches to a background thread
in the thread pool using `Task.Run`.  If started on a thread with null `SynchronizationContext.Current`, it executes
on that same thread.

> NOTE: This means in practice that calls to [`ConfigureAwait(false)`](https://devblogs.microsoft.com/dotnet/configureawait-faq/)
> are not typically needed in F# task code. Instead,
> tasks that are intended to run in the background should use the `backgroundTask { ... }` computation expression. An outer
> `task { .. }` binding to a `backgroundTask { .. }` will resynchronize to the `SynchronizationContext.Current` on
> completion of the background task.
>
> See [this discussion](https://github.com/rspeele/TaskBuilder.fs/issues/35#issuecomment-848077410) for more detail.

## Feature: Respecting Zero methods in computation expressions 

Prior to this RFC, the `do! expr` construct in final position of a computation branch
in an F# computation expressions was processed as follows:

1. If a `Return` method is present, process as if `builder.Bind(expr, fun () -> builder.Return ())`

2. Otherwise, process as `builder.Bind(expr, fun () -> builder.Zero ())`

The new rule has an extra condition as follows:

1. If a `Return` method is present and there is no `Zero` method present with `DefaultValue` attribute, process as if `builder.Bind(expr, fun () -> builder.Return ())`

2. Otherwise, process as `builder.Bind(expr, fun () -> builder.Zero ())`

For example,
```fsharp
task {
    if true then 
        do! Task.Delay(100)
    return 4
}
```

is de-sugared to

```fsharp
task.Combine(
    (if true then task.Bind(Task.Delay(100), fun () -> task.Zero()) else task.Zero()),
    task.Delay(fun () -> task.Return(4)))`.
```

rather than

```fsharp
task.Combine(
    (if true then task.Bind(Task.Delay(100), fun () -> task.Return()) else task.Zero()),
    task.Delay(fun () -> task.Return(4)))`.
```

Motivation: This corrects a minor problem with F# computation expressions.
This allows builders ensure `Return` is not implicitly required by `do!` expressions in final position by adding the `DefaultValue`
attribute to the `Zero` method on the builder type.  This brings the treatment of `do!` in line with the treatment of
the implicit result on an implicit `else` branch in `if .. then` constructs, and allows `Return` to have a more
restrictive signature than `Zero`.  In particular in the task builder, we have

```fsharp
type TaskBuilder =
    member inline Return: x: 'T -> TaskCode<'T, 'T>

    [<DefaultValue>]
    member inline Zero: unit -> TaskCode<'TOverall, unit>
```

Here implicit `Zero` values (implied by `do!` and `if .. then ..`) can now occur anywhere in a computation expression, regardless
of the overall type of the task being returned by  the CE.  In contrast, `Return` values (in an explicit `return`) must
match the type returned by the overall task.

To put this another way, the difference is that a `Zero()` is produced instead of a `Return(())` on the empty branch
The `Return` stores the result in the state machine and pins down the overall type produced by the state machine `'TOverall = 'T`. `Zero` just continues successfully.

```fsharp
        /// Used to represent no-ops like the implicit empty "else" branch of an "if" expression.
        [<DefaultValue>]
        member inline _.Zero() : TaskCode<'TOverall, unit> =
            ResumableCode.Zero()

        member inline _.Return (value: 'T) : TaskCode<'T, 'T> = 
            TaskCode<'T, _>(fun sm -> 
                sm.Data.Result <- value
                true)
```

## Feature: NoEagerConstraintApplicationAttribute

Adding this attribute to the method adjusts the processing of some generic methods
during overload resolution.

During overload resolution, caller arguments are matched with called arguments
to extract type information. By default, when the caller argument type is unconstrained (for example
a simple value `x` without known type information), and a method qualifies for
lambda constraint propagation, then member trait constraints from a method overload
are eagerly applied to the caller argument type. This causes that overload to be preferred,
regardless of other method overload resolution rules. Using this attribute suppresses this behaviour. 

For example, consider the following overloads:
```
type OverloadsWithSrtp() =
    [<NoEagerConstraintApplicationAttribute>]
    static member inline SomeMethod< ^T when ^T : (member Number: int)> (x: ^T, f: ^T -> int) = 1
    static member SomeMethod(x: 'T list, f: 'T list -> int) = 2

let inline f x = 
    OverloadsWithSrtp.SomeMethod (x, (fun a -> 1)) 
```

With the attribute, the overload resolution fails, because both members are applicable.
Without the attribute, the overload resolution succeeds, because the member constraint is
eagerly applied, making the second member non-applicable.  

This attribute is not expected to be used by anyone except the most advanced F# programmers and is really designed to correct what was effectively inadvertent behaviour that has since proved useful to some users, and thus allow for the design of overload sets with more normal properties.

As background to why this is necessary, the intention of the RFC is that the `Bind` and `returnFrom` overloads are used in the above priority order - that is, if no type information is available then `Task<'T>` is assumed. The actual RFC uses extension methods as the basis to ensure this priority order. With this design, overloads are resolved eagerly, and the highest priority one will be chosen based on available type annotations, so if you don't specify a type, `Task<_>` will be assumed.  For example

```fsharp
let TaskMethod t =
    task {
        let! result = t
        return result
    }
```
will compile and infer type
```fsharp
val TaskMethod: t: Task<'a> -> Task<'a>
```
Here `let!` uses overload resolution on `task.Bind`.  This has multiple overloads:
```
member Bind: Task<'T> * ('T -> TaskCode<...>)
member Bind: Async<'T> * ('T -> TaskCode<...>)
member Bind: ^TaskLike * ('T -> TaskCode<...>) when ... 
```

This differs from TaskBuilder.fs but is the intended design (among other things it prevents SRTP constraints floating everywhere throughout resolution, with their accompanying difficult error messages, and prevents default solutions for SRTP constraints causing strange errors).

However, the priority order was not applying.  The reason is subtle: a rule in overload resolution called "eager constraint application" meant the SRTP overload is being given absolute priority in the absence of other type information - the details are explained below.  This is established behaviour and even useful in some situations, and thus can't be changed universally (that is, some existing code will depend on it). That is, one of the phases of F# overload resolution is to propagate "known type information" into lambda expression arguments, e.g. for the overloads:

```fsharp
    type Overloads() =
        static member SomeMethod(x: 'T array, f: 'T -> int, n: int) = ()
        static member SomeMethod(x: 'T array, f: 'T -> int, s: string) = ()

    Overloads.SomeMethod([| "a" |], (fun a -> a.Length), 1)
    Overloads.SomeMethod([| "a" |], (fun a -> a.Length), "a")
```

Here the type `string` is applied to `'T` based on processing the first argument, then, prior to processing the second argument, the type of "a" is known to be string because both overloads have a lambda of the same shape in the same position.  This process is called "LambdaPropagationInfo" and uses the constraint solver in "MatchingOnly" mode which works out values for type variables in the overload matrix, without affecting the types on the callsite (we can't affect these as the overload has not been committed yet).

Now, when some overloads use SRTP the "MatchingOnly" mode was incorrectly pushing an SRTP constraint into the types on the callsite.  For example
```fsharp
    type OverloadsWithSrtp() =
        static member inline SomeMethod< ^T when ^T : (member Foo: int) > (x: ^T, f: ^T -> int) = 1
        static member inline SomeMethod(x: 'T list, f: 'T list -> int) = 2

    let inline f x = 
        OverloadsWithSrtp.SomeMethod (x, (fun a -> 1)) 
```
Here the call contains no specific information, and there is no grounds to prefer the SRTP overload. However, because one of the methods is an SRTP method, and the attribute is not present, the constraint is eagerly applied to the argument type for "x".  This means that the second method becomes non-applicable, and thus the overload does resolve.  This happens regardless of other overload resolution rules.


## Library additions 

We show the library additions in segments.  

### TaskCode, TaskBuilderBase, TaskBuilder

The basic contents of these are shown below:

```fsharp
type TaskBuilderBase =
    member inline Combine: TaskCode<'TOverall, unit> * TaskCode<'TOverall, 'T> -> TaskCode<'TOverall, 'T>
    member inline Delay: (unit -> TaskCode<'TOverall, 'T>) -> TaskCode<'TOverall, 'T>
    member inline For: seq<'T> * ('T -> TaskCode<'TOverall, unit>) -> TaskCode<'TOverall, unit>
    member inline Return: 'T -> TaskCode<'T, 'T>
    member inline TryFinally: TaskCode<'TOverall, 'T> * (unit -> unit) -> TaskCode<'TOverall, 'T>
    member inline TryWith: TaskCode<'TOverall, 'T> * (exn -> TaskCode<'TOverall, 'T>) -> TaskCode<'TOverall, 'T>
    member inline Using<'Resource, 'TOverall, 'T when 'Resource :> IAsyncDisposable> : resource: 'Resource * body: ('Resource -> TaskCode<'TOverall, 'T>) -> TaskCode<'TOverall,     member inline While: (unit -> bool) * TaskCode<'TOverall, unit> -> TaskCode<'TOverall, unit>
    member inline Zero: unit -> TaskCode<'TOverall, unit>

type TaskBuilder =
    inherit TaskBuilderBase
    member inline Run: code: TaskCode<'T, 'T> -> Task<'T>

type TaskCode<'TOverall, 'T> = ... // see further below

[<AutoOpen>]
module TaskBuilder = 
    val task: TaskBuilder
```

### `Using` support

The following are added to support `Using` on Tasks and task-like patterns:

```fsharp
namespace Microsoft.FSharp.Control

type TaskBuilderBase =
    ...
    /// High-priority method overload for 'IAsyncDisposable', preferred if it is a feasible candidate
    member inline Using: resource: 'Resource * body: ('Resource -> TaskCode<'TOverall, 'T>) -> TaskCode<'TOverall, , 'T> when 'Resource :> IAsyncDisposable

[<AutoOpen>]
namespace Microsoft.FSharp.Control.TaskBuilderExtensions

    [<AutoOpen>]
    module LowPriority = 
    
        /// Low-priority method overload for 'IDisposable', the 'IAsyncDisposable' is preferred if it is a feasible candidate
        member inline Using: resource: 'Resource * body: ('Resource -> TaskCode<'TOverall, 'T>) -> TaskCode<'TOverall, 'T> when 'Resource :> IDisposable
```

### `BackgroundTaskBuilder` support

```fsharp
type BackgroundTaskBuilder =
    inherit TaskBuilderBase
    member inline Run: code: TaskCode<'T, 'T> -> Task<'T>

[<AutoOpen>]
module TaskBuilder = 
    val backgroundTask: BackgroundTaskBuilder
```

### Bind, ReturnFrom support

The following are added to support `Bind` and `ReturnFrom` on Tasks and task-like patterns:

```fsharp
namespace Microsoft.FSharp.Control.TaskBuilderExtensions

    /// Contains low-priority overloads for the `task` computation expression builder.
    [<AutoOpen>]
    module LowPriority = 

        type TaskBuilderBase with 
            /// Specifies a unit of task code which draws a result from a task-like value
            /// satisfying the GetAwaiter pattern and calls a continuation.
            [<NoEagerConstraintApplicationAttribute>]
            member inline Bind< ^TaskLike, ^TResult1, 'TResult2, ^Awaiter, 'TOverall > :
                task: ^TaskLike * continuation: ( ^TResult1 -> TaskCode<'TOverall, 'TResult2>) -> TaskCode<'TOverall, 'TResult2>
                    when  ^TaskLike: (member GetAwaiter:  unit ->  ^Awaiter)
                    and ^Awaiter :> ICriticalNotifyCompletion
                    and ^Awaiter: (member get_IsCompleted:  unit -> bool)
                    and ^Awaiter: (member GetResult:  unit ->  ^TResult1) 

            /// Specifies a unit of task code which draws its result from a task-like value
            /// satisfying the GetAwaiter pattern.
            [<NoEagerConstraintApplicationAttribute>]
            member inline ReturnFrom< ^TaskLike, ^Awaiter, ^T> : task: ^TaskLike -> TaskCode< ^T, ^T > 
                    when  ^TaskLike: (member GetAwaiter:  unit ->  ^Awaiter)
                    and ^Awaiter :> ICriticalNotifyCompletion
                    and ^Awaiter: (member get_IsCompleted: unit -> bool)
                    and ^Awaiter: (member GetResult: unit ->  ^T)

            /// Specifies a unit of task code which binds to the resource implementing IDisposable and disposes it synchronously
            member inline Using: resource:
                'Resource * body: ('Resource -> TaskCode<'TOverall, 'T>) -> TaskCode<'TOverall, 'T> when 'Resource :> IDisposable

    /// Provides evidence that various types can be used in bind and return constructs in task computation expressions
    [<AutoOpen>]
    module MediumPriority =

        type TaskBuilderBase with 
            /// Specifies a unit of task code which draws a result from an F# async value then calls a continuation.
            member inline Bind: computation: Async<'TResult1> * continuation: ('TResult1 -> TaskCode<'TOverall, 'TResult2>) -> TaskCode<'TOverall, 'TResult2>

            /// Specifies a unit of task code which draws a result from an F# async value.
            member inline ReturnFrom: computation: Async<'T> -> TaskCode<'T, 'T>

    /// Provides evidence that various types can be used in bind and return constructs in task computation expressions
    [<AutoOpen>]
    module HighPriority =

        type TaskBuilderBase with 
            /// Specifies a unit of task code which draws a result from a task then calls a continuation.
            member inline Bind: task: Task<'TResult1> * continuation: ('TResult1 -> TaskCode<'TOverall, 'TResult2>) -> TaskCode<'TOverall, 'TResult2>

            /// Specifies a unit of task code which draws a result from a task.
            member inline ReturnFrom: task: Task<'T> -> TaskCode<'T, 'T>

```
The use of explicit low/medium/high priority extension member modules for `Bind` and `ReturnFrom` ensure that `Task` binding is preferred, for these reasons

1. Task satisfies the Task-like pattern, thus avoiding ambiguities

2. Code such as `let f x = task { let! v = x in return v + v }` compiles, assuming Task

3. Code such as `let f x = task { ... return! failwith "nope" }` compiles, assuming Task

The use of explicit low/medium/high priority extension member modules for `Using` ensures the `IAsyncDisposable` binding is preferred.

Although marked `AutoOpen` above, assembly attributes are actually used to ensure these modules are auto-opened in the right order and
that they count as independent sets of methods for the purposes of extension member priority.

```fsharp
    [<assembly: AutoOpen("Microsoft.FSharp.Control.TaskBuilderExtensions.LowPriority")>]
    [<assembly: AutoOpen("Microsoft.FSharp.Control.TaskBuilderExtensions.MediumPriority")>]
    [<assembly: AutoOpen("Microsoft.FSharp.Control.TaskBuilderExtensions.HighPriority")>]
```


### Library additions for task state machines

The following are necessarily revealed in the public surface area because they are used within inlined code implementations
of the `TaskBuilder` methods.  They are not for general use.
```fsharp
/// A special compiler-recognised delegate type for specifying blocks of task code with access to the state machine.
type TaskCode<'TOverall, 'T> = ResumableCode<TaskStateMachineData<'TOverall>, 'T>

[<Struct>]
[<CompilerMessage("This construct  is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
/// The extra data stored in ResumableStateMachine for tasks
type TaskStateMachineData<'T> =
    /// Holds the final result of the state machine
    val mutable Result : 'T

    /// Holds the MethodBuilder for the state machine
    val mutable MethodBuilder : AsyncTaskMethodBuilder<'T>

/// This is used by the compiler as a template for creating state machine structs
type TaskStateMachine<'TOverall> = ResumableStateMachine<TaskStateMachineData<'TOverall>>

```

### Library additions (inline residue for dynamic/non-compilable task specification)

The following are necessarily revealed in the public surface area of FSharp.Core to support reflective execution of quotations that create tasks, as part of the inline residue of the corresponding inlined builder methods:
```fsharp
type TaskBuilderBase =
    static member RunDynamic: code: TaskCode<'T, 'T> -> Task<'T>
    static member CombineDynamic: task1: TaskCode<'TOverall, unit> * task2: TaskCode<'TOverall, 'T> -> TaskCode<'TOverall, 'T>
    static member WhileDynamic: condition: (unit -> bool) * body: TaskCode<'TOverall, unit> -> TaskCode<'TOverall, unit>
    static member TryFinallyDynamic: body: TaskCode<'TOverall, 'T> * fin: (unit -> unit) -> TaskCode<'TOverall, 'T>
    static member TryWithDynamic: body: TaskCode<'TOverall, 'T> * catch: (exn -> TaskCode<'TOverall, 'T>) -> TaskCode<'TOverall, 'T>
    static member ReturnFromDynamic: task: Task<'T> -> TaskCode<'T, 'T>
```

# Performance

[Recent perf status of implementation](https://github.com/dotnet/fsharp/blob/feature/tasks/BenchmarkDotNet.Artifacts/results/TaskPerf.Benchmarks-report-github.md)

## Expected allocation profile for task { ... }

The allocation performance of the current approach should be:

* one allocation of Task per task { ... }

* the autobox transformation when `let mutable` is used in a task


# Limitations

## Limitation - Hot start, once

Unlike F# async, tasks start immediately ("hot start") and each task may only complete once.

## Limitation - No implicit cancellation token passing

Unlike F# async, tasks do *not* support implicit passing of cancellation tokens. You must pass and/or capture the cancellation token explicitly.

## Limitation - No asynchronous tailcalls

Unlike F# async, tasks do *not* support asynchronous tail recursion, thus unbounded chains of tasks can be created
consuming unbounded stack and heap resources. Thus the following will work for `N = 100` but not for very large `N`.
```fsharp
    let N = 100
    let rec loop n =
        task {
            if n < N then
                do! Task.Yield()
                let! _ = Task.FromResult(0)
                return! loop (n + 1)
            else
                return ()
        }
    (loop 0).Wait()
```
Aside: See [this paper](https://www.microsoft.com/en-us/research/publication/the-f-asynchronous-programming-model/) for more
information on asynchronous tailcalls in the F# async programming model.


# Drawbacks

Complexity

Lack of tailcalls.


# Alternatives

1. Don't do it, keep using TaskBuilder.fs

2. Build it all into the compiler. Ugh

# Compatibility

This is a backward compatible addition.

# Resolved questions

* [x] IAsyncDisposable. Resolution: support it

* [x] ContextInsensitiveTasks. Resolution: Resolved in favour of `backgroundTask { ... }`

* [x]  warnings for the lack of asynchronous tailcalls in `task { ... }`? Resolution: we won't do this in this RFC

# Unresolved questions

* [ ] There's a proposal to rename `backgroundTask { ... }` to `threadPoolTask { .. }` or something similar.   See https://github.com/rspeele/TaskBuilder.fs/issues/35#issuecomment-848077410 and below

* [ ] Should 'For' also accept `IAsyncEnumerable`?  Is it feasible to add this post-hoc?





