# F# RFC FS-1137 - Prefer extension method over intrinsic property when arguments are provided
* [x] Approved in principle
* [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1039)
* [x] Implementation: [In progress](https://github.com/dotnet/fsharp/pull/16032)
* [x] [Discussion](https://github.com/fsharp/fslang-design/discussions/752)

# Summary

Several C# libraries define extension methods bearing the same name as property, those are generally used for fluent notation over property setter.

This RFC defines the support for extension method and type extension in overload resolution, for those properties that are unambiguously used as getter property access, when those don't allow application syntax.

# Motivation

Up to F# 8.0, there is no support for calling the extension method or type extensions if a type exposes a property with the same name, forcing the use of work arounds when consuming C# libraries leveraging this idiom.

The feature enables support for Linq members such as Count on types that have a Count property:

```fsharp
open System.Linq
let r = ResizeArray<int>()
// val r: ResizeArray<int>
r.Count
// val it: int = 0
r.Count(fun _ -> true) // Before the feature: error FS0003: This value is not a function and cannot be applied.
// val it: int = 0
```

# Detailed design

Consider `TypeWithProperty` exposing property `X`, it is possible to define an extension method or a type extension of same name, and have it resolved in the same fashion it occurs in equivalent C# code.

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

The feature won't enable resolving those extension methods / type extensions in case the properties that supports indexer(s), or the type of the property is a function (`a -> b`, etc.).

## Implementation details

When there is a "delayed application" expression adjacent to the identifier which would normally resolve to a property, the resolution considers extension methods / type extensions that are in the name resolution environment.

## Documentation

Documentation around extension methods and type extension may be updated to reflect what this RFC enables.

# Drawbacks

Due to the idiom of indexed property not being possible to express in C#, and the ambiguity with delayed application of expressions in case of properties having indexers, or being of a function type, there is no plan to support resolution of extension methods / type extensions whenever those case apply.

# Alternatives

* Writing additional library bindings that work around the limitation
* Calling to the extension method through their natural qualified name rather than as an extension method

# Unresolved questions

None.
