# F# RFC FS-1055 -  subsumption for `yield` in sequence/list/array expressions 

* [x] Approved in principle 
* [x] Implementation: [Complete](https://github.com/Microsoft/visualfsharp/pull/4930)
* [x] Discussion: https://github.com/fsharp/fslang-design/issues/299

# Summary
[summary]: #summary

Previously, `yield` in sequence/array/list expressions might need an upcast where the corresponding simple expression did not. This upcast should no longer be required.

# Motivation
[motivation]: #motivation

Inserting upcasts is required in some places in F# code. However, this should be as regular and predictable as possible.

For example, the following example demonstrates a case where it's not obvious that an upcast is required:

```fsharp
let x0 : obj list  = [ "a" ] // ok
let x1 : obj list  = [ "a"; "b" ] // ok
let x2 : obj list  = [ yield "a" ] // was not ok
let x3 : obj list  = [ yield "a" :> obj ] // ok
```

It is arguable that this upcast should never be required. And indeed, if the nominal type is known by type inference, it should never be needed.

# Detailed design
[design]: #detailed-design

The design is to do what is already done for list and array expressions since F# 3.1: assess the known type of the list/array/sequence expression before it is analyzed. If the element type is known and nominal, then use it as the basis for inserting coercions automatically.

This is done by "inserting a flexible type varible". If the known element type is `C` then the known type of the element expression becomes type inference variable `?T` with constraint `?T :> C`. For sealed types this is solved immedaitely. For non-sealed types,the constraint remains. 

In addition, we need to avoid extra errors in situations like this:

```fsharp
let g2 () : System.Reflection.MemberInfo[] = 
    [| yield (typeof<int> :> _) |]
```

The known type of `typeof<int> :> _` is now a type inference variable `?T1`, and the `_` is given this type. But in F#, an error is given if you have a coercion expression from one type inference variable directly to another. We suppress this error in the case of the type inference variables introduced by this RFC, we call these "FlexCompat" variables in the implementation.  

FlexCompat variables (and ones they are equated with) are never generalized to avoid making more code generic than in previous releases of F# (this can be a breaking change if signature files are used, or for interop purposes). For example:

```fsharp
let g3 xs : System.Reflection.MemberInfo[] = 
    [| for x in xs do yield x |]
```

Here the type is:

```fsharp
g3: seq<MemberInfo> -> MemberInfo[]
```

and not:

```fsharp
g3: seq<#MemberInfo> -> MemberInfo[]
```

No warning is added for existing redudant casts. This could be added in a future release once a `/langlevel` flag is in place, it is a one line change to add such a warning.

# Drawbacks
[drawbacks]: #drawbacks

Error messages may change slightly.

# Alternatives
[alternatives]: #alternatives

Not doing this.

# Compatibility
[compatibility]: #compatibility

The additions are backwards compatible.

# Unresolved questions
[unresolved]: #unresolved-questions

None.
