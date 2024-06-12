# F# RFC FS-1129 - (`_.` shorthand for simple anonymous unary functions)

The design suggestion [Allow _.Property shorthand for accessor functions](https://github.com/fsharp/fslang-suggestions/issues/506) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/506)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/13907)
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
_.Foo.[5]  => fun o -> o.Foo[5]
_.Foo()    => fun o -> o.Foo()
_.Foo(5).X => fun o -> o.Foo(5).X
```

This transformation should take place after the SyntaxTree is created, during the checking of the expression.

Given an application expression where the first part of the long ident in the leading expression is an underscore, the elaborated form of `_.expr` should be `fun x -> x.expr` where `x` is a synthetic variable.

Or in other words: "`_.` binds only to dot-lookups and atomic function application."

> We should consider the interaction with property extraction in pattern matching, e.g.

> match x with
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

An alternative would be to extend the feature to allow anonymous functions with "captures" such as in [Clojure](https://clojure.org/guides/learn/functions#_anonymous_function_syntax) or [Elixir](https://hexdocs.pm/elixir/Kernel.SpecialForms.html#&/1-anonymous-functions), with something like:

```fsharp
&(& * 2) => (fun x -> x * 2)
```

TODO: why is this not worth it

What other designs have been considered? What is the impact of not doing this?

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change?
  * No
* What happens when previous versions of the F# compiler encounter this design addition as source code?
  * Parser errors
  * TODO: Check this would always be the case
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
  * TODO: investigate
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?

# Pragmatics

## Diagnostics

### Discard/wildcard confusion

It may be confusing to allow the use of this shorthand when a discard has already been used in the same context.

#### Wildcard `fun` binding

```fsharp
let a : string -> string = (fun _ -> 5 |> _.ToString())
```

The use of this shorthand inside a `fun` with a wildcard pattern should emit a warning.

#### Discard pattern

```fsharp
let b : int -> int -> string = function |5 -> (fun _ -> "Five") |_ -> _.ToString()
```

The use of this shorthand inside a pattern match with a discard should not warn.

#### Discarded local variable

```fsharp
let c : string = let _ = "test" in "asd" |> _.ToString()
```

TODO: Should this be OK?

Please list the reasonable expectations for diagnostics for misuse of this feature.

## Tooling

### Debugging

In summary, all language server features directed at the underscore should behave as if directed at the arg and/or reference of the equivalent elaborated form. All language features directed at anything after the dot should similarly behave as if directed at the body of the equivalent function.

* Debugging
  * Breakpoints/stepping
    * This should behave in the same way as the equivalent elaborated form: a breakpoint covering a line with the shorthand should break at least once each time the function is called. Similarly, stepping into the function should behave as if it had been written in the elaborated form.
  * Expression evaluator
    * TODO
  * Data displays for locals and hover tips
    * The debugger should show the value of the synthetic local variable storing the argument when stopped inside the function call
    * When inside the scope of multiple nested versions of this shorthand the variables should have unique (and somehow clear) names
    * The current synthetic variable value should be displayed when hovering over the underscore part of the shorthand.
      * Not any underscore, and the correct value for the correct function when nested
    * Other data displays and hover should be equivalent to the elaborated form
* Auto-complete
  * Completion after `_.` should be equivalent to completion after `(fun x -> x.` in the same context.
  * TODO: Check there aren't any snippets or other oddities
* Tooltips
  * On hovering over the underscore a tooltip should be shown the type of the anonymous function, and any other context that would normally be shown for a function arg.
  * TODO: Does the rhs `x` ever show something different to the lhs `x`?
* Navigation and Go To Definition
  * None? No document symbols are created by this construct.
* Colorization
  * TODO
* Brace/parenthesis matching
  * TODO

## Performance

TODO, but I can't see how this could cause any impact on the performance of generated code as the construct should be entirely erased.

This adds rules to the parser which could cause more backtracking.

This adds extra work between parsing and checking which requires walking the syntax tree.

## Scaling

Please list the dimensions that describe the inputs for this new feature, e.g. "number of widgets" etc.  For each, estimate a reasonable upper bound for the expected size in human-written code and machine-generated code that the compiler will accept.

For example

* Expected maximum number of widgets in reasonable hand-written code: 100
* Expected reasonable upper bound for number of widgets accepted: 500

Testing should particularly check that compilation is linear (or log-linear or similar) along these dimensions.  If quadratic or worse this should ideally be noted in the RFC.

## Culture-aware formatting/parsing

TODO, but no

Does the proposed RFC interact with culture-aware formatting and parsing of numbers, dates and currencies? For example, if the RFC includes plaintext outputs, are these outputs specified to be culture-invariant or current-culture.

# Unresolved questions

1. Is underscore definitely the appropriate symbol?
2. Is there a good name for this feature?
3. What happens when you nest this?
4. Are you allowed inline comments between `_` and `.` or between `.` and `expr`?
5. In what contexts should uses of discards/wildcards conflict and raise a warning?

What parts of the design are still TBD?
