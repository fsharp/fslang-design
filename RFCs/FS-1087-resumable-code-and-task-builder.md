# F# RFC FS-1087 - Resumable state machines and task builder

The design suggestion [Native support for task { ... } ](https://github.com/fsharp/fslang-suggestions/issues/581) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] Approved in principle
- [ ] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/581)
- [ ] [RFC Discussion](https://github.com/fsharp/fslang-design/issues/FILL-ME-IN)
- [ ] [Prototype](https://github.com/dotnet/fsharp/pull/6811)

# Summary

1. Add the ability to specify and emit resumable code hosted in a state machine object to the F# compiler

2. Use this to implement a `task` builder in FSharp.Core

# Motivation

`task { ... }` support is needed in F# with good quality, low-allocation generated code using resumable code in state machines.

We generalize this mechanism to allow low-allocation implementations of other computation expressions.

## Design Philosophy

The design philosophy is to treat this more as a compiler feature, where the actual feature is barely surfaced
as a language feature, but is rather a set of idioms known to the F# compiler, used to build efficient computation
expression implementations.

The compiler feature is only
activated in **compiled** code and not seen in F# quotations.  An alternative implementation can be given for
interpretation of quotation code.

The feature is not fully checked during type checking, but rather checks are made as code is emitted. 

The feature should only be used by highly skilled F# developers to implement higher-performance computation
expression builders.

# Detailed design

### State machines

Resumable code is specified using the ``__resumableObject`` compiler primitive, giving a state machine expression:
```
    if __useResumableCode then
        __resumableObject
            { new SomeStateMachineType() with 
                member __.Step ()  = 
                   <resumable code>
           }
    else
        <dynamic-implementation>
```
Notes

* `__useResumableCode` and `__resumableObject` are well-known compiler intrinsics in `FSharp.Core.CompilerServices.StateMachineHelpers`

* A value-type state machine may also be used to host the resumable code, see below. 

* Here `SomeStateMachineType` can be any user-defined reference type, and the object expression must contain a single method.

* The `if __useResumableCode then` is needed because the compilation of resumable code is only activated when code is compiled.
  The `<dynamic-implementation>` is used for dynamically interpreted code, e.g. quotation interpretation.
  In prototyping it can simply raise an exception. It should be semantically identical to the other branch.
  
* The above construct should be seen as a language feature.  First-class uses of of constructs such as `__resumableObject` are not allowed except in the exact pattern above.

### Resumable code

Resumable code is made of the following grammar:

* An call to an inlined function defining further resumable code, e.g. 

      let inline f () = <rcode>
      
      f(); f() 

  Such a function can have function parameters using the `__expand_` naming, e.g. 

      let inline callTwice __expand_f = __expand_f(); __expand_f()
      let inline print() = printfn "hello"
      
      callTwice print

  These parameters give rise the expansion bindings, e.g. the above is equivalent to
  
      let __expand_f = print
      __expand_f()
      __expand_f()

   Which is equivalent to
   
      print()
      print()

* An expansion binding definition (normally arising from the use of an inline function):

      let __expand_abc <args> = <expr> in <rcode>

  Any name beginning with `__expand_` can be used.
  Such an expression is treated as `<rcode>` with all uses of `__expand_abc` macro-expanded and beta-var reduced.
  Expansion binding definitions usually arise from calls to inline functions taking function parameters, see above.

* An invocation of a function known to be a lambda expression via an expansion binding:

      __expand_abc <args>

  An invocation can also be of a delegate known to be a delegate creation expression via an expansion binding:

      __expand_abc.Invoke <args>

  NOTE: Using delegates to form compositional code fragments is particularly useful because a delegate may take a byref parameter, normally
  the address of the enclosing value type state machine.

* A resumption point:

      match __resumableEntry() with
      | Some contID -> <rcode>
      | None -> <rcode>

  If such an expression is executed, the first `Some` branch is taken. However a resumption point is also defined which,
  if a resumption is performed, executes the `None` branch.
  
  The `Some` branch usually suspends execution by saving `contID` into the state machine
  for later use with a `__resumeAt` execution at the entry to the method. For example:
  
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

  Note a resumption expression can return a result - in the above the resumption expression indicates whether the
  task ran to completion or not.

* A `resumeAt` expression:

      __resumeAt <expr>

  Here <expr> is an integer-valued expression indicating a resumption point, which must be eith 0 or a `contID` arising from a resumption
  point resumable code expression on a previous execution.
  
* A sequential exection of two resumable code blocks:

      <rcode>; <rcode>

  Note that, because the code is resumable, each `<rcode>` may contain zero or more resumption points.
  This means it is **not** guaranteed that the first `<rcode>` will be executed before the
  second - a `__resumeAt` call can jump straight into the second code when the method is executed to resume previous execution.

* A binding sequential exection. The identifier ``__stack_step`` must be used precisely.

      let __stack_step = <rcode> in <rcode>

  Note that, because the code is resumable, each `<rcode>` may contain zero or more resumption points.  Again this
  means it is not guaranteed that the first `<rcode>` will be executed before the
  second - a `__resumeAt` call can jump straight into the second code when the method is executed to resume previous execution.
  As a result, `__stack_step` should always be consumed prior to any resumption points. For example:
   
    let inline combine (__expand_task1: (unit -> bool), __expand_task2: (unit -> bool)) =
          let __stack_step = __expand_task1()
          if __stack_step then 
              __expand_task2()
          else
              false

* A resumable `while` expression:

      while <rcode> do <rcode>

  Note that, because the code is resumable, each `<rcode>` may contain zero or more resumption points.  The execution
  of the method may thus "begin" (via `__resumeAt`) in the middle of such a loop.

* A resumable `try-catch` expression:

      try <rcode> with exn -> <rcode>

  Note that, because the code is resumable, each `<rcode>` may contain zero or more resumption points.  The execution
  of the code may thus "begin" (via `__resumeAt`) in the middle of either the `try` expression or `with` handler.

* A resumable `match` expression:

      match <expr> with
      | ...  -> <rcode>
      | ...  -> <rcode>

  Note that, because the code is resumable, each `<rcode>` may contain zero or more resumption points.  The execution
  of the code may thus "begin" (via `__resumeAt`) in the middle of the code on each branch.

* A binding expression:

      let <pat> = <expr> in <rcode>

  Note that, because the code is resumable, the `<rcode>` may contain zero or more resumption points.  The binding
  `let <pat> = <expr>` may thus not have been executed.  However,  all non-stack variables in `<pat>` used beyond the first resumption
  point are stored in the enclosing state machine to ensure their initialized value is valid on resumption.

* An arbitrary leaf expression

Resumable code may **not** contain `let rec` bindings.  These must be lifted out.

### Struct state machines

A struct may be used to host a state machine using the following formulation:

```fsharp
    if __useResumableCode then
        __resumableStruct<StructStateMachine<'T>, _>
            (MoveNextMethod(fun sm -> <resumable-code>))
            (SetMachineStateMethod<_>(fun sm state -> ...))
            (AfterMethod<_,_>(fun sm -> ...))
    else
        ...
```
Notes:

1. A "template" struct type must be given including a stub implementation of the `IAsyncMachine` interface. 

2. The `__resumableStruct` construct must be used instead of `__resumableObject`

3. The three delegate parameters specify the implementations of the `MoveNext`, `SetMachineState` methods, plus an `After` method
   that is run on the state machine immediately after creation.

4. For each use of this construct, the template struct type is copied to to a new (internal) struct type, the state variables
   from the resumable code are added, and the `IAsyncMachine` interface is filled in using the supplied methods.

For example:
```fsharp
[<Struct; NoEquality; NoComparison>]
type OptionStateMachine<'T> =
    [<DefaultValue(false)>]
    val mutable Result : 'T option

    static member Run(sm: byref<'K> when 'K :> IAsyncStateMachine) = sm.MoveNext()

    interface IAsyncStateMachine with 
        member sm.MoveNext() = failwith "no dynamic impl"
        member sm.SetStateMachine(state: IAsyncStateMachine) = failwith "no dynamic impl"

type OptionCode<'T> = delegate of byref<OptionStateMachine<'T>> -> unit

type OptionBuilder() =

    member inline __.Delay(__expand_f : unit -> OptionCode<'T>) : OptionCode<'T> = OptionCode (fun sm -> (__expand_f()).Invoke &sm)

    member inline __.Combine(__expand_task1: OptionCode<unit>, __expand_task2: OptionCode<'T>) : OptionCode<'T> =
        OptionCode<_>(fun sm -> 
            let mutable sm2 = OptionStateMachine<unit>()
            __expand_task1.Invoke &sm2
            __expand_task2.Invoke &sm)

    member inline __.Bind(res1: 'T1 option, __expand_task2: ('T1 -> OptionCode<'T>)) : OptionCode<'T> =
        OptionCode<_>(fun sm -> 
            match res1 with 
            | None -> ()
            | Some v -> (__expand_task2 v).Invoke &sm)

    member inline __.Return (value: 'T) : OptionCode<'T> =
        OptionCode<_>(fun sm ->
            sm.Result <- ValueSome value)

    member inline __.Run(__expand_code : OptionCode<'T>) : 'T option = 
        if __useResumableCode then
            __resumableStruct<OptionStateMachine<'T>, 'T option>
                (MoveNextMethod<_>(fun sm -> 
                       __expand_code.Invoke(&sm)))

                (SetMachineStateMethod<_>(fun sm state -> ()))

                (AfterMethod<_,_>(fun sm -> 
                    OptionStateMachine<_>.Run(&sm)
                    sm.ToOption()))
        else
            let mutable sm = OptionStateMachine<'T>()
            __expand_code.Invoke(&sm)
            sm.ToOption()
```

NOTE: This is an awkward formulation.  Reference-typed state machines are expressed using object expressions, whcih can
have additional state variables.  However in F# object-expressions may not be of struct type, so it is always necessary
to fabricate a new struct type for each state machine use.  Further, it is important that this be based on an existing, well-known
struct type for the specification of the fragments of code. Finally, the use of delgates is necessary to propgate the
address of the state machine throughout the code fragments specified by the builder calls.

The formualtion above was chosen to meet all these requirements.  Since the formulation effectively forms a compiler feature,
but a rarely used one used only in library code, it seems reasonable to make it awkward.

# Examples

### Using the mechanism code for low-allocation synchronous builders

The mechanisms above are enough to allow the definition of computation expression implementations that "expand out" 
the relevant code fragments into flattened code that runs w.r.t. some (mutable, accumulating) state machine context. 

For example:

```fsharp
[<Struct; NoEquality; NoComparison>]
type YieldStateMachine<'T> =
    [<DefaultValue(false)>]
    val mutable Result : ResizeArray<'T>

    /// A standard definition to start the state machine without copying the struct
    static member Run(sm: byref<'K> when 'K :> IAsyncStateMachine) = sm.MoveNext()

    interface IAsyncStateMachine with 
        member sm.MoveNext() = failwith "no dynamic impl"
        member sm.SetStateMachine(state: IAsyncStateMachine) = failwith "no dynamic impl"

    member sm.Yield (value: 'T) = 
        match sm.Result with 
        | null -> 
            let ra = ResizeArray()
            sm.Result <- ra
            ra.Add(value)
        | ra -> ra.Add(value)

type YieldCode<'T> = delegate of byref<YieldStateMachine<'T>> -> unit

type ListBuilder() =

    member inline __.Delay(__expand_f: unit -> YieldCode<'T>) : YieldCode<'T> =
        YieldCode (fun sm -> (__expand_f()).Invoke &sm)

    member inline __.Zero() : YieldCode<'T> =
        YieldCode(fun _sm -> ())

    member inline __.Combine(__expand_task1: YieldCode<'T>, __expand_task2: YieldCode<'T>) : YieldCode<'T> =
        YieldCode(fun sm -> 
            __expand_task1.Invoke &sm
            __expand_task2.Invoke &sm)
            
    member inline __.While(__expand_condition : unit -> bool, __expand_body: YieldCode<'T>) : YieldCode<'T> =
        YieldCode(fun sm -> 
            while __expand_condition() do
                __expand_body.Invoke &sm)

    member inline b.For(sequence: seq<'TElement>, __expand_body: 'TElement -> YieldCode<'T>) : YieldCode<'T> =
        b.Using (sequence.GetEnumerator(), 
            (fun e -> b.While((fun () -> e.MoveNext()), (fun sm -> (__expand_body e.Current).Invoke &sm))))

    member inline __.Yield (v: 'T) : YieldCode<'T> =
        YieldCode(fun sm -> sm.Yield v)

    member inline __.Run(__expand_code : YieldCode<'T>) : ResizeArray<'T> = 
        if __useResumableCode then
            __resumableStruct<YieldStateMachine<'T>, _>
                (MoveNextMethod(fun sm -> __expand_code.Invoke(&sm)))
                (SetMachineStateMethod(fun sm state -> ()))
                (AfterMethod(fun sm -> 
                    YieldStateMachine<_>.Run(&sm)
                    sm.Result |> Seq.toList))
        else
            let mutable sm = YieldStateMachine<'T>()
            __expand_code.Invoke(&sm)
            sm.Result

let list = ListBuilder()
```

Here there are no resumption points, but the inlining of the code "expands" the user code fragments and implements
the `yield` operation with respect to the propagated `sm` address of the state machine.

The overall result is a `list { ... }` builder that runs up to 4x faster than the built-in `[ .. ]` for generated lists of
computationally varying shape (i.e. `[ .. ]` that use conditionals, `yield` and so on).

# Drawbacks

Complexity

# Alternatives

1. Don't do it.
2. Don't generalise it (just to it for tasks)

# Compatibility

This is a backward compatiable addition.

# Unresolved questions

TBD
