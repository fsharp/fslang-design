# F# RFC 1130 - (Additions to collection functions in Array.Parallel )


The design suggestion [Make FSharp.Core collection functions for Array.Parallel more regular](https://github.com/fsharp/fslang-suggestions/issues/187) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/187)
- [x] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [ ] Design Review Meeting(s) with @dsyme and others invitees
- [Discussion](https://github.com/fsharp/fslang-design/discussions/719)
   - I believe that due to the tabular nature of the content, a line-level review directly in the PR might be better then a linear discussion?

# Summary

The aim of the feature is to close the gap between function in Array.Parallel compared to the three main collection modules - List/Seq/Array.

# Motivation

We have the Array.Parallel module already and it is objectively missing a lot of functions that are essential to collection operations.
Enabling a parallel version of them will offer a performance boost to applications using it.

# Detailed design

There is unlikely to be a one-size fits all design, so let me rather write some design assumptions and principles:
- Until Fsharp.Core is a FrameworkReference, this should work under netstandard2.0
- Parallelism brought in by TPL primitives, especially Parallel.For
- Avoided for O(1) operations, where existing Array module is already good
    - (Head,Last,IsEmpty,Length,etc.)
- Avoided for "manipulative" operations like Insert,Remove,Set and it's variants
- **Not** calling into PLINQ (PSeq library is there to do it)
- Predictable allocations, obviously never mutates the input array
- For operations with a map-reduce nature (Fold,Reduce,GroupBy,CountBy and their relatives):
   - Introduce an internal chunking mechanism that works over virtualized slices of the array    
   - Without allocating those subarrays
   - Chunking based on Environment.ProcessorCount


# Drawbacks

- It is work to be tested, documented, benchmarked.
- Increases the size of Fsharp.Core
- Might actually be slower for small-sized arrays
- Operations do not have equal benefit from parallelism
    - Providing all functions might create unrealistic expectations

# Alternatives

- Not doing this at all
- Providing via a separate library

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change?
   * No
* What happens when previous versions of the F# compiler encounter this design addition as source code?
   * Nothing
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
   * Nothing
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?
   * Nothing

# Pragmatics

## Diagnostics

Does not apply to library additions.

## Tooling

Does not apply to library additions.

## Performance


* Code size
* Performance on very large collection must be better
* Performance on very small collections is expected to be slower and part of the design

## Scaling

The functions should in general work where their current siblings in Array/List/Seq work fine as well.

## Culture-aware formatting/parsing

Does not apply to collection functions.

# Unresolved questions

Which functions are considered standard?
Which functions are the most desired by the community?
This allows an incremental implementation (a few functions at a time), is that acceptable even across versions?


## Table of existing collection modules and available functions
The last column is an indication of what is expected of the Array.Parallel module.
This is where comments and suggestions are expected.

Proposed approach:
- If current cell is blank and you would like to have that function implemented in Array.Parallel, write "code suggestion" comment, than can be 1-click commited
- If current cell has a value and you disagree or are not sure, use the conversation feature to start a thread.


|Function|ArrayModule|ListModule|SeqModule|Array.Parallel|
|:----|:----|:----|:----|:----|
|AllPairs|1|1|1| |
|Append|1|1|1|Not considered|
|Average|1|1|1| |
|AverageBy|1|1|1| |
|Cache| | |1|Not relevant|
|Cast| | |1|Not relevant|
|Choose|1|1|1|1|
|ChunkBySize|1|1|1| |
|Collect|1|1|1|1|
|CompareWith|1|1|1| |
|Concat|1|1|1|Not considered|
|Contains|1|1|1| |
|Copy|1| | |Not considered|
|CopyTo|1| | |Not considered|
|CountBy|1|1|1| |
|Create|1| | |Not considered|
|Delay| | |1|Not relevant|
|Distinct|1|1|1| |
|DistinctBy|1|1|1| |
|Empty|1|1|1|Not needed (fast anyway)|
|ExactlyOne|1|1|1| |
|Except|1|1|1| |
|Exists|1|1|1|ADD|
|Exists2|1|1|1| |
|Fill|1| | |Not considered|
|Filter|1|1|1|ADD|
|Find|1|1|1| |
|FindBack|1|1|1| |
|FindIndex|1|1|1| |
|FindIndexBack|1|1|1| |
|Fold|1|1|1| Must be sequential|
|Fold2|1|1|1| Must be sequential|
|FoldBack|1|1|1| Must be sequential|
|FoldBack2|1|1|1| Must be sequential|
|ForAll|1|1|1|ADD|
|ForAll2|1|1|1| |
|Get|1|1|1|Not needed (fast anyway)|
|GetSubArray|1| | |Not needed (fast anyway)|
|GroupBy|1|1|1| |
|Head|1|1|1|Not needed (fast anyway)|
|Indexed|1|1|1| |
|Initialize|1|1|1|1|
|InitializeInfinite| | |1|Not relevant|
|InsertAt|1|1|1|Not considered|
|InsertManyAt|1|1|1|Not considered|
|IsEmpty|1|1|1|Not needed (fast anyway)|
|Item|1|1|1|Not needed (fast anyway)|
|Iterate|1|1|1|1|
|Iterate2|1|1|1| |
|IterateIndexed|1|1|1|1|
|IterateIndexed2|1|1|1| |
|Last|1|1|1|Not needed (fast anyway)|
|Length|1|1|1|Not needed (fast anyway)|
|Map|1|1|1|1|
|Map2|1|1|1| |
|Map3|1|1|1| |
|MapFold|1|1|1|Must be sequential |
|MapFoldBack|1|1|1| Must be sequential|
|MapIndexed|1|1|1|1|
|MapIndexed2|1|1|1| |
|Max|1|1|1| |
|MaxBy|1|1|1|ADD|
|Min|1|1|1| |
|MinBy|1|1|1|ADD|
|OfArray| |1|1|Not relevant|
|OfList|1| |1| |
|OfSeq|1|1| |Not considered|
|Pairwise|1|1|1| |
|Partition|1|1| |1|
|Permute|1|1|1| |
|Pick|1|1|1| |
|ReadOnly| | |1|Not relevant|
|Reduce|1|1|1|ADD|
|ReduceBack|1|1|1| |
|RemoveAt|1|1|1|Not considered|
|RemoveManyAt|1|1|1|Not considered|
|Replicate|1|1|1| |
|Reverse|1|1|1| |
|Scan|1|1|1| |
|ScanBack|1|1|1| |
|Set|1| | |Not considered|
|Singleton|1|1|1|Not needed (fast anyway)|
|Skip|1|1|1| |
|SkipWhile|1|1|1| |
|Sort|1|1|1| |
|SortBy|1|1|1| |
|SortByDescending|1|1|1| |
|SortDescending|1|1|1| |
|SortInPlace|1| | | |
|SortInPlaceBy|1| | | |
|SortInPlaceWith|1| | | |
|SortWith|1|1|1| |
|SplitAt|1|1| | |
|SplitInto|1|1|1| |
|Sum|1|1|1| |
|SumBy|1|1|1| |
|Tail|1|1|1| |
|Take|1|1|1| |
|TakeWhile|1|1|1| |
|ToArray| |1|1|Not relevant|
|ToList|1| |1|Not considered|
|ToSeq|1|1| |Not considered|
|Transpose|1|1|1| |
|Truncate|1|1|1| |
|TryExactlyOne|1|1|1|Not needed (fast anyway)|
|TryFind|1|1|1|ADD|
|TryFindBack|1|1|1| |
|TryFindIndex|1|1|1|ADD|
|TryFindIndexBack|1|1|1| |
|TryHead|1|1|1|Not needed (fast anyway)|
|TryItem|1|1|1|Not needed (fast anyway)|
|TryLast|1|1|1|Not needed (fast anyway)|
|TryPick|1|1|1|ADD|
|Unfold|1|1|1| |
|Unzip|1|1| | |
|Unzip3|1|1| | |
|UpdateAt|1|1|1| |
|Where|1|1|1| |
|Windowed|1|1|1| |
|ZeroCreate|1| | |Not considered|
|Zip|1|1|1| |
|Zip3|1|1|1| |
