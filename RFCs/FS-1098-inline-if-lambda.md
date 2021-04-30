# F# RFC FS-1098 - inline-if-lambda attributes on parameters

The design suggestion "Inline if lambda attributes on parameters" is approved in principle. This RFC covers the detailed proposal for this suggestion.

- [x] Approved in principle
- [x] [Suggestion](https://github.com/fsharp/fslang-design/blob/master/RFCs/FS-1087-resumable-code.md#potential-for-over-use)
- [x] [RFC Discussion](https://github.com/fsharp/fslang-design/discussions/549)
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/6811)  (is part of this overall PR)

# Summary

We add an attribute `InlineIfLambdaAtttribute` to use on parameters of inlined functions and methods, indicating to the compiler that, if a lambda
is supplied as a parameter, it should be inlined.

# Motivation

High-performance code is often easiest achieved by inlining and reducing library code using standard F# code optimizations.  However, when lambdas are passed as parameters
they are often not inlined because the compiler deems them too large.  This causes closure allocations.

This problem is particularly chronic for F# computation expression  builders which compute functions and pass them as functions.

This problem is particularly motivated by the fact that [RFC FS-1087](https://github.com/fsharp/fslang-design/blob/master/RFCs/FS-1087-resumable-code.md#potential-for-over-use) proposes to perform more aggressive inlining for resumable code by default.  This raises the likelihood (a.k.a. certainty) that people would start to use resumable code to achieve higher performance for synchronous code, which would be a tragedy of epic proportions, resulting in an endless sea of unreadable and unmaintainable high performance code.


# Detailed design

We add `InlineIfLambdaAtttribute` to use on parameters of inlined functions and methods, indicating to the compiler that, if a lambda
is supplied as a parameter, it should be inlined.

The attribute is indicative only and may be ignored by the F# compiler.

The attribute need only be present in an implementation file, as it forms part of the logic of the inlined annotation.  It is optional in a signature.  If used
in the signature it must be present in the implementation. 

# Drawbacks

None

# Alternatives

### Use the `inline` keyword

One design option is to use the inline keyword at parameter position.  However this is misleading.

1. `inline` in F# has semantic meaning, allowing further generalization to take place.  
2. The `inline` doesn't always apply even in statically compiled code.
3. There are some small potential compatibility issues to consider if we allowed `inline` on parameter position
 
   ```fsharp
   let map (inline (f: int -> int)) xs  = ....  
   ```
 
   Here "inline" is outside the part of the pattern syntax that is about type annotations.   Is this allowed or not?  If inline is "outside" the pattern syntax then it makes sense.   However it's probably not what we want, preferring 
 
 
   ```fsharp
   let map (inline f: int -> int) xs  = ....  
   ```
 
   where the "inline" associates with the "f".  

   Currently in the parse, `inline` comes before `mutable` (though they are later determined to be mutually incompatible.  For mutable, we allow
   
   ```fsharp
   let mutable (a, b) = (1, 2)
   ```
   
   which makes both `a` and `b` mutable.  So `mutable` associates outside the pattern, not with each identifier.
 
Putting these together an attribute seems more appropriate, as this is much more like the `AggressiveInliningAttribute` of .NET, rather than a semantic
part of the F# language.

### Use on non-lambda values

Potentially this could be used on non-lambda arguments, requesting that the value be duplicated and copied if optimization information is known for it, and this
optimization information is never trimmed.  For example, on values of union type.   


# Compatibility

This is a backward compatible addition.

# Unresolved questions

* [x] Do we use this through FSharp.Core where existing inline functions take function parameters?  Answer: Yes, we will, however in a separate PR and not directly part of RFC
* [x] Is a warning or error given if used on a parameter of non-inline functions?   Answer: Yes, we will emit an error.


