# Summary

This RFC collects separate design suggestions to make F# literals resolvable to types that are not their default types: `int`, `float`, `char`, tuple (`'a * 'b`), `list`, `string`, `option`. Each feature can be implemented separately.

F# integer literals `1` would not just be resolved to `int`, but also `int64`, `byte`, `float`, `bigint`, `Complex` and so on.

F# float literals `1.0` would not just be resolved to `float`, but also `float32`, `decimal`, `NFloat` and so on.

F# char literals `'c'` would not just be resolved to `char`, but also `byte` and so on.

F# tuple literals would not just be resolved to Tuple (`'a * 'b`), but also struct tuple (`struct('a * 'b)`) and `KeyValuePair<_, _>`.

F# list literals `[]` would not just be resolved to `list`, but also `ImmutableArray<_>`, `ReadOnlySpan<_>` and so on.

F# string literals `"abc"` would not just be resolved to `string`, but also `PrintfFormat`, `char array`, `ReadOnlySpan<byte>`, `Rune list` and so on.

User types can be added to type resolution via `op_Implicit` conversions and `Deconstruct` methods. For example, if one wishes to define `System.Numerics.Vector3` via a tuple literal, it can be done with a type extension.

```fs
open System.Numerics
type Vector3 with
    static member op_Implicit struct(x, y, z) = Vector3(x, y, z)
    member this.Deconstruct(x: _ outref, y: _ outref, z: _ outref) =
        x <- this.X; y <- this.Y; z <- this.Z
let x: Vector3 = 2, 3, 4
match x with
| 2, 3, 4 -> printfn "It works"
| _ -> failwith "Won't reach"
```

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

It's easier code with fewer boilerplate. Moreover, F# 

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
There is also risk of introducing action-at-a-distance type resolution behaviour when editing F# code. This RFC enables the following:
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

For people who prefer being explicit at the cost of succinctness, an opt-in warning can be introduced that warns whenever the inferred type for a literal does not resolve to the default, for each kind of literal.

## Breaking changes

There are potential breaking changes with interactions on previously defined type-directed conversions. 

```fs
let f<'a> (x: 'a) = printfn $"{typeof<'a>}"; x
let a = f 1
let b: int64 = a
// before: Uses type-directed conversion of int32 -> int64.
// after: changes type of a to int64. also changes the generic type parameter used for f, from int32 to int64.
```

However, one can also argue that a better type is being picked. Since the types involved are all numerical, there wouldn't be much runtime behaviour differences. In fact, one can argue that the result is now better:
```fs
let c: float = 1 / 2
// before: 0
// after: 0.5
```

Meanwhile, changes are also observable to reflection and boxing.

```fs
let a = ["1"; "2"; "3"]
let b = a :> obj // Now errors because a is ReadOnlySpan instead of list
let c = System.String.Concat(",", b)
```

# Alternatives

Not doing this - F# loses an opportunity to work towards one of its stated goals - to be "succinct", while staying robust and performant.

# Detailed design

# FS-1150a Inference behaviour of new inferred statically resolved constraints

C# has been adding type-directed features like [collection expressions](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/collection-expressions), which makes C# more succinct, robust and performant when calling .NET methods that expect collections, like `ReadOnlySpan`. It is ideal that F# achieves parity with C# at least when calling method overloads using the cleanest collection syntax (`[ ... ]`). However, `[ ... ]` currently has the fixed type `_ list`. Therefore, this RFC focuses on adding type-directed inference of existing literals like `[ ... ]`. There are a few levels of implementation to choose from -

1. Only allow type-direction at literals passed directly to method arguments.

```fs
open System
// before
do
    String.Join(",", [| "1"; "2"; "3" |].AsSpan()) |> printfn "%s"
    String.Join(",", [| "1"; "2"; "3" |]) |> printfn "%s"
// after
do  // now calls (string * ReadOnlySpan<string>) overload with stack allocation.
    String.Join(",", ["1"; "2"; "3"]) |> printfn "%s"
    (String.Join: string * string array -> _)(",", ["1"; "2"; "3"]) |> printfn "%s"
```
2. In addition to level 1, also allow modification of type inference anywhere when the target type is known.
```fs
// also allow
do
    let xs: ReadOnlySpan<string> = ["1"; "2"; "3"]
    String.Join(",", xs) |> printfn "%s"
    let ys: string array = ["1"; "2"; "3"]
    String.Join(",", ys) |> printfn "%s"
```

3. In addition to level 2, also allow modification of type inference throughout sequences of local `let` bindings (or `|>` piped chains) as well, with impact on type inference.
```fs
// also allow
do
    let xs = ["1"; "2"; "3"] // now infers ReadOnlySpan<string>
    String.Join(",", xs) |> printfn "%s"
    let ys = ["1"; "2"; "3"] // now infers string array
    (String.Join: string * string array -> _)(",", xs) |> printfn "%s"
// also allow
do
    ["1"; "2"; "3"] // now infers ReadOnlySpan<string>
    |> fun xs -> String.Join(",", xs) |> printfn "%s" // Note: |> support for ref structs is in scope of this RFC.
    ["1"; "2"; "3"] // now infers string array
    |> Array.copy
    |> fun ys -> String.Join(",", ys) |> printfn "%s"
```

4. In addition to level 3, also allow `inline` functions to specify constraints to enable type direction for literals on a statically resolved type parameter.
```fs
// also allow
let inline g<^a when ^a: [string]>() =
        ["1"; "2"; "3"]
// ReadOnlySpan<string> is not applicable because ref structs cannot propagate across inline function boundaries. See https://github.com/fsharp/fslang-suggestions/issues/688#issuecomment-1201603354
// string * string array
do String.Join(",", g()) |> printfn "%s"
```

5. In addition to level 4, also allow modification of type inference across inline function boundaries.
```fs
// also allow
let inline f() = ["1"; "2"; "3"] // f: unit -> ^a when ^a: [string]
// string * string array
do String.Join(",", f()) |> printfn "%s"
```
6. In addition to level 5, also allow modification of type inference across non-public non-inline function boundaries.
```fs
// also allow
let private f() = ["1"; "2"; "3"] // unit -> string array
do String.Join(",", f()) |> printfn "%s"
```
7. In addition to level 6, also allow modification of type inference across public non-inline function boundaries.
```fs
// also allow
let f() = ["1"; "2"; "3"] // return type should admit ReadOnlySpan<string> - construct an array as stack allocated data cannot be returned here
do String.Join(",", f()) |> printfn "%s"
```

