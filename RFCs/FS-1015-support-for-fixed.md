# F# RFC FS-NNNN - (Fill me in with a feature name)

The design suggestion [Add suppor for 'fixed'](https://fslang.uservoice.com/forums/245727-f-language/suggestions/5663721-add-support-for-fixed) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [ ] Details: [under discussion](https://github.com/fsharp/FSharpLangDesign/issues/FILL-ME-IN)
* [ ] Implementation: [In progress](https://github.com/Microsoft/visualfsharp/pull/FILL-ME-IN)


# Summary
[summary]: #summary

The .NET CIL specification includes the ability to tag an pointer-typed local as "pinned".  Values pointed to by such 
stack-based pointers are automatically pinned through a process that is more efficient (and simple to use) than creating a GCHandle.  This surfaces in the C# language as [fixed](https://msdn.microsoft.com/en-us/library/f58wzh21.aspx).  

This RFC proposes to add matching support to the F# language.



# Motivation
[motivation]: #motivation

Mainly for parity with .NET features.  Pinned/fixed is more efficient than creating a GCHandle. 

# Detailed design
[design]: #detailed-design

The keyword ``fixed`` is already reserved. The suggestion is that '`fixed`' be made a proper keyword. Then ``use p = fixed addr`` is used to pin an address or string.  This is not a try/finally IDisposable, but is just being used to scope the ``fixed``.  For example:

Pinning objects:

```fsharp

type Point = { mutable x : int; mutable y : int }

let pinObject() = 
    let point = Point(1.0,2.0);
    use p1 = fixed &point.x
    ... // some code that uses p1
```

Pinning arrays:
```

let pinArray1() = 
    let arr = [| 0; 1.5; 2.3; 3.4; 4.0; 5.9 |]
    use p1 = fixed arr
    ...  // some code that uses p1

let pinArray2() = 
    let arr = [| 0; 1.5; 2.3; 3.4; 4.0; 5.9 |]
    // You can initialize a pointer by using the address of a variable. 
    use p = fixed &arr.[0]
    ...   // some code that uses p
```
Pinning strings
```
let pinString() = 
    let str = "Hello World"
    // The following assignment initializes p by using a string.
    use pChar = str
    ... // some coe that uses pChar, which has type char*
```

Like all pointer code, this is an unsafe feature.  A warning will be emitted when it is used.

``fixed`` can't occur in any other position except on the immediate right of a ``use``.

# Drawbacks
[drawbacks]: #drawbacks

None really.

# Alternatives
[alternatives]: #alternatives

Different syntax would be possible, but the above looks reasonable.

# Unresolved questions
[unresolved]: #unresolved-questions

None
