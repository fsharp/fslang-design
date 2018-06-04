# F# RFC FS-1053 - voidptr, IsReadOnly structs, inref, outref, ByRefLike structs, byref extension members

.NET has a new feature `Span`. This RFC adds support for this in F#.

* [x] Approved in principle [suggestion](https://github.com/fsharp/fslang-suggestions/issues/648)
* [x] Implementation: [Near completion](https://github.com/Microsoft/visualfsharp/pull/4888)
* [x] Discussion: https://github.com/fsharp/fslang-design/issues/287

# Summary
[summary]: #summary

The main aim of the RFC is to allow effective consumption of APIs using `Span`, `Memory` and ref-returns.  

Span is actually built from several features and a library:
* The `voidptr` type
* The `NativePtr.ofVoidPtr` and `NativePtr.toVoidPtr` functions
* The `inref` and `outref` types via capabilities on byref pointers
* `ByRefLike` structs (including `Span` and `ReadOnlySpan`)
* `IsReadOnly` structs
* implicit dereference of byref and inref-returns from methods
* `byref` extension members

# Quick Guide

|C# | F# |
|:---|:----|
| `out int arg` | `arg: byref<int>` |
| `out int arg` | `arg: outref<int>` |
| `in int arg` | `arg: inref<int>` |
| `ref readonly int` (return) | normally inferred, can use `: inref<int>` |
| `ref` _expr_ | `&expr` |

# Links

* [C# 7.2 feature "ref structs"](https://blogs.msdn.microsoft.com/mazhou/2018/03/02/c-7-series-part-9-ref-structs/)
* [C# 7.2 "read only structs"](https://blogs.msdn.microsoft.com/mazhou/2017/11/21/c-7-series-part-6-read-only-structs/)
* One key part of the F# compiler code for ref structs is [here](https://github.com/Microsoft/visualfsharp/blob/16dd8f40fd79d46aa832c0a2417a9fd4dfc8327c/src/fsharp/TastOps.fs#L5582)
* [C# 7.1 "readonly references"](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.2/readonly-ref.md)
* [C# 7.2: Compile time enforcement of safety for ref-like types.](https://github.com/dotnet/csharplang/blob/master/proposals/csharp-7.2/span-safety.md)
* Issue https://github.com/Microsoft/visualfsharp/pull/4576 deals with oddities in the warnings about struct mutation.

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

Semantically `outref` means "the holder of the byref pointer may only use it to write". It doesn't imply that other threads or aliases don't have read access to that pointer.

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

This applies to module-defined functions returning byrefs as well, e.g.

```fsharp
let mutable x = 1

let f () = &x

let test() = 
    let addr : byref<int> = &f()
    addr <- addr + 1
```
Hwoever it specifically doesn't apply to:

* the `&` operator itself

* `NativePtr.toByRef` which is an existing library function returning a byref

As noted above, in these cases the returned type `byref<T>` is expanded to `byref<T, ?Kind>` for a new type inference variable `?Kind`.

#### Assignment to return byrefs

Direct assignment to returned byrefs is permitted:
```fsharp
type C() = 
    let mutable v = System.DateTime.Now
    member __.InstanceM() = &v

let F1() = 
    let today = System.DateTime.Now.Date
    let c = C() 
    c.InstanceM() <-  today.AddDays(2.0)
```
The same applies to properties, e.g.
```fsharp
type C() = 
    let mutable v = System.DateTime.Now
    member __.InstanceProperty = &v

let F1() = 
    let today = System.DateTime.Now.Date
    let c = C() 
    c.InstanceProperty <- today.AddDays(2.0)
```


#### Implicit address-of when calling members with parameter type `inref<'T>` 

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

`IsReadOnly` is not added to F# struct types automatically, you must add it manually.

Using `IsReadOnly` attribute on a struct which has a mutable field will give an error.

#### ByRefLike structs

"ByRefLike" structs are stack-bound types with rules like `byref<_>` and `Span<_>`.
They declare struct types that are never allocated on the heap. These are
useful for high-performance programming as you get a set of strong checks
about the lifetimes and non-capture of these values. They are also potetnially useful for correctness
in some situations where capture must be avoided.

* Can be used as function parameters, method parameters, local variables, method returns
* Cannot be static or instance members of a class or normal struct
* Cannot be captured by any closure construct (async methods or lambda expressions)
* Cannot be used as a generic parameter

Here is an example:
```fsharp
open System.Runtime.CompilerServices

[<IsByRefLike; Struct>]
type S(count1: int, count2: int) = 
    member x.Count1 = count1
    member x.Count2 = count2
```

ByRefLike structs can have byref members, e.g.
```fsharp
open System.Runtime.CompilerServices

[<IsByRefLike; Struct>]
type S(count1: byref<int>, count2: byref<int>) = 
    member x.Count1 = count1
    member x.Count2 = count2
```

Note that `[<ReadOnly>]` does not imply `[<Struct>]`  both attributes have to be specified.

#### No special treatment of `stackalloc`

The F# approach to `stackalloc` has always been to make it an "unsafe library function" whose use generates a "here be dragons" warning.  The C# team make it part of the language and are able to do some additional checks.

In theory it would be possible to mirror those checks in F#.  However, the C# team are considerig further rule changes around `stackalloc` in any case. Thus it seems ok (or at least consistent) if we follow the existing approach for F# and don’t 
add any specific knowledge of stackalloc to the rules.  

#### Ignoring Obsolete attribute on existing `ByRefLike` definitions

C# attaches an `Obsolete` attribute to the `Span` and `Memory` types in order to give errors in down level compilers seeing these types, and presumably has special code to ignore it. We add a corresponding special case in the compiler to ignore the `Obsolete` attribute on `ByRefLike` structs.

The F# compiler doesn't emit these attributes when defining `ByRefLike` types.  Authoring these types in F# for consumption by down-level C# consumers will be extremely rare (if it ever happens at all). Down-level consumption by F# consumers will also never happen and if it does the consumer will discover extremely quickly that the later edition of F# is required. 


### Interoperability

* A C# `ref` return value is given type `outref<'T>` 
* A C# `ref readonly` return value is given type `inref<'T>`  (i.e. readonly)
* A C# `in` parameter becomes a `inref<'T>` 
* A C# `out` parameter becomes a `outref<'T>` 

* Using `inref<T>` in argument position results in the automatic emit of an `[In]` attribute on the argument
* Using `inref<T>` in return position results in the automatic emit of an `modreq` attribute on the return item
* Using `inref<T>` in an abstract slot signature or implementation results in the automatic emit of an `modreq` attribute on an argument or return
* Using `outref<T>` in argument position results in the automatic emit of an `[Out]` attribute on the argument

### Overloading

When an implicit address is being taken for an `inref` parameter, an overload with an argument of type `SomeType` is preferred to an overload with an argument of type `inref<SomeType>`. For example give this:
```
    type C() = 
         static member M(x: System.DateTime) = x.AddDays(1.0)
         static member M(x: inref<System.DateTime>) = x.AddDays(2.0)
         static member M2(x: System.DateTime, y: int) = x.AddDays(1.0)
         static member M2(x: inref<System.DateTime>, y: int) = x.AddDays(2.0)
    let res = System.DateTime.Now
    let v =  C.M(res)
    let v2 =  C.M2(res, 4)
```
In both cases the overload resolves to the method taking `System.DateTime` rather than the one taking `inref<System.DateTime>`.


### `byref` extension members

"byref" extension methods allow extension methods to modify the struct that is passed in. Here is an example of a C#-style byref extension member in F#:
```fsharp
open System.Runtime.CompilerServices
[<Extension>]
type Ext = 
    [<Extension>]
    static member ExtDateTime2(dt: inref<DateTime>, x:int) = dt.AddDays(double x)
```
Here is an example of using the extension member:
```fsharp
let dt2 = DateTime.Now.ExtDateTime2(3)
```

### `this` on immutable struct members becomes `inref<StructType>`

The `this` parameter on struct members is now `inref<StructType>` when the struct type has no mutable fields or sub-structures. 

This makes it easier to write performant struct code which doesn't copy values.

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

* The addition of pointer capabilities slightly increases the perceived complexity of the language and core library.

* The addition of `IsByRefLike` types increases the perceived complexity of the language and core library.

# Alternatives
[alternatives]: #alternatives

* Make `inref`, `byref` and `outref` completely separate types rather than algebraically related

  --> The type inference rules become much harder to specify and much more "special case".  The current rules just need a couple of extra equations added to the inference engine.

* Generalize the subsumption "InOut --> In" and "InOut --> Out" to be a more general feature of F# type inference for tagged/measure/... types.

   --> Thinking this over

* No implicit dereference on byref return from let-bound functions.  Originally we did not do implicit-dereference when calling arbitrary let-bound functions.  However usability feedback indicated this is a source of inconsistency


# Compatibility
[compatibility]: #compatibility


The additions are not backwards compatible for two reasons:

* The implementation of the RFC includes a bug fix to the design of ref-returns.  Functions, methods and properties
  returning byrefs are now implicitly de-referenced.  An explicit use of an address-of operator
  such as `&f(x)` or `&C.M()` is needed to access the value as a byref pointer.

  An error message is used when a type annotation indicates that an implicit dereference of a ref-return is now being used.

* "Evil struct replacement" now gives an error. e.g.

```fsharp
[<Struct>]
type S(x: int, y: int) = 
    member __.X = x
    member __.Y = y
    member this.Replace(s: S) = this <- s
```
Note that the struct is immutable, except for a `Replace` method.  The `this` parameter will now be considered `inref<S>` and
an error will be reported suggesting to add a mutable field if the struc is to be mutated.

Allowing this assignment was never intended in the F# design and I consider this as fixing a bug in the F# compiler now we have the
machinery to express read-only references.

# Notes

* Implicit address-of is not applied for parameters to let-bound functions.  For example:

```
let testIn (m: inref<System.DateTime>) =
    ()

let callTestIn() =
    testIn System.DateTime.Now
```

  The F# language does no conversions apart from subsumption at calls to let-bound functions (e.g. no F# lambda --> System.Func conversion).  While the programmer may initially expect the same conversions to take place, it is better to maintain consistency and not add this one adhoc type-directed auto-convert.  If we were to make a change here, it would be better to make a systematic change here that took into account all conversions.

* No new or additional checks are made that `outref<_>` parameters are written to.  F# has already had `[<Out>]` parameters and their rarity means there's not a lot of value in adding the control-flow-based checks to implement this.

* F# structs are not always readonly/immutable.
  1. You can declare `val mutable`.  The compiler knows about that and uses it to infer that the struct is readonly/immutable.
  2. Mutable fields can be hidden by signature files
  3. Structs from .NET libraries are mostly assumed to be immutable, so defensive copies aren't taken
  4. You can do wholesale replacement of the contents of the struct through `this <- v` in a member  (see above)

* Note that F# doesn't compile
```
type DateTime with 
    member x.Foo() = ...
```
as a "byref this" extension member where `x` is a readonly reference.  Instead you have to define a C#-style byref extension method. This means a copy happens on invocation. If it did writing performant extension members for F# struct types would be very easy.  Perhaps we should allow somthing like this

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
This makes it impossible to define byref extension properties in particular. 

* Note that `IsByRefLikeAttribute` is only available in .NET 4.7.2.

```fsharp
namespace System.Runtime.CompilerServices
    open System
    open System.Runtime.CompilerServices
    open System.Runtime.InteropServices
    [<AttributeUsage(AttributeTargets.All,AllowMultiple=false); Sealed>]
    type IsReadOnlyAttribute() =
        inherit System.Attribute()

    [<AttributeUsage(AttributeTargets.All,AllowMultiple=false); Sealed>]
    type IsByRefLikeAttribute() =
        inherit System.Attribute()
```



# Unresolved questions
[unresolved]: #unresolved-questions


None
