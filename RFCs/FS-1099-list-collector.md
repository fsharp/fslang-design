# F# RFC FS-1099 - faster computed list and array expressions

During work on FS-1087 and FS-1097, it became obvious that it's possible to build a substantially faster
`[ .. ]` and `[| .. |]` implementations.

This suggestion has been "approved in principle".
This RFC covers the detailed proposal for the resumable state machine support needed for this and other features.

- [x] Approved in principle
- [x] [RFC Discussion](https://github.com/fsharp/fslang-design/discussions/565)
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/6811)

# Summary

TBD

# Detailed Design 
```fsharp
namespace FSharp.Core.CompilerServices

[<Struct>]
type ListCollector<'T> =

     member Add: value: 'T -> unit

     member Close: unit -> 'T list

[<Struct>]
type ArrayCollector<'T> =

     member Add: value: 'T -> unit

     member Close: unit -> 'T []
```


After `Close` is called the collectors are reset to the empty state.

Example
```fsharp
let mutable c = ListCollector<int>()
c.Add(1)
c.Add(2)
c.Close()

let mutable c2 = ListCollector<int>()
c2.Close()
```

# Drawbacks

None

# Unresolved questions

None
