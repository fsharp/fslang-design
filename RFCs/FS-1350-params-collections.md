# F# RFC FS-1350 - Params collections (`ParamCollectionAttribute`)

The design suggestion [Native interop for C#13 params enhancements](https://github.com/fsharp/fslang-suggestions/issues/1377) is open and has not yet been marked "approved in principle". The F# team's comments on it ask for the feature to be considered together with type-directed collection construction, with `OverloadResolutionPriorityAttribute` (since specified as FS-1338), and with stack allocation of span arguments. This RFC is written ahead of a decision so that a concrete design answering those three points can be reviewed.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1377)
- [ ] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

**Related:** [FS-1338 OverloadResolutionPriorityAttribute support](FS-1338-OverloadResolutionPriorityAttribute.md) and [FS-1340 "Most concrete" tiebreaker](FS-1340-most-concrete-tiebreaker.md) (both preview) define the overload-resolution steps this RFC slots into. [FS-1093 Additional type directed conversions](../FSharp-6.0/FS-1093-additional-conversions.md) governs how each element is converted. [FS-1145](../FSharp.Core-9.0/FS-1145-csharp-collection-expression-support-for-lists-and-sets.md) put `CollectionBuilderAttribute` on FSharp.Core's `list` and `Set`. [FS-1053](../FSharp-4.5/FS-1053-span.md) defines byref-like types. The open RFC [FS-1342 Contextual target typing for numeric and collection literals](https://github.com/fsharp/fslang-design/pull/838) proposes building collections from `[ ... ]` at method-argument position; this RFC is self-contained and does not depend on it, but the two use the same shape vocabulary, and where both apply the collection is constructed by the same rules.

# Summary

F# recognises `System.Runtime.CompilerServices.ParamCollectionAttribute`, the metadata C# 13 emits for `params` on non-array parameter types, and lets a call supply the elements of such a parameter as separate trailing arguments, exactly as it does for `[<ParamArray>] T[]` today. The eligible parameter types are `ReadOnlySpan<T>`, `Span<T>`, the five collection interfaces (`IEnumerable<T>`, `IReadOnlyCollection<T>`, `IReadOnlyList<T>`, `ICollection<T>`, `IList<T>`), types with a `CollectionBuilder` create method, and collection-initializer types. For the two span shapes, which are the only shapes the .NET base class library uses, the collection is built in stack storage and no array is allocated. Overload resolution gains a last-resort "better params collection" preference (`ReadOnlySpan<T>` over `Span<T>` over other shapes), so `String.Join(", ", a, b)` moves to the span overload as it does for C# 13 callers. F# members may declare such parameters with the attribute. The feature is gated by a new language feature, `ParamsCollections`, and needs no FSharp.Core change.

```fsharp
open System
open System.IO
open System.Collections.Generic
open System.Collections.Immutable

String.Join(", ", "a", "b", "c")               // Join(string, params ReadOnlySpan<string>): stack buffer, no array
Path.Combine(root, "src", "bin", "Debug", "net9.0") // Combine(params ReadOnlySpan<string>): five parts, past the fixed-arity overloads
ImmutableArray.Create(1, 2, 3, 4, 5)           // Create<T>(params ReadOnlySpan<T>): one copy, no array
names.AddRange("x", "y")                       // CollectionExtensions.AddRange(List<T>, params ReadOnlySpan<T>): today FS0503
Log.Write("{0} + {1} = {2}", 1, 2, 3)          // F#-declared [<ParamCollection>] args: ReadOnlySpan<obj>

String.Join(", ", [| "a"; "b" |])              // normal form, Join(string, params string[]): unchanged
String.Join(", ", [ "a"; "b" ])                // normal form, Join(string, IEnumerable<string>): unchanged
String.Join(", ", ros)                         // ros: ReadOnlySpan<string>, normal form: unchanged

type Log =
    static member Write(format: string, [<ParamCollection>] args: ReadOnlySpan<obj>) =
        Console.WriteLine(format, args)        // calls the C# 13 params overload in normal form
```

# Motivation

F# recognises exactly one params convention, `System.ParamArrayAttribute` on a parameter of array type. C# 13 generalised `params` to spans, interfaces, collection builders and collection-initializer types, and marks every non-array `params` parameter with a new attribute, `System.Runtime.CompilerServices.ParamCollectionAttribute`, precisely so that consumers which only know `ParamArrayAttribute` keep working in normal form. F# is such a consumer today. The result is that every `params ReadOnlySpan<T>` member in .NET 9 and later is callable from F# only with an explicit span:

```fsharp
// Today
String.Join(", ", ReadOnlySpan([| a; b; c |]))   // allocates the array anyway
String.Join(", ", [| a; b; c |])                 // params string[] overload: allocates
CS13ParamArray.WriteNames("Petr", "Jana")        // span-only member: FS0503, "taking 2 arguments is not
                                                 // accessible ... All accessible versions take 1 arguments"
```

The FS0503 case is the current baseline in the compiler's own tests (`tests/FSharp.Compiler.ComponentTests/Interop/ParamArray.fs`): an arity error, with no hint that a params parameter is involved.

The surface this affects is not small. Every `[ParamCollection]` member in the .NET 11 base class library takes a `ReadOnlySpan<T>`, and every one of them sits beside an older `params T[]` twin: `String.Concat`, `String.Format`, `String.Join`, `String.Split`, `String.Trim`/`TrimStart`/`TrimEnd`, `Path.Combine`, `Path.Join`, `StringBuilder.AppendJoin`, `StringBuilder.AppendFormat`, `Console.Write`/`WriteLine`, `CollectionExtensions.AddRange`/`InsertRange`, `ImmutableArray.Create`, `ImmutableList.Create`, `ImmutableHashSet.Create`, `ImmutableSortedSet.Create`, `ImmutableQueue.Create`, `ImmutableStack.Create`, `FrozenDictionary.Create`, `FrozenSet.Create`, `ImmutableArray<T>.AddRange`/`InsertRange`, `ImmutableArray<T>.Builder.AddRange`, `Counter<T>.Add`, `Gauge<T>.Record`, `Histogram<T>.Record`, `UpDownCounter<T>.Add`, `JsonTypeInfoResolver.Combine`. No BCL member uses an interface, `List<T>` or builder shape. The span overloads exist so that a fixed number of arguments can be passed without a heap allocation; a C# 13 caller gets that on recompilation, an F# caller gets an array.

The suggestion thread raised three design questions, and this RFC is organised around them:

- **Stack allocation is the point** (T-Gro). Wrapping a freshly allocated array in a span "would not be any better" than the array overload. This RFC therefore makes stack storage a normative requirement for span-shaped params collections, not a lowering freedom.
- **It should go together with type-directed collection construction** (vzarytovskii). The rules for building a collection of a given shape from a list of element expressions are written here once, in the same shape vocabulary as the open FS-1342 RFC, so that the two features share one construction model whichever lands first.
- **`OverloadResolutionPriorityAttribute` is the other half** (brianrourkeboll). FS-1338 has since specified it; this RFC states how the new preference orders against that pre-filter and against FS-1340.

# Detailed design

## Recognising a params parameter

A parameter is a *params parameter* if it is the last parameter of a member with a single curried parameter group and either

- it carries `System.ParamArrayAttribute` and has a single-dimensional array type (today's rule), or
- it carries `System.Runtime.CompilerServices.ParamCollectionAttribute` and has a *params collection type* as defined below.

Both attributes are recognised by full type name, on imported IL metadata and on F#-declared members alike, in the same way as every other well-known attribute the compiler reads. A `ParamCollectionAttribute` type defined in the consuming assembly, as a polyfill for a target framework older than .NET 9, is therefore honoured exactly like the one in `System.Runtime`. This matches C#, which also matches by name.

A `ParamCollectionAttribute` on a parameter whose type is not a params collection type is ignored: the parameter is an ordinary parameter and only the normal form applies. No diagnostic is raised for imported metadata; for F#-declared members see [Declaring params collection parameters in F#](#declaring-params-collection-parameters-in-f).

## Params collection types and element types

| Shape | Parameter type | Element type `E` | Note |
|---|---|---|---|
| array | `T[]` (with `ParamArrayAttribute` only) | `T` | existing |
| read-only span | `System.ReadOnlySpan<T>` | `T` | the shape the BCL uses |
| span | `System.Span<T>` | `T` | |
| read-only interface | `IEnumerable<T>`, `IReadOnlyCollection<T>`, `IReadOnlyList<T>` | `T` | |
| mutable interface | `ICollection<T>`, `IList<T>` | `T` | constructed as `List<T>` |
| builder | a type carrying `[<CollectionBuilder(typeof<B>, "Create")>]` | the `E` of `B.Create(ReadOnlySpan<E>)` | validated as below |
| initializer | a class or struct implementing `System.Collections.IEnumerable` with an accessible parameterless constructor and an accessible instance method `Add` | the type's iteration type | `Add` is resolved per element by ordinary method application |

A builder shape is valid only when the attribute's builder type and method satisfy the .NET collection-expression contract, in the same words FS-1342 uses: the builder type is a non-generic class or struct at least as accessible as the member; the create method is declared directly on it, is static and accessible, has exactly one by-value parameter of type `ReadOnlySpan<E>`, has the same number of type parameters as the collection type (instantiated with the collection's type arguments), returns a type convertible to the collection type by identity or reference conversion, and `E` is the collection type's iteration type. If any condition fails, the type is not a params collection type.

The iteration type of an initializer shape is determined as for `for ... in`: from an accessible `GetEnumerator` whose `Current` has a type, else from `IEnumerable<T>`, else `obj`.

All seven shapes are in scope, not only the two span shapes. The metadata already exists in C# 13 libraries, each non-span shape lowers to code the compiler already knows how to build (an array, a `List<T>`, or ordinary constructor and method calls), and leaving a shape out means that calls against it keep failing with today's misleading FS0503.

## Applicability: normal and expanded form

F# already considers a method with a params parameter twice during overload resolution, once in *normal form* (the caller passes the collection) and once in *expanded form* (the caller passes the elements). The expanded candidate is generated when the caller supplies at least `n - 1` unnamed positional arguments for `n` unnamed parameters. Today the expanded candidate is generated only when the params parameter is an array; under this RFC it is generated for every params collection type.

In the expanded form each trailing argument is checked against the element type `E` with the rules that apply to any other method argument: subsumption, then the type-directed conversions of FS-1093 (with their warnings). Boxing `int` to `obj` is subsumption, not a type-directed conversion, so `Console.WriteLine("{0} {1} {2} {3}", 1, 2, 3, 4)` boxes each element silently, as it does today for `params obj[]`.

The normal form is unchanged and continues to accept whatever it accepts today: an array for the array shape; a span value for a span shape; an F# list, array or `seq` for an interface shape by subsumption; and, by the existing `op_Implicit` type-directed conversion of FS-1093, a `T[]` for a `ReadOnlySpan<T>` or `Span<T>` parameter.

Other existing rules carry over unchanged to every shape:

- Naming the params parameter (`M(names = xs)`) selects the normal form only.
- An indexed property setter keeps its special shape: the params parameter is the last *index* parameter and the assigned value follows it.
- A trailing params parameter is never treated as an optional or `out` parameter.
- Members with curried parameter groups cannot have params parameters (FS0440).

One divergence from C# follows from the order of F#'s existing preferences and is documented rather than fixed here. F# prefers a candidate that uses no type-directed conversion before it prefers the normal form over the expanded form. For a span-only member `M([<ParamCollection>] args: ReadOnlySpan<obj>)` called as `M(xs)` with `xs: obj[]`, the normal form needs the `obj[] -> ReadOnlySpan<obj>` conversion and the expanded form needs none (`xs` boxes to a single `obj` element), so F# selects the expanded form and passes a one-element span containing the array, where C# passes the array's elements. The case requires an `obj` element type and no array twin, which no BCL member has; `M(ReadOnlySpan xs)` selects the normal form explicitly. See [Unresolved questions](#unresolved-questions).

## Overload resolution

The existing params preference generalises without change of meaning: prefer candidates that do not use params collection conversion; between two candidates that both do, prefer the one whose element type is more specific (spec §14.4 step 7 rule 2, `noParamArrayRule` and `preciseParamArrayRule` in the compiler).

One new preference is added, as the last rule consulted during betterness:

```text
10) If two candidates both use params collection conversion, their element types are equivalent, and the
    same actual arguments form the collection elements in both, then with params collection types pcty1
    and pcty2, prefer the first candidate if
    a) pcty1 is System.ReadOnlySpan<_> and pcty2 is System.Span<_>; or
    b) pcty1 is System.ReadOnlySpan<_> or System.Span<_> and pcty2 is neither; or
    c) neither is a span type and pcty1 coerces to pcty2; or
    d) neither is a span type, pcty1 is a single-dimensional array type and pcty2 is not.
    Otherwise there is no preference.
```

Cases (a) to (c) are C# 13's three "better params collection" bullets stated in F# terms. Case (d) is an F#-only addition: C# leaves `params string[]` beside `params List<string>` ambiguous, whereas F# resolves that pair to the array today because the `List<string>` candidate has no expanded form. Case (d) keeps every such program compiling with the same resolution, and can only turn a C# ambiguity into an F# success, never choose a different successful answer from C#.

**Placement.** The rule runs after every other betterness preference, after FS-1340's most-concrete rule and after the FS-1338 priority pre-filter, and only under the `ParamsCollections` feature. C# places its equivalent at the end of its tie-break list for the same reason: the earlier preferences (normal form over expanded, more specific element type, intrinsic over extension, non-generic over generic) must decide first, so that an intrinsic `params string[]` still beats an extension `params ReadOnlySpan<string>`. Because it fires only when every earlier rule has returned no preference, it can only resolve what would otherwise be FS0041. FS-1340's statement that its rule "runs last" becomes "runs last among the rules that compare parameter types"; the shape rule runs after it.

**Existing calls move to the span overload.** When a method group carries both `params T[]` and `params ReadOnlySpan<T>` for the same element type, a call in expanded form resolves to the span overload under this feature, exactly as it does when a C# project is recompiled with C# 13. This is a deliberate choice, and the conservative alternative is discussed under [Alternatives](#alternatives). The two overloads are documented by the BCL as equivalent, the span one existing only to remove the allocation; the change applies only under the new language feature and only to method groups that carry both shapes; and passing the array is one keystroke away (`String.Join(", ", [| a; b |])`).

C#'s further tie-break "prefer the candidate whose params collection has fewer elements" has no F# analogue today and is not adopted here.

### Resolution table

With the feature on and .NET 9 or later:

| Call | Resolves to | Note |
|---|---|---|
| `String.Join(",", "a", "b")` | `Join(string, params ReadOnlySpan<string>)`, expanded | New. The `obj`-element overloads lose on element type (rule 2); the `string[]` twin loses by rule 10(b). No array is allocated. |
| `String.Join(",", [\| "a"; "b" \|])` | `Join(string, params string[])`, normal form | Unchanged. Normal form beats expanded (rule 2); `string[]` beats `IEnumerable<string>` and `obj[]` (rule 5). |
| `String.Join(",", [ "a"; "b" ])` | `Join(string, IEnumerable<string>)`, normal form | Unchanged. The list subsumes to the interface; the expanded `obj` candidates (one boxed element) lose by rule 2. |
| `String.Join(",", ros)` with `ros: ReadOnlySpan<string>` | `Join(string, params ReadOnlySpan<string>)`, normal form | Unchanged; works today. |
| `ImmutableArray.Create(1, 2, 3)` | `Create<int>(int, int, int)` | Unchanged. The fixed-arity overloads for one to four elements win by rule 2. |
| `ImmutableArray.Create(1, 2, 3, 4, 5)` | `Create<int>(params ReadOnlySpan<int>)`, expanded | New; was `Create<int>(params int[])`. Both candidates are generic and FS-1340 sees different constructors, so rule 10(b) decides. |
| `Console.WriteLine("{0} {1}", 1, "x")` | `WriteLine(string, obj, obj)` | Unchanged; the fixed-arity overload wins by rule 2. |
| `Console.WriteLine("{0}{1}{2}{3}", 1, 2, 3, 4)` | `WriteLine(string, params ReadOnlySpan<obj>)`, expanded | New; was `params obj[]`. Elements are boxed by subsumption. |
| `WriteNames()` against `params ReadOnlySpan<string>` | that member, with `ReadOnlySpan<string>.Empty` | New; today FS0503 when no array twin exists. With an array twin, rule 10(b) still picks the span. |
| `WriteNames("a", "b")` against `params List<string>` and `params IEnumerable<string>` | the `List<string>` member | New; today FS0503. Rule 10(c): `List<string>` coerces to `IEnumerable<string>`. |
| `WriteNames("a", "b")` against `params string[]`, `params List<string>` and `params IEnumerable<string>` | the `string[]` member | Unchanged. Rule 10(c) eliminates the interface; rule 10(d) prefers the array over the list. C# reports CS0121 here. |
| `WriteNames("a", "b")` against `params List<string>` and `params HashSet<string>` | FS0041 | Same element type, neither a span, neither coerces to the other, neither an array. C# reports CS0121. Pass `List [ "a"; "b" ]`. |

## Constructing the collection

The collection is a compiler-generated temporary built at the position of the params parameter: after the lexically preceding argument has been evaluated and before any following one. The element arguments are evaluated left to right and each is converted to `E` individually, following the existing elaboration of `ParamArray` calls. With `N` the number of trailing arguments:

**Array.** `[| e1; ...; eN |]`, unchanged. `N = 0` gives an empty array, as today.

**`ReadOnlySpan<T>` and `Span<T>`.**

> When `N > 0` and the target framework defines `System.Runtime.CompilerServices.InlineArrayAttribute` (.NET 8 and later), the compiler must not allocate a GC-heap array for the collection.
>
> The reference elaboration is: a compiler-synthesised generic struct `InlineArrayN<T>` in `<PrivateImplementationDetails$>`, marked `[InlineArray(N)]` and holding one field of type `T`; a mutable local of that struct; `MemoryMarshal.CreateSpan(&local.field, N)`; one store per element through the span's indexer; and, for a `ReadOnlySpan<T>` parameter, the implicit `Span<T>` to `ReadOnlySpan<T>` conversion. One struct is emitted per distinct `N` per assembly, shared across element types.
>
> When every element is a constant of a primitive type and the parameter is `ReadOnlySpan<T>`, the compiler may instead emit the data as an RVA field and call `RuntimeHelpers.CreateSpan<T>` (.NET 7 and later), which allocates nothing and copies nothing.
>
> When `InlineArrayAttribute` is not available (`netstandard2.x`, .NET 5 to 7, .NET Framework with `System.Memory`), the compiler constructs `[| e1; ...; eN |]` and calls the span constructor. The observable behaviour is identical.
>
> When `N = 0` the argument is the default span (`ReadOnlySpan<T>.Empty`).

The storage requirement is a performance guarantee, not a semantic one: any lowering with the same observable behaviour that does not allocate on the GC heap is permitted. The temporary lives on the caller's stack for the duration of the call only; see [Interactions](#interactions) for why nothing can escape.

**Read-only interfaces.** `[| e1; ...; eN |]` coerced to the interface. The runtime type of the object is unspecified, so an implementation may later substitute a non-array wrapper if FS-1342 introduces one. `N = 0` gives an empty array.

**Mutable interfaces.** `List<T>(N)` followed by `N` calls to `Add`. `N = 0` gives `List<T>()`.

**Builder.** A `ReadOnlySpan<E>` built by the span rule above, passed to `B.Create`. `N = 0` passes the default span.

**Initializer.** `new C()`, or `new C(N)` when `C` has an accessible constructor whose single parameter is an `int` named `capacity`; then `Add(ei)` for each element, each call resolved as an ordinary method application so that `Add` overloads and argument conversions apply. `N = 0` gives `new C()`.

Worked example, `String.Join(", ", a, b)` on .NET 9:

```fsharp
// Today (params string[])
String.Join(", ", [| a; b |])

// Under this RFC (params ReadOnlySpan<string>), shown as F# for illustration
let mutable buffer = Unchecked.defaultof<InlineArray2<string>>
let span = MemoryMarshal.CreateSpan(&buffer.Element0, 2)
span[0] <- a
span[1] <- b
String.Join(", ", ReadOnlySpan<string>.op_Implicit span)
```

## Declaring params collection parameters in F#

F# has no `params` keyword; authors write `[<ParamArray>]` on the parameter and the compiler does no further validation. This RFC adds the second attribute with the same surface and a small amount of checking:

1. `[<ParamCollection>]` is valid only on the last parameter of a member with a single curried parameter group, and only when the parameter's type is a params collection type other than an array. Any other placement or type is an error (see [Diagnostics](#diagnostics)). Arrays keep using `[<ParamArray>]`.
2. The parameter may not be optional, may not have a default value, may not be `byref`, `inref` or `outref`, and may not also carry `[<ParamArray>]`.
3. The attribute type must be available to the compilation: from the target framework (.NET 9 and later) or as a user-defined type with that full name. The compiler does not embed the attribute into the output assembly. This matches C#, whose compiler does not synthesise it either, and matches `[<ParamArray>]`. On an older target framework without a polyfill the usual FS0039 is reported.
4. The attribute is emitted as an ordinary custom attribute on the parameter. Signature files must carry it where the implementation does.
5. A params parameter of span type is *non-escaping*: callers may pass stack storage, and C# marks such parameters as implicitly `scoped`. The member may use the span freely during the call but must not return it or store it. How far the compiler enforces this is an [unresolved question](#unresolved-questions); the recommendation is an error on a direct return of the parameter, with `[<UnscopedRef>]` as the opt-out, as in C#.
6. Nothing else changes for the member body: the parameter is an ordinary value of its declared type.

Such a member is callable in expanded form from F# under this feature, and from C# 13, which reads the attribute by name:

```fsharp
type Log =
    static member Write(format: string, [<ParamCollection>] args: ReadOnlySpan<obj>) =
        Console.WriteLine(format, args)

Log.Write("{0} + {1} = {2}", 1, 2, 3)   // F#: expanded form, stack buffer
```

```csharp
Log.Write("{0} + {1} = {2}", 1, 2, 3);  // C# 13: expanded form
```

## Interactions

- **`OverloadResolutionPriorityAttribute` (FS-1338).** The priority pre-filter runs before betterness, so a library can force the choice between two shapes with priorities. The BCL does not, and relies on rule 10.
- **Most-concrete tiebreak (FS-1340).** Runs before rule 10. When two candidates differ in the concreteness of their parameter types it decides first, matching C#'s ordering.
- **Type-directed conversions (FS-1093).** Apply per element as for arrays today. A candidate that needs a type-directed conversion for an element ranks below one that does not, before any params preference is consulted.
- **Nullness.** The element type's nullness follows the collection's type argument, as for arrays.
- **Computation expressions.** Builder methods with `ParamArray` parameters are excluded from the arity inference of custom operations; params collection parameters are excluded identically.
- **SRTP constraints.** FS3532 already rejects `ParamArray` parameters in trait declarations and is extended to `ParamCollection`.
- **Type providers.** Provided methods can declare `ParamArray` parameters only; params collections are out of scope for provided members.
- **Quotations and `ReflectedDefinition`.** The expanded form is allowed inside a quotation for the array and interface shapes, which are expressible with `NewArray`, coercions and method calls. For the span, builder and initializer shapes it is an error inside a quotation: C# likewise excludes non-array expanded forms from expression trees, and a span cannot be materialised as a quotation value. Elements are still checked for `ReflectedDefinition` propagation as today.
- **Byref safety at the call site.** The temporary and the span exist only for the duration of the call; the caller cannot name them, so no escape analysis is needed on the caller's side. The callee's side is rule 5 of [Declaring params collection parameters in F#](#declaring-params-collection-parameters-in-f).
- **Evaluation order.** Element arguments are evaluated in source order, and the collection is created after the last of them, as for `ParamArray` today.

## Language feature

The feature is `LanguageFeature.ParamsCollections`, initially in `preview`. When it is off, behaviour is identical to today: expanded-form calls against a params collection fail with FS0503 or FS0001, and an F#-declared `[<ParamCollection>]` is passed through to metadata without validation, as `[<ParamArray>]` is. FS-1338 made the same choice for its attribute: raising FS3350 on a declaration that merely carries an attribute would break code that already declares it for C# consumers under a pinned language version. Whether FS0503 should mention the feature when it is off is an unresolved question.

# Changes to the F# spec

All changes are gated by the `ParamsCollections` language feature and belong to [§14.4 Method Application Resolution](https://fsharp.github.io/fslang-spec/inference-procedures/#144-method-application-resolution).

**Applicability.** Where the specification defines the expanded form for a candidate whose last formal parameter "has the `ParamArray` attribute and an array type", read instead:

```text
A candidate has a params parameter when its last formal parameter either has the System.ParamArrayAttribute
attribute and a single-dimensional array type, or has the System.Runtime.CompilerServices.ParamCollectionAttribute
attribute and a params collection type. A params collection type is one of: System.Span<E>; System.ReadOnlySpan<E>;
IEnumerable<E>, IReadOnlyCollection<E>, IReadOnlyList<E>, ICollection<E>, IList<E>; a type with a valid
CollectionBuilder create method taking ReadOnlySpan<E>; or a class or struct type implementing
System.Collections.IEnumerable with an accessible parameterless constructor and an accessible instance Add method,
whose iteration type is E. In each case E is the element type. Such a candidate may be applied in expanded form:
the trailing unnamed actual arguments are the params arguments, and each must have a type that coerces or
converts to E.
```

**Step 7, rule 2.**

```diff
- 2) Prefer candidates that do not use ParamArray conversion. If two candidates both use ParamArray conversion
-    with types pty1 and pty2, and pty1 feasibly subsumes pty2, then prefer the second candidate over the first.
+ 2) Prefer candidates that do not use params collection conversion. If two candidates both use params
+    collection conversion with element types ety1 and ety2, and ety1 feasibly subsumes ety2, then prefer the
+    second candidate over the first.
```

**Step 7, new rule 10** (after FS-1340's rule 9): the text of rule 10 in [Overload resolution](#overload-resolution). At runtime it runs after every other betterness preference, after rule 9, and after the `OverloadResolutionPriorityAttribute` pre-filter.

**Elaboration.** Where the specification says the params arguments "are converted to an array", read: they are used to construct a value of the params collection type as described in [Constructing the collection](#constructing-the-collection).

**Members.** A parameter of a member may carry `System.Runtime.CompilerServices.ParamCollectionAttribute` subject to rules 1 and 2 of [Declaring params collection parameters in F#](#declaring-params-collection-parameters-in-f).

# Drawbacks

- **Existing calls change overload under preview.** A call that resolves to `params T[]` today resolves to `params ReadOnlySpan<T>` when both exist. The result is the same by API contract; the difference is visible in allocation profiles, in IL, and to tooling that inspects the selected member.
- **New code-generation surface.** The compiler has never constructed a span, synthesised an inline-array struct or emitted `RuntimeHelpers.CreateSpan`; FS-1053 deliberately gave `stackalloc` "no special treatment". This RFC adds that machinery, confined to one elaboration helper and one type-synthesis routine.
- **Seven shapes behind one attribute.** The builder and initializer shapes require member lookup during applicability, not just a type test.
- **C#'s ambiguities become reachable.** `params List<T>` beside `params HashSet<T>` is FS0041 for F# as it is CS0121 for C#.
- **A documented divergence from C#** for `obj`-element span-only members called with an array (see [Applicability](#applicability-normal-and-expanded-form)).
- **The declaring side depends on the target framework** unless the author polyfills the attribute, as for C#.

# Alternatives

- **Support only the span shapes.** Simpler code generation. Rejected: C# 13 libraries already emit the other shapes, and F# would keep reporting FS0503 for them.
- **Keep the array overload as the winner when both shapes apply** (the "Phase A" policy of the FS-1342 draft: candidates applicable today always win). Stable resolutions, no change to existing code. Rejected: every span member in the BCL has an array twin, so this policy would remove the allocation benefit for the entire BCL surface and defeat the purpose the suggestion thread agreed on; it would also make F# and C# 13 callers of the same library pick different overloads. The change is confined to preview.
- **Exact C# parity without rule 10(d).** Rejected: `params string[]` beside `params List<string>` compiles today and would become FS0041, for no benefit.
- **Place rule 10 immediately after the existing params rules** rather than last. Rejected: an extension `params ReadOnlySpan<T>` would then beat an intrinsic `params T[]`, unlike C#.
- **Heap array plus span constructor as the only lowering.** Rejected for the reason given in the suggestion thread: it is no better than the array overload.
- **Embed `ParamCollectionAttribute` into the output assembly** when the target framework lacks it. Rejected: neither Roslyn nor `[<ParamArray>]` does this, and FS-1145 embedded a polyfill only as an `internal` type inside FSharp.Core itself.
- **A `params` keyword or `[<Params>]` shorthand covering both attributes.** Deferred; the attribute form matches how `[<ParamArray>]` is written today and can be sugared later.
- **Do nothing.** Users keep writing `ReadOnlySpan([| ... |])` and allocating.

# Prior art

- **C# 13 params collections** ([proposal](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-13.0/params-collections.md)). The set of allowed types, the metadata, the "better params collection" bullets and the expression-tree exclusion are taken from there. On the attribute: "the current VB compiler will not be able to consume them decorated with `ParamArrayAttribute` neither in normal, nor in expanded form. Therefore, an addition of 'params' modifier is likely to break VB consumers, and very likely consumers from other languages or tools. Given that, non-array `params` parameters are marked with a new `System.Runtime.CompilerServices.ParamCollectionAttribute`." On construction: the invocation "creates an instance of the parameter collection type according to the rules specified in Collection expressions as though the arguments were used as expression elements in a collection expression in the same order". On lifetime: "Params parameters are implicitly `scoped` when their type is a ref struct."
- **Visual Basic** does not consume `ParamCollectionAttribute`; F# is the first other .NET language to propose doing so.
- **F# `ParamArray`**: spec §14.4 rule 2 and the normal/expanded candidate pair are the mechanism this RFC generalises.
- **FS-1093** supplies the per-element conversions and the precedent that adhoc conversions are applied at method-argument position.
- **FS-1145** put `CollectionBuilderAttribute` on FSharp.Core's `list` and `Set`, so under this RFC an F# list is a valid params collection type for both C# and F# callers.
- **FS-1338 and FS-1340** define the pre-filter and the last comparison this RFC's rule follows.
- **FS-1342 (PR #838)** proposes the same shape vocabulary for `[ ... ]` literals; its "no guaranteed stack allocation" stance is the point on which this RFC is stricter, for the reason T-Gro gave.

# Compatibility

- **Binary.** Not breaking. Call sites emit ordinary calls; the only new metadata is a private inline-array struct in `<PrivateImplementationDetails$>`, which no consumer inspects.
- **Source, feature on.** Every program that compiles today still compiles. Calls that today resolve to `params T[]` may resolve to `params ReadOnlySpan<T>` when the method group carries both (`String.Join`, `String.Concat`, `String.Format`, `Path.Combine`, `Console.WriteLine` with five or more arguments, `ImmutableArray.Create` with five or more elements, and the rest of the list in [Motivation](#motivation)). No inferred type changes, because the twins return the same type. No program becomes ambiguous, by rule 10(d).
- **Source, feature off or older compiler.** Unchanged: FS0503 or FS0001 for expanded calls against a non-array params collection; the attribute on an F# declaration is passed through.
- **Older compilers, new binaries.** An F#-declared params collection member is an ordinary method with an attribute; older compilers call it in normal form.
- **FSharp.Core.** No change.
- **Target frameworks.**

  | Target framework | `ParamCollectionAttribute` in BCL | `ReadOnlySpan<T>` | `InlineArrayAttribute` | Effect |
  |---|---|---|---|---|
  | `netstandard2.0`, .NET Framework | no | via `System.Memory` | no | Only libraries that polyfill the attribute expose params collections; span shapes fall back to an array-backed span. |
  | `netstandard2.1`, .NET 5 to 7 | no | yes | no | As above; `RuntimeHelpers.CreateSpan` is available from .NET 7. |
  | .NET 8 | no | yes | yes | Stack storage for span shapes; the attribute still comes from polyfilling libraries. |
  | .NET 9 and later | yes | yes | yes | The full BCL surface listed in Motivation. |

# Interop

- **Consumed by C#.** An F#-declared `[<ParamCollection>]` member is an ordinary C# 13 params collection member. C# treats a span-typed one as `scoped`.
- **Consuming C#.** F# accepts the same set of members in expanded form as C# 13 and makes the same choice except in three documented places: rule 10(d) resolves a pair C# reports as ambiguous; the `obj`-element divergence in [Applicability](#applicability-normal-and-expanded-form); and C#'s "fewer elements" tie-break, which F# does not adopt.
- **Other languages and reflection.** Unchanged; the attribute is already in the metadata.

# Pragmatics

## Diagnostics

New and changed messages (numbers to be assigned at implementation time):

| Condition | Message (draft) |
|---|---|
| Normal form: the argument for a params slot is neither the collection type nor a single element (FS0489 generalised) | This method expects a CLI 'params' collection of type '%s' in this position. Pass the elements as separate trailing arguments, or pass a single value of that type. |
| F# declaration: parameter type is not a params collection type | The parameter '%s' is marked 'ParamCollection' but its type '%s' is not a valid params collection type. Valid types are System.Span<'T>, System.ReadOnlySpan<'T>, IEnumerable<'T>, IReadOnlyCollection<'T>, IReadOnlyList<'T>, ICollection<'T>, IList<'T>, a type with a 'CollectionBuilder' attribute, and a type implementing IEnumerable with an accessible parameterless constructor and 'Add' method. Use 'ParamArray' for array parameters. |
| F# declaration: not the last parameter, optional, or byref | A 'ParamCollection' parameter must be the last parameter of a member, may not be optional, and may not be a byref. |
| F# declaration: both attributes | 'ParamArray' and 'ParamCollection' cannot both be applied to a parameter. |
| F# declaration: span params parameter returned (if enforced) | A 'ParamCollection' parameter of type '%s' cannot be returned from the member, because callers may pass stack-allocated storage. |
| Rule 10 leaves a tie (appended to FS0041, as FS-1340 does) | The candidates differ only in the type of their params collection ('%s' and '%s') and neither is preferred. Pass an explicit collection to select an overload. |
| Expanded span, builder or initializer form inside a quotation | Expanded 'params' arguments of type '%s' cannot be used inside a quotation. Pass an explicit collection. |
| Optional, off by default | Overload resolution selected the params collection overload '%s' over '%s'. |
| FS3532 | Text extended to name `ParamCollection`. |
| Feature off | No new diagnostic; see [Language feature](#language-feature). |

## Tooling

- Tooltips and signature help render `[<ParamCollection>] names: ReadOnlySpan<string>` where they render `[<ParamArray>]` today; the printer currently special-cases only `ParamArrayAttribute`.
- FSharp.Compiler.Service: `FSharpParameter.IsParamArrayArg` stays true only for array params; a new `IsParamCollectionArg` reports the new attribute, so existing consumers see no change.
- Go to definition, rename and find references are unaffected; debugging steps over the synthesised buffer as over any compiler temporary.

## Performance

- **Compilation.** One shape classification per params candidate, cached with the candidate's argument data; the normal/expanded candidate pair already exists.
- **Generated code, existing calls.** Unchanged where the resolution is unchanged. Where a call moves to a span overload, `newarr` plus `N` `stelem` become a stack struct, `N` indexer stores and one `CreateSpan` call: no allocation.
- **Generated code, new calls.** As above; one synthesised struct per distinct element count per assembly.

## Scaling

- Element count per call: linear in generated code and in checking. Human-written calls rarely exceed a dozen elements; the inline-array approach has no fixed upper bound, and an implementation may fall back to a heap array above a threshold.
- Synthesised structs: at most one per distinct element count used in the assembly.

## Culture-aware formatting/parsing

Not applicable.

# Unresolved questions

- **Non-escape enforcement.** Whether the compiler should only document rule 5 of the declaring section, reject a direct `return` of a span params parameter, or implement full `scoped` analysis.
- **The `obj[]` divergence.** Whether the no-type-directed-conversion preference should be adjusted so that `M(xs: obj[])` against a span-only member picks the normal form as in C#.
- **"Fewer elements" tie-break.** Whether to adopt C#'s rule that a candidate whose params collection receives fewer elements is better.
- **`[<ParamArray>]` on a non-array parameter** is silently ignored today; whether that should start warning is out of scope here.
- **FS0503 hint.** Whether the arity error should mention the feature when it is off.
- **Empty arrays.** Whether `N = 0` for the array shape should emit `Array.Empty<T>()` instead of `newarr 0`; unchanged here.
- **Interface shapes and FS-1342.** Whether the read-only interface shapes should share a non-array implementation type if FS-1342 introduces one.
- **Feature name.** `ParamsCollections` is a placeholder.

# Appendix: implementation sketch (dotnet/fsharp)

For reviewers who want to see the compiler-side footprint; file references are to `dotnet/fsharp` `main` at the time of writing.

- **Language feature** — `src/Compiler/Facilities/LanguageFeatures.fs(i)`: `ParamsCollections`, `previewVersion`; `FSComp.txt`: `featureParamsCollections`, the new diagnostics above, and amended `csMethodExpectsParams` (FS0489) and `tcTraitMayNotUseComplexThings` (FS3532).
- **Well-known attribute bits** — `src/Compiler/AbstractIL/il.fs(i)`: `WellKnownILAttributes.ParamCollectionAttribute` (next free bit); `src/Compiler/TypedTree/WellKnownAttribs.fs(i)`: `WellKnownValAttributes.ParamCollectionAttribute`; `src/Compiler/TypedTree/TypedTreeOps.Attributes.fs`: one arm each in `classifyILAttrib` (the `System.Runtime.CompilerServices.` block) and `classifyValAttrib`; `TcGlobals.fs`: `attrib_ParamCollectionAttribute_opt = tryFindSysAttrib ...` beside `attrib_ParamArrayAttribute`.
- **Param metadata** — `src/Compiler/Checking/infos.fs(i)`: `ParamData` and `ParamAttribs` replace `isParamArray: bool` with a three-way `ParamsAttrib` (`NotParams | ParamArray | ParamCollection`), read in `CrackParamAttribsInfo` (F# members), `ComputeILMethodParamAttribs` (IL members) and the type-provider path (never `ParamCollection`); `HasParamArrayArg`; update the readers in `NicePrint.fs`, `CheckExpressions.fs` (trait check and unnamed-argument filter), `CheckComputationExpressions.fs`, `PostInferenceChecks.fs` and `Symbols.fs`.
- **Shape classification** — `src/Compiler/Checking/MethodCalls.fs`: a `ParamsCollectionShape` union (`Array | ReadOnlySpan | Span | ReadOnlyInterface | MutableInterface | Builder of MethInfo | Initializer of ctor * capacityCtorOpt`) and `TryGetParamsCollectionShape infoReader m ty`, using `isArray1DTy`, `tryDestReadOnlySpanTy`/`tryDestSpanTy` from `TypedTreeOps.Attributes.fs`, the interface type constructors, a by-name `CollectionBuilder` lookup and the enumerable-pattern iteration type; `CalledArg` gains the shape, computed in `MakeCalledArgs` or lazily in the `CalledMeth` constructor.
- **Applicability gate** — `MethodCalls.fs`, `CalledMeth` constructor: the `supportsParamArgs` test `possibleParamArg.IsParamArray && isArray1DTy g ...` becomes a shape test gated on the feature; the trailing-argument trimming uses the same predicate; `GetParamArrayElementType()` returns the shape's element type instead of `destArrayTy`.
- **Argument checking** — `src/Compiler/Checking/ConstraintSolver.fs`, `ArgsMustSubsumeOrConvert`: element type from the shape; the FS0489 guard generalised.
- **Elaboration** — `MethodCalls.fs` `AdjustParamArrayCallerArgs` dispatches on the shape; new helpers in `TypedTreeOps`: `mkParamsSpan` (inline-array path when `InlineArrayAttribute` resolves, else array plus constructor), `List<T>(capacity)` plus `Add`, the builder `Create` call, the initializer constructor plus `Add` calls, and `mkDefault` for empty spans.
- **Inline-array synthesis** — `src/Compiler/CodeGen/IlxGen.fs`: `GetOrCreateInlineArrayTypeSpec n` beside `GetOrCreateRawDataFieldSpec`, emitting `<PrivateImplementationDetails$>.InlineArrayN<T>` with `[InlineArray(n)]` and one field; a typed-tree marker for "address of the buffer's first element"; the optional constant path reusing `GenConstArray` and `RuntimeHelpers.CreateSpan`.
- **Tiebreak** — `src/Compiler/Checking/OverloadResolutionRules.fs(i)`: `TiebreakRuleId.BetterParamsCollection`; `betterParamsCollectionRule` with `RequiredFeature = Some ParamsCollections`, appended after `moreConcreteRule` in `allTiebreakRules`, with that rule's "runs last" comment amended; an FS0041 detail alongside `explainIncomparableMethodConcreteness`.
- **Declarations** — `src/Compiler/Checking/PostInferenceChecks.fs` beside the curried-member check, or `CheckDeclarations.fs`: rules 1 and 2 of the declaring section, and rule 5 if enforced; `SignatureConformance.fs`: add the attribute to the list of attributes that must agree between signature and implementation.
- **Quotations** — `src/Compiler/Checking/QuotationTranslator.fs`: reject the span, builder and initializer construction nodes with the new error.
- **Tests** — `tests/FSharp.Compiler.ComponentTests/Interop/ParamArray.fs`: the FS0503 baseline becomes a resolution to the `List<string>` member ("Second"); the three-overload test keeps resolving to the array ("First"); new cases for every row of the resolution table, every shape, the empty expansion, the `netstandard2.1` array-backed fallback, F#-declared members called from F# and from C# (C# helper libraries need `withCSharpLanguageVersionPreview` and a .NET 9 or later reference set for the attribute), quotation errors, declaration errors, feature-off baselines, and an IL baseline showing no `newarr` for `String.Join(", ", a, b)`. `Conformance/Tiebreakers/TiebreakerTests.fs`: rule-10 cases and their ordering against `moreConcreteRule`; `OverloadResolutionPriority` over shapes.
- **Release notes** — `docs/release-notes/.Language/preview.md`.
