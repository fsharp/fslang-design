# F# RFC FS-1342 - Contextual target typing for numeric and collection literals

The design suggestion [Type inference for literals](https://github.com/fsharp/fslang-suggestions/issues/1421) is approved in principle. This RFC is the focused successor to [#800](https://github.com/fsharp/fslang-design/pull/800) and contains only levels 1–2 approved by its [design review](https://github.com/fsharp/fslang-design/pull/800#issuecomment-3852354596).

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1421)
- [x] Approved in principle
- [ ] Implementation
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

# Summary

Allow an unsuffixed numeric literal or eligible bracket expression to use a fully known target type when it is:

1. an argument to a CLI method, constructor, indexer, property setter, or known delegate invocation; or
2. checked under an explicit type annotation.

```fsharp
type Texture =
    static member Upload(data: System.ReadOnlySpan<byte>, opacity: float32) = ()

Texture.Upload([0; 64; 128; 255], 0.75)

let kind: byte = 3
let data: byte array = [0; 64; 128; 255]
```

# Motivation

At .NET API boundaries, F# callers must often restate an already known parameter type with numeric suffixes, array syntax, span conversion, or collection factories. Contextual construction removes that ceremony and can avoid an intermediate F# list while retaining compile-time numeric range checks.

The restricted contexts preserve local reasoning and existing inferred signatures.

# Detailed design

## Context and ordering

A target is **fully known** when no unresolved inference variable relevant to the conversion remains. The literal cannot itself determine a generic argument. Outside the two entry points in the summary, current defaults and inference apply; no literal constraint or backwards inference is introduced.

For a non-overloaded target, first check the expression using current F# rules, including [FS-1093](../FSharp-6.0/FS-1093-additional-conversions.md). Attempt contextual literal conversion only if that fails.

For an overloaded call:

1. **Phase A:** determine applicability and the winner with current rules only. If any candidate is applicable, finish without contextual conversions.
2. **Phase B:** otherwise infer generic arguments from explicit type arguments and non-contextual arguments, discard candidates whose literal target remains unknown, and check the rest contextually.

Thus the feature does not replace an overload or conversion selected by an existing valid program.

Context from an annotation may pass through parentheses and result branches of `if`, `match`, and `try`. It does not cross a local binding, lambda, call, pipeline, computation expression, or operator application. Ordinary curried F# function application is outside the first version.

```fsharp
let ratio: float32 = 1 / 2             // operands use current rules
let ratio2: float32 = (1.0f / 2.0f)
```

## Numeric literals

Eligible syntax is an unsuffixed integer-form or floating-point-form literal, including existing bases, exponents, and separators. An adjacent unary sign participates in range checking. A suffix fixes the source type and disables contextual reparsing.

| Source form | Direct contextual targets | Current default |
| --- | --- | --- |
| Integer form | `sbyte`, `byte`, `int16`, `uint16`, `int32`, `uint32`, `int64`, `uint64`, `nativeint`, `unativeint`, `bigint`, `float32`, `float`, `decimal` | `int32` |
| Floating-point form | `float32`, `float`, `decimal` | `float` |

Transparent aliases use their underlying intrinsic type. The compiler parses directly for the target and reuses the corresponding suffixed literal's rounding, overflow, underflow, constant generation, and units-of-measure rules. No runtime narrowing conversion is emitted.

```fsharp
let ok: byte = 255
let error: byte = 256 // compile-time range error
```

The first version adds no direct construction for enums, nullable types, `Half`, `Int128`, `UInt128`, `NFloat`, `Complex`, `Rune`, `INumberBase<_>`, `NumericLiteralX`, or a multi-step numeric-plus-`op_Implicit` path. Existing conversions to such types remain available.

An explicitly typed `[<Literal>]` binding may use the rule when its target is otherwise a valid CLI literal type. Literal patterns are unchanged.

## Collection expressions

### Syntax and targets

The eligible bracket forms contain only:

- `[]`;
- expression elements, with optional `yield`;
- spread elements written `yield!`.

Ranges and collection-level `for`, `while`, `let`, `use`, `try`, `if`, or `match` retain their current list/sequence-expression meaning.

An eligible expression may target:

1. a one-dimensional array;
2. `Span<T>` or `ReadOnlySpan<T>`;
3. a type with a valid `CollectionBuilderAttribute`;
4. a class or struct implementing `System.Collections.IEnumerable`, with an accessible applicable parameterless constructor and, for non-empty input, an accessible applicable instance or extension `Add`;
5. `IEnumerable<T>`, `IReadOnlyCollection<T>`, `IReadOnlyList<T>`, `ICollection<T>`, or `IList<T>`.

Existing list construction remains available through ordinary checking. Multidimensional arrays and nullable lifting are outside the first version.

Each target supplies one element type `T`. Expression elements are checked against `T`; a spread's iteration type must convert to `T`. Numeric and nested collection elements may themselves be contextual. The empty form requires a target that determines `T`.

Source element and spread expressions are evaluated exactly once from left to right, before count queries or enumeration used to materialize the result.

### Builders and constructible targets

A `CollectionBuilderAttribute` target follows the .NET collection-expression contract. Its named builder type must be a non-generic class or struct with exactly one accessible directly declared static method whose generic arity matches the target, whose only parameter is `ReadOnlySpan<E>`, and whose return converts by identity, reference, or boxing conversion to the target. `E` must be the target iteration type.

A constructible target is created once, preferring an applicable `capacity: int` constructor when length is known and otherwise using the parameterless constructor; selected `Add` calls then receive elements in order. Mutable interface targets use `List<T>`. Non-mutable listed interfaces use an existing or synthesized implementation with the observable read-only behavior required by the .NET collection-expression contract.

