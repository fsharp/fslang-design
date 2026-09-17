# F# RFC FS-1135 - Map.binarySearch

The design suggestion [Add (binary) search function to Map and Set](https://github.com/fsharp/fslang-suggestions/issues/82) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/82)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/15107)
- [ ] Design Review Meeting(s) with @dsyme and others invitees
- [Discussion](https://github.com/dotnet/fsharp/discussions/15121)

# Summary
A function `Map.binarySearch`.

This version returns a tuple of 3 options: one for the item with the closest matching key below; one for the match; and one for the item with the closest matching key above.

# Motivation

This function provides a way to find items in a `Map` with adjacent keys.

This is useful in stepping through a list of items statelessly,
e.g. selections in an Elmish list sorted by non-contiguous ID, or "next" and "previous" posts in a blog.

In general, this provides an alternative to referencing items in an array or list by index, which can be clumsy when items move or are deleted.
It can also be said that this allows for `Map` to have the functionality of a sorted array (minus the ability to access by index),
but with the performance characteristics of a tree. Trees/maps are more memory and CPU efficient when there are frequent immutable updates.

Currently, the only way to accomplish something similar is to iterate through `Map.Keys` and perform a linear search.

# Detailed design

The function currently has the shape:
```fsharp
Map.binarySearch : 'Key -> Map<'Key, 'Value> -> ('Key, 'Value) option * ('Key, 'Value) option * ('Key, 'Value) option
```

The function is implemented in Map.fs in FSharp.Core. It should not throw any additional exceptions except `StackOverflowException` and `OutOfMemoryException`, including in the case where the Map is empty.

Example use in a hypothetical Elmish/Feliz view:

```fsharp
let blogEntries: Map<BlogEntryId, BlogEntry> = ...

let pagination = div [
  let (previous, _, next) = Map.binarySearch currentEntryId blogEntries
  match previous with
  | None -> ()
  | Some (id, entry) ->
    a [ prop.href $"/posts/{id}"
        prop.children [ text $"Previous post: {entry.title}" ]
      ]
      
  match next with
  | None -> ()
  | Some (id, entry) ->
    a [ prop.href $"/posts/{id}"
        prop.children [ text $"Next post: {entry.title}" ]
      ]
]
```

Example use deleting a blog entry in a list UI that also features a "current selection" UI feature:

```fsharp
match Msg with
| DeleteEntry idToDelete ->
  let entries = Map.remove idToDelete model.items
  let selectedEntryId =
    match Map.binarySearch selectedEntryId entries with
    | _, Some (id, _), _
      // The selected item still exists (perhaps another item was deleted using a different UI feature); keep it selected
    | Some (id, _), None, _
      // There is a previous item; selected that
    | None, None, Some (id, _)
      // There is a next item; select that
      Some id
    | None, None, None ->
      // There are no more items in the map
      None
```

# Drawbacks and Alternatives

We have discussed an alternative wherein the return value is a DU of four cases: An exact match, a match below, a match above, and a match on either side.
This would allow for eaiser pattern matching, as there are only four explicit cases to consider, as opposed to eight combinations.
The downside would be that we would be discarding potentially useful information, for example,
if an exact match is found but we are also interested in adjacent items.

We briefly considered a suite of functions: `findPrevious`, `findNext`, `tryFindPrevious`, `tryFindNext`,
which would each return just a single item or an option of an item.
We decided against this, as a single `binarySearch` function would solve all use cases,
and would have a more descriptive and familiar name.  

We have also discussed a `Map.splitAt` function,
which would return either a pair of `Seq`s, `List`s or `Map`s, each with all the items on either side of the given key.
A pair of `List`s would be an O(n) operation, whereas a `Seq` would have nearly the same performance as the currently proposed `Map.binarySearch` function,
and a `Map` may have performance somewhere between O(log n) and O(n), depending on the node's depth in the tree.

However, `Map.splitAt` would cover all of the needs that `Map.binarySearch` would and then some,
albeit with the need for some additional massaging on the part of the consumer.
For the convenience and simplicity of implementation that `Map.binarySearch` would offer over `Map.splitAt`,
it may make sense to implement both functions eventually.

# Compatibility, Performance and Culture-aware formatting

As this is a change to FSharp.Core, there should not be any breaking changes.
There are no dependencies outside of FSharp.Core/map.fs itself.

The function itself operates at O(log n), where the best alternative is O(n).

This feature does not involve any formatting or parsing.
It's unlikely that the return type would benefit from any special formatting, whether in FSI, Polyglot or `ToString`.

# Unresolved questions

- The return type: `('Key, 'Value) option * ('Key, 'Value) option * ('Key, 'Value) option` vs. a DU with 4 options
- Whether or not we should skip this and implement `Map.splitAt` instead, and if so, whether it should return lists, maps or sequences
