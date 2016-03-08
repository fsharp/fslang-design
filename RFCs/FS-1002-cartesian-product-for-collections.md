
# F# RFC FS-1002 - Cartesian product function for collections

The design suggestion [cartesian product for collections in FSharp.Core](http://fslang.uservoice.com/forums/245727-f-language/suggestions/7184398-cartesian-product-function-for-collections) has been marked "approved in principle".  
This RFC covers the detailed proposal for this suggestion.

[Discussion thread](https://github.com/fsharp/FSharpLangDesign/issues/47)

* [x] Completed
* [x] Details: [finalized](https://github.com/fsharp/FSharpLangDesign/issues/47)
* [x] Implementation: [submitted](https://github.com/Microsoft/visualfsharp/pull/989)

### Introduction

It's often useful to compute the Cartesian product (cross join) of two collections. Today users up writing something like this:

```fsharp
let allPairs xs ys = seq { for x in xs do for y in ys -> x, y  }
```

It would be useful to have this in the FSharp.Core standard collection modules.  It would be added to 
the Seq, List and Array modules in FSharp.Core.

#### Order

The implementation guarantees the order of the results to follow the following scheme:

```fsharp
let xs = [x1 .. xN]
let ys = [y1 .. yM]
cross xs ys =
    [x1, y1; x1, y.; x1, yM;
     x., y1; x., y.; x., yM;
     xN, yM; xN, y.; xN, yM]
```

### Naming 

The final name is ``allPairs``.  Some of the suggestions are

* ``cross``
* ``product``
* ``crossProduct``
* ``allPairs``


### Performance considerations

The standard range of performance considerations for F# library functions apply.

### Testing considerations

The standard range of testing  considerations for F# library functions apply.


