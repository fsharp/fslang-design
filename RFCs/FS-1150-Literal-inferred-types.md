# F# RFC FS-1150 - Type-directed resolution of literals

# Summary

This RFC collects separate design suggestions to make F# literals resolvable to types that are not their default types: `int`, `float`, `char`, tuple (`'a * 'b`), `list`, `string`. Each feature can be implemented separately.

F# integer literals `1` would not just be resolved to `int`, but also `int64`, `byte`, `float`, `bigint`, `Complex` and so on.

F# float literals `1.0` would not just be resolved to `float`, but also `float32`, `decimal`, `Half`, `NFloat` and so on.

F# char literals `'c'` would not just be resolved to `char`, but also `byte`, `Rune` and so on.

F# tuple literals would not just be resolved to Tuple (`'a * 'b`), but also struct tuple (`struct('a * 'b)`) and `KeyValuePair<_, _>`. There seems to be not much value to extend this syntax to other types.

F# list literals `[]` would not just be resolved to `list`, but also `ImmutableArray<_>`, `ReadOnlySpan<_>` and so on.

F# string literals `"abc"` would not just be resolved to `string`, but also `PrintfFormat`, `char array`, `ReadOnlySpan<byte>`, `Rune list` and so on.

# Motivation

F# APIs should be able to reasonably choose to adopt `ImmutableArray<_>` or other such collection types for inputs without particularly changing user code that uses list literals. F# promotes being "succinct", these features would eliminate syntactic noise just because the default types in F# are not used.

We over-emphasise the default types in F# - that is, in an API designer wants to get nice, minimal callside syntax, they have no choice but to accept `int`, `float`, `char`, tuple (`'a * 'b`), `list`, `string`. However this has problems:

- It can be relatively highly allocating, e.g. for `list` and `string`.
- It's not pleasant from C#, e.g. C# code cannot use F# `list` easily.
- It's probably not the data representation used inside the API. For example the F# quotations API uses `list` throughout. but internally converts to `array`s.

