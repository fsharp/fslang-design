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

(a) It Just Works across assembly boundaries. i.e. that type identity will by default be assembly neutral, that is ``{| X:int; Y: int |}`` in one assemby will be type equivalent to the same type in when used in another assembly

(b) It Just Works with .NET Reflection, %A, Json.NET and other features, i.e. has .NET metadata.  i.e. that the runtime objects/types correspdonding to anonymous record values/types will have .NET metadata (like F# nominal record types) supporting normal .NET reflection and .NET data binding.  


This leads to two different kinds of anonymous records:

* **Kind A** anonymous records that work smoothly across assembly boundaries 
* **Kind B** anonymous records that have corresponding strong .NET metadata

.NET provides no mechanism to achieve both of these, i.e. there is no .NET mechanism to make types both have "strong" .NET metadata shared and be equivalent across assembly boundaries.

We support both Kind A and B anonymous records.  However we make the default Kind A since F# developers can always move to either Kind B or nominal record types if necessary. However, we make Kind B an option, see below.


## Design Principle: A Smooth Path to Nominalization

A basic litmus test is this: can the user smoothly (through localized, regular transformations) adjust a closed body of code to use existing F# nominal record types instead of anonymous record types?

The answer is "yes" - they just have to expicitly define each implied record type, and replace ``{| ... |}`` by ``{ .. }``, and add
some type annotations.  Let's call this process "nominalization".

Nominalization is imoprtant as code matures, because values that start as "just data" often gradaully become more like objects: they
collect some associated derived properties, some methods, they start to have constraints and invariants applied, they may end up
having their representation hidden, they may become mutable.  Anonymous records will **not** support this full range of
machinery, though nominal record types and class types do.  As a type matures, you want to make sure
that the user can transition towards nominal record types and class types. (TODO: ink to related suggestions about improving nominal
record types and class types).

Supporting "smooth nominalization" means that features such as these are out of scope or orthogonal
* removing fields from anonymous records ``{ x without A}``
* adding fields to anonymous records ``{ x with A = 1 }``
* unioning anonymous records `` { include x; include y }``

These would all be fine features, but we will treat them as orthogonal: they wil be included if and only if
they are **also** implemented for nominal record types. Today F# record types do not support the above features - even ``{ x with A=1}``
is restricted to create objects of the same type as the original.

If smooth nominalization is not possible, then some users will inevitably use the unique features of anonymous record types, but then be left with no path to nominalize their code when they want to be more explicit, or as their types gradually 


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

## Design Principle: Kind A and Kind B are similar, not awkwardly different

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

## Design Principle: Natural, interoperable compiled representations

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

## Design Principle: Kind B just add .NET metadata, nothing else

There are numerous aspects of the F#/.NET object system that coud be supported by "Kind B" anonymous record types (which have full .NET metadata and a backing .NET type). This incudes
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

# Detailed design
[design]: #detailed-design


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
1. the primary syntax  ``{| X = 1; Y = 2 |}`` gives "Kind B" anonymous records, represented under-the-hood via tuples
2. the extended syntax  ``new {| X = 1; Y = 2 |}`` gives "Kind A" anonymous records, C# compatible and with full .NET metadata.  This types are implicitly assembly-qualified.

The precise syntax for the second is TBD, another suggestion is ``{< ... >}`` (e.g. to avoid extra parentheses) though the differences betweeen the two are subtle. The prototype will support both.


#### Basic anonymous records  ("Kind A")

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

#### Anonymous record values with added .NET metadata ("Kind B")

In addition we support a separate collection of C#-compatible anonymous object types. These are the "Kind B" objects mentioned above. The syntax is an open question - see "Unresolved questions" below. For example we may use ``{< X = 1 >}`` or  ``new {< X = 1 >}``

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

1. Do we emit and read C# tuple metadata information at return and argument positions?
2. Behaviour under equality and comparison


