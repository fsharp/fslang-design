# F# RFC FS-1109 - Additional intrinsics for the NativePtr module

The design suggestion [Additional intrinsics for the NativePtr module](https://github.com/fsharp/fslang-suggestions/issues/200) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/200)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/11682)
- [x] ~~Design Review Meeting(s) with @dsyme and others invitees~~
- [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

# Summary

The NativePtr module is to be extended with new values and functions that enable additional IL instructions to be taken advantage of.

# Motivation

When interoperating with native code, it would be handy if the NativePtr module included some additional "intrinsic" functions for taking advantage of low-level IL instructions; specifically, I'd like to be able to use 'cpblk', 'initblk', 'initobj', and 'copyobj'.
It would also be nice to have an easy way of checking for null pointer values.
Moreover, with [C# 7.3's generic pointers](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-7.3/blittable), it makes sense for F# to provide a way to interoperate with C# correctly with generic pointers, which are represented by `ilsigptr` which are not usable like `nativeptr` would be. Therefore, conversions between these two types should be added as well.

# Detailed design

The following is to be added to the NativePtr module:
```fs
[<GeneralizableValue>]
[<NoDynamicInvocation>]
[<CompiledName("NullPointer")>]
let inline nullPtr<'T when 'T : unmanaged> : nativeptr<'T> = (# "ldnull" : nativeptr<'T> #)

[<NoDynamicInvocation>]
[<CompiledName("IsNullPointer")>]
let inline isNullPtr<'T when 'T : unmanaged> (address : nativeptr<'T>) = (# "ceq" nullPtr<'T> address : bool #)

[<Unverifiable>]
[<NoDynamicInvocation>]
[<CompiledName("InitializeBlockInlined")>]
let inline initBlock (address : nativeptr<'T>) (value : byte) (size : uint32) = (# "initblk" address value size #)

[<Unverifiable>]
[<NoDynamicInvocation>]
[<CompiledName("ClearPointerInlined")>]
let inline clear (address : nativeptr<'T>) = (# "initobj !0" type('T) address #)

[<Unverifiable>]
[<NoDynamicInvocation>]
[<CompiledName("CopyPointerInlined")>]
let inline copy (destination : nativeptr<'T>) (source : nativeptr<'T>) = (# "copyobj !0" type('T) destination source #)

[<Unverifiable>]
[<NoDynamicInvocation>]
[<CompiledName("CopyBlockInlined")>]
let inline copyBlock (destination : nativeptr<'T>) (source : nativeptr<'T>) (count : int) = (# "cpblk" destination source (count * sizeof<'T>) #)

[<Unverifiable>]
[<CompiledName("OfILSigPtrInlined")>]
let inline ofILSigPtr (address: ilsigptr<'T>) = (# "" address : nativeptr<'T> #)

[<Unverifiable>]
[<NoDynamicInvocation>]
[<CompiledName("ToILSigPtrInlined")>]
let inline toILSigPtr (address: nativeptr<'T>) = (# "" address : ilsigptr<'T> #)

```

# Drawbacks

This adds more ways to write unsafe code in F#. However, to achieve high-performance code and interoperate with native code effectively, it is important to add these extensions.

# Alternatives

Also adding `fromByRef` for `NativePtr` for consistency with `toByRef`. However, [as @dsyme said](https://github.com/fsharp/fslang-suggestions/issues/200#issuecomment-875143754),

> > The .NET Runtime has no problem supporting conversion from byref to voidptr: https://github.com/dotnet/runtime/blob/main/src/libraries/System.Runtime.CompilerServices.Unsafe/ref/System.Runtime.CompilerServices.Unsafe.cs
> 
> The problem is that this is very, very unsafe, and the right way is to pin. I don't think we should allow managed pointer --> native pointer in FSharp.Core, especially if underlying .NET Core methods are available anyway.

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change? No.
* What happens when previous versions of the F# compiler encounter this design addition as source code? Works as usual.
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries? Works as usual.
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct? Works as usual.

# Unresolved questions

None.
