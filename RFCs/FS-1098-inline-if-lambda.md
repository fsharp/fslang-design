# F# RFC FS-1097 - inline-if-lambda attributes on parameters

The design suggestion "Inline if lambda attributes on parameters" is approved in principle. This RFC covers the detailed proposal for this suggestion.

- [x] Approved in principle
- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/581)
- [x] [RFC Discussion](https://github.com/fsharp/fslang-design/issues/455)
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/6811)

# Summary

We add an attribute `InlineIfLambdaAtttribute` to use on parameters of inlined functions and methods, indicating to the compiler that, if a lambda
is supplied as a parameter, it should be inlined.

# Motivation

High-performance code is often easiest achieved by inlining and reducing library code using standard F# code optimizations.  However, when lambdas are passed as parameters
they are often not inlined because the compiler deems them too large.  This causes closure allocations.

This problem is particularly chronic for F# computation expression  builders which compute functions and pass them as functions.

# Detailed design

We add `InlineIfLambdaAtttribute` to use on parameters of inlined functions and methods, indicating to the compiler that, if a lambda
is supplied as a parameter, it should be inlined.

The attribute is indicative only and may be ignored by the F# compiler.


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

# Compatibility

This is a backward compatible addition.

# Unresolved questions

* [ ] Do we use this through FSharp.Core?  Yes, I believe so.


