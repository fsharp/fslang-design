# F# RFC FS-1144 - Empty-bodied computation expressions

The design suggestion [Allow empty CE body and return `Zero`](https://github.com/fsharp/fslang-suggestions/issues/1232) has been marked "approved in principle."

This RFC covers the detailed proposal for this suggestion.

- [x] [Allow empty CE body and return `Zero`](https://github.com/fsharp/fslang-suggestions/issues/1232)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/17352)
- [x] [Discussion](https://github.com/fsharp/fslang-design/discussions/775)

# Summary

We add compiler support for using a [computation expression](https://learn.microsoft.com/dotnet/fsharp/language-reference/computation-expressions) with an empty body to represent a computation or data structure's zero, empty, or identity value.

```fsharp
let xs = seq { } // Empty sequence.
```

```fsharp
let html =
    div {
        p { "Some content." }
        p { } // Empty <p> element.
    }
```

# Motivation

<!-- Why are we doing this? What use cases does it support? What is the expected outcome? -->

F# computation expressions, whether built into FSharp.Core or user-defined, are a useful tool for declaratively describing computations or the construction and composition of a variety of data structures.

Many well-known computations or data structures for which computation expressions are used have an empty or zero value — for example, a [monoid](https://en.wikipedia.org/wiki/Monoid#Definition) like `'a seq` has an [identity](https://en.wikipedia.org/wiki/Identity_element) element, i.e., the empty sequence.

Computation expressions are also widely used in the creation of domain-specific languages (DSLs) that enable describing complex domains with concise, declarative code. It is common in user interface libraries, for example, to use a computation expression to represent an HTML element or a component in a mobile application UI framework.

Many of these data structures — like an HTML `div` or `p` element — again have a logical zero or empty value.

It is not currently possible to represent this empty, zero, or identity value in the natural, obvious way.

#### The natural representation of the zero or empty value does not compile

```fsharp
seq { }
----^^^

stdin(1,5): error FS0789: '{ }' is not a valid expression. Records must include at least one field. Empty sequences are specified by using Seq.empty or an empty list '[]'.
```

```fsharp
div { }
^^^

stdin(1,1): error FS0003: This value is not a function and cannot be applied.
```

#### The current way to represent the zero or empty value is non-obvious

```fsharp
seq { () }
```

```fsharp
div { () }
```

This is in contrast to list and array comprehension expressions, which, though they share many similarities with sequence expressions and regular computation expressions, _do_ allow the representation of the empty or identity value by omitting a body altogether.

#### Empty list and array expressions are allowed

```fsharp
[]
```

```fsharp
[||]
```

# Detailed design

<!-- This is the bulk of the RFC. Explain the design in enough detail for somebody familiar
with the language to understand, and for somebody familiar with the compiler to implement.
This should get into specifics and corner-cases, and include examples of how the feature is used.
 -->

We do not, strictly speaking, change the syntax of computation expressions, as defined in [§ 6.3.10](https://fsharp.org/specs/language-spec/4.1/FSharpSpec-4.1-latest.pdf#page=67) of the F# language specification. That is, we do not change the behavior of the parser.

We instead update the typechecker to detect and rewrite `builder { }` as `builder { () }`, which already typechecks and results in the desired generated code. We do this by detecting the syntactic application of a value to a fieldless `SynExpr.Record` and rewriting it during typechecking to the application of a value to a `SynExpr.ComputationExpr` with a synthetic `unit` body.

## Before

A computation expression with an empty body, like

```fsharp
builder { }
```

is currently parsed and represented in the untyped abstract syntax tree (AST) as an application of a value to a fieldless (`recordFields=[]`) `SynExpr.Record`:

```fsharp
SynExpr.App
    (ExprAtomicFlag.NonAtomic,
     false,
     SynExpr.Ident (Ident ("builder", m)),
     SynExpr.Record (None, None, [], m),
     m)
```

For comparison, a computation expression with a non-empty body, like

```fsharp
builder { () }
```

is parsed instead as an application of a value to a `SynExpr.ComputationExpr`:

```fsharp
SynExpr.App
    (ExprAtomicFlag.NonAtomic,
     false,
     SynExpr.Ident (Ident ("builder", m)),
     SynExpr.ComputationExpr (false, SynExpr.Const (SynConst.Unit, m), m),
     m)
```

The application to `SynExpr.ComputationExpr` passes typechecking because `SynExpr.ComputationExpr` [is special-cased and skipped during the propagation of type information from delayed applications](https://github.com/dotnet/fsharp/blob/075e8842142f9a58e6776eefce0073be5b70cdad/src/Compiler/Checking/CheckExpressions.fs#L8187-L8190).

The application to a fieldless `SynExpr.Record` does _not_ pass typechecking because [it is not special-cased and thus not skipped](https://github.com/dotnet/fsharp/blob/075e8842142f9a58e6776eefce0073be5b70cdad/src/Compiler/Checking/CheckExpressions.fs#L8233-L8237). This means that the compiler attempts to typecheck the fieldless record syntax `{ }` as a record construction expression, which [fails](https://github.com/dotnet/fsharp/blob/075e8842142f9a58e6776eefce0073be5b70cdad/src/Compiler/Checking/CheckExpressions.fs#L7723-L7725), since fieldless records are not allowed.

## After

The parsing of a computation expression with an empty body, like

```fsharp
builder { }
```

remains unchanged.

We instead update the typechecking logic as follows.

### Skip typechecking of `{ }` in argument position during propagation

Just as checking of the computation expression body is [already skipped](https://github.com/dotnet/fsharp/blob/075e8842142f9a58e6776eefce0073be5b70cdad/src/Compiler/Checking/CheckExpressions.fs#L8187-L8190) for `SynExpr.ComputationExpr` during the propagation of type information from delayed applications, we now do the same for `SynExpr.Record (None, None, [], _)`, i.e., `{ }`, when it is the object of an application expression.

### Rewrite `SynExpr.Record` to `SynExpr.ComputationExpr`

Later, when typechecking the application itself, we may now detect the following syntax, representing the original `builder { }`

```fsharp
SynExpr.App
    (ExprAtomicFlag.NonAtomic,
     false,
     SynExpr.Ident (Ident ("builder", m)),
     SynExpr.Record (None, None, [], m),
     m)
```

We then rewrite it to

```fsharp
SynExpr.App
    (ExprAtomicFlag.NonAtomic,
     false,
     SynExpr.Ident (Ident ("builder", m)),
     SynExpr.ComputationExpr (false, SynExpr.Const (SynConst.Unit, range0), m),
     m)
```

— equivalent to `builder { () }` — before we continue typechecking.

(Note that in the special case when `builder` is `seq` from FSharp.Core, we additionaly set `hasSeqBuilder=true` in the `SynExpr.ComputationExpr` case constructor, i.e., `SynExpr.ComputationExpr (true, SynExpr.Const (SynConst.Unit, range0), m)`.)

That is, it is now the case that `builder { }` ≡ `builder { () }`. The typechecking of `builder { () }` already results in a call to the builder's `Zero` method, which is the desired behavior for the new syntax.

We mark the inserted `()` (`SynExpr.Const (SynConst.Unit, range0)`) as synthetic by the use of `range0`, indicating that the construct is compiler-generated and does not come from the original source code.

### Use presence or absence of synthetic `()` to choose appropriate error message

We can later use this synthetic marker during the typechecking of the computation expression body to differentiate between a user-supplied and synthetic `()`.

When the computation expression body includes a `unit`-typed value, that value is user-supplied (not marked as synthetic), and the builder type has no `Zero` method, we continue to emit the existing error diagnostic:

```
error FS0708: This control construct may only be used if the computation expression builder defines a 'Zero' method
```

When the builder type has no `Zero` method and we instead detect the single synthetic `()` that we have inserted, we can emit a new error message specific to the new syntax:

```
error FS0708: An empty body may only be used if the computation expression builder defines a 'Zero' method.
```

# Drawbacks

<!-- Why should we *not* do this? -->

## Change in interpretation of incomplete code

There are two main ways in which the following F# syntax could be interpreted:

```fsharp
expr { }
```

1. The application of a computation expression builder value to an empty computation expression body.
2. The application of a function to an incomplete record construction expression.

These interpretations are perhaps equally likely _a priori_ from the user's point of view — i.e., as the user enters source code, it is equally as likely that the user has (2) in mind as (1). The code is incomplete under either interpretation according to the current rules, since neither empty-bodied computation expressions nor empty records are allowed.

As mentioned earlier in this document, however, the parser currently treats and will continue to treat this syntax as (2).

By updating the typechecker to rewrite (2) as (1), we are favoring an interpretation at odds with the parser (and, technically, the language specification). This could _in theory_ lead to a confusing error message if the compiler assumes interpretation (1) and the user assumes interpretation (2), or vice versa.

Specifically, the compiler would previously always attempt to typecheck `expr` in `expr { }` as a function value; if it could not be typechecked as a function value, the compiler, following interpretation (2), would emit:

```fsharp
expr { }
^^^^

stdin(1,1): error FS0003: This value is not a function and cannot be applied.
```

It did not matter whether `expr` was actually a computation expression builder value, or, if it was, which methods the builder type exposed — the compiler never even tried to typecheck it as one.

After this change, the only scenario with differing behavior is when `expr` cannot be typechecked as a function value (its type is not a function type or a type variable).

Before, this simply resulted in the message that `expr` is not a function and cannot be applied. Now, it will result in compilation as a computation expression if `expr` is a computation expression builder value whose type has a `Zero` method. If the type of `expr` does not have a `Zero` method, the new error message indicating that a `Zero` method is required for empty-bodied computation expressions will be emitted.

If `expr`'s type _is_ a function type, or _could be_ a function type (because it is a type variable), there is no change in behavior.

That is, we are now favoring interpretation (1) when `expr` is definitely not a function value. Even without the addition of empty-bodied computation expression support, it could be argued that an incomplete computation expression is more likely to be the user's intent in this scenario than the application of a non-function to an incomplete record construction expression.

It seems like this drawback is not actually a drawback in practice.

## Lack of generalizability

The original language suggestion for this feature notes that, while the value that a type function like `Seq.empty<'T>` produces is generalizable, the value produced by `seq { () }` (or any other builder) is not.

#### Allowed

```fsharp
let xs = Seq.empty                // 'a seq
let ys = xs |> Seq.map ((+) 1)    // int seq
let zs = xs |> Seq.map ((+) 1.0)  // float seq
```

#### Not allowed

```fsharp
let xs = seq { () }               // int seq
let ys = xs |> Seq.map ((+) 1)    // int seq
let zs = xs |> Seq.map ((+) 1.0)  // Doesn't work.
----------------------------^^^

stdin(3,29): error FS0001: The type 'int' does not match the type 'float'
```

Since we propose in this RFC that we compile `seq { }` as `seq { () }`, this means that the values produced by empty-bodied computation expressions will also not be generalizable.

It seems reasonable not to make `builder { }` generalizable for several reasons:

1. Not all computation expressions are generic.
2. Generalizability of computation expression values would be a new feature altogether. It seems like it could only apply to computation expressions with empty bodies. Would it also be applied to `builder { () }`? See (4) below.
3. There is currently no way to make a builder's `Zero` method (whether wrapped in `Delay` and/or `Run` or not) a generalizable value, even if `Zero` is made generic, because `Zero` must be a method, while `GeneralizableValueAttribute` [only works on type functions](https://github.com/dotnet/fsharp/blob/db21cf2597d00dac2b50e0f1ffaa480c8b32283f/src/Compiler/Checking/CheckExpressions.fs#L2071-L2072).
4. `builder { () }` and `builder { printfn "This is a side effect." }` are indistinguishable from the type system's perspective. It is surely undesirable to make the latter, side-effecting expression generalizable (although see discussion [here](https://github.com/fsharp/fslang-suggestions/issues/602) and [here](https://github.com/fsharp/fslang-design/blob/12be3195c8dc6fe1e905bf467743bcad747929fe/archived/FS-1038-evaluate-generalizable-values-once.md)), and having the resulting value be generalizable or not dependent on the purity of the `unit`-valued body would be untenable. This means that we would need to treat an empty body altogether differently from a `unit`-valued body. Even then, in order to produce a generalizable value, we would need to devise some mechanism whereby the user could annotate some value as the one to be used in this scenario (cf. the approach taken in C# with `CollectionBuilderAttribute`), the compiler could rewrite the original expression to use that instead, and so on. But this would add significant complexity for what seems like little gain.

# Alternatives

<!-- What other designs have been considered? What is the impact of not doing this? -->

## Update the parser

Update the parser to parse

```fsharp
builder { }
```

directly as

```fsharp
SynExpr.App
    (ExprAtomicFlag.NonAtomic,
      false,
      SynExpr.Ident (Ident ("builder", m)),
      SynExpr.ComputationExpr (false, SynExpr.Const (SynConst.Unit, range0), m),
      m)
```

### Pros

* No need to make any changes to typechecking.

### Cons

* The AST no longer represents what the user wrote. That is, the user wrote `builder { }`, not `builder { () }`, but the distinction between these is no longer possible to represent in the AST.
* It may be difficult to foresee [all potential effects on and interactions with existing parsing behavior](https://github.com/dotnet/fsharp/issues/16448). At a glance, the parsing of expressions involving curly braces `{ … }` is already rather complex and involves multiple layers of non-obvious fallthroughs, etc.

## Update the AST

In the untyped abstract syntax tree, update

```fsharp
SynExpr.ComputationExpr of hasSeqBuilder: bool * expr: SynExpr * range: range
```

to

```fsharp
SynExpr.ComputationExpr of hasSeqBuilder: bool * expr: SynExpr option * range: range
```

and update the parser to parse

```fsharp
builder { }
```

as

```fsharp
SynExpr.App
    (ExprAtomicFlag.NonAtomic,
      false,
      SynExpr.Ident (Ident ("builder", m)),
      SynExpr.ComputationExpr (false, None, m),
      m)
```

### Pros

* The AST actually more closely represents what the user wrote — _if_ their intent was to write the application of a computation expression builder value to an empty computation expression body. (See also cons below.)
* Minimal changes needed in the typechecker — just treat

  ```fsharp
  SynExpr.ComputationExpr (false, None, m)
  ```

  the same as

  ```fsharp
  SynExpr.ComputationExpr (false, Some (SynExpr.Const (SynConst.Unit, range0)), m)
  ```

### Cons

* In the absence of a body and additional type information, it is theoretically just as likely that the user is attempting to apply a function to an incomplete record construction expression. In this case, the AST now diverges from the user's intent.
* Both parsing and typechecking must be updated.
* Represents a breaking change to the untyped AST.
* As in the previous alternative, there is risk and complexity in making changes to the parser's treatment of curly-braced expressions.

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change?
  * No.
* What happens when previous versions of the F# compiler encounter this design addition as source code?
  * One of two compilation errors is produced — one for `seq` expressions and another for custom builders, including `async`:
    ```fsharp
    seq { }
    ----^^^

    stdin(1,5): error FS0789: '{ }' is not a valid expression. Records must include at least one field. Empty sequences are specified by using Seq.empty or an empty list '[]'.
    ```

    ```fsharp
    async { }
    ^^^^^

    stdin(1,1): error FS0003: This value is not a function and cannot be applied.
    ```
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
  * Older compiler versions will be able to consume the compiled result of this feature without issue.
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?
  * N/A.
* There is an existing compiler diagnostic used for `{ }` and `seq { }`, namely
  
  ```
  error FS0789: '{{ }}' is not a valid expression. Records must include at least one field. Empty sequences are specified by using Seq.empty or an empty list '[]'.
  ```
  
  This will no longer apply to `seq { }` when targeting newer language versions, but the message must remain the same when using newer versions of the compiler to target older language versions.
  
  The message remains applicable to future language versions, since someone may still try to use bare `{ }` to represent an empty sequence.
  
  We could add an augmented version that mentioned the now-valid `seq { }` — perhaps
  
  ```
  error FS0789: '{{ }}' is not a valid expression. Records must include at least one field. Empty sequences are specified by using 'Seq.empty', 'seq { }', or an empty list '[]'.
  ```

  This does not seem particularly necessary, however.

# Pragmatics

## Diagnostics

<!-- Please list the reasonable expectations for diagnostics for misuse of this feature. -->

The compiler should emit an error when a computation expression has an empty body and no intrinsic or extension method `member Zero : unit -> M<'T>` on the builder type is in scope.

We reuse the existing error diagnostic ID FS0708, whose [current message](https://github.com/dotnet/fsharp/blob/075e8842142f9a58e6776eefce0073be5b70cdad/src/Compiler/FSComp.txt#L563) is:

```
error FS0708: This control construct may only be used if the computation expression builder defines a '%s' method
```

Since, however, there is no user-visible "control construct" in the new syntax in such a scenario, we add the following message variant for clarity:

```
error FS0708: An empty body may only be used if the computation expression builder defines a 'Zero' method.
```

#### New error message for `builder { }` when the builder type has no `Zero`

```fsharp
type Builder () =
    member _.Delay f = f
    member _.Run f = f ()

let builder = Builder ()

let xs : int seq = builder { }
-------------------^^^^^^^^^^^

stdin(8,20): error FS0708: An empty body may only be used if the computation expression builder defines a 'Zero' method.
```

We continue to emit the older message when the user _does_ supply a body, e.g.,

#### Same error message for `builder { () }` when the builder type has no `Zero`

```fsharp
type Builder () =
    member _.Delay f = f
    member _.Run f = f ()

let builder = Builder ()

let xs : int seq = builder { () }
-----------------------------^^

stdin(8,30): error FS0708: This control construct may only be used if the computation expression builder defines a 'Zero' method
```

## Tooling

Please list the reasonable expectations for tooling for this feature, including any of these:

* Debugging
  * Breakpoints/stepping
    * N/A.
  * Expression evaluator
    * N/A.
  * Data displays for locals and hover tips
    * N/A.
* Auto-complete
  * N/A.
* Tooltips
  * N/A.
* Navigation and go-to-definition
  * N/A.
* Error recovery (wrong, incomplete code)
  * N/A.
* Colorization
  * N/A.
* Brace/parenthesis matching
  * N/A.

## Performance

<!-- Please list any notable concerns for impact on the performance of compilation and/or generated code -->

* For existing code
  * The addition of this feature will not affect the performance of the compiler on existing code.
* For the new feature
  * We do not foresee any performance issues with the updated typechecking logic required for this feature.

## Scaling

<!-- Please list the dimensions that describe the inputs for this new feature, e.g. "number of widgets" etc.  For each, estimate a reasonable upper bound for the expected size in human-written code and machine-generated code that the compiler will accept. -->

This feature's effect on compilation time and space complexity scales linearly with the number of instances of empty-bodied computation expressions in source code. There is no limit foreseen to the number of instances of empty-bodied computation expressions in source code that the compiler will accept.

## Culture-aware formatting/parsing

Does the proposed RFC interact with culture-aware formatting and parsing of numbers, dates and currencies? For example, if the RFC includes plaintext outputs, are these outputs specified to be culture-invariant or current-culture.

* No.

# Unresolved questions

None.