C# only allows up to level 2 because it does not perform type inference as much as F# does.
```cs
using System;
// level 1
Console.WriteLine(String.Join(",", ["1", "2", "3"]));

// level 2
ReadOnlySpan<string> xs = ["1", "2", "3"];
Console.WriteLine(String.Join(",", xs));
// Note: The above line emits nullability warning CS8620 on `xs` because ReadOnlySpan<string> is passed into ReadOnlySpan<string?>

// level 3 - error
var xs = ["1", "2", "3"];
// error CS9176: There is no target type for the collection expression.
Console.WriteLine(String.Join(",", xs));

// level 6 - error
var f() => ["1"; "2"; "3"];
// error CS0825: The contextual keyword 'var' may only appear within a local variable declaration or in script code
Console.WriteLine(String.Join(",", f()));
```

Level 7 breaks binary compatibility with existing code. Not just for list literals but also for numeric literals when implicit conversions are applied to `int64`, `nativeint` and `float` as defined in [FS-1093](https://github.com/fsharp/fslang-design/blob/main/FSharp-6.0/FS-1093-additional-conversions.md). Therefore, it must not be implemented.

```fs
module A
let a = 1 // This is public. There may be external dependencies.
let b = a + 1L
// before: Uses type-directed conversion of int32 -> int64.
// after: changes type of a to int64.
```

[When are new features a good thing?](https://github.com/fsharp/fslang-suggestions/tree/8955f1b4a01312f5efe79ec724f8d0536748885b?tab=readme-ov-file#when-are-new-features-a-good-thing) notes that

> features which make the language more orthogonal, simpler and easier to use are generally a very good thing.

Allowing more literals to fit different types makes the language more orthogonal. Literals can now be "implemented" with different types without the need to be explicit about conversions. The annotations that are required to denote a different numeric or collection type that is not the default can be eliminated.
```fs
// before
let simple: int list = [1; 2; 3; 4]
let moreSyntax: uint64 Set = set [1UL; 2UL; 3UL; 4UL]
let evenMoreSyntax: ImmutableArray<byte> = ImmutableArray.Create [|1uy; 2uy; 3uy; 4uy|]
// after
let simple: int list = [1; 2; 3; 4]
let moreSyntax: uint64 Set = [1; 2; 3; 4]
let evenMoreSyntax: ImmutableArray<byte> = [1; 2; 3; 4]
```
It's also simpler and easier to use.

Meanwhile, this should also be as orthogonal as possible with type inference - the fact that more types support direct definitions from literals should not interfere with type inference. If `[ ... ]` can be an `ImmutableArray`, then it should behave like one as much as possible under type inference.

Ideally for all the new type-directed inference, the same rules for inference of statically resolved constraints should also be followed:

```fs
let f a b = a + b
let g = f 1L 2L // Changes type of f to long -> long -> long
```

Ideally level 7 would result in the most orthogonality between type-directed resolution and type inference but with binary compatibility constraints, only level 6 is achievable. This RFC aims to achieve a level 6 implementation. It can also be trimmed as necessary to target a lower level implementation, as each lower level is a subset of a higher level.

Targeting implementation level 6 instead of 7 means that a series of public `let` bindings in a module might work differently than a series of local `let` bindings or a series of non-public `let` bindings in a module.

```fs
do
    let x = ["1"; "2"; "3"] // ReadOnlySpan<string> (best type in a local context)
    printfn "%s" <| System.String.Concat(",", x)
    let y = ["1"; "2"; "3"] // string Set (limited by type constraint below)
    printfn "%A" <| Set.union y y
module private PrivateModule =
    let x = ["1"; "2"; "3"] // string array (best type outside a local context)
    printfn "%s" <| System.String.Concat(",", x)
    let y = ["1"; "2"; "3"] // string Set (limited by type constraint below)
    printfn "%A" <| Set.union y y
module PublicModule =
    let x = ["1"; "2"; "3"] // string list (defaulted for binary compatibility)
    printfn "%s" <| System.String.Concat(",", x)
    let y = ["1"; "2"; "3"] // string list (defaulted for binary compatibility)!!
    printfn "%A" <| Set.union y y // error!!
```

This is especially confusing if the series of `let` bindings is used in [anonymous implementation files](https://github.com/fsharp/fslang-spec/blob/main/spec/program-structure-and-execution.md#implementation-files). A warning should be implemented to warn against defaulting behaviour due to public visibility despite later code trying to infer it as a different type.

```fs
module PublicModule =
    let x = ["1"; "2"; "3"] // warn - defaulted to string list due to public visibility despite best type being string array
    printfn "%s" <| System.String.Concat(",", x)
    let y = ["1"; "2"; "3"] // warn - defaulted to string list due to public visibility despite later code trying to constrain to Set
    printfn "%A" <| Set.union y y // error
    let n = 1 // no warning - int is chosen as the default because the type is unconstrained, not because of public visibility. 
```

It is good practice to specify types for public declarations that others may depend on anyway. This warning should encourage clearer code.

## Interactions between inference of different literals

During method overload resolution, there is a need to disambiguate between different overloads.

Constraint solving will define a `LiteralConversionCost` vector. It defaults to 0, and each constraint will add to a dimension. Only a vector with lower values in some or all vector dimensions and same in other or no vector dimensions, is preferred, i.e. choose a Pareto front over other solutions. In addition, some partial ordering rules may also be defined at each constraint.

## Changes to specification - [Method Application Resolution](https://github.com/fsharp/fslang-spec/blob/1890512002c43f832cbdd6524587c22563589403/spec/inference-procedures.md#method-application-resolution)

Before the [method overload preference rule introduced by FS-1093](https://github.com/fsharp/fslang-design/blob/b3cdb5805855a186195d677a266d358f4caf6032/FSharp-6.0/FS-1093-additional-conversions.md#interaction-with-method-overload-resolution), add a new rule that reads the `LiteralConversionCost` vector result from constraint solving, and prefers an overload that when compared to all other overloads, meets all of the following criteria:
1. satisfies all of:
    - less than or equal in `NumericTwoStep` dimension
    - less than or equal in sum (`Numeric` + `NumericTwoStep`) dimensions
    - less than or equal in sum (`NumericBackCompat` + `Numeric` + `NumericTwoStep`) dimensions
  
   i.e. preserves the preference of None (default `int` / `float`) < `NumericBackCompat` < `Numeric` < `NumericTwoStep` order within numeric type inference.
2. satisfies all of:
    - less than or equal in `TupleTwoStep` dimension
    - less than or equal in sum (`Tuple` + `TupleTwoStep`) dimensions

    i.e. preserves the preference of 
> Note: If an overload dominates another in the above comparisons, the dominated overload can be removed from future comparisons. Up-sums can also be memoized.

The above condition guarantees a unique choice only if `LiteralConversionCost` is all zero, i.e. resolves to all default types.

The following dimensions are defined for `LiteralConversionCost` vector:
- `NumericBackCompat`
- `Numeric`
- `NumericTwoStep`
- ``

## Changes to specification - [Function and Value Definitions in Modules](https://github.com/fsharp/fslang-spec/blob/1890512002c43f832cbdd6524587c22563589403/spec/namespaces-and-modules.md#function-and-value-definitions-in-modules)

```diff
Function and value definitions in modules are processed in the same way as function and value definitions in expressions (§14.6), with the following adjustments:

* Each defined value may have an accessibility annotation (§10.5). By default, the accessibility annotation of a function or value definition in a module is public.
* Each defined value is externally accessible if its accessibility annotation is public and it is not hidden by an explicit signature. Externally accessible values are guaranteed to have compiled CLI representations in compiled CLI binaries.
+ * If the function or value is not `inline` and is externally accessible and all containing modules are externally accessible, then all unresolved type variables for argument types and return type, are marked as forced default as defined in Type Constraints (§5.2).
```
## Changes to specification - [Members](https://github.com/fsharp/fslang-spec/blob/1890512002c43f832cbdd6524587c22563589403/spec/type-definitions.md#members)

```diff
+ * If the member is not `inline` and is externally accessible (§10.2.1) and all containing types and modules are externally accessible, then all unresolved type variables for argument types and return type, are marked as forced default as defined in Type Constraints (§5.2).
```

# FS-1150b Numeric statically resolved type parameter constraints

The design suggestion [#1421](https://github.com/fsharp/fslang-suggestions/issues/1421) was marked "approved in principle" before.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1421)
- [ ] Approved in principle
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
`<numeric-value>` can be any integer or floating-point literal. This constraint admits any type that includes that literal in the valid range (built-in or via implicit conversion), i.e. `MinValue` to `MaxValue` inclusive, and only float types accept float value constraints. This means that `float` fits the `'T: 1e100` constraint but not `float32`. Moreover, `float` also fits the `'T: 1.5` constraint but not `int`. 

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

The numeric value constraint can be satisfied by the following types, in the order of method overload resolution preference for a single argument, generating a successful solution if the numeric value is in range:
1. when without floating-point constraint, the default integer type: `int32`
2. when without floating-point constraint, built-in types with existing type-directed conversions from `int32` as defined in FS-1093: `nativeint` (with `int32` range) / `int64` / `float` / any type with an `op_Implicit` conversion from `int32`
3. when without floating-point constraint, other built-in integer types: `uint32` / `unativeint` (with `uint32` range) / `uint64` / `decimal` / `int8` / `uint8` / `int16` / `uint16` / `bigint` (direct calls to `FSharp.Core.NumericLiterals.NumericLiteralI` will exist even if NumericLiteralI is shadowed) / `System.Half` / `System.Int128` / `System.UInt128` (matching by namespace and type name)
4. when without floating-point constraint, for `t` in `nativeint` (with `int32` range) / `int64` / `float` / `unativeint` (with `uint32` range) / `uint32` / `uint64` / `decimal` / `int8` / `uint8` / `int16` / `uint16` / `bigint` / `System.Half` / `System.Int128` / `System.UInt128`, any other type with an `op_Implicit` conversion from `t`. 
    - `System.Half` / `System.Int128` / `System.UInt128` are special cased as their full ranges cannot be easily supported via conversion from other types without limitations in functionality.
    - `System.Numerics.Complex` does not have built-in support. It is supported using `int32` conversion, then `int64` conversion, then `uint64` conversion, if possible.
    - `System.Runtime.InteropServices.NFloat` does not have built-in support. It is supported using `int32` conversion, then `int64` conversion, then `uint64` conversion, if possible.
    - Note: Some types like [System.Buffers.NIndex](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.nindex?view=net-9.0-pp) only provide implicit conversion from `nativeint`. Therefore, `nativeint` support is necessary.
5. the default float type: `float`
6. existing type-directed conversions from `float` as defined in FS-1093: any type with an `op_Implicit` conversion from `float`
7. other built-in float types: `decimal` / `float32` / `System.Half`
8. for `t` in `decimal` / `float32` / `System.Half`, any other type with an `op_Implicit` conversion from `t`.
    - `System.Numerics.Complex` does not have built-in support. It is supported using `float` conversion, if possible.
    - `System.Runtime.InteropServices.NFloat` does not have built-in support. It is supported using `float32` conversion, if possible.
    
Note that `char` is NOT included. Use a char literal instead of an integer literal.

Note that when the compiler supports more built-in types in the future, there may be new types inserted into the overload lookup chain. However, it is also expected that `int32`, `int64` and `float` overloads are always chosen if available in that order, with `bigint` being the last in order due to performance considerations.

In the absence of type information or when public visibility is encountered, when outside an `inline` context, the numeric range constraints default to, in this order:

Range | Type
-|-
-2^31 <= range without float constraint <= 2^31 - 1 | `int32`
-2^63 <= range without float constraint <= 2^63 - 1 | `int64`
-(2^1024 - 2^971) <= range with float constraint <= 2^1024 - 2^971 | `float`
any other range | error

Due to performance considerations, there is no default to `bigint`. Use an explicit type annotation to use `bigint` as the concrete type.

This means that:
```fs
let a = 9e22 // This now becomes 
```

## Changes to specification - [Type Constraints](https://github.com/fsharp/fslang-spec/blob/1890512002c43f832cbdd6524587c22563589403/spec/types-and-type-constraints.md#type-constraints)

```diff
+ token numeric-value-constraint :=
+    float
+    int
+    + float
+    + int
+    - float
+    - int
+ static-typar := ^ ident

constraint :=
    typar :> type                  -- coercion constraint
    typar : null                   -- nullness constraint
    static-typars : ( member-sig ) -- member "trait" constraint
    typar : (new : unit -> 'T)     -- CLI default constructor constraint
    typar : struct                 -- CLI non-Nullable struct
    typar : not struct             -- CLI reference type
    typar : enum< type >           -- enum decomposition constraint
    typar : unmanaged              -- unmanaged constraint
    typar : delegate<type, type>   -- delegate decomposition constraint
    typar : equality
    typar : comparison
+   static-typar : numeric-value-constraint -- numeric value constraint
+   static-typar : numeric-value-constraint .. numeric-value-constraint -- numeric range constraint
+   static-typar : 'float' -- float constraint
```

```diff
F# supports the following type constraints:
+ - Numeric value constraints
+ - Numeric range constraints
+ - Float constraints
```
### Numeric value constraints
An _explicit numeric value constraint_ has the following form:
```
static-typar : numeric-value-constraint
```
During constraint solving (see §14.5), for the constraint `type : numeric-value-constraint`, it is normalized to a new numeric range constraint by having the numeric value on both sides of the range constraint.

### Numeric range constraints
An _explicit numeric range constraint_ has the following form:
```
static-typar : numeric-value-constraint .. numeric-value-constraint
```

Each `numeric-value-constraint` is normalized to sign, mantissa and exponent form. Sign is 1 or -1. Mantissa is a decimal number in [1,10). Exponent is an arbitrarily sized integer. The value of the `numeric-value-constraint` is represented by `sign * mantissa * 10 ^ exponent`. If the right `numeric-value-constraint` has a smaller value than the left `numeric-value-constraint`, a compile-time error is reported.

During constraint solving (see §14.5), for the constraint `type : numeric-value-constraint .. numeric-value-constraint`, 
1. If `type` is one of: `sbyte`, `byte`, `int16`, `uint16`, `int32`, `uint32`, `int64`, `uint64`, `float32`, `float`, `decimal`: The constraint is satisfied when `MinValue` of  `type` <= left `numeric-value-constraint`, and right `numeric-value-constraint` <= `MaxValue` of `type`.
2. If `type` is `bigint`: the constraint is satisfied.
3. If `type` is `nativeint`: the constraint is satisfied when `MinValue` of `int32` <= left `numeric-value-constraint`, and right `numeric-value-constraint` <= `MaxValue` of `int32`.
4. If `type` is `unativeint`: the constraint is satisfied when `MinValue` of `uint32` <= left `numeric-value-constraint`, and right `numeric-value-constraint` <= `MaxValue` of `uint32`.
5. If `type` is `System.Half` (matching a value type with this namespace and type name): the constraint is satisfied when -65504 (hardcoded, = `System.Half.MinValue`) <= left `numeric-value-constraint`, and right `numeric-value-constraint` <= 65504 (hardcoded, = `System.Half.MaxValue`).
6. If `type` is `System.Int128` (matching a value type with this namespace and type name): the constraint is satisfied when -170,141,183,460,469,231,731,687,303,715,884,105,728 (hardcoded, = `System.Int128.MinValue`) <= left `numeric-value-constraint`, and right `numeric-value-constraint` <= 170,141,183,460,469,231,731,687,303,715,884,105,727 (hardcoded, = `System.Int128.MaxValue`), and a constructor `uint64 * uint64` is defined.
7. If `type` is `System.UInt128` (matching a value type with this namespace and type name): the constraint is satisfied when 0 (hardcoded, = `System.UInt128.MinValue`) <= left `numeric-value-constraint`, and right `numeric-value-constraint` <= 340,282,366,920,938,463,463,374,607,431,768,211,455 (hardcoded, = `System.UInt128.MaxValue`), and a constructor `uint64 * uint64` is defined.
8. If `type` defines a static member `op_Implicit` from `base-type` to `type`: the constraint is satisfied when `base-type` used as `type` in steps 1 to 7 satisfies the constraint.
9. Otherwise, the constraint is not satisfied.

After the above steps, an additional check on a _forced default_ flag is done. It is set during checking Function and Value Definitions in Modules (see §10.2.1) and checking Members (see §8.1). If the forced default flag is set and the constraint is not resolved to its default type (see below), a compiler warning is issued about forced default due to public visibility, with information on the default type and otherwise inferred type. The default type is then used instead of the inferred type.

The _default type_ for a numeric range constraint and float constraint is as follows, in this order.
Range | Type
-|-
`System.Int32.MinValue` <= range without float constraint <= `System.Int32.MaxValue` | `int32`
`System.Int64.MinValue` <= range without float constraint <= `System.Int64.MaxValue` | `int64`
-1.7976931348623157E+308 (`System.Double.MinValue`) <= range with float constraint <= 1.7976931348623157E+308 (`System.Double.MaxValue`) | `float`
any other range | out of range error

### Float constraints
An _explicit float constraint_ has the following form:
```
static-typar : 'float'
```
During constraint solving (see §14.5), for the constraint `type : 'float'`, it is satisfied if `type` is `float`, `float32`, `decimal`, `System.Half`, or a type that defines static member `op_Implicit` from `float`, `float32`, `decimal` or `System.Half`, to `type`.

## Changes to specification - [Inference Procedures](https://github.com/fsharp/fslang-spec/blob/1890512002c43f832cbdd6524587c22563589403/spec/inference-procedures.md#constraint-solving)

```diff
typar :> type
typar : null
( type or ... or type ) : ( member-sig )
typar : (new : unit -> 'T)
typar : struct
typar : unmanaged
typar : comparison
typar : equality
typar : not struct
typar : enum< type >
typar : delegate< type, type >
+ typar : numeric-value-constraint .. numeric-value-constraint
+ typar : 'float'
```
### Solving numeric value and range constraints

During constraint solving (see §14.5), for any new numeric value constraint `typar : numeric-value-constraint` or any new numeric range constraint `typar : numeric-value-constraint .. numeric-value-constraint`, the normalization procedure as described for each of the two constraints in Type Constraints (§5.2) occurs.

If any existing numeric range constraints on the same type variable `typar` exist in the constraint set `typar: numeric-value-constraint .. numeric-value-constraint`, the new constraint unifies with the existing constraint, with left hand side numeric value being the minimum of the left hand side numeric value of the existing constraint and the new constraint, and with right hand side numeric value being the maximum of the left hand side numeric value of the existing constraint and the new constraint.

When `type` is not a variable type in `type : numeric-value-constraint .. numeric-value-constraint`, it is resolved using the procedures for satisfying numeric range constraints as described in Type Constraints (§5.2).

While resolving a numeric range constraint,
- If there is not a float constraint on `type`:
    - if constraint satisfaction occurs at steps 1 to 7, and `type` is `int64`, `nativeint` or `float`, then `NumericBackCompat` of the `LiteralConversionCost` vector is incremented.
    - if constraint satisfaction occurs at steps 1 to 7, and `type` is not `int32`, `int64`, `nativeint` or `float`, then `Numeric` of the `LiteralConversionCost` vector is incremented.
    - if constraint satisfaction occurs at step 8, and `base-type` is `int32`, then `NumericBackCompat` of the `LiteralConversionCost` vector is incremented.
    - if constraint satisfaction occurs at step 8, and `base-type` is not `int32`, then `NumericTwoStep` of the `LiteralConversionCost` vector is incremented.
- If there is a float constraint on `type`:
    - if constraint satisfaction occurs at steps 1 to 7, and `type` is not `float`, then `Numeric` of the `LiteralConversionCost` vector is incremented.
    - if constraint satisfaction occurs at steps 8, and `base-type` is `float`, then `NumericBackCompat` of the `LiteralConversionCost` vector is incremented.
    - if constraint satisfaction occurs at steps 8, and `base-type` is not `float`, then `NumericTwoStep` of the `LiteralConversionCost` vector is incremented.

By the end of a type inference environment, if `typar` in `typar : numeric-value-constraint` and `typar : 'float'` fails to undergo generalization, the default type as described in Type Constraints (§5.2) is applied.

### Solving Nullness, Struct, and Other Simple Constraints

```diff
type : null
type : (new : unit -> 'T)
type : struct
type : not struct
type : enum< type >
type : delegate< type, type >
type : unmanaged
+ type : 'float'
```

# FS-1150c Type-directed resolution of numeric literals

The above constraints are to be inferred from numeric literals. For example, instead of always requiring `1` to have the type `int`, it now has the statically resolved type `^a when ^a: 1`. The same applies to numeric literals that currently infer the `float` type, for example `23e2` and `1.2`.

When there is a decimal point in the numeric literal declaring intention of floating point, or if the exponent part of the scientific notation is negative, a float constraint is automatically inferred.

```fs
let inline a() = 1
// val inline a: unit -> ^a when ^a: 1
let inline b() = 1. // decimal point detected; float required.
// val inline b: unit -> ^a when ^a: 1 and ^a: float
let inline c() = 1.0
// val inline c: unit -> ^a when ^a: 1 and ^a: float
let inline d() = 1.1
// val inline d: unit -> ^a when ^a: 1.1 and ^a: float
let inline e() = 1e+4 // no decimal point - allow integer interpretation!
// val inline e: unit -> ^a when ^a: 10000
let inline f() = 1.e4
// val inline f: unit -> ^a when ^a: 10000 and ^a: float
let inline g() = 1e-1
// val inline g: unit -> ^a when ^a: 0.1 and ^a: float
```

Why look at the decimal point and require a float?
- Expectation: `9.` must be a `float` before this RFC. It is a shorthand for `9.0`, which cannot be typed as an integer in almost all programming languages. The decimal point already represents imprecision.
    ```fs
    let bacterialPopulationInSeaWater = 1.20e9 // inherently imprecise, uncertainty +-0.05e9
    ```
    In contrast, integers are expected to be exact, without the use of the decimal point.
    ```fs
    let oneEther = 1e18 // exact value in Etherium wei
    ```
- Simpler rules: Decimal point infers float, that's it. This is compared to alternatives like looking at whether the last decimal place is past the unit place, or extra annotations aside from the decimal point to prevent `9.00e2` from being an integer.
- Conciseness: Still, for large `bigint` values, one has to count and write out all the zeroes or use verbose multiplication and exponentiation operators to achieve the same thing as scientific notation. Allowing scientific notation without the decimal point improves this for exact integer values that have a large magnitude. 

By type inference, declaring a `[<Literal>]` type without the type suffix will be possible.
```fs
let [<Literal>] a: byte = 1
let [<Literal>] b: float32 = 1.2
```

Since integer literals with underscores, hexadecimal, octal and binary notations must be included with this feature, considering the interaction with NumericLiteralX modules, https://github.com/fsharp/fslang-design/pull/770 must be included. For floating point literals, there is also potential interaction with NumericLiteralX modules if https://github.com/fsharp/fslang-design/pull/770 is implemented.

```fs
let a: bigint = 0xCABBDBEBDEBFA // should work
```

Error checking will happen on the literal for out-of-bounds, instead of silently creating infinity for floating point values.

```fs
let a: byte = 300 // error here
let b: float32 = 1e100 // error here
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

Moreover, integer and float literal patterns will also be updated to be consistent with declaration of integers and floats. Since the specification defines [simple constant patterns](https://github.com/fsharp/fslang-spec/blob/1890512002c43f832cbdd6524587c22563589403/spec/patterns.md#simple-constant-patterns) in terms of simple constant expressions and FSharp.Core.Operators.(=), no change to specification is needed.

```fs
match 1: byte with 1 -> true | _ -> false
match 1.2: float32 with 1.2 -> true | _ -> false
```

```fs
match 2: System.Half with
| 2000 // error here
    -> ()
| _ -> ()
match b with
| 1e100 // error here
    -> ()
| _ -> ()
let c = 1e1000 // error here
```

## Changes to specification - [Simple Constant Expressions](https://github.com/fsharp/fslang-spec/blob/1890512002c43f832cbdd6524587c22563589403/spec/expressions.md#simple-constant-expressions)

```diff
- 86              // int/int32
- 1.              // float/double
- 1.01            // float/double
- 1.01e10         // float/double
```

### Numeric literals

An expression lexed ([§3](https://github.com/fsharp/fslang-spec/blob/1890512002c43f832cbdd6524587c22563589403/spec/lexical-analysis.md#lexical-analysis)) from any of the following tokens is a numeric literal:

```
int
float
xint
```

If `xint` is lexed, underscores are first removed from it, then parsed as a decimal number. Normalization procedures for the numeric range constraint in Type Constraints (§5.2) occurs.

Type checking proceeds as follows:

The expression is checked with initial type `ty`.
A numeric value constraint is applied (§5.2.3):
```
static-typar: numeric-value-constraint
```
If the lexed numeric literal contains a decimal dot or the exponent part of scientific notation (with an `e`) is negative, a float constraint is also inferred.

When the numeric value constraint is satisfied at steps 1 to 4 of resolving the equivalent numeric range constraint, this constraint ensures that the type directly supports the numeric values specified. Implementations should choose the closest value representable by the resolved type.

When the numeric value constraint is satisfied at step 5 of resolving the equivalent numeric range constraint, a `uint16` with the same bits as the target 16-bit floating point number would be stack-allocated and reinterpreted as `System.Half`.

When the numeric value constraint is satisfied at step 6 or 7 of resolving the equivalent numeric range constraint, a call to the `uint64 * uint64` constructor discovered during type resolution should be used, with the first `uint64` being the upper 64 bits of the 128 bit value and the second `uint64` being the lower 64 bits of the 128 bit value respectively.

When the numeric value constraint is satisfied at step 8 of resolving the equivalent numeric range constraint, a call to the `op_Implicit` static member discovered during type resolution should be used, with the argument as the closest value representable by the base type. If there are multiple `op_Implicit` candidates, the compiler is free to choose from one of the base types arbitrarily for best performance.

The compiler is free to assume that any `op_Implicit`, `op_Explicit` or constructor calls generated for resolving the equivalent numeric range constraints are idempotent and are free to cache.

## Diagnostics

Hovering the cursor above the numeric literal should show the `op_Implicit` method if used, or the inferred type otherwise. Currently this action does not popup anything.

Pressing Go To Definition on the numeric literal should navigate to the `op_Implicit` definition if used.

# FS-1150d Type-directed resolution of special float values

Similarly to float literals, the values `infinity` and `nan` would also become type-directed. Both would have the statically resolved type `^a when ^a: float`.
In addition, the new globally available inline type functions `pi`, `tau` and `e` are to be defined:

```fs
// Sample implementation
let inline infinity<^a when ^a: float and ^a: (static member PositiveInfinity: ^a)> =
    'a.PositiveInfinity
let inline nan<^a when ^a: float and ^a: (static member NaN: ^a)> =
    'a.NaN
let inline pi<^a when ^a: float and ^a: (static member PI: ^a)> =
    'a.PI
let inline tau<^a when ^a: float and ^a: (static member PI: ^a)> =
    'a.Tau
let inline e<^a when ^a: float and ^a: (static member E: ^a)> =
    'a.E
    
// Usage
let a: System.Half = infinity // Currently works
let b: System.Half = nan // Currently works
let c: System.Double = infinity // Currently errors
let d: System.Double = nan // Currently errors
let e: System.Single = infinity // Currently errors
let f: System.Single = nan // Currently errors
```
All 6 value definitions as above should all work.

The underlying issue is that infinity and nan are defined on `System.Single` and `System.Double` as constant fields. Fields do not satisfy statically resolved member constraints - suggestion [[SRTP] Allow field constraints on SRTP (val)](https://github.com/fsharp/fslang-suggestions/issues/1307). Fields should also satisfy member constraints for the above code to work.

After changing this, the following modification can be done in [prim-types.fs](https://github.com/dotnet/fsharp/blob/e30b2da35395488ff52693884344f84ebae0e39e/src/FSharp.Core/prim-types.fs#L4993C1-L5004C1) of FSharp.Core:
```diff
  [<CompiledName("Infinity")>]
- let infinity = Double.PositiveInfinity
+ [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
+ let infinityDouble = Double.PositiveInfinity

  [<CompiledName("NaN")>]
- let nan = Double.NaN
+ [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
+ let nanDouble = Double.NaN

+ [<CompiledName("InfinityGeneric")>]
+ let inline infinity<^a when ^a: float and ^a: (static member PositiveInfinity: ^a)> = 'a.PositiveInfinity
+ [<CompiledName("NaNGeneric")>]
+ let inline nan<^a when ^a: float and ^a: (static member NaN: ^a)> = 'a.NaN
+ [<CompiledName("PIGeneric")>]
+ let inline pi<^a when ^a: float and ^a: (static member PI: ^a)> = 'a.PI
+ [<CompiledName("TauGeneric")>]
+ let inline tau<^a when ^a: float and ^a: (static member PI: ^a)> = 'a.Tau
+ [<CompiledName("EGeneric")>]
+ let inline e<^a when ^a: float and ^a: (static member E: ^a)> = 'a.E

+ [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
  [<CompiledName("InfinitySingle")>]
  let infinityf = Single.PositiveInfinity

+ [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
  [<CompiledName("NaNSingle")>]
  let nanf = Single.NaN 

```

This will cause a new version of FSharp.Core to cause a source breakage on an old compiler that uses these two definitions but that's to be expected, it should be fine as long as it's not a binary breakage.

## Changes to specification - [Solving member constraints](https://github.com/fsharp/fslang-spec/blob/1890512002c43f832cbdd6524587c22563589403/spec/inference-procedures.md#solving-member-constraints)

```diff
A member constraint is satisfied if one of the types in the support set type1 ... typen satisfies the member constraint. A static type type satisfies a member constraint in the form (static~opt member ident : arg-type1 * ... * arg-typen -> ret-type) if all of the following are true:

* type is a named type whose type definition contains the following member, which takes n arguments: static~opt member ident : formal-arg-type1 * ... * formal-arg-typen -> ret-type
+ or type is a named type whose type definition contains the following field: static~opt val ident : ret-type
+ and the set of arg-types is empty.
* The type and the constraint are both marked static or neither is marked static.
* The assertion of type inference constraints on the arguments and return types does not result in a type inference error.
```

# FS-1150e float64 type abbreviation and d literal suffix

After the above changes, unlike the `l` suffix for `int32`, there is no suffix that forces `float` value interpretation. In fact, out of all the built-in numeric types, `float` is the only one without a suffix - even `int` has `l`.

 C# has `d`/`D` to specify `double` values. F# can just copy it. This is consistent with float numeric suffixes being case insensitive (float32 `f`/`F`, decimal `m`/`M`) despite integer numeric suffixes being case sensitive (int8 `y` uint8 `uy` int16 `s` uint16 `us` int32 `l` uint32 `u` int64 `L` uint64 `uL`/`UL` nativeint `n` unativeint `un`).

Moreover, the current type abbreviations can be grouped into 
1. Fixed size integers - `[u]int[<size>]` (`<size>` defaults to 32)
2. Integers of unknown size - `[u]nativeint`, `bigint`
3. Binary floating point - `float[<size>]` (`<size>` defaults to 64)
4. `decimal`
5. `[s]byte`

Note how `int32` exists in spite of `int`, but `float64` doesn't exist. Why not make the naming system more consistent by including `float64` as a type abbreviation too?

## Changes to specification - [Numeric literals](https://github.com/fsharp/fslang-spec/blob/1890512002c43f832cbdd6524587c22563589403/spec/lexical-analysis.md#numeric-literals)

```diff
token ieee64 =
-   | float                                      For example, 3.0
+   | float [Dd]                                 For example, 3.0
    | xint 'LF'                                  For example, 0x0000000000000000LF
```

## Changes to specification - [Basic type abbreviations](https://github.com/fsharp/fslang-spec/blob/1890512002c43f832cbdd6524587c22563589403/spec/the-f-library-fsharpcoredll.md#basic-type-abbreviations)

```diff
- | `float`, `double` | `System.Double` |
+ | `float`, `float64`, `double` | `System.Double` |
```

## Changes to specification - [Basic Types that Accept Unit of Measure Annotations](https://github.com/fsharp/fslang-spec/blob/1890512002c43f832cbdd6524587c22563589403/spec/the-f-library-fsharpcoredll.md#basic-types-that-accept-unit-of-measure-annotations)

```diff
-| `float<_>` | Underlying representation `System.Double`, but accepts a unit of measure. |
+| `float<_>`, `float64<_>` | Underlying representation `System.Double`, but accepts a unit of measure. |
```

# FS-1150f Type-directed resolution of tuple literals
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

## Specification changes - [Type Constraints](https://github.com/fsharp/fslang-spec/blob/1890512002c43f832cbdd6524587c22563589403/spec/types-and-type-constraints.md#type-constraints)

```diff
constraint :=
    typar :> type                  -- coercion constraint
    typar : null                   -- nullness constraint
    static-typars : ( member-sig ) -- member "trait" constraint
    typar : (new : unit -> 'T)     -- CLI default constructor constraint
    typar : struct                 -- CLI non-Nullable struct
    typar : not struct             -- CLI reference type
    typar : enum< type >           -- enum decomposition constraint
    typar : unmanaged              -- unmanaged constraint
    typar : delegate<type, type>   -- delegate decomposition constraint
    typar : equality
    typar : comparison
+   static-typar : (type , ... , type) -- tuple constraint
```

### Tuple constraints
An _explicit tuple constraint_ has the following form:
```
static-typar : (type , ... , type)
```

where `type , ... , type` has at least two types.

During constraint solving (see §14.5), for the constraint `type : (type , ... , type)`:
1. if `type` is the tuple type `type * ... * type` or struct tuple type `struct(type * ... * type)`: the constraint is satisfied when the types in `type * ... * type` match the types in `type , ... , type` in order and the type counts are equal.
2. if `type` is `System.Tuple<type , ... , type>` or `System.ValueTuple<type , ... , type>`: the constraint is satisfied when `type` converted to an F# tuple satisfies step 1.
3. if `type` is `System.Collections.Generic.KeyValuePair<type1, type2>`: the constraint is satisfied when `type , ... , type` is of length 2 and the two types match `type1` and `type2` in that order.
4. if `type` has a static member `op_Implicit` with `type`: the constraint is satisfied when the input type of this member satisfies one of steps 1 to 3.

After the above steps, an additional check on a _forced default_ flag is done. It is set during checking Function and Value Definitions in Modules (see §10.2.1) and checking Members (see §8.1). If the forced default flag is set and the constraint is not resolved to its default type (see below), a compiler warning is issued about forced default due to public visibility, with information on the default type and otherwise inferred type. The default type is then used instead of the inferred type.

The _default type_ for a tuple constraint is the tuple type `type * ... * type`.

## Changes to specification - [Tuple expressions](https://github.com/fsharp/fslang-spec/blob/1890512002c43f832cbdd6524587c22563589403/spec/expressions.md)

An expression of the form `expr1 , ..., exprn` is a _tuple expression_. For example:

```fsharp
let three = (1,2,"3")
let blastoff = (10,9,8,7,6,5,4,3,2,1,0)
```

The expression has the type of a fresh statically resolved type variable `^S` and the type constraint `^S : (ty1 , ... , tyn)` for fresh types `ty1 ... tyn`. Each individual expression `expri` is checked using initial type `tyi`.

An expression of the form `struct (expr1 , ..., exprn)` is a _struct tuple expression_. For example:

```fsharp
let pair = struct (1,2)
```

The expression has the type `struct (ty1 * ... * tyn)` for fresh types `ty1 ... tyn`. Each individual expression `expri` is checked using initial type `tyi`.

Tuple types and expressions that have their type resolved to reference tuple `ty1 * ... * tyn` are translated into applications of a family of .NET types named
[`System.Tuple`](https://learn.microsoft.com/dotnet/api/system.tuple). Tuple types `ty1 * ... * tyn` are translated as follows:

(unchanged text omitted)

Tuple types and expressions that have their type resolved to struct tuple `struct (ty1 * ... * tyn)` are translated in the same way to [`System.ValueTuple`](https://learn.microsoft.com/dotnet/api/system.valuetuple).

(unchanged note omitted)

Tuple expressions that have their type resolved to `System.Collections.Generic.KeyValuePair<ty1, ty2>` are translated to an invocation of the `ty1 * ty2` constructor of that type with the 2 tuple arguments applied.

Tuple expressions that have their type resolved to a type that supports an `op_Implicit` conversion  are translated to an invocation of the `ty1 * ty2` constructor of that type with the 2 tuple arguments applied.



# FS-1150g Type-directed resolution of tuple patterns
The design suggestion [#751](https://github.com/fsharp/fslang-suggestions/issues/751) is marked "approved in principle".

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/988)
- [x] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

The [C#-way](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/functional/deconstruct#deconstructing-user-defined-types) of doing quick pattern matching and value extraction is by declaring member functions of name `Deconstruct`, or static extension methods accordingly. A `Deconstruct` method has the signature of:

```cs
public void Deconstruct(out T1 name1, out T2 name2, ...)
```
... which actively extracts values from the class instance.
Multiple overloads can be supplied to accommodate different ways of deconstruction.

Note that `Deconstruct` with one out variable can only be used with positional patterns but not deconstruction in C#:
```cs
using System;
if(new Person { Name = "John Doe", Age = 69 } is Person())
    Console.WriteLine(1);
if(new Person { Name = "John Doe", Age = 69 } is Person(var a))
    Console.WriteLine(2);
var (x, y) = new Person { Name = "John Doe", Age = 69 };
// var (z) = new Person { Name = "John Doe", Age = 69 }; // doesn't work

class Person {
    public string Name; public int Age; public DateTime DateOfBirth;
    public void Deconstruct() { Console.WriteLine("a"); }
    public void Deconstruct(out string name) { name = Name; }
    public void Deconstruct(out string name, out int age) { name = Name; age  = Age; }
}
```

Here, the deconstruction syntax equivalent is implemented in F#. This means that one-output deconstruction is unsupported. The tuple pattern will be modified to support `Deconstruct` method lookups:

```fs
type Person(name: string, age: int) =
    member _.Deconstruct(n: _ outref, a: _ outref) = n <- name; a <- age
let n, a = Person("John Doe", 69)
Person("John Doe", 69) |> fun (n, a) -> ()
// n: string = "John Doe"
// a: int = 69
```

The [design considerations](https://github.com/dotnet/csharplang/blob/5c5e51654f7f217cc5d6bfa0442c97b9c2606891/meetings/2016/LDM-2016-05-03-04.md) behind the `Deconstruct` method can be summarized as:
> 1. `Deconstruct` is a method because other features in C# that operate on objects are also method-based (`await` -> `.GetAwaiter`, `for` -> `.GetEnumerator`, collection initializer -> `.Add`). 
> 2. `Deconstruct` uses `out` parameters for method overloading, where future added data in deconstructions can come from additional overloads instead of breaking binary compatibility.

For the second point, conversion operators also support return type overloading, so `op_Implicit` to tuples would be an alternative way to implement deconstruction. However, unlike the rest of this RFC which relies on `op_Implicit` operators, they are not used here because of semantic differences.

Implicit conversions to tuples do a lot more than just deconstruction and change the way that a given type can be consumed. Conversion operators focus on constructing the destination type, which other references to `op_Implicit` in this RFC do. However, even though a `Rectangle` for example can be deconstructed into width and height, constructing a tuple storing the same values as width and height would lose this context, as opposed to constructing a tuple from a `Point2D` for example, as a point is nothing more than the tuple of its coordinates. This is about API design flexibility rather than these behaviours being intrinsically unpreferable.

The precise steps to determine a `Deconstruct` overload follows the same steps as in C#. This means that when multiple `Deconstruct` methods of the same number of parameters is encountered, an ambiguity error is issued regardless of the concrete types.

## Diagnostics

Hovering the cursor above the tuple pattern should show `Deconstruct` overload used if available, or the inferred type otherwise. Currently this action does not popup anything.

Pressing Go To Definition on the tuple pattern should navigate to any `Deconstruct` methods used under the hood if used.

# FS-1150g Type-directed resolution of tuple patterns

# FS-1150h Special support for pipeline operators to allow ref struct usage

When the operators `|>` `||>` `|||>` `<|` `<||` `<|||` as defined in FSharp.Core are used in an infix place, and the tuple argument for multi-argument pipeline operators (`||>` `|||>` `<||` `<|||`) is a syntactical tuple (maximum 1 layer of surrounding parentheses), there will no longer be calls to the actual operators, rather direct syntactical translation before type inference will be done at compile-time. Full support of `allows ref struct` authoring from F# is outside of the scope of this RFC. This enables the most common pipline usages of `ReadOnlySpan` espeically when combined with type-directed resolution of list literals.

## Diagnostics

Currently, this is the tooltip when hovering over the pipeline operator in `let f a = a in 1 |> f`
```
val inline (|>): arg : 'T1 -> func: ('T1 -> 'U) -> 'U

Apply a function to a value, the value being on the left, the function on the right.

Returns:
The function result.

Generic Parameters:
'T1 is int
'U is obj

Full name: Microsoft.FSharp.Core.Operators.(|>)
```

It should be changed to the tooltip for argument application.

```
val f: x: 'a -> 'b
'a
```

For multi-argument pipeline operators, the highlighted argument should be all of the arguments that are applied.

The type of the tuple applied immediately to the pipeline operators would lose their type. The tooltip for hovering over the syntactical tuple immediately applied to a pipeline operator should show the same tooltip as the pipeline operator as shown above.

# FS-1150i Type-directed resolution of list literals
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

# FS-1150j Constructor arguments for list literals

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

# FS-1150k Type-directed resolution of list patterns

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

# FS-1150l Using type-directed list literals to fulfill params parameters

The design suggestion [#1377](https://github.com/fsharp/fslang-suggestions/issues/1377) is **not yet** marked "approved in principle".

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1377)
- [ ] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

Whenever there is a `[<ParamArray>]` parameter encountered (`params` in C#), instead of always inserting an array, wrap the variable-length parameter list inside a type-directed list literal behind  the scenes instead. Reuse all the previously defined rules for type-directed list literals.

# FS-1150m Type-directed resolution of char literals
The design suggestion [#1421](https://github.com/fsharp/fslang-suggestions/issues/1421) was marked "approved in principle" before.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1421)
- [ ] Approved in principle
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

# FS-1150n Type-directed resolution of string literals
The design suggestion [#1421](https://github.com/fsharp/fslang-suggestions/issues/1421) was marked "approved in principle" before.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1421)
- [ ] Approved in principle
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

# FS-1150o Extending B-suffix string literals to be UTF-8 strings
The design suggestion [#1421](https://github.com/fsharp/fslang-suggestions/issues/1421) was marked "approved in principle" before.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1421)
- [ ] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

Currently, B-suffix string literals in F# only allow ASCII values. As UTF-8 is the modern standard of text communication, it makes little sense to continue limiting B-suffix string literals to ASCII values only. It will be changed to accept any well-formed UTF-8 string, i.e. a string literal as modified above but only targetting `byte array`.

```fs
let a = "你好"B
```

# FS-1150p Type-directed resolution of string patterns

With type-directed resolution of string construction, it also makes sense to change string deconstruction to be type-directed too.

If the inferred type is not a `string`, the string pattern would be a shorthand for the list pattern to match `char`s or `byte`s.

```fs
match [97; 98; 99]: ReadOnlySpan<byte> with
| "abc" -> printfn "It matches"
| _ -> failwith "Won't reach here"
```

This pattern is not customizable, use an active pattern instead for customizing this behaviour.

This subsumes suggestion [#1351](https://github.com/fsharp/fslang-suggestions/issues/1351).

# FS-1150q Type-directed resolution of boolean literals and patterns

For uniformity with numeric, char, tuple, list and string literals, it also makes sense for boolean literals to undergo similar type-directed resolution.

```fs
open System
let nil = Nullable<bool>()
let a = [true; nil; false]
```

