
## Additional Lambda Optimizations

The [RFC FS-1097](https://github.com/fsharp/fslang-design/blob/master/FSharp-6.0/FS-1097-task-builder.md)
and [FS-1087](https://github.com/fsharp/fslang-design/blob/master/FSharp-6.0/FS-1087-resumable-code.md)
highlighted the need for some additional optimizations.

The F# optimizer is considered part of tooling and, while its optimizations are not all documented via RFCs
it is worth writing out the spec of the new optimizations here and their rationale as a tooling RFC. THis
is partly to ensure we have test coverage for these.


1. `__stack_` prefixed vars are not eliminated during optimization. The rationale is that these
   are only used when describing resumable code, primarily for tasks.

2. `InlineIfLambda` attribute is recognized and applied.  The rationale is covered in
   [RFC FS-1098](https://github.com/fsharp/fslang-design/blob/master/FSharp-6.0/FS-1098-inline-if-lambda.md).
   
3. If a computed function is immediately executed in a sequential block the code is reduced, e.g.

       let part1 = fexpr in part1 arg; rest

       --->  
       
       fexpr arg; rest
      
   if `fexpr` is not used in `rest`. Importantly, this applies no matter what expression `fexpr` is.  

   Likewise if a computed delegate is immediately executed in a sequential block the code is reduced, e.g.

       let part1 = delexpr in part1(); rest
      
       --->  
       
       delexpr.Invoke(arg); rest
      
   In both cases the application of the function/delegate is a beta-reduction application with subsequent optimization.
   
   
4. Delegate construction of F#-defined delegate types now gives rise to a "known lambda value" in the internal operation of the optimizer and cross-module
   optimization information.   This is a non-breaking conservative addition to the cross-module optimization information.
   
   This ony applies to F#-defined delegate types.  This could be extended to .NET-defined delegate types but there was no need for this for the
   above RFCs.
   
   When F#-defined delegate expressions with known value are invoked, the corresponding reduction occurs.  So
   
       type D = delegate of int -> int
       
       let v = D(fun x -> x + 1)
       v.Invoke(3) // reduces to '4'
       
   This also applies in cases such as where the delegate type takes byref arguments.
   
   
5. Optimization of `match __resumableEntry() with` constructs is completely skipped.  This is because they have special semantics for resumable code, see the RFC.

6. We optimize certain common patterns related to computed functions that are rare in normal F# code,
   but very common in F# lambda code representing synchronous machines.
   
   Specificlly we "lift" pre-computations off computed functions, allowing the resulting function lambda to be "exposed" so reduction can
   occur.
   
   For this case, we always lift 'let', 'letrec', sequentials and 'match' off computed functions so:

   ```fsharp
   (let x = 1 in fexpr) arg ---> let x = 1 in fexpr arg 
   
   (let rec binds in fexpr) arg ---> let rec binds in fexpr arg 
   
   (e; fexpr) arg ---> e; fexpr arg 
   
   (match e with pat1 -> func1 | pat2 -> func2) args --> (match e with pat1 -> func1 args | pat2 -> func2 args)
   ```
   
   These can be nested.  The lifting of `match` involves duplicating 'args' and is limited to matches with two cases.
   
   This applies to both function and F#-defined delegate invocations.
   
   
