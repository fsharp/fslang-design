# F# RFC FS-1134 - Try-with in sequence expressions

The design suggestion [try/with in seq expressions](https://github.com/fsharp/fslang-suggestions/issues/1027) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1027)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/14540)

# Summary

Until now, using try-with within a seq{} expression resulted into a compiler error.
This PR adds the possibility to use it.

# Motivation

Motivation is to remove restriction, and reduce surprises for users.

# Detailed design

This RFC adds support for TryWith within sequence expressions.
This affects seq{} creation, [] list creation and [||] array creation as well.

The design motto is to get as close as possible to the semantics of  regular code (outside sequence expressions).
Following are the main design decisions:
- Any code possible in seq{} is allowed inside the 'try' block of try/with
- Exception handlers can also include code which is normally allowed in seq{}, namely:
   - Only a side-effect (e.g. logging) and return unit
   - yield a single element (semantics of implicit vs. explicit yield same as in regular seq{})
   - yield! inner sequence
   - Call another sequence generator of the same type, incl. a recursive call to itself
- Each exception handler can independently either produce some resulting values, or not do anything and just return unit
- Disposal of the inner 'try' is taken care of before 'with' handlers are invoked
- If the disposal within inner 'try' fails with an exception, this exception takes precedence over original handlers for the 'with' handlers (matches what e.g. task{} or async{} do)
- With handlers can create their own disposal scope 
- The conditions/guards for with clauses are executed twice
   - This exists also in other places of exception handling, and is only a problem if the exception guard produces a side effect (see examples)


## Examples

### Happy-path example of selected language elements composed into try/with

```fsharp
let rec mySeq inputEnumerable =
    seq {
        for x in inputEnumerable do       
            try
                match x with                                     
                | 0 -> yield 1                                      // - Single value
                | 1 -> yield! (mySeq [0;3;6])                       // - Recursion
                | 2 -> ()                                           // - Empty
                | 3 -> failwith "This should get caught!"           // - Generic exn throw
                | 4 -> yield (4/0)                                  // - Specific exn throw
                | 5 ->                                            
                    yield 5                                         // - Two yields, will be a state machine
                    yield 5
                | _ -> failwith "This should get caught!"
            with                                               
                | :? System.DivideByZeroException -> yield 4          // - Specific exn
                | anyOther when x = 3 -> yield 3                     // - Generic exn using 'x', no yield
                | anyOther when x = 6 -> ()                          // - Empty yield from 'with' clause
    }
 
if (mySeq [0..5] |> Seq.sum) <> (1+(1+3)+3+4+5+5) then
    failwith $"Sum was {(mySeq [0..5] |> Seq.sum)} instead"
```

### Order of execution when try-finally inside an try-with

```fsharp
let mutable l = []
let s() = seq {
    try
        try
            l <- "Before try" :: l
            yield (1/0)
            l <- "After crash should never happen" :: l
        finally
            l <- "Inside finally" :: l
    with ex when (l <- "Inside with pattern" :: l;true) ->
        l <- "Inside with body" :: l
        yield 1
        l <- "End of with body" :: l
}
l <- "Before sum" :: l
let totalSum = s() |> Seq.sum
l <- "After sum" :: l
if totalSum <> 1 then
    failwith $"Sum was {{totalSum}} instead"

l <- List.rev l
let expectedList = 
    [ "Before sum"   // Seq is lazy, so we do not expect anything until iteration starts
      "Before try"
      "Inside finally"
      "Inside with pattern"
      "Inside with pattern"   // Yes indeed, the exn matching pattern is executed twice
      "Inside with body"
      "End of with body"
      "After sum"]

```


# Drawbacks

Main drawback is the lack of lowering into a state machine, and only existing via a combinator call.
The main reason is the existing separation between .MoveNext() and .Dispose() calls for the optimized code path. 
Since try-with can also produce new values from the with clause, it means that inner disposals would have to be delayed and result in an unexpected order of executions.

# Alternatives

Alternative is not doing this at all.

# Compatibility

Please address all necessary compatibility questions:

This is not a breaking change - the codepath is guarded by a language version switch.
Previous version of the compiler would still see such code as an error.
For compiled code, the combinator has to be present in Fsharp.Core. If it is, code can be executed.

When previous versions of F# compiler see the combinator in Fsharp.Core, they will not do anything with it.


# Pragmatics

## Diagnostics

A big misuse can be relying on throwing and catching exceptions for flow control.
This compiles and runs fine, but the performance of constant throwing+catching exceptions is orders of maginute lower compared to regular code.

```fsharp
let rec f () = seq {
    try
        yield 1
        yield (1/0)
    with pat ->
        yield! f()
}
let topNsum = f() |> Seq.take 100_000 |> Seq.sum

```

## Tooling

Tooling should follow existing expectations for regular code written outside of sequence expressions.
All standard features like Intellisense, stepping and debugging (both the 'try' as well as 'with' part) must be debuggable.

## Performance

Please list any notable concerns for impact on the performance of compilation and/or generated code

*  Existing code is not affected at all
* Code wrapped in try-with will see a performance degradation due to lack of lowering into a state machine
* Code overusing throwing exceptions and catching them for flow control will see big degradation 
    * This stays in line with dotnet guidelines of using Exceptions for exceptional cases, and not for flow control.

## Scaling

The compilation speed scales with number of TryWith expressions within sequence expressions.

## Culture-aware formatting/parsing

N/A

# Unresolved questions

N/A