Some may [say](https://github.com/fsharp/fslang-suggestions/issues/1086#issuecomment-942676668) that these features just save a few characters - the saving is not worth in comparison to either effort spent on other features + extra cost of "magic" conversions happening. However, F# positions itself with "succinctness". It is important that syntactic noise be reduced to a minimum to stay different from major languages such as C#.

```fs
// Current
let xx = seq { "red"; "yellow"; "blue" } |> Set // Eliminates most allocations out of the 4 choices. However, it involves the use of curly braces, not obvious that this creates a collection.
let yy = ["red"; "yellow"; "blue"] |> List.toSeq |> Set.ofSeq // Least magic hidden behind the scene. It's also the most verbose.
let zz = ["red"; "yellow"; "blue"] |> Set.ofList // Eliminates a List.toSeq call. Relies on the availability of toList which is not always the case.

// Proposed
let vv: Set<string> = ["red"; "yellow"; "blue"] // This is the most readable. Is it helpful to reduce the concept count on grouping syntax, particularly with the curly for sequence.
```

There have been similar efforts to reduce syntactic noise before:
- [FS-1080 Dotless float32 literals](https://github.com/fsharp/fslang-design/blob/main/FSharp-5.0/FS-1080-float32-without-dot.md), implemented in F# 5.
- [FS-1110 Dotless indexer syntax](https://github.com/fsharp/fslang-design/blob/main/FSharp-6.0/FS-1110-index-syntax.md), implemented in F# 6.

Aside from being more succinct, there are also potential performance gains - another principle that F# advertises on. For example,

```fs
let a: Set<byte> = set [1uy..10uy]
```

1. Creates a sequence
2. Converts that sequence into a list
3. Constructs the set with the `seq<_>`-acceptable constructor
4. Uses `seq<_>` constructs to add items to the underlying set structure

Instead, type-directed resolution of the list literal can make use of more efficient operations. In addition, the `uy` type specifiers can also be eliminated.

```fs
let a: Set<byte> = [1..10]
```

# Drawbacks

There would be a lot of hidden magic behind the process of type-directed resolution. One of the strengths of F# is that implicit conversion is very, very rare in the language. Nearly everything is explicit in terms of conversions.
- Implicit `yield` solved way more problems than it introduced (especially around Elmish), but one must understand the difference between `yield`, `yield!` and implicit `yield` in the rare corner cases.
- Implicit `op_Implicit` conversions help more than it hinders e.g. going from some concrete type to a base class, and it's pretty easy to explain. But, it'll require some explanation of what `op_Implicit` is etc. - a completely foreign concept for everyday F# developers.
- Implicit type-directed resolutions of literals require explanation of constructors and builder patterns, and the fact that `let x : Set = [ 1 .. 10 ]` isn't the same from a performance point of view as `let x = Set [ 1 .. 10 ]` will be challenging.

There is also risk of introducing action-at-a-distance type resolution behaviour when editing F# code.
```fs
let a = 1 // Defaults to int
let b = 2 // Without code below, this defaults to int.
...
let c = b + 1.5 // Without code below, this defaults b and c to float instead of int,
...
let d: float32 = c // This fixes c, and therefore b, to float32.
```
This can be mitigated with two potential approaches:
1. a warning that type defaulting behaviour is used. For example, `let x = 1` without further type restriction.
2. exposing the behaviour of type defaulting first-class. More on this below.

# Alternatives

Not doing this - F# loses an opportunity to work towards one of its stated goals - to be "succinct", while staying robust and performant.



# Detailed design

# FS-1150a Displaying type defaulting
The design suggestion [#1427](https://github.com/fsharp/fslang-suggestions/issues/1427) has **NOT YET** been marked "approved in principle".

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1086#issuecomment-1575921470)
- [ ] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

This feature provides transparency into how default types are assigned.

```fs
let plus x y = x + y
let plus2 (x: System.DateTime) y = x + y
```
currently have the inferred type of
```fs
val plus: x: int -> y: int -> int
val plus2: x: System.DateTime -> y: System.TimeSpan -> System.DateTime
```
With this feature, they will now change to
```fs
val plus: x: (^a = int) -> y: (^b = int) -> (^c = int)
val plus2: x: (^a = System.DateTime) -> y: (^b = System.TimeSpan) -> (^c = System.DateTime)
```
This is the explicit type of a function with default types.

Whenever F# defaults an otherwise statically resolved type parameter in a non-inline context, the behaviour of type defaulting will now be revealed. This can mitigate current surprises of action-at-a-distance where
```fs
let plus x y = x + y // Originally shows int -> int -> int
plus 3. 4. // How come I can make it float -> float -> float here?
```

Once the types are inferred, they lose their defaults.
```fs
let plus x y = x + y // val plus: x: float -> y: float -> float 
plus 3. 4.
```

The inferred statically resolved type parameter names are to be derived from the equivalent in an `inline` context:
```fs
val inline plus:
  x: ^a -> y: ^b -> ^c when (^a or ^b) : (static member (+) : ^a * ^b -> ^c)
```

The compiled representation will not change. F# code cannot define default types explicitly, they are merely for showing how type inference works.

## Alternative: Allow explicit use of default types
F# functions can define this behaviour explicitly, reusing type parameters where appropriate:
```fs
let plus3 (x: (^a = float)) (y: ^a): ^a = x + y
```

If this function's signature is not constrained by a type elsewhere, the default of `float` is applied to `^a`. Otherwise, the `float` default would be replaced with another foreign type.

```fs
let plus3 (x: (^a = float)) (y: ^a): ^a = x + y
plus3 1 1 // Usage here.
// val plus3: x: int -> y: int -> int
```

However, supporting the definition of default types seems to provide little value for the added maintenance cost.

## Alternative: Keep current behaviour

Keeping the current behaviour of hiding the behaviour of type defaulting. This means that other features in this RFC will have the problem of action-at-a-distance type resolution.

# FS-1150b Type-directed resolution of integer literals
The design suggestion [#1421](https://github.com/fsharp/fslang-suggestions/issues/1421) is marked "approved in principle".

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1421)
- [x] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

The integer literal will now have the type of `^a = int`. The integer literal will try in this order to become:
- built-in integer types: int8, uint8, int16, uint16, int32, uint32, int64, uint64, nativeint, unativeint
- built-in float types: float, float32, decimal
- bigint: direct calls to FSharp.Core.NumericLiterals.NumericLiteralI will exist even if NumericLiteralI is shadowed.
- any NumericLiteralX module in scope: custom QRZING suffixes
- for `t` in [int8; uint8; int16; uint16; int32; uint32; nativeint (with int32 range); unativeint (with uint32 range); int64; uint64; bigint] (ordered by size), if the value is within the range of `t`, then any other type with an `op_Implicit` conversion from `t`
    - System.Half is supported for sbyte and byte ranges.
    - System.Int128, System.Runtime.InteropServices.NFloat and System.Numerics.Complex are supported for int64 and uint64 ranges.
    - System.UInt128 is supported for uint64 range.
    - Some types like [System.Buffers.NIndex](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.nindex?view=net-9.0-pp) only provide implicit conversion from nativeint. Therefore, nativeint support is necessary.
However,
- char is NOT included. Use a char literal instead of an integer literal.

By type inference, declaring a `[<Literal>]` type without the type suffix will be possible. `let [<Literal>] a: byte = 1`

Moreover, integer literal patterns will also be updated to be consistent with declaration of integers. `match 1: byte with 1 -> true | _ -> false`

Since integer literals with underscores, hexadecimal, octal and binary notations must be included with this feature, considering the interaction with NumericLiteralX modules, https://github.com/fsharp/fslang-design/pull/770 must be included.

```fs
let a: bigint = 0xCABBDBEBDEBFA // should work
```

Error checking will happen on the literal for out-of-bounds.

```fs
let a: byte = 300 // error here
match 2: System.Half with
| 300 // error here (integer literals are supported for System.Half for sbyte and byte range only)
    -> ()
| _ -> ()
```

[FS-1093 Additional type directed conversions](https://github.com/fsharp/fslang-design/blob/main/FSharp-6.0/FS-1093-additional-conversions.md), added in F# 6, specifies existing conversions for literals:
```fs
let a: int64 array = [| 1; 2; 3 |] // Converts int32 -> int64
let b: nativeint array = [| 1; 2; 3 |] // Converts int32 -> nativeint
let c: double array = [| 1; 2; 3 |] // Converts int32 -> double
let d: System.Int128 array = [| 1; 2; 3 |] // Converts int32 -> Int128 via Int128.op_Implicit
```

This feature supercedes FS-1093 for the case of integer literals.
```fs
let a: int64 array = [| 1; 2; 3 |] // The integer themselves are int64
let b: nativeint array = [| 1; 2; 3 |] // The integer themselves are nativeint
let c: double array = [| 1; 2; 3 |] // The integer themselves are double
let d: System.Int128 array = [| 1; 2; 3 |] // The integer themselves are Int128
```

Moreover, inference of other types are possible.
```fs
let e: byte array = [| 1; 2; 3 |] // The integer themselves are byte
let f: float32 array = [| 1; 2; 3 |] // The integer themselves are float32
```

## Diagnostics

Hovering the cursor above the integer literal should show the inferred type. Currently this action does not popup anything.

Pressing Go To Definition on the integer literal should navigate to the conversion function used from the `NumericLiteralX` module or the `op_Implicit` definition if used.

# FS-1150c Type-directed resolution of float literals
The design suggestion [#1421](https://github.com/fsharp/fslang-suggestions/issues/1421) is marked "approved in principle".

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1421)
- [x] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

The float literal will now have the type of `^a = float`. The float literal will try in this order to become:
- built-in float types (with compile-time bounds checking): float, float32, decimal
- for `t` in [decimal; float; float32] (ordered by precision), if the value is within the range of `t`, then any other type with an `op_Implicit` conversion from `t`
    - System.Runtime.InteropServices.NFloat is supported for float32 range.
    - System.Numerics.Complex is supported for float range.

By type inference, declaring a `[<Literal>]` type without the type suffix will be possible. `let [<Literal>] a: float32 = 1.2`

Moreover, float literal patterns will also be updated to be consistent with declaration of integers. `match 1.2: float32 with 1.2 -> true | _ -> false`

There is potential interaction with NumericLiteralX modules if https://github.com/fsharp/fslang-design/pull/770 is implemented.

Error checking will happen on the literal for out-of-bounds instead of silently creating infinity. Error checking will also happen on the literal if it becomes zero but declared as a non-zero value. This is a new check that the current compiler does not perform.

```fs
let a: float32 = 1e100 // error here
match a with
| 1e-100 // error here
    -> ()
| _ -> ()
```

## Diagnostics

Hovering the cursor above the float literal should show the inferred type. Currently this action does not popup anything.

Pressing Go To Definition on the float literal should navigate to the conversion function used from the `NumericLiteralX` module (if implemented) or the `op_Implicit` definition if used.

# FS-1150d Type-directed resolution of char literals
The design suggestion [#1421](https://github.com/fsharp/fslang-suggestions/issues/1421) is marked "approved in principle".

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1421)
- [x] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

The char literal will now have the type of `^a = char`. The char literal will try in this order to become:
- char
- byte: It is a little-known fact that the `B` suffix exists for char literals `'a'B`. This allows easier handling of UTF-8 bytes.
- for `t` in [char; byte] (ordered by expectation of being text), any other type with an `op_Implicit` conversion from `t`

System.Text.Rune support can be optionally considered. It would appear after `byte` to handle Unicode scalars that don't fit within 16 bits.

By type inference, declaring a `[<Literal>]` type without the type suffix will be possible. `let [<Literal>] a: byte = 'a'`

Moreover, char literal patterns will also be updated to be consistent with declaration of integers. `match 'a': byte with 'a' -> true | _ -> false`

## Diagnostics

Hovering the cursor above the char literal should show the inferred type. Currently this action does not popup anything.

Pressing Go To Definition on the char literal should navigate to the `op_Implicit` definition if used.

# FS-1150e Type-directed resolution of tuple literals
The design suggestion [#988](https://github.com/fsharp/fslang-suggestions/issues/988) is marked "approved in principle".

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/988)
- [x] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

The tuple literal will now have the type of `^a = _ * _` where `_` is a nominal type. The tuple literal will try in this order to become:
- tuple (`_ * _`)
- struct tuple (`struct(_ * _)`)
- System.Collections.Generic.KeyValuePair<_, _> (if the literal is a 2-tuple)

KeyValuePair<_, _> is important because it's the type used in C#'s proposed [dictionary expressions](https://github.com/dotnet/csharplang/blob/main/proposals/dictionary-expressions.md). This means that together with type-directed list literals as specified below, F# does not need extra syntax to support dictionary expressions.

This approach will alleviate many of the struct tuple inference problems that result from type information only flowing forwards.

```fs
type C() =
    member _.M(x) =
        for a, b in x do ()
        x |> Array.iter (fun (a, b) -> ())
        for v in x do
            let a, b = v
            // up to this point struct has not been used, so we can't be sure either way yet
            v |> fun (a, b) -> ()
            // no struct in the last pattern either, so x defaults to an array of "heap" tuples
            // x has the type `(^a = 'a * 'b) array`
    member _.M2(x) =
        for a, b in x do ()
        x |> Array.iter (fun (a, b) -> ())
        for v in x do
            let a, b = v
            // up to this point struct has not been used, so we can't be sure either way yet
            v |> fun struct(a, b) -> ()
            // x is an array of struct tuples
            // x has the type `struct('a * 'b) array`
```

# FS-1150f Type-directed resolution of list literals
The design suggestion [#1086](https://github.com/fsharp/fslang-suggestions/issues/1086) was marked "approved in principle" before.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1086)
- [x] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

This list literal `[1]` will now have the type of `^a = int list`. Examples that the list literal is allowed to be inferred include:
- list types, per current semantics
- array types by obvious translation
- `Set`/`Map` types
- `ReadOnlySpan<_>` or `Span<_>`, using `stackalloc`
- Mutable collection types that have a constructor (optionally with a capacity arg), and an 'Add' method to add the elements.
- `System.Collections.Immutable` collections.

The full list of possible target types and the order of resolution will follow [C# 12 collection expressions](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-12.0/collection-expressions) with [C# 13 improvements](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-13.0/collection-expressions-better-conversion.md). Parity with C# collection expressions is important because .NET types will be designed for C# consumption.

These types are already supported as of C# 12:
```fs
let a: System.Collections.ArrayList = [1, 2, 3]
let b: System.Collections.Generic.List<int> = [1, 2, 3]
let c: System.Collections.Generic.HashSet<int> = [1, 2, 3]
let d: System.Collections.Generic.SortedSet<int> = [1, 2, 3]
let e: System.Collections.Immutable.ImmutableArray<int> = [1, 2, 3]
let f: System.Collections.Immutable.ImmutableList<int> = [1, 2, 3]
let g: System.Collections.Immutable.ImmutableHashSet<int> = [1, 2, 3]
let h: System.Collections.Immutable.ImmutableSortedSet<int> = [1, 2, 3]
let i: System.Span<int> = [1, 2, 3]
let j: System.ReadOnlySpan<int> = [1, 2, 3]
let k: int array = [1, 2, 3]
```

Support for [C#'s proposed dictionary expressions](https://github.com/dotnet/csharplang/blob/main/proposals/dictionary-expressions.md) via combining type-directed resolution of lists of tuples should also be considered.

F# `list`, `Set`, `Map` types will include the necessary collection builder types as specified in the C# collection expression specification to enable C# consumption. F# implementation of type-directed resolution of list literals will also use them.

## Diagnostics

Hovering the cursor above the list literal should show the inferred type. Currently this action does not popup anything.

Pressing Go To Definition on the list literal should navigate to any conversion methods used under the hood.

# FS-1150g Constructor arguments for list literals

- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)

[C# has identified a need to supply constructor arguments for collection literals.](https://github.com/dotnet/csharplang/blob/main/proposals/collection-expression-arguments.md) It should also make sense for F# to allow this.

```fs
let a: Dictionary<string, int> nameToAge1 = [
    with StringComparer.OrdinalIgnoreCase
    "alice", 23
    "bob", 34
    "carol", 55
]
```

List literals will include `with` as a computation expression keyword. It must be placed at the start of the list literal. Its use will be the same as in C#'s collection expressions. The syntax to the right of `with` is the constructor call syntax, this means that it can either take an argument application without parentheses or tupled arguments with optionally named parameters.
```fs
let l: ResizeArray<int> = [with(capacity = 3); 1; 2]
``` 

# FS-1150h Type-directed resolution of string literals
The design suggestion [#1421](https://github.com/fsharp/fslang-suggestions/issues/1421) is marked "approved in principle".

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1421)
- [x] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

This string literal `"abc"` will now have the type of `^a = string`. The string literal can target, in this order:
- string
- any type targettable by the list literal with its contents set to:
    - char or
    - byte or
    - a type with `op_Implicit` conversion from char or byte

```fs
let a: ReadOnlySpan<byte> = "abc"
let b: ImmutableArray<char> = "abc"
```

If string or char is involved, the string literal is checked by UTF-16 rules, i.e. the current rules. The collection of chars would be the string formatted as UTF-16.

If byte is involved, the string literal is checked by UTF-8 rules, i.e. the current rules except no surrogate characters without its corresponding pair. The collection of bytes would be the string formatted as UTF-8. String interpolation for UTF-8 strings would only accept other collections of bytes, not any object as seen in UTF-16 rules.

## Diagnostics

Hovering the cursor above the string literal should show the inferred type. Currently this action does not popup anything.

Pressing Go To Definition on the string literal should navigate to any conversion methods used under the hood.

# FS-1150i Extending B-suffix string literals to be UTF-8 strings
The design suggestion [#1421](https://github.com/fsharp/fslang-suggestions/issues/1421) is marked "approved in principle".

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1421)
- [x] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

Currently, B-suffix string literals in F# only allow ASCII values. As UTF-8 is the modern standard of text communication, it makes little sense to continue limiting B-suffix string literals to ASCII values only. It will be changed to accept any well-formed UTF-8 string, i.e. a string literal as modified above but only targetting `byte array`.

```fs
let a = "你好"B
```
