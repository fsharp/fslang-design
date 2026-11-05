# F# RFC FS-1346 - Copy-and-update expressions for C# records and structs

The design suggestion [Support F# record syntaxes for C# defined records](https://github.com/fsharp/fslang-suggestions/issues/1138) is approved in principle. This RFC is the focused successor to section **t** of [#800](https://github.com/fsharp/fslang-design/pull/800), as requested by its [design review](https://github.com/fsharp/fslang-design/pull/800#issuecomment-3852354596). Construction and type inference from member names are outside scope.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1138)
- [x] Approved in principle
- [ ] Implementation
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

# Summary

Extend the existing copy-and-update syntax to a statically known C# record class or non-byref-like CLI struct.

```fsharp
// C#: public record Person(string Name, int Age);
let birthday (person: Person) =
    { person with Age = person.Age + 1 }

// C#: public readonly record struct Point(int X, int Y);
let moveX (point: Point) =
    { point with X = 10 }
```

# Motivation

C# records expose non-destructive mutation through `with`, including virtual cloning that preserves a derived runtime record. Reconstructing through a public constructor can lose hidden state or slice inheritance, while `<Clone>$` is not a normal source-level member.

Record structs have no reliable record marker in CLI metadata. C# therefore defines `with` for structs generally as copy-and-assign. Applying the same rule gives F# reliable record-struct interoperation without a heuristic.

# Detailed design

## Interpretation and receiver type

The compiler first tries the existing F# record or anonymous-record interpretation, unchanged. Otherwise it checks the receiver independently of the initializer labels. The nominal interpretation applies only when that static type is fully known and eligible; labels do not infer a nominal type or constrain a bare type parameter.

A constructed generic receiver is allowed when its arguments are already known, and the result retains the same constructed type.

## Eligible receivers

A reference type is an eligible record class when it has exactly one method satisfying the Roslyn record contract:

- compiled name `<Clone>$`;
- public, instance, parameterless, and non-generic;
- virtual, override, or abstract unless the containing type is sealed;
- the containing type is identical to or derives from the method's return type.

The metadata shape is authoritative regardless of source language. No copy-constructor, `Clone`, `ICloneable`, reflection, or serialization fallback is attempted.

Any statically known CLI value type is eligible except a value already handled by the F# record branch, a byref-like type, a nullable wrapper used without making its underlying value explicit, or a bare generic type parameter. This includes ordinary structs and C# record structs.

## Eligible members

Each label resolves by ordinary nominal lookup on the static receiver type. It must name an accessible instance member that is either:

- a non-readonly field; or
- a non-indexed property with an accessible `set` or `init` accessor.

Static members, events, methods, indexers, get-only properties, readonly fields, inaccessible setters, and duplicate labels are errors. Inherited members follow normal accessibility and hiding rules. Each right-hand expression is checked against the selected member type using existing expected-type and conversion rules.

## Evaluation and lowering

The receiver is evaluated once. A result temporary is then created as follows:

| Receiver | Initial value of result temporary |
| --- | --- |
| Record class `R` | invoke the selected clone once using virtual dispatch, then convert or checked-cast its result to `R` |
| Struct `S` | copy the receiver value once into a mutable local of type `S` |

For each initializer from left to right, evaluate its right-hand expression once and immediately assign it to the result temporary. Return that temporary with the static receiver type.

This ordering governs all effects and exceptions. If cloning, conversion, an expression, or a setter throws, the exception propagates; earlier effects are not rolled back. A null class receiver has ordinary instance-call behavior.

An accessible init-only setter is permitted only on this fresh result temporary. Ordinary assignment to an existing object's init-only property remains invalid. Required-member checking is not repeated because cloning or copying preserves the receiver's existing state.

## Quotations and reflected definitions

Explicit quotations and `[<ReflectedDefinition>]` preserve the nominal interpretation and lowering.

- A class update is represented with existing bindings, method call, conversion, and field/property assignment quotation forms.
- A struct update is represented as an unboxed mutable local initialized from the receiver, assignments to that local, and the local's final value.

Mutating the original struct or a boxed copy is not equivalent. No generic record-update quotation node is introduced. If the quotation implementation cannot preserve the mutable-local copy semantics, compilation rejects that quotation rather than changing the operation.

## Exclusions

This RFC does not add construction syntax, field-name-driven nominal inference, generic record/clone constraints, runtime-type dispatch through an interface or `obj`, byref-like updates, compound initializers, property patterns, or conversion between F# and C# records.

# Changes to the F# spec

- **Record expressions and inference:** add the ordered nominal interpretation in [Interpretation and receiver type](#interpretation-and-receiver-type).
- **Metadata and initializers:** add the receiver and member predicates above.
- **Evaluation and quotations:** adopt the contracts in [Evaluation and lowering](#evaluation-and-lowering) and [Quotations and reflected definitions](#quotations-and-reflected-definitions).

# Drawbacks

The syntax can denote either compiler-known F# field copying or runtime clone/setter calls, so tooling must reveal the selected interpretation. Clone methods and setters may have effects. General struct support is broader than record structs and can copy large values. Requiring a known receiver type also prevents generic structural update.

# Alternatives

- Use C#-shaped `receiver with { ... }`: makes the metadata model visible but adds syntax for an operation already expressed by F# update notation.
- Select a copy constructor: can slice derived records and may be inaccessible.
- Use `ICloneable` or reflection: neither represents the C# record contract or permits init-only assignment safely.
- Support only record classes: leaves record structs without a reliable detection mechanism.
- Infer nominal types from labels: expands F# field-label inference into a global, ambiguous search.

# Prior art

F# records and anonymous records already support copy-and-update. C# record classes use virtual clone then member initialization; C# structs use value copy then member initialization. F# already consumes init-only and required members under FS-1127.

# Compatibility

Because the nominal interpretation is attempted only after existing F# record checking fails, it makes previously invalid expressions valid without reinterpreting current ones. Produced assemblies contain ordinary calls, casts, copies, and stores, so older compilers can consume them and no FSharp.Core or metadata addition is required. A library update can change eligibility or member writability through ordinary metadata versioning. The feature should initially be gated by the corresponding preview language version.

# Interop

The feature consumes and emits only CLI metadata and operations; no wrapper or F#-specific protocol is introduced.

# Pragmatics

## Diagnostics and tooling

Diagnostics distinguish an unknown receiver, an invalid clone shape, an ineligible struct, an unresolved or non-writable member, a duplicate initializer, and a type mismatch. FSharp.Compiler.Service distinguishes nominal update from F# record update and exposes the clone/member symbols. Completion lists writable members; navigation targets the selected symbols; hover reports clone or copy semantics.

## Performance and scaling

A class update has the cost of its clone and one assignment per initializer. A struct update copies its receiver and may be expensive for a large value. Recognition is a bounded lookup for `<Clone>$`; no global search by label occurs.

## Culture-aware formatting/parsing

Not applicable.

# Unresolved questions

Byref-like structs and generic copy-and-update constraints require separate proposals.
