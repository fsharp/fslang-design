# F# RFC FS-1068 - Open static classes

The design suggestion [Open static classes](https://github.com/fsharp/fslang-suggestions/issues/383) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.


[Discussion thread](https://github.com/fsharp/fslang-design/issues/352)

* [x] Approved in principle
* [x] Details: [Resolved to Preview](https://github.com/fsharp/fslang-design/issues/352)
* [x] Implementation: [Complete to Preview](https://github.com/dotnet/fsharp/pull/6325)


## Summary
[summary]: #summary

Add support for opening static classes, e.g. `open type System.Math`. For example

```fsharp
> open type System.Math;;
> Min(1.0, 2.0);;
val it : float = 1.0

> Min(2.0, 1.0);;
val it : float = 1.0
```

**Note**: in the preview `open System.Math` is used instead of `open type System.Math`. 

## Motivation
[motivation]: #motivation

This greatly increases the expressivity of F# DSLs by allowing method-API facilities such as named arguments, optional arguments and type-directed overloading to be used in the DSL design.

Type providers can provide static classes, hence this would allow type providers to provide "unqualified" names. This useful for some projections where the natural thing is unqualified, e.g. the "R" type provider always required `R.plot` etc. With this feature, `R.plot` can now be `plot`.

Additionally, important C# DSL APIs are starting to appear that effectively require this.

## Background: static classes

In .NET, "static classes" are abstract and sealed, containing only static members.

To declare these in F#, you need to specify those attributes on a type:

```fsharp
[<AbstractClass; Sealed>]
type C =
    static member M(x: int) = x * 2

C.M(2) |> ignore
```

A benefit to using these over module-bound functions is that you can take advantage of overloading and optional parameters. F# tooling will also provide more information in tooltips. The downside is that they are not functions, so you miss out on things like first-class functions (methods cannot take other methods as input, etc.).

## Detailed design
[design]: #detailed-design

Opening of static classes allows for treating a static class somewhat as if it were a module with module-bound functions inside of it. So the previous code sample could look like this:

```fsharp
[<AbstractClass; Sealed>]
type C =
    static member M(x: int) = x * 2

open type C

M(2) |> ignore
```

However, **members are not functions**, so very different rules apply beyond simple cases like the above. This primarily concerns name resolution when overloads are involved, or when considering whether or not we resolve a method or a property first.

Additionally, visible static fields should also be accessible. Example:

```fsharp
open System.Data.Common.DbMetaDataColumnNames

printfn "%s" ColumnSize
```

Visible consts should also be allowed. Example:

```fsharp
open type System.Math

PI
```

### Non-static classes can be opened

**Note**: in the preview only static classes can be opened

The corresponding feature in C# allows for any class or struct to be "opened", allowing you access to any static members defined on it. Such examples include the `Vector2` and `Vector3` structs, which contain static methods for operating on those data types.

This functionality is explicitly scoped out for now.

### Attributes

There are two relevant attributes that are also respected: `AutoOpen` and `RequireQualifiedAccess`.

#### `RequireQualifiedAccess`

As with modules, application of this attribute to a static class in F# will require full qualification to use a member defined within.

```fsharp
[<AbstractClass; Sealed; RequireQualifiedAccess>]
type C =
    static member M(x: int) = x * 2

open type C // Compile error
M(12) // 'M' is not recognized
```

#### `AutoOpen`

Also like modules, specifying `AutoOpen` on a static class automatically brings its members into scope.

```fsharp
[<AbstractClass; Sealed; AutoOpen>]
type C =
    static member M(x: int) = x * 2

M(12) // Can call 'M' without opening 'C'
```

### Opening types reveals nested types

**Note**: In the preview nested types are not revealed when opening a static type

When a static .NET type (or a provided type) any accessible nested types also become accessible without qualification.

### Extending static classes

It is possible to use type extensions to extend static classes. Members defined as type extensions are visible when opening these static classes:

```fsharp
type System.Math with
    static member MyPI = 3.14

open type System.Math
MyPI // works!
```

Resolving members on type extensions works the same as with non-static extensions. These rules are not adjusted. That is, members defined in type extensions don't get magically attached to an already-opened static class. The following code will not compile because the `N` member is not in scope:

```fsharp
[<AbstractClass; Sealed>]
type C =
    static member M(x: int) = x * 2

open type C

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

open type C

M(12)
N(12.0) // Works!
```

## Drawbacks
[drawbacks]: #drawbacks

* This introduces another avenue to encounter issues when resolving overloads
* This introduces more avenues to mix overloaded members with type inference, which can lead to source breaking changes if APIs defining those members add overloads over time
* Code using this feature could be harder to understand without editor tooling

## Alternatives
[alternatives]: #alternatives

###  `open C`, `open type C` or `open static C`

We have resolved to use `open type C`.

See original discussion here: https://github.com/fsharp/fslang-design/issues/352#issuecomment-499146012.  

Issues to do with potential breaking changes do to changes in shadowing led to a change of heart, see https://github.com/fsharp/fslang-design/issues/352#issuecomment-522533863

### Open non-static-classes

In C#, any class or struct can be opened using `using static System.String`, and its static content made available. This can be possibly done in the future, as an extension of this feature.

This would enable some things like this:

```fsharp
open type System.Numerics.Vector2

let v1 = Vector2(1.0f, 2.0f)
let v2 = Vector2(1.0f, 2.0f)

Dot(v1, v2) // No need to fully qualify 'Dot'
```

In the preview only static classes could be opened. Post preview have resolved to allow non-static types to be opened.

See original discussion here: https://github.com/fsharp/fslang-design/issues/352#issuecomment-499146012.  

Issues to do with potential breaking changes coming from changes in shadowing led to a change of heart, see https://github.com/fsharp/fslang-design/issues/352#issuecomment-522533863

Also, consider C# examples of this kind:
```csharp
using System.Numerics;
using static System.Numerics.Vector3;
using static System.Numerics.Vector2;

namespace ConsoleApp378
{
    class Program
    {
        static void Main(string[] args)
        {
            var v1 = new Vector3();
            var result1 = Lerp(v1, v1, 1);
            var v2 = new Vector2();
            var result2 = Lerp(v2, v2, 1);
        }
    }
}
```

#### Combining overloaded methods from different types

Post-preview we will allow combination of method overloads from different types according to C# rules.

When multiple methods of the same name are in scope, they can be overloaded provided that their signatures are unique:

```fsharp
[<AbstractClass; Sealed>]
type A =
    static member M(x: int) = x * 2

[<AbstractClass; Sealed>]
type B =
    static member M(x: float) = x * 2.0

open type A
open type B

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

open type A
open type B

// Error: ambiguous call
M(1)
```

Because overloads coming from multiple opened static classes are not resolved in the context of a type, we do not require compatibility with the existing rules for resolution.

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


### Issue: Opening static classes with generic parameter instantiations

**Proposed resolution**: do not do this as part of this RFC

C# allows for opening static classes with generic parameters like this:

```csharp
using static MyStaticClass<int>;

M(12);
```

When opening different generic instantiations of `MyStaticClass`, the specialized members are viewed as overloaded methods:

```csharp
using static MyStaticClass<int>;
using static MyStaticClass<string>;

M(12); // This is one overload
M("hello"); // This is another overload
```

This appears barely documented in the C# design docs. The preview version of the F# feature explicitly does not allow this: `open type` is only allowed on non-generic static classes.

From an interop perspective, opening a generic static class may be required as some APIs may use generic type parameters on the static class, forcing users to open these with a concrete substitution when using them.

Should this be done for F#, we'll also have to match how C# creates a method group based on specialized members coming from the opened generic static class. Additional behavioral considerations for this:

* It requires resolving overloaded members coming from different `open` declarations, or at least the same mechanism, should `open C<int>` and `open C<string>` be specified
* A concrete substitution is likely to be required, as `open C<_>` would make little sense here given that it's already possible to define generic static methods without the containing class also defining a generic type

That is, the following should likely be the behavior:

```fsharp
open type C<_> // Error: disallowed
open type C<int>
open type C<string>

M(12); // Allowed, viewed as an overload
M("hello"); // Allowed, viewed as an overload
```
