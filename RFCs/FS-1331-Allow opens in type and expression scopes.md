# F# RFC FS-1331 - Allow opens in type and expression scopes

The design suggestion [Allow the use of open at type and expression scopes](https://github.com/fsharp/fslang-suggestions/issues/96) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/96)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/18814)
- [x] [Discussion](https://github.com/fsharp/fslang-design/discussions/812)

# Summary

This RFC proposes to allow the use of `open` at type and expression scopes. Type/expression-scoped `open`s will work as same as module-scoped `open`, that is, modules or types with `[<RequireQualifiedAccess>]` cannot be opened.

# Motivation

By allowing this, we can 
1. Picking a certain set of operator overloads in some tight context like `Checked`.
2. Making the `open`s span less than the whole module/namespace.

# Detailed design

1. Expression-scoped `open` is an expression that opens a module in the body expression scope, and its type is the body's type.

    ```fsharp
    ((open System
      Int32.MaxValue + 1   // The body expression
    ): int)
    ```

    It can be used in any expression.

    ```fsharp
    let test () =
        open global.System
        printfn "%d" (Int32.MaxValue + 1)

        open type System.Int32
        open Checked
        printfn "%d" (MaxValue + 1)

    // In `match`
    match Some 1 with
    | Some 1 when open System; Int32.MinValue < 0 -> 
      open type System.Console
      WriteLine "Is 1"
    | _ -> ()

    // In `for`
    for _ in open System.Linq; Enumerable.Range(0, 10) do
      open type System.Console
      WriteLine "Hello, World!"

    // In `while`
    while
      (open type System.Int32
       MaxValue < 0) do
      open type System.Console
      WriteLine "MaxValue is negative"

    // In `if`
    if (open type System.Int32; MaxValue <> MinValue) then
      open type System.Console
      WriteLine "MaxValue is not equal to MinValue"
    elif (open type System.Int32; MaxValue < 0) then
      open type System.Console
      WriteLine "MaxValue is negative"
    else
      open type System.Console
      WriteLine "MaxValue is positive"

    // In `try`
    try
      open type System.Int32
      open Checked
      MaxValue + 1
    with | exn -> open type System.Console; WriteLine exn.Message; 0

    // In lambdas
    let f = fun x -> open System; x + 1
    let f2 = function x -> open type System.Int32; x + MinValue

    // In computation expressions
    let res = async {
        open System
        Console.WriteLine("Hello, World!")
        let! x = Async.Sleep 1000
        return x
    }

    // In type and member's definitions 
    type C() =
        do 
            open System
            printfn "%d" Int32.MaxValue
        member _.M() = open type Int32; MaxValue
    ```

    It cannot be used in a pattern.

    ```fsharp
    match Some 1 with
    | Some (open System; Int32.MaxValue) -> ()  // <- Error

    module M =
      let (|Id|) x = x

    let (open M; Id x) = 1  // <- Error
    ```

2. Type-scoped `open` is a statement that opens a module in the type's following scope. It can be used any type definitions, type expressions and the `with` section of a record/union/exception type. 

    Due to the complexity of implementing this feature, the type-scoped `open` can only be placed at the beginning of the type definition.

    This feature is only available in implementation files.

```fsharp
type C() =
    open System
    inherit Object()

    let maxValue = Int32.MaxValue
    [<DefaultValue>] val mutable minValue: Int32
    
    do printfn "%d" Int32.MaxValue
    member _.M() = open type Int32; MaxValue
    
    open System.Collections.Generic    // <- Error, must be at the beginning of the type definition
    member _.M3() = List<int>()

    interface IDisposable with
        member this.Dispose (): unit = raise (NotImplementedException())
 
[<Struct>]
type ABC =
    open System
    val a: Int32
    val b: Int32
    new (a) = { a = Int32.MaxValue; b = 0 }

type System.Int32 with
    open type System.Math
    member this.Abs111 = Abs(this)

type A = A of int
    with
        open System
// ....
```

# Drawbacks

The expression/type-scoped `open` cannot have a `type` keyword not on the same line, which is different from module-scoped `open`.

```fsharp
open
  type
    System.Console    // this is ok

(open
  type
    System.Console)   // this is not ok

(open type
    System.Console)   // this is ok
```

# Alternatives

The original suggestion mentioned 3 ways to handle expression/type-scoped `open` + `[<RequireQualifiedAccess>]` attribute:

1. Do nothing, RQA is still respected, and this specific use case is something we just accept as not accomplishable
2. Have type- and function-scoped open declarations bypass RQA
3. Introduce a "mostly RQA" attribute that allows you to open them only in the type and function scope

This RFC is based on the first option.

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change?
> No.

* What happens when previous versions of the F# compiler encounter this design addition as source code?
> It will fail to compile, as `open` is not allowed in type and expression scopes in previous versions.

* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
> It should work fine, as `open`s are code-level constructs.

# Pragmatics

## Diagnostics

1. 'open' declarations must come before all other definitions in type definitions or augmentation

## Tooling

Please list the reasonable expectations for tooling for this feature, including any of these:

* Debugging
  * Breakpoints/stepping
    Make sure that cannot set breakpoints in the `open` statement
* Error recovery (wrong, incomplete code)
* Auto-complete
    Check if the completion list after the expression/type-scoped `open` is same as the module-scoped `open`.
* Code fixes
  * `AddOpenCodeFixProvider`
    It should works like before, that is, append the missing `open` to the nearest module/namespace-scoped `open`s.

  * `ConvertCSharpUsingToFSharpOpen`
    It should works like before.

  * `RemoveUnusedOpens`
    It should be able to recognize `open`s on expression/type level and remove unused items.
