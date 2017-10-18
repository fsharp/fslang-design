# F# RFC FS-1039 - (Struct representation for active patterns)

The design suggestion [Struct representation for active patterns](https://github.com/fsharp/fslang-suggestions/issues/612) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/612)
* [ ] Details: [under discussion](https://github.com/fsharp/FSharpLangDesign/issues/FILL-ME-IN)
* [ ] Implementation: [In progress](https://github.com/Microsoft/visualfsharp/pull/FILL-ME-IN)


# Summary
[summary]: #summary

We should be able to compile active patterns using struct versions of `FSharpOption` and `FSharpChoice` unions.

# Motivation
[motivation]: #motivation

Such powerful and extensible feature is worthy to be near-zero-cost abstraction. We should be able to providing better performance just via a simple attribute addition!

# Detailed design
[design]: #detailed-design

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

# Drawbacks
[drawbacks]: #drawbacks

- It's one more trick for F# programmers to learn

# Alternatives
[alternatives]: #alternatives

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
