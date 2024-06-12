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

There is unlikely to be a one-size fits all design, so let me  write some design assumptions and principles:
- Should work under netstandard2.0
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
- If current cell is blank and you would like to have that function implemented in Array.Parallel, write "code suggestion" comment, than can be 1-click committed
- If current cell has a value and you disagree or are not sure, use the conversation feature to start a thread.


|Function|Signature(array.fsi)|ArrayModule|ListModule|SeqModule|Array.Parallel|
|:----|:----|:----|:----|:----|:----|
|AllPairs|  array1:'T1[] -> array2:'T2[] -> ('T1 * 'T2)[]|1|1|1| |
|Append|  array1:'T[] -> array2:'T[] -> 'T[]|1|1|1|Not considered|
|Average|  array:^T[] -> ^T   |1|1|1| |
|AverageBy|  projection:('T -> ^U) -> array:'T[] -> ^U   |1|1|1| |
|Choose|  chooser:('T -> 'U option) -> array:'T[] -> 'U[]|1|1|1|1|
|ChunkBySize|  chunkSize:int -> array:'T[] -> 'T[][]|1|1|1| |
|Collect|  mapping:('T -> 'U[]) -> array:'T[] -> 'U[]|1|1|1|1|
|CompareWith|  comparer:('T -> 'T -> int) -> array1:'T[] -> array2:'T[] -> int|1|1|1| |
|Concat|  arrays:seq<'T[]> -> 'T[]|1|1|1|Not considered|
|Contains|  value:'T -> array:'T[] -> bool when 'T : equality|1|1|1| |
|Copy|  array:'T[] -> 'T[]|1| | |Not considered|
|CopyTo|  source:'T[] -> sourceIndex:int -> target:'T[] -> targetIndex:int -> count:int -> unit|1| | |Not considered|
|CountBy|  projection:('T -> 'Key) -> array:'T[] -> ('Key * int)[] when 'Key : equality|1|1|1| |
|Create|  count:int -> value:'T -> 'T[]|1| | |Not considered|
|Distinct|  array:'T[] -> 'T[] when 'T : equality|1|1|1| |
|DistinctBy|  projection:('T -> 'Key) -> array:'T[] -> 'T[] when 'Key : equality|1|1|1| |
|Empty|  'T[]|1|1|1|Not needed (fast anyway)|
|ExactlyOne|  array:'T[] -> 'T|1|1|1| |
|Except|  itemsToExclude:seq<'T> -> array:'T[] -> 'T[] when 'T : equality|1|1|1| |
|Exists|  predicate:('T -> bool) -> array:'T[] -> bool|1|1|1|ADD|
|Exists2|  predicate:('T1 -> 'T2 -> bool) -> array1:'T1[] -> array2:'T2[] -> bool|1|1|1| |
|Fill|  target:'T[] -> targetIndex:int -> count:int -> value:'T -> unit|1| | |Not considered|
|Filter|  predicate:('T -> bool) -> array:'T[] -> 'T[]|1|1|1|ADD|
|Find|  predicate:('T -> bool) -> array:'T[] -> 'T|1|1|1| |
|FindBack|  predicate:('T -> bool) -> array:'T[] -> 'T|1|1|1| |
|FindIndex|  predicate:('T -> bool) -> array:'T[] -> int|1|1|1| |
|FindIndexBack|  predicate:('T -> bool) -> array:'T[] -> int|1|1|1| |
|Fold|  folder:('State -> 'T -> 'State) -> state:'State -> array: 'T[] -> 'State|1|1|1|Must be sequential|
|Fold2|  folder:('State -> 'T1 -> 'T2 -> 'State) -> state:'State -> array1:'T1[] -> array2:'T2[] -> 'State|1|1|1|Must be sequential|
|FoldBack|  folder:('T -> 'State -> 'State) -> array:'T[] -> state:'State -> 'State|1|1|1|Must be sequential|
|FoldBack2|  folder:('T1 -> 'T2 -> 'State -> 'State) -> array1:'T1[] -> array2:'T2[] -> state:'State -> 'State|1|1|1|Must be sequential|
|ForAll|  predicate:('T -> bool) -> array:'T[] -> bool|1|1|1|ADD|
|ForAll2|  predicate:('T1 -> 'T2 -> bool) -> array1:'T1[] -> array2:'T2[] -> bool|1|1|1| |
|Get|  array:'T[] -> index:int -> 'T|1|1|1|Not needed (fast anyway)|
|GetSubArray|  array:'T[] -> startIndex:int -> count:int -> 'T[]|1| | |Not needed (fast anyway)|
|GroupBy|  projection:('T -> 'Key) -> array:'T[] -> ('Key * 'T[])[]  when 'Key : equality|1|1|1|ADD|
|Head|  array:'T[] -> 'T|1|1|1|Not needed (fast anyway)|
|Indexed|  array:'T[] -> (int * 'T)[]|1|1|1| |
|Initialize|  count:int -> initializer:(int -> 'T) -> 'T[]|1|1|1|1|
|InsertAt|  index: int -> value: 'T -> source: 'T[] -> 'T[]|1|1|1|Not considered|
|InsertManyAt|  index: int -> values: seq<'T> -> source: 'T[] -> 'T[]|1|1|1|Not considered|
|IsEmpty|  array:'T[] -> bool|1|1|1|Not needed (fast anyway)|
|Item|  index:int -> array:'T[] -> 'T|1|1|1|Not needed (fast anyway)|
|Iterate|  action:('T -> unit) -> array:'T[] -> unit|1|1|1|1|
|Iterate2|  action:('T1 -> 'T2 -> unit) -> array1:'T1[] -> array2:'T2[] -> unit|1|1|1| |
|IterateIndexed|  action:(int -> 'T -> unit) -> array:'T[] -> unit|1|1|1|1|
|IterateIndexed2|  action:(int -> 'T1 -> 'T2 -> unit) -> array1:'T1[] -> array2:'T2[] -> unit|1|1|1| |
|Last|  array:'T[] -> 'T|1|1|1|Not needed (fast anyway)|
|Length|  array:'T[] -> int|1|1|1|Not needed (fast anyway)|
|Map|  mapping:('T -> 'U) -> array:'T[] -> 'U[]|1|1|1|1|
|Map2|  mapping:('T1 -> 'T2 -> 'U) -> array1:'T1[] -> array2:'T2[] -> 'U[]|1|1|1| |
|Map3|  mapping:('T1 -> 'T2 -> 'T3 -> 'U) -> array1:'T1[] -> array2:'T2[] -> array3:'T3[] -> 'U[]|1|1|1| |
|MapFold|  mapping:('State -> 'T -> 'Result * 'State) -> state:'State -> array:'T[] -> 'Result[] * 'State|1|1|1|Must be sequential|
|MapFoldBack|  mapping:('T -> 'State -> 'Result * 'State) -> array:'T[] -> state:'State -> 'Result[] * 'State|1|1|1|Must be sequential|
|MapIndexed|  mapping:(int -> 'T -> 'U) -> array:'T[] -> 'U[]|1|1|1|1|
|MapIndexed2|  mapping:(int -> 'T1 -> 'T2 -> 'U) -> array1:'T1[] -> array2:'T2[] -> 'U[]|1|1|1| |
|Max|  array:'T[] -> 'T  when 'T : comparison |1|1|1| |
|MaxBy|  projection:('T -> 'U) -> array:'T[] -> 'T when 'U : comparison |1|1|1|ADD|
|Min|  array:'T[] -> 'T  when 'T : comparison |1|1|1| |
|MinBy|  projection:('T -> 'U) -> array:'T[] -> 'T when 'U : comparison |1|1|1|ADD|
|OfList|  list:'T list -> 'T[]|1| |1| |
|OfSeq|  source:seq<'T> -> 'T[]|1|1| |Not considered|
|Pairwise|  array:'T[] -> ('T * 'T)[]|1|1|1| |
|Partition|  predicate:('T -> bool) -> array:'T[] -> 'T[] * 'T[]|1|1| |1|
|Permute|  indexMap:(int -> int) -> array:'T[] -> 'T[]|1|1|1| |
|Pick|  chooser:('T -> 'U option) -> array:'T[] -> 'U |1|1|1| |
|Reduce|  reduction:('T -> 'T -> 'T) -> array:'T[] -> 'T|1|1|1|ADD|
|ReduceBack|  reduction:('T -> 'T -> 'T) -> array:'T[] -> 'T|1|1|1| |
|RemoveAt|  index: int -> source: 'T[] -> 'T[]|1|1|1|Not considered|
|RemoveManyAt|  index: int -> count: int -> source: 'T[] -> 'T[]|1|1|1|Not considered|
|Replicate|  count:int -> initial:'T -> 'T[]|1|1|1| |
|Reverse|  array:'T[] -> 'T[]|1|1|1| |
|Scan|  folder:('State -> 'T -> 'State) -> state:'State -> array:'T[] -> 'State[]|1|1|1| |
|ScanBack|  folder:('T -> 'State -> 'State) -> array:'T[] -> state:'State -> 'State[]|1|1|1| |
|Set|  array:'T[] -> index:int -> value:'T -> unit|1| | |Not considered|
|Singleton|  value:'T -> 'T[]|1|1|1|Not needed (fast anyway)|
|Skip|  count:int -> array:'T[] -> 'T[]|1|1|1| |
|SkipWhile|  predicate:('T -> bool) -> array:'T[] -> 'T[]|1|1|1| |
|Sort|  array:'T[] -> 'T[] when 'T : comparison |1|1|1|ADD|
|SortBy|  projection:('T -> 'Key) -> array:'T[] -> 'T[] when 'Key : comparison |1|1|1|ADD|
|SortByDescending|  projection:('T -> 'Key) -> array:'T[] -> 'T[] when 'Key : comparison|1|1|1|ADD|
|SortDescending|  array:'T[] -> 'T[] when 'T : comparison|1|1|1|ADD|
|SortInPlace|  array:'T[] -> unit when 'T : comparison |1| | | |
|SortInPlaceBy|  projection:('T -> 'Key) -> array:'T[] -> unit when 'Key : comparison |1| | | |
|SortInPlaceWith|  comparer:('T -> 'T -> int) -> array:'T[] -> unit|1| | | |
|SortWith|  comparer:('T -> 'T -> int) -> array:'T[] -> 'T[]|1|1|1| |
|SplitAt|  index:int -> array:'T[] -> ('T[] * 'T[])|1|1| | |
|SplitInto|  count:int -> array:'T[] -> 'T[][]|1|1|1| |
|Sum|  array: ^T[] -> ^T |1|1|1|ADD|
|SumBy|  projection:('T -> ^U) -> array:'T[] -> ^U |1|1|1| |
|Tail|  array:'T[] -> 'T[]|1|1|1| |
|Take|  count:int -> array:'T[] -> 'T[]|1|1|1| |
|TakeWhile|  predicate:('T -> bool) -> array:'T[] -> 'T[]|1|1|1| |
|ToList|  array:'T[] -> 'T list|1| |1|Not considered|
|ToSeq|  array:'T[] -> seq<'T>|1|1| |Not considered|
|Transpose|  arrays:seq<'T[]> -> 'T[][]|1|1|1| |
|Truncate|  count:int -> array:'T[] -> 'T[]|1|1|1| |
|TryExactlyOne|  array:'T[] -> 'T option|1|1|1|Not needed (fast anyway)|
|TryFind|  predicate:('T -> bool) -> array:'T[] -> 'T option|1|1|1|ADD|
|TryFindBack|  predicate:('T -> bool) -> array:'T[] -> 'T option|1|1|1| |
|TryFindIndex|  predicate:('T -> bool) -> array:'T[] -> int option|1|1|1|ADD|
|TryFindIndexBack|  predicate:('T -> bool) -> array:'T[] -> int option|1|1|1| |
|TryHead|  array:'T[] -> 'T option|1|1|1|Not needed (fast anyway)|
|TryItem|  index:int -> array:'T[] -> 'T option|1|1|1|Not needed (fast anyway)|
|TryLast|  array:'T[] -> 'T option|1|1|1|Not needed (fast anyway)|
|TryPick|  chooser:('T -> 'U option) -> array:'T[] -> 'U option|1|1|1|ADD|
|Unfold|  generator:('State -> ('T * 'State) option) -> state:'State -> 'T[]|1|1|1| |
|Unzip|  array:('T1 * 'T2)[] -> ('T1[] * 'T2[])|1|1| | |
|Unzip3|  array:('T1 * 'T2 * 'T3)[] -> ('T1[] * 'T2[] * 'T3[])|1|1| | |
|UpdateAt|  index: int -> value: 'T -> source: 'T[] -> 'T[]|1|1|1| |
|Where|  predicate:('T -> bool) -> array:'T[] -> 'T[]|1|1|1| |
|Windowed|  windowSize:int -> array:'T[] -> 'T[][]|1|1|1| |
|ZeroCreate|  count:int -> 'T[]|1| | |Not considered|
|Zip|  array1:'T1[] -> array2:'T2[] -> ('T1 * 'T2)[]|1|1|1|ADD|
|Zip3|  array1:'T1[] -> array2:'T2[] -> array3:'T3[] -> ('T1 * 'T2 * 'T3)[]|1|1|1| |

