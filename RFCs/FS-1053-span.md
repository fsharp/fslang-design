# F# RFC FS-1053 - Add support for `Span`

.NET has a new feature `Span`. This RFC adds support for this in F#.

* [x] Approved in principle
* Discussion: https://github.com/fsharp/fslang-design/issues/287
* Implementation: [In progress](https://github.com/Microsoft/visualfsharp/pull/4888)

# Summary
[summary]: #summary

.NET has a new feature `Span`. This RFC adds support for this in F#.

Span is actually built from other features, covered by these RFCs
* https://github.com/fsharp/fslang-design/blob/master/RFCs/FS-1051-readonly-struct-attribute.md
* https://github.com/fsharp/fslang-design/blob/master/RFCs/FS-1052-isbyreflike-on-structs.md

This RFC will cover any extra work needed to get realistic support for `Span`.

# Motivation
[motivation]: #motivation

Parity with .NET/C#, better codegen, performance, interop.

# Detailed design
[design]: #detailed-design

There are several elements to the design.

#### Add the `voidptr` type to represent `void*`

Addition:
```
    /// <summary>Represents an untyped unmanaged pointer in F# code.</summary>
    ///
    /// <remarks>This type should only be used when writing F# code that interoperates
    /// with native code.  Use of this type in F# code may result in
    /// unverifiable code being generated.  Conversions to and from the 
    /// <c>nativeint</c> type may be required. Values of this type can be generated
    /// by the functions in the <c>NativeInterop.NativePtr</c> module.</remarks>
    type voidptr = (# "void*" #)
```

#### Add `NativePtr.toVoidPtr` and `NativePtr.ofVoidPtr`

Signature file addition to `NativePtr`:
```fsharp
        [<Unverifiable; CompiledName("ToVoidPtrInlined")>]
        /// <summary>Returns a typed native pointer for a given machine address.</summary>
        /// <param name="address">The pointer address.</param>
        /// <returns>A typed pointer.</returns>
        val inline toVoidPtr : address:nativeptr<'T> -> voidptr

        [<Unverifiable; CompiledName("OfVoidPtrInlined")>]
        /// <summary>Returns a typed native pointer for a untyped native pointer.</summary>
        /// <param name="address">The untyped pointer.</param>
        /// <returns>A typed pointer.</returns>
        val inline ofVoidPtr : voidptr -> nativeptr<'T>

```
And implementation:
```fsharp
    [<CompiledName("ToVoidPtrInlined")>]
    let inline toVoidPtr (address: nativeptr<'T>) = (# "" address : voidptr #)

    [<CompiledName("OfVoidPtrInlined")>]
    let inline ofVoidPtr (address: voidptr) = (# "" address : nativeptr<'T> #)

```


#### Adjust return byrefs so that they implicitly dereference when a call such as `span.[i]` is used, unless you write `&span.[i]`

This allows the byref return of `get_Item` to be implicitly dereferenced:

```fsharp
        let SafeSum(bytes: Span<byte>) =
            let mutable sum = 0
            for i in 0 .. bytes.Length - 1 do 
                sum <- sum + int bytes.[i]
            sum
```

#### Support for `ref this` extension members, e.g.

Example of normal extension members:
```fsharp
    [<Extension>]
    type Ext = 
        [<Extension>]
        static member ExtDateTime(dt: DateTime, x:int) = dt.AddDays(double x)
    
    module UseExt = 
        let dt = DateTime.Now.ExtDateTime(3)
```

Example of "ref this" extension members (assuming no special syntax is added):
```fsharp
    [<Extension>]
    type Ext = 
        [<Extension>]
        static member ExtDateTime2([<In; IsReadOnly>] dt: byref<DateTime>, x:int) = dt.AddDays(double x)
    
    module UseExt = 
        let dt2 = DateTime.Now.ExtDateTime2(3) // this doesn't compile in F# 4.1, we add this
```

# Drawbacks
[drawbacks]: #drawbacks

TBD

# Alternatives
[alternatives]: #alternatives

TBD

# Compatibility
[compatibility]: #compatibility

TBD

# Unresolved questions
[unresolved]: #unresolved-questions

* An implementation needs to be done, it will flush out the issues
