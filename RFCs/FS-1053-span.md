# F# RFC FS-1053 - Add support for `voidptr`, `IsReadOnly`, `ByRefLike`, "return byrefs", "byref this extension members" and `Span`

.NET has a new feature `Span`. This RFC adds support for this in F#.

* [x] Approved in principle
* [ ] Implementation: [In progress](https://github.com/Microsoft/visualfsharp/pull/4888)
* Discussion: https://github.com/fsharp/fslang-design/issues/287

# Summary
[summary]: #summary

Span is actually built from several features and a library:
* `voidptr` type
* `NativePtr.ofVoidPtr` and `NativePtr.toVoidPtr` functions
* `ByRefLike` structs
* `IsReadOnly` structs
* `IsReadOnly` parameters
* `IsReadOnly` locals
* `IsReadOnly` returns
* `byref this` extension members
*  implicit dereference of ref-returns

# Motivation
[motivation]: #motivation

Parity with .NET/C#, better codegen, performance, interop.

# Detailed design
[design]: #detailed-design

There are several elements to the design.

#### Add the `voidptr` type to represent `void*`

The `voidptr` type represents `void*` in F# code, i.e. an untyped unmanaged pointer.
This type should only be used when writing F# code that interoperates
with native code.  Use of this type in F# code may result in
unverifiable code being generated.  Conversions to and from the 
`nativeint` or other native pointer  type may be required.
Values of this type can be generated
by the functions in the `NativeInterop.NativePtr` module.

#### Add `NativePtr.toVoidPtr` and `NativePtr.ofVoidPtr`

We add these:
```fsharp
        val inline toVoidPtr : address:nativeptr<'T> -> voidptr
        val inline ofVoidPtr : voidptr -> nativeptr<'T>
```
With implementation:
```fsharp
    let inline toVoidPtr (address: nativeptr<'T>) = (# "" address : voidptr #)
    let inline ofVoidPtr (address: voidptr) = (# "" address : nativeptr<'T> #)
```

#### ByRefLike structs

"ByRefLike" structs are stack-bound types with rules like `byref<_>`.
They declare struct types that are never allocated on the heap. These are extremely
useful for high-performance programming (and also for correctness) as you get a set of strong checks
about the lifetimes of these values.

* [C# feature](https://blogs.msdn.microsoft.com/mazhou/2018/03/02/c-7-series-part-9-ref-structs/)

Checked imitations:
* [x] Can be used stack-only. i.e. method parameters and local variables
* [x] Cannot be static or instance members of a class or normal struct
* [x] Cannot be method parameter of async methods or lambda expressions
* [x] Cannot be dynamic binding, boxing, unboxing, wrapping or converting

Unchecked limitations:
* [ ] Represents a sequential struct layout (QUESTION: should we emit Sequential?)
* [ ] Can implement ToString but you can't call it
* [ ] They can not implement interfaces (QUESTION: should we check this)

Other things to note:
* [ ] Can make readonly byreflike structs

Here is an example of what declaring a byref struct looks like in F#:

```fsharp
open System.Runtime.CompilerServices

[<IsByRefLike>]
type S(count1: int, count2: int) = 
    member x.Count1 = count1
    member x.Count2 = count2
```

Separately, C# attaches the following Obsolete attribute to these types, and presumably has special code to ignore it. We add a special case in the compiler to ignore the `Obsolete` attribute on `ByRefLike` structs.

#### `IsReadOnly` on structs

F# structs are normally readonly, it is quite hard to write a mutable struct. Knowing a struct is readonly gives more efficient code and fewer warnings. 

* The C# 7.2 feature is described here: https://blogs.msdn.microsoft.com/mazhou/2017/11/21/c-7-series-part-6-read-only-structs/

Example code:

```fsharp
[<IsReadOnly>]
type S(count1: int, count2: int) = 
    member x.Count1 = count1
    member x.Count2 = count2
```

Notes:

* The key part of the F# compiler code is [here](https://github.com/Microsoft/visualfsharp/blob/16dd8f40fd79d46aa832c0a2417a9fd4dfc8327c/src/fsharp/TastOps.fs#L5582)

* A related issue is https://github.com/Microsoft/visualfsharp/pull/4576 which deals with oddities in the warnings about struct mutation.

Question: should we add the IsReadOnly attribute automatically when an F# struct is inferred to be readonly, or should the programmer need to make it explicit?

#### Implicit dereference of return byrefs

F# 4.1 added return byrefs. We adjust these so that they implicitly dereference when a call such as `span.[i]` is used, unless you write `&span.[i]`

This allows the byref return of `get_Item` to be implicitly dereferenced:

```fsharp
        let SafeSum(bytes: Span<byte>) =
            let mutable sum = 0
            for i in 0 .. bytes.Length - 1 do 
                sum <- sum + int bytes.[i]
            sum
```

#### Support for `byref this` extension members, e.g.

"byref this" extension methods allow extension methods to modify the struct that is passed in.

Example of normal extension members:
```fsharp
    [<Extension>]
    type Ext = 
        [<Extension>]
        static member ExtDateTime(dt: DateTime, x:int) = dt.AddDays(double x)
    
    module UseExt = 
        let dt = DateTime.Now.ExtDateTime(3)
```

Example of "ref this" extension members (assuming no special syntax is added, which seems reasonable):
```fsharp
    [<Extension>]
    type Ext = 
        [<Extension>]
        static member ExtDateTime2([<In; IsReadOnly>] dt: byref<DateTime>, x:int) = dt.AddDays(double x)
    
    module UseExt = 
        let dt2 = DateTime.Now.ExtDateTime2(3) // this doesn't compile in F# 4.1, we add this
```

# Examples of using `Span` and `Memory`

```fsharp
let SafeSum (bytes: Span<byte>) =
    let mutable sum = 0
    for i in 0 .. bytes.Length - 1 do 
        sum <- sum + int bytes.[i]
    sum

let TestSafeSum() = 
    // managed memory
    let arrayMemory = Array.zeroCreate<byte>(100)
    let arraySpan = new Span<byte>(arrayMemory);
    SafeSum(arraySpan)|> printfn "res = %d"

    // native memory
    let nativeMemory = Marshal.AllocHGlobal(100);
    let nativeSpan = new Span<byte>(nativeMemory.ToPointer(), 100);
    SafeSum(nativeSpan)|> printfn "res = %d"
    Marshal.FreeHGlobal(nativeMemory);

    // stack memory
    let mem = NativePtr.stackalloc<byte>(100)
    let mem2 = mem |> NativePtr.toVoidPtr
    let stackSpan = Span<byte>(mem2, 100)
    SafeSum(stackSpan) |> printfn "res = %d"
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
