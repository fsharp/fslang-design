# F# RFC FS-1055 - align subsumption for `yield` in sequence/list/array expressions with simple expressions


* [x] Approved in principle 
* [x] Implementation: [Ready](https://github.com/Microsoft/visualfsharp/pull/4930)
* Discussion: TBD

# Summary
[summary]: #summary

Previously `yield` in sequence/array/list expressions might need an upcast where the corresponding simple expression did not.
This fixes this problem

# Motivation
[motivation]: #motivation

Inserting upcasts is required in some places in F# code. However this should be as regular and intuitive as possible.

F# array and list computation expressions are expressive. However when you start to use yield you have to insert coercions, e.g.

```fsharp
let x0 : obj list  = [ "a" ] // ok
let x1 : obj list  = [ "a"; "b" ] // ok
let x2 : obj list  = [ yield "a" ] // was not ok
let x3 : obj list  = [ yield "a" :> obj ] // ok
```
If the nominal type is known by type inference this should not be needed
since the corresponding coercion is not needed for the simpler form.


# Detailed design
[design]: #detailed-design

The design is to do the thing that is already done for simple list and array expressions since F# 3.1: assess the known type of the
list/array/sequence expression before it is analyzed. If the element type is known and nominal, then use it as the basis for inserting
coercions automatically.

This is done by "inserting a flexible type varible". If the known element type is `C` then the known type of the element expression
becomes type inference variable `?T` with constraint `?T :> C`.  For sealed types this is solved immedaitely. For non-sealed types
the constraint remains. 


In addition, we need to avoid extra errors in situations like this:
```
let g2 () : System.Reflection.MemberInfo[] = 
    [| yield (typeof<int> :> _) |]
```
The known type of `typeof<int> :> _` is now a type inference variable `?T1`, and the `_` is given this type.  But in F#
an error is given if you have a coercion expression from one type inference variable directly to another. We suppress this
error in the case of the type inference variables introduced by this RFC, we call these "FlexCompat" variables in the implementation.

# Drawbacks
[drawbacks]: #drawbacks

* Error messages may change slightly

# Alternatives
[alternatives]: #alternatives

* Don't do this

# Compatibility
[compatibility]: #compatibility

The additions are backwards compatible.  


# Unresolved questions
[unresolved]: #unresolved-questions

* Consider if there are cases where flex-compat variables get generalized (without being re-condensesed),
  and if they should always be non-generalized. Possible example:

```fsharp
let g3 xs : System.Reflection.MemberInfo[] = 
    [| for x in xs do yield x |]
```

Is this type `g3: seq<#MemberInfo> -> MemberInfo[]` or  `g3: seq<MemberInfo> -> MemberInfo[]`?

