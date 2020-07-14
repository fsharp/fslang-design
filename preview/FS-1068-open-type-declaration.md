# F# RFC FS-1068 - Open Type Declaration

The design suggestion [Open static classes](https://github.com/fsharp/fslang-suggestions/issues/383) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.


[Discussion thread](https://github.com/fsharp/fslang-design/issues/352)

* [x] Approved in principle
* [x] Details: [Resolved to Preview](https://github.com/fsharp/fslang-design/issues/352)
* [x] Implementation: [Complete to Preview](https://github.com/dotnet/fsharp/pull/6325)


## Summary
[summary]: #summary

A new declaration form is added

    open type <type>

This allows unqualified  access to static members and nested types. For example:

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

Type providers can provide static classes, hence this would allow type providers to provide "unqualified" names. This is useful for some projections where the natural thing is unqualified, e.g. the "R" type provider always required `R.plot` etc. With this feature, `R.plot` can now be `plot`.

Additionally, important C# DSL APIs are starting to appear that effectively require this.

## Detailed design
[design]: #detailed-design

A new declaration form is added

    open type <type>

Here `<type>` can be a (possibly instantiated) type definition or a type abbreviation. For example:

```fsharp
type A() =
    static member M(x: int) = x * 2
    static member P = 2+2

open type A

M(2)
P
```


Visible static fields and properties are also be accessible. For example:

```fsharp
open type System.Data.Common.DbMetaDataColumnNames

printfn "%s" ColumnSize
```

Visible literal constants are accessible. Example:

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

For example:

```fsharp
let e1 = new System.Collections.Generic.List<int>.Enumerator();;

open System.Collections.Generic.List<int>

let e2 = new Enumerator();;
```

### Extending types

If F# type extensions extend a type with static members, then these type extensions are visible when opening these types:

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

A generic type may be opened with a generic parameter instantiation:

```fsharp
open System.Numerics
open type Vector<int>

let x = One // "x" is of type "Vector<int>"
```

Anonymous or unsolved type parameters may not be used:

```fsharp
open System.Numerics
open type Vector<_> // Compile error
```

It is possible to open multiple type instantiations, however the content of the second will out-scope the content of the first

```fsharp
open System.Numerics

open type Vector<int>
open type Vector<float>

let x = One // "x" is of type "Vector<float>"
```

### Inherited members

`open type` only makes accessible static members and nested types declared in the specified type. Inherited members are not imported.

### C# Extension members

C#-style extension members are declared as static members with the `[<Extension>]` attribute.

`open type` makes extension methods declared in the specified type available for extension method lookup.
However, the names of the extension methods are not imported into scope for unqualified reference in code.


### Named Types

Named, or nominal, types are the only types allowed in `open type` declarations. This means you cannot open a function, tuple, or anonymous record:
```fsharp
open type (int * int) // Compile error, named types are only allowed
```

Using an abbreviation of a non-nominal type is not allowed:
```fsharp
type MyAbbrev = (int * int)
open type MyAbbrev // Compile error, named types are only allowed
```

#### Exception

You cannot open `byref<'T>`/`inref<'T>`/`outref<'T>`.

```fsharp
open type byref<int> // Compile error
```

### Opening types with conflicting/overlapping overload sets

When two different types are opened, there is a potential that this will introduce overlapping and/or conflicting overloads. C#'s corresponding feature allows this.

However

1. Incremental evaluation scenarios such as FSI or Jupyter Notebooks are particularly affected by this: if method groups combine then repeated definition and opening of a type with the same name becomes problematic. 

2. Code that utilises combination of method overloads becomes extremely difficult to understand.

As a result, in F#, overload sets are considered to out-scope (i.e. shadow) other overload sets for methods of the same name.

For example:
```fsharp
type A() =
    static member M(x: int) = x * 2

type B() =
    static member M(x: float) = x * 2.0

open type A
open type B

M(1.0) // this is resolved
M(1) // this is NOT resolved
```
and this:
```fsharp
type A() =
    static member M(x: int) = x * 2

type B() =
    static member M(x: int) = x * 2

open type A
open type B

// Calls B
M(1)
```

If the signatures for `M` are not unique, Full qualification is required, or if the source is available, a change to one of the methods is required.


### Tooling

Updates to FCS are made to understand `open type` to display a list of types that can be opened.

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

### Combining overloaded methods from different types

We considered following the C# design for combining method overload sets from different types.  See the discussion above.

## Compatibility
[compatibility]: #compatibility

This is a non-breaking change.

## Unresolved questions and feedback
[unresolved]: #unresolved-questions

None
