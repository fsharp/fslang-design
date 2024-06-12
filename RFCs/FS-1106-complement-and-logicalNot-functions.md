# F# RFC FS-1106 - "complement" and "logicalNot" functions

The design suggestion [Add "complement" and "logicalNot" operators to resolve the confusion with "~~~"](https://github.com/fsharp/fslang-suggestions/issues/472) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/472)
- [x] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [ ] Design Review Meeting(s) with @dsyme and others invitees
- [Discussion](https://github.com/fsharp/fslang-design/discussions/599)

# Summary

https://github.com/fsharp/fslang-suggestions/issues/472#issue-184124658
> When used on user-defined types, the ~~~ operator resolves to op_LogicalNot rather than the expected op_OnesComplement. 
> See https://github.com/dotnet/fsharp/issues/457#issuecomment-104900399 for a workaround.
> This should be fixed FSharp.Core. An attempt to fix this transparently failed, see dotnet/fsharp#458.
> A plan to address this going forward is at https://github.com/dotnet/fsharp/issues/457#issuecomment-104900399 but will require an update to FSharp.Core.

# Motivation

https://github.com/dotnet/fsharp/issues/457
> (~~~) doesn't work on bigints for some reason, but System.Numerics.BigInteger has op_OnesComplement, which does what we want.
> In fact, op_OnesComplement is the correct name for this operator--it seems F# mixed up op_OnesComplement and op_LogicalNot. See also I.10.3.1, "Unary operators", in the CLI standard.

# Detailed design

https://github.com/dotnet/fsharp/pull/458#issuecomment-127711336
> 1. add new complement and logicalNot operators that work as intended. See also this workaround
> 2. add a compiler warning when ~~~ statically resolves to a non-primitive op_LogicalNot operator, or doesn't resolve statically at all (in let inline ... code). The warning would direct the user to use complement or logicalNot explicitly instead.
> 3. in some far off future revision of F# change ~~~ to resolve to op_OnesComplement and consider adding the symbolic !!! operator for op_LogicalNot.

# Drawbacks

It's a breaking change in the far off future.

# Alternatives

- Not doing anything - continue having incorrect resolution of (~~~).
Forcing the use of https://github.com/dotnet/fsharp/issues/457#issuecomment-104900399:
```fs
module Impl = 
  type CBits = 
    static member inline complement (x : int8) = ~~~x
    static member inline complement (x : uint8) = ~~~x
    static member inline complement (x : int16) = ~~~x
    static member inline complement (x : uint16) = ~~~x
    static member inline complement (x : int32) = ~~~x
    static member inline complement (x : uint32) = ~~~x
    static member inline complement (x : int64) = ~~~x
    static member inline complement (x : uint64) = ~~~x
    static member inline complement (x : bigint) = bigint.op_OnesComplement x

  let inline instance< ^a, ^b when
      (^a or ^b) :    (static member complement : ^b -> ^b)>
      (x : ^b) =
    ((^a or ^b) : (static member complement : ^b -> ^b) x)

let inline complement num = Impl.instance<CBits, _> num
```

- Fixing everything in one go (https://github.com/dotnet/fsharp/pull/458), which causes runtime errors when different versions of FSharp.Core and F# compiler collide.

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change? Yes, in a far off future
* What happens when previous versions of the F# compiler encounter this design addition as source code? This is not a syntax change.
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries? No warnings, no errors. (~~~) resolves correctly.
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct? These functions resolve as usual.

# Unresolved questions

Should definitions of `(~~~)` continue emitting `op_LogicalNot`? Or deprecate this operator entirely for the moment?
