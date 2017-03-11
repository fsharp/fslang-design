# F# RFC FS-1014 - Struct Discriminated Unions

The design suggestion [Allow single case unions to be compiled as structs](https://fslang.uservoice.com/forums/245727-f-language/suggestions/6147144-allow-single-case-unions-to-be-compiled-as-structs) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] [User Voice Request](https://fslang.uservoice.com/forums/245727-f-language/suggestions/6147144-allow-single-case-unions-to-be-compiled-as-structs)
* [x] Details: [under discussion](https://github.com/fsharp/FSharpLangDesign/issues/95)
* [x] Implementation: [Completed for Single-case](https://github.com/Microsoft/visualfsharp/pull/1262) and for [Multi-case](https://github.com/Microsoft/visualfsharp/pull/1399)


# Summary
[summary]: #summary

See [struct records](https://github.com/fsharp/fslang-design/blob/master/FSharp-4.1/FS-1008-struct-records.md):

Like record types, Discriminated Union types should be able to be marked as a struct,
effectively making the union type have the semantics of value types.


# Motivation
[motivation]: #motivation

Enable better performance in some situations via a simple attribute addition.


# Detailed design
[design]: #detailed-design

How to use:

```fsharp
// Single case:

[<Struct>]
type UnionExample = U of int * int * bool

// Multi-case:

[<Struct>]
type Shape =
    | Circle of radius: double
    | Square of side: int
```

Key differences in struct records:

* You cannot have cyclic references to the same type being defined. ex: `type T = U of T`

* You also cannot call the default ctor, like you could with normal F# structs.

* For multi-case struct unions, each case must have a unique name.

## Feature interaction - Generated IComparable, GetHashCode, Equals

The code generation for these generated interface/overrides must be carefully adjusted.

## Feature interaction - Reflection

`FSharp.Reflection.FSharpType` and `FSharp.Reflection.FSharpValue` implementations must work correctly on struct union types.  

## Performance Considerations and Code Quality 

### Avoiding needless copying in normal code

Consider code such as the following:
```fsharp 

[<Struct>]
type U = U of int * int

let g1 (U(a,b)) = a + b 

let g2 u = 
    let (U(a,b)) = u
    a + b 
```

A naive (e.g. debug) implementation of these will create many copies of the input structs.  For example, the debug form of ``g2`` will look like this (in C syntax)
```fsharp

let g2 u = 
    let patternInput = u
    let a = (&patternInput)->item1
    let b = (&patternInput)->item2
    a + b
```
giving three locals.  The actual optimized code is
```fsharp
let g2 u = (&u)->item1) + (&u)->item2
```
The F# optimizer gets from the first form to the second.

Likewise, for this code:
```fsharp
let g3 (x:U) (y: U) = 
    match x,y with 
    | U(3,a), U(5,b) -> a + b
    | U(a,b), U(c,d) -> a + b + c + d
```
a very considerable amount of optimization work needs to happen to make this copy-free. For example 
the ``x, y`` is a tuple of structs.  In the naive debug code-quality form a new tuple gets 
allocated.  

In the implementation, the F# compiler generally does an OK-ish job of avoiding copying 
of structs - an address is often generated to an existing copy of an immutable struct.

### Avoiding needless copying in code using byrefs to structs

In higher-performance code it is normal to pass around pointers (byrefs) to structs.

For example consider

```fsharp
let f1 (x:U byref) =  
    let (U(a,b)) = x 
    a + b 
```
Which gets compiled to approximately the following using C-style syntax

```fsharp
let f1 (x : U byref ) = 
  let pi = *x
  let a = (&pi)->item1
  let b = (&pi)->item2
  a + b
```

The ``pi`` copy of the input struct is difficult to eliminate because reading the byref is seen as an effect (e.g. if the byref is accessible from other threads and is being written into).

The ideal code would be:
```fsharp
let f1 (x : U byref ) = x->item1 + x->item2
```
and this code is relatively easy to get for the equivalent code for a struct-record.  For struct-unions, the 
only way to decompose the union is through pattern matching, and the semantics of pattern matching 
is "build the input to the match then decompose it".

# Drawbacks
[drawbacks]: #drawbacks

The same as [struct records](https://github.com/fsharp/fslang-design/blob/master/FSharp-4.1/FS-1008-struct-records.md):
* People may not understand when to use the attribute, and, like inline, use it inappropriately, giving worse performance.
* People may "fiddle around" applying the attribute when performance is OK or performance gains are more likely to come via other routes
* It's one more trick for F# programmers to learn

# Alternatives
[alternatives]: #alternatives

* Require programmers to code structs by hand

# Known issues

* Assemblies build using single-case struct DUs under this RFC are [not backward-consumable by v4.0 or earlier of the F# compiler](https://github.com/neoeinstein/fsharp-struct-test/blob/master/cns.diff#L354). The IL generated by `fsc.exe` v4.0 attempts to address members and functions using `class` instead of `valuetype`, and thus produces unverifiable code. Libraries generated with single-case struct DUs SHOULD advertise that they use this construct and are incompatible when consumed by `fsc.exe` and `fsi.exe` v4.0. (See also [comments from the implementing PR](https://github.com/Microsoft/visualfsharp/pull/620#issuecomment-190580488))
