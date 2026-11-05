# F# RFC FS-1360 - C#-compatible members on F# records and unions

The design suggestions [#683](https://github.com/fsharp/fslang-suggestions/issues/683) (copy-and-update from C#), [#903](https://github.com/fsharp/fslang-suggestions/issues/903) (the CLI shape for C# `with`) and [#902](https://github.com/fsharp/fslang-suggestions/issues/902) (`Deconstruct` on union cases) are approved in principle.
This RFC covers the detailed proposal for all three.

- [x] Suggestions: [#683](https://github.com/fsharp/fslang-suggestions/issues/683), [#903](https://github.com/fsharp/fslang-suggestions/issues/903), [#902](https://github.com/fsharp/fslang-suggestions/issues/902)
- [x] Approved in principle
- [ ] Implementation (not started)
- [x] [Discussion](https://github.com/fsharp/fslang-design/pull/865)

Related: [FS-1127](../FSharp-7.0/FS-1127-init-only-and-required-properties.md) (F# consumes `init` and `required`), [FS-1346](https://github.com/fsharp/fslang-design/pull/842) (`{ r with }` for C# records, the mirror direction), [FS-1344](https://github.com/fsharp/fslang-design/pull/840) (F# consumes `Deconstruct`), [fslang-suggestions#1464](https://github.com/fsharp/fslang-suggestions/issues/1464) (unions).

# Summary

C# reads three metadata shapes: a ``<Clone>$`` method for `with`, `init` accessors for object initializers, and `Deconstruct` for positional patterns. When an F# project targets a framework that defines `System.Runtime.CompilerServices.IsExternalInit`, the compiler emits these members on records and union cases. F# source does not change.

```fsharp
type Person = { Name: string; Age: int }
type Shape = Circle of radius: float | Square of side: float
```

```csharp
var older = person with { Age = person.Age + 1 };   // <Clone>$ and an init accessor
var (name, age) = person;                           // Deconstruct
var area = shape switch
{
    Shape.Circle(var r) => Math.PI * r * r,         // Deconstruct on the case class
    Shape.Square(var s) => s * s,
};
```

The feature is `CSharpInteropMembers` (preview). Emission is automatic by target framework; there is no attribute.

# Motivation

- C# `with` works only on a type with a public ``<Clone>$`` method and `set` or `init` accessors. An F# record has neither, so C# code copies a record through its constructor and repeats every field (#683, #903).
- A C# positional pattern needs a `Deconstruct` method. A C# `switch` over an F# union reads `Item1` and `Item2` through property patterns (#902).
- F# 7 reads `init` and `required` members (FS-1127), and FS-1346 proposes `{ r with }` for C# records. The other direction is missing.
- A hand-written ``<Clone>$`` and `Deconstruct` on a `[<CLIMutable>]` record satisfy Roslyn today. The compiler can emit the same members for every record.

# Detailed design

## Condition

The compiler emits the members of this RFC when all of these hold:

- the language version supports `CSharpInteropMembers`;
- a framework reference defines `System.Runtime.CompilerServices.IsExternalInit` (net5.0 and later). The compiler finds it as it finds other optional framework types. A polyfill in a package is not found;
- the type is a record or a union, and its representation is public. This is the condition under which the all-fields constructor is public today.

When the type is missing, the compiler emits nothing and reports nothing. One condition covers all three members, so netstandard2.x libraries and FSharp.Core do not change.

The members are IL-only. F# code does not see them, as it does not see `NewCase` and `Tag`. They are public and carry `[CompilerGenerated]` and `[DebuggerNonUserCode]`.

## Records

For a reference record `R` the compiler emits:

- ``<Clone>$``: `public R <Clone>$()`, non-virtual. Records are sealed, and Roslyn accepts a non-virtual clone on a sealed type. The body calls the all-fields constructor with the current field values, the lowering of `{ r with }`. A record has no state outside its fields, so the copy is complete.
- An `init` accessor for every field that has no setter today: `set_X(value)` with return type `void modreq(IsExternalInit)`. Body, accessibility and nullness attributes are those of today's setters, and the property definition references the method. A `mutable` field and a `[<CLIMutable>]` record keep the plain `set` accessor, so ORMs and serializers still write them.
- `Deconstruct`: `public void Deconstruct(out T1 a, …, out Tn b)` over the fields in declaration order, for one or more fields. The parameters have the names and nullness attributes of the constructor parameters. The body stores each field.

A struct record gets the `init` accessors and `Deconstruct`, and no ``<Clone>$``. C# `with` copies a struct by value.

Backing fields are not `initonly` today, so no field changes. C# calls an `init` accessor only on the fresh result of ``<Clone>$`` or of `new R(…) { … }`. The optimizer and structural equality keep their assumptions about record fields.

## Unions

For every case of a reference union with at least one field, the compiler emits `Deconstruct` on the type that stores the fields of the case:

- the nested case class, for a union with several cases (`Shape.Circle`);
- the root type, when the fields live on the root: a single-case union, or the one allocated case of a `UseNullAsTrueValue` union.

The parameters have the names and nullness of the case constructor parameters (`item`, `item1`, or the declared names). A nullary case gets nothing. `[<DefaultAugmentation(false)>]` does not suppress `Deconstruct`: the member is IL-only and cannot collide with an F# name.

Struct unions, `list` and `option` are out of scope. A struct union has no type per case; [fslang-suggestions#1464](https://github.com/fsharp/fslang-suggestions/issues/1464) covers it.

## User members win

The compiler MUST NOT emit:

- ``<Clone>$`` when the type declares a member with that compiled name;
- `Deconstruct` with an arity that a user `Deconstruct` already has;
- either member when a field has that name.

No diagnostic is reported. A hand-written ``<Clone>$`` or `Deconstruct` keeps compiling.

# Changes to the F# spec

- [§8.4 Record Type Definitions](https://fsharp.github.io/fslang-spec/type-definitions/#84-record-type-definitions), compiled form: add the condition and the three members; state that F# code cannot access them.
- [§8.5 Union Type Definitions](https://fsharp.github.io/fslang-spec/type-definitions/#85-union-type-definitions), compiled form: add `Deconstruct` on each non-nullary case type, or on the root for root-stored cases.

# Drawbacks

- A recompile with a new SDK changes the public surface of every record and union on net5.0 and later. API-surface monitors report the new members. There is no per-type opt-out, only the language version.
- A library that reads `PropertyInfo.CanWrite` without a check for `IsExternalInit` now treats record properties as writable. `JsonConvert.PopulateObject` on an existing record now writes fields it skipped before.
- Assembly size grows by two methods per record, one per immutable field, and one per non-nullary union case.
- Roslyn fixes the ``<Clone>$`` shape; no specification does.

# Alternatives

- **Opt-in attribute** (`[<CLIRecord>]`, precedent `[<CLIMutable>]`): no surprise on recompile; every library author must add it, and most would not.
- **Opt-out attribute**: can be added later without a break.
- **MSBuild property or compiler flag** (precedent `--realsig`): a project-wide switch that moves the decision to the build.
- **A `With(…)` method family** (#683 as filed): needs optional parameters and does not light up C# `with`.
- **`Deconstruct` only**: no framework dependency and no setter; loses `with`.
- **`MemberwiseClone` body**: needed only if records gain state outside their fields.

# Prior art

C# records compile to this exact shape, and Roslyn reads the shape, not an attribute. F# consumes `init` and `required` members (FS-1127). FS-1346 proposes `{ r with }` for C# records.

# Compatibility

- No source change. F# code does not observe the members; F# consumers read signature data. An assembly compiled with `--nointerfacedata` is read as plain IL, and F# then sees `Deconstruct`, the `init` accessors and ``<Clone>$``.
- Binary: additive. The constructor, `CompilationMapping` and `FSharp.Reflection` do not change.
- Older F# compilers ignore the members. C# 8 and VB see an unspeakable ``<Clone>$`` and setters they cannot call; they could not call any setter before.
- System.Text.Json and Newtonsoft.Json construct a record through its single public constructor, as today.
- FSharp.Core targets netstandard2.0 and netstandard2.1 and does not change.

# Interop

C# 9 and later use `with`, positional patterns and deconstruction on F# records and union cases. VB sees the `init` accessors as unsupported.

# Pragmatics

## Diagnostics

None. A user member silently replaces a generated one.

## Tooling

FSharp.Compiler.Service, signature files and `--refonly` do not change; the setters survive `--refonly` like other accessors. Debuggers skip the members because of `[DebuggerNonUserCode]`.

## Performance

``<Clone>$`` allocates one record. The other members run only when C# calls them.

## Scaling

Not applicable.

## Culture-aware formatting/parsing

Not applicable.

# Unresolved questions

- `Deconstruct` alone on frameworks without `IsExternalInit`; it would then also reach FSharp.Core.
- A public polyfilled `IsExternalInit` in a referenced package.
- Parameter names of `Deconstruct`: camelCase like the constructor, or PascalCase like C# records.
- FSharp.Core types (`option`, `voption`, `Result`) once FSharp.Core targets a modern framework.
- The feature name `CSharpInteropMembers` is a placeholder.
