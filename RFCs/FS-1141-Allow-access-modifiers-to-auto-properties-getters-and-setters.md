# F# RFC FS-1141 - Allow access modifiers to auto properties getters and setters

The design suggestion [Allow access modifies to auto properties getters and setters](https://github.com/fsharp/fslang-suggestions/issues/430) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/430)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/16687)
- [ ] Design Review Meeting(s) with @dsyme and others invitees
<!-- - [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN) -->

# Summary

Allow access modifiers to auto properties getters and setters.

# Motivation

To simplify the syntax of defining a private-settable-read-only properties.

# Detailed design

Allowing placing access modifiers before getter and setter of auto properties when no access modifier specified before the property name.

Syntax:

```fsharp
// Automatically implemented properties.
[ attributes ]
[ static ] member val [accessibility-modifier] PropertyName = initialization-expression [ with [accessibility-modifier] get, [accessibility-modifier] set ]
```

Example:

```fsharp
type A() =
    member val internal B = 0 with get, set
    // allow
    member val B2 = 0 with public get, private set
    // not allowed
    member val internal B3 = 0 with public get, private set

```

# Drawbacks

> A more way to do same thing.

# Alternatives

What other designs have been considered? What is the impact of not doing this?

> Write an explicit property with getter and setter.

```fsharp
type A() =
    [<DefaultValue>]
    val mutable private _B: int
    member this.B with public get () = this._B and private set value = this._B <- value
```

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change?

    > No

* What happens when previous versions of the F# compiler encounter this design addition as source code?

    > Build error

* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?

    > Unchanged, as explicit property works.

* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?

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

What parts of the design are still TBD?
