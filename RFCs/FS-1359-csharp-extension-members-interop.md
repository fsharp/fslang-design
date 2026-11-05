# F# RFC FS-1359 - Interop with C# 14 extension members

The design suggestion [Interop with C# 14 extension members in both directions](https://github.com/fsharp/fslang-suggestions/issues/1479) has not been marked "approved in principle" yet.
This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1479)
- [ ] Approved in principle
- [ ] Implementation (not started)
- [x] [Discussion](https://github.com/fsharp/fslang-design/pull/864)

Related: [FS-1043 extension members in SRTP](FS-1043-extension-members-for-operators-and-srtp-constraints.md), [dotnet/fsharp#20035](https://github.com/dotnet/fsharp/issues/20035), [dotnet/fsharp#16362](https://github.com/dotnet/fsharp/issues/16362), [dotnet/fsharp#9405](https://github.com/dotnet/fsharp/issues/9405), [dotnet/roslyn#84629](https://github.com/dotnet/roslyn/issues/84629).

# Summary

C# 14 compiles an `extension` block to a documented metadata shape. This RFC makes F# read that shape and write it.

- F# resolves every member of a C# 14 extension block: methods, properties and operators, instance or static. Today F# resolves only the instance methods.
- An `[<Extension>]` member of an optional type extension, in an `[<Extension>]` module, compiles to that shape with a plain name. F# already uses this placement for C#-style extension members. C# then sees F# extension properties, static extension members and extension operators.

```fsharp
open System.Runtime.CompilerServices

[<Extension>]
module ThingExtensions =
    type Thing with
        [<Extension>]
        member x.Tripled = x.Value * 3                                      // C#: thing.Tripled
        [<Extension>]
        static member Create v = Thing v                                    // C#: Thing.Create(5)
        [<Extension>]
        static member (+) (a: Thing, b: Thing) = Thing(a.Value + b.Value)   // C#: a + b
```

The feature is `CSharpExtensionMembers` (preview). It gates emission. Consumption is not gated.

# Motivation

- .NET 11 `System.Private.CoreLib` declares the static properties `E`, `Pi`, `Tau`, `Epsilon`, `NaN`, `NegativeOne`, … of `Vector64/128/256/512<T>` in extension blocks. `Vector128<float32>.Pi` is FS0039 in F#. The only call today is `Vector128.get_Pi<float32>()`.
- ASP.NET Core 10 `Microsoft.AspNetCore.Identity` ships one block with 42 members. More libraries will follow.
- The unspeakable nested types of a block resolve in F#. A call to their members throws `NotSupportedException` at run time.
- F# compiles a member of an optional type extension to a static method with a dotted name: `Thing.Doubled`, `Thing.get_Tripled`, `Thing.Create.Static`, `Thing.op_Addition.Static`. `[<Extension>]` on the member and on the module does not change the name, so C# cannot name the method ([dotnet/fsharp#16362](https://github.com/dotnet/fsharp/issues/16362), [dotnet/fsharp#20035](https://github.com/dotnet/fsharp/issues/20035)). `[<CompiledName>]` repairs a method, but F# ignores it on extension properties ([dotnet/fsharp#9405](https://github.com/dotnet/fsharp/issues/9405)). Users write C# shims.
- The C# team will not add an attribute for other languages ([dotnet/roslyn#84629](https://github.com/dotnet/roslyn/issues/84629)). The metadata shape is the contract.

# Detailed design

## The metadata shape

Terms are from the [C# specification](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-14.0/extensions.md#metadata-for-declarations). Roslyn checks this structure only. It does not check the type names.

- **Container**: a top-level, non-generic, abstract sealed class with `[Extension]`.
- **Grouping type**: nested in the container. Public, sealed, not abstract, `specialname`, `[Extension]`, base `Object`, no interfaces. One for each receiver type and set of CLR constraints. Its type parameters are the type parameters of the block.
- **Marker type**: nested in the grouping type. Public, abstract sealed, `specialname`, base `Object`, no interfaces. It redeclares the type parameters of the grouping type with the same constraints. It holds exactly one method `static void '<Extension>$'(receiver)`: `specialname`, not generic, one parameter. The parameter gives the receiver type, its ref kind and its name.
- **Skeleton members**: methods, properties and their accessors in the grouping type. Each carries `[ExtensionMarker("<marker type name>")]`. Instance members have `this`. Their bodies throw. Operators are `public specialname` with `op_` names.
- **Implementation methods**: static methods in the container with the name and accessibility of the skeleton. Type parameters: those of the block, then those of the member. Parameters: the receiver first for an instance member, then the parameters of the member. Return type and constraints match the skeleton. An instance method also carries `[Extension]`.
- `System.Runtime.CompilerServices.ExtensionMarkerAttribute(string)` is in `System.Runtime` since .NET 10. Roslyn matches it by name and embeds a copy when the target lacks it.

## Consumption

Name resolution treats the container as a classic extension class, with these additions:

- For each grouping type it reads each marker type. The marker method gives the receiver type `R` over the type parameters of the marker type.
- For each skeleton member whose attribute names the marker type, it finds the implementation method with the rules above. A skeleton without an implementation is not visible.
- The skeleton is the only extension candidate for its member. The compiler MUST NOT register the implementation method of an instance member as a classic extension method too. Roslyn shadows it the same way. An implementation method with `[Extension]` and no visible skeleton stays a classic extension method. A direct static call to an implementation method stays valid.
- An instance member becomes an extension member of `R`. A static member becomes a static extension member of `R`, as an F# static extension member. The type arguments of `R` at the use site instantiate the type parameters of the block. A property becomes an extension property. Its accessors are the implementation methods `get_P` and `set_P`.
- Member lookup, overload resolution, accessibility, nullness and attributes use the skeleton signature.
- Code generation calls the implementation method. For an instance member, the receiver is the first argument.
- Completion and `open type` hide grouping and marker types. Name resolution still accepts their names, as C# does.
- Priority: an intrinsic member wins over an extension member. F# and C# extension members in scope follow the existing `open` order.

## Emission

A *marked member* is an `[<Extension>]` member of an optional type extension, in a module with `[<Extension>]`. The code MUST write the attribute on the module. F# adds it to a module for `let` bindings, but not for type-extension members. Intrinsic extensions do not change.

For marked members the compiler emits the shape:

- The module is the container.
- Marked members group by receiver type and constraints into grouping types. The grouping and marker type names are content-based hashes of the signature. They are stable between builds and unspeakable.
- Each marked member gets a skeleton and an implementation method. The skeleton has the F# name, for example `Tripled`. The implementation has a plain name: `Doubled`, `get_Tripled`, `set_Tripled`, `Create`, `op_Addition`. An explicit `[<CompiledName>]` renames the skeleton and the implementation together, and for a property both accessors; C# matches them by name. The F# signature data keeps the source name.
- The implementation of an instance method also carries `[Extension]`, so F# and C# 13 see a classic extension method. No other implementation carries it.
- The compiler embeds `ExtensionMarkerAttribute` when the target framework lacks it, as for `IsReadOnlyAttribute`.

With the feature on, the skeleton and the implementation replace the dotted method. Unmarked members compile as today.

Two marked members of one module can compile to the same implementation signature: name, total generic arity (block plus member) and parameter types. `Create(int)` on two receiver types is an example; `Create()` and `Create<T>()` are not. C# reports the same conflict. The compiler MUST report an error.

A marked member in a nested module gets a warning. A nested module compiles to a nested class, and C# reads extension members only from a top-level container.

## Operators

- Consumption: an extension operator from a block is an SRTP candidate for its receiver type `R` when FS-1043 is on. Older language versions ignore it. F# ignores `op_Checked*`, instance compound-assignment operators and instance `op_IncrementAssignment`/`op_DecrementAssignment`; it has no counterpart for them.
- Emission: a marked operator MUST be public, and one operand MUST have the receiver type (C# CS9317, CS9319). For a shift operator it MUST be the first operand (CS9320). Otherwise C# cannot find the operator, and the compiler reports a warning.
- Only CLS operator names (`op_Addition`, `op_UnaryNegation`, …) are operators in C#. A custom F# operator such as `(+.)` compiles to `op_PlusDot`, and C# sees a static extension method.

# Changes to the F# spec

- [§8.12 Type Extensions](https://fsharp.github.io/fslang-spec/type-definitions/#812-type-extensions): add the compiled form of [Emission](#emission).
- [§8.12.1 Imported CLI C# Extensions Members](https://fsharp.github.io/fslang-spec/type-definitions/#8121-imported-cli-c-extensions-members): replace "C# limits extension members to instance methods" with the members of [Consumption](#consumption).
- [§14.1 Name Resolution](https://fsharp.github.io/fslang-spec/inference-procedures/#141-name-resolution): add the members of [Consumption](#consumption) to the extension members of a type, with the receiver from the marker method.

# Drawbacks

- A marked member emits two methods, a skeleton and an implementation. Assemblies grow.
- F# assemblies get unspeakable nested types.
- A marked member changes its compiled name. A caller compiled against the old binary must recompile.
- F# depends on the structural checks of Roslyn. A change there needs a change in F#.

# Alternatives

- **`[<Extension>]` on the module only**: opts in every type-extension member. The author cannot exclude one member.
- **A new attribute** such as `[<CSharpExtension>]`: a second attribute for one concept.
- **Plain names only**: fixes [dotnet/fsharp#16362](https://github.com/dotnet/fsharp/issues/16362). C# 13 gets the instance methods. Properties, static members and operators stay invisible.
- **Emit the shape for every optional type extension**: changes compiled names and breaks binary compatibility.
- **Consume only**: F# reads the BCL members; F# libraries stay invisible to C#.
- **An attribute contract from C#**: rejected in [dotnet/roslyn#84629](https://github.com/dotnet/roslyn/issues/84629).

# Prior art

- C# 14 extension members: [proposal](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-14.0/extensions.md). Roslyn recognises the shape in `PENamedTypeSymbol` and accepts hand-written IL with plain type names in its tests.
- Classic extension methods: the `[Extension]` contract since .NET 3.5. F# consumes and emits it.
- VB.NET consumes the instance methods as classic extension methods only.

# Compatibility

- The attribute syntax is valid today. Emission and its diagnostics are behind the feature, so a project changes only when it turns the feature on. Then two marked members with the same implementation signature are a new error; today their dotted names differ.
- Consumption is not gated. A C# 14 extension block in a referenced assembly adds candidates to member lookup and SRTP. Existing code can resolve to a new candidate, as it does when a referenced assembly adds a classic extension method. Gating is an open question.
- The compiled name of a marked member changes when the library recompiles with the feature on. C# could not call the old name. An F# caller compiled against the old binary must recompile.
- Older F# compilers reading source: they compile as today, with dotted names.
- Older F# compilers reading new binaries: they use the F# signature data and call the implementation methods by compiled name. They see grouping types as plain nested types, as the current compiler sees C# blocks.
- FSharp.Core does not change.

# Interop

- C# 14 sees the members as extension members. C# 13 and VB.NET see the instance methods as classic extension methods and the rest as static methods of the module.
- F# sees C# 14 extension members with the same syntax as F# extension members.

# Pragmatics

## Diagnostics

| Number | Condition | Severity |
|---|---|---|
| new | a marked member in a nested module | warning |
| new | a marked member is `op_Implicit` or `op_Explicit`; C# ignores it | warning |
| new | a marked operator without an operand of the receiver type, or not public | warning |
| new | two marked members of one module compile to the same name, generic arity and parameter types | error |
| FS1215 | a marked operator, language version without FS-1043 | warning, unchanged |

## Tooling

Completion, tooltips and Go to Definition show C# 14 extension members as extension members of the receiver type. Completion hides grouping and marker types. FCS reports them through `IsExtensionMember` with the receiver as the apparent enclosing entity. Debugging and colorization do not change.

## Performance

The compiler reads a container once per assembly and caches the result, as for classic extension classes. Emission adds one pass over marked members.

## Scaling

Not applicable.

## Culture-aware formatting/parsing

Not applicable.

# Unresolved questions

- Whether the compiler adds `[<Extension>]` to the module for a marked type-extension member, as it does for `let` bindings.
- Whether to gate consumption. New candidates can change overload resolution in existing code.
- The hash scheme for the generated names. Roslyn uses XxHash128 of a raw signature string. The same scheme gives the same XML doc IDs from C# and F#.
- Indexers: extension indexers are preview in C#.
- Receivers with `byref`/`inref`, which C# encodes as `ref`/`ref readonly` on the marker parameter.
- The feature name `CSharpExtensionMembers` is a placeholder.
