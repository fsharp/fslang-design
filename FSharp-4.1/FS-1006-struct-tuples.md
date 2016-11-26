# F# RFC FS-0006 - Struct Tuples and Interop with C# 7.0 Tuples

The design suggestion [Add Support for Struct Tuples](https://fslang.uservoice.com/forums/245727-f-language/suggestions/6148669-add-support-for-structtuple) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] [Approved in principle](https://fslang.uservoice.com/forums/245727-f-language/suggestions/6148669-add-support-for-structtuple)
* [x] Details: [under discussion](https://github.com/fsharp/FSharpLangDesign/issues/53)
* [x] Implementation: [Completed](https://github.com/Microsoft/visualfsharp/pull/1043)


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

For F# 1.0-4.0, F# tuples are boxed reference types.  In many circumstances there are very large performance gains to be had
by using struct (unboxed) representations for tuple types. There can also be downsides, due to extra data copying.

Further, in C# 7.0 it is likely that a tuple feature of some kind will be added (see [this issue](https://github.com/dotnet/roslyn/issues/347)).  The design details have yet to be finalized,
but it seems possible that at least smaller tuples may use unboxed representations.  Irrespective of that, 
unboxed tuple representations are an independently useful addition to F#.

Further, the proposal for C# 7.0 tuples is likely to carry additional compile-time only metadata giving field names.  
In the current C# design notes, this information is
lost in reflection, and is  available via custom attributes on parameter, return and field declaration locations.  We need
to consider whether F# will import, produce and/or propagate this metadata.


# Detailed design
[design]: #detailed-design

The syntax of types, expressions and patterns is extended to include a struct annotation like this:

```fsharp
type = struct (type * ... * type)
     | ...
```

```fsharp
expr = struct (expr, ..., expr)
     | ...
```

```fsharp
pat = struct (pat, ..., pat)
    | ...
```

e.g.

```fsharp
    let origin = struct (0,0)
    let f (struct (x,y)) = x+y
```

The "structness" of tuple types would be propagated through the F# type inference process. So, for example

```fsharp
let origin2 = origin
```

would infer that ``origin2`` has type ``struct (int * int)``. Otherwise, wherever possible, the existing design of F# tuples applies.

No code would be generic over structness, so 

```fsharp
let f (x,y) = x+y
```

would apply only to reference tuples.

There are no implitict conversions from struct tuples to reference tuples or vice-versa in this basic design.

### Possible Extension: structness inference

Optionally, the "structness" of tuple expressions and patterns could be inferred through 
the F# type inference process through the unification of structness annotations. So, for example

```fsharp
let (x0,y0) = origin
```

would be permitted, as the tuple pattern on the left is inferred to be a struct, rather than requiring

```fsharp
let (struct (x0,y0)) = origin
```

Likewise the structness of tuple expressions can be inferred:

```fsharp
let points : (struct (int * int)) list = [ (1,2); (3,4) ]
```

rather than requiring a ``struct`` annotation on each struct expression:

```fsharp
let points : (struct (int * int)) list = [ struct (1,2); struct (3,4) ]
```

At a technical level, every time a non-annotated tuple form is used, a structness inference parameter is created.
If structness can't be inferred for a structness inference variable, it will default to "not a struct", i.e. a reference type.
These defaults would be applied at the end of type inference like other defaults.

Structness would always be inferred statically for all locations throughout all F# programs. 

Structness inference is not essential but may ease interoperabillity.  On the other hand it adds considerable complexity to the implementation and may potentially confuse some users who prefer a high degree of type/expression form separation.


### Possible Extension: Tuple Field Metadata

C# 7.0 Named annotations already exist in the syntax of types like this:

```fsharp
type = idopt: type * ... * idopt: type
     | ...
```

and to expressions:

```fsharp
expr = (id=expr, ..., id=expr)
     | ...
```

and to patterns:

```fsharp
pat = (id=pat, ..., id=pat)
    | ...
```

e.g.

```fsharp
let f () = struct (x=1,y=1)
```

### Type equivalence

Two tuple types are only equivalent if they have the same structness (and named argument metadata if that feature is supported)


### Compiled form

Struct tuples would compile to a family of types ``System.StructTuple<...>`` in the same way that existing tuples compile to types ``System.Tuple<...>``.

Because structness is determined statically it is always known what the structness is by the time code is emitted.

Named tuple data is 


### Optimizations

The F# compiler applies optimizations to expressions involving the existing F# tuple. These optimizations must be carefully assessed and considered for


### Feature Interaction: Quotations

The F# quotation nodes ``Expr.NewTuple`` and ``Expr.TupleGet`` assume reference type tuples. 

TBD: determine the quotation form of struct tuples.

### Feature Interaction: Reflection

The following F# reflection functions in FSHarp.Core work reference tuples:

* ``FSharpValue.MakeTuple``
* ``FSharpValue.GetTupleField``
* ``FSharpValue.GetTupleFields``
* ``FSharpValue.GetTupleFields``
* ``FSharpValue.PreComputeTupleReader``
* ``FSharpValue.PreComputeTuplePropertyInfo``
* ``FSharpValue.PreComputeTupleConstructor``
* ``FSharpValue.PreComputeTupleConstructorInfo``
* ``FSharpType.MakeTupleType``
* ``FSharpType.IsTuple``

The proposal is that all these work implicitly on struct tuple,  with the exception of ``FSharpType.MakeTupleType``, and ``FSharpType.MakeStructTupleType`` is added.  The System.Type for a struct tuple type can be distinguished from a
reference tuple type using ``typ.IsValueType``.


### Feature Interaction: Provided Types and Expressions

Provided expressions (i.e. expressions provided by type providers) can be quotation 
nodes ``Expr.NewTuple`` and ``Expr.TupleGet``. These currently assume reference type tuples (see above).

TBD: determine whether struct tuples can appear in provided expressions.

### Feature Interaction: FSharp.Core Library changes

It is expected that the .NET standard libraries will add types ``System.StructTuple<...>``.  In the absence of these types we expect that either the F# core library or
a new component will define these types.  Without these types in some standard referenced component, the feature is not really implementable inn a satisfactory way that
promotes the goals of interoperability.

### Feature Interaction: Interaction with ``equality`` and ``comparison`` constraints

The F# special constraints ``equality`` and ``comparison`` have specific rules for tuples: a tuple type
supports ``equality`` if its element types support ``equality``, and likewise ``comparison``.

TBD: We must decide if this will also apply to struct tuples.

### Feature Interaction: Implicit flexibility on use of function-typed values

F# has a special rule that adds subtype-flexibility when a function value is used, e.g.

```fsharp
let f (x: IComparable) = ...
```

can be called using any value that supports IComparable, because each use of ``f`` is implicitly replaced by a coercing expression:

```fsharp
(fun (x: #IComparable) -> f (x :> IComparable))
```

This also applies to tupled arguments, e.g.

```fsharp
let f (x1: IComparable, x2: IComparable) = ...
```

becomes 

```fsharp
(fun (x1: #IComparable, x2: #IComparable) -> f (x1 :> IComparable, x2 :> IComparable))
```

TBD: We must decide if this will also apply to functions taking struct tuples, and whether this would be consistently applied to members taking struct tuples too.

### Feature Interaction: Generalization of tuples 

In F#, like most other ML langauges, some simple values can be generalized.
This means these values are given generic type rather than hitting the value-restriction error. e.g.

```fsharp
let x = null
val x : 'a when 'a : null

let x = []
val x : 'a list
```

This extends to tuples of these values:

```fsharp
let x = ([], [])
val x : 'a list * 'b list
```

TBD: decide if this extends to struct tuples.

```fsharp
let x = struct ([], [])
```

# Performance and Generated Code

Care must be taken to generate the correct code for pattern matching. The expected generated code for

```fsharp
let f2 (struct (x,y)) = x + y
```

is

```
.method public static int32  f2(valuetype [FSharp.Core]System.StructTuple`2<int32,int32> s) cil managed
{
  // Code size       16 (0x10)
  .maxstack  8
  IL_0000:  ldarga.s   s
  IL_0002:  call       instance !0 valuetype [FSharp.Core]System.StructTuple`2<int32,int32>::get_Item1()
  IL_0007:  ldarga.s   s
  IL_0009:  call       instance !1 valuetype [FSharp.Core]System.StructTuple`2<int32,int32>::get_Item2()
  IL_000e:  add
  IL_000f:  ret
} // end of method A::f2

```

# Examples and Error Messages
[exampes]: #examples

The prototype currently has this (it could be improved)

```fsharp
>  let f (x: struct (int * int)) : (int * int) = x;;

   let f (x: struct (int * int)) : (int * int) = x;;
  -----------------------------------------------^

stdin(2,48): error FS0001: One tuple type is a struct tuple, the other is a reference tuple
```

Some more examples:

```fsharp
> let f (struct (a,b)) = (a,b);;

val f : struct ('a * 'b) -> 'a * 'b

> let f (struct (a,b)) = struct (a,b);;

val f : struct ('a * 'b) -> struct ('a * 'b)

> let f (struct (a,b) as x) = x;;

val f : struct ('a * 'b) -> struct ('a * 'b)

> let f (struct (a,struct (c,d)) as x) = x;;

val f : struct ('a * struct ('b * 'c)) -> struct ('a * struct ('b * 'c))

> let f (struct (a,struct (c,d)) as x) = (a,b,c);;

  let f (struct (a,struct (c,d)) as x) = (a,b,c);;
  ------------------------------------------^

stdin(7,43): error FS0039: The value or constructor 'b' is not defined
> let f (struct (a,struct (c,d)) as x) = (a,c,d);;

val f : struct ('a * struct ('b * 'c)) -> 'a * 'b * 'c

> let f (struct (a,struct (c,d)) as x) = struct (a,c,d);;

val f : struct ('a * struct ('b * 'c)) -> struct ('a * 'b * 'c)

>
```

And comparison/equality:
```fsharp
> struct (1,2) = struct (1,2);;
val it : bool = true
> struct (1,2) = struct (1,3);;
val it : bool = false
> struct (1,2) < struct (1,3);;
val it : bool = true
> struct (1,2) < struct (1,1);;
val it : bool = false

> struct (1,2) = struct (1,3,4);;

  struct (1,2) = struct (1,3,4);;
  -----------------------^^^^^

stdin(3,24): error FS0001: Type mismatch. Expecting a
    struct (int * int)
but given a
    struct (int * int * 'a)
The tuples have differing lengths of 2 and 3
```

# Tooling Considerations
[tooling]: #tooling-considerations

The F# Compiler Service tests should be updated to include specific new tests that autocomplete and symbol information is returned for struct types, expressions and patterns, including incomplete textual versions of these.

# Testing Considerations
[testing]: #testing-considerations

TBD 


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

### Alternative: Always use struct tuples

An alternative is to adjust the F# compiler and specification so that F# tuples are always compiled as  structs, or some other variation on this.  This option has been discarded since
it would be a breaking change to the binary compatibility of F# code.


### Alternative: Resolved Syntax Questions

The syntax above assumes that parentheses are needed:

```fsharp
struct (int * int)
struct (3,4)
```

rather than

```fsharp
struct int * int
struct 3,4
```

This seems reasonable.


# Unresolved questions
[unresolved]: #unresolved-questions

* Issues remain in the areas of optimizations, quotations, library dependencies, hashing/equality/comparison and other areas noted above.

* The feature is sensitive to the design of C# tuples, which is still being developed.


### Question: Hashing/Equality/Comparison

Do struct tuple types admit hashing, equality and comparison in the same way as existing reference tuple types?  How
efficiently is this implemented?

### Question: Possible Syntax Ambiguities

The syntax chosen may induce ambiguities, e.g.

```fsharp
type X = struct val X : int end
```

v.s. a type abbreviation:

```fsharp
type X = struct (int * int)
```

This may be particularly problematic w.r.t. whitespace indentation rules, where ``end`` is meant to balance the ``struct`` token.  
However it should be possible to lookahead one token after the "struct" and avoid the use of the relevant offside processing
rule when the next token is a ``(``.

In the current prototype you can disambiguate with parentheses:

```fsharp
type X = (struct (int * int))
```