### Arrays, spans, and escape scope

Known- and unknown-length expressions are valid. The compiler may use static data, inline or stack storage, a growable buffer, or a heap array, provided it preserves target mutability, source evaluation, effects, exceptions, and aliasing. No stack allocation is guaranteed and no intermediate F# list is semantically required.

For byref-safety analysis, a collection converted directly to `Span<T>` or `ReadOnlySpan<T>` has the containing declaration block as its safe-to-escape scope, independent of the chosen backing storage. It may be used for a direct call or within that block, but cannot be returned, captured, or stored beyond it.

For another byref-like target created through a collection builder, compute the scope as for an invocation of that create method with the constructed `ReadOnlySpan<E>` argument. Existing byref-like restrictions then apply. Lowering choices must not widen or narrow the source-level escape permission.

### Quotations and reflected definitions

Contextual selection occurs before quotation construction.

- A contextual numeric literal is quoted as the existing constant expression for its selected intrinsic type.
- A non-byref-like contextual collection is quoted using existing array, binding, constructor, `Add`, and builder-call forms for the selected construction, preserving evaluation order and fresh-object semantics.
- A contextual collection whose result is byref-like, including direct span targets, is rejected in explicit quotations and `[<ReflectedDefinition>]` in the initial implementation.

No target-neutral collection quotation node is introduced, and the compiler must not retarget an expression merely to make it quotable.

## Phase-B overload preferences

After ordinary better-member rules, compare contextual conversions as follows.

For numeric literals, retaining the current default type is better than selecting a different intrinsic type. Between two non-default targets, use an existing better-conversion-target relation when one exists; otherwise the call is ambiguous.

For collections, compare every element conversion. One candidate is better only if it is no worse for all elements and better for at least one. If element types are identical, `ReadOnlySpan<T>` is better than `Span<T>`, and either span is better than an array or listed array-interface target. Between non-span targets, an unambiguous implicit conversion from one target to the other can make the former better. Otherwise neither is better.

Diagnostics list the contextual target for each ambiguous candidate.

# Changes to the F# spec

- **Expression checking and method application:** add the contextual mode, propagation boundary, and two-phase ordering in [Context and ordering](#context-and-ordering).
- **Numeric literals:** add direct target parsing for the table above without changing lexical grammar.
- **Collection expressions:** adopt the syntax, target, construction, ref-safety, and quotation contracts above.
- **Inference:** add no constraint form, generalization rule, or inferred signature.
- **FSharp.Compiler.Service:** expose the selected target, element type, overload, and construction members.

# Drawbacks

A literal can denote more than one representation when its target is external to the syntax. Direct CLI calls and ordinary F# function calls are initially asymmetric. Extracting a literal into an unannotated binding may require adding the target annotation. Compiler, tooling, quotation, overload, and ref-safety implementations gain new cases, and concise collection syntax can still invoke allocating or effectful builders.

# Alternatives

- Keep suffixes and factories: explicit but retains interop ceremony and possible intermediate collections.
- Use only `op_Implicit`: cannot provide direct target parsing for every narrowing literal and can allocate before conversion.
- Add generic literal constraints or backwards inference: changes generalization, diagnostics, tooling, and existing signatures; rejected in the review of #800.
- Add new collection syntax: avoids contextual brackets but adds another notation and loses adaptation of idiomatic F# syntax.
- Prefer new span overloads over existing candidates: can change behavior; Phase A instead preserves current programs.
- Support only arrays and spans: omits standard builder-backed immutable and domain collections.

# Prior art

C# has constant-expression numeric conversions, target-typed collection expressions, `CollectionBuilderAttribute`, ref-safety rules, and element-based better-conversion rules. FS-1093 establishes F#'s last-resort type-directed conversion model.

# Compatibility

The fallback ordering in [Context and ordering](#context-and-ordering) makes previously invalid annotated or call-site forms valid without changing an existing winner. Produced assemblies use existing CLI constants, arrays, spans, constructors, and calls, so older compilers can consume them, subject to their target frameworks, and no FSharp.Core dependency is added.

A later overload can change or make ambiguous a call already relying on Phase B, as with other overload versioning. The feature should initially be gated by the corresponding preview language version.

# Interop

The numeric targets and collection protocols are standard CLI/.NET types and metadata. Builders and `Add` methods may be authored in any CLI language; no F#-specific construction protocol is required.

# Pragmatics

## Diagnostics and tooling

Diagnostics distinguish unknown targets, numeric range or form errors, incompatible elements or spreads, invalid builders, Phase-B ambiguity, and byref escape violations. Successful contextual conversion is silent by default; an optional warning, disabled by default, may flag a call-site numeric target different from its default or a call-site bracket expression constructing a non-list target. Explicit annotations do not warn, and existing FS-1093 warning behavior is unchanged. FSharp.Compiler.Service exposes the selected conversion and construction. Hover reports the contextual representation; navigation can target a builder; extraction refactorings preserve the necessary annotation.

## Performance and scaling

Numeric conversion is compile-time only. Collection construction is linear in produced elements, subject to spread enumerators and builders. Phase B runs only after ordinary candidate checking fails; implementations should cache candidate conversion and builder validation.

## Culture-aware formatting/parsing

Numeric source parsing and collection construction are culture-invariant.

# Unresolved questions

Separate proposals may consider ordinary F# function application, ranges and the wider sequence-expression grammar, nullable lifting, and additional intrinsic numeric targets.
