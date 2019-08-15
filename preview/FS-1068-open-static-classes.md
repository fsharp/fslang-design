# F# RFC FS-1068 - Open static classes

The design suggestion [Open static classes](https://github.com/fsharp/fslang-suggestions/issues/383) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.


# Summary
[summary]: #summary

Add support for opening static classes, e.g. `open System.Math`. For example

```fsharp
> open System.Math;;
> Min(1.0, 2.0);;
val it : float = 1.0

> Min(2.0, 1.0);;
val it : float = 1.0

> open System.Math;;
> Min(2.0, 1.0);;
val it : float = 1.0
```

# Motivation
[motivation]: #motivation

This greatly increases the expressivity of F# DSLs by allowing method-API facilities such as named arguments, optional arguments and type-directed overloading to be used in the DSL design.

Type providers can provide static classes, hence this would allow type providers to provide "unqualified" names. This useful for some projections where the natural thing is unqualified, e.g. the "R" type provider always required `R.plot` etc. With this feature, `T.plot` can now be `plot`.

Additionally, important C# DSL APIs are starting to appear that effectively require this.

# Background: static classes

In .NET, "static classes" are abstract and sealed, containing only static members.

To declare these in F#, you need to specify those attributes on a type:

```fsharp
[<AbstractClass; Sealed>]
type C =
    static member M(x: int) = x * 2

C.M(2) |> ignore
```

A benefit to using these over module-bound functions is that you can take advantage of overloading and optional parameters. F# tooling will also provide more information in tooltips. The downside is that they are not functions, so you miss out on things like first-class functions (methods cannot take other methods as input, etc.).

# Detailed design
[design]: #detailed-design

Opening of static classes allows for treating a static class somewhat as if it were a module with module-bound functions inside of it. So the previous code sample could look like this:

```fsharp
[<AbstractClass; Sealed>]
type C =
    static member M(x: int) = x * 2

open C

M(2) |> ignore
```

However, **members are not functions**, so very different rules apply beyond simple cases like the above. This primarily concerns name resolution when overloads are involved, or when considering whether or not we resolve a method or a property first.

Additionally, visible static fields should also be accessible. Example:

```fsharp
open System.Data.Common.DbMetaDataColumnNames

printfn "%s" ColumnSize
```

## Only static classes can be opened

The corresponding feature in C# allows for any class or struct to be "opened", allowing you access to any static members defined on it. Such examples include the `Vector2` and `Vector3` structs, which contain static methods for operating on those data types.

This functionality is explicitly scoped out for now.

## Attributes

There are two relevant attributes that are also respected: `AutoOpen` and `RequireQualifiedAccess`.

### `RequireQualifiedAccess`

As with modules, application of this attribute to a static class in F# will require full qualification to use a member defined within.

```fsharp
[<AbstractClass; Sealed; RequireQualifiedAccess>]
type C =
    static member M(x: int) = x * 2

open C // Compile error
M(12) // 'M' is not recognized
```

### `AutoOpen`

Also like modules, specifying `AutoOpen` on a static class automatically brings its members into scope.

```fsharp
[<AbstractClass; Sealed; AutoOpen>]
type C =
    static member M(x: int) = x * 2

M(12) // Can call 'M' without opening 'C'
```

## Extending static classes

It is possible to use type extensions to extend static classes. Members defined as type extensions are visible when opening these static classes:

```fsharp
type System.Math with
    static member MyPI = 3.14

open System.Math
MyPI // works!
```

Resolving members on type extensions works the same as with non-static extensions. These rules are not adjusted. That is, members defined in type extensions don't get magically attached to an already-opened static class. The following code will not compile because the `N` member is not in scope:

```fsharp
[<AbstractClass; Sealed>]
type C =
    static member M(x: int) = x * 2

open C

type C with
    static member N(x: float) = ()

M(12)
N(12.0) // Compile error, not in scope
```

The `open C` declaration must be placed below the type extension:

```fsharp
[<AbstractClass; Sealed>]
type C =
    static member M(x: int) = x * 2

type C with
    static member N(x: float) = ()

open C

M(12)
N(12.0) // Works!
```

## Resolving overloaded methods

When multiple methods of the same name are in scope, they can be overloaded provided that their signatures are unique:

```fsharp
[<AbstractClass; Sealed>]
type A =
    static member M(x: int) = x * 2

[<AbstractClass; Sealed>]
type B =
    static member M(x: float) = x * 2.0

open A
open B

// Both methods are resolved
M(1) |> ignore
M(1.0) |> ignore
```

If the signatures for `M` were not unique, then it would not be possible to call `M`. Full qualification is required, or if the source is available, a change to one of the methods is required:

```fsharp
[<AbstractClass; Sealed>]
type A =
    static member M(x: int) = x * 2

[<AbstractClass; Sealed>]
type B =
    static member M(x: int) = x * 2

open A
open B

// Error: ambiguous call
M(1)
```

### Name resolution for members coming from different static classes or type extensions

Because overloads coming from multiple opened static classes are not resolved in the context of a type, we do not require compatibility with the existing rules for resolution.

Because APIs utilizing static members generally use methods instead of properties, we prefer methods over properties and adjust the previous ordered-list as such:

Try to resolve `member-ident` to one of the following, in order:

1. A union case.
**2. A method group.**
3. A property group.
4. A field.
5. An event.
**6. A method group of extension members, by consulting the `ExtensionsInScope` table.**
7. A property group of extension members , by consulting the `ExtensionsInScope` table.
8. A nested type `type-nested`. Recursively resolve .rest if it is present, otherwise return `type-nested`

That is to say, we will resolve methods over properties in the context of opening static classes or static members extending a static class.

# Drawbacks
[drawbacks]: #drawbacks

* This introduces another avenue to encounter issues when resolving overloads
* This introduces more avenues to mix overloaded members with type inference, which can lead to source breaking changes if APIs defining those members add overloads over time
* Code using this feature could be harder to understand without editor tooling

# Alternatives
[alternatives]: #alternatives

## Open anything

In C#, any class or struct can be opened using `using static System.String`, and its static content made available. This can be possibly done in the future, as an extension of this feature.

This would enable some things like this:

```fsharp
open System.Numerics.Vector2

let v1 = Vector2(1.0f, 2.0f)
let v2 = Vector2(1.0f, 2.0f)

Dot(v1, v2) // No need to fully qualify 'Dot'
```

But it is currently considered out of scope.

# Compatibility
[compatibility]: #compatibility

This is a non-breaking change.

# Unresolved questions
[unresolved]: #unresolved-questions

## Resolving static classes with generic parameters

C# allows for opening static classes with generic parameters like this:

```csharp
using static MyStaticClass<int>;
```

However, this is partly due to the fact that you _must_ parameterize either a method via `M<T>` or a class containing that member with `C<T>` to actually have something like a generic parameter as input to a member.

F# does not have this restriction. That is, this F# code is not possible to write in C#:

```fsharp
[<AbstractClass; Sealed>]
type C =
    static member M(x: 'T) = ()
```

So for an F#-only perspective, there is no strong requirement to allow for something like `open C<int>` because there is no need to express a `C<'T>` in F# to allow for having static members that use generics.

There is a case that involves properties, where something like the following imposes a value restriction that requires parameterizing `C`:

```fsharp
[<AbstractClass; Sealed>]
type C =
    static member P: 'T = Unchecked.defaultof<'T> // value restriction; `T is inferred to be 'obj'
```

To make the property a generic return type, `C` must be made `C<'T>`, but then it cannot be opened.

More commonly, from an interop perspective, opening a generic static class may be required as some APIs may use generic type parameters on the static class, forcing users to open these with a concrete substitution when using them.
