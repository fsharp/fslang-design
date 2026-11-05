# F# RFC FS-1361 - Interop with C# unions and closed hierarchies

The design suggestion [Interop with C# Union Types and Closed Hierarchies](https://github.com/fsharp/fslang-suggestions/issues/1464) has not been marked "approved in principle" yet.
This RFC covers the detailed proposal for its consumption half: F# reads C# unions and closed classes. Emission on F# types is left to a later RFC. The design follows [discussion 1463](https://github.com/fsharp/fslang-suggestions/discussions/1463).

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1464)
- [ ] Approved in principle
- [ ] Implementation (not started)
- [x] [Discussion](https://github.com/fsharp/fslang-design/pull/866)

Related: C# [unions](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md) and [closed hierarchies](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/closed-hierarchies.md), [FS-1093](../FSharp-6.0/FS-1093-additional-conversions.md) (type-directed conversions), [FS-1360](https://github.com/fsharp/fslang-design/pull/865) (C#-compatible members on F# types), [#1215](https://github.com/fsharp/fslang-suggestions/issues/1215) (sealed classes).

# Summary

C# 15 ships union types and closed classes. F# matches them with case-type names as pattern names, with exhaustiveness. A case value converts to its union at a known target type. F# rejects inheriting a closed class.

```csharp
public record Cat(string Name);
public record Dog(string Name);
public union Pet(Cat, Dog);

public closed class GateState { }
public sealed class Locked : GateState { }
public sealed class Open(float percent) : GateState { public float Percent => percent; }
```

```fsharp
let describe (pet: Pet) =
    match pet with
    | Cat c -> c.Name
    | Dog d -> d.Name
    | null -> "no pet"            // Value is object?, so null is a case

let gate (state: GateState) =     // exhaustive
    match state with
    | Locked -> "locked"
    | Open o -> string o.Percent

let p : Pet = Cat "Tom"           // union conversion
```

The feature is `UnionAndClosedTypeInterop` (preview).

# Motivation

- `union` is in C# 15, and .NET 11 libraries will expose it. Today F# calls the constructor and reads `Value : obj | null`. `:? Cat` on a `Pet` value is FS0016, because a struct has no subtypes, and a match on `pet.Value` warns FS0025.
- A `closed class` promises a fixed set of subtypes, but F# warns FS0025 on a match over them. F# also accepts `inherit GateState()`, which C# forbids through `[CompilerFeatureRequired("ClosedClasses")]`.
- Exhaustive matching across the language boundary is the point of both features.

# Detailed design

## Recognition

- A *union type* is an imported type with `[System.Runtime.CompilerServices.UnionAttribute]` and a public `Value : object` getter. Its *case types* are the parameter types of its public constructors with one parameter, by value or `in`. When the type nests an interface `IUnionMembers`, the case types are the parameter types of the interface's static `Create` methods, and `Value` is read from the interface.
- A *closed class* is an imported class with `[System.Runtime.CompilerServices.IsClosedTypeAttribute]`. Its *direct subtypes* are the types of its assembly whose base type is the class. The compiler computes the set once per assembly and keeps the subtypes whose base instantiation unifies with the scrutinee type.
- A type that misses a part of the shape is an ordinary type.

## Case patterns

The static type of the scrutinee can be a union type, a closed class, or `Nullable<U>` of a struct union. Then an identifier in pattern position resolves against the *case set* first: the case types of the union, or the direct subtypes of the class.

- A single identifier names a case type by its name without arity: `Cat`, `String`, `Int32`, or the name of the union's type parameter. Otherwise the identifier resolves as a type name in scope: `int`, `string`, `Zoo.Cat`, an alias.
- `Pet.Cat c` names the case through the union and also fixes the scrutinee type. Use it when the scrutinee type is not known yet.
- A lowercase identifier without an argument stays a variable pattern.
- A case pattern takes zero or one argument: `Cat`, `Cat c`, `Cat _`, and `Cat (Kitten k)` when `Cat` is itself a union. The argument is an ordinary pattern of the case type, so every pattern form F# has or gains for that type composes: `Cat (name, _)` with FS-1344, `Cat { Name = n }` if C# records get record patterns. `as`, or-patterns and `when` work as for F# union cases.
- Two case types with the same name (`union U(List<int>, List<string>)`) are an error.
- The scrutinee type MUST be known at the pattern. Otherwise the compiler reports FS0039 with a hint to annotate the scrutinee or to qualify the case.

The pattern binds a symbol of the case type's entity, so tooling navigates to the C# type.

## Semantics

The *held value* `v` of a scrutinee `u` is `u.Value` for a struct union. For a class union or `Nullable<U>` it is `null` when `u` is null, else `u.Value`. The compiler reads `Value` at most once per union value per match.

- `Cat c` succeeds when `v` is a non-null instance of `Cat`; `c` is `v` as `Cat`. A case type `Nullable<T>` takes the name `T` and binds a `T`.
- `null` succeeds when `v` is null. On a struct union whose `Value` is non-nullable, `null` is FS0043.
- On a closed class the case pattern is an ordinary type test on the scrutinee. FS0016 does not apply.

The compiler MAY implement a case test with `TryGetValue(out T)` and `HasValue` when the union declares them and the result is the same. Quotations use the `Value` form.

## Exhaustiveness

- A union match is complete when a tested type subsumes every case type, and `null` is covered when `Value` is annotated nullable. Every C# `union` declaration emits `object?`. An unannotated `object` does not require `null`.
- A closed-class match is complete when every direct subtype is covered, or is itself closed and complete. `:? T` tests count.
- Overlapping case types (`union Content(string, object)`) are tested in source order. A case that an earlier test covers is FS0026.
- The counter-example of FS0025 is the first uncovered case, shown as `Dog _`, or `null`.
- A complete match keeps a default branch that throws `MatchFailureException`. It covers a case type that a newer C# assembly adds, and an unexpected null. The last arm does not become the default as it does for an F# union.

## Union conversion

At a known expected type `U` that is a union type, an expression of type `A` converts when exactly one case type equals `A`. Failing that, it converts when exactly one case type subsumes `A`. The compiler calls that constructor, or `Create`. Two candidates are an error. The conversion is one step: `Cat` does not reach `Animal` through `Pet`. It applies where FS-1093 applies `op_Implicit`: method arguments and expressions with a known type. A new warning, off by default, reports the conversion. In overload resolution it counts as a type-directed conversion: an overload that takes `Cat` wins over one that takes `Pet`.

## Closed hierarchies

- `inherit C(…)` and `{ new C(…) with … }` are errors when `C` is a closed class. F# code is never in the defining assembly.
- The compiler reads the feature name of `[CompilerFeatureRequired]`. `RequiredMembers` keeps its behaviour, and `ClosedClasses` is understood. Any other name on a member that F# code uses is an error, as in Roslyn. This rule can break a library that emits an unknown name, so the feature gates it.
- The interface-convertibility restriction of C# is not adopted.

## Nullness

A bound variable has the non-null case type. With nullness checking off, an instance-null arm is not required; the default branch throws. The compiler reads the annotation of `Value` even with nullness checking off.

# Changes to the F# spec

- [§7 Patterns](https://fsharp.github.io/fslang-spec/patterns/): add the case pattern form and its completeness rule.
- [§14.4 Type-directed conversions](https://fsharp.github.io/fslang-spec/inference-procedures/#144-method-application-resolution): add the union conversion.
- [§8 Type Definitions](https://fsharp.github.io/fslang-spec/type-definitions/): add the closed-class restriction on `inherit` and object expressions.

# Drawbacks

- A scrutinee-directed name lookup is new to F# patterns. Today a pattern identifier never depends on the scrutinee type.
- `| Locked ->` on a closed-class scrutinee changes meaning: a variable with FS0049 today, a test with this RFC.
- A complete match keeps a throw branch, unlike a match on an F# union.
- F# depends on the structural checks of Roslyn.

# Alternatives

- **`:? Cat as c` with exhaustiveness**: no new name rules, but FS0016 on struct unions must go, lowercase types read oddly, and nested unions do not compose.
- **`Cat as c`**: saves `:?` only; still a new syntax.
- **Canonical names only** (`Int32 i`): avoids lowercase case names. This RFC accepts both.
- **Require `Pet.Cat`**: unambiguous and verbose.
- **Active patterns per union**: no exhaustiveness, one adapter per type.

# Prior art

C# 15 unions and closed hierarchies. F# union cases: a case name is a pattern name. FS-1093 type-directed conversions. Kotlin sealed classes ([#1215](https://github.com/fsharp/fslang-suggestions/issues/1215)).

# Compatibility

- `| Cat c` is FS0039 today, so no code changes meaning. `| Locked ->` on a scrutinee of a union or closed type was a variable with FS0049; such types exist only in .NET 11 assemblies.
- Older compilers reject the new patterns and the conversion. Compiled code contains ordinary calls and type tests, so older compilers consume the assemblies.
- The `CompilerFeatureRequired` rule is the only potential break for existing code, and the feature gates it.
- FSharp.Core does not change.

# Interop

F# reads the same metadata shapes as Roslyn. The RFC does not change what C# sees in F# assemblies; FS-1360 and a later RFC cover emission.

# Pragmatics

## Diagnostics

| Number | Condition | Severity |
|---|---|---|
| FS0025 | incomplete match; counter-example `Dog _` or `null` | warning |
| FS0026 | a case that an earlier test covers | warning |
| FS0039 | case not found; hint to annotate or qualify | error |
| FS0043 | `null` on a struct union with non-nullable `Value` | error |
| new | two case types match the identifier, or accept the converted expression | error |
| new | the identifier is not a case of `U` or a subtype of `C`; more than one argument | error |
| new | union conversion used | warning, off |
| new | `inherit` or object expression on a closed class; a used member requires an unknown compiler feature | error |

## Tooling

Completion after `|` lists the case names and `null`. Hover and Go to Definition target the case type.

## Performance

The compiler computes the case set once per type and the subtype set once per assembly, and caches both. A match reads `Value` once.

## Scaling

Not applicable.

## Culture-aware formatting/parsing

Not applicable.

# Unresolved questions

- Names for case types that are type parameters of the union.
- Explicit instantiation for ambiguous generic case names (`List<int> xs`).
- `Nullable<U>` scrutinees, and whether F# collapses the two null states of a class union as C# does for `Nullable<U>`.
- Whether to commit to the `TryGetValue` lowering.
- The feature name `UnionAndClosedTypeInterop` is a placeholder.
