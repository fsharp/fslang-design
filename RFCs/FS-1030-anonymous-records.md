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

# Design Tensions and Limitations

## Design Principle: Type Identity v. .NET metadata

In general F# developers will expect two contradictory things:

(a) that the runtime objects/types correspdonding to anonymous record values/types will have .NET metadata (like F# nominal record types) supporting normal .NET reflection and .NET data binding.  

(b) that type identity will by default be assembly neutral, that is ``{| X:int; Y: int |}`` in one assemby will be type equivalent to the same type in when used in another assembly

.NET provides no mechanism to achieve both of these, i.e. there is no .NET mechanism to make types with strong .NET metadata shared
and equivaent across assembly boundaries.

We choose to make the default (b) over (a) since F# developers can always move to nominal record types if necessary. However,
we make (a) an option, see below.

## Design Principle: Anonymous Records, not Anonymous Objects

The aim of this feature is **not** to create a new "obejct calculus" in F#.  For example, the user can't define "anonymous class types"
such as this:
```fsharp
let obj = {| member x.M(y) = 1 + y 
             member x.P = 2 |}

obj : {| member M : int -> int
         member P : int } 
```
without defining an explicit nominal class.  

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

## Design Principle: No subtyping, no structural typing

"Smooth nominalization" encourages another design limitation:

* Anonymous record types will not support structural subtyping, except to type ``obj``.  So ``{| A : int; B : string |}`` is not a subtype of ``{| A: int |}``

To consider: anonymous record types which have full .NET metadata (and thus assembly-bound identity) could in theory
include interface implementations. For example:

    new {| A : int; interface IComparable |}
 
This tends towards an object calculus, or at least intersection types.  We don't plan to incude this feature.  It is better to use existing nominal types and object expressions for this purpose.


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

1. Relationship between anonymous record types and existing nominal record types
2. Do we emit and read C# tuple metadata information at return and argument positions?
3. Behaviour under equality and comparison
4. We need to identify the scenario where C#-compatible anonymous objects are required, and the scenarios where they need correct property  names in the .NET metadata
5. Do we use ``netobj {| ... |]`` to generate C# compatibile objects, expecially for use in LINQ queries?  Is that handy enough in queries?  Should them name be ``anonobj {| ...  |}`` or ``{< ... >}`` or ...??


