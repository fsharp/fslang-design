# F# RFC FS-1092- Add Map.maxKeyValue and Map.minKeyValue functions

The design suggestion [Add Map.maxBinding and Map.minBinding functions](https://github.com/fsharp/fslang-suggestions/issues/933) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] Approved in principle
- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/933)
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [ ] Design Review Meeting(s) with @dsyme and others invitees
- [Discussion](https://github.com/fsharp/fslang-design/issues/PLEASE-ADD-A-DISCUSSION-ISSUE-AND-LINK-HERE)

# Summary

This feature extends the `Map` module by adding two functions, tentatively named
`Map.maxKeyValue` and `Map.minKeyValue`. Those functions allow users to look up
the current minimum/maximum key-value pair from a map without having to iterate
through the whole key-value pairs.

# Motivation

Since the `Map` represents an *ordered* map, users often expect to find or use
the largest/smallest key from the map. Consider the following example where
there is a hypothetical database implementation that stores age-name mappings.

```fsharp
let db: Map<uint32, string list> = Map.empty

let addMember age name db =
  match Map.tryFind age db with
  | Some names -> Map.add age (name :: names) db
  | None -> Map.add age [ name ] db

let findLargestAge db =
  (* Hypothetical example usage of maxKeyValue *)
  Map.maxKeyValue db |> fst

let main () =
  (* Add members to the db *)
  let db = addMember 20 "Alice"
  let db = addMember 20 "Bob"
  let db = addMember 42 "Charlie"
  (* Find the largest age in the db *)
  findLargestAge db |> printfn "%d"
```

In this case, we would like to find out the largest age in the
database. Currently the only way to get the largest age is to use `Map.filter`
or `Map.fold`, which requires iterating the whole entries in the worst case.

# Detailed design

Since the `Map` is implementing the traditional self-balancing persistent tree,
one can implement this by simply traversing the internal tree. The complexity
should be `O(log n)`. For example, in order to find the minimum key-value pair,
one can recursively follow the "left" of the tree.

In the case where the given map is empty, both the functions should raise
`KeyNotFoundException` as in
https://github.com/dotnet/fsharp/blob/main/src/fsharp/FSharp.Core/map.fs#L158.

To gracefully handle exceptions without `try-with`, we may consider adding
`tryMinKeyValue` and `tryMaxKeyValue`, too. These functions are only useful when
the given map is empty, so this is not absolutely necessary.

Example code:

```fsharp
let x = Map.maxKeyValue 42 db // Return the result or raise an exception
let x = Map.maxKeyValue 42 Map.empty // This will raise an exception
```

# Drawbacks

I don't see any drawbacks of having this feature.

# Alternatives

What other designs have been considered? What is the impact of not doing this?

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change? No
* What happens when previous versions of the F# compiler encounter this design addition as source code? No problem.
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries? No problem.
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct? No problem.


# Unresolved questions

What parts of the design are still TBD?
