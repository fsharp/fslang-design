# F# RFC FS-1093 - Additional type directed conversions

The design suggestion [Additional type directed conversions](https://github.com/fsharp/fslang-suggestions/issues/849) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/849)
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/10884)
- [x] [Community Review Meeting](https://github.com/fsharp/fslang-design/issues/589)
- [x] [Discussion](https://github.com/fsharp/fslang-design/discussions/525)

# Summary

This RFC extends F# to include type-directed conversions when known type information is available. It does three things:

1. Puts in place a general backwards-compatible last-resort mechanism for type directed conversions.

2. Adds a particular set of type directed conversions.  These are:
   - Delegates: the existing func (`'a -> 'b`) --> `delegate` type directed conversions
   - Expressions: the existing `delegate` --> LINQ `Expression` type directed conversions
   - Subsumption: upcasting
   - Numeric: `int32` --> `int64`/`nativeint`/`float`
   - Code-defined: `op_Implicit` when both source and destination are nominal.

   Numeric and code-defined type-directed conversions only apply when both types are fully known, and are generally only useful for non-generic types.

3. Implements warnings when any of these are used (excluding the two existing conversions for delegates and expressions). These warnings are generally off by default, see below.

# Design Principles

The intent of this RFC is to give a user experience where:

1. Interop is easier (including interop with some F# libraries, not just C#)

2. You don't notice the feature and are barely even aware of its existence.

3. Fewer upcasts are needed when programming with types that support subtyping

4. Fewer widening conversions are needed when mixing `int32`, `int64`, `nativeint` and `float`.

5. Numeric `int64`/`float` data in tuple, list and array expressions looks nicer

6. Working with new numeric types such as System.Half whose design includes `op_Implicit` should be less irritating

7. Inadvertent use of the mechanism should not introduce confusion or bugs

8. The F# programmer is discouraged from adding `op_Implicit` conversions to their type designs

NOTE: The aim is to make a feature which is trustworthy and barely noticed. It's not the sort of feature where you tell people "hey, go use this" - instead the aim is that you don't need to be cognisant of the feature when coding in F#, though you may end up using the mechanism when calling a library, or when coding with numeric data, or when using data supporting subtyping.  Technical knowledge of the feature is not intended to be part of the F# programmer's working knowledge, it's just something that makes using particular libraries nice and coding less irritating, without making it less safe.

There is no design goal to mimic all the numeric widenings of C#.

There is no design goal to eliminate all explicit upcasts.

There is no design goal to eliminate all explicit numeric widening.

There is no design goal to eliminate all explicit calls to `op_Implicit`, though in practice we'd expect `op_Implicit` calls to largely disappear.

There is no goal to make defining `op_Implicit` methods a normal part of F# methodology.

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
let a : int seq = [1; 2; 3] 
// ❌ error FS0001: This expression was expected to have type 'seq<int>' but here has type ''a list'    
let b : obj seq = [1; 2; 3] 
// ❌ error FS0001: This expression was expected to have type 'seq<int>' but here has type ''a list'    
```

Or, alternatively, at return position, consider this:

```fsharp
type A() = class end
type B() = inherit A()
type C() = inherit A()
let f () : A = if true then B() else C()
// ❌ error FS0001: This expression was expected to have type 'A' but here has type 'B'
```

Or, alternatively, at constructions of data such as options, consider this:

```fsharp
type A() = class end
type B() = inherit A()
let f2 (x: A option) = ()

let (data: A option) = Some (B())
// ❌ error FS0001: This expression was expected to have type 'A' but here has type 'B'
f2 (Some (B())
// ❌ error FS0001: This expression was expected to have type 'A' but here has type 'B'
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

### Motivation for `int32` --> `int64` type-directed conversion

The primary use case is using integer literals in int64 data.
APIs using 64-bit integers are very common in some domains.  For example, in a typical tensor library shapes are given using `int64[]`. So it is
frequent to write `[| 6L; 5L |]`.  There is a reasonable case to writing `[| 6; 5 |]` instead when the types are known.

Note a non-array-literal expression of type `int[]` still need to be explicitly converted to `int64[]`.

### Motivation for `int32` --> `nativeint` type-directed conversion

The primary use case is APIs using nativeint. APIs using nativeint are beginning to be more common in some domains.

### Motivation for `int32` --> `double` type-directed conversion

The primary use case is using integer literals in floating point data such as `[| 1.1; 3.4; 6; 7 |]` when the types are known.  

### Motivation for `op_Implicit` type-directed conversion

Certain newer .NET APIs (like ASP.NET Core) as well as popular 3rd party libraries, make frequent use of `op_Implicit` conversions.
  
Examples
  * many APIs in `Microsoft.Net.Http.Headers` make use of `StringSegment` arguments
  * [MassTransit](https://github.com/MassTransit/MassTransit) uses `RequestTimeout`, which has conversion from `TimeSpan` as well as `int` (milliseconds), seemingly for a simpler API with fewer overloads
  * [Eto.Forms](https://github.com/picoe/Eto/) uses implicit constructor for many entities.

Remarks:

* In those cases, C# devs can just pass a string, but F# devs must explicitly call op_Implicit all over the place.
* One thing to watch out for is `op_Implicit` conversions to another type. E.g. `StringSegment` has conversions to `ReadOnlySpan<char>` and `ReadOnlyMemory<char>`. 

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

When an overall type `overallTy` is propagated to an expression with type `exprTy` and the "must convert to" flag is set, and
`overallTy` doesn't unify with `exprTy`, then a type-directed conversion is attempted by:

1. Trying to add a coercion constraint from `exprTy :> overallTy`. If this succeeds, a coercion operation is added to the elaborated expression.

2. Trying to convert a function type `exprTy` to a delegate type `overallTy` by the standard rules. If this succeeds, a delegate construction operation is added to the elaborated expression.

3. Trying the special conversions `int32` to `int64`, `int32` to `nativeint`, `int32` to `double` from `exprTy` to `overallTy`.
   If this succeeds, a numeric conversion is added to the elaborated expression.

4. Searching for a matching `op_Implicit` method on either `exprTy` or `overallTy`. If this succeeds, a call to the `op_Implicit` is added to the elaborated expression.

If the "must convert to" flag is not set on `overallTy`, then unification between `overallTy` and `exprTy` occurs as previously.

> NOTE: There are some existing cases in the F# language specification where an inference variable and flexibility constraint
> is already added for a known type (specifically when checking an element of a list or array when the element type has
> been inferred to be nominal and supports subtyping). In these cases, the checking process is unchanged.

> NOTE: For list, array, `new`, object expressions, tuple, record and anonymous-record expressions, pre-checking integration
> allows the overall type to be propagated into the partially-known type (e.g. an
> list expression is known to have at least type `list<_>` for some element type), which in turn allows the
> inference of element types or type arguments.  This may be relevant to processing the contents of the expression.
> For example, consider
> 
```fsharp
let xs : seq<A> = [ B(); C() ]
```
> 
> Here the list is initially known to have type `list<?>` for some unknown type, and the overall known type `seq<A>` 
> is then integrated with this type, inferring `?` to be `A`.  This is then used as the known overall type when checking
> the element expressions.


> NOTE: Branches of an if-then-else or `match` may have different types even when checked against the same known type, e.g. 
> 
> ```fsharp
> let f () : A = if true then B() else C()
> ```
>
> is elaborated to
>
> ```fsharp
> let f () : A = if true then (B() :> A) else (C() :> A)
> ```


### Selection of implicit conversions

Implicit conversions are only selected for type `X` to `Y` if a precise implicit conversion exists as
an intrinsic method on either type `X` or `Y` and:

1. no feasible subtype relationship between X and Y (an approximation), OR
2. T --> some-type-containing-T

Note that even for (2) implicit conversions are still only activated if the
types *precisely* and *completely* match based on *known* type information at the point of resolution.

Conversions are also allowed for type `X` to `?` where the ? is a type inference variable constrained
by a coercion constraint to Y for which there is an op_Implicit from X to Y, and the other conditions above apply.
The type inference variable will later eliminated to by Y.

It is important to emphasise that implicit conversions are only activated if the types **precisely** match based on known type information
at the point of resolution.  For example

```fsharp
let f1 (x: 'T) : Nullable<'T> = x
```
is enough, whereas

```fsharp
let f2 (x: 'T) : Nullable<_> = x 
let f3 x : Nullable<'T> = x
```
are not enough to activate the `op_Implicit: 'T -> Nullable<'T>` conversion on `Nullable<_>`.
Indeed `f2` and `f3` already type check today in F#:

* `f2` has type `val f2: Nullable<'a> -> Nullable<'a>` with a warning saying `T` is instantiated to `Nullable<'a>`

* `f3` has type `val f3: Nullable<'T> -> Nullable<'T>`


### Interaction with method overload resolution 

Overloads not making use of type-directed conversion are always preferred to overloads with type-directed conversion in overload resolution.

### Warnings for type directed conversions

Type-directed conversions can cause problems in understanding and debugging code.  Further, we don't
want to encourage the use of `op_Implicit` as a routine part of F# library design (though occasionally it has its uses).

As a result, four warnings are added, three of which are off by default:

1. FS3388: Type-directed conversion by subtyping (e.g. `string --> obj`). This warning is OFF by default.  

2. FS3389: Type-directed conversion by a built-in numeric conversion (`int --> int64` etc.). This warning is OFF by default.  

3. FS3395: Type-directed conversion by an `op_Implicit` conversion at method-argument position.
   This warning is OFF by default.  

4. FS3391: Type-directed conversion by an `op_Implicit` conversion at non-method-argument. This warning is ON by default.

The user can enable all these warnings through `--warnon:3388 --warnon:3389 --warnon:3395`. The warnings will contain a link to further documentation.

This policy is chosen because `op_Implicit` is part of .NET library design, but in F# we generally only
want it applied at method argument position, like other adhoc conversions applied at that point.
If it is applied elsewhere, a warning is given by default.

See also [this part of the RFC discussion](https://github.com/fsharp/fslang-design/discussions/525#discussioncomment-1051349) for
examples where the F# programmer may be tempted to adopt `op_Implicit` to little advantage.


### No upcast without known type information

Consider this example:

```fsharp
type A() = class end
type B() = inherit A()
type C() = inherit A()

let Plot (elements: A list) = ()

[B(); C()] |> Plot
// ❌ error FS0193: Type constraint mismatch. The type 'C' is not compatible with type 'B'
```

This RFC change will not address this example - the element type of the list is inferred to be `B`. This however works:

```fsharp
Plot [B(); C()]
```

APIs sensitive to this should consider avoiding the use of piping `|>` as a result, and
instead maximise the flow of type information from destination (here `Plot`) into checking of contents (here `[B(); C()]`).

### Tailcalls

Some newly allowed calls may not be tailcalls, e.g.:

```fsharp
 let f1 () : int = 4
 let f2 () : obj = f1()
 // this is not a tailcall, since an implicit boxing conversion happens on return
```

Turning on the optional warning and removing all use of type-directed conversions from your code can avoid this if necessary.

### Interaction with SRTP constraint resolution

Type-directed conversions are ignored during SRTP constraint processing.

### Interaction with optional arguments

Type-directed conversions are applied for optional arguments, for example:

```fsharp
type C() = 
    static member M1(?x:int64) = 1
C.M1(x=2)
```

### Interaction with the option types

The two `op_Implicit` on the F# `option` (`Option`) and `voption` (`ValueOption`) types are ignored. These are present for C# interop.

### Interaction with post-application property setters

Type-directed conversions are applied for post-application property setters, for example:

```fsharp
type C() = 
    member val X : int64 = 1L with get, set
    
let c = C(X=2)
c.X // = 2L : int64
```

### Interaction with record fields

Because record fields give rise to known type information, type-directed conversions are applied when filling in record fields:

```fsharp
type R =  { X: int64 }
    
let r = { X = 2 }
> val r : R = { X = 2L }
```


### Interaction with union fields

Because union fields give rise to known type information, type-directed conversions are applied when filling in record fields:

```fsharp
type U = U of int64
    
let r = U(2)
> val r : U = U 2L
```

### Interaction with anonymous record fields

Anonymous record fields can in theory have known type information, if an appropriate annotation is present, e.g.

```fsharp
let r : {| X: int64 |} = {| X = 2 |}

> val r : {| X: int64 |} = { X = 2L }
```

However, often types are inferred from contents for these, e.g. 
```fsharp
let r = {| X = 2 |}
> val r : {| X: int32 |} = { X = 2 }
```

See "Expressions may change type when extracted" below.


### Incomplete matrix of integer widenings

The matrix of integer widenings is somewhat deliberately incomplete.   Specifically there are no 

    int8 --> int16
    int8 --> int32
    int8 --> int64
    int16 --> int32
    int16 --> int64

widenings, nor their unsigned equivalents, not any unsigned-to-signed widenings.

This raises one issue: hand-written .NET numeric types such as `System.Half`, `System.Decimal` and `System.Complex` do allow certain implicit conversions via `op_Implicit`. 
The user may ask, if these types have widening from `int8` and `int16`then why doesn't, say, `int64`?  However in practice the `int8` and `int16` types are very rarely
used in F#, so we expect this to be vanishingly rare, and code will clearer if these specific widenings are made explicit.

In addition, these widenings are not included:

    int32 --> float32
    float32 --> float

Earlier drafts of this PR included `int32` --> `float32` and `float32` --> `float` widenings. However, the use cases for these as
adhoc type-directed conversions in F# programming are not particularly compelling - remember, adhoc conversions can cause confusion, and
shuold only be added if really necessary.

* One proposed use case for an implicit TDC for `int32` --> `float32`  is machine learning
  APIs which accept `float32` data, for example ideally little usability penalty should apply when switching from `float` to `float32`.
  However adhoc type directed conversions do not solve this.

* One proposed use case for an implicit TDC for `float32` --> `float` is floating point utility code (e.g. printing) to use 64-bit floating point
  values, and yet be routinely usable with 32-bit values.  However a non-array-literal value of `single[]` still need to be converted to `double[]`, for example,
  so the lack of uniformity in practical settings will still require explicit conversions.

As a result these have been removed from the proposal. They can always be added at a later date.

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
    f [ data1; data2 ]
// ❌ error FS0001: Type mismatch. Expecting a 'obj * obj' but given a 'int * string'. The type 'obj' does not match the type 'int'.
```

Here, `data1` now needs a type annotation to maintain the same inferred type.  This matters because `data` is a tuple, and, in the absence of 
co-variance on tuples (which wouldn't apply to the `int --> obj` conversion in any case), would need to be unpackaged and repackaged to
get the correct destination type of `obj * obj`.  Here is a working version with the type annotation:

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
* Allow the user to define the default constraint, which will in any case make the language more consistent.
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

> 
> A type-directed conversion (TDC) can only ever apply in cases where type
> checking of the overload was definitely going to fail. By preferring overloads that succeeded without TDC we first
> commit to any existing successful resolution that would have followed for existing code, and then allow new resolutions into the game.
>
> Further, TDCs are ignored during SRTP resolution.

# Notes from community design/implementation/test review on 08/06/2021 hosted by @dsyme

Questions:

> "[21:31] Chet Husk - oh, related question: how does this interop with Fable? I'd expect just by altering the shape of returned exprs?

Answer: no impact, just more programs will be expected, no new expression shapes
  
> "[21:27] Heron Barreto - are we going to expand usage of the flexible type operator(?) with this RFC?"

Answer: no, indeed it might be used less

> "[20:43] Chet Husk - for 'Interaction with post-application property setters', the property was given an explicit type before use. if the type was not directly specified and instead inferred through usage (2L), would that be enough to 'fix' the overall type for purposes of these rules?

Answer: yes, exactly, inference is enough to fix the 'overall type'

> "[20:24] Chet Husk - was part of the decision for allowing op_Implicit only, and not op_Explicit, is because implicit conversions in .Net are assumed to be total/safe?"

Answer: we should consider whether we want to address `op_Explicit`.  These calls are rare in .NET libraries, most are numeric narrowings, already catered for in most cases

For the second part of the question - yes, `op_Implicit` are assumed to be total/safe

> "[20:27] Chet Husk - the concern with extracting expressions can be mitigated with editor tooling (ie 'extract binding'/'extract expression') being aware of the context and applying the desired type annotation to the extracted expression, yeha?

Yes, that's right, indeed note this feature may make it harder to implement minimal-type-annotation extraction transforms.


# Unresolved questions

Things to follow-up on from community design review:

* [x] Proof using XML APIs that make existing use of op_Implicit

* [ ] Proof using Newtonsoft Json APIs that make existing use of op_Implicit

* [x] Proof defining op_Implicit for FSharp.Data `JsonValue`

* [ ] Ask community for further examples of using `op_Implicit`

* [ ] Check removal of `Nullable` calls on more examples

* [ ] Proof using .NET DataFrame APIs

* [ ] "another popular library to validate with is StackExchange.Redis, which relies heavily on implicit operators for keys and values in redis"

* [x] Should there be a TDC from `int32<measure>` to `int64<measure>` given that `int32` auto-widens to `int64`? (NO)

* [ ] Should there be a TDC from function types _ -> _ to System.Delegate. This comes up in "minimal APIs" which take System.Delegate as an argument. See https://github.com/halter73/HoudiniPlaygroundFSharp/pull/3/files for an example of the kind of change this would allow. The RFC includes the existing TDC from function types to specific delegate types. However this doesn't get us to the base type System.Delegate
