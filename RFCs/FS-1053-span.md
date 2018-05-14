# F# RFC FS-1053 - Add support for `voidptr`, `IsReadOnly`, `ByRefLike`, "return byrefs", "byref this extension members" and `Span`

.NET has a new feature `Span`. This RFC adds support for this in F#.

* [x] Approved in principle
* [ ] Implementation: [In progress](https://github.com/Microsoft/visualfsharp/pull/4888)
* Discussion: https://github.com/fsharp/fslang-design/issues/287

# Summary
[summary]: #summary

The main aim of the RFC is to allow effective consumption of APIs using `Span`, `Memory` and ref-returns.  

Span is actually built from several features and a library:
* `voidptr` type
* `NativePtr.ofVoidPtr` and `NativePtr.toVoidPtr` functions
* `ByRefLike` structs (including `Span` and `ReadOnlySpan`)
* `IsReadOnly` structs
* `IsReadOnly` byref parameters
* `IsReadOnly` byref locals
* `IsReadOnly` byref returns
* `byref this` extension members
* implicit dereference of byref-returns

# Motivation
[motivation]: #motivation

The main aim of the RFC is to allow effective consumption of APIs using `Span`, `Memory` and ref-returns.  

Also, parity with .NET/C#, better codegen, performance, interop.

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

This does not apply to:
* the `&` operator itself
* `NativePtr.toByRef` which is an existing library function returning a byref

#### `byref this` extension members

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

* Consider if/where we really need to track readonly-ness of byref pointers.

* F# structs are not always readonly/immutable.
  1. You can declare `val mutable`.  The compiler knows about that and uses it to infer that the struct is readonly/immutable.
  2. Mutable fields can be hidden by signature files
  3. Structs from .NET libraries are mostly assumed to be immutable, so defensive copies aren't taken
  4. You can do wholesale replacement of the contents of the struct through `this <- v` in a member  

  In practice 2, 3 and 4 don't deeply affect the correctness of F# code, they just means that you can trick the compiler into thinking that a `let x = S(...)` binding for such an "odd" struct is immutable when it is actually mutable. This is not normally a problem in practice.

  For (4) I'm not sure if we will be able to make a change to make the `this` parameter be `readonly`. We might start to emit a warning on wholesale replacement.  

* There are a set of questions about how many of the constraints/conditions we check for new declarations of byref structs and explicit uses of `IsReadOnly` etc. 

* Currently in the prototype, adding IsReadOnly to a struct is unchecked - it is an assertion that the struct can be regarded as immutable, and thus defensive copies do not need to be taken when calling operations. For your examples the attribute doesn't make any difference (with or without the PR) as the compiler has already assumed the structs to be immutable (since it doesn't know about this <- x "wholesale replacement"). With the prototype PR, a mutable struct declared ReadOnly will be treated as if it is immutable, and non defensive copies will be taken.

* The main niggles left to sort out are indeed about readonly references, also ref this extension members.


* Unfortunately F# doesn't compile
```
type DateTime with 
    member x.Foo() = ...
```
as a "byref this" extension member where `x` is a readonly reference.  If it did writing performant extension members for F# struct types would be very easy.  Perhaps we should allow this

```
type DateTime with 
    [<IsReadOnly>]
    member x.Foo() = ...
```
If we support "byref this" C#-style extension members then you can do it, but not for extension properties etc.

* ref readonly return / ref readonly locals. F# must at least be able to define ref readonly return methods

* C# also added a new modifier beside out and ref: in, it acts the same as ref but ensure that the callee can't reassign the ref to a new value.
