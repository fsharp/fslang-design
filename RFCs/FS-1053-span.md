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
* Capabilities on byref pointers (In, Out, InOut)
* `ByRefLike` structs (including `Span` and `ReadOnlySpan`)
* `IsReadOnly` structs
* implicit dereference of byref and inref-returns from methods
* `byref` extension members

# Links

* [C# 7.2 feature "ref structs"](https://blogs.msdn.microsoft.com/mazhou/2018/03/02/c-7-series-part-9-ref-structs/)
* [C# 7.2 "read only structs"](https://blogs.msdn.microsoft.com/mazhou/2017/11/21/c-7-series-part-6-read-only-structs/)
* One key part of the F# compiler code for ref structs is [here](https://github.com/Microsoft/visualfsharp/blob/16dd8f40fd79d46aa832c0a2417a9fd4dfc8327c/src/fsharp/TastOps.fs#L5582)
* A related issue is https://github.com/Microsoft/visualfsharp/pull/4576 which deals with oddities in the warnings about struct mutation.

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
//namespace FSharp.Core

module NativePtr = 
    val toVoidPtr : address:nativeptr<'T> -> voidptr
    val ofVoidPtr : voidptr -> nativeptr<'T>
```

#### Add `byref<'T, 'Kind>` to represent pointers with different capabilities

We add this type:
```fsharp
//namespace FSharp.Core

type byref<'T, 'Kind> 
```
The following capabilities are defined:
```fsharp
//namespace FSharp.Core

module ByRefKinds = 
    /// Read capability
    type In
    /// Write capability
    type Out
    /// Read and write capabilities
    type InOut
```
For example, the type `byref<int, In>` represents a byref pointer with read capabilities.
The following abbreviations are defined:
```fsharp
//namespace FSharp.Core

type byref<'T> = byref<'T, ByRefKinds.InOut>
type inref<'T> = byref<'T, ByRefKinds.In>
type outref<'T> = byref<'T, ByRefKinds.Out>
```
Here `byref<'T>` is the existing F# byref type.

A special constraint solving rule allows `ByRefKinds.InOut :> ByRefKinds.In` and `ByRefKinds.InOut :> ByRefKinds.Out` in subsumption,
e.g. most particularly at method and function argument application.  This
means, for example, that a `byref<int>` can be passed where an `inref<int>` is expected (dropping the `Out` capability) and
a  `byref<int>` can be passed where an `outref<int>` is expected (dropping the `In` capability).

Capabilities assigned to pointers get solved in the usual way by type inference. Inference variables for capabilities
are introduced each point the operators `&expr` and `NativePtr.toByRef` are used. In both cases their listed return
type of `byref<T>` is expanded to `byref<T, ?Kind>` for a new type inference variable `?Kind`.

#### `inref<T>` for readonly references and input reference parameters

The type `inref<'T>` is defined as `byref<'T, ByRefKinds.In>` to indicate a read-only byref, e.g. one that acts as an input parameter. It's primary use is to pass structures more efficiently, without copying and without allowing mutation. For example:
```fsharp
let f (x: inref<System.DateTime>) = x.Days
```
Semantically `inref` means "the holder of the byref pointer may only use it to read". It doesn't imply that other threads or aliases don't have write access to that pointer.  And `inref<SomeStruct>` doesn't imply that the struct is immutable.

By the type inference rules above, a `byref<'T>` may be used where an `inref<'T>` is expected.

For methods and properties on F# value types, the F# value type `this` paramater
is given type `inref<'T>` if the value type is considered immutable (has no mutable fields and no mutable sub-structs).

#### `outref<T>` for output reference parameters

The type `outref<'T>` is defined as `byref<'T, ByRefKinds.Out>` to indicate a write-only byref that can act as an output parameter.
```fsharp
let f (x:outref<System.DateTime>) = x <- System.DateTime.Now
```

Semantically `inref` means "the holder of the byref pointer may only use it to write". It doesn't imply that other threads or aliases don't have read access to that pointer.

By the type inference rules above, a `byref<'T>` may be used where an `outref<'T>` is expected.

#### Implicit dereference of return byrefs

F# 4.1 added return byrefs. We adjust these so that they implicitly dereference when a call such as `span.[i]` is used, which calls `span.get_Item(i)` method on `Span`. To avoid the implicit dereference you must write `&span.[i]`.

For example:
```fsharp
let SafeSum(bytes: Span<byte>) =
    let mutable sum = 0
    for i in 0 .. bytes.Length - 1 do 
        sum <- sum + int bytes.[i]
    sum
```

This does not apply to module-defined functions returning byrefs.  It specifically doesn't apply to:

* the `&` operator itself

* `NativePtr.toByRef` which is an existing library function returning a byref

As noted above, in these cases the returned type `byref<T>` is expanded to `byref<T, ?Kind>` for a new type inference variable `?Kind`.

#### TBD: Implicit address-of when calling members with parameter type `inref<'T>` 

When calling a member with an argument of type `inref<T>` an implicit address-of-temporary-local operation is applied.
```fsharp
type C() = 
    static member Days(x: inref<System.DateTime>) = x.Days

let mutable now = System.DateTime.Now
C.Days(&now) // allowed

let now2 = System.DateTime.Now
C.Days(now2) // allowed
```
This only applies when calling members, not arbitrary let-bound functions.

#### `IsReadOnly` on structs

F# structs are normally readonly, it is quite hard to write a mutable struct. Knowing a struct is readonly gives more efficient code and fewer warnings. 

Example code:

```fsharp
[<IsReadOnly>]
type S(count1: int, count2: int) = 
    member x.Count1 = count1
    member x.Count2 = count2
```

TBD: should we add the IsReadOnly attribute automatically when an F# struct is inferred to be readonly, or should the programmer need to make it explicit?


#### ByRefLike structs

"ByRefLike" structs are stack-bound types with rules like `byref<_>`.
They declare struct types that are never allocated on the heap. These are extremely
useful for high-performance programming (and also for correctness) as you get a set of strong checks
about the lifetimes of these values.

Checked imitations:
* [x] Can be used stack-only. i.e. method parameters and local variables
* [x] Cannot be static or instance members of a class or normal struct
* [x] Cannot be method parameter of async methods or lambda expressions
* [x] Cannot be dynamic binding, boxing, unboxing, wrapping or converting
* [x] Can make readonly byreflike structs

Unchecked limitations:
* Represents a sequential struct layout (QUESTION: should we emit Sequential?)
* They can not implement interfaces (QUESTION: should we check this)
* Can implement ToString but you can't call it

Here is an example of what declaring a byref struct looks like in F#:

```fsharp
open System.Runtime.CompilerServices

[<IsByRefLike>]
type S(count1: int, count2: int) = 
    member x.Count1 = count1
    member x.Count2 = count2
```

### Interoperability

* A C# `ref` return value is given type `outref<'T>` 
* A C# `ref readonly` return value is given type `inref<'T>`  (i.e. readonly)
* A C# `in` parameter becomes a `inref<'T>` 
* A C# `out` parameter becomes a `outref<'T>` 

* TBD: Using `inref<T>` in argument position results in the automatic emit of an `[In]` attribute (QUESTION - do we want this?)
* TBD: Using `inref<T>` in return position results in the automatic emit of an `modreq` attribute (QUESTION - do we want this?)
* TBD: Using `outref<T>` in argument position results in the automatic emit of an `[Out]` attribute

* C# `in` parameters get `modreq` when used on virtual signatures. Test that the F# compiler cope with this, and emits it where needed, we need to check:

```
instance void  V([in] int32& modreq([mscorlib]System.Runtime.InteropServices.InAttribute) a) cil managed
```

### Ignoring Obsolete attribute on `ByRefLike`

Separately, C# attaches an `Obsolete` attribute to the `Span` and `Memory` types in order to give errors in down level compilers seeing these types, and presumably has special code to ignore it. We add a corresponding special case in the compiler to ignore the `Obsolete` attribute on `ByRefLike` structs.


#### TBD: `byref` extension members

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

* Considered allowing implicit-dereference when calling arbitrary let-bound functions.  Hwoever we don't normally apply this kind of implicit rule for calling let-bound functions, so decided against it.


# Compatibility
[compatibility]: #compatibility

TBD

# Notes

* F# structs are not always readonly/immutable.
  1. You can declare `val mutable`.  The compiler knows about that and uses it to infer that the struct is readonly/immutable.
  2. Mutable fields can be hidden by signature files
  3. Structs from .NET libraries are mostly assumed to be immutable, so defensive copies aren't taken
  4. You can do wholesale replacement of the contents of the struct through `this <- v` in a member  

  In practice 2, 3 and 4 don't deeply affect the correctness of F# code, they just means that you can trick the compiler into thinking that a `let x = S(...)` binding for such an "odd" struct is immutable when it is actually mutable. This is not normally a problem in practice.

  For (4) we make a change to make the `this` parameter be `readonly` when the struct type has no mutable fields or sub-structures. 


# Unresolved questions
[unresolved]: #unresolved-questions

* see TBD noteas above

* There are a set of questions about how many of the constraints/conditions we check for new declarations of byref structs and explicit uses of `IsReadOnly` etc.   Currently in the prototype, adding IsReadOnly to a struct is unchecked - it is an assertion that the struct can be regarded as immutable, and thus defensive copies do not need to be taken when calling operations. For your examples the attribute doesn't make any difference (with or without the PR) as the compiler has already assumed the structs to be immutable (since it doesn't know about this <- x "wholesale replacement"). With the prototype PR, a mutable struct declared ReadOnly will be treated as if it is immutable, and non defensive copies will be taken.

* F# doesn't compile
```
type DateTime with 
    member x.Foo() = ...
```
as a "byref this" extension member where `x` is a readonly reference.  This means a copy happens on invocation. If it did writing performant extension members for F# struct types would be very easy.  Perhaps we should allow somthing like this

```
type DateTime with 
    member (x : inref<T>).Foo() = ...
```
or
```
type DateTime with 
    [<SomeNewCallByRefAttribute>]
    member x.Foo() = ...
```
If we support "byref this" C#-style extension members then you can do it, but not for extension properties etc.

