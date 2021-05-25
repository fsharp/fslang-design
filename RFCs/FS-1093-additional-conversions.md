# F# RFC FS-1093 - Additional type directed conversions

The design suggestion [Additional type directed conversions](https://github.com/fsharp/fslang-suggestions/issues/849) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/10884)
- [ ] Design Review Meeting(s) with @dsyme and others invitees
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/525)

# Summary

This RFC extends F# to include type-directed conversions when known type information is available. It does three things:

1. Puts in place a general backwards-compatible mechanism for type directed conversions (and one that works in conjunction with the existing techniques to allow subsumption at some specific places)

2. Selects a particular set of type directed conversions to use.  These are currently
   - the existing func --> delegate type directed conversions
   - the existing delegate --> LINQ Expression type directed conversions
   - upcasting
   - int32 --> int64/float32/float64
   - float32 --> float64 
   - op_Implicit when both source and destination are nominal.

3. Implements an opt-in warning when any of these are used (outside existing uses of upcasting)

Type-directed conversions are used as an option of last resort, at leaf expressions, so different types
may be returned on each bracnh of compound structures like `if .. then ... else`.

# Design Principles

The intent of this RFC is to give a user experience where:

1. Interop is easier (including interop with some F# libraries, not just C#)

2. You don't notice the feature and are barely even aware of its existence.

3. Fewer upcasts are needed when programming with types that support subtyping

4. Fewer widening conversions are needed when mixing int32, float32 and float64.

5. Numeric int64/float32/float64 data in tuple, list and array expressions looks nicer

6. Working with new numeric types such as System.Half whose design includes op_Implicit should be less irritating

7. Inadvertent use of the mechanism should not introduce confusion or bugs

NOTE: The aim is to make a feature which is trustworthy and barely noticed. It's not the sort of feature where you tell people "hey, go use this" - instead the aim is that you don't need to be cognisant of the feature when coding in F#, though you may end up using the mechanism when calling a library, or when coding with numeric data, or when using data supporting subtyping.  Technical knowledge of the feature is not intended to be part of the F# programmer's working knowledge, it's just something that makes using particular libraries nice and coding less irritating, without making it less safe.

There is no design goal to mimic all the numeric widenings of C#.

There is no design goal to eliminate all explicit upcasts.

There is no design goal to eliminate all explicit numeric widening.

There is no design goal to eliminate all explicit calls to `op_Implicit`, though in practice we'd expect `op_Implicit` calls to largely disappear.

# Motivation

There are a number of motivations both for the general mechanism and the specific type-directed conversions admitted.


### Motivation for general mechanism

The mechanism is required for type-directed features adding more implicit (i.e. auto-upcast) structural subtyping to F# such as [Anonymous Type-tagged Unions](https://github.com/fsharp/fslang-design/blob/master/RFCs/FS-1092-anonymous-type-tagged-unions.md).


### Motivation regarding explicit upcasts

Explicit upcasts are some times required in F# coding and this can be unexpected. For example, prior to this RFC this is allowed:

```fsharp
let a : obj list = [1; 2; 3] // ✅ This works
```

but this raised an error:

```fsharp
let a : int seq = [1; 2; 3] // ❌ error FS0001: This expression was expected to have type 'seq<int>' but here has type ''a list'    
let b : obj seq = [1; 2; 3] // ❌ error FS0001: This expression was expected to have type 'seq<int>' but here has type ''a list'    
```

Or, alternatively, at return position, consider this:

```fsharp
type A() = class end
type B() = inherit A()
type C() = inherit A()
let f () : A = if true then B() else C() // ❌ error FS0001: This expression was expected to have type 'A' but here has type 'B'
```

Or, alternatively, at constructions of data such as options, consider this:

```fsharp
type A() = class end
type B() = inherit A()
let f2 (x: A option) = ()

let (data: A option) = Some (B())  // ❌ error FS0001: This expression was expected to have type 'A' but here has type 'B'
f2 (Some (B())                     // ❌ error FS0001: This expression was expected to have type 'A' but here has type 'B'
```

Instead, in all cases, prior to this RFC type upcasts or `box` are needed:
```fsharp
let a : int seq = ([1; 2; 3] :> int seq)
let b : obj seq = ([box 1; box 2; box 3] :> int seq)
let f () : A = if true then (B() :> _) else (C() :>_)
let (data: A option) = Some (B() :> _)
f2 (Some (B() :> _)
```

The requirement to make upcasts is surprising and counter-intuitive (though comes with benefits see 'Drawbacks').

There are several existing techniques in the F# language to reduce the occurence of upcasts and to ensure resulting code is as general
as possible.  These include:

- Condensation and decondensation of function types each time a function or method value is used,
  e.g. given `let f (x: A) = ()` then when `f` is used as a first class value it's given type `f : 'T -> unit when 'T :> A`.

- Auto-introduction of conversions for elements of lists with a type annotation, e.g. `let a : obj list = [1; 2; 3]`

- Auto-introduction of conversions for assignments into mutable record fields and some other places with known type information.

### Motivation for int32 --> int64 type-directed conversion

The primary use case is using integer literals in int64 data.
APIs using 64-bit integers are very common in some domains.  For example, in a typical tensor library shapes are given using `int64[]`. So it is
frequent to write `[| 6L; 5L |]`.  There is a reasonable case to writing `[| 6; 5 |]` instead when the types are known.

Note a non-array-literal expression of type `int[]` still need to be explicitly converted to `int64[]`.

### Motivation for int32 --> single/double type-directed conversion

The primary use case is using integer literals in floating point data such as `[| 1.1; 3.4; 6; 7 |]` when the types are known.  

### Motivation for single --> double type-directed conversion

The motivations for this is fairly weak.  For example it allows floating point utility code (e.g. printing) to use 64-bit floating point
values, and yet be routinely usable with 32-bit values.  However a non-array-literal value of `single[]` still need to be converted to `double[]`.

### Motivation for op_Implicit type-directed conversion

* Certain newer .NET APIs, such as those in ASP.NET Core, make frequent use of `op_Implicit` conversions. For example,
  many APIs in `Microsoft.Net.Http.Headers` make use of `StringSegment` arguments, where C# devs can just pass a
  string, but F# devs must explicitly call op_Implicit all over the place

* Some popular 3rd party libraries also use `op_Implicit`. E.g. MassTransit uses `RequestTimeout`, which has an `op_Implicit` conversion from `TimeSpan` as well as `int` (milliseconds), seemingly for a simpler API with fewer overloads.

* One thing to watch out for is `op_Implicit` conversions to another type. E.g. `StringSegment` has conversions to `ReadOnlySpan<char>` and `ReadOnlyMemory<char>`. 

### Motivation for completing the matrix of integer widenings

If `op_Implicit` is accetped as a type-directed conversion then there is also an additional "consistency" motivation to include `int8` --> `int16` --> `int32` --> `int64` and similar widenings.  Specifically additional .NET numeric types such as `System.Half`, `System.Decimal` and `System.Complex` do allow certain implicit conversions via `op_Implicit`.  So if these types have widening from `int8`, `int16` and `int32` then why doesn't `System.Int64`? 

# Detailed design

The F# language specification defines the notion of the "overall type" when checking expressions (called the "known type" in the specification and "overall type"
in the implementation).  The easiest way to specify an overall type for an expression is through a type annotation:

