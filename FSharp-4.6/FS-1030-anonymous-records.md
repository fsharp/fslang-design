# F# RFC FS-1030 - Anonymous Records

The design suggestion [Anonymous Records](https://github.com/fsharp/fslang-suggestions/issues/207) has been marked "approved in principle".

* [x] Approved in principle

* [x] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/170)

* [x] Implementation: [ready](https://github.com/Microsoft/visualfsharp/pull/4499)

# Summary
[summary]: #summary

Add anonymous records as a feature to F#, e.g.

```fsharp
let data = {| X = 1; Y = "abc" |}

val data : {| X : int; Y : string |}

let result = data.X + data.Y.Length

let newData = {| data with Z = data.X + 5 |}
```

# Motivation/Background
[motivation]: #motivation

1. Writing named record types is painful in F#, especially when

   * the records are used ephemerally in functions, and/or 
   
   * the records are in return values from functions, and/or
   
   * the return types are easily entirely inferred, and/or
   
   * the types are needed when interoperating with C# code

2. There is evidence a lot of pain around converting C# code that uses C# 3.0 "anonymous objects" into F# code ([List courtesy of @jpierson](https://github.com/fsharp/fslang-suggestions/issues/207#issuecomment-282570213)).
   * http://stackoverflow.com/q/8144184/83658
   * http://stackoverflow.com/q/31909234/83658
   * http://stackoverflow.com/q/8546823/83658
   * http://stackoverflow.com/q/21879859/83658
   * http://stackoverflow.com/q/26961004/83658
   * http://stackoverflow.com/q/8650463/83658
   * http://stackoverflow.com/q/13991448/83658

3. C# 7.0 has tuple types with named fields.  Currently F# ignores the named fields. We expect that these will become more frequent in .NET APIs.  However this design does not include interacting with this metadata.

# Design Principles

## Design Principle: Low Ceremony, Cheap and Cheerful data

The basic design principle is that an explicit record type decoration is not needed when packaging data in a record-like way:

```fsharp
let data = {| X = 1; Y = "abc" |}

val data : {| X : int; Y : string |}
```

instead of
```fsharp
type Data = 
    { X : int 
      Y : string }

let data = { X = 1; Y = "abc" }

val data: Data
```

## Design Principle: By Default Works Across Assembly Boundaries

In theory F# developers will expect two contradictory things:

(a) **It Just Works across assembly boundaries**. That is, type identity for anonymous types will, by default, be assembly neutral. So ``{| X:int; Y: int |}`` in one assembly will be type equivalent to the same type when used in another assembly

(b) **It Just Works with .NET Reflection, ``sprintf "%A"``, Json.NET and other features.** That is, the implied runtime types of the objects have .NET metadata, i.e. the runtime objects/types correspdonding to anonymous record values/types will have .NET metadata (like F# nominal record types).

Unfortunately .NET provides no mechanism to achieve both of these, i.e. there is no .NET mechanism to make types both have "strong" .NET metadata and be equivalent across assembly boundaries.

This leads to two different kinds of anonymous records:

* **Kind A** anonymous records that work smoothly across assembly boundaries 
* **Kind B** anonymous records that have corresponding strong .NET metadata but are nominally tied to a specific assembly

**In this proposal we support only Kind B anonymous records.**


## Design Principle: A Smooth Path to Nominalization

A basic litmus test of this feature is this: can the user smoothly (through localized, regular transformations) adjust a closed body of code to use existing F# nominal record types instead of anonymous record types?

We adopt the dsign principle that the answer to this must be "yes" - the developer just has to
1. expicitly define each implied record type
2. replace ``{| ... |}`` by ``{ .. }``
3. add some type annotations.
Let's call this process "nominalization".

Supporting smooth nominalization is important as code matures, because values that start
as "just data" often gradually become more like objects: they collect some associated derived properties,
some methods, they start to have constraints and invariants applied, they may end up
having their representation hidden, they may become mutable.  Anonymous record types do **not** support this full range of
nominal type machinery, however nominal record types and class types do.  As a type matures, you want to make sure
that the user can transition towards nominal record types and class types. (TODO: link to related suggestions about improving nominal
record types and class types).

Supporting "smooth nominalization" means we need to carefully consider whether features such as these allowed:
* removing fields from anonymous records ``{ x without A }``
* adding fields to anonymous records ``{ x with A = 1 }``
* unioning anonymous records `` { include x; include y }``

These should be included if and only if they are **also** implemented for nominal record types. Futher, their use makes the cost of nominalization higher, because F# nominal record types do not support the above
features - even ``{ x with A=1 }`` is restricted to create objects of the same type as the original ``x``, and thus multiple
nominal types will be needed where this construct is used.

## Design Principle: Interop

The feature must achieve compatibility with C# anonymous objects (from C# 3.0). These have an underlying .NET representation that:

(a) is assembly-private

(b) uses very specific type and property names (often understood by debugging tools)

(c) has normal .NET metadata that supports normal .NET reflection

(d) is in particular usable in LINQ queries

The major difference in the F# types are they are not assembly-private

## Design Principle: Interoperable compiled representations

The need for interop means that anonymous records must use the "natural" compiler representations available on .NET. Anonymous records must use a generated type with the same characteristics as used by C# anonymous records (except that it will be assembly-public)

## Design Principle: Anonymous Records, not Anonymous Objects

The aim of this feature is **not** to create a new "object calculus" in F#.  For example, the user can't define "anonymous class types"
such as this:
```fsharp
let obj = {| member x.M(y) = 1 + y 
             member x.P = 2 |}

obj : {| member M : int -> int
         member P : int |} 
```
without defining an explicit nominal class.  

## Design Principle: No structural subtyping

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

where the `..` means "a set of object members".  (You could also call this "column polymorphism" or "column generics" since it is being generic over the set of other columns in a database). This kind of polymorphism is very natural for anonymous objects.  However, it can't
be expressed in the .NET type system.  It could in theory be supported for inlined F# functions but doing that would be somewhat
complex and is orthogonal to this PR.  Some practical uses of this kind of genericity can be adequately dealt with via object interfaces and, if necessary, a limited amount of casting.


Code that is generic over record types  _can_ be written using static member constraints, e.g.
```
    let inline getX (x: ^TX) : ^X = 
          (^TX : (member get_X : unit -> ^X) (x))

    getX {| X = 0 |}
    getX {| X = 1; Y = "abc" |}
    getX {| X = 2; Y = "2" |}

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
let data = {| A = 3; interface IDisposable with member x.Dispose() = ... |}
```

Likewise "Kind B" anonymous record types could also in theory have attributes:

```fsharp
let data = {| [<Foo>] A = 3; B = 4 |}
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


## Type Identity

Different types, e.g. ``{| X : int |}`` and ``{| Y : int |}`` are considered to be separate types.  

In the generated code, anonymous record types are given a unique name by SHA1 hashing the names of the fields.  This name must never change in future F# compilers.  The exact hash used is very, very, very, very, very, very, very, very, very, very, very unlikely to collide, see [probability of SHA-1 hash collision](http://stackoverflow.com/questions/1867191/probability-of-sha1-collisions)

## Closure under substitution

```fsharp
let f (x: 'T) = {| X = x |}

f 3
```
gives a value of the same type as 
```
let f () = {| X = 3 |}
```

That is, if you create anonymous records generically,  their types are correctly "filled in" and become type equivalent.  This is because the generated class for the anonymous type is made generic in an appropriate number of generic type parameters, with one generic parameter for each field type.

## Checking and Elaboration

The checking and elaboration of these forms is fairly straight-forward.

Notes:
* Anonymous record types are marked serializable

Anonymous record types types have  full C#-compatible anonymous object metadata. Underneath these compile to an instantiation of a generic type defined in the declaring assembly with appropriate .NET metadata (property names). These types are CLIMutable and thus C#-compatible. The identity of the types are implicitly assembly-qualified.

These types are usable in LINQ queries.

Struct representations may be specified.

```fsharp
{| X = 1; Y = 2 |}
struct {| X = 1; Y = 2 |}
```

These values _can_ be used outside their assembly, but the types can _not_ be named in the syntax of types outside that assembly.

## Name resolution

Names are *only* resolved if known type information is available, e.g.

```fsharp
let f x = x.P // no resolution
    
let data = {| P = 3 |}
data.P  // has a resolution
```


## Copy and Update

Copy and update expressions for anonymous records are like those for normal records with some significant differences.  For

    {| origExpr with X = 1; Y = 2 ... |}

1. The origExpr may be either a record or anonymous record.
2. The origExpr may be either a struct or not.
3. All the properties of origExpr are copied across except where they are overridden.
4. The result is an anonymous record.
5. Unlike records, we do _not_ assume that the origExpr has the same type as the overall expression.
6. Unlike records,  {| a with X = 1 |} does not force a.X to exist or have had type 'int'

For example:

```fsharp

let data = {| X = 1 |}               // gives {| X = 1 |}

let data2 = {| data with Y = "1" |}  // gives {| X = 1; Y = "1" |}

let data4 = {| data2 with X = "3" |} // gives {| X = "3"; Y = "1" |}
```

## Field Ordering

Fields are placed in a canonical order by the compiler, so type ``{| A : int; B : int |}`` is type-equivalent to ``{| B: int; A : int |}``. 

## Interaction with F# Reflection Utilies

The FSharp.Core functions ``FSharp.Reflection.FSharpType.GetRecordFields`` and ``FSharp.Reflection.FSharpValue.MakeRecord/GetRecordField/GetRecordFields`` work with anonymous record values and types.


## Equality and Comparison

Anonymous types support both structural equality (if all constituent members support equality) and structural comparison (if all constituent types support comparison)

## Pattern matching

It is not possible to pattern match on anonymous record values, the dot-notation must be used instead.

## Cross-assembly working

Anonymous record types can't easily be created in other assemblies without type annotations (in which case normal record types can often be used).

In general values of an anonymous record type from another assembly can't easily be created in other assemblies without type annotations (in which case normal record types can often be used).

However, if an anonymous record type flows across an assembly boundary, and an anonymous record expression has known type of that anonymous record type, with correctly matching field labels etc, then the anonymous record expression will be assumed to be creating an instance of that type.  For example

```fsharp
let x : SomeOtherAssembly.SomeAbbreviationForAnAnonymousRecordType = {| A = 3; B = 4 |}
```

## Structness inference

The structness of the anonymous record expressions is also inferred from the known type.

For consistency, the structness of anonymous tuple expressions and types is now also inferred from the known type.  So

```fsharp
let f (struct (x,y)) = x + y
f (4,5) // the structness of the tuple is inferred here
```

and

```fsharp
let f (x : struct {| A: int; B : int |})  = x.A + y.B

f {| A = 1; B = 3 |} // the structness of the anonymous record is inferred here
```

## Examples: Basic anonymous records  

```fsharp

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

## FCS Symbols API

Straight-forward additions are made to the FCS symbols API, see the PR for details

## Tooling

The following features flow naturally from the implementation

* Mouse-hover labels reports the instantiated type of the label

* Anonymous record types are formatted and displayed in type info

* Go-to-definition on a label `x.P` takes you to one of the declaration sites responsible for the declaration of `P`

* Find-all-uses finds uses of labels when they are associated with the same anonymous record type

# Unresolved questions
[unresolved]: #unresolved-questions

None

# Future extensions

There are several possible future extensions that are compatible with this RFC:

1. Records using implied field names ``{| x.Name; Age = 31 |}`` instead of `` {| Name=x.Name; Age=31 |}``. 

1. Supporting pattern matching, e.g. `match x with {| Y = y |} -> ...`

1. Allowing the use of `[<CLIMutable>]` on anonynous record values

1. Interaction between nominal record types and anonymous record types:

       type R = { X : int; Y: int; Z: int }
       let data = {| X = 1; Y = 2 |}
       let data2 : R = { data with Z = 3 }

   As mentioned in the RFC this breaks the principle of "easy nominalization" since the following is not allowed:

       type R = { X : int; Y: int; Z: int }
       type Data = { X : int; Y: int }
       let data = { X = 1; Y = 2 }
       let data2 : R = { data with Z = 3 }

   because the last line constrains "data" and "data2" to be the same type.  However in the presence of enough type annotations like above we could lift this restriction.


# Drawbacks
[drawbacks]: #drawbacks

#### Drawback: It's work

#### Drawback: More ways to do things

This adds another way to tuple data in F#. We already have tuples, records, classes, single-case-unions....

Response: yes, but it gives a smooth path to nominalization and relatively few new surprises

# Alternatives
[alternatives]: #alternatives

#### Alternative: Don't do it

Just have users continue to use tuples or new nominal record types.


#### Alternative: Use ``{< ... >}`` syntax for Kind B

1.  Use `{< ... >}` for kind B values. Here's an example of this alternative syntax:

```fsharp
module CSharpCompatAnonymousObjects = 
    
    let data1 = {< X = 1 >}

    let f1 (x : {< X : int >}) =  x.X
```

However we decided against this.  It is one thing to expain that ``new`` adds .NET metadata to an anonymous type.  It is another to explain the existence of an entirely new set of ``{< ... >}`` parentheses.

#### Alternative: Allow optional naming of anonymous types.  

This proposal was to allow anonymous types to be named:

```fsharp
let makePerson(name:string, dob:DateTime) : Person = {| Name = name; DOB = dob |}
```

Note the ``Person``.  We decided not to do this.  If you want to name the types, use a type declaration - either an abbreviation or a proper record type.

#### Alternative: Use existing tuple syntax

This proposal was to use the existing tuple notation for anonymous records. e.g.

```fsharp
struct (x = 4, y = "")
(x = 4, y = "")
```

or some other variation.

There are pros and cons to this. The biggest positive is that it may help to emphasise that the field names are erased.  The biggest negative is that the process of "nominalization" is much less smooth should you want to move to nominal record types.

#### Alternative: Do not sort fields by name

Sorting by field name is the natural thing for the programmer from a type-system usability perspective.

However it does have some downsides.  For example, when using anonymous record data for rows in tabular data the fields will not imply a column ordering.  

#### Alternative: Various alternatives aroud copy-and-update

Copy-and-update could be design differently:

* In the design, F# records _can_ be used as the starting expression for copy-and-update.

* Other object types could also be allowed, but what properties would be used as the starting selection?  Better to require `{| x.Name, x.Foo |}` explicitly.  

* Other whacky alternatives are possible, e.g. `{|  x.Foo* with A = 1 |}`

* `{| x |}` without any `with` bindings is not allowed.  In theory it could be allowed.

* `{| x |} : SomeOtherAnonymousRecordType` is not allowed, but in theory could be, where the fields to be selected out are determined by `SomeOtherAnonymousRecordType`


#### Alternative: implicit conversion

It would be possible to imagine an implicit conversion being applied whenever a value of one anonymous record type is used with a known type of another anonymous record type.  This is not done as this kind of implicit conversion is rarely used in the F# design.  

Equally, such a conversion could either 

1. be in a special function, e.g. `conv x` that "knows" about a whole range of conversions

2. be applied at member application (i.e. in places where such conversions are already applied today

#### Alternative: Use a dynamic representation

Alternative:

> Kind A values do not support any runtime metadata for field names - it has been erased.  This begs the question whether "Kind A" records would be better off using a dynamically typed representation at runtime, in the sense of ``Map<string, obj>``.

Response:

> It's just too deeply flawed - it neither gives performance, nor interop, nor reflection metadata. We can't leave such a huge performance hole lying around F#.

### Alternative: support pattern matching

A note by @dsyme: The omission of pattern matching for anonymous records really shows my strong bias against pattern matching on records at all - I nearly always dislike code that uses pattern matching on records. For exaple, I don't think it adds to the robustness of code since pattern matching on records is "flexible", i.e. fields can be omitted.   I know others will disagree however.

### Alternative: syntax ``type {| i = 1 |}``

Response: This is one of a number of alternatives trying imply "this value has runtime type information".  Others might be ``rtt {| i = 1 |}`` (``rtt`` for "runtime type") or ``obj {| i = 1 |}``.  However each of which seems worse in other ways. For example ``type`` mighy imply "what comes after this is in the syntax of types" or something like that.

#### Alternative: Support both Kind A and Kind B types 

The original version of this RFC supported both Kind A and Kind B types
```
{| X = 1 |} //  Kind A

new {| X = 1 |} //  Kind B
```

The problem is that the distinction between Kind A and Kind B is very subtle, as is the lack of reflection metadata on Kind A. See discussion:

    https://github.com/fsharp/fslang-design/issues/170#issuecomment-288394546

Description:

> Passing "Kind A" records to any reflection-based serializer will cause the value to be serialized like a tuple. "Kind B" exists to address these types of concerns, but "Kind A" may be violating expectations. This may be an avenue for serious bugs, incorrectly writing to a database because somebody forgot to put a new keyword before the record declaration.

In response: 

> Creating objects to hand off to reflection operations is indeed one use case - though it's not the only one. The feature is useful enough simply to avoid writing out record types for transient data within F#-to-F# code.
> 
> C# 7.0 tuples are very much exposed to this -  it's even worse there because there is more reliance in C# on .NET metadata, and not much of a tradition of erased information. Many C# people will try to go and mine the metadata, e.g. by looking at the calling method, cracking the IL etc. However this information is often completely erased so they will be frustrated at how hard it is to do, and in most cases just give up.
> A lot of this depends on how you frame the purpose of the feature, and how much reflection programming you see F# programmers doing. It is also why I emphasize the importance of nominalization as a way to transition from "cheep and cheerful data" to data with strong .NET types and cross-assembly type names.


# Addenda

## C# anonymous type MSIL

this is IL generated for C# code  containing this expression:

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
