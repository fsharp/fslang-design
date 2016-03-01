# F# RFC FS-0006 - Struct Tuples and Compat with C# 7.0 Tuples

The design suggestion [Add Support for Struct Tuples](https://fslang.uservoice.com/forums/245727-f-language/suggestions/6148669-add-support-for-structtuple) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] [Approved in principle](https://fslang.uservoice.com/forums/245727-f-language/suggestions/6148669-add-support-for-structtuple)
* [ ] Details: [under discussion](https://github.com/fsharp/FSharpLangDesign/issues/FILL-ME-IN)
* [ ] Implementation: initial experiments underway


# Summary
[summary]: #summary

The proposal is to add a new "struct" annotation to tuple types, expressions and patterns. This is based partly on the assumption that the proposed C# 7.0 tuples will use struct representations for at least some small tuple types.

The intention is that the feature would be used primarily in interop and performance-tuning.

[The C# 7.0 tuple design discussion](https://github.com/dotnet/roslyn/issues/347) is highly relevant to this proposal and any
F# feature in this area is highly sensitive to the exact design details chosen there.

A possible extension is to add "structness inference" to reduce the number of "struct" annotations required when using struct tuples.

A possible extension is to add "named field metadata" to both reference and struct tuples to align with C# 7.0.



# Motivation
[motivation]: #motivation

For F# 1.0-4.0, tuples always commpile to reference types.  In many circumstances there are very large performance gains to be had
by using struct (unboxed) representations for tuple types. 

Further, in C# 7.0 it is likely that a tuple feature of some kind will be added.  The design details have yet to be finalized,
but it seems likely that at least smaller tuples will use unboxed representations.  

Further, the proposal for C# 7.0 tuples is likely to carry additional compile-time only metadata giving field names.  This information is
lost in reflection, and is only available via attributes on parameter, return and field declaration locations.  We need
to consider whether F# will import and/or produce and/or propagate this metadata.


# Detailed design
[design]: #detailed-design

The syntax of types, expressions and patterns is extended to include a struct annotation like this:

    type = struct (type * ... * type) 
         | ...


    expr = struct (expr, ..., expr) 
         | ...

    pat = struct (pat, ..., pat) 
        | ...

e.g.

    let origin = struct (0,0)

    let f (struct (x,y)) = x+y

The "structness" of tuple types would be propagated through 
the F# type inference process through unification of structness annotations. So, for example

    let origin2 = origin

would infer that ``origin2`` has type ``struct (int * int)``.

Otherwise, wherever possible, the existing design of F# tuples applies.

No code would be generic over structness, so 

    let f (x,y) = x+y

would apply only to reference tuples, unless "f" has been applied to a struct tuple somewhere in the code. Multiple copies of tuple-related functions may be needed.

### Possible Extension: structness inference

Optionally, the "structness" of tuple expressions and patterns could be inferred through 
the F# type inference process through the unification of structness annotations. So, for example

    let (x0,y0) = origin

would be permitted, as the tuple pattern on the left is inferred to be a struct, rather than requiring

    let (struct (x0,y0)) = origin

Likewise the structness of tuple expressions can be inferred:

    let points : (struct (int * int)) list = [ (1,2); (3,4) ]

rather than requiring a ``struct`` annotation on each struct expression:

    let points : (struct (int * int)) list = [ struct (1,2); struct (3,4) ]

At a technical level, every time a non-annotated tuple form is used, a structness inference parameter is created.
If structness can't be inferred for a structness inference variable, it will default to "not a struct", i.e. a reference type.
These defaults would be applied at the end of type inference like other defaults.

Structness would always be inferred statically for all locations throughout all F# programs. 

Structness inference is not essential but may ease interoperabillity.  On the other hand it adds considerable complexity to the implementation
and may potentially confuse some users who prefer a high degree of type/expression form separation.


### Possible Extension: Tuple Field Metadata

C# 7.0 Named annotations already exist in the syntax of types like this:

    type = idopt: type * ... * idopt: type
         | ...

and to expressions:

    expr = (id=expr, ..., id=expr) 
         | ...

and to patterns:

    pat = (id=pat, ..., id=pat) 
        | ...

e.g.

    let f () = struct (x=1,y=1)

### Type equivalence

Two tuple types are only equivalent if they have the same structness and named argument metadata.

### Compiled form

Struct tuples would compile to a family of types ``System.StructTuple<...>`` in the same way that existing tuples compile to types ``System.Tuple<...>``.

Because structness is determined statically it is always known what the structness is by the time code is emitted.

Named tuple data is 


### Optimizations

The F# compiler applies optimizations to expressions involving the existing F# tuple. These optimizations must be carefully assessed and considered for


### Quotations

The F# quotation nodes ``Expr.NewTuple`` and ``Expr.TupleGet`` assume reference type tuples. 

TBD: determine the quotation form of struct tuples.


### Library changes

It is expected that the .NET standard libraries will add types ``System.StructTuple<...>``.  In the absence of these types we expect that either the F# core library or
a new component will define these types.  Without these types in some standard referenced component, the feature is not really implementable inn a satisfactory way that
promotes the goals of interoperability.


# Drawbacks
[drawbacks]: #drawbacks

* Using struct types implicitly can lead to negative feature interactions, for example, on the 64-bit JIT, for value types > 64-bit in size there is 
  a potential performance minefield when interacting with the "tail" IL instruction.

* The feature requires the definition of ``StructTuple`` types in the standard library.  It is unclear if/when these types will become available, and what 
  dependencies would arise in F# library code.

* The feature is sensitive to the details of the design of C# tuples, which are still being finalized.

* The F# compiler includes many code paths that use reference tuples as a utility representation, for example in argument descriptions for multi-argument methods and 
  in the results of optimizations.  These code paths must be very carefully adjusted to either apply to struct tuples, or not.

* The F# core library has dependencies on reference tuples and includes specialized optimization code for hashing and comparing reference tuples.
  This code must be very carefully adjusted to either apply only to reference tuples or to both kinds of tuples.  Ideally, ``FSharp.Core.dll`` will not 
  pick up a dependency on the ``System.StructTuple<...>`` types until these are available in the minimal .NET versions that ``FSharp.Core`` refers to.

# Alternatives
[alternatives]: #alternatives

* The named-fields metadata feature and the structness feature are orthogonal and each can be considered optional.

* The inference of structness where ``let (x0,y0) = origin`` allows a tuple pattern to be matched to a struct tuple is optional.  
  We could require that structness be noted at all pattern and expression positions in the code, e.g. ``let (struct (x0,y0)) = origin``.

# Unresolved questions
[unresolved]: #unresolved-questions

* Issues remain in the areas of optimizations, quotations, library dependencies, hashing/equality/comparison and other areas noted above.

* The feature is sensitive to the design of C# tuples, which is still being developed.


### Possible Syntax Ambiguities

The syntax chosen may induce ambiguities, e.g.

   type X = struct val X : int end
   
v.s. a type abbreviation:

   type X = struct int * int

This may be particularly problematic w.r.t. whitespace indentation rules, where ``end`` is meant to balance the ``struct`` token.

TBD: the viability of the proposed syntax.


