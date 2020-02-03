# F# RFC FS-1043 - Extension members should be available to solve operator trait constraints

These design suggestions:
* https://github.com/fsharp/fslang-suggestions/issues/230
* https://github.com/fsharp/fslang-suggestions/issues/29

have been marked "approved in principle". This RFC covers the detailed proposal for these

* [x] Approved in principle
* [ ] Implementation: [In progress](https://github.com/dotnet/fsharp/pull/6805)


# Summary
[summary]: #summary

Extension methods are previously ignored by SRTP constraint resolution.  This RFC means they are taken into account.

For example, consider
```fsharp

type System.String with
    static member ( * ) (foo, n: int) = String.replicate n foo

let r4 = "r" * 4
let spaces n = " " * n
```
Prior to this RFC the result is:
```
foo.fs(2,21): warning FS1215: Extension members cannot provide operator overloads.  Consider defining the operator as part of the type definition instead.
foo.fs(4,16): error FS0001: The type 'int' does not match the type 'string'
```

# Motivation
[motivation]: #motivation

It is reasonable to use extension methods to retrofit operators and other semantics on to existing types. This "completes the picture" as extension methods in a natural way.


# Detailed design
[design]: #detailed-design

The proposed change is as follows, in the internal logic of the constraint solving process:

1. During constraint solving, the record of each SRTP constraint incorporates the relevant extension methods in-scope at the point the SRTP constraint is asserted. That is, at the point a generic construct is used and "freshened".  The accessibility domain (i.e. the information indicating accessible methods) is also noted as part of the constraint.  Both of these pieces of information are propagated as part of the constraint. We call these the *trait possible extension solutions* and the *trait accessor domain*

2. When checking whether one unsolved SRTP constraint A *implies* another B (note: this a process used to avoid asserting duplicate constraints when propagating a constraint from one type parameter to another - see `implies` in `ConstraintSolver.fs`), both the possible extension solutions and the accessor domain of A are ignored, and those of the existing asserted constraint are preferred.

3. When checking whether one unsolved SRTP constraint is *consistent* with another (note: this is a process used to check for inconsistency errors amongst a set of constraints - see `consistent` in `ConstraintSolver.fs`), the possible extension solutions and accessor domain are ignored.

4. When attempting to solve the constraint via overload resolution, the possible extension solutions which are accessible from the trait accessor domain are taken into account.  

5. Built-in constraint solutions for things like `op_Addition` constraints are applied if and when the relevant types match precisely, and are applied even if some extension methods of that name are available.

# Examples

## Widening to specific type

**NOTE: this is an example of what is allowed by this RFC, but is not recommended for standard F# coding. In particular error messages may degrade for existing code.**

By default `1 + 2.0` doesn't check in F#.  By using extension members on addition you can make this check:
```fsharp

type System.Int32 with
    static member inline widen_to_int64 (a: int32) : int64 = int64 a
    static member inline widen_to_single (a: int32) : single = single a
    static member inline widen_to_double (a: int32) : double = double a

type System.Single with
    static member inline widen_to_double (a: int) : double = double a

let inline widen_to_int64 (x: ^T) : int64 = (^T : (static member widen_to_int64 : ^T -> int64) (x))
let inline widen_to_single (x: ^T) : single = (^T : (static member widen_to_single : ^T -> single) (x))
let inline widen_to_double (x: ^T) : double = (^T : (static member widen_to_double : ^T -> double) (x))

type System.Int64 with
    static member inline (+)(a: int64, b: 'T) : int64 = a + widen_to_int64 b
    static member inline (+)(a: 'T, b: int64) : int64 = widen_to_int64 a + b

type System.Single with
    static member inline (+)(a: single, b: 'T) : single = a + widen_to_single b
    static member inline (+)(a: 'T, b: single) : single = widen_to_single a + b

type System.Double with
    static member inline (+)(a: double, b: 'T) : double = a + widen_to_double b
    static member inline (+)(a: 'T, b: double) : double = widen_to_double a + b

let examples() =

    (1 + 2L)  |> ignore<int64>
    (1 + 2.0f)  |> ignore<single>
    (1 + 2.0)  |> ignore<double>

    (1L + 2)  |> ignore<int64>
    (1L + 2.0)  |> ignore<double>
```

## Defining safe conversion

**NOTE: this is an example of what is allowed by this RFC, but is not recommended for standard F# coding. In particular error messages may degrade for existing code.**

By default there is no function which captures the notion of safe `op_Implicit` implicit conversion in F#.
You can define one like this:
```
let inline safeConv (x: ^T) : ^U = ((^T or ^U) : (static member op_Implicit : ^T -> ^U) (x))
```
With this RFC you can then populate this with instances for existing primitive types:
```fsharp
type System.SByte with
    static member inline op_Implicit (a: sbyte) : int16 = int16 a
    static member inline op_Implicit (a: sbyte) : int32 = int32 a
    static member inline op_Implicit (a: sbyte) : int64 = int64 a
    static member inline op_Implicit (a: sbyte) : nativeint = nativeint a
    static member inline op_Implicit (a: sbyte) : single = single a
    static member inline op_Implicit (a: sbyte) : double = double a

type System.Byte with
    static member inline op_Implicit (a: byte) : int16 = int16 a
    static member inline op_Implicit (a: byte) : uint16 = uint16 a
    static member inline op_Implicit (a: byte) : int32 = int32 a
    static member inline op_Implicit (a: byte) : uint32 = uint32 a
    static member inline op_Implicit (a: byte) : int64 = int64 a
    static member inline op_Implicit (a: byte) : uint64 = uint64 a
    static member inline op_Implicit (a: byte) : nativeint = nativeint a
    static member inline op_Implicit (a: byte) : unativeint = unativeint a
    static member inline op_Implicit (a: byte) : single = single a
    static member inline op_Implicit (a: byte) : double = double a

type System.Int16 with
    static member inline op_Implicit (a: int16) : int32 = int32 a
    static member inline op_Implicit (a: int16) : int64 = int64 a
    static member inline op_Implicit (a: int16) : nativeint = nativeint a
    static member inline op_Implicit (a: int16) : single = single a
    static member inline op_Implicit (a: int16) : double = double a

type System.UInt16 with
    static member inline op_Implicit (a: uint16) : int32 = int32 a
    static member inline op_Implicit (a: uint16) : uint32 = uint32 a
    static member inline op_Implicit (a: uint16) : int64 = int64 a
    static member inline op_Implicit (a: uint16) : uint64 = uint64 a
    static member inline op_Implicit (a: uint16) : nativeint = nativeint a
    static member inline op_Implicit (a: uint16) : unativeint = unativeint a
    static member inline op_Implicit (a: uint16) : single = single a
    static member inline op_Implicit (a: uint16) : double = double a

type System.Int32 with
    static member inline op_Implicit (a: int32) : int64 = int64 a
    static member inline op_Implicit (a: int32) : nativeint = nativeint a
    static member inline op_Implicit (a: int32) : single = single a
    static member inline op_Implicit (a: int32) : double = double a

type System.UInt32 with
    static member inline op_Implicit (a: uint32) : int64 = int64 a
    static member inline op_Implicit (a: uint32) : uint64 = uint64 a
    static member inline op_Implicit (a: uint32) : unativeint = unativeint a
    static member inline op_Implicit (a: uint32) : single = single a
    static member inline op_Implicit (a: uint32) : double = double a

type System.Int64 with
    static member inline op_Implicit (a: int64) : double = double a

type System.UInt64 with
    static member inline op_Implicit (a: uint64) : double = double a

type System.IntPtr with
    static member inline op_Implicit (a: nativeint) : int64 = int64 a
    static member inline op_Implicit (a: nativeint) : double = double a

type System.UIntPtr with
    static member inline op_Implicit (a: unativeint) : uint64 = uint64 a
    static member inline op_Implicit (a: unativeint) : double = double a

type System.Single with
    static member inline op_Implicit (a: int) : double = double a
```

# Drawbacks
[drawbacks]: #drawbacks

* This slightly strengthens the "type-class"-like capabilities of SRTP resolution. This means that people may increasingly use SRTP code as a way to write generic, reusable code rather than passing parameters explicitly.  While this is reasonable for generic arithmetic code, it has many downsides when applied to other things.

# Alternatives
[alternatives]: #alternatives

1. Don't do it


# Compatibility
[compatibility]: #compatibility

Status: We are trying to determine when/if this RFC is a breaking change.

We assume it must be a breaking change, because additional methods are taken into account in the overload resolution used in SRTP constraint resolution. That must surely cause it to fail where it would have succeeded before. However,

1. All the new methods are extension methods, which are lower priority in overload resolution

Even if it's theoretically a breaking change, we may still decide it's worthwhile because the risk of change is low.  This seems plausible because

1. Taking the extra existing extension methods into account is natural and a lot like an addition to the .NET libraries causing overload resolution to fail. We don't really consider that a breaking change (partly because this is measured differently for C# and F#, as they have different sensitivities to adding overloads).

2. For the built-in operators like `(+)`, there will be relatively few such candidate extension methods in F# code because we give warnings when users try to add extension methods for these

3. Nearly all SRTP constraints (at least the ones for built-in operators) are on static members, and C# code can't introduce extension members that are static - just instance ones. So C# extension members will only cause compat concern for F# code using SRTP constraints on instance members, AND where the C# extension methods make a difference to overload resolution.

Still, we're pretty sure this must be a breaking change. We would appreciate help construct test cases where it is/isn't.


# Unresolved questions
[unresolved]: #unresolved-questions

* [ ] Points 2 & 3 (`consistent` and `implies`) are subtle and I will attempt to expand the test cases where constraints flow together from different accessibility
domains to try to identify a case where this matters. However it's actually very hard and artificial to construct tests where this matters, because SRTP constraints are typically freshened
and solved within quite small scopes where the available methods and accessibility domain is always consistent.

