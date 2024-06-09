# F# RFC FS-1113 - Add insert/remove/update functions for collections, also Keys/Values for Map

The design suggestion [Additions to collections for insert/update/ remove](https://github.com/fsharp/fslang-suggestions/issues/1047) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1047)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/11888)
- [x] [Discussion](https://github.com/fsharp/fslang-suggestions/issues/1047)

# Summary

1. Add insert/remove/update functions to List, Array, Seq, Map to improve them as workhorse functional collections
2. Add `Keys` and `Values` properties to Map

# Motivation

All the above are commonly useful.

# Detailed design

We add the following:

```fsharp

namespace FSharp.Collections

type Map<'Key, 'Value> =
    ....
    /// <summary>The keys in the map.</summary>
    member Keys : ICollection<'Key>

    /// <summary>All the values in the map, including duplicates.</summary>
    member Values : ICollection<'Value>

module Map =
    ....
    
    /// <summary>The keys in the map.</summary>
    [<CompiledName("Keys")>]
    val keys: table: Map<'Key, 'T> -> ICollection<'Key> 

    /// <summary>The values in the map.</summary>
    [<CompiledName("Values")>]
    val values: table: Map<'Key, 'T> -> ICollection<'T> 

module List =
    /// Return a new list with the item at a given index removed
    /// If the index is outside the range of the list then it is ignored.
    val removeAt: index: int -> source: 'T list -> 'T list

    /// Return a new list with the number of items starting at a given index removed.
    /// If index is outside 0..source.Length-1 then it is ignored.
    val removeManyAt: index: int -> count: int -> source: 'T list -> 'T list

    /// Return a new list with the item at a given index set to the new value. If 
    /// index is outside 0..source.Length-1 an exception is raised.
    val updateAt: index: int -> value: 'T -> source: 'T list -> 'T list

    /// Return a new list with a new item inserted before the given index.The index may be source.Length to
    /// include extra elements at the end of the list. If 
    /// index is outside 0..source.Length an exception is raised.
    val insertAt: index: int -> value: 'T -> source: 'T list -> 'T list

    /// Return a new list with new items inserted before the given index. The index may be source.Length to
    /// include extra elements at the end of the list. 
    /// If index is outside 0..source.Length  an exception is raised.
    val insertManyAt: index: int -> values: seq<'T> -> source: 'T list -> 'T list

module Array =
    /// Return a new array with the item at a given index removed
    /// If the index is outside the range of the array then it is ignored.
    val removeAt: index: int -> source: 'T[] -> 'T[]

    /// Return a new array with the number of items starting at a given index removed.
    /// If an implied item index is outside the range of the array then it is ignored.
    val removeManyAt: index: int -> count: int -> source: 'T[] -> 'T[]

    /// Return a new array with the item at a given index set to the new value. If 
    /// index is outside 0..source.Length-1 an exception is raised.
    val updateAt: index: int -> value: 'T -> source: 'T[] -> 'T[]

    /// Return a new array with a new item inserted before the given index. The index may be source.Length to
    /// include extra elements at the end of the array. If 
    /// index is outside 0..source.Length than source.Length an exception is raised.
    val insertAt: index: int -> value: 'T -> source: 'T[] -> 'T[]

    /// Return a new array with new items inserted before the given index. The index may be source.Length to
    /// include extra elements at the end of the array.   If index is outside 0..source.Length an exception is raised.
    val insertManyAt: index: int -> values: seq<'T> -> source: 'T[] -> 'T[]

module Seq =
    /// Return a new ssequence which, when iterated, will have the item at a given index removed
    /// If the index is outside the range of valid indexes for the sequence then it is ignored.
    val removeAt: index: int -> source: seq<'T> -> seq<'T>

    /// Return a new sequence which, when iterated, will have the given count of items starting at a given index removed.
    /// If any implied item index is outside the range of valid indexes for the sequence then it is ignored.
    val removeManyAt: index: int -> count: int -> source: seq<'T> -> seq<'T>

    /// Return a new sequence which, when iterated, will return the given item for the given index, and
    /// otherwise return the items from source. If index is below zero an exception is raised immediately.
    /// If the index is beyond the range on the source sequence the update is ignored.
    val updateAt: index: int -> value: 'T -> source: seq<'T> -> seq<'T>

    /// Return a new sequence which, when iterated, includes a new item inserted before the given index. The index may be source.Length to
    /// include extra elements at the end of the sequence. If 
    /// index is outside 0..source.Length than source.Length an exception is raised.
    val insertAt: index: int -> value: 'T -> source: seq<'T> -> seq<'T>

    /// Return a new sequence which, when iterated, will include additional items given by values, starting
    /// at the given index. The index may be source.Length to
    /// include extra elements at the end of the sequence. 
    /// If index is outside 0..source.Length an exception is raised.
    val insertManyAt: index: int -> values: seq<'T> -> source: seq<'T> -> seq<'T>
```

# Drawbacks

Adds extra functions to FSharp.Core

# Alternatives

See suggestion discussion  thread for discussion on `updateManyAt` and also other naming alternatives.

# Compatibility

Not a breaking change

# Unresolved questions

None

