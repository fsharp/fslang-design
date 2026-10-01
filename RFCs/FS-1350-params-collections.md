# F# RFC FS-1350 - Params collections (`ParamCollectionAttribute`)

The design suggestion [Native interop for C#13 params enhancements](https://github.com/fsharp/fslang-suggestions/issues/1377) is open and has not been marked "approved in principle". This RFC gives a concrete design for the questions in that thread before a decision.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1377)
- [ ] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [x] [Discussion](https://github.com/fsharp/fslang-design/pull/848)

Related: [FS-1338](FS-1338-OverloadResolutionPriorityAttribute.md), [FS-1340](FS-1340-most-concrete-tiebreaker.md), [FS-1093](../FSharp-6.0/FS-1093-additional-conversions.md), [FS-1053](../FSharp-4.5/FS-1053-span.md), [FS-1145](../FSharp.Core-9.0/FS-1145-csharp-collection-expression-support-for-lists-and-sets.md), [FS-1342](https://github.com/fsharp/fslang-design/pull/838) (target-typed collection literals; no dependency, but where both apply, construction follows this RFC, including the stack storage that FS-1342 does not guarantee).

# Summary

F# recognises `System.Runtime.CompilerServices.ParamCollectionAttribute`, which C# 13 emits for `params` on non-array types, and calls such members in expanded form, as it calls `[<ParamArray>] T[]` members today. Span arguments use stack storage on .NET 8 and later. A last overload tiebreak prefers span shapes, as in C# 13. F# members can declare such parameters. The language feature is `ParamsCollections` (preview). FSharp.Core does not change.

```fsharp
open System
open System.Runtime.CompilerServices

String.Join(", ", a, b, c)        // Join(string, params ReadOnlySpan<string>): stack buffer, no array
String.Join(", ", [| a; b |])     // normal form, Join(string, params string[]): unchanged

type Log =
    static member Write(format: string, [<ParamCollection>] args: ReadOnlySpan<obj>) =
        Console.WriteLine(format, args)

Log.Write("{0} + {1}", 1, 2)
```

# Motivation

F# knows one params convention: `ParamArrayAttribute` on an array parameter. C# 13 marks all other `params` parameters with `ParamCollectionAttribute`, so that older consumers still see them in normal form. F# is such a consumer:

```fsharp
String.Join(", ", ReadOnlySpan [| a; b |])   // works, but allocates the array
names.AddRange("x", "y")                     // FS0503: all versions of 'AddRange' take 1 arguments
"a,b;c".AsSpan().CountAny(',', ';')          // FS0001: expected 'ReadOnlySpan<char>'
```

In the .NET 11 shared framework, every `[ParamCollection]` member takes `ReadOnlySpan<T>`, for example `String.Join`, `Path.Combine`, `Task.WhenAll` and `ImmutableArray.Create`. Most of them have an older `params T[]` overload. The span overloads exist to remove the array allocation.

The suggestion thread asks for stack storage (T-Gro: a span over a new array "would not be any better" than the array overload), construction shared with type-directed collections (vzarytovskii), and an order with `OverloadResolutionPriorityAttribute` (brianrourkeboll; now FS-1338).

# Detailed design

## Params parameters

The last parameter of a member with one curried parameter group is a *params parameter* if it has `ParamArrayAttribute` and a single-dimensional array type (unchanged), or `ParamCollectionAttribute` and one of these types:

| Shape | Parameter type | Element type `E` |
|---|---|---|
| span | `ReadOnlySpan<T>`, `Span<T>` | `T` |
| interface | `IEnumerable<T>`, `IReadOnlyCollection<T>`, `IReadOnlyList<T>`, `ICollection<T>`, `IList<T>` | `T` |
| builder | a type with `[<CollectionBuilder(typeof<B>, "M")>]` | `E` of `B.M(ReadOnlySpan<E>)` |
| initializer | a class or struct that implements `System.Collections.IEnumerable` and has an accessible parameterless constructor and an accessible instance `Add` method | the iteration type, as for `for ... in` |

A builder is valid when `B.M` is a create method under the [C# 12 rules](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md#create-methods) and is at least as accessible as the member. FSharp.Core's `list` and `Set` qualify in its netstandard2.1 and later builds (FS-1145).

Both attributes are matched by full name, on IL metadata and on F# members, so a polyfill works on target frameworks before .NET 9. On imported metadata, `ParamCollectionAttribute` on any other type is ignored, and the parameter is an ordinary parameter.

## Applicability

The expanded form (elements as trailing unnamed arguments) now exists for every params parameter, beside the unchanged normal form (the collection itself, so `T[]` still converts to `ReadOnlySpan<T>` by `op_Implicit`). Each element is checked against `E` as any method argument is: by subsumption (boxing to `obj` included), then by the type-directed conversions of FS-1093.

These rules stay the same for all shapes: a named params argument (`M(xs = v)`) selects the normal form; in an indexed property setter the params parameter is the last index parameter; a params parameter is never optional or `out`.

F# prefers a candidate without a type-directed conversion before it prefers the normal form, and type inference does not use `op_Implicit`. So an array passed alone to a span-only params member becomes one element when the array itself is a valid element. With `M([<ParamCollection>] xs: ReadOnlySpan<obj>)` and `arr: obj[]`, `M(arr)` passes a one-element span; C# 13 passes the elements. With `arr: int[]`, `ReadOnlyCollection.CreateCollection(arr)` infers `T = int[]`, as in C# 13. C# 14 (first-class spans) passes the elements in both cases. `M(ReadOnlySpan arr)` passes the elements.

## Overload resolution

Rule 2 of §14.4 step 7 changes from arrays to all params parameters:

```diff
- 2) Prefer candidates that do not use ParamArray conversion. If two candidates both use ParamArray conversion
-    with types pty1 and pty2, and pty1 feasibly subsumes pty2, then prefer the second candidate over the first.
+ 2) Prefer candidates that do not use params collection conversion. If two candidates both use params
+    collection conversion with element types ety1 and ety2, and ety1 feasibly subsumes ety2, then prefer the
+    second candidate over the first.
```

A new rule 10 runs after FS-1340's rule 9, which no longer runs last (this amends FS-1340). Like every betterness rule, it runs after the FS-1338 priority pre-filter, so a library can force a shape with priorities:

```text
10) If two candidates both use params collection conversion in expanded form, the same actual arguments
    form the collection elements in both, and their element types are equivalent, then with params
    collection types pcty1 and pcty2, prefer the first candidate if
    a) pcty1 is ReadOnlySpan<_> and pcty2 is Span<_>; or
    b) pcty1 is ReadOnlySpan<_> or Span<_>, and pcty2 is not; or
    c) neither is a span type and pcty1 coerces to pcty2; or
    d) neither is a span type, pcty1 is a single-dimensional array type and pcty2 is not.
```

Cases (a) to (c) are the "better params collection" rules of C# 13, except that C# (b) prefers a span only over an array or one of the five interfaces. Case (d) is F# only. The wider (b) and (d) decide only pairs that C# reports as ambiguous (CS0121), so a method group with `params T[]` and any other shapes still resolves, as today. FS-1340 does not order `T[]` against `ReadOnlySpan<T>`, so rule 10 decides `ImmutableArray.Create(1, 2, 3, 4, 5)`.

| Call | Result |
|---|---|
| `String.Join(",", "a", "b")` | `params ReadOnlySpan<string>` by 10(b); the `obj` overloads lose by rule 2. New, as in C# 13. |
| `ImmutableArray.Create(1, 2, 3)` | `Create(T, T, T)`: fixed arity wins by rule 2. Unchanged. |
| `M()`, only `params ReadOnlySpan<string>` | `M` with an empty span. New. |
| `M("a", "b")`, `params List<string>` and `params IEnumerable<string>` | `List<string>` by 10(c). New. |
| `M("a", "b")`, `params string[]`, `params List<string>` and `params IEnumerable<string>` | `string[]` by 10(c) and 10(d). Unchanged; CS0121 in C#. |
| `M("a", "b")`, `params List<string>` and `params HashSet<string>` | FS0041, as CS0121 in C#. |

## Constructing the collection

The collection is built at the position of the params arguments: after the preceding arguments and before any following named argument. The elements are evaluated left to right and converted to `E`, then the collection is created and filled, as for `ParamArray` today. With `N` elements:

| Shape | Value | `N = 0` |
|---|---|---|
| array | `[\| e1; ...; eN \|]` (unchanged) | empty array |
| span | storage below | `default` |
| read-only interface | a `T[]`; the runtime type is not part of the contract | empty array |
| `ICollection<T>`, `IList<T>` | `List<T>(N)`, then `Add` for each element | `List<T>()` |
| builder | `B.M(span)`, with the span in storage below | `B.M(default)` |
| initializer | `new C(N)` if `C` has an accessible constructor whose only parameter is `int capacity`, else `new C()`; then `Add(ei)` for each element, each resolved as a method application | `new C()` |

**Storage.** For a span-shaped argument, and for the span passed to a create method, the first rule that applies decides:

1. A `ReadOnlySpan<T>` whose elements are all constants of type `bool`, `char`, `sbyte`, `byte`, `int16`, `uint16`, `int`, `uint32`, `int64`, `uint64`, `float32` or `float` (C#'s list) refers to static data in the assembly, where the runtime supports it: for 1-byte types on any runtime, for other types through `RuntimeHelpers.CreateSpan` (.NET 7 and later).
2. If the core library of the target framework defines `System.Runtime.CompilerServices.InlineArrayAttribute` (.NET 8 and later; a user-defined copy does not count), the elements must be in a stack buffer: a local of a compiler-generated `[InlineArray(N)]` struct, for any `N` and in any position. In lambdas, sequence expressions and resumable code, the buffer is a local of the generated method, as span temporaries are today. Each call site has one buffer, which it reuses each time it runs.
3. Otherwise, the elements are in a new array.

This covers the collection storage only; boxing an element to `obj` still allocates. Array, interface and initializer shapes allocate on the heap, and a create method may allocate its result.

## Escape rules for callers

An expanded span argument is *stack-referring* (FS-1053) unless it is empty or is a `ReadOnlySpan<T>` whose elements are all constants of a type listed in storage rule 1, as in C#. This classification is the same on every target framework, whatever storage the compiler uses. These are the first stack-referring values in F#; the existing FS-1053 limit analysis applies to them, with these additions:

- If the callee returns a byref, a byref-like value, or a byref to one, the result refers to the caller's stack. It can be used and bound locally, but it cannot be returned from the enclosing function (FS3228 for byrefs, FS3235 for byref-like values).
- The call cannot also take a `byref` or `outref` to a byref-like value, because the callee can store the span through it (FS3233). This includes the receiver of an instance member of a byref-like struct that is not readonly; today FS3233 does not check receivers. `inref` arguments and readonly receivers are allowed, as in C#.
- Inside a `for` or `while` loop, a stack-referring value cannot be stored, by `<-` or through a `byref` or `outref` argument, into a mutable local declared outside the loop or a field of one (new error), because the next iteration reuses the buffer. C# reports CS8352 here.
- A call with a stack-referring argument is never a tail call: the compiler emits no `tail.` prefix and does not compile a self-recursive call as a jump. A tail call frees the buffer before the callee reads it, and a jump refills the buffer while a parameter still refers to it.
- A create method follows the same rules. If the builder's collection type is byref-like, its result is stack-referring. Otherwise the result cannot hold the span and is unrestricted. So F# uses stack storage for every create method, where the C# 12 text allows it only for a `scoped` span parameter; Roslyn also ignores the scope here.

F# does not read `ScopedRefAttribute` or `UnscopedRefAttribute`, so it treats every params span parameter, and every create-method span parameter, as unscoped. C# gives the same errors for an unscoped parameter, and accepts some of these programs when the parameter is `scoped`. In the .NET 11 shared framework, all params span parameters are `scoped` except one, on a member that returns a byref-like type:

```fsharp
// SplitAny<T>(this ReadOnlySpan<T>, [UnscopedRef] params ReadOnlySpan<T>): SpanSplitEnumerator<T>
let hasParts (line: string) (sep: char) =
    let mutable parts = line.AsSpan().SplitAny(sep, ';')   // ok: used locally
    parts.MoveNext()

let split (line: string) (sep: char) = line.AsSpan().SplitAny(sep, ';')   // error FS3235, as in C#
let splitConst (line: string) = line.AsSpan().SplitAny(',', ';')          // ok: static data, as in C#
```

## Declaring params collection parameters in F#

`[<ParamCollection>]` is allowed only on the last parameter of a member with one curried parameter group, and only on a params collection type other than an array (arrays use `[<ParamArray>]`). The parameter cannot be optional, have a default value, be `byref`, `inref` or `outref`, or also have `[<ParamArray>]`. The attribute must come from the target framework or from a polyfill. Signature files must repeat it.

The compiler emits no `ScopedRefAttribute`, so the parameter is unscoped. The member body can use it as any parameter of its type, and can return it where span rules allow. Callers keep stack storage safe by the [escape rules](#escape-rules-for-callers). C# 13 also reads the parameter as unscoped, because C# takes the scope of a metadata parameter from `ScopedRefAttribute`.

## Interactions

- **Quotations**: in quoted code (quotation literals, `query` bodies and `[<ReflectedDefinition>]` members), the expanded span, builder and initializer forms are not applicable, so `String.Join(",", a, b)` there still resolves to `params string[]`. If no other form applies, an error says that the expanded form cannot be quoted. The interface shapes are allowed and quote as their construction in [Constructing the collection](#constructing-the-collection). C# 13 excludes all non-array expanded forms from expression trees (CS9226).
- **Computation expressions**: params collection parameters are excluded from custom-operation arity inference, as `ParamArray` parameters are.
- **SRTP**: FS3532 rejects `ParamCollection` parameters in trait signatures, as it rejects `ParamArray`.
- **Type providers**: provided methods can declare `ParamArray` only.
- **Nullness**: the nullness of `E` comes from the collection's type argument, as for arrays.

# Changes to the F# spec

§14.4 Method Application Resolution, under the feature: the expanded form uses [Params parameters](#params-parameters) and [Applicability](#applicability); step 7 gets rule 2 and rule 10 of [Overload resolution](#overload-resolution); elaboration follows [Constructing the collection](#constructing-the-collection) instead of always building an array. Member parameters can have `ParamCollectionAttribute` as in [Declaring](#declaring-params-collection-parameters-in-f). The byref safety rules gain the checks in [Escape rules for callers](#escape-rules-for-callers).

# Drawbacks

- Calls that move to a span overload give the same result by API contract, but IL, allocation profiles and tools that show the selected member change.
- New code generation: inline-array structs and span construction.
- Builder and initializer shapes need member lookup during applicability.

# Alternatives

- **Span shapes only** (T-Gro's suggestion in the thread): simpler. Rejected: C# 13 libraries also use the other shapes, and calls to them keep failing with FS0503.
- **Keep the array overload when both exist**: no resolution change. Rejected: most BCL span overloads have array twins, so F# would get almost no benefit, and F# and C# would select different overloads.
- **Rule 10 directly after rule 2**: an extension `params ReadOnlySpan<T>` then beats an intrinsic `params T[]`, unlike C#.
- **A `params` keyword**: deferred; the attribute matches `[<ParamArray>]`.

# Prior art

- C# 13 [params collections](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-13.0/params-collections.md): types, metadata, betterness rules, the expression-tree exclusion, implicitly `scoped` span parameters.
- C# 12 [collection expressions](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-12.0/collection-expressions.md): create methods and ref safety. C# *may* use the stack; this RFC requires it.
- Visual Basic does not consume `ParamCollectionAttribute`.

# Compatibility

- With the feature on, programs that compile today still compile, except one case. A call that moves from `params T[]` to a span overload can fail the [escape rules](#escape-rules-for-callers) if the overload returns a byref or a byref-like value, is a member of a non-readonly byref-like struct, or takes a `byref` or `outref` to a byref-like value. No .NET 11 shared-framework member has this combination. Calls that move keep their inferred type, because the BCL overload pairs return the same type. They also stop being tail calls, which changes stack depth, not results.
- With the feature off, or with an older compiler, expanded calls to non-array params collections fail with FS0503 or FS0001, as today. An F# `[<ParamCollection>]` goes to metadata without validation, as `[<ParamArray>]` does (FS-1338 made the same choice).
- Binaries: call sites are ordinary calls. Older compilers call F#-declared params collection members in normal form.
- C# 13 embeds an internal `ParamCollectionAttribute` when the framework lacks it, so C# 13 libraries expose params collections on every target framework; F# matches it by name.

# Interop

C# 13 calls F#-declared params collection members in expanded form. For C# members, the differences in overload choice are listed in [Applicability](#applicability), [Overload resolution](#overload-resolution) and Quotations.

# Pragmatics

## Diagnostics

Numbers are assigned at implementation time.

| Condition | Diagnostic |
|---|---|
| normal-form argument is neither the collection type nor an element | FS0489, changed to name the collection type |
| `[<ParamCollection>]` on a type that is not a params collection type | new error that lists the valid types |
| `[<ParamCollection>]` not last, optional, byref, or with `[<ParamArray>]` | new error |
| rule 10 cannot decide | FS0041 with a detail that names both collection types |
| only an expanded span, builder or initializer form applies in quoted code | new error |
| stack-referring value stored into a mutable declared outside the loop | new error |
| a call with a stack-referring argument also takes a byref to a byref-like value | FS3233, extended to receivers and to every byref of a byref-like value |
| a call selects a params collection overload over an array overload | new informational warning, off by default |

## Tooling

Tooltips and signature help show `[<ParamCollection>]` where they show `[<ParamArray>]` today. FSharp.Compiler.Service adds `FSharpParameter.IsParamCollectionArg`; `IsParamArrayArg` stays true only for arrays. Navigation and rename do not change. The debugger shows the buffer as a compiler temporary.

## Performance

Overload resolution classifies the shape once per candidate.

## Scaling

Generated code is linear in the element count, with one inline-array struct per distinct `N` per assembly. `N` is the number of source arguments, so stack storage has no upper limit, as in Roslyn.

## Culture-aware formatting/parsing

Not applicable.

# Unresolved questions

- Whether F# should read `ScopedRefAttribute`: on calls, so that a result can escape when the params parameter is `scoped`, as in C#; and on overrides and implementations, which today can return a `scoped` span.
- Whether the compiler should embed `ParamCollectionAttribute` when the target framework lacks it, as C# 13 does and as F# does for attributes such as `IsReadOnlyAttribute`.
- Whether quoted code should follow C# 13 instead: select the span overload, and report an error.
- Whether to match C# 14 in the [Applicability](#applicability) cases.
- Whether to adopt C#'s "fewer elements" rule.
- Whether FS0503 should name the feature when it is off.
- Whether the read-only interface shapes should use a non-array type if FS-1342 adds one.
- The feature name `ParamsCollections` is a placeholder.