```fsharp
let xs : A = ...
```

Here `A` is the known type of the expression on the right-hand side.

The overall type of an expression (as defined in the F# language specification)
is augmented to be annotated with a flag as to whether it is "must convert to" or "must equal". The "must convert to" flag is set for:

- The right-hand side of a binding 

- The body of a function or lambda

- Each expression used in argument position

- If an if-then-else or match expression has the flag, then the flag is set for each branch.

- If a let, let-rec, try-finally or try-catch has the flag, then the flag is set for the body.

The overall type is "propagated" either before or after the checking of the expression, depending
on the expression.  For the following, it is propagated before: 

* Array and list expressions, both fixed-size and computed
* `new ABC<_>(...)` expressions
* Object expressions `{ new ABC<_>(...) with ... }`
* Tuple, record and anonymous record expressions when the overall known type is also a tuple, record or anonymous record expression.

For other expressions, the overall type is propagated after checking.

When an overall type `overallTy` is propagated to an expresson with type `exprTy` and the "must convert to" flag is set, and
`overallTy` doesn't unify with `exprTy`, then a type-directed conversion is attempted by:

1. Trying to add a coercion constraint from `exprTy :> overallTy`. If this succeeds, a coercion operation is added to the elaborated expression.

2. Trying to convert a function type `exprTy` to a delegate type `overallTy` by the standard rules. If this succeeds, a delegate construction operation is added to the elaborated expression.

3. Trying the special conversions `int32` to `int64`, `int32` to `single`, `int32` to `double` from `exprTy` to `overallTy`.
   If this succeeds, a numeric conversion is added to the elaborated expression.

4. Searching for a matching `op_Implicit` method on either `exprTy` or `overallTy`. If this succeeds, a call to the `op_Implicit` is added to the elaborated expression.

If the "must convert to" flag is not set on `overallTy`, then unification between `overallTy` and `exprTy` occurs as previously.

> NOTE: There are some existing cases in the F# language specification where an inference variable and flexibility constraint
> is already added for a known type (specifically when checking an element of a list or array when the element type has
> been inferred to be nominal and supports subtyping). In these cases, the checking process is unchanged.

> NOTE: For list, array, `new`, object expressions, tuple, record and anonymous-record expressions, pre-checking integration
> allows the overall type to be propagated into the partially-known type (e.g. an
> list expression is known to have at least type `list<_>` for some element type), which in turn allows the
> inference of element types or type araguments.  This may be relevant to processing the contents of the expression.
> For example, consider
> 
>      let xs : seq<A> = [ B(); C() ]
>
> Here the list is initially known to have type `list<?>` for some unknown type, and the overall known type `seq<A>` 
> is then integrated with this type, inferring `?` to be `A`.  This is then used as the known overall type when checking
> the element expressions.

These conversions also apply for optional arguments, for example:

```fsharp
type C() = 
    static member M1(?x:int64) = 1
C.M1(x=2)
```


> NOTE: Branches of an if-then-else or `match` may have different types even when checked against the same known type, e.g. 
> 
> ```fsharp
> let f () : A = if true then B() else C()
> ```
> is elaborated to
> ```fsharp
> let f () : A = if true then (B() :> A) else (C() :> A)
> ```


### Overload resolution 

Overloads not making use of type-directed conversion are always preferred to overloads with type-directed conversion in overload resolution.

### Opt-in warning for type directed conversions (`--warnon:3386`)

Type-directed conversions can cause problems in understanding and debugging code.

As a result, an opt-in warning `--warnon:3386` (off by default) is available to report a warning whenever a type-directed conversion is used in code. A typical warning is as follows:

```
tests\fsharp\core\auto-widen\preview\test.fsx(169,18): warning FS3386: This expression uses an implicit conversion to convert type 'Y' to type 'X'. Warnings are enabled for implicit conversions. Consider using an explicit conversion or disabling this warning.
```

### No upcast without known type information

Consider this example:

```fsharp
type A() = class end
type B() = inherit A()
type C() = inherit A()

let Plot (elements: A list) = ()

[B(); C()] |> Plot // ❌ error FS0193: Type constraint mismatch. The type 'C' is not compatible with type 'B'
```

This RFC change will not address this example - the element type of the list is inferred to be `B`. This however works:

```fsharp
Plot [B(); C()]
```

APIs sensitive to this should consider avoiding the use of piping `|>` as a result, and
instead maximise the flow of type information from destination (here `Plot`) into checking of contents (here `[B(); C()]`).

# Drawbacks

### Expressions may change type when extracted

Despite appearances, the existing F# approach to type checking prior to this change has advantages:

1. When a sub-expression is extracted to a `let` binding for a value or function, its inferred type rarely changes (it may just become more general)

2. Information loss is made explicit in many important places

3. Adding type annotations to existing checked code using `: A` is "harmless" and rarely change elaboration

4. Implicit boxing (i.e. representation changes) may occur

Consider the following which now checks with this feature:
```fsharp
let f (x: (obj * obj) list ) = ()

let g() =
    f [ (1, "2"), (3, "4") ]
```
Note consider the following seemingly routine extraction:

```fsharp
let g() =
    let data1 = (1, "2")
    let data2 = (3, "4")
    f [ data1; data2 ] // ❌ error FS0001: Type mismatch. Expecting a 'obj * obj' but given a 'int * string'. The type 'obj' does not match the type 'int'.
```

Here, `data1` now needs a type annotation to maintain the same inferred type.  This matters because `data` is a tuple, and, in the absence of 
co-variance on tuples (which wouldn't apply to the `int --> obj` conversion in any case), would need to be unpackaged and repackaged to
get the correcct destination type of `obj * obj`.  Here is a working version with the type annotation:

```fsharp
let g() =
    let data1 : (obj * obj) = (1, "2")
    let data2 : (obj * obj) = (3, "4")
    f [ data1; data2 ] 
```

That is, certain transformations of unelaborated F# code are no longer valid without possibly including type annotations.  However type annotations
are, in practice, already needed when performing similar extractions in F#, e.g. to resolve dot-notation, so the above is not too surprising.


### 'box' needed in fewer places, 

This changes programming around the "obj" type, for example consider:

```fsharp
 let f () : obj = "abc"
```

Currently introducing `obj` requires an explicit `box` in the basic programming model except in the existing places where
auto-coercion/widening is available, such as method calls.

# Alternatives


#### Don't do this and maintain status quo

Enough said

#### Choices over which type-directed conversions

The choices of type-directed conversions are potentially controversial. In order of controversy we have:

    AutoCasting < Literal widening < Integer widening < Float widening < Int-to-Float widening < op_Implicit widening

#### Generic numbers by default

Some of the motivation for this RFC relates to numeric literals. The discussion has raised some possible alternative approaches to these.

@gusty says:

> it's better to implement generic numbers ...

@dsyme says: I understand this position, and we considered it for F# 1.0, though as I think we've discussed elsewhere I do not believe it's feasible to implement generic literals without making a significant breaking change - so I don't actually think it's a starter. The fact that `17` commits to type `int` immediately (if there is no known type) is very significant for a lot of F# code and there's no real way of escaping that.

> do `op_Implicit` separately

It seems likely that any discussion about `op_Implicit` and widening of integer types eventually iterates to "add additional type directed conversions for `op_Implicit` and numeric widenings".  Any proposed solution for these will later trend towards the use of this mechanism when further examples arise.  

### Generic numbers by opt-in

There is an alternative solution to generic literals which is to enhance the existing literal mechanism to allow the user to implement their own, namely:

* Allow the user opening a module NumericLiteralD (where D stands for default) which will be called when no suffix is used in number literals.
* Allow the user to define the default constraint, which will in anycase make the language more consistent.
* Introduce an optional method to interpret float-like literals, something like FromDecimal
* Implement this optimization https://github.com/fsharp/fslang-suggestions/issues/602#issuecomment-510754929

@gusty says:

> "Having this in place would result in a more consisting language, instead of adding another half-way feature, you complete an existing half-way feature and make it full usable. And now the problem of a specific library is solved by another library."

# Compatibility

This is not a breaking change. The elaboration of existing code that passes type checking is not changed.

This doesn't extend the F# metadata format.

There are no additions to FSharp.Core.

#### Overload resolution

This RFC allows overloads to succeed where previously they would have failed. However overloads not using type-directed conversions are preferred to those using it.

@gusty says:

> I suspect that rule will result in breaking changes as there are some cases where type information is not complete.

@dsyme says: 

> In principle I don't think so, as a type-directed conversion (TDC) can only ever apply in cases where type
> checking of the overload was definitely going to fail. By preferring overloads that succeeded without TDC we first
> commit to any existing successful resolution that would have followed for existing code, and then allow new resolutions into the game.


# Unresolved questions

TODO: There are things to tune in this RFC, including

1. Whether warnings are given for implicit conversions

2. Whether these warnings are opt-in or not

3. Consider impact on tailcalls https://github.com/fsharp/fslang-design/discussions/525#discussioncomment-484473

4. See comment on generic literals by opt-in

