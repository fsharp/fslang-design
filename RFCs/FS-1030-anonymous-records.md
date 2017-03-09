# F# RFC FS-1030 - Anonymous Records

The design suggestion [Anonymous Records](https://github.com/fsharp/fslang-suggestions/issues/207) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

This RFC is preliminary and very much WIP

* [x] Approved in principle
* [x] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/170)
* [x] Implementation: [very preliminary draft](https://github.com/Microsoft/visualfsharp/compare/master...dsyme:anon-1?expand=1)


# Summary
[summary]: #summary

Add anonymous records as a feature to F#, e.g.

```fsharp
let data = {| X = 1; Y = 2 |}
```


# Motivation
[motivation]: #motivation

1. Writing named record types is painful in F#, especially when
   * the records are used ephemerally in functions, and/or 
   * the records are in return values from functions, and/or
   * the return types are easily entirely inferred, and/or
   * the types are needed when interoperating with C# code

2. There is evidence a lot of pain around converting C# code that uses anonymous records into F# code ([List courtesy of @jpierson](https://github.com/fsharp/fslang-suggestions/issues/207#issuecomment-282570213))
   * http://stackoverflow.com/q/8144184/83658
   * http://stackoverflow.com/q/31909234/83658
   * http://stackoverflow.com/q/8546823/83658
   * http://stackoverflow.com/q/21879859/83658
   * http://stackoverflow.com/q/26961004/83658
   * http://stackoverflow.com/q/8650463/83658
   * http://stackoverflow.com/q/13991448/83658


# Detailed design
[design]: #detailed-design


## Assembly Neutrality and .NET metadata

This is a tricky space: we simultaneously need to satisfy various needs including

1. optional compatibility with C# anonymous objects (from C# 3.0). These have an underlying .NET representation that:
   (a) is assembly-private
   (b) uses very specific type and property names (understood by debugging tools)
   (c) has normal .NET metadata that supports normal .NET reflection
   (d) is in particular usable in LINQ queries

2. optional compatibility with the metadata-only (no .NET metadata) C#s struct tuples. These have an underlying .NET representation that:
   (a) is assembly-neutral
   (b) does _not_ have normal .NET metadata but rather is encoded into ``StructTuple`` typees
   (c) uses associated attribute-encoded metadata at argument and return positions.
   (d) is mutable
   (e) is in usable in LINQ queries (needs to be checked)

Carrying precise .NET metadata for types of kind (1) is required.

In general F# developers will expect two contradictory things
(a) that types will have .NET metadata (like F# nominal record types) supporting normal .NET reflection and .NET data binding.  
(b) that types will be assembly neutral

We choose to make the default (b) over (a) since F# developers can always move to nominal record types if necessary.

## Syntax

In the prototype the primary syntax is 

```fsharp
let data = {| X = 1; Y = 2 |}
```

An expression like this can be formed without a prior type definition for a record type.  The type of the expression is the natural syntax:

```fsharp
val data :  {| X : int; Y : int |}
```

The proposal is 
1. the primary syntax  ``{| X = 1; Y = 2 |}`` should be used to support the most common, natural case in F#, where we are happy for the values to have no backing .NET metadata.  
2. the extended syntax  ``new {| X = 1; Y = 2 |}`` gives C#-3.0 compatible anonymous objects with full .NET metadata.  This syntax may not be written in types outside the assembly where the objects are used. The types are implicitly assembly-qualified.

The precise syntax for the second is TBD, another suggestion is ``{< ... >}`` (e.g. to avoid extra parentheses) though the differences betweeen the two are subtle. The prototype will support both.


#### F#-friendly anonymous records 

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

    // TODO: field reordering....
    //let test3b() = {| a = 1+1; b = 2 |} = {| b = 1; a = 2 |} 

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

#### C#-compatible anonymous objects

In addition we support a separate collection of C#-compatible anonymous object types. The syntax is an open question - see "Unresolved questions" below. For example we may use ``{< X = 1 >}`` or  ``new {< X = 1 >}``

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

Here's the alternative syntax:
```fsharp
module CSharpCompatAnonymousObjects = 
    
    let data1 = new {< X = 1 >}

    let f1 (x : new {< X : int >}) =  x.X

```

# Implementation TBD

1. The prototype generates struct representations by default.  We don't normally want this.
2. The prototype C#-compatible types are TBD
3. The alternative syntax is TBD
4. The language service features are TBD
5. Give a good error message when ``new { ... }`` is used when converting from C# code

# Drawbacks
[drawbacks]: #drawbacks

1. It's work

2. It adds another **two** ways to tuple data in F# (we already have tuples, records, classes, single-case-unions....)

3. The types don't by default carry .NET metadata, so for example ``sprintf "%A"`` doesn't show record field names by default unless we do a lot of extra work. But this also applies to C# tuple types, so we have some of this pain in any case.  But are we just making it much worse?

4. The distinction between types that carry .NET metadata and types that don't is subtle

5. The cost of checking that intellisense, auto-compete etc. work is quite high

# Alternatives
[alternatives]: #alternatives

1. Don't do it.  Just use tuples or new nominal record types

# Unresolved questions
[unresolved]: #unresolved-questions

1. Relationship beteen anonymous record types and existing nominal record types
2. Do we emit and read C# tuple metadata information at return and argument positions?
3. Behaviour under equality and comparison
4. We need to identify the scenario where C#-compatible anonymous objects are required, and the scenarios where they need correct property  names in the .NET metadata
5. Do we use ``netobj {| ... |]`` to generate C# compatibile objects, expecially for use in LINQ queries?  Is that handy enough in queries?  Should them name be ``anonobj {| ...  |}`` or ``{< ... >}`` or ...??


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
