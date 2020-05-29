# F# RFC FS-1086 - Public [\<Literal>]s as static members

The design suggestion https://github.com/fsharp/fslang-suggestions/issues/746 has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] Approved in principle
- [ ] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/746)
- [ ] [RFC Discussion](https://github.com/fsharp/fslang-design/issues/469)
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)

# Summary

Allow defining a public `[<Literal>]` as a `static member`.

# Motivation

Currently, [`let` bindings in types must be private and the `[<Literal>]` attribute can't be applied to members.](https://stackoverflow.com/questions/1834923/f-public-literal) This results in the inability to define C# `public const`s in types other than `module`s. This proposal lifts this restriction.

# Detailed design

The following code will be compilable:

```fsharp
type Class() =
    [<Literal>]
    static member PublicLiteral = 1
type [<Struct>] Struct =
    [<Literal>]
    static member PublicLiteral = 2
type Record = { Field:int } with
    [<Literal>]
    static member PublicLiteral = 3
type DU = DU of int with
    [<Literal>]
    static member PublicLiteral = 4
```

Each of the `static member`s with `[<Literal>]`s will be compiled to the equivalent of `public const` in C#.

As with `let` `[<Literal>]`s, attempting to define non-constants as `static member` `[<Literal>]`s will result in `error FS0267: This is not a valid constant expression or custom attribute value`.

# Drawbacks

As public `[<Literal>]`s are already definable as `let` `[<Literal>]` in `module`s, it can be argued that this proposal gives multiple ways to achieve the same thing. However, lifting the restriction on `static member` `[<Literal>]`s results in better scoping, as `[<Literal>]` declarations will be able to exist anywhere that a C# `public const` can.

# Alternatives

The alternative is not implementing this suggestion. [Confusion on why F# cannot define C# `public const`s will continue to exist.](https://stackoverflow.com/questions/1834923/f-public-literal#comment1839525_1834926)

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change? No
* What happens when previous versions of the F# compiler encounter this design addition as source code? `error FS0842: This attribute is not valid for use on this language element`
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries? The same behaviour as encountering C# `public const`s.
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct? The same behaviour as encountering C# `public const`s.


# Unresolved questions

None.
