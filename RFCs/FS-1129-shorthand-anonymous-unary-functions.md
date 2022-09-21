# F# RFC FS-1129 - (`_.` shorthand for simple anonymous unary functions)

The design suggestion [Allow _.Property shorthand for accessor functions](https://github.com/fsharp/fslang-suggestions/issues/506) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/506)
- [x] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/13907)
- [ ] Design Review Meeting(s) with @dsyme and others invitees
- [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

# Summary

Add a syntactic sugar shorthand (`_.`) for simple functions consisting of accessors and atomic function application only.

```fsharp
_.Foo  => fun o -> o.Foo
```

# Motivation

It is fairly common to need to create function expressions which consist of a single argument and only "dot" operations upon it.

```fsharp
someList |> List.map (fun x -> x.ToString())
someList |> List.exists (fun x -> x.Contains "foo")
someList |> List.sortBy (fun x -> x.Bar)
```

These functions:

- Add a small amount of visual noise
- Add a small amount of cognitive overhead and maintenance burden on naming the function argument
- Have a small risk of leading to errors if the wrong ident is used on the rhs

The introduction of a dedicated syntax for these functions could improve the general experience when piping values.

Why are we doing this? What use cases does it support? What is the expected outcome?

# Detailed design

This is a syntactical transformation of the form:

```fsharp
_.Foo  => fun o -> o.Foo
```

And the following things should be valid:

```fsharp
_.Foo.Bar  => fun o -> o.Foo.Bar
_.Foo.[5]  => fun o -> o.Foo.[5]
_.Foo()    => fun o -> o.Foo()
_.Foo(5).X => fun o -> o.Foo(5).X
```

This transformation should take place after the SyntaxTree is created, during the checking of the expression.

Given an application expression where the first part of the long ident in the leading expression is an underscore, the elaborated form of `_.expr` should be `fun x -> x.expr` where `x` is a synthetic variable.

And allowing things like (_ + 5) is massively increasing the scope of this extension, without providing much benefit.

Or in other words: "_. binds only to dot-lookups and atomic function application."

We should consider the interaction with property extraction in pattern matching, e.g.

match x with
| _.Length as 0 -> ....


This is the bulk of the RFC. Explain the design in enough detail for somebody familiar
with the language to understand, and for somebody familiar with the compiler to implement.
This should get into specifics and corner-cases, and include examples of how the feature is used.

Example code:

```fsharp
let add x y = x + y
```

# Drawbacks

1. Additional complexity in the language and compiler.
2. It may lead to an expectation that similar shorthands are available in other contexts (such as in pattern matching).
3. It may lead to confusion as to the meaning of underscore (`_`).

# Alternatives

What other designs have been considered? What is the impact of not doing this?

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change?
* What happens when previous versions of the F# compiler encounter this design addition as source code?
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?

# Pragmatics

## Diagnostics

the use of this feature should really be disallowed or give a warning in cases such as [this]
(Allow _.Property shorthand for accessor functions #506 (comment)) e.g. at least if_ is used as a fun _-> ... wildcard pattern somewhere in the enclosing expression . I'm not sure what the exact limitation should be though, since you might reasonably use_ as a nested pattern in a match statement wildcard. But some kind of limitation would seem sensible.

```fsharp
let h : string array array -> (string -> int) array = Array.map (fun _->_.Length) // return tuple from array length and function that ignores argument and returns array length
```

Please list the reasonable expectations for diagnostics for misuse of this feature.

## Tooling

### Debugging

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
