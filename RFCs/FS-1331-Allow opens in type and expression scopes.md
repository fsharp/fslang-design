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

let test () =
    open global.System
    printfn "%d" (Int32.MaxValue + 1)

    open type System.Int32
    open Checked
    printfn "%d" (MaxValue + 1)
```

2. Type-scoped `open` is a statement that opens a module in the type's following scope. It can be used any type definitions, type expressions and the `with` section of a record/union/exception type.


```fsharp
type C() =
    do printfn "%d" Int32.MaxValue   // <- Cannot find the `Int32` here
    open System
    do printfn "%d" Int32.MaxValue
    member _.M() = open type Int32; MaxValue


type System.Int32 with
    open type System.Math
    member this.Abs111 = Abs(this)


type A = A of int
    with
        open System
// ....
```

# Drawbacks

TODO

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

## Tooling

Please list the reasonable expectations for tooling for this feature, including any of these:

* Debugging
  * Breakpoints/stepping
    make sure that cannot set breakpoints in the `open` statement
* Error recovery (wrong, incomplete code)
* Auto-complete
