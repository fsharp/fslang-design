# F# RFC FS-1068 - Open Type Declaration

The design suggestion [Open static classes](https://github.com/fsharp/fslang-suggestions/issues/383) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.


[Discussion thread](https://github.com/fsharp/fslang-design/issues/352)

* [x] Approved in principle
* [x] Details: [Resolved to Preview](https://github.com/fsharp/fslang-design/issues/352)
* [x] Implementation: [Complete to Preview](https://github.com/dotnet/fsharp/pull/6325)


## Summary
[summary]: #summary

Add support for opening types to gain access to static members and nested types, e.g. `open type System.Math`. For example

```fsharp
> open type System.Math;;
> Min(1.0, 2.0);;
val it : float = 1.0

> Min(2.0, 1.0);;
val it : float = 1.0
```

## Motivation
[motivation]: #motivation

This greatly increases the expressivity of F# DSLs by allowing method-API facilities such as named arguments, optional arguments and type-directed overloading to be used in the DSL design.

Type providers can provide static classes, hence this would allow type providers to provide "unqualified" names. This useful for some projections where the natural thing is unqualified, e.g. the "R" type provider always required `R.plot` etc. With this feature, `R.plot` can now be `plot`.

Additionally, important C# DSL APIs are starting to appear that effectively require this.

## Detailed design
[design]: #detailed-design

Opening of types allows for treating a type somewhat as if it were a module with module-bound functions inside of it. For example:

```fsharp
type A() =
    static member M(x: int) = x * 2

open type A

M(2)
```

However, **members are not functions**, so very different rules apply beyond simple cases like the above. This primarily concerns name resolution when overloads are involved, or when considering whether or not we resolve a method or a property first.

Additionally, visible static fields should also be accessible. Example:

```fsharp
open type System.Data.Common.DbMetaDataColumnNames

printfn "%s" ColumnSize
```

Visible consts should also be allowed. Example:

```fsharp
open type System.Math

PI
```

### Attributes

There are two relevant attributes that are also respected: `AutoOpen` and `RequireQualifiedAccess`.

#### `RequireQualifiedAccess`

As with modules, application of this attribute to a type in F# will require full qualification to use a member defined within.

```fsharp
[<RequireQualifiedAccess>]
type A() =
    static member M(x: int) = x * 2

open type C // Compile error

M(2) // 'M' is not recognized
```

#### `AutoOpen`

Also like modules, specifying `AutoOpen` on a type automatically brings its members into scope.

```fsharp
[<AutoOpen>]
type A() =
    static member M(x: int) = x * 2

M(2) // Can call 'M' without opening 'A'
```

### Opening types reveals nested types

When opening a .NET type (or a provided type), any accessible nested types also become accessible without qualification.

### Extending types

It is possible to use optional type extensions to extend types. Members defined as type extensions are visible when opening these types:

```fsharp
type System.Math with
    static member MyPI = 3.14

open type System.Math

MyPI // works!
```

Resolving members on optional type extensions works the same as with non-static extensions. These rules are not adjusted. That is, members defined in type extensions don't get magically attached to an already-opened static class. The following code will not compile because the `N` member is not in scope:

```fsharp
type A() =
    static member M(x: int) = x * 2

open type A

type A with
    static member N(x: float) = ()

M(2)
N(2.0) // Compile error, not in scope
```

The `open type A` declaration must be placed below the type extension:

```fsharp
type A() =
    static member M(x: int) = x * 2

type A with
    static member N(x: float) = ()

open type C

M(2)
N(2.0) // Works!
```

### Opening types with generic parameter instantiations

You can open types with generic parameter instantiations:

```fsharp
open System.Numerics
open type Vector<int>

let x = One // "x" is of type "Vector<int>"
```

But, you cannot have any anonymous parameters:

```fsharp
open System.Numerics
open type Vector<_> // Compile error
```

### Named Types

Named, or nominal, types are only allowed. It means you cannot open a function, tuple, or anonymous record:
```fsharp
open type (int * int) // Compile error, named types are only allowed
```

Using an abbreviation will still not allow it:
```fsharp
type MyAbbrev = (int * int)
open type MyAbbrev // Compile error, named types are only allowed
```

These rules are very similar when using `inherit` in a type definition.

## Drawbacks
[drawbacks]: #drawbacks

* This introduces another avenue to encounter issues when resolving overloads
* This introduces more avenues to mix overloaded members with type inference, which can lead to source breaking changes if APIs defining those members add overloads over time
* Code using this feature could be harder to understand without editor tooling

## Alternatives
[alternatives]: #alternatives

###  `open C`, `open type C` or `open static C`

We have resolved to use `open type C`.

Issues to do with potential breaking changes do to changes in shadowing led to a change of heart, see https://github.com/fsharp/fslang-design/issues/352#issuecomment-522533863

See original discussion here: https://github.com/fsharp/fslang-design/issues/352#issuecomment-499146012.  

### Open non-static-classes

In 'preview', we could only open static classes and not any class or type. 

Issues to do with potential breaking changes coming from changes in shadowing led to a change of heart, see https://github.com/fsharp/fslang-design/issues/352#issuecomment-522533863

See original discussion here: https://github.com/fsharp/fslang-design/issues/352#issuecomment-499146012.  

### Combining overloaded methods from different types

We will **not** allow combination of method overloads from different types according to C# rules. The reasoning is for interactivity; we need to be able to shadow method groups in order to not have old method overloads from previous types in scope.

When multiple methods of the same name are in scope, they can be overloaded provided that their signatures are unique. If we did, this is an example:

```fsharp
type A() =
    static member M(x: int) = x * 2

type B() =
    static member M(x: float) = x * 2.0

open type A
open type B

// Both methods are resolved
M(1)
M(1.0)
```

If the signatures for `M` were not unique, then it would not be possible to call `M`. Full qualification is required, or if the source is available, a change to one of the methods is required:

```fsharp
type A() =
    static member M(x: int) = x * 2

type B() =
    static member M(x: int) = x * 2

open type A
open type B

// Error: ambiguous call
M(1)
```

Because overloads coming from multiple opened types are not resolved in the context of a type, we would not require compatibility with the existing rules for resolution.

Because APIs utilizing static members generally use methods instead of properties, we prefer methods over properties and adjust the previous ordered-list as such:

Try to resolve `member-ident` to one of the following, in order:

1. A union case.
2. **A method group.**
3. A property group.
4. A field.
5. An event.
6. **A method group of extension members, by consulting the `ExtensionsInScope` table.**
7. A property group of extension members , by consulting the `ExtensionsInScope` table.
8. A nested type `type-nested`. Recursively resolve .rest if it is present, otherwise return `type-nested`

That is to say, we will resolve methods over properties in the context of opening static classes or static members extending a static class.


## Compatibility
[compatibility]: #compatibility

This is a non-breaking change.

## Unresolved questions and feedback
[unresolved]: #unresolved-questions

None

