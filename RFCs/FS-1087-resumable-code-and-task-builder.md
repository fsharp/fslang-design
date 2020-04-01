# F# RFC FS-1087 - Resumable compiled code and task builder

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

`task { ... }` support is needed in F#.  The primary mechanism need for this is a way to flatten a composition
of calls to methods like `task.Bind` and `task.Return` and then convert each task to a state machine using
a single method made of "resumable code", that is, code that includes a jump table and the start and, when
resuming, jumps into the middle of a .NET method using the logical equivalent of a `goto` statement.

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

### Value type state machines

TBD

### Resumable code and builders

TBD

# Examples

Example code:

```fsharp
TBD
```

# Drawbacks

TBD

# Alternatives

TBD

# Compatibility

This is a backward compatiable addition.

# Unresolved questions

TBD
