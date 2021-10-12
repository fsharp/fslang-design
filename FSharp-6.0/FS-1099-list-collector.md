# F# RFC FS-1099 - library support for faster computed list and array expressions

During work on FS-1087 and FS-1097, it became obvious that it's possible to build a substantially faster
`[ .. ]` and `[| .. |]` implementations. Implementing this optimization needs some library support.

This suggestion has been "approved in principle".
This RFC covers the detailed proposal for the resumable state machine support needed for this and other features.

- [x] [Approved in principle](https://github.com/fsharp/fslang-suggestions/issues/926)
- [x] [RFC Discussion](https://github.com/fsharp/fslang-design/discussions/565)
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/11592)

# Summary

Optimize runtime cost of list and array expressions. Those currently rely on the Sequence expression infrastructure.

In conjunction with [FS-1087 resumable code](FS-1087-resumable-code.md), this open alley for performance gains.

# Detailed Design 
```fsharp
namespace FSharp.Core.CompilerServices

[<Struct>]
type ListCollector<'T> =

     member Add: value: 'T -> unit

     member AddMany: valued: seq<'T> -> unit

     member AddManyAndClose: valued: seq<'T> -> 'T list

     member Close: unit -> 'T list

[<Struct>]
type ArrayCollector<'T> =

     member Add: value: 'T -> unit

     member AddMany: valued: seq<'T> -> unit

     member AddManyAndClose: valued: seq<'T> -> 'T[]

     member Close: unit -> 'T []
```

AddManyAndClose is used for a final emit of extra elements in final, tailcall position.
For F# lists, a `yield!` of a list in tailcall position simply stitches that list into
the result without copying (`AddManyAndClose` on `ListCollector<T>`).  This is valid because
lists are immutable - and we already do this for `List.append` for example.  In theory this
could reduce some `O(n)` operations to `O(1)` (though I doubt we'll see that in practice).

After `Close` is called the collectors are reset to the empty state.

Example
```fsharp
let mutable c = ListCollector<int>()
c.Add(1)
c.Add(2)
c.AddMany([2;3;4])
c.Close()

let mutable c2 = ListCollector<int>()
c2.Close()
```

# Drawbacks

None

# Unresolved questions

None
