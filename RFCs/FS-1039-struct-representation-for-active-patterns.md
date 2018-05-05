# F# RFC FS-1039 - Struct options and struct representations for active patterns and optional arguments

The design suggestion [Struct representation for active patterns](https://github.com/fsharp/fslang-suggestions/issues/612) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/612)
* [ ] Details: [under discussion](https://github.com/fsharp/FSharpLangDesign/issues/230)
* [ ] Implementation: not started


# Summary
[summary]: #summary

This RFC covers several things:
1. FSharp.Core should have an unboxed (struct) version of the `option` type, e.g. `ValueOption` and `voption`
2. FSharp.Core should have unboxed (struct) versions of the `Choice` types
3. It should be possible to compile partial `(|A|_|)` active patterns to use struct options
4. It should be possible to compile total `(|A|B|)` active patterns to use struct choices
5. It should be possible to use struct options in optional parameters, e.g. `[<Struct>] ?x : int` or `??x : int` or...
6. FSharp.Core should contain a `ValueOption` module of operators
7. FSharp.Core should contain `List/Seq/Array/Map.tryv*` operations which take/produce unboxed options

Parts (2) and (4) are optional and could be done later (parts 2 and 4), as could parts (6) and (7).  As a result, for the most part this RFC will focus initially on parts (1), (3) and (5).

# Motivation
[motivation]: #motivation

Powerful and extensible features such as active patterns and optional arguments should be near-zero-cost abstractions. 

We should be able to providing better performance just via a simple attribute addition!

# Detailed design
[design]: #detailed-design


**1. FSharp.Core should have an unboxed (struct) version of the `option` type, e.g. `ValueOption` and `voption`**

See discussion here: https://github.com/fsharp/fslang-design/issues/230#issuecomment-386806657

The current proposal is 

```
    [<Struct>]
    type ValueOption<'T> =
        | VNone
        | VSome of 'T
    and 'T voption = ValueOption<'T>
```
though alternatives are discussed.

**2. FSharp.Core should have unboxed (struct) versions of the `Choice` types

TBD

**3. It should be possible to compile partial `(|A|_|)` active patterns to use struct options**

**4. It should be possible to compile total `(|A|B|)` active patterns to use struct choices**

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
[<Struct>]
let (|Even|Odd|) n =
    if n % 2 = 0 then
        Even
    else
        Odd
```

It should be compiled as function that returns struct version of `FSharpChoice<Unit, Unit>` union. In F#4.1 it's possible to define struct discriminated unions, so we can avoid extra allocations.

The same for partial active patterns.

```fsharp
let (|Int|_|) str =
   match System.Int32.TryParse(str) with
   | (true,int) -> Some(int)
   | _ -> None
```

```fsharp
[<Struct>]
let (|Int|_|) str =
   match System.Int32.TryParse(str) with
   | (true,int) -> StructSome(int)
   | _ -> StructNone
```

You might to note in struct version different names of `Some`/`None` cases are used. This is because of we need to distinguish between struct and non-struct versions of the `option` type.

Сonsequently we need to make this changes in compiler and FSharp.Core:

- add new struct versions for `FSharpOption` and `FSharpChoice` types
- allow `StructAttribute` on active patterns
- change codegen

**5. It should be possible to use struct options in optional parameters

TBD. This is tricky partly because of the problem of finding a good signature syntax, e.g. for boxed options we use:

    static member M(?x) = 
        let x = defaultArg x 5
        string x

and in signatures:

    static member M : ?x : int -> string

Today, the option type for argument `x` is applied immediately that `?x` is seen. This means we need some kind of syntax at `?x` to stop this happening, e.g. the somewhat unfortunate:

    static member M([<Struct>] ?x) = 
        let x = defaultvArg x 5
        string x

Likewise in a signature:

    static member M : [<Struct>] ?x : int -> string

Alternatively:

    static member M(struct ?x) = 
        let x = defaultvArg x 5
        string x

Likewise in a signature:

    static member M : struct ?x : int -> string

though in both cases it's not at all clear that `struct` is sufficiently disambiguated from its use as a type (I think it is not).


# Drawbacks
[drawbacks]: #drawbacks

- More tricks for F# programmers to learn

# Alternatives
[alternatives]: #alternatives

- Don't do any of this
- The "choice" and total-active-pattern parts of this are optional (parts 2 and 4)
- Require programmers to code complex matching by hands without expressiveness of active patterns
- Provide better inlining and optimization for active patterns. It can be _hard_ to achieve.

# Compatibility
[compatibility]: #compatibility

It's not breaking change due to it doesn't require new syntax at all, just addition to FSharp.Core and changes in codegen

# Unresolved questions
[unresolved]: #unresolved-questions

- We need good names for struct versions of `FSharpOptions` and `FSharpChoice`. Same for `Some`/`None` cases
- Shouldn't we add utility functions for struct options in FSharp.Core? Like `StructOption` module with `bind` and `map`
- Can we consider changing defaults and omitting `StructAttribute` for future major version of F#?
