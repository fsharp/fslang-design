# F# RFC FS-1023 - Allow type providers to generate types from types

The design suggestion [Allow type providers to generate types from other types](https://github.com/fsharp/fslang-suggestions/issues/212) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] [User Voice Request](https://github.com/fsharp/fslang-suggestions/issues/212)
* [ ] Details: [under discussion](https://github.com/fsharp/FSharpLangDesign/issues/125)
* [ ] Implementation: [early proof of concept](https://github.com/colinbull/visualfsharp/tree/rfc/fs-1023-type-providers)

# Summary
[summary]: #summary

Today, type providers can take instances of primitive types as argument, e.g. a connection string or a path to a file. We want to extend this to take a type - effectively adding the possibility for type providers to take generic type arguments:

```fsharp
// a normal record type
type MyRecord = { Id : int; Description : string }

// a provided type based on the existing type
type MyPocoRecord = PocoTypeProvider<MyRecord>()
```
This allows the provider to generate types based on the type argument(s).

# Motivation
[motivation]: #motivation

This proposal significantly increases the power of F# type providers, pushing them firmly into the realm of functionality that currently only the compiler is capable of, and that is typically handled today by code generation, manual implementation or workarounds.

In the example given in the summary, the type provider could provide a mutable C# "record" class to interoperate with existing .NET frameworks  like NHibernate that don't work well with immutable types. It could also augment the provided type with interfaces or members (e.g. `INotifyPropertyChanged`) that are necessary for interoperation but otherwise boilerplate. It could even provide methods on the provided type to convert instances of the argument type(s) to instances of the provided type. So one use case is to significantly improve interop with existing .NET libraries.

From another perspective, it also allows type-first specifications of external data sources in various formats. For example, various type providers (like CSV) can infer an expected schema from an actual file given as a path to the type provider. But in some cases (when the data source has no proper schema definition language) such a schema is more easily given as an actual type. For example, ignoring column order, we could say we expect a CSV file with three columns and corresponding types:

```fsharp
type Stock = { Ticker: string; Price: float; Date: DateTimeOffset }

type StockReader = CsvProvider<Stock>

let stocks : Stock seq = StockReader.Read("path/to/file.csv")
```
This is pertinent for serialization and messaging: the format is known beforehand, and typically specified naturally as a type. The type provider can generate specialized serialization/deserialization code that is more efficient than using reflection.

# Detailed design
[design]: #detailed-design

This is largely to be determined besides the simple example above. For now the design will mostly refer to open questions.

## Type Provider Usage

From the usage side, it now becomes possible to pass in a type to a type provider, in much the same way as passing a type as a generic type argument:

```fsharp
type SomeType = ...

type ProvidedType = TypeProvider<SomeType, {... other arguments ...}>
```
And further on `ProvidedType` can be used like existing provided types.

It is very likely there will be constraints on the kind of types that can be passed in to a type provider, and the types' provenance. See unresolved questions for more details.

## Type Provider Implementation

To allow implementations of type providers with this new feature, some of the API needs to change.

In particular, it must be possible to take in a static parameter of type `Type` in addition to the existing primitive types (`sbyte, int16, int32, int64, byte, uint16, uint32, uint64, bool, char, string, single, double`):

```fsharp
ProvidedStaticParameter(parameterName="name", parameterType=typeof<Type>)
```

This constructor currently also takes an optional default value; it's unclear whether this should be allowed for `Type` arguments. In addition, perhaps it should be possible for the type provider to impose some constraints on the possible types that can be passed in (similar to generic type constraints). See unresolved questions.

From there on, type provider should be able to proceed as usual, having access to the standard methods on Type to guide the generated types. There are unresolved questions on what kind of access the type provider has to the `Type` object - there will likely be various constraints. See unresolved questions.

# Implementation Notes

The implementation of the FSharp.Compiler.Service "Symbols" API in [Symbols.fs](https://github.com/fsharp/FSharp.Compiler.Service/blob/master/src/fsharp/vs/Symbols.fs) contains much of the logic needed for this work.

# Things to test

* [ ] Type abbreviations, e.g. ``int32`` in the blow

```
type Test3 = TypePassing.TypePassingTP<int32>
```

* [ ] Generic type instantiations implied by type abbreviations, e.g. 

```
type A = list<int>
type Test3 = TypePassing.TypePassingTP<A>``
```

* [ ] lots of recursive cases

# Drawbacks
[drawbacks]: #drawbacks

This would significantly increase the complexity of the implementation of the compiler. It's a very likely new source of bugs. It seems unlikely that on a first cut it will cater for all corner cases. There will be expected and unexpected limitations.

Type provider implementations will be pretty tricky. Since the space of possibilities for an input as rich as an arbitrary type (if arbitrary types are allowed) is pretty big, it is likely that  type providers will be constrained in non-obvious ways. Users of such type providers may not have a great experience.

It might prove difficult or a lot of work to provide great error messages if something goes wrong - e.g. if the type provider implementation or a compiler limitation causes some unexpected behavior in the type provider.

In short, it is a potentially magical feature, which makes it hard to understand in general, and in particular if things go sideways.

To a certain extent, these downsides are already present in the current type provider feature.

# Alternatives
[alternatives]: #alternatives

* One workaround is to put the types in another assembly, and pass the path to this assembly as input to a type provider. The type provider can then load that assembly and generate types based on it. This quickly becomes cumbersome.
* In many cases, reflection can be used to solve a subset of use cases, e.g. decoding a JSON message to a type. However, the potential of type providers is that they can provide seamless specialized, higher performance code.
* Run-time type and IL generation can be used to solve the aforementioned performance problem; however this is of course invisible at compile time.
* Code generation is an alternative to type providers in general. This is generally a less seamless solution, and also can't provide "infinite" types, because it would need infinite amounts of code.

# Unresolved questions
[unresolved]: #unresolved-questions

* What kind of types are allowed as arguments - any type? Only records/unions? Generic types? Interface types?
* Where can the argument types come from? Non-provided types only? Defined in a previous module? From other assemblies (what about runtime redirections?)
* Can the type provider impose any constraints on the type arguments - i.e. any or all of the existing generic type constraints.
* Can type arguments be optional?
* Is an instance of `Type` an appropriate choice for passing into the type provider implementation?
* Which subset of functionality on the passed in `Type` instances can the type provider access? For example, can it access only structure (members and signature) or also implementation? Can it traverse the class hierarchy?
I would add 
* F# type definitions are ``realized`` in several phases, as described in the F# spec.  For example, the "kind" of the type is first established, then the method symbols, then the method signatures, then the method implementations.  Is this process visible via the (changing?) results of the ``Type`` object?  This is mostly problematic when provided types are in a mutually-referential cycle with normal (non-provided) type definitions, and the normal types are passed as static parameters to the provided types.  However it may also be problematic when a normal (non-provided) type definition has members whose type signatures are incomplete from the type inference process (i.e. contain as-yet-un-inferred type variables).


