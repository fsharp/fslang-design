# F# RFC FS-1365 - C# collection expression support for F# maps

The design suggestion [Support for C# collection expressions in F# maps](https://github.com/fsharp/fslang-suggestions/issues/1480) is not approved yet.
This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1480)
- [ ] Approved in principle
- [ ] Implementation (not started)
- [x] [Discussion](https://github.com/fsharp/fslang-design/pull/870)

# Summary

FSharp.Core marks `Map<'Key, 'Value>` with `CollectionBuilderAttribute` and adds a hidden builder method. C# can then create F# maps with collection expressions:

```csharp
FSharpMap<string, int> ages = [new("Ann", 31), new("Bob", 27)];
```

[FS-1145](https://github.com/fsharp/fslang-design/blob/main/FSharp.Core-9.0/FS-1145-csharp-collection-expression-support-for-lists-and-sets.md) did the same for lists and sets. This RFC applies its design to maps.

# Motivation

C# has no short way to create an F# map. It must build tuples first:

```csharp
var ages = new FSharpMap<string, int>(new[] { Tuple.Create("Ann", 31), Tuple.Create("Bob", 27) });
```

.NET 11 marks `ImmutableDictionary<,>` and `FrozenDictionary<,>` with `CollectionBuilderAttribute`. `Map` is the immutable dictionary of F#, and it has no such attribute.
The C# [dictionary expressions](https://github.com/dotnet/csharplang/blob/main/proposals/dictionary-expressions.md) proposal and F# target-typed literals ([FS-1342](https://github.com/fsharp/fslang-design/pull/838)) use the same builder, so one attribute serves all of them.

# Detailed design

FSharp.Core adds a non-generic builder type next to `Map<'Key, 'Value>`, as FS-1145 does for `Set`:

```fsharp
#if NETSTANDARD2_1_OR_GREATER || NET
[<System.Runtime.CompilerServices.CollectionBuilder(typeof<Map>, "Create")>]
#endif
type Map<'Key, 'Value when 'Key: comparison> = …

#if NETSTANDARD2_1_OR_GREATER || NET
and [<CompilerMessage("This type is for compiler use and should not be used directly", 1204, IsHidden = true);
      Sealed; AbstractClass; CompiledName("FSharpMap")>] Map =
    [<CompilerMessage("This method is for compiler use and should not be used directly", 1204, IsHidden = true)>]
    static member Create([<System.Runtime.CompilerServices.ScopedRef>] items: ReadOnlySpan<KeyValuePair<'Key, 'Value>>) : Map<'Key, 'Value> = …
#endif
```

1. `Create` MUST add the items in order. When a key repeats, the last value MUST win, as in `Map.add`. `ImmutableDictionary.CreateRangeWithOverwrite` and `FrozenDictionary.Create` have the same rule.
2. `Create` MUST use the default comparer of `'Key`, as the `Map` constructor does.
3. `Create` MUST NOT keep a reference to the span. Thus the `scoped` parameter is valid, and C# can put the span on the stack.
4. The builder type and the attribute exist only for `netstandard2.1` and later. The internal `CollectionBuilderAttribute` and `ScopedRefAttribute` polyfills from FS-1145 do not change.

C# accepts `Create` because the iteration type of `Map<'Key, 'Value>` is `KeyValuePair<'Key, 'Value>`.

# Changes to the F# spec

None. The change is in FSharp.Core only.

# Drawbacks

FSharp.Core gets one more hidden public type. Its public surface continues to differ between `netstandard2.0` and `netstandard2.1`, as after FS-1145.

# Alternatives

- `Create` on the `Map` module. An attribute cannot refer to the type of a module (FS-1145, "Static builder classes versus modules").
- A span of tuples. C# requires the iteration type, which is `KeyValuePair<'Key, 'Value>`.
- The first value wins when a key repeats. The BCL builders and the C# dictionary expressions proposal keep the last value.
- An overload with a comparer, as the BCL builders have. `Map` always uses the default comparer of `'Key`.
- No change. C# code continues to build tuples first.

# Prior art

- FS-1145: F# lists and sets.
- .NET 11: `ImmutableDictionary<,>` with `CreateRangeWithOverwrite`, and `FrozenDictionary<,>` with `Create`.

# Compatibility

- This is not a breaking change. The new type and method are hidden.
- `Map [ ... ]` and the `Map` module MUST keep their meaning. `Set [ ... ]` kept its meaning after FS-1145.
- F# compilers before FS-1342 ignore the attribute.
- C# code that uses the builder fails with `MissingMethodException` on an earlier FSharp.Core, as for every FSharp.Core addition.

# Interop

C# 12 and later use the builder for collection expressions. The C# dictionary expressions can use it when they ship.
C# does not check the F# `comparison` constraint, as it does not for the `FSharpMap` constructor today.

# Pragmatics

## Diagnostics

None.

## Tooling

Completion does not show the hidden type or its method.

## Performance

`Create` adds n items in O(n log n) time, with no intermediate collection. Code that compiles today does not change.

## Scaling

Not applicable.

## Culture-aware formatting/parsing

Not applicable.

# Unresolved questions

None.
