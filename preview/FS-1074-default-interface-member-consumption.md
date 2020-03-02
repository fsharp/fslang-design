# F# RFC FS-1074 - Default Interface Member Consumption

The design suggestion [Default interface member interop](https://github.com/fsharp/fslang-suggestions/issues/679) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

# Summary

.NET Core 3.0 will be adding a new runtime concept: default implementations for interface members. We extend the interface implementation mechanism of F# to allow for consuming default implementations. Production of default implementations is not supported.

Given the following C#:

```csharp
namespace Dims
{
    public interface IHaveADefaultMember
    {
        int GetNumber() => 12;
    }
}
```

It is possible to consume the default member in F#:

```fsharp
open Dims

type C() =
    interface IHaveADefaultMember

let i = C() :> IHaveADefaultMember
printfn "%d" (i.GetNumber()) // 12
```

# Motivation

Because this is a new, fundamental concept added to .NET, F# must understand it.

# Detailed design

## Interface Implementation

Interface implementation is extended to not require explicit implementation of members that have a default implementation. That is, the following C# code:

```csharp
namespace Dims
{
    public interface IHaveADefaultMember
    {
        int GetNumber() => 12;
    }
}
```

Already has an implementation for the `GetNumber` member. So when a type is specified as implementing `IHaveADefaultMember`, there is no need to specify an implementation of `GetNumber` in the corresponding F# code.

For F# classes and structs, only `interface IHaveADefaultMember` is required:

```fsharp
open Dims

type C() =
    interface IHaveADefaultMember

let i = C() :> IHaveADefaultMember
printfn "%d" (i.GetNumber()) // 12
```

For F# object expressions, only `new IHaveADefaultMember` is required:

```fsharp
open Dims

let i' = { new IHaveADefaultMember }
printfn "%d" (i'.GetNumber()) // 12
```

If the interface being implemented has members that do not have a default implementation, it is still required to implement them in F# code. Only the members with a default implementation do not require implementing in F# code.

## Overriding a Default Implementation

It is possible to override a default implementation when implementing an interface:

```fsharp
open Dims

type C() =
    interface IHaveADefaultMember with
        member __.GetNumber() = 12

let i = C() :> IHaveADefaultMember
printfn "%d" (i.GetNumber()) // 12

let i' =
    { new IHaveADefaultMember
        member __.GetNumber() = 13 }
printfn "%d" (i'.GetNumber()) // 13
```

Overriding a default implementation in an interface is not enabled though as that would be an entirely separate feature.

## Explicit Interface Implementation

The need for an explicit interface implementation will be aware of default interface implementations.

For example:

```csharp
public interface IA
{
    void M()
    {
        // default implementation
    }
}

public interface IB : IA
{
    void MB();     
}

public interface IC : IA
{
    void MC();
}
```

```fsharp
type Test () =

    interface IB with

        member __.MB() = ()

    interface IC with

        member __.MC() = ()
```

The user does not need to be explicit with `IA` because all the slots for `IA` have a default implementation. `IA.M` has a most specific implementation, which is `IA`; there is no ambiguity here.

## Most Specific Implementation Error

Having default implementations for interface members will result in the diamond problem:

```csharp
namespace CSharpTest
{
    public interface IA
    {
        void M();
    }

    public interface IB : IA
    {
        void IA.M()
        {
        }
    }

    public interface IC : IA
    {
        void IA.M()
        {
        }
    }
}
```

```fsharp
module FSharpTest

open CSharpTest

type Test () =

    interface IB
    interface IC
```

When implementing `IB` and `IC` this way and because they both implement `IA.M`, the member is considered ambiguous and an error will be thrown stating that `IA.M` does not have a most specific implementation.

# Drawbacks

This is an inheritance-based feature. Since F# code tends towards expressions and function composition over inheritance, this is of minimal value to most F# programmers. It also adds another vector by which someone could write inheritance-oriented F# code. However, it has been determined that the following make it worth doing this interop work:

1. By being a .NET language, F# should generally understand .NET concepts. And DIMs are a .NET concept.
1. F# language defaults generally lead people away from inheritance anyways, so it is unlikely that this will result in a huge influx of inheritance-oriented F# code.

# Alternatives

The impact of not doing this means two things:

1. Not understanding a .NET concept, despite being a .NET language. Inevitably someone will need this, and not having it available will be painful.
2. Not having it means that any future .NET component that requires its use will be incapable of being used by F# programmers.

The only alternative aside from not doing this work is to also allow the _production_ of default implementations for interface members. This would give F# full "feature parity" with the .NET concept, and align with C# 8.0. The downside to not also allow production means that it will remain a second-class concept for F# programmers.

# Compatibility

* Is this a breaking change?

No.

* What happens when previous versions of the F# compiler encounter this design addition as source code?

Previous compilers will not know how to implement interfaces with default implementations.

* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?

Nothing special; same as always.

* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?

N/A.
