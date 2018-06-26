# F# RFC FS-1060 - Nullable Reference Types

The design suggestion [Add non-nullable instantiations of nullable types, and interop with proposed C# 8.0 non-nullable reference types](https://github.com/fsharp/fslang-suggestions/issues/577) has been marked "approved in principle". This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [ ] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/577)
* [ ] Details: [TODO](https://github.com/fsharp/fslang-design/issues/FILL-ME-IN)
* [ ] Implementation: [TODO](https://github.com/Microsoft/visualfsharp/pull/FILL-ME-IN)

# Summary
[summary]: #summary

The main goal of this feature is to address the pains of dealing with implicitly nullable reference types by providing syntax and behavior that distinguishes between nullable and non-nullable reference types. This will be done in lockstop with C# 8.0, which will also have this as a feature, and F# will interoperate with C# by using and emitting the same metadata adornment that C# will emit.

Conceptually, reference types can be thought of as having two forms:

* Normal reference types
* Nullable reference types

These will be distinguished via syntax, type signatures, and tools. Additionally, F# will offer flow analysis to allow you to determine when a nullable reference type is non-null and can be "viewed" as a reference type.

Warnings are emitted when reference and nullable reference types are not used according with their intent, and these warnings are tunable as errors.

# Motivation
[motivation]: #motivation

Today, any reference type (e.g., `string`) could be `null`, forcing developers to account for this with special checks at the top-level of their functions or methods. Especially in F#, where types are almost always assumed to be non-null, this is a pain that can often be forgotten, resulting in a `NullReferenceException`, which has traditionally been difficult to diagnose. Moreover, one of the primary benefits in working with F# is with immutable and **non-null** data. This is in line with the general goals of F#.

The `[<AllowNullLiteral>]` attribute allows you to declare F# reference types as nullable when you need it, but this "infects" any place where the type might be used. In practice, F# types are not normally checked for `null`, which could mean that there is a lot of code out there that does not account for `null` when using a type that is decorated with `[<AllowNullLiteral>]`.

Additionally, nullable reference types is a headlining feature of C# 8.0 that will have a dramatic impact on .NET and its ecosystem. It is vital that we interoperate with this change smoothly and can emit the appropriate information so that C# consumers of F# components can consume things as if they were also C# 8.0 components.

The desired outcome over time is that significantly less `NullReferenceException`s are produced by code at runtime. This feature should allow F# programmers to more safely eject `null` from their concerns when interoperating with other .NET components, and make it more explicit when `null` could be a problem.

# Principles

The following principles guide all further considerations and the design of this feature.

1. On the surface, F# is "almost" as simple to use as it is today. In practice, it must feel simpler due to nullability of types like `string` being explicit rather than implicit.
2. The value for F# is primarily in flowing non-nullable reference types into F# code from .NET Libraries and from F# code into .NET libraries.
3. Adding nullable annotations should not be part of routine F# programming.
4. Nullability annotations/information is carefully suppressed and simplified in tooling (tooltips, FSI) to avoid extra information overload.
5. F# users are typically concerned with this feature at the boundaries of their system so that they can flow non-null data into the inner parts of their system.
6. The feature produces warnings by default, with different classes of warnings (nullable, non-nullable) offering opt-in/opt-out mechanisms.
7. All existing F# projects compile with warnings turned off by default. Only new projects have warnings on by default. Compiling older F# code with newer F# code may opt-in the older F# code.
8. F# non-nullness is reasonably "strong and trustworthy" today for F#-defined types and routine F# coding. This includes the explicit use of `option` types to represent the absence of information. No compile-time guarantees today will be "lowered" to account for this feature.
9. The F# compiler will strive to provide flow analysis such that common null-check scenarios guarantee null-safety when working with nullable reference types.
10. This feature is useful for F# in other contexts, such as interop with `string` in JavaScript via [Fable](http://fable.io/).
11. F# programmers can start to phase out their use of `[<AllowNullLiteral>]` and the `null` type constraint when they need ad-hoc nullability for reference types declared in F#.
12. Syntax for the feature feels "baked in" to the type system, and should not be horribly unpleasant to use.

# Detailed design
[design]: #detailed-design

This is the bulk of the RFC. Explain the design in enough detail for somebody familiar
with the language to understand, and for somebody familiar with the compiler to implement.
This should get into specifics and corner-cases, and include examples of how the feature is used.

## Overview and syntax

Nullable reference types can be thought of as a special case of [Erased type-tagged anonymous union types](https://github.com/fsharp/fslang-suggestions/issues/538). This is because, conceptually, reference types are already a union of the type and `null`. That is, `string` is implicitly either a `string` or `null`. This concept is lifted into syntax:

```
reference-type | null
```

Examples in F# code:

```fsharp
// Parameter to a function
let len (str: string | null) =
    match str with
    | null -> -1
    | s -> s.Length

// Return type
let findOrNull (index: int) (list: 'T list) : 'T | null =
    match List.tryItem index list with
    | Some item -> item
    | None -> null

// Declared type at let-binding
let maybeAValue : int | null = hopefullyGetAnInteger()

// Generic type parameter
type IMyInterface<'T | null, > =
    abstract DoStuff<'T | null> : unit -> 'T | null

// Array type signature
let f (arr: (float | null) []) = ()

// Nullable types with nullable generic types
type C<'T | null>() =
    member __.M(param1: List<List<'T | null> | null> | null) = ()
```

The syntax accomplishes two goals:

1. Lifts the concept that a reference type is either that type or null.
2. Becomes a bit unweildly when nullability is used a lot.

Because it is a design goal to flow **non-null** reference types, and **avoid** nullable reference types unless absolutely necessary.

Note that there is no way to declare an F# type to be unioned with `null`, e.g.,

```fsharp
type (NotSupportedAtAll() | null)() = class end
```

This is because `[<AllowNullLiteral>]` already exists, as we do not offer two syntaxes to accomplish the same thing.

## Checking of nullable references

TODO

## Checking of non-nullable references

TODO

## Flow analysis

TODO

## Emission of metadata

TODO

## Type inference

TODO

## Asserting non-nullability

TODO

## Tooling considerations

TODO

# Breaking changes

There are two forms of breaking changes with this feature:

* Non-null warnings (i.e., not checking for `null`) on a `string | null` type
* Nullable warnings

TODO

# Compatibility with existing F# nullability features

Today, classes declared in F# are non-null by default. To that end, F# has a way to express opt-in nullability with `[<AllowNullLiteral>]`. Attempting to assign `null` to a class without this attribute is a compile error:

```fsharp
type CNonNull() = class end

let mutable c = CNonNull()
c <- null // ERROR: 'CNonNull' does not have 'null' as a proper value.

[<AllowNullLiteral>]
type CNullable() = class end

let mutable c2 = CNullable()
c2 <- null // OK
```

Additionally, F# can constrain generic types to require types which support `null` as a proper value. Parameterizing a generic type with a type that does not support `null` as a proper value is a compile error:

```fsharp
type CNonNull() = class end

[<AllowNullLiteral>]
type CNullable() = class end

type C< ^T when ^T: null>() = class end

let c1 = C<CNonNull>() // ERROR: 'CNonNull' does not have 'null' as a proper value
let c2 = C<CNullable>() // OK
```

This behavior will remain unmodified. Any existing code that uses `[<AllowNullLiteral>]` and the `null` constraint will be source-compatible with this feature and exhibit the same compile-time behavior as before. Neither feature is up for deprecation.

# Drawbacks
[drawbacks]: #drawbacks

Why should we *not* do this?

# Alternatives
[alternatives]: #alternatives

What other designs have been considered? What is the impact of not doing this?

## Syntax

TODO `foo?`

## Warnings

TODO

## Assering non-nullability

TODO

# Unresolved questions
[unresolved]: #unresolved-questions

What parts of the design are still TBD?

