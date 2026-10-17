# F# RFC FS-1356 - `allows ref struct` constraint on F# type parameters

The design suggestion [Allow F# type parameters to accept byref-like types](https://github.com/fsharp/fslang-suggestions/issues/1476) has not been marked "approved in principle" yet.
This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1476)
- [ ] Approved in principle
- [ ] Implementation (not started)
- [ ] Discussion

Related: [FS-1053 Span and byref-like structs](../FSharp-4.5/FS-1053-span.md); F# 9 consumption of the constraint, [dotnet/fsharp#16800](https://github.com/dotnet/fsharp/issues/16800) and [dotnet/fsharp#17597](https://github.com/dotnet/fsharp/pull/17597).

# Summary

A type parameter with the constraint `allows ref struct` accepts byref-like types, such as `Span<'T>`, as type arguments. Its definition treats the type parameter as a byref-like type. F# 9 accepts such type arguments when C# declares the constraint. This RFC lets F# declare it. The feature is `AllowsRefStructConstraint` (preview).

```fsharp
let tryFindBy<'Alternate when 'Alternate : allows ref struct and 'Alternate : not null>
    (key: 'Alternate) (map: FrozenDictionary<string, int>) =
    let mutable value = 0
    if map.GetAlternateLookup<'Alternate>().TryGetValue (key, &value) then ValueSome value else ValueNone

tryFindBy ("a".AsSpan ()) map   // 'Alternate = ReadOnlySpan<char>
```

# Motivation

.NET 9 added the anti-constraint to core generics: `Func<_, _>`, `Action<_>`, `IEnumerable<_>`, `IEquatable<_>`, and the alternate lookups of `Dictionary`, `HashSet`, `FrozenDictionary` and `FrozenSet`. F# 9 calls them with byref-like type arguments. But an F# definition cannot give its own type parameter to them:

