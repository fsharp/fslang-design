# F# RFC FS-1344 - `Deconstruct` method support in tuple patterns

The design suggestion [Support C#-style Deconstruct method based pattern matching](https://github.com/fsharp/fslang-suggestions/issues/751) is approved in principle. This RFC is the focused successor to section **h** of [#800](https://github.com/fsharp/fslang-design/pull/800), as requested by its [design review](https://github.com/fsharp/fslang-design/pull/800#issuecomment-3852354596).

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/751)
- [x] Approved in principle
- [ ] Implementation
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

# Summary

When the input of an ordinary tuple pattern is already known to have a non-tuple type, allow the pattern to use a compatible instance or extension `Deconstruct` method.

```fsharp
type Person(name: string, age: int) =
    member _.Deconstruct(nameResult: string outref, ageResult: int outref) =
        nameResult <- name
        ageResult <- age

let name, age = Person("Ada", 36)
```

The feature adds no new syntax.

# Motivation

F# already presents .NET `out` parameters as tuple returns, so the method above can be consumed explicitly:

```fsharp
let name, age = person.Deconstruct()
```

The proposal lets an existing .NET decomposition contract participate directly in nested patterns, without requiring a type-specific active-pattern adapter. It is especially useful for C# records, which synthesize `Deconstruct`.

# Detailed design

## Pattern selection

For an ordinary tuple pattern with at least two components, apply the first matching case:

1. **Input shape unresolved:** constrain it to an F# reference tuple, as today. Do not defer or revisit this choice.
2. **Known reference tuple:** use existing tuple semantics and arity checking.
3. **Known non-tuple type:** select a valid `Deconstruct` method with the pattern arity, then match its outputs against the component patterns.

The explicit `struct (...)` form remains a struct-tuple pattern and never invokes `Deconstruct`. Component annotations do not help select a method; they are checked only after selection.

```fsharp
let pair (x, y) = x, y                    // unchanged tuple inference
let show ((name, age): Person) = name     // Deconstruct is eligible
```

## Eligible methods and overload resolution

A candidate must:

- have compiled name `Deconstruct`;
- be an accessible applicable instance or extension method;
- return CLI `void`;
- have exactly one `out` parameter per component, excluding an extension receiver.

Ordinary instance, inheritance, accessibility, generic, and extension lookup rules apply. Instance members take their normal precedence over extensions.

The compiler supplies fresh synthetic output locations. Receiver information and independently fixed generic arguments may determine applicability; component patterns do not. Therefore same-arity overloads that differ only in output types are ambiguous, and a generic type parameter occurring only in outputs cannot be inferred.

## Runtime semantics

For each logical test of a selected pattern:

1. evaluate the input once;
2. invoke the method once, using ordinary virtual or extension dispatch;
3. store each output in a fresh location;
4. test the corresponding component patterns.

Nested patterns and guards keep their existing semantics. A thrown exception propagates; it is not a failed match. A null instance receiver has ordinary instance-call behavior, while an extension receives the null value normally. The pattern compiler may share an extraction only where this preserves observable call count, effects, and exception behavior.

A selected decomposition is total at the extraction layer: partiality comes only from nested patterns. APIs requiring fallible extraction should use an active pattern or `Try...` operation.

## Quotations and reflected definitions

Explicit quotations and `[<ReflectedDefinition>]` use the same method selection. Their representation is the existing quotation lowering for an explicit call with omitted `out` arguments—fresh output locations, one method call, and subsequent matching—not an opaque `Deconstruct`-pattern or tuple-conversion node.

The quoted form must preserve dispatch, invocation count, output order, and exceptions. If the current quotation representation cannot express a required byref temporary or call, compilation rejects the quotation rather than dropping, duplicating, or replacing the operation.

## Exclusions

This RFC does not add zero- or one-output patterns, named output subpatterns, property/field extraction, dynamic `ITuple` lookup, `op_Implicit` tuple conversion, or a generic “deconstructable” constraint.

# Changes to the F# spec

- **Tuple patterns and inference:** add the ordered selection in [Pattern selection](#pattern-selection).
- **Member lookup and pattern compilation:** add the candidate and runtime rules above.
- **Quotations:** use the contract in [Quotations and reflected definitions](#quotations-and-reflected-definitions).

# Drawbacks

Ordinary tuple syntax can now invoke user code when its input type is known, so effects are less visible than with an active pattern. Same-arity overload additions can also make source ambiguous. Conversely, preserving tuple inference for unknown inputs means the syntax cannot define a generic function over all deconstructable values.

# Alternatives

- Require `Deconstruct(p1, p2)`: makes invocation explicit but adds a special pattern form and is less idiomatic for positional .NET APIs.
- Require active patterns: explicit and more general, but forces one adapter per foreign type.
- Add a deferred constraint: enables generic use but changes inference, generalization, diagnostics, and tooling; this broader direction was rejected in the review of #800.
- Use `op_Implicit` or `ITuple`: changes expression typing or introduces dynamic behavior instead of consuming the established static extraction contract.

# Prior art

C# deconstruction and positional patterns use instance or extension `Deconstruct` methods with `out` parameters. C# records synthesize such methods. F# active patterns remain the general facility for custom, partial, or named matching.

# Compatibility

The ordered selection in [Pattern selection](#pattern-selection) supplies a new interpretation only where current tuple checking cannot apply. Compiled code contains ordinary method calls and locals, so older compilers reject the new source interpretation but can consume produced assemblies. A later library version can add a competing same-arity method and create ambiguity, as with ordinary overload and extension-method versioning. The feature should initially be gated by the corresponding preview language version.

# Interop

The feature consumes the standard CLI shape used by C# records, hand-written methods, inherited virtual methods, and C# or F# extensions. It requires no FSharp.Core helper or F#-specific metadata.

# Pragmatics

## Diagnostics and tooling

Diagnostics identify the static input type and requested arity, and distinguish no method, invalid shape, uninferred generic arguments, and ambiguity. After selection, ordinary nested-pattern diagnostics apply.

FSharp.Compiler.Service exposes the selected method and output types. Hover distinguishes tuple projection from method-backed decomposition; navigation can target the method; refactorings preserve an annotation when removing it would restore tuple inference.

## Performance and scaling

A tested occurrence performs one call and creates one temporary per output, subject to semantics-preserving optimization. Candidate search uses ordinary member and extension lookup; pattern compilation otherwise scales as tuple-pattern compilation does today.

## Culture-aware formatting/parsing

Not applicable.

# Unresolved questions

Zero/one-output syntax and named positional subpatterns require separate proposals.
