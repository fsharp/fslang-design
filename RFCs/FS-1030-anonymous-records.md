# F# RFC FS-1030 - Anonymous Records

The design suggestion [Anonymous Records](https://github.com/fsharp/fslang-suggestions/issues/207) has been marked "approved in principle".

This RFC is WIP

* [x] Approved in principle
* [x] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/170)
* [x] Implementation: [preliminary draft](https://github.com/Microsoft/visualfsharp/compare/master...dsyme:anon-1?expand=1)


# Summary
[summary]: #summary

Add anonymous records as a feature to F#, e.g.

```fsharp
let data = {| X = 1; Y = "abc" |}

val data : {| X : int; Y : string |}
```



# Motivation
[motivation]: #motivation

1. There are many use cases where you want to package data without needing to write an explicit type

2. Writing named record types is painful in F#, especially when
   * the records are used ephemerally in functions, and/or 
   * the records are in return values from functions, and/or
   * the return types are easily entirely inferred, and/or
   * the types are needed when interoperating with C# code

3. There is evidence a lot of pain around converting C# code that uses C# 3.0 "anonymous objects" into F# code ([List courtesy of @jpierson](https://github.com/fsharp/fslang-suggestions/issues/207#issuecomment-282570213)).
   * http://stackoverflow.com/q/8144184/83658
   * http://stackoverflow.com/q/31909234/83658
   * http://stackoverflow.com/q/8546823/83658
   * http://stackoverflow.com/q/21879859/83658
   * http://stackoverflow.com/q/26961004/83658
   * http://stackoverflow.com/q/8650463/83658
   * http://stackoverflow.com/q/13991448/83658

   In addition C# 7.0 has tuple types with named fields.  Currently F# ignores the named fields. We expect that these will become more frequent in .NET APIs.

# Design Principles

## Design Principle: Low Ceremony, Cheap and Cheerful data

The basic design principle is that an explicit type decaration is not needed when packaging data in a record-like way:

```fsharp
let data = {| X = 1; Y = "abc" |}

val data : {| X : int; Y : string |}
```

## Design Principle: By Default Works Across Assembly Boundaries

In general F# developers will expect two contradictory things:

(a) **It Just Works across assembly boundaries**. That is, type identity for anonymous types will, by default, be assembly neutral. So ``{| X:int; Y: int |}`` in one assembly will be type equivalent to the same type when used in another assembly

(b) **It Just Works with .NET Reflection, ``sprintf "%A"``, Json.NET and other features.** That is, the implied runtime types of the objects have .NET metadata, i.e. the runtime objects/types correspdonding to anonymous record values/types will have .NET metadata (like F# nominal record types).



Unfortunately .NET provides no mechanism to achieve both of these, i.e. there is no .NET mechanism to make types both have "strong" .NET metadata and be equivalent across assembly boundaries.

This leads to two different kinds of anonymous records:

* **Kind A** anonymous records that work smoothly across assembly boundaries 
* **Kind B** anonymous records that have corresponding strong .NET metadata

In this proposal we support both Kind A and B anonymous records.  We make the default "Kind A", but allow F# developers to move to either Kind B or nominal record types if necessary.


## Design Principle: A Smooth Path to Nominalization

A basic litmus test of this feature is this: can the user smoothly (through localized, regular transformations) adjust a closed body of code to use existing F# nominal record types instead of anonymous record types?

We adopt the dsign principle that the answer to this must be "yes" - the developer just has to
1. expicitly define each implied record type
2. replace ``{| ... |}`` by ``{ .. }``
3. add some type annotations.
Let's call this process "nominalization".

Supporting smooth nominalization is important as code matures, because values that start
as "just data" often gradaully become more like objects: they collect some associated derived properties,
some methods, they start to have constraints and invariants applied, they may end up
having their representation hidden, they may become mutable.  Anonymous record types do **not** support this full range of
nominal type machinery, however nominal record types and class types do.  As a type matures, you want to make sure
that the user can transition towards nominal record types and class types. (TODO: link to related suggestions about improving nominal
record types and class types).

Supporting "smooth nominalization" means that features such as these are out of scope or orthogonal
* removing fields from anonymous records ``{ x without A}``
* adding fields to anonymous records ``{ x with A = 1 }``
* unioning anonymous records `` { include x; include y }``

These would all be fine features, but we will treat them as orthogonal: they could be included if and only if
they are **also** implemented for nominal record types. F# nominal record types do not support the above
features - even ``{ x with A=1}`` is restricted to create objects of the same type as the original ``x``.

Without smooth nominalization, developers will inevitably use unique features of anonymous record types,
but be left with no path to nominalize their code as it matures.


## Design Principle: Interop

The feature must achieve both of these:

1. optional compatibility with C# anonymous objects (from C# 3.0). These have an underlying .NET representation that:
   (a) is assembly-private
   (b) uses very specific type and property names (understood by debugging tools)
   (c) has normal .NET metadata that supports normal .NET reflection
   (d) is in particular usable in LINQ queries

2. optional compatibility with the metadata-only (no .NET metadata) C#s struct tuples. These have an underlying .NET representation that:
   (a) is assembly-neutral
   (b) does _not_ have normal .NET metadata but rather is encoded into ``StructTuple`` types
   (c) uses associated attribute-encoded metadata at argument and return positions.
   (d) is mutable
   (e) is in usable in LINQ queries (needs to be checked)

Carrying precise .NET metadata for types of kind (1) is required.

From the point of view of regular F# coding there is very little difference between these.

## Design Principle: Kind A and Kind B are syntactically similar, despite their semantic differences

C# has both Kind A (C# 7.0 tuples) and Kind B (C# 3.0 anonymous objects) mechanisms, but they sit awkwardly alongside.  They use a different syntax, and the C# 3.0 feature is very limited in scope. It is hard to transition from one to the other without losing things. This means they are hard for C# programmers to learn how to use well, and different members of the same team will use these mechanisms differently and conflictingly.  We want to avoid this.

From the point of view of regular F# coding there is very little difference between Kind A and Kind B anonymous records, it should be very seamlesss ("slick and non-invasive") to move between the kinds. 

In practice this means adding and removing ``new`` as needed, from this (Kind A):

```fsharp
let data = {| X = 1; Y = "abc" |}

val data : {| X : int; Y : string |}
```

to this (Kind B):

```fsharp
let data = new {| X = 1; Y = "abc" |}

val data : new {| X : int; Y : string |}
```
The second has strong .NET metadata, the first doesn't.  The first is usable freely across assembly boundaries, the second isn't.

## Design Principle: Interoperable compiled representations

The need for interop means that anonymous records must use the "natural" compiler representations available on .NET:

1. "Kind A" anonymous records must use the ``System.Tuple<...>`` and ``System.ValueTuple<...>`` encodings

2. "Kind B" anonymous records must use a generated type with the same characteristics as mentioned above (except that it will be assembly-public)


## Design Principle: Anonymous Records, not Anonymous Objects

The aim of this feature is **not** to create a new "object calculus" in F#.  For example, the user can't define "anonymous class types"
such as this:
```fsharp
let obj = {| member x.M(y) = 1 + y 
             member x.P = 2 |}

obj : {| member M : int -> int
         member P : int } 
```
without defining an explicit nominal class.  



## Design Principle: No structural typing

"Smooth nominalization" and "Interop" imply the following design limitation:

* Anonymous record types will not support structural subtyping, except to type ``obj``.  So ``{| A : int; B : string |}`` is not a subtype of ``{| A: int |}``

This is because
1. the nominalized versions of these types don't support structural subtyping. 
2. is not possible to support structural subtyping in the natural compiled representations of anonymous record types

## Design Principle: No row polymorphism

OCaml supports an object calculus that includes polymorphism (generics) over sets of bindings - so-called "row variables", e.g.

```fsharp
let f x = x#p

val f : { x : 'a; .. } -> 'b 
```

where the `..` means "a set of object members".  This kind of polymorphism is very natural for anonymous objects.  However, it can't
be expressed in the .NET type system.  It could in theory be supported for inlined F# functions but doing that would be somewhat
complex for reatively little gain in the overall context of F#.  Many practical uses of this kind of genericity can be adequately dealt with via object interfaces and, if necessary, a limited amount of casting.


Code that is generic over record types  _can_ be written using static member constraints, e.g.
```
    let inline getX (x: ^TX) : ^X = 
          (^TX : (member get_X : unit -> ^X) (x))

    getX {| X = 0 |}
    
    let data1 = new {| X = 1; Y = "abc" |}
    getX data1
    
    getX {| X = 2; Y = "2" |}

```
Or, with the syntax proposed for F# 4.1:
```fsharp

    let inline getX x = (_.X) x

    getX {| X = 0 |}
```


## Design Principle: Not anonymous object expressions

There are numerous aspects of the F#/.NET object system that could, in theory, be supported by "Kind B" anonymous record types (which have full .NET metadata and a backing .NET type). This incudes
* properties (computerd on-demand)
* interface implementations
* methods
* indexer properties
* attributes on methods and properties
* events
* object-private ``let`` bindings
* static members
* mutable state

While they don't prevent nominalization, we still don't plan to allow any of these in this feature.   It is better to use existing nominal types and object expressions for this purpose.  Just give the object type a name.

For example these types  could in theory include members and interface implementations:

```fsharp
let data = new {| A = 3; interface IDisposable with member x.Dispose() = ... |}
```

Likewise "Kind B" anonymous record types could also in theory have attributes:

```fsharp
let data = new {| [<Foo>] A = 3; B = 4 |}
```

However we don't plan to alow either of these as part of this feature.

## Design Principle: No anonymous unions

Anonymous unions are a natural analogue to anonymous records. They can be tagged or untagged. However this proposal doesn't
cover anonymous unions.

# Detailed design
[design]: #detailed-design


## Syntax

The primary syntax is 

```fsharp
let data = {| X = 1; Y = 2 |}
```

An expression like this can be formed without a prior type definition for a record type.  The type of the expression is the natural syntax:

```fsharp
val data :  {| X : int; Y : int |}
```

In more detail, a new form of expression is added:

    expr = 
       | ... 
       | new_opt struct_opt {| record-field-bindings |}

A new form of type is added:

    type = 
       | ... 
       | new_opt struct_opt {| record-field-declarations |}


* The primary syntax  ``{| X = 1; Y = 2 |}`` gives "Kind A" anonymous records, represented under-the-hood via tuples
* The extended syntax  ``new {| X = 1; Y = 2 |}`` gives "Kind B" anonymous records, C# compatible and with full .NET metadata.  This types are implicitly assembly-qualified.

The checking and elaboration of these forms is fairly straight-forward.

## Examples: Basic anonymous records  ("Kind A")

These are the "Kind A" anonymous record values mentioned above.

```fsharp
module FSharpFriendlyAnonymousObjectsWithoutDotNetReflectionData = 

    // Gives object that has compile-time only metadata, can be used as an F# type across
    // assembly boundaries.  Compiles to System.Tuple.  May
    // potentially be C#-tuple compatible for named field metadata
    let data1 = {| X = 1 |}

    // Types can be written with the same syntax
    let data2 : {| X : int |} = data1

    // Access is as expected
    let f1 (v : {| X : int |}) = v.X

    // Access can be nested
    let f2 (v : {| X: {| X : int |} |}) = v.X.X

    // Access can be nested
    let f3 (v : {| Y: {| X : int |} |}) = v.Y.X

    // Access can be nested
    let f4 (v : {| Y: {| X : 'T |} |}) = v.Y.X

    // TBD: types can provide solutions to static member constraints
    // ...

    // Equality is possible and types unify correctly
    let test2() = ({| a = 1 |} = {| a = 1 |}) // true
    let test3() = ({| a = 1 |} = {| a = 2 |}) // false

    printfn "{| X = 10 |} = %A" {| X = 10 |} 
    printfn "{| X = 10; Y = 1 |} = %A" {| X = 10; Y = 1 |}
    printfn "10 = %A" (f2 {| X = {| X = 10 |} |}) 
    printfn "10 = %A" (f2 {| X = {| X = 10 |} |}) 

    // field reordering....
    let test3b() = {| a = 1+1; b = 2 |} = {| b = 1; a = 2 |} 

    // Check we get compile-time errors
    //let negTest1() = {| a = 1+1; b = 2 |} = {| a = 2 |} 
    //let negTest2() = {| b = 2 |} = {| a = 2 |} 
    // Check we get parsing error and decent recovery
    //let negParsingTest2() = {| b = 2 }

    // Equality is possible
    let test4() = {| a = 1+1 |} = {| a = Unchecked.defaultof<_> |}
    
    // Comparison is possible
    let test5() = {| a = 1+1 |} > {| a = 0 |}

    // Check we can alias these types
    type recd1 = {| a : int |}

    let test6() : recd1 = {| a = 1+1 |}

    // test a generic function
    let test7<'T>(x:'T) = {| a = x  |}

    // test a generic function
    let test8<'T>(x:'T) = {| a = x; b = x  |}
```

## Examples: Anonymous record values with added .NET metadata ("Kind B")

In addition we support a separate collection of C#-compatible anonymous object types. These are the "Kind B" objects mentioned above. The syntax is ``new {| X = 1; Y = 2 |}``.

These give an object that has full C#-compatible anonymous object metadata. 
Underneath these compile to an instantiation of a generic type defined in the declaring assembly with appropriate .NET 
metadata (property names). These types are CLIMutable and thus C#-compatible. The identity of the types are implicitly assembly-qualified.

These types are usable in LINQ queries.

Struct representations may not be specified, since C# doesn't allow them
    
Copy-and-update may not be used, since C# doesn't allow this on anonymous objects

These values _can_ be used outside their assembly, but the types can _not_ be named in the syntax of types outside that assembly.


```fsharp
module CSharpCompatAnonymousObjects = 
    
    let data1 = new {| X = 1 |}

    let f1 (x : new {| X : int |}) =  x.X

```

# Implementation Notes

* Kind B types are given a unique name by SHA1 hashing the names of the fields

* Kind B types are marked serializable


# Implementation TBD

1. Equaity, hash and comparison on Kind B types are TBD
2. The language service features are TBD

# Drawbacks
[drawbacks]: #drawbacks

1. It's work

2. It adds another **two** ways to tuple data in F# (we already have tuples, records, classes, single-case-unions....)

3. The distinction between Kind A and Kind B types is subtle

# Alternatives
[alternatives]: #alternatives

1. Don't do it.  Just use tuples or new nominal record types


2. Use `{< ... >}` for kind B values. Here's an example of this alternative syntax:

```fsharp
module CSharpCompatAnonymousObjects = 
    
    let data1 = new {< X = 1 >}

    let f1 (x : new {< X : int >}) =  x.X
```


# Unresolved questions
[unresolved]: #unresolved-questions

1. Do we emit and read C# tuple metadata information at return and argument positions?
2. Behaviour under equality and comparison
3. Can records be created using implied field names ``{ x.Name; Age = 31 }``
4. Do FSharp.Core functions ``FSharp.Reflection.FSharpType.GetRecordFields`` and ``FSharp.Reflection.FSharpValue.MakeRecord/GetRecordField/GetRecordFields`` work with anonymous record values?  

# Addenda

## C# anonymous type MSIL

this is IL generated for assembly containing this expression:

```csharp
new { a = 1 }
```

```csharp
.class private auto ansi sealed beforefieldinit '<>f__AnonymousType0`1'<'<a>j__TPar'>
	extends [mscorlib]System.Object
{
	.custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = (
		01 00 00 00
	)
	.custom instance void [mscorlib]System.Diagnostics.DebuggerDisplayAttribute::.ctor(string) = (
		01 00 0c 5c 7b 20 61 20 3d 20 7b 61 7d 20 7d 01
		00 54 0e 04 54 79 70 65 10 3c 41 6e 6f 6e 79 6d
		6f 75 73 20 54 79 70 65 3e
	)
	// Fields
	.field private initonly !'<a>j__TPar' '<a>i__Field'
	.custom instance void [mscorlib]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggerBrowsableState) = (
		01 00 00 00 00 00 00 00
	)

	// Methods
	.method public hidebysig specialname 
		instance !'<a>j__TPar' get_a () cil managed 
	{
		// Method begins at RVA 0x2050
		// Code size 7 (0x7)
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: ldfld !0 class '<>f__AnonymousType0`1'<!'<a>j__TPar'>::'<a>i__Field'
		IL_0006: ret
	} // end of method '<>f__AnonymousType0`1'::get_a

	.method public hidebysig specialname rtspecialname 
		instance void .ctor (
			!'<a>j__TPar' a
		) cil managed 
	{
		.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
			01 00 00 00
		)
		// Method begins at RVA 0x2058
		// Code size 14 (0xe)
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: call instance void [mscorlib]System.Object::.ctor()
		IL_0006: ldarg.0
		IL_0007: ldarg.1
		IL_0008: stfld !0 class '<>f__AnonymousType0`1'<!'<a>j__TPar'>::'<a>i__Field'
		IL_000d: ret
	} // end of method '<>f__AnonymousType0`1'::.ctor

	.method public hidebysig virtual 
		instance bool Equals (
			object 'value'
		) cil managed 
	{
		.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
			01 00 00 00
		)
		// Method begins at RVA 0x2068
		// Code size 36 (0x24)
		.maxstack 3
		.locals init (
			[0] class '<>f__AnonymousType0`1'<!'<a>j__TPar'>
		)

		IL_0000: ldarg.1
		IL_0001: isinst class '<>f__AnonymousType0`1'<!'<a>j__TPar'>
		IL_0006: stloc.0
		IL_0007: ldloc.0
		IL_0008: brfalse.s IL_0022

		IL_000a: call class [mscorlib]System.Collections.Generic.EqualityComparer`1<!0> class [mscorlib]System.Collections.Generic.EqualityComparer`1<!'<a>j__TPar'>::get_Default()
		IL_000f: ldarg.0
		IL_0010: ldfld !0 class '<>f__AnonymousType0`1'<!'<a>j__TPar'>::'<a>i__Field'
		IL_0015: ldloc.0
		IL_0016: ldfld !0 class '<>f__AnonymousType0`1'<!'<a>j__TPar'>::'<a>i__Field'
		IL_001b: callvirt instance bool class [mscorlib]System.Collections.Generic.EqualityComparer`1<!'<a>j__TPar'>::Equals(!0, !0)
		IL_0020: br.s IL_0023

		IL_0022: ldc.i4.0

		IL_0023: ret
	} // end of method '<>f__AnonymousType0`1'::Equals

	.method public hidebysig virtual 
		instance int32 GetHashCode () cil managed 
	{
		.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
			01 00 00 00
		)
		// Method begins at RVA 0x2098
		// Code size 29 (0x1d)
		.maxstack 8

		IL_0000: ldc.i4 -327796526
		IL_0005: ldc.i4 -1521134295
		IL_000a: mul
		IL_000b: call class [mscorlib]System.Collections.Generic.EqualityComparer`1<!0> class [mscorlib]System.Collections.Generic.EqualityComparer`1<!'<a>j__TPar'>::get_Default()
		IL_0010: ldarg.0
		IL_0011: ldfld !0 class '<>f__AnonymousType0`1'<!'<a>j__TPar'>::'<a>i__Field'
		IL_0016: callvirt instance int32 class [mscorlib]System.Collections.Generic.EqualityComparer`1<!'<a>j__TPar'>::GetHashCode(!0)
		IL_001b: add
		IL_001c: ret
	} // end of method '<>f__AnonymousType0`1'::GetHashCode

	.method public hidebysig virtual 
		instance string ToString () cil managed 
	{
		.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
			01 00 00 00
		)
		// Method begins at RVA 0x20b8
		// Code size 77 (0x4d)
		.maxstack 7
		.locals init (
			[0] !'<a>j__TPar',
			[1] !'<a>j__TPar'
		)

		IL_0000: ldnull
		IL_0001: ldstr "{{ a = {0} }}"
		IL_0006: ldc.i4.1
		IL_0007: newarr [mscorlib]System.Object
		IL_000c: dup
		IL_000d: ldc.i4.0
		IL_000e: ldarg.0
		IL_000f: ldfld !0 class '<>f__AnonymousType0`1'<!'<a>j__TPar'>::'<a>i__Field'
		IL_0014: stloc.0
		IL_0015: ldloca.s 0
		IL_0017: ldloca.s 1
		IL_0019: initobj !'<a>j__TPar'
		IL_001f: ldloc.1
		IL_0020: box !'<a>j__TPar'
		IL_0025: brtrue.s IL_003b

		IL_0027: ldobj !'<a>j__TPar'
		IL_002c: stloc.1
		IL_002d: ldloca.s 1
		IL_002f: ldloc.1
		IL_0030: box !'<a>j__TPar'
		IL_0035: brtrue.s IL_003b

		IL_0037: pop
		IL_0038: ldnull
		IL_0039: br.s IL_0046

		IL_003b: constrained. !'<a>j__TPar'
		IL_0041: callvirt instance string [mscorlib]System.Object::ToString()

		IL_0046: stelem.ref
		IL_0047: call string [mscorlib]System.String::Format(class [mscorlib]System.IFormatProvider, string, object[])
		IL_004c: ret
	} // end of method '<>f__AnonymousType0`1'::ToString

	// Properties
	.property instance !'<a>j__TPar' a()
	{
		.get instance !0 '<>f__AnonymousType0`1'::get_a()
	}

} // end of class <>f__AnonymousType0`1

```