- A generic F# helper over such an API gives FS0412 when the type argument is a span. Libraries repeat the helper for each type or write it in C#.
- A type extension of `IEnumerable<'T>` does not compile on .NET 9. The extension cannot restate the constraint of `'T` (FS0957, [dotnet/fsharp#18001](https://github.com/dotnet/fsharp/issues/18001)).
- An F# override or interface implementation of such a C# method loses the constraint.
- FSharp.Core cannot declare the constraint on functions that are safe for byref-like types, for example `Unchecked.defaultof` and `raise` ([#952](https://github.com/fsharp/fslang-suggestions/issues/952)).

# Detailed design

## Syntax

The constraint grammar gets one form:

```fsgrammar
constraint :=
    ...
    typar ':' allows ref struct    -- byref-like type arguments allowed
```

`allows` is a keyword only in this position. Tooltips and FS0341 already print this form for C#-declared type parameters.

```fsharp
type ISink<'T when 'T : allows ref struct> =
    abstract Put: 'T -> unit

[<Struct; IsByRefLike>]
type Pair<'T when 'T : allows ref struct> =
    val First: 'T
    val Second: 'T
    new (first, second) = { First = first; Second = second }
```

- Code MUST declare the constraint explicitly. Inference and generalization never add it, so existing signatures do not change.
- Other type parameters do not inherit it, as in C#. In `'U when 'U :> 'T`, `'U` allows ref structs only if `'U` declares the constraint.
- Code MUST NOT combine it with a constraint that requires a reference type: `not struct`, `null`, `delegate<_, _>` or a subtype constraint to a class type (C# CS9243).
- Code MAY declare it on the type parameters of functions, members, `inline` functions (including `^T`), type definitions and type extensions.

## Instantiation

A type parameter that allows ref structs accepts two more kinds of type arguments: byref-like types and type parameters that allow ref structs. This rule applies to type parameters that F# or IL declares. F# 9 applies it to IL only. All other type parameters still reject such type arguments with FS0412.

## Checking the definition

Let `'T` be a type parameter that allows ref structs. In the scope of `'T`, type checking and byref safety analysis treat `'T` as byref-like. They also treat every type that contains `'T` as byref-like.

Values of these types follow the [FS-1053](../FSharp-4.5/FS-1053-span.md) rules for byref-like values. Code can pass them to methods and functions, return them and bind them to locals. The escape analysis decides where they can flow. F# has no `scoped` modifier, so these parameters are never scoped.

Code MUST NOT do these operations on a value of such a type:

- Box it, or convert it to `obj`, `ValueType` or an interface type, with `box`, `:>`, `:?>`, `:?` or a type-directed conversion. The compiler calls interface members with constrained calls, as for any type parameter.
- Store it in a field, except an instance field of a byref-like struct (C# CS8345). Static fields, class `let` bindings, `member val`, captured primary constructor parameters and closures are all fields. Closures come from lambdas, partial applications, object expressions, and sequence and computation expressions.
- Use it as an array element, a tuple element or a type argument of a type parameter that does not allow ref structs (C# CS9244). Examples are `'T option`, `'T list`, function values `'T -> 'U`, and FSharp.Core operations such as `=`, `compare`, `hash`, `string` and `printf "%A"`.
- Call a member that the type argument possibly does not implement: members inherited from `Object` or `ValueType`, and default interface members (C# CS9246).

```fsharp
let pass<'T when 'T : allows ref struct> (x: 'T) = x        // returned like a Span<_> parameter
let bad1<'T when 'T : allows ref struct> (x: 'T) = box x    // error: boxing
let bad2<'T when 'T : allows ref struct> (x: 'T) = [ x ]    // error: 'T list

type Holder<'T when 'T : allows ref struct>(x: 'T) =
    member _.Value = x                                      // error: x is stored in a field
```

## Overrides, interface implementations and extensions

- Signature conformance compares this constraint as any other constraint. FS0341 already reports a mismatch.
- An override or interface implementation MUST copy the constraint from its slot. The rules above apply to its body. Today the compiler drops the constraint.
- A type extension MAY restate the constraint of the extended type parameter. Then `type IEnumerable<'T when 'T : allows ref struct> with ...` compiles on .NET 9. [#1390](https://github.com/fsharp/fslang-suggestions/issues/1390) makes the restatement optional.

## Compilation

- The compiler MUST emit `GenericParameterAttributes.AllowByRefLike` (`0x0020`) on the type parameter.
- The F# signature data MUST store the constraint in stream B. The pickler already supports it there.
- The compiler MUST report an error for a definition that declares the constraint if the target runtime does not define `System.Runtime.CompilerServices.RuntimeFeature.ByRefLikeGenerics`. .NET 9 and later define it. C# reports CS9240 in this case.

## Interactions

- **`inline` and SRTP**: the rules apply to the definition. Thus it is safe to inline it into a caller with a byref-like type argument. Constraint solving does not change.
- **Quotations**: the compiler rejects a quotation that uses a value of such a type, as for byref-like types.
- **Type providers**: no change.
- **C# interop**: C# sees `where T : allows ref struct` and can pass `ref struct` type arguments.
- **FSharp.Compiler.Service**: `FSharpGenericParameterConstraint.IsAllowsRefStructConstraint` reports the constraint, as for IL type parameters.

# Changes to the F# spec

- [§5 Types and Type Constraints](https://fsharp.github.io/fslang-spec/types-and-type-constraints/): add `typar ':' allows ref struct` to the `constraint` grammar.
- [§5.2 Type Constraints](https://fsharp.github.io/fslang-spec/types-and-type-constraints/#52-type-constraints): add *Allows Ref Struct Constraints* with the rules of [Syntax](#syntax) and [Instantiation](#instantiation).
- [§14.8 Dispatch Slot Checking](https://fsharp.github.io/fslang-spec/inference-procedures/#148-dispatch-slot-checking): copy the constraint from the slot.
- [§14.9 Byref Safety Analysis](https://fsharp.github.io/fslang-spec/inference-procedures/#149-byref-safety-analysis): replace "Byref types may not be used as generic arguments" with the rule of [Instantiation](#instantiation). Add the rules of [Checking the definition](#checking-the-definition).

# Drawbacks

- A definition that uses the constraint cannot use closures, sequence or computation expressions, structural equality, `%A` or other boxing operations on the type parameter.
- Code that uses the feature needs .NET 9 or later at run time.
- F# has no `scoped`, so it cannot express some C# signatures.

# Alternatives

- **An attribute on the type parameter** (`[<AllowByRefLike>] 'T`): differs from the form that F# prints and from C#.
- **`'T :? byref<struct>` or constraint unions** (dotnet/fsharp#16800 discussion): new syntax for one constraint.
- **Infer the constraint, or check `inline` functions after inlining** ([#688](https://github.com/fsharp/fslang-suggestions/issues/688), closed): signatures and errors would depend on the optimizer.
- **Relax only the constraint matching for extensions** ([#1390](https://github.com/fsharp/fslang-suggestions/issues/1390)): fixes extensions, but not generic code.
- **Do nothing**: such helpers stay in C#.

# Prior art

- **C# 13** `where T : allows ref struct` ([ref struct interfaces](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-13.0/ref-struct-interfaces.md)). The rules above follow C#. C# also has `scoped`, and it rejects ref structs as iterator element types (CS9267).
- **.NET 9** runtime support for byref-like generics, detected through `RuntimeFeature.ByRefLikeGenerics`.
- **F# 9** consumes the constraint on IL type parameters ([dotnet/fsharp#17597](https://github.com/dotnet/fsharp/pull/17597)).

# Compatibility

- This is not a breaking change. Today the syntax is an error (FS0571). Inference never adds the constraint, and existing definitions do not change.
- Older compilers and binaries that use the feature: F# before 9 ignores stream B. F# 9 and 10 apply the constraint only to IL type parameters. Both reject byref-like type arguments with FS0412, so neither accepts unsafe code.
- C# and other .NET languages read the IL flag.
- FSharp.Core does not change in this RFC.

# Interop

- C# code can pass `ref struct` type arguments to F# definitions that declare the constraint.
- F# overrides and implementations of C# members keep the constraint, so callers can use them with `ref struct` type arguments.

# Pragmatics

## Diagnostics

| Number | Condition |
|---|---|
| new | the constraint is combined with a constraint that requires a reference type |
| new | the target runtime does not define `RuntimeFeature.ByRefLikeGenerics` |
| new | code calls a member inherited from `Object` or `ValueType`, or a default interface member, on a value of such a type |
| FS3350 | the feature is off |
| FS0412, FS0406, FS0431, FS0437, FS3234, FS3235 | unchanged, now also for values of such type parameters |
| FS0341, FS0957 | unchanged, now satisfiable by declaring the constraint |

All are errors.

## Tooling

Tooltips, signature generation and FCS show `'T: allows ref struct`, as they do for IL type parameters today. Completion offers `allows ref struct` after `'T :`. Debugging, navigation and colorization do not change.

## Performance

Generic code over spans needs no copies for each type. Checking reuses the byref-like analysis, and the change does not affect existing code.

## Scaling

Not applicable.

## Culture-aware formatting/parsing

Not applicable.

# Unresolved questions

- Which FSharp.Core functions declare the constraint: `Unchecked.defaultof`, `typeof`, `typedefof`, `sizeof`, `raise`, `ignore`, `id` ([#952](https://github.com/fsharp/fslang-suggestions/issues/952)). Function values over byref-like types need it on `FSharpFunc<_, _>`, which also enables `|>` ([#688](https://github.com/fsharp/fslang-suggestions/issues/688)).
- A `scoped` modifier for parameters.
- Whether to allow a call of an `Object` member when the type argument overrides it.
- The feature name `AllowsRefStructConstraint` is a placeholder.
