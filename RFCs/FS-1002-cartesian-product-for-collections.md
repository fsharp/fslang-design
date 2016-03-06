
# F# RFC FS-1002 - Cartesian product function for collections

The design suggestion [cartesian product for collections in FSharp.Core](http://fslang.uservoice.com/forums/245727-f-language/suggestions/7184398-cartesian-product-function-for-collections) has been marked "approved in principle".  
This RFC covers the detailed proposal for this suggestion.

[Discussion thread](https://github.com/fsharp/FSharpLangDesign/issues/47)

* [x] Approved in principle
* [ ] Details: [under discussion](https://github.com/fsharp/FSharpLangDesign/issues/47)
* [x] Implementation: [prototype submitted](https://github.com/Microsoft/visualfsharp/pull/989)

### Introduction

It's often useful to compute the Cartesian product (cross join) of two collections. Today users up writing something like this:

```fsharp
let cross xs ys = seq { for x in xs do for y in ys -> x, y  }
```

It would be useful to have this in the FSharp.Core standard collection modules.  It would be added to 
the Seq, List and Array modules in FSharp.Core.

### Naming 

The name of the function has not been finalized.  Some of the suggestions are

* ``cross``
* ``product``
* ``crossProduct``
* ``allPairs``

### Performance considerations

The standard range of performance considerations for F# library functions apply.

### Testing considerations

The standard range of testing  considerations for F# library functions apply.


