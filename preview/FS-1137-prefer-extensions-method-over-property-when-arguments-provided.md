# F# RFC FS-1137 - Prefer extension method over intrinsic property when arguments are provided
* [x] Approved in principle
* [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1039)
* [x] Implementation: [In progress](https://github.com/dotnet/fsharp/pull/16032)
* [x] [Discussion](https://github.com/fsharp/fslang-design/discussions/752)

# Summary

Several C# libraries define extension methods bearing the same name as property, those are generally used for fluent notation over property setter.

This RFC defines the support for extension method and type extension in overload resolution, for those properties that normally are unambiguously used without further application syntax.

# Motivation

F# 8.0 doesn't support calling the extension method or type extensions in such cases, forcing the use of work arounds when consuming C# libraries leveraging this idiom.

This will enable support for Linq members such as Count on types that have a Count property:

```fsharp
open System.Linq
let r = ResizeArray<int>()
// val r: ResizeArray<int>
r.Count
// val it: int = 0
r.Count(fun _ -> true)
// val it: int = 0
```

# Detailed design

```fsharp
open System.Runtime.CompilerServices
type TypeWithProperty () =
  let mutable x = 0
  member _.X
    with get () = x
    and set value = x <- value

[<Extension>]
type TypeWithPropertyExt =
    [<Extension>]
    static member X (t: TypeWithProperty, i: int) = t.X <- i; t

let t = TypeWithProperty()
t.X // 0
t.X <- 1
t.X // 1
t.X(2).X(3).X(4) // fluent calls to the extension method
t.X // 4
```
## Implementation details

## Documentation

# Drawbacks

# Alternatives

# Unresolved questions

