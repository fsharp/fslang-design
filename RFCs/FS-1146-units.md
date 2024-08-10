# F# RFC FS-1146 - Ease conversion between Units of Measure (UoM) and undecorated numerals and simplify casting

The design suggestion [Ease conversion between Units of Measure (UoM) and undecorated numerals and simplify casting](https://github.com/fsharp/fslang-suggestions/issues/892) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/892)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/17518)
- [ ] Discussion

## Summary

A new type `Units` will be added to FSharp.Core and provide `static` methods for addition, removal and casting of UoM for supported primitive types and common collections containing primitive types.

## Motivation

Prior to this RFC, F# programmers have been able to apply units of measure supported primitive types through the use of functions in the `LanguagePrimitives` module e.g. `LanguagePrimitives.FloatWithMeasure<'M>`, or, by multiplying the value by one `1.0<kg>`. It has been commonly asked by the community for a simple way to remove units of measure from a given value or a way to convert units of measure from one unit to another directly. It has also been requested that there should be a way to convert collections of primitives e.g. `int[]` to `int<kg>[]` without having to create a copy of the collection.

## Detailed design

### FSharp.Core additions

The additions follow a common pattern:

- An overloaded `Add` method for each of the supported primitive types adds a UoM to the value
- An overloaded `Remove` method for each of the supported primitive types removed UoM from the value
- An overloaded `Cast` method for each of the supported primitive types replaces UoM on the value
- Overloaded `Add*`, `Remove*` and `Cast*` e.g. `AddArray` for common collection types containing supported primitives.

```fsharp
namespace Microsoft.FSharp.Core


type Units =
    static member inline Add<[<Measure>]'Measure>(input: byte):byte<'Measure> = retype input
    static member inline Add<[<Measure>]'Measure>(input: float):float<'Measure> = retype input
    ...
    static member inline Remove<[<Measure>]'Measure>(input: byte<'Measure>):byte = retype input
    static member inline Remove<[<Measure>]'Measure>(input: float<'Measure>):float = retype input
    ...
    static member inline Cast<[<Measure>]'MeasureIn, [<Measure>]'MeasureOut>(input: byte<'MeasureIn>):byte<'MeasureOut> = retype input
    static member inline Cast<[<Measure>]'MeasureIn, [<Measure>]'MeasureOut>(input: float<'MeasureIn>):float<'MeasureOut> = retype input
    ...
    static member inline AddArray<[<Measure>]'Measure>(input: byte[]):byte<'Measure>[] = retype input
```

### Drawbacks

- `FSharp.Core` already offers some of the functionality in the `LanguagePrimitives` module however this has not been easy to discover for users.

### Alternatives

- Don't do this and maintain status quo
- Make the additions to `LanguagePrimitives`
- Use different type and/or method names
- Modify the F# language to provide a generic way to perform unit conversions - this would require the addition of `'T<'m>` as a language concept.

### Compatibility

This is not a breaking change. The elaboration of existing code that passes type checking is not changed.

This doesn't extend the F# metadata format.

### Unresolved questions

- Should any other collections be added?
