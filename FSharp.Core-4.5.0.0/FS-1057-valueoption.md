# F# RFC FS-1039 - ValueOption

The design suggestion [Struct representation for active patterns](https://github.com/fsharp/fslang-suggestions/issues/612) has been marked "approved in principle".

Likewise, the idea of a struct option type is implied by this suggestion and has also been approved.

* [x] Approved in principle
* [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/612)
* Details: [under discussion](https://github.com/fsharp/FSharpLangDesign/issues/230)
* Implementation: [Done](https://github.com/Microsoft/visualfsharp/pull/4837)

# Summary
[summary]: #summary

This RFC covers Part 1 of [RFC FS-1039 Struct representation for active patterns](../RFCs/FS-1039-struct-representation-for-active-patterns.md), but factors out the specific conern of a struct-based optional type, `ValueOption`.

# Motivation
[motivation]: #motivation

Powerful and extensible features such as Active Patterns and Optional Arguments should be near-zero-cost abstractions. A `ValueOption` type is a first step in this direction.

Additionally, in situations where a struct can provide a benefit over a reference type, `ValueOption` may allow developers to eek out more performance independently of their usage of Active Patterns and Optional Arguments.

# Detailed design
[design]: #detailed-design

An unboxed (struct) version of the `Option` type:

```fsharp
/// <summary>The type of optional values, represented as structs.</summary>
///
/// <remarks>Use the constructors <c>ValueSome</c> and <c>ValueNone</c> to create values of this type.
/// Use the values in the <c>ValueOption</c> module to manipulate values of this type,
/// or pattern match against the values directly.
[<StructuralEquality; StructuralComparison>]
[<CompiledName("FSharpValueOption`1")>]
[<Struct>]
type ValueOption<'T> =
    /// <summary>The representation of "No value"</summary>
    | ValueNone: 'T voption

    /// <summary>The representation of "Value of type 'T"</summary>
    /// <param name="Value">The input value.</param>
    /// <returns>An option representing the value.</returns>
    | ValueSome: 'T -> 'T voption

    /// <summary>Get the value of a 'ValueSome' option. An InvalidOperationException is raised if the option is 'ValueNone'.</summary>
    member Value : 'T

/// <summary>The type of optional values, represented as structs.</summary>
///
/// <remarks>Use the constructors <c>ValueSome</c> and <c>ValueNone</c> to create values of this type.
/// Use the values in the <c>ValueOption</c> module to manipulate values of this type,
/// or pattern match against the values directly.
and 'T voption = ValueOption<'T>
```

With a `defaultValueArg` function:

```fsharp
[<CompiledName("DefaultValueArg")>]
val defaultValueArg : arg:'T voption -> defaultValue:'T -> 'T 
```

# Drawbacks
[drawbacks]: #drawbacks

- Another thing F# programmers must learn (i.e., `Option` types can come in two forms based on how you want the code to run).
- Potentially confusing people who are not familiar with how performance vis-a-vis structs and reference types work on .NET, thus allowing them to think that this is merely a "faster" version of `Option`.

# Alternatives
[alternatives]: #alternatives

Not adding anything.

# Compatibility
[compatibility]: #compatibility

It's not breaking change due to it not requiring new syntax at all, just addition to FSharp.Core and changes in code generation.

# Unresolved questions
[unresolved]: #unresolved-questions

None.