# F# RFC FS-1143 - Generic Attributes

NOTE: new sections have been added to this template! Please use this template rather than copying an existing RFC.

The design suggestion [Generic attributes (965)](https://github.com/fsharp/fslang-suggestions/issues/965) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/965)
- [x] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/17258)
- [ ] Design Review Meeting(s) with @dsyme and others invitees

# Summary

This language feature all type arguments in the usage of attributes, e.g. `[<SomeAttribute<int>>]`

# Motivation

1. Expanding the functionality of F# to ensure it more fully utilizes the features of the CIL, as well as maximizing interop with C#.
2. Enriching reflection information for better contextualization, and more ergonomic access of custom attributes.


# Detailed design

The storage of complex type information (via a System.Type) is currently only achievable through passing a `typeof<SomeType>` as a method argument to the attribute being used. By implementing generic attributes, the process of storing this information in attributes becomes much more ergonomic.

While this information is currently accessible by default for constant expressions/etc for enums, primitives, etc., the _only_ way to store Type information for things not currently permitted as attribute arguments is via a `typeof` call.

One of the greatest benefits is to the use of reflection:

```fsharp
[<AClassAttribute<SomeTypeA>>]
[<AClassAttribute<SomeTypeB>>]
type Example =
    class end

typeof<Example>.GetCustomAttributes(typeof<AClassAttribute<SomeTypeA>>)
//gives like [|AClassAttribute<SomeTypeA>|]
typeof<Example>.GetCustomAttributes(typeof<AClassAttribute<SomeTypeB>>)
//gives like [|AClassAttribute<SomeTypeB>|]
```

whereas currently, you might do something like:

```fsharp
[<AClassAttribute(typeof<SomeTypeA>)>]
[<AClassAttribute(typeof<SomeTypeB>)>]
type Example =
    class end

//assuming AClassAttribute has a property "_.TheType" where we store the single argument we pass above:
typeof<Example>.GetCustomAttributes(typeof<AClassAttribute>)
|> Array.filter (fun x -> (x :?> AClassAttribute |> _.TheType) = typeof<SomeTypeA>)
//gives like [|AClassAttribute|]
typeof<Example>.GetCustomAttributes(typeof<AClassAttribute>)
|> Array.filter (fun x -> (x :?> AClassAttribute |> _.TheType) = typeof<SomeTypeA>)
//gives like [|AClassAttribute|]
```

# Drawbacks

This feature will increase the complexity of attribute handling throughout the compiler. 

# Alternatives

While the benefit to reflection seems to have no alternative, the actual storage of type information in the instance of an attribute is currently achievable via passing a typeof<_> argument to the attribute ctor itself.

Given that the core purpose of this RFC is to increase both the ergonomics of F# and the interop between F# and C#, there is no real alternative on that end.

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change?
  * No
* What happens when previous versions of the F# compiler encounter this design addition as source code?
  * Unexpected postfix token error
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
  * Attributes that require type arguments would be unusable. Reflection should be unaffected (as the GetCustomAttributes functionality relies on type specs.)
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?
  * Not a change to Core.

# Pragmatics

## Diagnostics

Please list the reasonable expectations for diagnostics for misuse of this feature.

## Tooling

Please list the reasonable expectations for tooling for this feature, including any of these:

* Debugging
  * Breakpoints/stepping
  * Expression evaluator
  * Data displays for locals and hover tips
* Auto-complete
* Tooltips
* Navigation and Go To Definition
* Colorization
* Brace/parenthesis matching

## Performance

Please list any notable concerns for impact on the performance of compilation and/or generated code

* For existing code
* For the new features

## Scaling

Please list the dimensions that describe the inputs for this new feature, e.g. "number of widgets" etc.  For each, estimate a reasonable upper bound for the expected size in human-written code and machine-generated code that the compiler will accept.

For example

* Expected maximum number of widgets in reasonable hand-written code: 100
* Expected reasonable upper bound for number of widgets accepted: 500

Testing should particularly check that compilation is linear (or log-linear or similar) along these dimensions.  If quadratic or worse this should ideally be noted in the RFC.

## Culture-aware formatting/parsing

Does the proposed RFC interact with culture-aware formatting and parsing of numbers, dates and currencies? For example, if the RFC includes plaintext outputs, are these outputs specified to be culture-invariant or current-culture.

# Unresolved questions

In what instances will type inferencing be possible? Presumably, the following will be a (relatively) trivial case:

```fsharp
type MyAttribute<^T>(context : ^T)=
  inherit Attribute()

[<MyAttribute(24)>]
let x = 1
```

Though it may be less common, could we also support inferencing from the surrounding environment:

```fsharp
type MyOtherAttribute<^T>()=
  inherit Attribute()

type SomeClass<^T,^S>(arg1, arg2)=

  [<MyOtherAttribute<^T>>]
  member _.Field1 = arg1
  
  [<MyOtherAttribute<^S>>]
  member _.Field1 = arg1
```

This may be completely incongruent with how attributes are currently handled in F# (or dotnet entirely), but given that whenever we talk about `SomeClass` we would do so while also contextualizing `^T` and `^S`, it seems like something like this might be able to work.
