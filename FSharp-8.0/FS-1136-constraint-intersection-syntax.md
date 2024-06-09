# F# RFC FS-1136 - Constraint Intersection Syntax

[The design suggestion](https://github.com/fsharp/fslang-suggestions/issues/1262) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1262)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/15413)
- [ ] Design Review Meeting(s) with @dsyme and others invitees
- [ ] Discussion

# Summary

We introduce a limited form of intersection type as a succinct means of defining multiple subsumption constraints on type parameters. 

# Motivation

The current way of defining multiple subsumptions looks like this:

```fsharp
type Component<'serviceA, 'serviceB when 'serviceA :> IDisposable and 'serviceA :> ISomeInterface and 'serviceB :> IDisposable> = class end
```

The verbosity caused by the repetition of type parameter names, especially when one would like the latter to be descriptive, quickly gets out of hand. Whenever there is a need to define several such constraints for one or more type parameters, this syntax becomes unwieldy.

# Detailed design

The proposed way of defining subsumption constraints is an *intersection* of flexible types—a list of flexible types, separated by the `&` token—immediately following each type parameter, that is to say, not after the type parameter list and the `when` keyword. The type parameter name may be omitted outside of an explicit type parameter definition within angle brackets.

The following snippet lists examples of the current and proposed ways of using subsumption constraints across several constructs:

```fsharp
let f1old (x: 't when 't :> IDisposable and 't :> int seq) = ()
let f1new1 (x: 't & #IDisposable & #(int seq)) = () 
// ` 't & ` may be omitted when they do not need to refer back to the type parameter
let f1new2 (x: #IDisposable & #(int seq)) = () 

let computeOld<'n, 'o when 'n :> INumber<'n> and 'o :> 'n seq> (x: 'n, other: 'o) = ()
let computeNew<'n & #INumber<'n>, 'o & #('n seq)> (x: 'n, others: 'o) = ()

type MyClassOld<'t when 't :> string seq and 't :> IDisposable> = class end
type MyClassNew<'t & #(string seq) & #IDisposable> = class end

type MyClass =
    member _.Old1 (x: 't when 't :> IDisposable and 't :> int seq) = ()
    member _.New1 (x: #IDisposable & #(int seq)) = ()

    member _.Old2<'t when 't :> IDisposable and 't :> int seq> (x: 't) = ()
    member _.New2<'t & #IDisposable & #(int seq)> (x: 't) = ()

type MyInterface =
    abstract fOld1: 't -> unit when 't :> IDisposable and 't :> int seq
    abstract fNew1: #IDisposable & #(int seq) -> unit

    abstract fOld2<'t when 't :> IDisposable and 't :> int seq> : 't -> unit
    abstract fNew2<'t & #IDisposable & #(int seq)> : 't -> unit

// See unresolved questions
let f2old<'t when 't :> IDisposable and 't :> MyInterface> () = ResizeArray<'t> ()
let f2new () = ResizeArray<#IDisposable & #MyInterface> ()
```

It is also required that constraint intersection be combinable with `when`-style constraints:

```fsharp
type MyClass<'t & #seq<int> & #IDisposable when 't: null> = class end
```

Usage of a non-flexible type should result in an error. Usage of a sealed type should result in a warning or an error.

# Drawbacks

We already have a standard way of defining subsumption constraints. Other types of constraints cannot be expressed using intersections.

Users could expect full-fledged [intersection type support](https://github.com/fsharp/fslang-suggestions/issues/600), which is currently not planned.

# Alternatives

* What other designs have been considered?

  Using the `and` keyword for intersections.

* What is the impact of not doing this?

  Expressing complex constraints will continue to require more verbosity.

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change?

  No.

* What happens when previous versions of the F# compiler encounter this design addition as source code?

  Compilation failure.

* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?

  Code compiles fine. The binary output does not change.

* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?

  N/A.

# Unresolved questions

Should we allow intersections in type application positions and other places, where constraints and anonymous type variables are not currently accepted?

```fsharp
let f () = ResizeArray<#IDisposable & #MyInterface> ()

type U<'t> =
    | Value of 't & #IDisposable
```

This comes for free when intersections are part of the `appType` grammar. Moving them to `typeWithTypeConstraints` in order to disallow the type application above would also deprive us of intersections under the `topType` grammar, typically encountered in abstract members:

```fsharp
type I =
    abstract g1: #IDisposable & #seq<int> -> unit

    // However, we could still use a bit more verbose variation...
    abstract g2<'t & #IDisposable & #seq<int>> : 't -> unit

    // ...which is nonetheless slightly better than what is required today.
    abstract g3: 't -> unit when 't :> IDisposable and 't :> seq<int>
    abstract g4<'t when 't :> IDisposable and 't :> seq<int>> : 't -> unit
```