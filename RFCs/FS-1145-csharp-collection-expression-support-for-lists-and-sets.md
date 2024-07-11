# F# RFC FS-1145 - C# collection expression support for F# lists and sets

The design suggestion [Support for C# collection expressions in F# lists and sets](https://github.com/fsharp/fslang-suggestions/issues/1355) has been marked "approved in principle."

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1355)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/17359)
- [x] [Discussion](https://github.com/fsharp/fslang-design/discussions/777)

# Summary

<!-- One paragraph explanation of the feature. -->

We annotate the `'T list` and `Set<'T>` types in FSharp.Core with `CollectionBuilderAttribute` and expose static `Create` methods to enable instantiating F# lists and sets in C# using C# 12's [collection expressions](https://learn.microsoft.com/dotnet/csharp/language-reference/proposals/csharp-12.0/collection-expressions) feature.

## Before

### Instantiation of intermediate collections required

```csharp
FSharpList<int> xs = ListModule.OfArray(new[] { 1, 2, 3 });
```

```csharp
FSharpSet<int> xs = SetModule.OfArray(new[] { 1, 2, 3 });
```

## After

### Instantiation of intermediate collections no longer required

```csharp
FSharpList<int> xs = [1, 2, 3];
```

```csharp
FSharpSet<int> xs = [1, 2, 3];
```

# Motivation

<!-- Why are we doing this? What use cases does it support? What is the expected outcome? -->

Instantiating F# lists and sets in C# is currently verbose, cumbersome, and inefficient.

Just as it is common for F# code to consume .NET libraries written in C#, there are many scenarios where it is necessary or desirable to interact with F# libraries from C#.

In mixed-language solutions containing both C# and F# projects, for example, unit tests written in C# may need to test APIs written in F#.

In C#-only solutions, unit tests written in C# may wish to use the automatically-implemented structural equality of the F# list and set types to make test assertions.

## Status quo

The primary way to create F# lists in C# is currently to instantiate an intermediate collection (e.g., an array) and pass that to one of the methods on `ListModule`, like `ListModule.OfArray` or `ListModule.OfSequence`. Technically, it is also possible to manually chain calls of `FSharpList<T>.Cons` and `FSharpList<T>.Empty` to create a list, but this is extremely verbose and seldom done in practice.

The situation is similar for F# sets: an intermediate collection must be instantiated and either passed into the `FSharpSet<T>` constructor (which takes an `IEnumerable<T>`) or into one of the methods on the `SetModule`, e.g., `SetModule.OfArray` or `SetModule.OfSequence`.

### Lists

#### Chaining `Cons`

```csharp
var xs = FSharpList<int>.Cons(1, FSharpList<int>.Cons(2, FSharpList<int>.Cons(3, FSharpList<int>.Empty)));
```

#### Instantiating an intermediate collection

```csharp
var xs = ListModule.OfArray(new[] { 1, 2, 3 });
```

### Sets

#### Using the `Set<'T>` constructor

```csharp
var xs = new FSharpSet<int>(new[] { 1, 2, 3 });
```

#### Instantiating an intermediate collection

```csharp
var xs = SetModule.OfArray(new[] { 1, 2, 3 });
```

## Collection expressions

C# has long supported collection initializer syntax using curly braces for types that expose a public parameterless constructor and a `void`-returning `Add` method. This syntax is not compatible with immutable collection types, including F# lists and sets, however, since F# lists and sets have neither public parameterless constructors nor mutating, `void`-returning `Add` methods.

C# 12 added support for [collection expressions](https://github.com/dotnet/csharplang/blob/d9caa3ce753a6b9583da9bdeba316bcb0fe84d44/proposals/csharp-12.0/collection-expressions.md) to enable a unified syntax for instantiating collections. The design additionally includes support for initializing immutable collections through the use of the [`CollectionBuilderAttribute`](https://learn.microsoft.com/dotnet/api/system.runtime.compilerservices.collectionbuilderattribute?view=net-8.0) and an appropriate `Create` method.

By adding the appropriate attributes and `Create` methods to FSharp.Core, we can make interoperating with F# APIs from C# — and using F# lists and sets in C# in general — more efficient, less verbose, and overall more pleasant and idiomatic.

# Detailed design

<!-- This is the bulk of the RFC. Explain the design in enough detail for somebody familiar
with the language to understand, and for somebody familiar with the compiler to implement.
This should get into specifics and corner-cases, and include examples of how the feature is used.
 -->

 Here follows the relevant information from the [C# feature specification](https://github.com/dotnet/csharplang/blob/d9caa3ce753a6b9583da9bdeba316bcb0fe84d44/proposals/csharp-12.0/collection-expressions.md) required to enable support for using C# collection expressions to instantiate F# lists and sets in C#:

> A *create method* is indicated with a `[CollectionBuilder(...)]` attribute on the *collection type*.
> The attribute specifies the *builder type* and *method name* of a method to be invoked to construct an instance of the collection type.

> The *builder type* must be a non-generic `class` or `struct`.

> * The method must have the name specified in the `[CollectionBuilder(...)]` attribute. 
> * The method must be defined on the *builder type* directly.
> * The method must be `static`.
> * The method must be accessible where the collection expression is used.
> * The *arity* of the method must match the *arity* of the collection type.
> * The method must have a single parameter of type `System.ReadOnlySpan<E>`, passed by value.
> * There is an [*identity conversion*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/conversions.md#1022-identity-conversion), [*implicit reference conversion*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/conversions.md#1028-implicit-reference-conversions), or [*boxing conversion*](https://github.com/dotnet/csharpstandard/blob/standard-v6/standard/conversions.md#1029-boxing-conversions) from the method return type to the *collection type*.

> The span parameter for the *create method* can be explicitly marked `scoped` or `[UnscopedRef]`. If the parameter is implicitly or explicitly `scoped`, the compiler *may* allocate the storage for the span on the stack rather than the heap.

## Target framework compatibility

FSharp.Core currently multitargets `netstandard2.0` and `netstandard2.1`. The C# collection expression feature's support for immutable collections depends on certain .NET BCL types that do not exist in one or both of these target frameworks.

We can address this as follows for the `netstandard2.1` target. We do not add C# collection expression support for the `netstandard2.0` target at this time.

## Add internal `CollectionBuilderAttribute` polyfill to FSharp.Core

The [`System.Runtime.CompilerServices.CollectionBuilderAttribute`](https://learn.microsoft.com/dotnet/api/system.runtime.compilerservices.collectionbuilderattribute?view=net-8.0) exists only in .NET 8 and above. When the C# compiler looks for this attribute, however, it does not need to come from the `System.Runtime` assembly.

We can satisfy the C# compiler's requirements by defining a polyfill of this type with the appropriate shape and fully-qualified name. We neither need nor mean to make this type directly accessible outside of FSharp.Core itself, so we mark it `internal`.

```fsharp
namespace System.Runtime.CompilerServices

[<Sealed>]
[<AttributeUsage(AttributeTargets.Class ||| AttributeTargets.Struct ||| AttributeTargets.Interface, Inherited = false)>]
type internal CollectionBuilderAttribute (builderType: Type, methodName: string) =
    inherit Attribute ()
    member _.BuilderType = builderType
    member _.MethodName = methodName
```

## Add internal `ScopedRefAttribute` polyfill to FSharp.Core

While marking the parameter of `Create` as a scoped reference is not required, doing so takes little additional effort and will help the C# compiler generate more performant code.

Since the [`System.Runtime.CompilerServices.ScopedRefAttribute`](https://learn.microsoft.com/dotnet/api/system.runtime.compilerservices.scopedrefattribute?view=net-8.0) also exists only in .NET 8 and above, we provide an `internal` polyfill of this type as well.

```fsharp
namespace System.Runtime.CompilerServices

[<Sealed>]
[<AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)>]
type internal ScopedRefAttribute () =
    inherit Attribute ()
```

## Add non-generic `List` and `Set` types with appropriate `Create` methods

### Static builder classes versus modules

The F# list and set types already have corresponding modules, which are compiled to static classes. In theory, these modules could be used as the "builder" type to which the appropriate `Create` method could be added for C# collection expression support. This is not possible in practice for multiple reasons:

1. The `CollectionBuilderAttribute` requires a reference to an appropriate builder type. The syntax of attributes requires that this be done with `typeof<CollectionBuilder>` for some type `CollectionBuilder`, but it is not possible to access the type of the class underlying a module in this way. While there are ways to access this underlying type (e.g., by getting the declaring type of a nested type defined in the module), they are not compatible with the syntax of attributes.
2. The `CollectionBuilderAttribute` must be placed on the definition of the collection type itself while also referring to the builder type; the builder type's `Create` method must return an instance of the collection type. That is, the two types must have a mutually-recursive relationship. It would not currently be possible to achieve this with the `List` and `Set` modules, since they are defined in separate files from the types they act on.

The builder type must also be non-generic, which means we cannot add `Create` methods to `'T list` and `Set<'T>` themselves.

Other generic immutable collection types defined in the .NET base class library (BCL) that support C# collection expressions, like `System.Collections.Immutable.ImmutableArray<T>` or `System.Collections.Immutable.ImmutableList<T>`, define a `Create` method on a non-generic static class with the same name, e.g., `ImmutableArray` and `ImmutableList`.

We follow a similar pattern here.

### `System.ReadOnlySpan<'T>` requires `netstandard2.1` or greater

The `System.ReadOnlySpan<'T>` type does not exist in `netstandard2.0`. While it would be possible to take a dependency on the [System.Memory NuGet package](https://www.nuget.org/packages/System.Memory) to add support for this type for the `netstandard2.0` target, adding further dependencies to FSharp.Core is not desired.

We thus conditionally compile and expose the `List` and `Set` types and their `Create` methods only for a target framework of `netstandard2.1` or higher.

### `ScopedRefAttribute`

For both lists and sets, we annotate the `Create` method's `items` parameter with `ScopedRefAttribute` to indicate to the C# compiler that it may generate more efficient code when possible. This is valid because the lifetime of the input `ReadOnlySpan<'T>` [does not escape the scope](https://github.com/dotnet/csharplang/blob/d9caa3ce753a6b9583da9bdeba316bcb0fe84d44/proposals/csharp-11.0/low-level-struct-improvements.md#scoped-modifier) of the `Create` method.

### `List`

We give the static `List` type the compiled name `FSharpList` to align with the generic list type's ``FSharpList`1``. As mentioned above, a similar convention is followed by the types in the `System.Collections.Immutable` namespace in the BCL.

(Not shown: to discourage direct use in source code, we also annotate the `List` type and its `Create` method with the `CompilerMessageAttribute` and specify `IsHidden=true`.)

```fsharp
type List<'T> = …

#if NETSTANDARD2_1_OR_GREATER
and [<Sealed; AbstractClass; CompiledName("FSharpList")>] List =
    static member Create ([<System.Runtime.CompilerServices.ScopedRef>] items: System.ReadOnlySpan<'T>) = …
#endif
```

### `Set`

We give the static `Set` type the compiled name `FSharpSet` to align with the generic set type's ``FSharpSet`1``.

(Not shown: to discourage direct use in source code, we also annotate the `Set` type and its `Create` method with the `CompilerMessageAttribute` and specify `IsHidden=true`.)

```fsharp
type Set<'T> (…) = …

#if NETSTANDARD2_1_OR_GREATER
and [<Sealed; AbstractClass; CompiledName("FSharpSet")>] Set =
    static member Create([<System.Runtime.CompilerServices.ScopedRef>] items: System.ReadOnlySpan<'T>) = …
#endif
```

## Apply `CollectionBuilderAttribute` to `'T list` and `Set<'T>` definitions

We make the application of `CollectionBuilderAttribute` conditional on a target framework of `netstandard2.1` or greater because the builder types themselves are conditional on this.

### `'T list`

```fsharp
#if NETSTANDARD2_1_OR_GREATER
    [<System.Runtime.CompilerServices.CollectionBuilder(typeof<List>, "Create")>]
#endif
type List<'T> = …
```

### `Set<'T>`

```fsharp
#if NETSTANDARD2_1_OR_GREATER
    [<System.Runtime.CompilerServices.CollectionBuilder(typeof<Set>, "Create")>]
#endif
type Set<'T> = …
```

# Drawbacks

<!-- Why should we *not* do this? -->

The main arguable drawback of this approach is that the API surface area of FSharp.Core is now technically different depending on the target framework. In practice, we mitigate this by annotating the new APIs with the `CompilerMessageAttribute` and specifying `IsHidden=true`. This means that editors will not suggest `List.Create` and `Set.Create` in autocompletion, and, if a user nonetheless attempts to use them directly, the compiler will emit a warning that these APIs are intended for compiler use only.

# Alternatives

<!-- What other designs have been considered? What is the impact of not doing this? -->

## Use the existing `List` and `Set` modules instead of static classes

It would be convenient if we could do this, but owing to the requirement that the collection types and their builder types be in a mutually-recursive relationship with each other, this is not currently possible.

## Support the `netstandard2.0` target by depending on the System.Memory NuGet package

We could avoid the need for ifdefs, and add support for this feature for the `netstandard2.0` target, by adding a dependency on the System.Memory NuGet package.

We don't do this for two reasons:

1. Adding dependencies to FSharp.Core is not desired.
2. The C# collection expressions feature requires a compiler supporting C# 12 or above, which requires the .NET 8 SDK or above anyway. While it is possible that someone would want to use a newer SDK to compile while using a runtime that does not support `netstandard2.1` (e.g., .NET Framework), supporting this scenario does not warrant adding a package dependency to FSharp.Core.

## Add `net8.0` or greater target frameworks

Instead of adding polyfills for .NET 8-specific types, we could instead simply add a `net8.0` target framework to FSharp.Core. There are many reasons why this has not been done yet, and supporting this feature alone is not enough to outweigh them.

## Add `Create` overloads taking arrays

See [dotnet/csharplang#8144](https://github.com/dotnet/csharplang/issues/8144).

A future version of the C# compiler may support a pattern wherein an array may be created on the heap and its ownership transferred to a collection builder type's `Create` method. There does not however seem to be a good reason to add array overloads unless or until that feature should come to pass.

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change?
  * No.
* What happens when previous versions of the F# compiler encounter this design addition as source code?
  * N/A.
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
  * N/A.
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?
  * Previous and current versions of the F# compiler are not aware of `CollectionBuilderAttribute`. If a future version of the F# compiler becomes aware of it (perhaps in support of [an analagous feature in F#](https://github.com/fsharp/fslang-suggestions/issues/1086)), this part of the design will likely follow the C# design in order to be compatible with C#-defined types, like those in `System.Collections.Immutable`, etc. The additions we are making for this feature are compliant with that design.

# Pragmatics

## Diagnostics

Please list the reasonable expectations for diagnostics for misuse of this feature.

N/A.

## Tooling

Please list the reasonable expectations for tooling for this feature, including any of these:

* Debugging
  * Breakpoints/stepping
    * N/A.
  * Expression evaluator
    * N/A.
  * Data displays for locals and hover tips
    * N/A.
* Auto-complete
  * N/A.
* Tooltips
  * N/A.
* Navigation and go-to-definition
  * N/A.
* Error recovery (wrong, incomplete code)
  * N/A.
* Colorization
  * N/A.
* Brace/parenthesis matching
  * N/A.

## Performance

Please list any notable concerns for impact on the performance of compilation and/or generated code

* For existing code
  * N/A.
* For the new features
  * N/A.

## Scaling

<!-- Please list the dimensions that describe the inputs for this new feature, e.g. "number of widgets" etc.  For each, estimate a reasonable upper bound for the expected size in human-written code and machine-generated code that the compiler will accept.

For example

* Expected maximum number of widgets in reasonable hand-written code: 100
* Expected reasonable upper bound for number of widgets accepted: 500

Testing should particularly check that compilation is linear (or log-linear or similar) along these dimensions.  If quadratic or worse this should ideally be noted in the RFC. -->

N/A.

## Culture-aware formatting/parsing

Does the proposed RFC interact with culture-aware formatting and parsing of numbers, dates and currencies? For example, if the RFC includes plaintext outputs, are these outputs specified to be culture-invariant or current-culture.

* No.

# Unresolved questions

None.
