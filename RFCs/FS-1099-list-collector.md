# F# RFC FS-1099 - list collector

During work on FS-1087 and FS-1097, it became obvious that it's possible to build a substantially fast `list { ... }` builder
of `[ .. ]` implementation if we allow FSharp.Core to include compiler/builder-use-only helpers for
implementing tail-cons mutation on F# lists.

This suggestion has been "approved in principle".
This RFC covers the detailed proposal for the resumable state machine support needed for this and other features.

- [x] Approved in principle
- [x] [RFC Discussion](https://github.com/fsharp/fslang-design/discussions/565)
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/6811)

# Summary

During work on FS-1087 and FS-1097, it became obvious that it's possible to build a substantially fast `list { ... }` builder
if we allow FSharp.Core to include compiler helpers for implementing tail-cons mutation on F# lists.

Any variation on this will require FSharp.Core library intrinsic helpers. @dsyme says it is 100% certain this will
be needed for the inlined code for faster implementations of `[ ... ]` in the future.


# Detailed Design 
```fsharp
namespace FSharp.Core.CompilerServices

[<Struct>]
type ListCollector<'T> =

     member Yield: value: 'T -> unit

     member ToList: unit -> 'T list
```

After `ToList` is called the ListCollector is reset to the empty state.

Example
```fsharp
let mutable c = ListCollector<int>()
c.Yield(1)
c.Yield(2)
c.ToList()

let mutable c2 = ListCollector<int>()
c2.ToList()
```

# Drawbacks

None

# Unresolved questions

None
