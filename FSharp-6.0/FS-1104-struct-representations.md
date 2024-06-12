# F# RFC FS-1104 - Further struct representations 

The design suggestion [Struct representation for active patterns](https://github.com/fsharp/fslang-suggestions/issues/612) has been marked "approved in principle".

This RFC covers elaborations of the detailed proposal starting from this suggestion into 4 more parts.

# Summary
[summary]: #summary

This RFC covers 5 things:

1. It should be possible to use struct options in optional parameters, e.g. `[<Struct>] ?x : int` or `??x : int` or...
2. FSharp.Core should contain `List/Seq/Array/Map.tryv*` operations which take/produce unboxed options
3. FSharp.Core should have unboxed (struct) versions of the `Choice` types
4. It should be possible to compile total `(|A|B|)` active patterns to use struct choices

# Motivation

Powerful and extensible features such as active patterns and optional arguments should be near-zero-cost abstractions. 

We should be able to provide better performance just via a simple attribute addition!

# Detailed design

These are sequenced in expected order of being implemented.


**1. It should be possible to use struct options in optional parameters**

This is tricky partly because of the problem of finding a good signature syntax, e.g. for boxed options we use:
```fsharp
static member M(?x) = 
    let x = defaultArg x 5
    string x
```
and in signatures:

```fsharp
static member M : ?x : int -> string
```

Today, the option type for argument `x` is applied immediately that `?x` is seen. This means we need some kind of syntax at `?x` to stop this happening, e.g. the somewhat unfortunate:

```fsharp
static member M([<Struct>] ?x) = 
    let x = defaultvArg x 5
    string x
```

Likewise in a signature:

```fsharp
static member M : [<Struct>] ?x : int -> string
```

Alternatively:

```fsharp
static member M(struct ?x) = 
    let x = defaultvArg x 5
    string x
```

Likewise in a signature:

```fsharp
static member M : struct ?x : int -> string
```

though in both cases it's not at all clear that `struct` is sufficiently disambiguated from its use as a type (I think it is not).

**2. FSharp.Core should contain `List/Seq/Array/Map.tryv*` operations which take/produce unboxed options**

TBD, likely to be moved to a separate RFC, but naming should be considered here.

**3. FSharp.Core should have unboxed (struct) versions of the `Choice` types**

TBD. Naming would follow whatever is devised for struct options.

**4. It should be possible to compile total `(|A|B|)` active patterns to use struct choices**

Requires 4. TBD, likely to be moved to a separate RFC, but preliminary decisions should be considered here.

How to use:

```fsharp
let (|Even|Odd|) n =
    if n % 2 = 0 then
        Even
    else
        Odd
```

Put the `StructAttribute` on the active pattern definition.

```fsharp
[<return Struct>]
let (|Even|Odd|) n =
    if n % 2 = 0 then
        Even
    else
        Odd
```

It should be compiled as function that returns struct version of `FSharpChoice<Unit, Unit>` union. It's possible to define struct discriminated unions, so we can avoid extra allocations.

# Drawbacks

- More tricks for F# programmers to learn

# Alternatives

- Don't do any of this

- The "choice" and total-active-pattern parts of this are optional (parts 3 and 4)

- Require programmers to code complex matching by hands without expressiveness of active patterns

- Provide better inlining and optimization for active patterns. It can be _hard_ to achieve.

- Add a modality for "use structness for things declared in this scope", e.g.
  * use struct representations for return results for all active pattern declared in this scope
  * use struct representations for all syntactic tuples in this assembly in this scope
  * use struct representations for all optional arguments declared in this scope

# Compatibility

TBD

# Unresolved questions

TBD
