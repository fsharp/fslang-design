# F# RFC FS-1097 - Tasks 

The design suggestion [Native support for task { ... } ](https://github.com/fsharp/fslang-suggestions/issues/581) has been marked "approved in principle". This RFC covers the detailed proposal for this suggestion.

- [x] Approved in principle
- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/581)
- [x] [RFC Discussion](https://github.com/fsharp/fslang-design/issues/455)
- [x] [Prototype](https://github.com/dotnet/fsharp/pull/6811)

# Summary

We add a `task { .. }` builder to the F# standard library, implemented using [resumable code](https://github.com/fsharp/fslang-design/blob/master/RFCs/FS-1087-resumable-code.md).

# Motivation

`task { ... }` support is needed in F# with good quality, low-allocation generated code.

# Detailed design

We add support for tasks along the lines of `TaskBuilder.fs`.  This supports

* The standard computation expresion features for imperative code (`Delay`, `Run`, `While`, `For`, `TryWith`, `TryFinally`, `Return`, `Using`)

* `let!` and `return!` on task values

* `let!` and `return!` on async values

* `let!` and `return!` "task-like" values (supporting `task.GetAwaiter`, `awaiter.IsCompleted` and `awaiter.GetResult` members).

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

### ValueTask and other bindable values

For `task { ... }`, the constructs  `let!` and `return!` can bind to any `Task` or `ValueTask` or `Async` corresponding
to the appropriate design pattern. See the SRTP constraints for `Bind` and `ReturnFrom` in the design below.

There is no direct way to produce a `ValueTask` using the `task { ... }` builder.  


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


## Library additions 

The following are added to FSharp.Core:
```fsharp
namespace Microsoft.FSharp.Control

type TaskBuilder =
    member Combine: TaskCode<'TOverall, unit> * TaskCode<'TOverall, 'T> -> TaskCode<'TOverall, 'T>
    member Delay: (unit -> TaskCode<'TOverall, 'T>) -> TaskCode<'TOverall, 'T>
    member For: seq<'T> * ('T -> TaskCode<'TOverall, unit>) -> TaskCode<'TOverall, unit>
    member Return: 'T -> TaskCode<'T, 'T>
    member Run: TaskCode<'T, 'T> -> Task<'T>
    member TryFinally: TaskCode<'TOverall, 'T> * (unit -> unit) -> TaskCode<'TOverall, 'T>
    member TryWith: TaskCode<'TOverall, 'T> * (exn -> TaskCode<'TOverall, 'T>) -> TaskCode<'TOverall, 'T>
    member Using: 'Resource * ('Resource -> TaskCode<'TOverall, 'T>) -> TaskCode<'TOverall, 'T> when 'Resource :> IDisposable
    member While: (unit -> bool) * TaskCode<'TOverall, unit> -> TaskCode<'TOverall, unit>
    member Zero: unit -> TaskCode<'TOverall, unit>
    member ReturnFrom: Task<'T> -> TaskCode<'T, 'T>

/// Used to specify fragments of task code
type TaskCode<'TOverall, 'T> 

[<AutoOpen>]
module TaskBuilder = 
    val task : TaskBuilder
```

The following are added to support `Bind` and `ReturnFrom` on Tasks and task-like patterns
```fsharp
namespace Microsoft.FSharp.Control

[<AutoOpen>]
module ContextSensitiveTasks =
    type TaskWitnesses = <TBD>

    [<AutoOpen>]
    module TaskHelpers = 

        type TaskBuilder with 
            member Bind: ^TaskLike * (^TResult1 -> TaskCode<'TOverall, 'TResult2>) -> TaskCode<'TOverall, 'TResult2> (+ SRTP constaint for Bind)

            member ReturnFrom: ^TaskLike -> TaskCode< 'T, 'T > (+ SRTP constaint for CanReturnFrom)
```

See the implementation source code for the exact specification of the SRTP constraints added.

### Library additions (inlined code residue)

The following are necessarily revealed in the public surface area because they are used within inlined code implementations
of the `TaskBuilder` methods.  They are not for general use.
```fsharp
type TaskCode<'TOverall, 'T> = delegate of byref<TaskStateMachine<'TOverall>> -> bool 

/// This is used by the compiler as a template for creating state machine structs
[<Struct>]
type TaskStateMachine<'T> =

    /// Holds the final result of the state machine
    val mutable Result : 'T

    /// When statically compiled, holds the continuation goto-label further execution of the state machine
    val mutable ResumptionPoint : int

    val mutable MethodBuilder : AsyncTaskMethodBuilder<'T>

    interface IAsyncStateMachine

```

### Library additions (inline residue for reflective execution of task creation)

The following are necessarily revealed in the public surface area of FSharp.Core to support reflective execution of quotations that create tasks, as part of the inline residue of the corresponding inlined builder methods:
```fsharp
type TaskBuilder =
    static member RunDynamic: code: TaskCode<'T, 'T> -> Task<'T>
    static member CombineDynamic: task1: TaskCode<'TOverall, unit> * task2: TaskCode<'TOverall, 'T> -> TaskCode<'TOverall, 'T>
    static member WhileDynamic: condition: (unit -> bool) * body: TaskCode<'TOverall, unit> -> TaskCode<'TOverall, unit>
    static member TryFinallyDynamic: body: TaskCode<'TOverall, 'T> * fin: (unit -> unit) -> TaskCode<'TOverall, 'T>
    static member TryWithDynamic: body: TaskCode<'TOverall, 'T> * catch: (exn -> TaskCode<'TOverall, 'T>) -> TaskCode<'TOverall, 'T>
    static member ReturnFromDynamic: task: Task<'T> -> TaskCode<'T, 'T>

[<Struct>]
type TaskStateMachine<'T> =
    ...
    /// When dynamically invoked, holds the continuation for the further execution of the state machine
    val mutable ResumptionFunc : TaskMachineFunc<'T>

    /// When dynamically invoked, holds the awaiter used to suspend of the state machine
    val mutable Awaiter : ICriticalNotifyCompletion

/// When dynamically invoked, represents a resumption for task code
type TaskMachineFunc<'TOverall> = delegate of byref<TaskStateMachine<'TOverall>> -> bool
```

### Library additions (priorities for SRTP witnesses)

This design uses a SRTPs to charactize the "Task-like" pattern.  This in turn uses the "priority specification pattern" in the
possible witnesses for SRTPs.

To enable this pattern we add three interfaces in hierarchy order to `FSharp.Core.CompilerServices`:
```fsharp
namespace FSharp.Core.CompilerServices

/// A marker interface to give priority to different available overloads.
type IPriority3 = interface end

/// A marker interface to give priority to different available overloads. Overloads using a
/// parameter of this type will be preferred to overloads with IPriority3,
/// all else being equal.
type IPriority2 = interface inherit IPriority3 end

/// A marker interface to give priority to different available overloads. Overloads using a
/// parameter of this type will be preferred to overloads with IPriority2 or IPriority3,
/// all else being equal.
type IPriority1 = interface inherit IPriority2 end
```


# Performance

[Recent perf status of implementation](https://github.com/dotnet/fsharp/blob/feature/tasks/BenchmarkDotNet.Artifacts/results/TaskPerf.Benchmarks-report-github.md)

## Expected allocation profile for task { ... }

The allocation performance of the current approach should be:

* one allocation of Task per task { ... }

* the autobox transformation when `let mutable` is used in a task


# Limitations

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

# Alternatives

1. Don't do it.

# Compatibility

This is a backward compatible addition.

# Unresolved questions

* [ ] ContextInsensitiveTasks

* [ ] consider warnings for the lack of asynchronous tailcalls in `task { ... }`


