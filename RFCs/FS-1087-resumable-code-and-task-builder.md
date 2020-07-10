# F# RFC FS-1087 - Tasks and resumable state machines

The design suggestion [Native support for task { ... } ](https://github.com/fsharp/fslang-suggestions/issues/581) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] Approved in principle
- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/581)
- [x] [RFC Discussion](https://github.com/fsharp/fslang-design/issues/455)
- [x] [Prototype](https://github.com/dotnet/fsharp/pull/6811)

# Summary

We add a `task { .. }` builder to the F# standard library.

To implement this efficiently, we add a general capability to specify and emit resumable
code hosted in state machine objects recognized by the F# compiler, and to allow F#
computation expressions to be implemented via resumable code.

# Motivation

`task { ... }` support is needed in F# with good quality, low-allocation generated code.

Further, there is a need for low-allocation implementations of other computation expressions. Some applications are asynchronous
sequences, faster list/array comprehensions and faster `option` and `result` computation expressions.

# Detailed design

## Feature: Tasks

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

## Feature: Resumable state machines

Tasks are implemented via library definitions utilising a more general feature called "resumable state machines". The implementation can be found [here](https://github.com/dotnet/fsharp/blob/feature/tasks/src/fsharp/FSharp.Core/tasks.fs).

### Design Philosophy and Principles

The design philosophy for the "resumable state machines" feature is as follows:

1. No new syntax is added to the F# language. 

2. The F# metadata format is unchanged. Like `nameof` and other F# features,
   resumable state machines are encoded within existing TypedTree constructs using a combination of known compiler intrinsics
   and TypedTree expresions.

3. We treat this as a compiler feature. The actual feature is barely surfaced
   as a language feature, but is rather a set of idioms known to the F# compiler, together used to build efficient computation
   expression implementations.

4. The feature is activated in compiled code.  An alternative implementation of the primitives can be
   given for reflective execution, e.g. for interpretation of quotation code.

5. The feature is not fully checked during type checking, but rather checks are made as code is emitted. This means
   mis-implemented resumable code may be detected late in the compilation process, potentially when compiling user code. (NOTE: we will review this)

6. The feature is designed for use only by highly skilled F# developers to implement low-allocation computation
   expression builders.

7. Semantically, there's nothing you can do with resumable state machines that you can't already do with existing
   workflows. It is better to think of them as a performance feature, a compiler optimization partly implemented
   in workflow library code.

Points 1-2 guide many of the decisions below.

Tasks are implemented via the more general mechanism of resumable state machines.

### Specifying a resumable state machine (reference types)

Resumable state machines of reference type are specified using the ``__resumableStateMachine`` compiler primitive, giving a state machine expression:
```fsharp
    if __useResumableStateMachines then
        __resumableStateMachine
            { new SomeStateMachineType() with 
                member __.Step ()  = 
                   <resumable code>
            }
    else
        <dynamic-implementation>
```
Notes

* `__useResumableStateMachines` and `__resumableStateMachine` are well-known compiler intrinsics in `FSharp.Core.CompilerServices.StateMachineHelpers`

* A value-type state machine may also be used to host the resumable code, see below. 

* Here `SomeStateMachineType` can be any user-defined reference type, and the object expression must contain a single `Step` method.

* The `if __useResumableCode then` is needed because the compilation of resumable code is only activated when code is compiled.
  The `<dynamic-implementation>` is used for reflective execution, e.g. quotation interpretation.
  In prototyping it can simply raise an exception. It should be semantically identical to the other branch.
  
* The above construct should be seen as a language feature.  First-class uses of constructs such as `__resumableObject` are not allowed except in the exact pattern above.

### Specifying resumable code

Resumable code is made of the following grammar:

* A call to an inlined function defining further resumable code, e.g. 

      <rcode> :=
          let inline f () = <rcode>
      
          f(); f() 

  Such a function can have function parameters using the `__expand_` naming, e.g. 
    ```fsharp
    let inline callTwice __expand_f = __expand_f(); __expand_f()
    let inline print() = printfn "hello"
      
    callTwice print
    ```
  These parameters give rise the expansion bindings, e.g. the above is equivalent to
    ```fsharp  
    let __expand_f = print
    __expand_f()
    __expand_f()
    ```
   Which is equivalent to
     ```fsharp   
     print()
     print()
     ```
* An expansion binding definition (normally arising from the use of an inline function):

      <rcode> :=
          let __expand_abc <args> = <expr> in <rcode>

  Any name beginning with `__expand_` can be used.
  Such an expression is treated as `<rcode>` with all uses of `__expand_abc` macro-expanded and beta-var reduced.
  Expansion binding definitions usually arise from calls to inline functions taking function parameters, see above.

* An invocation of a function known to be a lambda expression via an expansion binding:

      <rcode> :=
          __expand_abc <args>

  An invocation can also be of a delegate known to be a delegate creation expression via an expansion binding:

      <rcode> :=
          __expand_abc.Invoke <args>

  NOTE: Using delegates to form compositional code fragments is particularly useful because a delegate may take a byref parameter, normally
  the address of the enclosing value type state machine.

* A resumption point:

      <rcode> :=
          match __resumableEntry() with
          | Some contID -> <rcode>
          | None -> <rcode>

  If such an expression is executed, the first `Some` branch is taken. However a resumption point is also defined which,
  if a resumption is performed, executes the `None` branch.
  
  The `Some` branch usually suspends execution by saving `contID` into the state machine
  for later use with a `__resumeAt` execution at the entry to the method. For example:
    ```fsharp
    let inline returnFrom (task: Task<'T>) =
        let mutable awaiter = task.GetAwaiter()
        match __resumableEntry() with 
        | Some contID ->
            sm.ResumptionPoint <- contID
            sm.MethodBuilder.AwaitUnsafeOnCompleted(&awaiter, &sm)
            false
        | None ->
            sm.Result <- awaiter.GetResult()
            true
    ```
  Note that, a resumption expression can return a result - in the above the resumption expression indicates whether the
  task ran to completion or not.

* A `resumeAt` expression:

      <rcode> :=
          __resumeAt <expr>

  Here <expr> is an integer-valued expression indicating a resumption point, which must be either 0 or a `contID` arising from a resumption point resumable code expression on a previous execution.
  
* A sequential exection of two resumable code blocks:

      <rcode> :=
          <rcode>; <rcode>

  Note that, because the code is resumable, each `<rcode>` may contain zero or more resumption points.
  This means it is **not** guaranteed that the first `<rcode>` will be executed before the
  second - a `__resumeAt` call can jump straight into the second code when the method is executed to resume previous execution.

* A binding sequential exection. The identifier ``__stack_step`` must be used precisely.

      <rcode> :=
          let __stack_step = <rcode> in <rcode>

  Note that, because the code is resumable, each `<rcode>` may contain zero or more resumption points.  Again this
  means it is not guaranteed that the first `<rcode>` will be executed before the
  second - a `__resumeAt` call can jump straight into the second code when the method is executed to resume previous execution.
  As a result, `__stack_step` should always be consumed prior to any resumption points. For example:
    ```fsharp
    let inline combine (__expand_task1: (unit -> bool), __expand_task2: (unit -> bool)) =
        let __stack_step = __expand_task1()
        if __stack_step then 
            __expand_task2()
        else
            false
     ```
* A resumable `while` expression:

      <rcode> :=
          while <rcode> do <rcode>

  Note that, because the code is resumable, each `<rcode>` may contain zero or more resumption points.  The execution
  of the method may thus "begin" (via `__resumeAt`) in the middle of such a loop.

* A resumable `try-catch` expression:

      <rcode> :=
          try <rcode> with exn -> <rcode>

  Note that, because the code is resumable, each `<rcode>` may contain zero or more resumption points.  The execution
  of the code may thus "begin" (via `__resumeAt`) in the middle of either the `try` expression or `with` handler.

* If no previous case applies, a resumable `match` expression:

      <rcode> :=
          match <expr> with
          | ...  -> <rcode>
          | ...  -> <rcode>

  Note that, because the code is resumable, each `<rcode>` may contain zero or more resumption points.  The execution
  of the code may thus "begin" (via `__resumeAt`) in the middle of the code on each branch.

* If no previous case applies, a binding expression:

      <rcode> :=
          let <pat> = <expr> in <rcode>

  Note that, because the code is resumable, the `<rcode>` may contain zero or more resumption points.  The binding
  `let <pat> = <expr>` may thus not have been executed.
  
  All variables in `<pat>` used beyond the first resumption
  point are stored in the enclosing state machine to ensure their initialized value is valid on resumption.
  
  However, if a variable bound in `<pat>` has name beginning with `__stack_` then it is not given
  storage in the enclosing state machine,  but rather remains a `local` within the code of the method.

* If no previous case applies, then an arbitrary leaf expression

      <expr>

Resumable code may **not** contain `let rec` bindings.  These must be lifted out.

The execution of resumable code is best understood in terms of the direct translation of the constructs into a .NET method.
For example, `__resumeAt` corresponds either to a `goto` (for a known label) or a switch table (for a computed label at the
start of a method).

### Specifying resumable state machine structs

A struct may be used to host a resumable state machine using the following formulation:

```fsharp
    if __useResumableStateMachines then
        __resumableStateMachineStruct<StructStateMachine<'T>, _>
            (MoveNextMethod(fun sm -> <resumable-code>))
            (SetMachineStateMethod(fun sm state -> ...))
            (AfterMethod(fun sm -> ...))
    else
        ...
```
Notes:

1. The `__resumableStateMachineStruct` construct must be used instead of `__resumableStateMachine`

2. A "template" struct type must be given at a type parameter to `__resumableStateMachineStruct`, in this example it is `StructStateMachine`, a user-defined type normally in the same file.

3. The template struct type must implement one interface, the `IAsyncMachine` interface. 

4. The three delegate parameters specify the implementations of the `MoveNext`, `SetMachineState` methods, plus an `After` code
   block that is run on the state machine immediately after creation.  Delegates are used as they can receive the address of the
   state machine.

5. For each use of this construct, the template struct type is copied to to a new (internal) struct type, the state variables
   from the resumable code are added, and the `IAsyncMachine` interface is filled in using the supplied methods.

NOTE: Reference-typed resumable state machines are expressed using object expressions, which can
have additional state variables.  However F# object-expressions may not be of struct type, so it is always necessary
to fabricate an entirely new struct type for each state machine use. There is no existing construct in F#
for the anonymous specification of struct types whose methods can capture a closure of variables. The above
intrinsic effectively adds a limited version of a capability to use an existing struct type as a template for the
anonymous specification of an implicit, closure-capturing struct type.  The anonymous struct type must be immediately
eliminated (i.e. used) in the `AfterMethod`.  

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

### Library additions (tasks)

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

### Library additions (resumable state machine intrinsics)

```fsharp
namespace FSharp.Core.CompilerServices

type MoveNextMethod<'Template> = delegate of byref<'Template> -> unit

type SetMachineStateMethod<'Template> = delegate of byref<'Template> * IAsyncStateMachine -> unit

type AfterMethod<'Template, 'Result> = delegate of byref<'Template> -> 'Result

/// Contains compiler intrinsics related to the definition of state machines.
module StateMachineHelpers = 

    val __useResumableStateMachines<'T> : bool 

    val __resumableStateMachine<'T> : _obj: 'T -> 'T

    val __resumableStateMachineStruct<'Template, 'Result> : moveNext: MoveNextMethod<'Template> -> _setMachineState: SetMachineStateMethod<'Template> -> after: AfterMethod<'Template, 'Result> -> 'Result

    val __resumableEntry: unit -> int option

    val __resumeAt : pc: int -> 'T   
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

# Examples

## Example: option { ... }

See [option.fs](https://github.com/dotnet/fsharp/blob/feature/tasks/tests/fsharp/perf/tasks/FS/option.fs)

The use is as for a typical `option { ... }` computation expression builder:

```fsharp
let testOption i = 
    option {
        let! x1 = (if i % 5 <> 2 then Some i else None)
        let! x2 = (if i % 3 <> 1 then Some i else None)
        return x1 + x2
    } 
```

## Example: sync { ... }

As a micro example of defining a `sync { ... }` builder for entirely synchronous computation with no special semantics.

See [sync.fs](https://github.com/dotnet/fsharp/blob/feature/tasks/tests/fsharp/perf/tasks/FS/sync.fs)

Examples of use:
```fsharp
let t1 y = 
    sync {
       printfn "in t1"
       let x = 4 + 5 + y
       return x
    }

let t2 y = 
    sync {
       printfn "in t2"
       let! x = t1 y
       return x + y
    }

printfn "t2 6 = %d" (t2 6)
```
Code performance is approximately the same as normal F# code except for one allocation for each execution of each `sync { .. }` as we allocate the "SyncMachine".  In later work we may be able to remove this.

## Example: task { ... }

See [tasks.fs](https://github.com/dotnet/fsharp/blob/feature/tasks/src/fsharp/FSharp.Core/tasks.fs).  

## Example: taskSeq { ... }

See [taskSeq.fs](https://github.com/dotnet/fsharp/blob/feature/tasks/tests/fsharp/perf/tasks/FS/taskSeq.fs).

This is for state machine compilation of computation expressions that generate `IAsyncEnumerable<'T>` values. This is a headline C# 8.0 feature and a very large feature for C#.  It appears to mostly drop out as library code once general-purpose state machine support is available.

## Example seq2 { ... }

See [seq2.fs](https://github.com/dotnet/fsharp/blob/feature/tasks/tests/fsharp/perf/tasks/FS/seq2.fs)

This is a resumable machine emitting to a mutable context held in a struct state machine. The state holds the current
value of the iteration.

This is akin to `seq { ... }` expressions, for which we have a baked-in state machine compilation in the F# compiler today. 

## Example: low-allocation list and array builders

See [list.fs](https://github.com/dotnet/fsharp/blob/feature/tasks/tests/fsharp/perf/tasks/FS/list.fs)

These are synchronous machines emitting to a mutable context held in a struct state machine.

The sample defines  `list { .. }`, `array { .. }` and `rsarray { .. }` for collections, where the computations generate directly into a `ResizeArray` (`System.Collections.Generic.List<'T>`).
The overall result is a `list { ... }` builder that runs up to 4x faster than the built-in `[ .. ]` for generated lists of
computationally varying shape (i.e. `[ .. ]` that use conditionals, `yield` and so on).

F#'s existing `[ .. ]` and `[| ... |]` and `seq { .. } |> Seq.toResizeArray` all use an intermediate `IEnumerable` which is then iterated to populate a `ResizeArray` and then converted to the final immutable collection. In contrast, generating directly into a `ResizeArray` is potentially more efficient (and for `list { ... }` further perf improvements are possible if we put this in `FSharp.Core` and use the mutate-tail-cons-cell trick to generate the list directly). This technique has been known for a while and can give faster collection generation but it has not been possible to get good code generation for the expressions in many cases. Note that, these aren't really "state machines" because there are no resumption points - there is just an implicit collection we are yielding into in otherwise synchronous code.

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

TODO: consider warnings for this. This is also akin to another gotcha quoted from http://tomasp.net/blog/csharp-async-gotchas.aspx/ - the most common gotcha is that tail-recursive functions must use `return!` instead of `do!` to avoid leaks.


# Drawbacks

Complexity

# Alternatives

1. Don't do it.
2. Don't generalise it (just do it for tasks)

# Compatibility

This is a backward compatible addition.

# Technical note: From computation expressions to state machines

There is a strong relationship between F# computation expressions and state machines.  F# computation expressions compose functional fragments using `Bind`, `ReturnFrom` and other constructs.  With some transformation (e.g. inlining the `Bind` method and others) these quickly reduce to code similar to the inefficient implementation above. For example:

```fsharp
task {
    printfn "intro"
    let! x = task1()
    printfn "hello"
    let! y = task2()
    printfn "world"
    return x + y
}
```

becomes 

```fsharp
task.Run ( 
    task.Delay (fun () -> 
        printfn "intro"
        task.Bind(task1(), fun x -> 
            printfn "hello"
            task.Bind(task2(), fun y -> 
                printfn "world"
                task.Return (x+y)))))
```

Now the meaning of the above code depends on the definition of the builder `task`.  If we assume `Bind` is inlined to `GetAwaiter` and `AwaitOnCompleted` and `Run` ultimately accepts a `MoveNext` function then it is something like:

```fsharp
let rec state0() = 
        printfn "intro"
        let t = task1()
        let awaiter = t.GetAwaiter()
        if t.IsCompleted then 
            state1 (awaiter.GetResult())
        else
            awaiter.AwaitOnCompleted(fun () -> state1 (awaiter.GetResult()))

and state1(x) = 
        printfn "hello"
        let t = task2()
        let awaiter = t.GetAwaiter()
        if t.IsCompleted then 
            state2 (x, awaiter.GetResult())
        else
            awaiter.AwaitOnCompleted(fun () -> state2 (x, awaiter.GetResult()))

and state2(x, y) = 
        printfn "world"
        DONE (x + y)

task.Run ( fun () -> state0()))
```

However today there is no way to get the F# compiler to convert this functional code to more efficient resumable code.   Two main things need to be done. First the "state variables" are all lifted to be mutables, and the code is combined into a single method. The first step is this:

```fsharp
let mutable awaiter1 = Unchecked.defaultof<_>
let mutable xv = Unchecked.defaultof<_>
let mutable awaiter2 = Unchecked.defaultof<_>
let mutable yv = Unchecked.defaultof<_>
let rec state0() = 
        printfn "intro"
        awaiter1 <- task1().GetAwaiter()
        if awaiter1.IsCompleted then 
            state1 ()
        else
            awaiter1.AwaitOnCompleted(fun () -> state1 ())

and state1() = 
        xvar <- awaiter1.GetResult()
        printfn "hello"
        awaiter2 <- task2().GetAwaiter()
        if awaiter2.IsCompleted then 
            state2 ()
        else
            awaiter2.AwaitOnCompleted(fun () -> state2 ())

and state2() = 
        yvar <- awaiter2.GetResult()
        printfn "world"
        Task.FromResult(xvar + yvar)

task.Run ( task.Delay (fun () -> state0()))
```

then:

```fsharp
let mutable awaiter1 = Unchecked.defaultof<_>
let mutable xv = Unchecked.defaultof<_>
let mutable awaiter2 = Unchecked.defaultof<_>
let mutable yv = Unchecked.defaultof<_>
let mutable pc = 0
let next() =
    match pc with  
    | 0 -> 
        printfn "intro"
        awaiter1 <- task1().GetAwaiter()
        if awaiter1.IsCompleted then 
            pc <- 1
            return CONTINUE
        else
            pc <- 1
            awaiter1.AwaitOnCompleted(this)
            return AWAIT
    | 1 -> 
        xvar <- awaiter1.GetResult()
        awaiter2 <- task2().GetAwaiter()
        if awaiter2.IsCompleted then 
            pc <- 2
            return CONTINUE
        else
            pc <- 2
            awaiter2.AwaitOnCompleted(this)
            return AWAIT
    | 2 -> 
        printfn "world"
        return DONE (xvar + yvar)

task.Run (... return a task that repeatedly calls next() until AWAIT or DONE ...)
```

This is a sketch to demonstrate the progression from monadic computation expression code to compiled state machine code with integer state representations.

Note:

* the above kind of transformation is **not** valid for all computation expressions - it depends on the implementation details of the computation expression.  It also depends on doing this transformation for a finite number of related binds (i.e. doing it for all of a single `task { ... }` or other CE expression), which allows the use of a compact sequence of integers to represent the different states.   
* The transformation where values passed between states become "state machine variables" is also not always valid - this can extend the lifetime of values in subtle ways, though the state machine generation can also generally zero out the variables at appropriate points.

* If the computation expression contains conditional control flow (`if ... then... else` and `match` and `while` and `for`) then the AWAIT points can occur in the middle of generated code, and thus the initial `match` in the integer-based version can branch into the middle of control-flow.
 
* If the computation expression contains exception handling then these can sometimes be carefully implemented using the stack-based control-flow constructs in .NET IL and regular F# code.  However this must be done with care.

The heart of a typical state machine is a `MoveNext` or `Step` function that takes an integer `program counter` (`pc`) and jumps to a target:

```fsharp
      member __.MoveNext() = 
            match pc with
               | 1 -> goto L1 
               | 2 -> goto L2 
               | _ -> goto L0

            L0: ...
                ... this code can return, first setting "pc <- L1"...

            L1: ...
                ... this code can return, e.g. first setting "pc <- L2"...

            L2: ...

```

This is roughly what compiled `seq { ... }` code looks like in F# today and what compiled async/await code looks like in C#, at a very high level. Note that, you can't write this kind of code directly in F# - there is no `goto` and especially not a `goto` that can jump directly into other code, resuming from the last step of the state machine.  

# Unresolved questions

* [ ] ContextInsensitiveTasks
* [ ] `let rec` not supported in state machines, possibly other constructs too
* [ ] Consider adding `Unchecked` to names of primitives.  
* [ ] Document the ways the mechanism can be cheated.  For example, the `__resumeAt` can be cheated by using an arbitrary integer for the destination. The code will still be verifiable, however it will be the equivalent of a drop-through the switch statement generated for a `__resumeAt`. Because of this it likely still warrants an `Unchecked`. 
* [ ] consider warnings for the lack of asynchronous tailcalls.

* [ ] "I have a design question. Is there any reason magic naming (i.e. __expand_) was used over, say, attributes? Was it ease of implementation, or because of limitations of attributes? Or something else?"

  > This is all still open for discussion.  Basically, attributes aren't allowed on expression-locals in F# today, and I was looking for something sufficiently weird to highlight that this is a here-be-dragons compiler optimization feature.

