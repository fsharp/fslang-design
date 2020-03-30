# FS 1082 - `uint` Type Abbreviation in FSharp.Core

The design suggestion [Add `uint` type abbreviation to FSharp.Core](https://github.com/fsharp/fslang-suggestions/issues/818) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

# Summary

Add the `uint` type abbreviation and casting function to FSharp.Core to align with the `int` type abbreviation and casting function.

# Motivation

The following three type abbreviations exist in FSharp.Core today:

* `int32`
* `int`
* `uint32`

The `int` abbreviation is equivalent to `int32`. However, there is no corresponding `uint` abbreviation. This feels like an omission more than a deliberate decision. In fact, the F# compiler already assumes that the abbreviation exists in [formatting](https://github.com/dotnet/fsharp/blob/master/src/utils/sformat.fs#L147) and [an error message](https://github.com/dotnet/fsharp/blob/master/src/fsharp/FSComp.txt#L790). Because of this, the type abbreviation should be added.

Additionally, there is an `int` casting function, but not `uint` equivalent.

# Detailed design

The addition is trivial:

```diff
type uint = uint32
```

This would allow F# programmers to write code like this:

```fsharp
let f (x: uint) = ()
```

Formatting with `%u` should also work because this is an abbreviation for the same type as `uint32`.

This also includes a `uint` function in FSharp.Core, allowing you to cast to `uint` instead of needing to write `uint32`:

```fsharp
let f x: uint = uint x
```

Its signature is as follows:

```fsharp
val inline uint: value:^T -> uint when ^T: (static member op_Explicit: ^T -> uint) and default ^T: uint
```

Additionally, a casting function in the nullable value type operators is added:

```fsharp
val inline uint: value: Nullable< ^T > -> Nullable<uint> when ^T: (static member op_Explicit: ^T -> uint) and default ^T: uint
```

# Drawbacks

`uint` looks like `unit`, which could confuse some people, though this would mostly be when type signatures are concerned like:

```fsharp
val f: x:uint -> unit
```

# Alternatives

Do nothing.

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change?

No.

* What happens when previous versions of the F# compiler encounter this design addition as source code?

They fail to compile, unless that type abbreviation is already defined in their source code.

* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?

They continue working.

* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?

They continue working.

# Unresolved questions

None.
