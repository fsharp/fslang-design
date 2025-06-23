# Summary

This RFC collects separate design suggestions to make F# literals resolvable to types that are not their default types: `int`, `float`, `char`, tuple (`'a * 'b`), `list`, `string`, `option`. Each feature can be implemented separately.

F# integer literals `1` would not just be resolved to `int`, but also `int64`, `byte`, `float`, `bigint`, `Complex` and so on.

F# float literals `1.0` would not just be resolved to `float`, but also `float32`, `decimal`, `Half`, `NFloat` and so on.

F# char literals `'c'` would not just be resolved to `char`, but also `byte` and so on.

F# tuple literals would not just be resolved to Tuple (`'a * 'b`), but also struct tuple (`struct('a * 'b)`) and `KeyValuePair<_, _>`. There seems to be not much value to extend this syntax to other types.

F# list literals `[]` would not just be resolved to `list`, but also `ImmutableArray<_>`, `ReadOnlySpan<_>` and so on.

F# string literals `"abc"` would not just be resolved to `string`, but also `PrintfFormat`, `char array`, `ReadOnlySpan<byte>`, `Rune list` and so on.

# Motivation

We over-emphasise the default types in F# - that is, in an API designer wants to get nice, minimal callside syntax, they have no choice but to accept `int`, `float`, `char`, tuple (`'a * 'b`), `list`, `string`. However this has problems:

- It can be relatively highly allocating, e.g. for `list` and `string`.
- It's not pleasant from C#, e.g. C# code cannot use F# `list` easily.
- It's probably not the data representation used inside the API. For example the F# quotations API uses `list` throughout. but internally converts to `array`s.

## Succinctness
F# APIs should be able to reasonably choose to adopt `ImmutableArray<_>` or other such collection types for inputs without particularly changing user code that uses list literals. F# promotes being "succinct", these features would eliminate syntactic noise just because the default types in F# are not used.

