# F# RFC FS-1087 - Resumable code and resumable state machines

The design suggestion [Native support for task { ... } ](https://github.com/fsharp/fslang-suggestions/issues/581) has been marked "approved in principle".
This RFC covers the detailed proposal for the resumable state machine support needed for this and other features.

- [x] Approved in principle
- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/581)
- [x] [RFC Discussion](https://github.com/fsharp/fslang-design/issues/455)
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/6811)

# Summary

We add a general low-level capability recognized by the F# compiler to specify and emit statically compositional resumable
code hosted in state machines. This allows some F#
computation expressions including `task` and `taskSeq` to be implemented highly efficiently. It is also similar to the
implementation of sequence expressions baked into the F# compiler and the implementation has been derived from that
initially.

This is used to implement [RFC FS-1097 - tasks](https://github.com/fsharp/fslang-design/blob/master/FSharp-6.0/FS-1097-task-builder.md).

This is also related to [RFC FS-1098 - inline if lambda attribute](https://github.com/fsharp/fslang-design/blob/master/FSharp-6.0/FS-1098-inline-if-lambda.md)

This is also related to [Tooling RFC FST-1034 - additional lambda optimizations](https://github.com/fsharp/fslang-design/blob/master/tooling/FST-1034-lambda-optimizations.md)

# Motivation

F# has a very general mechanism for describing computations called [computation expressions](https://docs.microsoft.com/en-us/dotnet/fsharp/language-reference/computation-expressions).  Some examples are `seq { ... }`, `task { ...}`, `async { ... }`, `asyncSeq { ... }`, `option { ... }` and many more. 

`task { ... }` and other computation expressions admit very low-allocation implementations. Some other examples are sequences and asynchronous
sequences. Implementations of `task { ... }` exist for F# today, e.g. [TaskBuilder.fs](https://github.com/rspeele/TaskBuilder.fs/) and [Ply](https://github.com/crowded/ply)
but they tend to have allocation overhead.

There are enough variations on such computation expressions (for example, whether tailcalls are supported, or other tradeoffs - see the FAQ at the end)
that it is better to provide a general mechanism in F# that allows the efficient compilation of a large range of
such constructs rather than baking each into the F# compiler.

To examine the variation in asynchronous and resumable control constructs, consider the following categorization:

|    | produces | async-waits |  results | hot/cold/multi   |  tailcalls  | cancellation token propagation | cancellation checks | explicitly schedulable | boxing |
|:----:|:-----:|:-----:|:-------:|:------:|:------:|:---------:|:--------:|:---------:|:---------:|
| normal code | `T` | no async waits | one result | once, hot start |  tailcalls |   explicit | explicit |  no | |
| [cancellable](https://github.com/dotnet/fsharp/blob/main/src/fsharp/absil/illib.fs#L716) | `Cancellable<_>` |  no async waits | one result |  multiple cold starts |  tailcalls |   implicit | implicit | no | |
| [resumable](https://github.com/dotnet/fsharp/blob/main/src/fsharp/absil/illib.fs#L837) |   `Resumable<_>` | no async wait | one result |  multiple cold starts |  tailcalls |   implicit | implicit | yes | |
| seq | `IEnumerable<_>` | no async waits | multiple results | multiple cold starts | tailcalls | explicit | explicit | no | |
| task |  `Task<_>` |  async waits | one result | once, hotstart |  no-tailcalls |   explicit | explicit | no | |
| vtask |  `ValueTask<_>` |  async waits | one result | once, hotstart |  no-tailcalls |   explicit | explicit | no | box-after-creation | 
| async | `Async<_>` | async waits | one result | multiple cold starts |  tailcalls |   implicit | implicit | no | |
| taskSeq  | `IAsyncEnumerable<_>` | async waits | multi result | multiple cold starts | no tailcalls |  implicit | explicit |  no | |
| asyncSeq | `AsyncSeq<_>` | async waits | multi result | multiple cold starts |  tailcalls | implicit | implicit | no | |


# Design Philosophy and Principles

The general mechanism has a very similar effect to adding [co-routines](https://en.wikipedia.org/wiki/Coroutine) to the F# language and runtime.
However co-routines themselves are of little use to F#, since "task" support largely subsumes co-routines, and needs similar mechanisms.
So we use a general static code-weaving mechanism (built around the 'inline' mechanism) to build an
overall resumable code method for control structure, which is then combined with user-code, which can also be used for multiple
control structures whose most efficient implementation is built on resumable code. This mechanism is part of the F# compiler.

The design philosophy is as follows:

1. No new syntax is added to the F# language. 

2. The primary aim is for _statically composable_ resumable code. That is, chunks of resumable code that can be merged
   to form an overall resumable state machine in user code.

3. The F# metadata format is unchanged. Like `nameof` and other F# features,
   resumable state machines are encoded within existing TypedTree constructs using a combination of known compiler intrinsics
   and TypedTree expresions.

4. We treat this as a compiler feature. The actual feature is barely surfaced
   as a language feature, but is rather a set of idioms known to the F# compiler, together used to build efficient computation
   expression implementations.

5. The feature is activated in compiled code.  An alternative implementation of the primitives can be
   given for reflective execution, e.g. for interpretation of quotation code.

6. The feature is not fully checked during type checking, and some checks are made as code is emitted. This means
   mis-implemented resumable code may be detected late in the compilation process, potentially when compiling user code.

7. The feature is designed for use only by highly skilled F# developers to implement low-allocation computation
   expression builders.

8. Semantically, there's nothing you can do with resumable state machines that you can't already do with existing
   workflows. It is better to think of them as a performance feature, a compiler optimization partly implemented
   in workflow library code.

Points 1-3 guide many of the decisions below.

# Detailed design


### Specifying resumable code

Resumable code is a new low-level primitive form of compositional re-entrant code suitable only for writing
high-performance compiled implementations of computation expressions.

Resumable code is represented by the `ResumableCode<'Data, 'T>` delegate type. 

```fsharp
type ResumableCode<'Data, 'T> = delegate of byref<ResumableStateMachine<'Data>> -> bool
```

The `'Data` type parameter indicates the extra data stored in the state machine for this variation of resumable code. It may be
a struct or reference type. The `'T` type parameter is not used in the delegate but is useful for constraining the combinations
of resumable code allow in computation expressions.

Resumable code is formed by either:

1. Resumable code combinators, that is, calls to `ResumableCode.Zero`, `ResumableCode.Delay`, `ResumableCode.Combine` and other functions from FSharp.Core, or

2. Writing new explicit low-level `ResumableCode` delegate implementations.

The only reason to specify resumable code is to implement the `MoveNext` method of state machines, see further below.
In practice this is only needed when defining new variations on computation expressions using state machines.

### Resumable code combinators

Most resumable code can be specified using the functions in `ResumableCode.*`.
Functions forming resumable code should be inline.  For example:

```fsharp
let inline printThenYield () =
    ResumableCode.Combine(
        ResumableCode.Delay(fun () -> printfn "hello"; ResumableCode.Zero()),
        ResumableCode.Yield()
    )
```     

#### ResumableCode.Yield

A resumption point can be created by invoking `ResumableCode.Yield` as a `ResumableCode` value:

```fsharp
ResumableCode.Yield()
```

or within low-level resumable code:

```fsharp
let __stack_yield_complete = ResumableCode.Yield().Invoke(&sm)
```

Here `__stack_yield_complete` will return `false` if the code suspends and `true` if the code resumes at the
implied resumption point.  `ResumableCode.Yield` has the following definition in terms of low-level
resumable code (see further below).

```fsharp
let inline Yield () : ResumableCode<'Data, unit> = 
     ResumableCode<'Data, unit>(fun sm -> 
         if __useResumableCode then 
             match __resumableEntry() with 
             | Some contID ->
                 sm.ResumptionPoint <- contID
                 false
             | None ->
                 true
         else
             YieldDynamic(&sm))
```

#### ResumableCode.Combine

Specifies the sequential composition of two blocks of resumable code.
   
```fsharp
ResumableCode.Combine(<resumable-code>, <resumable-code>)
```

Because the code is resumable, each `<resumable-code>` may contain zero or more resumption points.
This means it is **not** guaranteed that the first `<resumable-code>` will be executed before the
second.

#### ResumableCode.TryWith

Specifies try/with semantics for resumable code.

```fsharp
ResumableCode.TryWith(<resumable-code>, <resumable-code>)
```

#### ResumableCode.TryFinally, ResumableCode.TryFinallyAsync

Specifies try/finally semantics for resumable code.

```fsharp
ResumableCode.TryFinally(<resumable-code>, <compensation>)
```
or

```fsharp
ResumableCode.TryFinallyAsync(<resumable-code>, <resumable-code>)
```

The combinator does not use a low-level .NET IL `try`/`finally` block, but rather a `try`/`with`, see the implementation.

`ResumableCode.TryFinallyAsync` can be used to allow resumable code in the logical `finally` block, e.g. for `IAsyncDisposable`.
Note that while the F# computation expression syntax doesn't allow binding in the 'finally' block, `TryFinallyAsync` can still be used
by `Using` and `use!` when binding the resource implements `IAsyncDisposable`.

#### ResumableCode.While

Specifies iterative semantics for resumable code.
```fsharp
ResumableCode.While((fun () -> expr), <resumable-code>)
```

Note that, because the code is resumable, the `<resumable-code>` may contain zero or more resumption points.   The execution
of the code may thus branch into the middle of the `while` expression.

The guard expression is not resumable code and can't contain a resumption point.  Asynchronous while loops that
contain asynchronous while conditions must be handled by placing the resumable code for the guard expression
separately, see examples.


### Specifying low-level resumable code

Low-level resumable code occurs in the bodies of `ResumableCode<_,_>(fun sm -> <optional-resumable-expr>)` 
and `__stateMachine` `MoveNextMethodImpl` delegate implementations.
Specifying low-level resumable code generates a warning which should be suppressed if intended.
The return value of a `ResumableCode` delegate indicates if the code completed (`true`) or yielded (`false`).

```fsharp
    ResumableCode<_,_>(fun sm -> <optional-resumable-expr>) 
    MoveNextMethodImpl(fun sm -> <resumable-expr>)
```

For example:

```fsharp
    ResumableCode<_,_>(fun sm -> printfn "hello"; true) 
```
    
An `<optional-resumable-expr>` is:

```fsharp
   if __useResumableCode then <resumable-expr> else <expr>
```
or
```fsharp
   <resumable-expr>
```

If the overall state machine in which the resumable code is _compilable_ (see further below) then `<resumable-expr>` is used, otherwise `<expr>` is used.

A `<resumable-expr>` is:

1. A resumption point created by an explicit `__resumableEntry`

   ```
   match __resumableEntry() with
   | Some contId -> <resumable-expr>
   | None -> <resumable-expr>
   ```

   If such an expression is executed, the first `Some` branch is taken. However a resumption point is also defined which,
   if a resumption is performed using `__resumeAt`, executes the `None` branch.
  
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

2. A `__resumeAt` expression

   ```fsharp
   __resumeAt <expr>
   ```

   At runtime, `__resumeAt contId` will jump directly to 'None' branch of the corresponding `match __resumableEntry ... `.
   All `__stack_*` locals in scope will be zero-initialized on resumption.

3. A `let` binding of a stack-bound variable initialized to zero on resumption.

   ```fsharp
   let __stack_var = ... in <resumable-expr> 
   ```

   Within resumable code, the name `__stack_*` indicates that the variable is always stack-bound and given the default value on resumption.
   
   Note that, because the code is resumable, the  `<resumable-expr>` may contain zero or more resumption points.  Again this
   means it is not guaranteed that the first `<resumable-expr>` will be executed before the
   second - a `__resumeAt` call can jump straight into the second code when the method is executed to resume previous execution.
   As a result, the variable should always be consumed prior to any resumption points and re-assigned if used after any resumption points. For example:

   ```fsharp
   let inline combine ([<ResumableCode>] __expand_task1: (unit -> bool), __expand_task2: (unit -> bool)) : [<ResumableCode>] (unit -> bool)  =
       (fun () -> 
           let __stack_step = __expand_task1()
           if __stack_step then 
               __expand_task2()
           else
              false)
   ```

4. A resumable try/with expression:

   ```fsharp
   try <resumable-expr> with <expr>
   ```

   Because the body of the try/with is resumable, the `<resumable-expr>` may contain zero or more resumption points.  The execution
   of the code may thus branch (via `__resumeAt`) into the middle of the `try` expression.
   
   > Note that the rules of .NET IL prohibit jumping directly into the code block of a try/with.  Instead the F# compiler
   > arranges that a jump is performed to the `try` and a subsequent jump is performed after the `try`.
   
   The `with` block is not resumable code and can't contain a resumption point.

5. A resumable try/finally expression:

   ```fsharp
   try <resumable-expr> finally <expr>
   ```
   
   Similar rules apply as for `try-with`. Because the body of the try/finally  is resumable, the `<resumable-expr>` may contain zero or more resumption points.  The execution
   of the code may thus branch (via `__resumeAt`) into the middle of the `try` expression.
   
   Note that in F# 
   
   > Note that the rules of .NET IL prohibit jumping directly into the code block of a try/with.  Instead the F# compiler
   > arranges that a jump is performed to the `try` and a subsequent jump is performed after the `try` is entered.
   
6. A resumable while-loop

   ```fsharp
   while <expr> do <resumable-expr>
   ```

   Note that, because the code is resumable, the `<resumable-expr>` may contain zero or more resumption points.   The execution
   of the code may thus branch (via `__resumeAt`) into the middle of the `while` expression.

   The guard expression is not resumable code and can't contain a resumption point.  Asynchronous while loops that
   contain asynchronous while conditions must be handled by placing the resumable code for the guard expression
   separately, see examples.

7. A sequential execution of resumable code

   ```fsharp
   <resumable-stmt>; <resumable-stmt>
   ```

   Note that, because the code is resumable, each `<resumable-stmt>` may contain zero or more resumption points.
   This means it is **not** guaranteed that the first `<resumable-stmt>` will be executed before the
   second - a `__resumeAt` call can jump straight into the second code.

8. A call/invoke of a `ResumableCode` delegate/function parameter, e.g.

    ```fsharp
    code arg
    code.Invoke(&sm)
    (code arg).Invoke(&sm)
    ```

    NOTE: Using delegates to form compositional code fragments is particularly useful because a delegate may take a byref parameter, normally
    the address of the enclosing value type state machine.

9. If no previous case applies, a resumable `match` expression:

    ```fsharp
    match <expr> with
    | ...  -> <resumable-expr>
    | ...  -> <resumable-expr>
    ```

    Note that, because the code is resumable, each `<resumable-stmt>` may contain zero or more resumption points.  The execution
    of the code may thus "begin" (via `__resumeAt`) in the middle of the code on each branch.

10. Any other F# expression

    ```fsharp
    <expr>
    ```

Static checks are performed for the specificaiton of low-level resumable code as outlined above.


### Hosting resumable code in a resumable state machine struct

A resumable state machine is specified using `__stateMachine`.
Resumable code is always ultimately hosted in a compiler-generated struct type based on `ResumableStateMachine`.
```fsharp
    __stateMachine<_, _>
        (MoveNextMethod(fun sm -> <resumable-code>))
        (SetMachineStateMethod(fun sm state -> ...))
        (AfterMethod(fun sm -> ...))
```
If the state machine is eventually hosted in a boxed object, then it must be held as a field within an object,
see the longer examples.

At compile-time, when `__stateMachine` is encountered, the `ResumableStateMachine` type guides the generation of a new struct type by the F# compiler
with added closure-capture fields in a way similar to an object expression. 
Any mention of the `ResumableStateMachine` type in the `MoveNextMethod`, `SetMachineStateMethod` and `AfterMethod` are rewritten to this
fresh struct type.  The 'methods' are used to implement the `IAsyncStateMachine` interface of `ResumableStateMachine` on the generated struct type.
The `AfterMethod` method is then executed and must eliminate the state machine by running it and potentially saving it to the heap.
Its return type must not include ResumableStateMachine.

```fsharp
[<Struct; NoComparison; NoEquality>]
type ResumableStateMachine<'Data> =
    val mutable Data: 'Data
    val mutable ResumptionPoint: int
    val mutable ResumptionDynamicInfo: ResumptionDynamicInfo<'Data>
    interface IResumableStateMachine<'Data>
    interface IAsyncStateMachine
```

Notes:

1. The three delegate parameters specify the implementations of the `MoveNext`, `SetMachineState` methods, plus an `After` code
   block that is run on the state machine immediately after creation.  Delegates are used as they can receive the address of the
   state machine.

2. For each use of this construct, the `ResumableStateMachine` struct type is copied to to a new (internal) struct type, the state variables
   from the resumable code are added, and the `IAsyncStateMachine` interface is filled in using the supplied methods.

3. The `MoveNext` method may be resumable code, see below.

NOTE: By way of explanation, state machines are a form of closure.  However existing F# closures may not be of struct type, and it is necessary
to fabricate a new struct type for each state machine use. There is no existing construct in F#
for the anonymous specification of struct types whose methods can capture a closure of variables. The `__stateMachine`
intrinsic adds a limited version of a capability to use a specific existing struct type as a template for the
anonymous specification of an implicit, closure-capturing struct type.  The anonymous struct type must be immediately
eliminated (i.e. used) in the `AfterMethod`.  

### Compilability of state machines

A state machine is not _compilable_ if its resumable code is not compilable, that is, if any of the following are true for its inlined, expanded form:

1. The resumable code is an integer `for` loop with `__resumableEntry` points in the body.

2. The resumable code is a `let rec`.

3. The resumable code contains an unreduced use of a `ResumableCode` parameter.

4. The resumable code is a try/finally with `__resumableEntry` points.

   > NOTE: The resumable code combinator `ResumableCode.TryFinally` does not use a low-level try/finally block, see the implementation.

5. The resumable code is a try/with where the `with` block  has `__resumableEntry` points.

   > NOTE: The resumable code combinators `ResumableCode.TryWith` and `ResumableCode.TryFinally` return resumable code that implements
   > resumable exception handlers without hitting these restrictions, see the implementations of these two functions.

If a state machine is not compilable, see "Execution of non-compilable state machines" below.

> NOTE: Non-compilable state machines often occur when defining functions producing state machines.
> This occurs because any `ResumableCode` parameters are not yet fully defined through inlining.
> 
> State machines are made compilable using 'inline' on the function.  However all F# inlined code also has corresponding non-inline code emitted for 
> reflection and quotations.  For this reason, when defining functions producing state machines, an   `if __useResumableCode then` alternative should 
> still typically be given, even if your function is inlined.

#### Execution of compilable state machines

The execution of of a compilable state machine can be understood in terms of the direct translation its low-level resumable code to the constructs into a .NET method.
For example, `__resumeAt` corresponds either to a `goto` (for a known label) or a switch table (for a computed label at the
start of a method).

If a `ResumableCode` expression is determined to be valid resumable code, then the semantics of the
method or function hosting the resumable code is detemined by the following:

1. All implementations are inlined under the static assumption `__useResumableCode` is true.

2. All resumption points `match __resumableEntry() with Some contId -> <stmt1> | None -> <stmt2>` are removed by the static allocation of a unique integer within the resumable code for `contID` and using `<stmt1>` as the primary implementation.  `stmt2` is placed as the target for `contID` in a single implied jump table for the overall resumable code.

3. Any `__stack_*` variables are represented as locals of the method. These are zero-initialized each time the method is invoked.

4. Any non `__stack_*` variables are represented as locals of the host object. (Note, if the variables are not used in or after continuation branches then they may be represented as locals of the method).

5. Any uses of `__resumeAt <expr>` are represented as an invocation of the implied jump table.

   - If `<expr>` is a statically-determined code label (e.g. a `contID`) then this is effectively a `goto` statement
     to the `None` branch of the resumption point corresponding to the `contID`. 

   - If `<expr>` is not a statically-determined code label then the `__resumeAt` must be the first statement within the method.
     If at runtime the `<expr>` doesn't correspond to a valid resumption point within the method then execution continues subsequent to the `__resumeAt`.
   
#### Execution of non-compilable state machines

If a state machine is not compilable and the state machine is guarded by `if __useResumableCode` as follows:

```fsharp
    if __useResumableCode then
        <state-machine-expr>
    else
        <alternative-expr>
```         

then a warning is emitted and the alternative is used as a regular F# expression.  If no alternative is given a compilation failure occurs.

The alternative may include the invoke of `ResumableCode` delegate values. In this case, a warning is emitted and should be suppressed if intended.

The low-level code for `ResumableCode` can include also guards `if __useResumableCode then .. else ..`.  These alternatives are used
for resumable code that occurs outside the successful compilation of a state machine. Such alternatives should always be given
for any explicit `ResumableCode` delegate that explicitly returns `false` values, indicating suspension of computation.
In this case, the alternative must set the `ResumptionFunc` field in `ResumptionDynamicInfo` of
the enclosing state machine to an appropriate dynamic continuation.  For example, see the implementation of `ResumableCode.Yield`
and `ResumableCode.YieldDynamic`.

## Debuggable two-phase inlined code

A general construct `__debugPoint` is added to allow manual specification of debug points in delayed-execution portions of inlined code. `__debugPoint ""` should only be used in inlined code, though this is not checked. In this RFC it is used to improve debugging of `for` loops, however the construct is more generally useful for inlined code.

As background, debug points are removed from inlined code. This means, for example, that any local functions within that code (that are not themselves inlined) will show as "code not available".

To avoid this, inlined code may use `__debugPoint` to place debug points. The source range associated with the debug point is "where the code is inlined to". For example consider:


```fsharp
open FSharp.Core.CompilerServices.StateMachineHelpers

// ----- Composable computation framework

type F = F of (unit -> unit)

let inline Compose(F f, F g) = F (fun () -> __debugPoint ""; f(); g())

let inline Print s = F (fun () -> __debugPoint ""; printfn "%s" s)

let Run (F f) = f()

// ----- Composition
let f = Compose(Print "a", Print "b")

// ----- Execution
Run f
```

Here the inlined closures have debug points associated with the source ranges `Compose(Print "a", Print "b")`, `Print "a"` and `Print "b"`. That is, debugging the _execution_ of the composition steps uses source code locations at the composition itself, rather than the implementations (if not inlined) or "no source code available" (if `__debugPoint` is not used).

Although orthogonal to this RFC, this construct is useful to help improve debugging of computation expressions, which we document here for completeness. Consider for example the following compositional computations:

```fsharp
// Cold-start cancellable code
type Cancellable<'T> = Cancellable of (CancellationToken -> unit)
```

Now consider the code

```fsharp
let f() =
    cancellable {
        let! v = expr
        body 
    }
```

which is equivalent to this (ignoring details such as `cancellable.Delay` calls):

```fsharp
let f() =
    cancellable.Bind(expr, (fun v -> body))
```

The `Bind` operation for `let!` can be written as follows:

```fsharp
module Cancellable =

    /// Run a cancellable computation using the given cancellation token
    let run (ct: CancellationToken) (Cancellable oper) =
        if ct.IsCancellationRequested then
            raise (System.OperationCanceledException ct)
        else
            oper ct

type CancellableBuilder() =
    member inline _.Bind(comp, [<InlineIfLambda>] body) =
        Cancellable(fun ct ->
            __debugPoint ""
            Cancellable.run ct (body v1))
```

Now, `cancellable.Bind(expr, (fun v -> body))` is by default given source range `let! v = expr` by the F# compiler, and a default debug point is places before the `Bind` call with this source range. We can visualize this as follows:

```fsharp
let f() =
    DebugPoint "let! v = expr"
    cancellable.Bind(expr, (fun v -> body))
```

Because of inlining `Bind` and `__debugPoint`, a 2nd debug point is also placed, and the flattened code after inlining can be visualized like this:

```fsharp
let f() =
    DebugPoint "let! v = expr"
    let comp = expr
    Cancellable(fun ct ->
        DebugPoint "let! v = expr"
        Cancellable.run ct (k v1))
```

This has the following properties:

* Stack traces when running `comp` include a function at location `let! v = expr`

* A breakpoint may be placed on `let!`, associated with each of the two debug points. The first causes a break before the evaluation of `expr` (producing `comp`) and the second causes a break before the "run" of `comp`.

* If a breakpoint has been placed on `let!`, then step-into or step-over at the first debug point will progress to the second debug point at the same source location. After reaching the second debug point, the `body` code can then be stepped through.

Note while this is useful, it doesn't give perfect debugging, in particular:

* If no debug point has been placed on `let!`, then step-over at the first debug point will **not** proceed to the body of the computation (as the user will assume), but instead the whole invocation of the `Cancellable` resulting from the `Bind` will be stepped-over.

The use of two debug points with identical source ranges `"let! v = expr"` is not ideal for stepping or breaking, however it does mean reasonable stack traces and source locations are given on the second phase of execution. In future RFCs the debug locations available to be named when using `__debugPoint` may be refined.

## Library additions (primitive resumable code)

```fsharp
namespace FSharp.Core.CompilerServices

/// A special compiler-recognised delegate type for specifying blocks of resumable code
/// with access to the state machine.
type ResumableCode<'Data, 'T> = delegate of byref<ResumableStateMachine<'Data>> -> bool

module StateMachineHelpers = 

    /// Indicates a resumption point within resumable code
    val __resumableEntry: unit -> int option

    /// Indicates to jump to a resumption point within resumable code.
    /// This may be the first statement in a MoveNextMethodImpl.
    /// The integer must be a valid resumption point within this resumable code.
    val __resumeAt : programLabel: int -> 'T
```

## Library additions (resumable code combinators)

A set of combinators is provided for combining resumable code. This is the normal way to specify resumable code for computation expression builders,
see the examples.

```fsharp
namespace FSharp.Core.CompilerServices

/// Contains functions for composing resumable code blocks
module ResumableCode =

    /// Sequences one section of resumable code after another
    val inline Combine: code1: ResumableCode<'Data, unit> * code2: ResumableCode<'Data, 'T> -> ResumableCode<'Data, 'T>

    /// Creates resumable code whose definition is a delayed function
    val inline Delay: f: (unit -> ResumableCode<'Data, 'T>) -> ResumableCode<'Data, 'T>

    /// Specifies resumable code which iterates an input sequence
    val inline For: sequence: seq<'T> * body: ('T -> ResumableCode<'Data, unit>) -> ResumableCode<'Data, unit>

    /// Specifies resumable code which iterates yields
    val inline Yield: unit -> ResumableCode<'Data, unit>

    /// Specifies resumable code which executes with try/finally semantics
    val inline TryFinally: body: ResumableCode<'Data, 'T> * compensation: ResumableCode<'Data,unit> -> ResumableCode<'Data, 'T>

    /// Specifies resumable code which executes with try/finally semantics
    val inline TryFinallyAsync: body: ResumableCode<'Data, 'T> * compensation: ResumableCode<'Data,unit> -> ResumableCode<'Data, 'T>

    /// Specifies resumable code which executes with try/with semantics
    val inline TryWith: body: ResumableCode<'Data, 'T> * catch: (exn -> ResumableCode<'Data, 'T>) -> ResumableCode<'Data, 'T>

    /// Specifies resumable code which executes with 'use' semantics
    val inline Using: resource: 'Resource * body: ('Resource -> ResumableCode<'Data, 'T>) -> ResumableCode<'Data, 'T> when 'Resource :> IDisposable

    /// Specifies resumable code which executes a loop
    val inline While: [<InlineIfLambda>] condition: (unit -> bool) * body: ResumableCode<'Data, unit> -> ResumableCode<'Data, unit>

    /// Specifies resumable code which does nothing
    val inline Zero: unit -> ResumableCode<'Data, unit>

```

## Library additions (resumable state machines)

```fsharp
namespace FSharp.Core.CompilerServices

/// Acts as a template for struct state machines introduced by __stateMachine, and also as a reflective implementation
[<Struct>]
type ResumableStateMachine<'Data> =

    interface IResumableStateMachine<'Data>
    interface IAsyncStateMachine

    /// When statically compiled, holds the data for the state machine
    val mutable Data: 'Data

    /// When statically compiled, holds the continuation goto-label further execution of the state machine
    val mutable ResumptionPoint: int

type IResumableStateMachine<'Data> =
    /// Get the resumption point of the state machine
    abstract ResumptionPoint: int

    /// Copy-out or copy-in the data of the state machine
    abstract Data: 'Data with get, set

/// Defines the implementation of the MoveNext method for a struct state machine.
type MoveNextMethodImpl<'Data> = delegate of byref<ResumableStateMachine<'Data>> -> unit

/// Defines the implementation of the SetStateMachine method for a struct state machine.
type SetStateMachineMethodImpl<'Data> = delegate of byref<ResumableStateMachine<'Data>> * IAsyncStateMachine -> unit

/// Defines the implementation of the code reun after the creation of a struct state machine.
type AfterCode<'Data, 'Result> = delegate of byref<ResumableStateMachine<'Data>> -> 'Result

module StateMachineHelpers = 

    /// Statically generates a closure struct type based on ResumableStateMachine,
    /// At runtime an instance of the new struct type is populated and 'afterMethod' is called
    /// to consume it.
    val __stateMachine<'Data, 'Result> :
        moveNextMethod: MoveNextMethodImpl<'Data> -> 
        setStateMachineMethod: SetStateMachineMethodImpl<'Data> -> 
        afterCode: AfterCode<'Data, 'Result> 
            -> 'Result
```

### Library additions (for execution of dynamically-specified resumable code)

The following additions help support dynamic composition and execution of resumable code. These execute less efficiently.

```fsharp
namespace FSharp.Core.CompilerServices

[<Struct>]
type ResumableStateMachine<'Data> =
    ...
    /// Represents the delegated runtime continuation for a resumable state machine created dynamically
    val mutable ResumptionDynamicInfo: ResumptionDynamicInfo<'Data>

/// Represents the delegated runtime continuation of a resumable state machine created dynamically
type ResumptionDynamicInfo<'Data> =
    new: initial: ResumptionFunc<'Data> -> ResumptionDynamicInfo<'Data>
    
    member ResumptionFunc: ResumptionFunc<'Data> with get, set 
    
    /// Delegated MoveNext
    abstract MoveNext: machine: byref<ResumableStateMachine<'Data>> -> unit
    
    /// Delegated SetStateMachine
    abstract SetStateMachine: machine: byref<ResumableStateMachine<'Data>> * machineState: IAsyncStateMachine -> unit

/// Represents the runtime continuation of a resumable state machine created dynamically
type ResumptionFunc<'Data> = delegate of byref<ResumableStateMachine<'Data>> -> bool

/// Contains functions for composing resumable code blocks
module ResumableCode =

    /// The dynamic implementation of the corresponding operation. This operation should not be used directly.
    val CombineDynamic: sm: byref<ResumableStateMachine<'Data>> * code1: ResumableCode<'Data, unit> * code2: ResumableCode<'Data, 'T> -> bool

    /// The dynamic implementation of the corresponding operation. This operation should not be used directly.
    val WhileDynamic: sm: byref<ResumableStateMachine<'Data>> * condition: (unit -> bool) * body: ResumableCode<'Data, unit> -> bool

    /// The dynamic implementation of the corresponding operation. This operation should not be used directly.
    val TryFinallyAsyncDynamic: sm: byref<ResumableStateMachine<'Data>> * body: ResumableCode<'Data, 'T> * compensation: ResumableCode<'Data,unit> -> bool

    /// The dynamic implementation of the corresponding operation. This operation should not be used directly.
    val TryWithDynamic: sm: byref<ResumableStateMachine<'Data>> * body: ResumableCode<'Data, 'T> * handler: (exn -> ResumableCode<'Data, 'T>) -> bool

    /// The dynamic implementation of the corresponding operation. This operation should not be used directly.
    val YieldDynamic: sm: byref<ResumableStateMachine<'Data>> -> bool

module StateMachineHelpers = 

    /// When used in a conditional, statically determines whether the 'then' branch
    /// represents valid resumable code and provides an alternative implementation
    /// if not.
    val __useResumableCode<'T> : bool 
```

### Library additions (for future List builders and high-performance list functions)

As an aside, earlier editions of this RFC explored more efficient list and array builders.
To support the definition of faster list builders outside FSharp.Core, we expose a library intrinsic to allow the tail-mutation of
lists. Like other constructs in FSharp.Core this is a low-level primitive not for use in user-code and carries a warning.

> NOTE: this may be moved to a separate RFC

```fsharp
namespace Microsoft.FSharp.Core.CompilerServices

    [<RequireQualifiedAccess>]
    module RuntimeHelpers = 
        ...
    
        [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
        [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
        val inline FreshConsNoTail: head: 'T -> 'T list
        
        [<Experimental("Experimental library feature, requires '--langversion:preview'")>]
        [<MethodImpl(MethodImplOptions.NoInlining)>]
        [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
        val SetFreshConsTail: cons: 'T list -> tail: 'T list -> unit
```

This function can also be used for higher-performance list function implementations external to FSharp.Core, though must be used with care.

## Library additions (debuggable two-phase code)

```fsharp
module StateMachineHelpers = 

    /// Indicates a named debug point arising from the context of inlined code.
    ///
    /// Only a limited range of debug point names are supported.
    ///
    /// If the debug point name is the empty string then the range used for the debug point will be
    /// the range of the outermost expression prior to inlining.
    ///
    /// If the debug point name is <c>ForLoop.InOrToKeyword</c> and the code was ultimately
    /// from a <c>for .. in .. do</c> or <c>for .. = .. to .. do</c> construct in a computation expression,
    /// de-sugared to an inlined <c>builder.For</c> call, then the name "ForLoop.InOrToKeyword" can be used.
    /// The range of the debug point will be precisely the range of the <c>in</c> or <c>to</c> keyword.
    ///
    /// If the name doesn't correspond to a known debug point arising from the original source context, then
    /// an opt-in warning 3514 is emitted, and the range used for the debug point will be
    /// the range of the root expression prior to inlining.
    val __debugPoint: string -> unit
```

## Examples

### A one-step state machine

As a very simple example, consider the following:
```fsharp
/// zero-allocation call to an interface method
let inline MoveNext(x: byref<'T> when 'T :> IAsyncStateMachine) = x.MoveNext()

/// Make one call to the state machine and return its data
let inline MoveOnce(x: byref<'T> when 'T :> IAsyncStateMachine and 'T :> IResumableStateMachine<'Data>) = 
    MoveNext(&x)
    x.Data

let makeStateMachine()  = 
    __stateMachine<int, int>
         (MoveNextMethodImpl<_>(fun sm -> 
             if __useResumableCode then
                 sm.Data <- 1 // we expect this result for successful resumable code compilation
             else
                 sm.Data <- 0xdeadbeef // if we get this result it means we've failed to compile as resumable code
             )) 
         (SetStateMachineMethodImpl<_>(fun sm state -> ()))
         (AfterCode<_,_>(fun sm -> MoveOnce(&sm)))
```
# Longer example - basic coroutines 

At high approximation a Coroutine is Task<unit> without async I/O allowed. They make an interesting entry-level example of what you can build with resumable code.
   
> NOTE: Co-routines are not particularly useful in F# programming, with higher-level control structures like `task` and `taskSeq` and `async`
> and so on preferred. There is no plan to add co-routines to FSHarp.Core.  Tasks are "better" than coroutines in that: 
>  
> * you get async I/O
> * exceptions get stored away when they happen
> * you get to return a result `Task<T>` (which makes them more "functional" and type-safe)

See [coroutineBasic.fs](https://github.com/dotnet/fsharp/blob/main/tests/benchmarks/CompiledCodeBenchmarks/TaskPerf/TaskPerf/coroutineBasic.fs).

In this example we show how to use resumable code to define a computation expression for a basic form of coroutines. The logical
properties are:
1. These coroutines are always boxed (for initially-unboxed, see "tasks")
2. These coroutines are cold-start (for hot start, see "tasks")
3. These coroutines are "use once" - you can't run them multiple times  (for multi-execution, see "taskSeq")
4. They have an integer id

Our goal is to be able to write code like this:

```fsharp
    let t1 () = 
        coroutine {
           printfn "in t1"
           yield ()
           printfn "hey ho"
           yield ()
        }
```
and run it:
```fsharp
    let dumpCoroutine (t: Coroutine) = 
        printfn "-----"
        while (t.MoveNext() &&
               not t.IsCompleted) do 
            printfn "yield"
```
and be able to inspect the generated `MoveNext` method and see that it is efficient and without allocation due to the control structures

First we define basic type:

```fsharp
/// This is the type of coroutines
[<AbstractClass>] 
type Coroutine() =
    
    /// Gets the ID of the coroutine
    abstract Id: int

    /// Checks if the coroutine is completed
    abstract IsCompleted: bool

    /// Executes the coroutine until the next 'yield'
    abstract MoveNext: unit -> unit
```
In this example our state machines will store just one element of extra data - an ID. 
```fsharp
/// This extra data stored in ResumableStateMachine (and it's templated copies using __stateMachine) 
/// In this example there is just an ID
[<Struct>]
type CoroutineStateMachineData(id: int) = 
    member _.Id = id

let nextId = 
    let mutable n = 0
    fun () -> n <- n + 1; n
```
 These are standard definitions filling in the 'Data' parameter of each
```fsharp
type ICoroutineStateMachine = IResumableStateMachine<CoroutineStateMachineData>
type CoroutineStateMachine = ResumableStateMachine<CoroutineStateMachineData>
type CoroutineResumptionFunc = ResumptionFunc<CoroutineStateMachineData>
type CoroutineResumptionDynamicInfo = ResumptionDynamicInfo<CoroutineStateMachineData>
type CoroutineCode = ResumableCode<CoroutineStateMachineData, unit>
```
Next we define a builder for coroutines, where the internal compositional type of the builder is `CoroutineCode`. an instantiation of `ResumableCode<_,_>`

```fsharp
type CoroutineBuilder() =
    
    member inline _.Delay(f : unit -> CoroutineCode) : CoroutineCode = ResumableCode.Delay(f)

    [<DefaultValue>]
    member inline _.Zero() : CoroutineCode = ResumableCode.Zero()

    member inline _.Combine(code1: CoroutineCode, code2: CoroutineCode) : CoroutineCode =
        ResumableCode.Combine(code1, code2)

    member inline _.While ([<InlineIfLambda>] condition : unit -> bool, body : CoroutineCode) : CoroutineCode =
        ResumableCode.While(condition, body)

    member inline _.TryWith (body: CoroutineCode, catch: exn -> CoroutineCode) : CoroutineCode =
        ResumableCode.TryWith(body, catch)

    member inline _.TryFinally (body: CoroutineCode, [<InlineIfLambda>] compensation : unit -> unit) : CoroutineCode =
        ResumableCode.TryFinally(body, ResumableCode<_,_>(fun _ -> compensation(); true))

    member inline _.Using (resource : 'Resource, body : 'Resource -> CoroutineCode) : CoroutineCode when 'Resource :> IDisposable = 
        ResumableCode.Using(resource, body)

    member inline _.For (sequence : seq<'T>, body : 'T -> CoroutineCode) : CoroutineCode =
        ResumableCode.For(sequence, body)

    member inline _.Yield (_dummy: unit) : CoroutineCode = 
        ResumableCode.Yield()
```
Next we define an implementation of `Coroutine` in terms of an arbitrary struct type `'Machine` that implements `IAsyncStateMachine` and `IResumableStateMachine`
```fsharp
/// This is the implementation of Coroutine with respect to a particular struct state machine type.
[<NoEquality; NoComparison>] 
type Coroutine<'Machine when 'Machine : struct
                        and 'Machine :> IAsyncStateMachine 
                        and 'Machine :> ICoroutineStateMachine>() =
    inherit Coroutine()

    // The state machine struct
    [<DefaultValue(false)>]
    val mutable Machine: 'Machine

    override cr.IsCompleted =
        GetResumptionPoint(&cr.Machine) = -1

    override cr.MoveNext() = 
        MoveNext(&cr.Machine)

    override cr.Id() = 
        GetData(&cr.Machine).Id
```
This is an important definition
* This is a boxed type, implementing `Coroutine`
* `'Machine` will be instantiated to a compiler-generated struct type based on `ResumableStateMachine`
* Some helpers are used, using the standard trick for zero-allocation calls to interface methods on .NET structs:
```fsharp
[<AutoOpen>]
module internal Helpers =
    let inline MoveNext(x: byref<'T> when 'T :> IAsyncStateMachine) = x.MoveNext()
    let inline GetResumptionPoint(x: byref<'T> when 'T :> IResumableStateMachine<'Data>) = x.ResumptionPoint
    let inline SetData(x: byref<'T> when 'T :> IResumableStateMachine<'Data>, data) = x.Data <- data
    let inline GetData(x: byref<'T> when 'T :> IResumableStateMachine<'Data>) = x.Data
```

Next we define the `Run` method for the builder.
```fsharp
    /// Create the state machine and outer execution logic
    member inline _.Run(code : CoroutineCode) : Coroutine = 
        if __useResumableCode then 
            __stateMachine<CoroutineStateMachineData, Coroutine>

                // IAsyncStateMachine.MoveNext
                (MoveNextMethodImpl<_>(fun sm -> 
                    __resumeAt sm.ResumptionPoint 
                    let __stack_code_fin = code.Invoke(&sm)
                    if __stack_code_fin then
                        sm.ResumptionPoint  <- -1 // indicates complete))

                // IAsyncStateMachine.SetStateMachine
                (SetStateMachineMethodImpl<_>(fun sm state -> ()))

                // Box the coroutine.  In this example we don't start execution of the coroutine.
                (AfterCode<_,_>(fun sm -> 
                    let mutable cr = Coroutine<CoroutineStateMachine>()
                    SetData(&cr.Machine, CoroutineStateMachineData(nextId()))
                    cr.Machine <- sm
                    cr :> Coroutine))
        else 
            failwith "dynamic implementation nyi"
```
Finally, we add a dynamic implementation for the coroutine, in cases where state machine compilation can't be used:
```fsharp
        else 
            // The dynamic implementation
            let initialResumptionFunc = CoroutineResumptionFunc(fun sm -> code.Invoke(&sm))
            let resumptionInfo =
                { new CoroutineResumptionDynamicInfo(initialResumptionFunc) with 
                    member info.MoveNext(sm) = 
                        if info.ResumptionFunc.Invoke(&sm) then
                            sm.ResumptionPoint <- -1
                    member info.SetStateMachine(sm, state) = ()
                 }
            let mutable cr = Coroutine<CoroutineStateMachine>()
            cr.Machine.ResumptionDynamicInfo <- resumptionInfo
            cr.Machine.Data <- CoroutineStateMachineData(nextId())
            cr :> Coroutine
```
Here
* The state machine has a `MoveNext` method which advances the machine.
* The `AfterMethod` is run after the stack-allocation of the state machine and hosts it in a boxed `Coroutine<'Machine>` object, setting the Id.
* Note these coroutines are always boxed, and are not started immediately. In `AfterCode` we do not take a step of the state machine
  before returning the coroutine.

Finally we instantiate the builder:
```fsharp
let coroutine = CoroutineBuilder()
```
and use it:
```fsharp
    let t1 () = 
        coroutine {
           printfn "in t1"
           yield ()
           printfn "hey ho"
           yield ()
        }
```
and run it:
```fsharp
    let dumpCoroutine (t: Coroutine) = 
        printfn "-----"
        while ( t.MoveNext()
                not t.IsCompleted) do 
            printfn "yield"
```
The IL code for the MoveNext method of the coroutine can be inspected to check no closures are created,
resumption points are properly created, and its assembly code checked for performance.

The generated IL for MoveNext is approximately as follows. Note the use of a jump table based on `ResumptionPoint`.
While there are improvements that can be made here, the JIT will perform obvious improvements, and there are no allocations
```
.method public strict virtual instance void 
        MoveNext() cil managed
{
  .override [System.Runtime]System.Runtime.CompilerServices.IAsyncStateMachine::MoveNext
...
  IL_0000:  ldarg.0
  IL_0001:  ldfld      int32 Tests.CoroutinesBasic/Examples/'t1@158-2'::ResumptionPoint
  IL_0006:  stloc.0
  IL_0007:  ldloc.0
  IL_0008:  ldc.i4.1
  IL_0009:  sub
  IL_000a:  switch     (IL_001a, IL_001d)
  IL_0017:  nop
  IL_0018:  br.s       IL_0020
  IL_001a:  nop
  IL_001b:  br.s       IL_003f
  IL_001d:  nop
  IL_001e:  br.s       IL_0074
  IL_0020:  ldarg.0
  IL_0021:  stloc.2
  IL_0022:  ldstr      "in t1"
  <printing>
  IL_0039:  ldarg.0
  IL_003a:  stloc.s    V_5
  IL_003c:  ldc.i4.0
  IL_003d:  brfalse.s  IL_0043
  IL_003f:  ldc.i4.1
  IL_0040:  nop
  IL_0041:  br.s       IL_004d
  IL_0043:  ldloc.s    V_5
  IL_0045:  ldc.i4.1
  IL_0046:  stfld      int32 Tests.CoroutinesBasic/Examples/'t1@158-2'::ResumptionPoint
  IL_004b:  ldc.i4.0
  IL_004c:  nop
  IL_004d:  stloc.s    V_4
  IL_004f:  ldloc.s    V_4
  IL_0051:  brfalse.s  IL_0084
  IL_0053:  ldarg.0
  IL_0054:  stloc.s    V_5
  IL_0056:  ldstr      "hey ho"
  <printing>
  IL_006d:  ldloc.s    V_5
  IL_006f:  stloc.s    V_6
  IL_0071:  ldc.i4.0
  IL_0072:  brfalse.s  IL_0078
  IL_0074:  ldc.i4.1
  IL_0075:  nop
  IL_0076:  br.s       IL_0086
  IL_0078:  ldloc.s    V_6
  IL_007a:  ldc.i4.2
  IL_007b:  stfld      int32 Tests.CoroutinesBasic/Examples/'t1@158-2'::ResumptionPoint
  IL_0080:  ldc.i4.0
  IL_0081:  nop
  IL_0082:  br.s       IL_0086
  IL_0084:  ldc.i4.0
  IL_0085:  nop
  IL_0086:  stloc.1
  IL_0087:  ldloc.1
  IL_0088:  brfalse.s  IL_0092
  IL_008a:  ldarg.0
  IL_008b:  ldc.i4.m1
  IL_008c:  stfld      int32 Tests.CoroutinesBasic/Examples/'t1@158-2'::ResumptionPoint
  IL_0091:  ret
  IL_0092:  ret
} // end of method 't1@158-2'::MoveNext  
```

## Example: coroutine { ... } with tailcalls

See [coroutine.fs](https://github.com/dotnet/fsharp/blob/main/tests/benchmarks/TaskPerf/coroutine.fs).

This is for state machine compilation of coroutine computation expressions that support yielding and tailcalls.

## Example: task { ... }

See [tasks.fs](https://github.com/dotnet/fsharp/blob/main/src/FSharp.Core/tasks.fs).  

## Example: taskSeq { ... }

See [taskSeq.fs](https://github.com/dotnet/fsharp/blob/main/tests/benchmarks/CompiledCodeBenchmarks/TaskPerf/TaskPerf/taskSeq.fs).

This is for state machine compilation of computation expressions that generate `IAsyncEnumerable<'T>` values. This is a headline C# 8.0 feature and a very large feature for C#.  It appears to mostly drop out as library code once general-purpose state machine support is available.

## Example: reimplementation of F# async

I did a trial re-implementation of F# async (imperfectly and only a subset of the API) using resumable code. You can take a look at the subset that's implemented by looking in the signature file

* Implementation: https://github.com/dotnet/fsharp/blob/main/tests/benchmarks/CompiledCodeBenchmarks/TaskPerf/TaskPerf/async2.fs

* Signature fle: https://github.com/dotnet/fsharp/blob/main/tests/benchmarks/CompiledCodeBenchmarks/TaskPerf/TaskPerf/async2.fsi

Recall how async differs from tasks:

|    | async-waits |  results | hot/cold/multi   |  tailcalls  | cancellation token propagation | cancellation checks |
|:----:|:-----:|:-------:|:------:|:------:|:---------:|:--------:|
| F# async | async-waits | one result | multiple cold starts |  tailcalls |   implicit | implicit |
| F# task/C# task |   async-waits | one result | once-hot-start |  no-tailcalls |   explicit | explicit |
| F# seq | no-async-waits | multiple results | multi cold starts | tailcalls | none | none |
| F# taskSeq/C# async seq  | async waits | mutli result | multi-cold-start | no-tailcalls |  implicit | explicit | 

Anyway the approximate reimplementation appears to run as fast as TaskBuilder for sync cases, and as fast as tasks for async cases. That makes it like 10-20x faster than the current F# async implementation.  Stack traces etc. would be greatly improved to.
 
However it's not a perfect reimplementation - there are no tailcalls nor cancellation checks yet  -  and perfect compat is probably impossible sadly, there are lots of subtleties. For example `async.Return()` and other direct use of CE methods change type from `Async<T>` to `AsyncCode<T>`, so the API is not perfect compat (an `Async.Return` etc. would be needed instead).   We could possible fix that in the F# compiler though there are lots of other little niggles too.

That said it should be good enough to allow an FSharp.Control.Async2 package that is a drop-in replacement for F# async for 99.9% compat.  (The `Async2<T>` would be a different type in that case, though that may matter less now `Task<T>` is so established more as an interop standard)

# Performance

[Recent perf status of implementation](https://github.com/dotnet/fsharp/blob/main/BenchmarkDotNet.Artifacts/results/TaskPerf.Benchmarks-report-github.md)

# Drawbacks

### Complexity

The mechanism is non-trivial.

### Non-compilability

Not all F# constructs can yet be included in compilable resumable code, notable "fast integer for loops" and "let rec".  The
first doesn't matter since, inside computation expressions, integer for-loops work over IEnumerables in any case (though this
may cause more allocations than expected). However `let rec` is not yet usable, and, for example, this generates a compilation warning
during code generation, indicating that the dynamic implementation of tasks will be used:

```fsharp
        task { 
            let rec f x = f x + 1
            return f 1
        }
```
The problem here is that we have previously encountered subtle recursive-fixup bugs when making mutually-recursive
functions into state variable fields of state machines.  Also, for non-escaping functions, ideally 
a non-closure representation should be used for these functions in any case.

On the whole this is not a problem since these don't generally appear in F# computation expressions for tasks etc. 
The user can, of course, lift out the `let rec` binding outside the task, and this may also result in clearer and faster
code in any case.

It is possible these restrictions can be lifted in future iterations, however if we do we should also lift them
for failed state machine compilation of `let rec` in sequence expressions (for which no warning is currently given)

### Imperfect optimization

The resumable code composition and elimination happens late in the F# compiler.  Not all code optimizations are applied.


### Potential for over-use

The code-weaving mechanism of resumable code can also be used to accurately statically combine non-resumable code fragments. For example, this is
done by the `list { .. }`, `option { .. }` and `voption { .. }` examples.

The code achieved is more reliably efficient than that achieved by simply inlining all combinators, because user code is identified as resumable code and
passed in via `ResumableCode` parameters which are statically inlined and flattened through the code weaving process.  Additionally, the control code and
user code can be woven via delegates taking the "this" state machine argument as a byref to a struct state machine (e.g. see the `list` sample) which
means zero allocations occur in the final resulting code.

There is a risk that this mechanism will prove so effective at statically eliminating allocations of closures that there will
it will start to be used to eliminate for synchronous code taking function parameters, resulting in subtle and obfuscated code.

See https://github.com/fsharp/fslang-design/blob/master/FSharp-6.0/FS-1098-inline-if-lambda.md for the RFC for this



# Alternatives

### Don't do it

There's always an option not to.

### Build in compiler support for each computation expression

The C# compiler builds in specific support for tasks and asynchronous sequences.  This means the only user-code that can be efficiently resumable is code
that returns these two types.

### Restrict use to FSharp.Core

In the preview of this feature, it will be possible to use this feature outside FSHarp.Core with the `/langversion:preview` flag.

It is possible that a future release will only make this feature non-preview within FSharp.Core, and withdraw the feature for external use.

### Micro decisions

There are many alternatives possible with regard to relatively small decisions, for example

* Naming
* Whether the `__resumeAt` construct is explcit at all at the start of `MoveNext` methods
* Whether the `__stateMachine` construct is a new language construct or, as in this design, is a compiler-special construct built out of existing syntax.
* many others

We encourage the reader to remember this is a low-level mechanism and essentially none of the details are user-facing.



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


# Resolved issues from review

* [x] we should also verify the stacktraces are minimal and complete. Specifically we should test post-yield exceptions.

* [x] We need to generate `CompilerGenerated` on the state machine types, for better debugging, see https://github.com/dotnet/coreclr/pull/15781 and
      the changes to do this in Ply. https://github.com/crowded/ply/blob/master/Ply.fs#L93. This is related to the state machine detection code
      in CoreCLR that runs while generating stack traces, see https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Diagnostics/StackTrace.cs#L396.
      
* [x] remove ResumptionDynamicInfo from the generated state machines 

* [x] Bind task `use!` to IAsyncDisposable? `use` desugars to builder.Using and we could overload that on both IDisposable and IAsyncDisposable.
      If types  support both we sort out a priority

      ```fsharp
      task { use res = some-IAsyncDisposable
             .. }
      ```
       
* Should we support `vtask { ... }` or `valueTask { ... }` for ValueTask?

  Answer: Not as yet. It's almost simple to define from the outside, but a different AsyncValueTaskMethodBuilder thing is
  needed, so you probably have to replicate all of tasks.fs with a different state machine type.
  We could engineer tasks.fs based on AsyncValueTaskMethodBuilder but it is only in netstandard2.1,
  so that would mean no tasks on .NET Framework.  

# Unresolved questions and notes from review

* [ ] check generated ASM code 

  > Nino says: I've been following many of the runtime advancements in the past few years and it's doing a much better job at F# code these days but the benchmarks do suggest the runtime isn't able to reduce all of it to C# level output


# FAQ

## Are these co-routines?

@dsyme replies

> Coroutines are almost a no-op to build on the mechanism - they're in the examples for educational purposes.
> I'm not planning on adding them to F# as such, though users could define them.  So in that sense it's
> essentially adding co-routines, though there is no specific type for co-routines in the library.


## Why a general mechanism?

Mads Torgersen asked:

> I like the generalization to "resumable code" (not quite coroutines?). As you may remember we tried
> in C# to generalize existing iterators (sequences) to a coroutine-like concept in the language to use
> for async, but had to give it up. Part of that was because of syntax, but some of it was a lack of
> scenarios beyond sequences, async and the combo of async sequences. I'm curious if you have some in mind!

@dsyme replies:

> Regarding generality, the mechanism is worth it for F# just for `task { .. }` and `taskSeq { .. }` alone
> since it means much of the implementation lies in the F# library not the F# compiler, where it's much easier to work with.
> 
> However for F#, important variations on these come up, particularly around 
> 1. implicit passing of cancellation tokens (e.g. a `task2 {..}` that does this)
> 2. tailcalls (does an infinite chain of `return! otherTask` run in finite size? likewise with `taskSeq`)
> 3. what can you bind to?
> 
> These are important considerations for functional
> where recursion and implicit information propagation is more common.
> 
> There are also the cold/hot start variations, and the context sensitive/insensitive variations (ConfigureAwait(false) for tasks).  
> So having the general mechanism without baking all these into the compiler seems right.
> 
> I'm still undecided if we'll see other uses.  There is a whole zoo of synchronous computation expressions
> which needed better compilation - that's what RFC FS-1098 and FST-1034 are about.
> e.g. `option {...}`, `cancellable { .. }` (implicitly propagating a cancellation token through synchronous code), 
> parser combinators and so on.  
> 
> Then there is the original - F# `async { .. }` - which  effectively adds explicit-multi-start, tailcalls
> and cancellation token propagation to tasks.  It might benefit from this - though compat will make it
> hard for us to reimplement async to use this. 
> 
> People also define `asyncOption {...}`, combining async and option. In principle this indicates an
> efficiently compiled `taskOption { ... }` might be desirable.   See
> [here](https://github.com/dotnet/fsharp/blob/dbf9a625d3188184ecb787a536ddb85a4ea7a587/vsintegration/src/FSharp.Editor/CodeFix/RenameUnusedValue.fs#L33)
> for an example where this is used in the F# implementation.
> 
> So in essence, variations and combinations.
> 
