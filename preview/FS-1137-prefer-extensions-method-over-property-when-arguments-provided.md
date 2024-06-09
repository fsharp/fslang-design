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

## Properties that cannot be shadowed

The feature won't enable resolving those extension methods / type extensions in case the properties that supports indexer(s), or the type of the property is a function (`a -> b`, etc.).

## Support for type extension

The reasonning for also supporting type extensions (which C# does not support/are F# specific) is to keep extension methods and methods defined in a type extension conceptually identical from standpoint of F# consumer.

## Support for static properties

The reasonning for also supporting shadowing of static properties is to avoid introducing disparity against instance and static properties and the ability to use shadowing through type extensions (there is no such thing as extension method seen as static member).

## Implementation details

When there is a "delayed application" expression adjacent to the identifier which would normally resolve to a property, the resolution considers extension methods / type extensions that are in the name resolution environment.

## Documentation

Documentation around extension methods and type extension may be updated to reflect what this RFC enables.

## Tooling

Editors have to decide how they want to list the members, the `DeclarationListInfo` type of FCS is being updated to not merge extension methods that may bear the same name as properties anymore in order to make the support look the same as what C# editor in VS does (property and extension methods having the same name are listed as separate intellisense entries).

The implementation detail for this change carries a slight risk of having separate entries for unexpected members (in presence of methods defined in type extension or extension method) while before the feature, only the first entry by name would show.

Non-regression tests can be added and the implementation detail adjusted, in case such an issue surfaces.

Such an issue isn't expected to be disastrous to the user experience, while not having the separate entries make the feature much less discoverable.

# Drawbacks

## Properties that cannot be shadowed

Due to the idiom of indexed property not being possible to express in C#, and the ambiguity with delayed application of expressions in case of properties having indexers, or being of a function type, there is no plan to support resolution of extension methods / type extensions whenever those cases apply. This introduces a subtle inconsistency among properties that can be defined on F# types.

## The features renders obsolete the lack of support for intrinsic methods of same name as a property

In terms of language design, beside subtleties in overload resolution pertaining to methods, there is little reasons to distinguish intrinsic methods versus extension methods or methods defined in type extensions. Yet, C#, VB.NET and F# still preclude for methods to be defined with the same name as a property, despite:
* they can't bear the same name in the IL (properties have a `get_` or `set_` prefix adorned to the IL methods implementation of getter and setter)
* they can be defined as extension methods in C# & VB.NET, and in F# with this feature, as method in type extension and extension methods

It may make sense to lift this restriction in case C# moves in this direction.

In meantime, attempting to define such intrinsic methods still results in 

>  error FS0434: The property '{property name}' has the same name as a method in type '{type name}'.

## FCS behaviour change in items returned `DeclarationListInfo`

* Code consuming FCS, that expect the list returned by `DeclarationListInfo` to never contain entries with the same name, will have such assumption being broken
* IDE that rely on `DeclarationListInfo` and intend to behave a certain way that makes the feature non discoverable, will require extra logic
* There is a risk that the implementation choice for the change in `DeclarationListInfo` missed edge cases that would result in duplicate entries

# Alternatives

* Writing additional library bindings that work around the limitation
* Calling to the extension method through their natural qualified name rather than as an extension method

# Unresolved questions

None.
