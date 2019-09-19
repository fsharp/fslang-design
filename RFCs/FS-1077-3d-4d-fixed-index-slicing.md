# F# RFC FS-1077 - Slicing for 3D/4D arrays with fixed index

The design suggestion [(link)](https://github.com/fsharp/fslang-suggestions/issues/700) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [ ] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/700)
* [ ] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/393)
* [ ] Implementation: Open


# Summary
[summary]: #summary

Right now this is possible
```
arr2d.[0, 0..1]
arr3d.[0..1, 0..1, 0..1]
```
but this is not possible.
```
arr3d.[0, 0, 0..1]
```

Right now, for slicing 3D/4D arrays, we can provide a range of indices for each of the slice dimensions, but we cannot provide a single, fixed index for any dimension.

This is because the only function we expose for 3D/4D slicing is:

```
let GetArraySlice3D (source: _[,,]) start1 finish1 start2 finish2 start3 finish3 =
let GetArraySlice4D (source: _[,,,]) start1 finish1 start2 finish2 start3 finish3 start4 finish4 = 
```

The slice function for 3D only accepts 3 sets of ranges. We cannot pass 1 range and 2 fixed indexes like in `arr3d.[0, 0, 0..1]`

This "fixed index" slicing behavior is only available for 2D arrays. This RFC proposes to enable this feature for 3D/4D arrays.


# Motivation
[motivation]: #motivation

This can be useful when we want to obtain a subarray of a smaller dimension from an array of larger dimension.

If we have the following 3D array

*z = 0*

x\y | 0 | 1 |
----|----|---
0 | 1 | 2
1 | 2 | 3

*z = 1*

x\y | 0 | 1 |
----|----|---
0 | 4 | 5
1 | 6 | 7

If we want the sequence `4,5` from this 3D array, we do not currently have a method to do so. Because the current interfaces for `GetSlice` on 3D/4D array do not let the user specify a fixed index, we cannot obtain an array slice of reduced dimension.

We should be able to obtain `4,5` from `arr3.[0, 0..1, 1]`.

# Detailed design
[design]: #detailed-design

In `prim-types.fs` we the following two function overloads for 2D arrays:


```
            let inline GetArraySlice2DFixed1 (source: _[,]) index1 start2 finish2 = 
                let bound2 = source.GetLowerBound(1)
                let start2, finish2 = ComputeSlice bound2 start2 finish2 (GetArray2DLength2 source)
                let len2 = (finish2 - start2 + 1)
                let dst = zeroCreate (if len2 < 0 then 0 else len2)
                for j = 0 to len2 - 1 do 
                    SetArray dst j (GetArray2D source index1 (start2+j))
                dst

            let inline GetArraySlice2DFixed2 (source: _[,]) start1 finish1 index2 =
                let bound1 = source.GetLowerBound(0)
                let start1, finish1 = ComputeSlice bound1 start1 finish1 (GetArray2DLength1 source) 
                let len1 = (finish1 - start1 + 1)
                let dst = zeroCreate (if len1 < 0 then 0 else len1)
                for i = 0 to len1 - 1 do 
                    SetArray dst i (GetArray2D source (start1+i) index2)
                dst

            let inline GetArraySlice2DFixed2 (source: _[,]) start1 finish1 index2 =
                let bound1 = source.GetLowerBound(0)
                let start1, finish1 = ComputeSlice bound1 start1 finish1 (GetArray2DLength1 source) 
                let len1 = (finish1 - start1 + 1)
                let dst = zeroCreate (if len1 < 0 then 0 else len1)
                for i = 0 to len1 - 1 do 
                    SetArray dst i (GetArray2D source (start1+i) index2)
                dst

            let inline SetArraySlice2DFixed1 (target: _[,]) index1 start2 finish2 (source: _[]) = 
                let bound2 = target.GetLowerBound(1)
                let start2  = (match start2 with None -> bound2 | Some n -> n) 
                let finish2 = (match finish2 with None -> bound2 + GetArray2DLength2 target - 1 | Some n -> n) 
                let len2 = (finish2 - start2 + 1)
                for j = 0 to len2 - 1 do
                    SetArray2D target index1 (bound2+start2+j) (GetArray source j)

```



We also have this wired up in `typechecker.fs` as:

```
 let info = 
                match isString, isArray, wholeExpr with 
                | false, true, SynExpr.DotIndexedGet (_, [SynIndexerArg.One(SynExpr.Tuple (false, ([_;_] as idxs), _, _))], _, _)           -> Some (indexOpPath, "GetArray2D", idxs)
                .
                .
                .
                | false, true, SynExpr.DotIndexedGet (_, [SynIndexerArg.Two _], _, _)                                            -> Some (sliceOpPath, "GetArraySlice", GetIndexArgs indexArgs)
                | false, true, SynExpr.DotIndexedGet (_, [SynIndexerArg.One _;SynIndexerArg.Two _], _, _)                        -> Some (sliceOpPath, "GetArraySlice2DFixed1", GetIndexArgs indexArgs)
                | false, true, SynExpr.DotIndexedGet (_, [SynIndexerArg.Two _;SynIndexerArg.One _], _, _)                        -> Some (sliceOpPath, "GetArraySlice2DFixed2", GetIndexArgs indexArgs)
                | false, true, SynExpr.DotIndexedSet (_, [SynIndexerArg.One _;SynIndexerArg.Two _], e3, _, _, _)                                         -> Some (sliceOpPath, "SetArraySlice2DFixed1", (GetIndexArgs indexArgs @ [e3]))
                | false, true, SynExpr.DotIndexedSet (_, [SynIndexerArg.Two _;SynIndexerArg.One _], e3, _, _, _)                                         -> Some (sliceOpPath, "SetArraySlice2DFixed2", (GetIndexArgs indexArgs @ [e3]))
                .
                .
                .
```

For this feature 20 similar overloads will have to be added to the above places. The 20 overloads are as follows:
```
3D -> 2D
[x, *, *]
[*, x, *]
[*, *, x]

3D -> 1D
[x, y, *]
[*, x, y]
[*, x, y]

4D -> 3D
[x, *, *, *]
[*, x, *, *]
[*, *, x, *]
[*, *, *, x]

4D -> 2D
[x, y, *, *]
[x, *, y, *]
[x, *, *, y]
[*, x, y, *]
[*, x, *, y]
[*, *, x, y]

4D -> 1D
[x, y, z, *]
[x, y, *, z]
[x, *, y, z]
[*, x, y, z]
```

# Drawbacks
[drawbacks]: #drawbacks

No drawbacks have been identified so far.

# Alternatives
[alternatives]: #alternatives

Perhaps a syntax like:
```
let arr1 = arr3
    |> Array3D.GetZ 0
    |> Array2D.GetX 0

arr1.[0..1]
```

# Compatibility
[compatibility]: #compatibility

* Is this a breaking change? *No*
* What happens when previous versions of the F# compiler encounter this design addition as source code? *It would error out*
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries? *Error*
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct? *Error, as the extra slicing logic isn't defined as GetSlice, so it would require the new logic in the compiler under TypeChecker to be wired up properly*


# Unresolved questions
[unresolved]: #unresolved-questions

Is there a better way to do this that doesn't involve writing 20 functions and retains good performance?