Some may [say](https://github.com/fsharp/fslang-suggestions/issues/1086#issuecomment-942676668) that these features just save a few characters - the saving is not worth in comparison to either effort spent on other features + extra cost of "magic" conversions happening. However, F# positions itself with "succinctness". It is important that syntactic noise be reduced to a minimum to stay different from major languages such as C#.

```fs
// Current
let xx = seq { "red"; "yellow"; "blue" } |> Set // Eliminates most allocations out of the 4 choices. However, it involves the use of curly braces, not obvious that this creates a collection.
let yy = ["red"; "yellow"; "blue"] |> List.toSeq |> Set.ofSeq // Least magic hidden behind the scene. It's also the most verbose.
let zz = ["red"; "yellow"; "blue"] |> Set.ofList // Eliminates a List.toSeq call. Relies on the availability of toList which is not always the case.

// Proposed
let vv: Set<string> = ["red"; "yellow"; "blue"] // This is the most readable. Is it helpful to reduce the concept count on grouping syntax, particularly with the curly for sequence.
```

It's easier code with fewer boilerplate.

There have been similar efforts to reduce syntactic noise before:
- [FS-1080 Dotless float32 literals](https://github.com/fsharp/fslang-design/blob/main/FSharp-5.0/FS-1080-float32-without-dot.md), implemented in F# 5.
- [FS-1110 Dotless indexer syntax](https://github.com/fsharp/fslang-design/blob/main/FSharp-6.0/FS-1110-index-syntax.md), implemented in F# 6.

## Robustness

As explained in [C#'s collection expressions](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-12.0/collection-expressions) specification:

> Getting the best performance for constructing each collection type can be tricky. Simple solutions often waste both CPU and memory. Having a literal form allows for maximum flexibility from the compiler implementation to optimize the literal to produce at least as good a result as a user could provide, but with simple code.

Having collection initialization logic be done by the compiler can ensure reliable code that works. You do not need to hand-wire stack initialization logic; the compiler can do it for you.

## Performance
Aside from being more succinct, there are also potential performance gains - another principle that F# advertises on.

### Using Span overloads
Modern .NET libraries have added Span overloads for better performance using stack-allocated data. F# currently cannot take advantage of this easily; the user has to write wiring code themselves.

```fs
System.String.Join(",", ["a", "b", "c"])
// before: constructs a list of strings, allocating on the heap.
// after: stack-allocates a ReadOnlySpan<string>, efficiently uses stack memory.
```

### Fewer runtime conversions
Another example:

```fs
let a: Set<byte> = set [1uy..10uy]
```

1. Creates a sequence
2. Converts that sequence into a list
3. Constructs the set with the `seq<_>`-acceptable constructor
4. Uses `seq<_>` constructs to add items to the underlying set structure

Instead, type-directed resolution of the list literal can make use of more efficient operations, resulting in fewer runtime conversions. In addition, the `uy` type specifiers can also be eliminated.

```fs
let a: Set<byte> = [1..10]
```

# Drawbacks

## Explainability
There would be a lot of hidden magic behind the process of type-directed resolution. One of the strengths of F# is that implicit conversion is very, very rare in the language. Nearly everything is explicit in terms of conversions.
- Implicit `yield` solved way more problems than it introduced (especially around Elmish), but one must understand the difference between `yield`, `yield!` and implicit `yield` in the rare corner cases.
- Implicit `op_Implicit` conversions help more than it hinders e.g. going from some concrete type to a base class, and it's pretty easy to explain. But, it'll require some explanation of what `op_Implicit` is etc. - a completely foreign concept for everyday F# developers.
- Implicit type-directed resolutions of literals require explanation of constructors and builder patterns, and the fact that `let x : Set = [ 1 .. 10 ]` isn't the same from a performance point of view as `let x = Set [ 1 .. 10 ]` will be challenging.

## Diagnostics
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
2. exposing the behaviour of type defaulting first-class. This is covered in a separate suggestion - [Display type defaulting](https://github.com/fsharp/fslang-suggestions/issues/1427).

## Breaking changes

There are potential breaking changes with interactions on previously defined type-directed conversions. Specifically, around `int64`, `nativeint` and `float` as defined in [FS-1093](https://github.com/fsharp/fslang-design/blob/main/FSharp-6.0/FS-1093-additional-conversions.md).

```fs
module A
let a = 1 // This is public. There may be external dependencies.
let b = a + 1L
// before: Uses type-directed conversion of int32 -> int64.
// after: changes type of a to int64.
```

This can easily be solved with a recompilation of consuming code.

```fs
let f<'a> (x: 'a) = printfn $"{typeof<'a>}"; x
let a = f 1
let b = a + 1L
// before: Uses type-directed conversion of int32 -> int64.
// after: changes type of a to int64. also changes the generic type parameter used for f, from int32 to int64.
```

However, one can also argue that a better type is being picked. Since the types involved are all numerical, there wouldn't be much runtime behaviour differences. In fact, one can argue that the result is now better:
```fs
let c: float = 1 / 2
// before: 0
// after: 0.5
```

# Alternatives

Not doing this - F# loses an opportunity to work towards one of its stated goals - to be "succinct", while staying robust and performant.

# Detailed design

# FS-1150a Numeric statically resolved type parameter constraints

The design suggestion [#1421](https://github.com/fsharp/fslang-suggestions/issues/1421) is marked "approved in principle".

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1421)
- [x] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

For the purpose of type inference of numeric literals, new statically resolved constraints are needed.

- Numeric value constraint
```fs
<statically-resolved-type-parameter>: <numeric-value>
```
e.g.
```fs
^a: 1
^b: 1.5
^T: -5e-44
```
`<numeric-value>` can be any integer or floating-point literal. This constraint admits any type that includes that literal in the valid range, i.e. `MinValue` to `MaxValue` inclusive, and only float types accept float value constraints. This means that `float` fits the `'T: 1e100` constraint but not `float32`. Moreover, `float` also fits the `'T: 1.5` constraint but not `int`. 

`<numeric-value>` can have arbitrarily many digits specified. It will be parsed as an arbitrary precision decimal literal, then normalized to the floating point form, i.e. mantissa and exponent. If the exponent is within -4 to 5 (i.e. the default when using the `%g` format specifier), it will be displayed with exponent 0 and hidden. Otherwise, the exponent will be displayed as `e+` or `e-` after the mantissa.
```fs
^a: 999999 // ^a: 999999
^b: 999999.999999 // ^b: 999999.999999
^c: 1000000 // ^c: 1e+6
^d: 1000000.1 // ^d: 1.0000001e+6
^e: -999999 // ^e: -999999
^f: -999999.999999 // ^f: -999999.999999
^g: -1000000 // ^g: -1e+6
^h: -1000000.1 // ^h: -1.0000001e+6
^i: 0.0001 // ^i: 0.0001
^j: 0.00009999999 // ^j: 9.999999e-5
^k: -0.0001 // ^k: -0.0001
^l: -0.00009999999 // ^l: -9.999999e-5
```

This constraint only checks for the valid range, not precision. This means that numbers that cannot be exactly represented in binary floating-point numbers are still allowed to be specified. For example, `0.3`. Also, an arbitrary number of decimal digits are allowed to be specified before or after the decimal point.

- Numeric range constraint

When two numeric value constraints are combined, to avoid generating too many separate numeric value constraints, they are consolidated into the numeric range constraint.

```fs
<statically-resolved-type-parameter>: <numeric-value> .. <numeric-value>
```

The numeric values chosen are the minimum and maximum of the numeric value constraints to be combined. 

```fs
let inline f (a: ^a when ^a: -3 and ^a: 7.5 and ^a: 10) = ()
// val inline f: a: ^a -> unit when ^a: -3 .. 10
```

- Floating point constraint

Some numeric computations may want to declare that integer types are unsupported, for example when requiring floating-point division instead of integer division. The floating-point constraint can handle this.

```fs
<statically-resolved-type-parameter>: float
```

```fs
let inline f (a: ^a when ^a: -3 and ^a: 7.5 and ^a: 10 and ^a: float) = ()
// val inline f: a: ^a -> unit when ^a: -3 .. 10 and ^a: float
// now integer types cannot satisfy this type constraint
```

# FS-1150b Type-directed resolution of numeric literals

The above constraints are to be inferred from numeric literals. For example, instead of always requiring `1` to have the type `int`, it now has the statically resolved type `^a when ^a: 1`. The same applies to numeric literals that currently infer the `float` type, for example `23e2` and `1.2`.

When there are decimal digits specified after the unit place, zero or not, an additional floating point constraint is inferred.

```fs
let inline a() = 1
// val inline a: unit -> ^a when ^a: 1
let inline b() = 1. // no digits after the unit place yet.
// val inline b: unit -> ^a when ^a: 1
let inline c() = 1.0
// val inline c: unit -> ^a when ^a: 1 and ^a: float
let inline d() = 1.1
// val inline d: unit -> ^a when ^a: 1.1 and ^a: float
let inline e() = 1e+4
// val inline e: unit -> ^a when ^a: 10000
let inline f() = 1.00001e+4
// val inline f: unit -> ^a when ^a: 10000.1 and ^a: float
let inline g() = 1.0000e+4
// val inline g: unit -> ^a when ^a: 10000
let inline h() = 1.00000e+4 // Note the final 0 falls behind the unit place
// val inline h: unit -> ^a when ^a: 10000 and ^a: float
```

The numeric value constraint can be satisfied by the following types, in order of method overload preference, applying if the numeric value is in range:
1. when without floating-point constraint, the default integer type: `int32`
2. when without floating-point constraint, built-in types with existing type-directed conversions from `int32` as defined in FS-1093: `nativeint` (with `int32` range) -> `int64` -> `float` (with -2^53 to 2^53 range)
3. when without floating-point constraint, other built-in integer types: `uint32` -> `unativeint` (with `uint32` range) -> `uint64` -> `decimal` (with -(2^96-1) to (2^96-1) range) -> `int8` -> `uint8` -> `int16` -> `uint16` -> `bigint` (direct calls to `FSharp.Core.NumericLiterals.NumericLiteralI` will exist even if NumericLiteralI is shadowed)
4. when without floating-point constraint, for `t` in `int32` -> `nativeint` (with `int32` range) -> `int64` -> `float` (with -2^53 to 2^53 range) -> `unativeint` (with `uint32` range) -> `uint32` -> `uint64` -> `decimal` (with -(2^96-1) to (2^96-1) range) -> `int8` -> `uint8` -> `int16` -> `uint16` -> `bigint`, any other type with an `op_Implicit` conversion from `t`. Error if multiple options are found for the same `t`.
    - `System.Int128` does not have built-in support. It is supported using `int32` conversion, then `int64` conversion, then `uint64` conversion, if possible.
    - `System.UInt128` does not have built-in support. It is supported using `uint32` conversion, then `uint64` conversion, if possible.
    - `System.Half` does not have built-in support. It is supported using `int8` conversion, then `uint8` conversion, if possible.
    - `System.Numerics.Complex` does not have built-in support. It is supported using `int32` conversion, then `int64` conversion, then `uint64` conversion, if possible.
    - `System.Runtime.InteropServices.NFloat` does not have built-in support. It is supported using `int32` conversion, then `int64` conversion, then `uint64` conversion, if possible.
    - Note: Some types like [System.Buffers.NIndex](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.nindex?view=net-9.0-pp) only provide implicit conversion from `nativeint`. Therefore, `nativeint` support is necessary.
5. the default float type: `float`
6. other built-in float types: `decimal` -> `float32`
7. for `t` in `float` -> `decimal` -> `float32`, any other type with an `op_Implicit` conversion from `t`. Error if multiple options are found for the same `t`.
    - `System.Numerics.Complex` does not have built-in support. It is supported using `float` conversion, if possible.
    - `System.Runtime.InteropServices.NFloat` does not have built-in support. It is supported using `float32` conversion, if possible.
8. any `NumericLiteralX` module in scope (custom QRZING suffixes), using existing numeric literal conversion functions.

Note that `char` is NOT included. Use a char literal instead of an integer literal.

Note that when the compiler supports more built-in types in the future, there may be new types inserted into the overload lookup chain. However, it is also expected that `int32`, `int64` and `float` overloads are always chosen if available in that order, with `bigint` being the last in order due to performance considerations.

In the absence of type information, when outside an `inline` context, the numeric range constraints default to:

Range | Type
-|-
-2^31 <= range without float constraint <= 2^31 - 1 | `int32`
-2^63 <= range without float constraint <= 2^63 - 1 | `int64`
-(2^1024 - 2^971) <= range with float constraint <= 2^1024 - 2^971 | `float`
any other range | error

Due to performance considerations, there is no default to `bigint`. Use an explicit type annotation to use `bigint` as the concrete type.

By type inference, declaring a `[<Literal>]` type without the type suffix will be possible.
```fs
let [<Literal>] a: byte = 1
let [<Literal>] b: float32 = 1.2
```

Moreover, integer and float literal patterns will also be updated to be consistent with declaration of integers and floats.
```fs
match 1: byte with 1 -> true | _ -> false
match 1.2: float32 with 1.2 -> true | _ -> false
```
If resolved to a non-built-in type, error.

Since integer literals with underscores, hexadecimal, octal and binary notations must be included with this feature, considering the interaction with NumericLiteralX modules, https://github.com/fsharp/fslang-design/pull/770 must be included. For floating point literals, there is also potential interaction with NumericLiteralX modules if https://github.com/fsharp/fslang-design/pull/770 is implemented.


```fs
let a: bigint = 0xCABBDBEBDEBFA // should work
```

Error checking will happen on the literal for out-of-bounds, instead of silently creating infinity for floating point values.

```fs
let a: byte = 300 // error here
match 2: System.Half with
| 300 // error here (numeric literals are supported for System.Half for sbyte and byte range only)
    -> ()
| _ -> ()
let b: float32 = 1e100 // error here
match b with
| 1e100 // error here
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

Hovering the cursor above the numeric literal should show the inferred type. Currently this action does not popup anything.

Pressing Go To Definition on the numeric literal should navigate to the conversion function used from the `NumericLiteralX` module or the `op_Implicit` definition if used.

# FS-1150c Type-directed resolution of infinity and nan

Similarly to float literals, the values `infinity` and `nan` would also become type-directed. Both would have the staticaly resolved type `^a when ^a: float`.

```fs
// Sample implementation
let inline infinity<^a when ^a: float and ^a: (static member PositiveInfinity: ^a)> =
    'a.PositiveInfinity
let inline nan<^a when ^a: float and ^a: (static member NaN: ^a)> =
    'a.NaN
    
// Usage
let a: System.Half = infinity // Currently works
let b: System.Half = nan // Currently works
let c: System.Double = infinity // Currently errors
let d: System.Double = nan // Currently errors
let e: System.Single = infinity // Currently errors
let f: System.Single = nan // Currently errors
```
All 6 value definitions as above should all work.

# FS-1150d Type-directed resolution of char literals
The design suggestion [#1421](https://github.com/fsharp/fslang-suggestions/issues/1421) is marked "approved in principle".

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1421)
- [x] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

Char literals within the ASCII range will now have the statically resolved type of `^a when ^a: (byte|char)`.

```fs
let inline f() = 'a'
val inline f: unit -> ^a when ^a: (byte|char)
```
It is a little-known fact that the `B` suffix exists for char literals `'a'B`. Allowing char literals to resolve to `byte` allows easier handling of UTF-8 bytes.

This "or" statically resolved constraint is similar to the one from printf format strings:
```fs
let inline f() = printfn "%f";;
val inline f: unit -> (^a -> unit) when ^a: (float|float32|System.Decimal) // You cannot write this in source for now but it is displayed as this in FSI
```

Char literals outside the ASCII range will stay having their type as `char`.

`System.Text.Rune` support can be optionally considered. It can handle Unicode scalars that don't fit within 16 bits. However, it is also possible that the added implementation complexity doesn't justify this less used type.

By type inference, declaring a `[<Literal>]` type without the type suffix will be possible. `let [<Literal>] a: byte = 'a'`

If a char literal is matched with a `byte` type, then current rules for B-suffix apply: `'¶': byte` would give an out-of-range error, because B-suffix char literals are strictly ASCII, and `¶` has the value 182. For UTF-8 processing, strict ASCII is preferable, char literals should not fall into the range of 128 to 255.

Moreover, char literal patterns will also be updated to be consistent with declaration of integers. `match 'a': byte with 'a' -> true | _ -> false` If resolved to a type other than char or byte, error.

## Diagnostics

Hovering the cursor above the char literal should show the inferred type. Currently this action does not popup anything.

Pressing Go To Definition on the char literal should navigate to the `op_Implicit` definition if used.

# FS-1150e Type-directed resolution of tuple literals
The design suggestion [#988](https://github.com/fsharp/fslang-suggestions/issues/988) is marked "approved in principle".

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/988)
- [x] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

A new statically resolved type constraint is to be added: `^a: (^b, ^c)` where `^b` and `^c` can be any nominal type or type variable. This constraint allows variable length specification: `^a: (^b, ^c, ^d)`, `^a: (^b, ^c, ^d, ^e)` etc.

The tuple literal will now have this statically resolved type constraint. It can be resolved to the following types, in the order of method overloading preference:
- reference tuple (`_ * _` / `_ * _ * _` / `_ * _ * _ * _`...) (default)
- struct tuple (`struct(_ * _)` / `struct(_ * _ * _)` / `struct(_ * _ * _ * _)`...)
- `System.Collections.Generic.KeyValuePair<_, _>` if the tuple literal has arity 2 i.e. `(_, _)`
- Any type with an `op_Implicit` conversion from struct tuple
- Any type with an `op_Implicit` conversion from `KeyValuePair<_, _>`
- Any type with an `op_Implicit` conversion from reference tuple

Due to performance considerations, `op_Implicit` conversion from the reference tuple is placed last. It being the default type is merely consideration for backwards compatibility.

`KeyValuePair<_, _>` is important because it's the type used in C#'s proposed [dictionary expressions](https://github.com/dotnet/csharplang/blob/main/proposals/dictionary-expressions.md). This means that together with type-directed list literals as specified below, F# does not need extra syntax to support dictionary expressions.

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

Tuple patterns will also be changed to allow type-directed resolution to struct tuple and `KeyValuePair<_, _>`.

```fs
match 1, 2: KeyValuePair<int, int> with
| 1, 2 -> printfn "Works"
| _ -> failwith "Won't reach here"
```

# FS-1150f Type-directed resolution of list literals
The design suggestion [#1086](https://github.com/fsharp/fslang-suggestions/issues/1086) was marked "approved in principle" before.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1086)
- [x] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

A new statically resolved type constraint is to be added: `^a: [^b]` where `^b` can be any nominal type or type variable. The list literal will now be typed as this statically resolved type variable. Examples that the list literal is allowed to be inferred include:
- list types, per current semantics
- array types by obvious translation
- `Set`/`Map` types
- `ReadOnlySpan<_>` or `Span<_>`, using `stackalloc`
- Mutable collection types that have a constructor (optionally with a capacity arg), and an 'Add' method to add the elements.
- `System.Collections.Immutable` collections.

The full list of possible target types and the order of resolution will follow [C# 12 collection expressions](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-12.0/collection-expressions) with [C# 13 improvements](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-13.0/collection-expressions-better-conversion.md). Parity with C# collection expressions is important because .NET types will be designed for C# consumption. Note that list literals are always eagerly evaluated, a `seq` computation expression should be used instead for lazy evaluation.

These types are already supported as of C# 12:
```fs
let a: System.Collections.ArrayList = [1; 2; 3]
let b: System.Collections.Generic.List<int> = [1; 2; 3]
let c: System.Collections.Generic.HashSet<int> = [1; 2; 3]
let d: System.Collections.Generic.SortedSet<int> = [1; 2; 3]
let e: System.Collections.Immutable.ImmutableArray<int> = [1; 2; 3]
let f: System.Collections.Immutable.ImmutableList<int> = [1; 2; 3]
let g: System.Collections.Immutable.ImmutableHashSet<int> = [1; 2; 3]
let h: System.Collections.Immutable.ImmutableSortedSet<int> = [1; 2; 3]
let i: System.Span<int> = [1; 2; 3]
let j: System.ReadOnlySpan<int> = [1; 2; 3]
let k: int array = [1; 2; 3]
```

With type-directed resolution of numeric literals as specified above, this means that `[1]` can now take on the types `int list`, `int Set`, `byte array` etc. List computation syntax should continue to work. For fixed size stack-only collections like `Span`, stack allocation would only be possible if all `yield!` collections are countable. Otherwise, an `ArrayCollector` might be used which allocates heap memory.

Support for [C#'s proposed dictionary expressions](https://github.com/dotnet/csharplang/blob/main/proposals/dictionary-expressions.md) via combining type-directed resolution of lists of tuples should also be implemented.

```fs
let nameToAge1: Dictionary<string, int> = [
    "alice", 23
    "bob", 34
    "carol", 55
    for i in ["david"; "edward"] do i, 67
]
```

F# `list`, `Set`, `Map` types will include the necessary collection builder types as specified in the C# collection expression specification to enable C# consumption. F# implementation of type-directed resolution of list literals will also use them.

A list literal that is initialized to be a `ReadOnlySpan<byte>` with compile-time-known content will be compiler-optimized to [read from static data](https://github.com/fsharp/fslang-suggestions/issues/1350).

## Diagnostics

Hovering the cursor above the list literal should show the inferred type. Currently this action does not popup anything.

Pressing Go To Definition on the list literal should navigate to any conversion methods used under the hood.

# FS-1150g Constructor arguments for list literals

- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)

[C# has identified a need to supply constructor arguments for collection expressions.](https://github.com/dotnet/csharplang/blob/main/proposals/collection-expression-arguments.md) It should also make sense for F# to allow this.

While C# chose to use the `with` keyword at the start of the collection expression, like the following syntax, it does not read well and does not play well with type inference. It would require a constructor/builder argument constraint which is intrinsically tied to the target type instead of being generalizable. It is also hard to troubleshoot: Error messages on the type would be away from the argument definition and typos on argument names would also throw the type inference mechanism off.

```fs
let nameToAge2: Dictionary<string, int> = [
    with StringComparer.OrdinalIgnoreCase
    "alice", 23
    "bob", 34
    "carol", 55
]
```

Instead, a similar mechanism as generative type providers can be considered. `Collection` can be thought of as an generative type provider that takes variable length arguments, named or not, and checks against the inferred type, using C# rules for collection expressions with arguments. The generated type would allow type-directed resolution of list literals without any arguments.

```fs
let inline f() = ["x", 1; "y", 2]
type MyDict1 = Collection<Dictionary<string, int>, StringComparer.OrdinalIgnoreCase>
type MyDict2 = Collection<Dictionary<string, int>, capacity = 2, comparer = StringComparer.OrdinalIgnoreCase>
let x: MyDict1 = f()
let y: MyDict2 = f()
```

This feature is ideally implemented with the general mechanism ([FS-1023 - Allow type providers to generate types from types](https://github.com/fsharp/fslang-design/blob/b3cdb5805855a186195d677a266d358f4caf6032/RFCs/FS-1023-type-providers-generate-types-from-types.md)). It can also be implemented as a compiler intrinsic, but the benefits brought by directly implementing that proposal far outweighs implementing only this special case here.

# FS-1150h Type-directed resolution of list patterns

With type-directed resolution of list construction, it also makes sense to change list deconstruction to be type-directed too.

C# has the [list pattern](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/list-patterns.md) for this. It works based on indexing. F# can do the same:

```fs
match [1; 2; 3]: ReadOnlySpan<byte> with
| [1; 2; 3] -> printfn "It matches"
| _ -> failwith "Won't reach here"
```

If the inferred type of this pattern is not a `list`, the list pattern is changed to match if the length matches and the indexed elements further match the nested patterns. Keep the current behaviour if the inferred type is a `list`. It should work in the same places as the C# list pattern would.

The reliance on indexing means that some types, e.g. sets, can be constructed using the list literal syntax but not deconstructed using the list pattern syntax. However, it also makes sense: you add elements to a collection with order, but this order isn't necessarily preserved within the collection.

This pattern is not customizable, use an active pattern instead for customizing this behaviour.

# FS-1150i Using type-directed list literals to fulfill params parameters

The design suggestion [#1377](https://github.com/fsharp/fslang-suggestions/issues/1377) is **not yet** marked "approved in principle".

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1377)
- [ ] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

Whenever there is a `[<ParamArray>]` parameter encountered (`params` in C#), instead of always inserting an array, wrap the variable-length parameter list inside a type-directed list literal behind  the scenes instead. Reuse all the previously defined rules for type-directed list literals.

# FS-1150j Type-directed resolution of string literals
The design suggestion [#1421](https://github.com/fsharp/fslang-suggestions/issues/1421) is marked "approved in principle".

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1421)
- [x] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

String literals like `"abc"` will now be typed as statically resolved type variables with the statically resolved type constraint `^a when ^a: [^b] and ^b: (byte|char)`. This means that for ASCII content, string literals are now a special case of the list literal with char literals as content. `"abc"` is equivalent to `['a'; 'b'; 'c']`. This allows target typing to any collection of `byte`s and `char`s.

```fs
let a: ReadOnlySpan<byte> = "abc"
let b: ImmutableArray<char> = "abc"
```

If char is involved, the string literal is checked by UTF-16 rules, i.e. the current rules. The collection of chars would be the string formatted as UTF-16.

If byte is involved, the string literal is checked by UTF-8 rules, i.e. the current rules except no surrogate characters without its corresponding pair. The collection of bytes would be the string formatted as UTF-8. String interpolation for UTF-8 strings would only accept other collections of bytes, not any object as seen in UTF-16 rules. String interpolation for UTF-8 strings will not allow format specifiers.

For scenarios preferring to avoid compiler checking UTF-8 strings, list literals of bytes should be used instead. `let a: ReadOnlySpan<byte> = [yield! "abc"; 0xD0]`

Meanwhile, `string` will satisfy the statically resolved type constraint of `^a when ^a: [char]`. For declaring `string` bindings with string literals, the compiler can directly emit a string literal in IL, as is done today.

`StringBuilder`s will be used if a list literal is target-typed to a `string`. This feature gives a free path to [string computation expressions](https://github.com/fsharp/fslang-suggestions/issues/1149).
```fs
let a: string = [
    'a'
    yield! "bcd"
    for i in 'e' .. 'z' do i
]
```

A UTF-8 string literal that is initialized to be a `ReadOnlySpan<byte>` will be compiler-optimized to [read from static data](https://github.com/fsharp/fslang-suggestions/issues/1350).

## Diagnostics

Hovering the cursor above the string literal should show the inferred type. Currently this action does not popup anything.

Pressing Go To Definition on the string literal should navigate to any conversion methods used under the hood.

# FS-1150k Extending B-suffix string literals to be UTF-8 strings
The design suggestion [#1421](https://github.com/fsharp/fslang-suggestions/issues/1421) is marked "approved in principle".

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1421)
- [x] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

Currently, B-suffix string literals in F# only allow ASCII values. As UTF-8 is the modern standard of text communication, it makes little sense to continue limiting B-suffix string literals to ASCII values only. It will be changed to accept any well-formed UTF-8 string, i.e. a string literal as modified above but only targetting `byte array`.

```fs
let a = "你好"B
```

# FS-1150l Type-directed resolution of string patterns

With type-directed resolution of string construction, it also makes sense to change string deconstruction to be type-directed too.

If the inferred type is not a `string`, the string pattern would be a shorthand for the list pattern to match `char`s or `byte`s.

```fs
match [97; 98; 99]: ReadOnlySpan<byte> with
| "abc" -> printfn "It matches"
| _ -> failwith "Won't reach here"
```

This pattern is not customizable, use an active pattern instead for customizing this behaviour.

This subsumes suggestion [#1351](https://github.com/fsharp/fslang-suggestions/issues/1351).
