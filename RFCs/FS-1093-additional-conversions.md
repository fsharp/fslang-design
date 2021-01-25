# F# RFC FS-1093 - Additional type directed conversions

The design suggestion [Additional type directed conversions](https://github.com/fsharp/fslang-suggestions/issues/849) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/10884)
- [ ] Design Review Meeting(s) with @dsyme and others invitees
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/525)

# Summary

This RFC extends F# to include type-directed conversions when known type information is available.
A type-directed conversion is used as an option of last resort, at leaf expressions, so different types
may be returned on each bracnh of compound structures like `if .. then ... else`.

# Motivation

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

Instead, in all cases, prior to this RFC type upcasts of `box` are needed:
```fsharp
let a : int seq = ([1; 2; 3] :> int seq)
let b : obj seq = ([box 1; box 2; box 3] :> int seq)
let f () : A = if true then (B() :> _) else (C() :>_)
let (data: A option) = Some (B() :> _)
f2 (Some (B() :> _)
```

The requirement to make upcasts is surprising and counter-intuitive, though came with benefits (see 'Drawbacks').

There are several existing techniques in the F# language to reduce the occurence of upcasts and to ensure resulting code is as general
as possible.  These include:

- Condensation and decondensation of function types each time a function or method value is used,
  e.g. given `let f (x: A) = ()` then when `f` is used as a first class value it's given type `f : 'T -> unit when 'T :> A`.

- Auto-introduction of conversions for elements of lists with a type annotation, e.g. `let a : obj list = [1; 2; 3]`

- Auto-introduction of conversions for assignments into mutable record fields and some other places with known type information.


# Detailed design

The "known type" of an expression is annotated with a flag as to whether it is "must convert to" or "must equal". The flag is set for:

- The right-hand side of a binding 

- The body of a function or lambda

- Each expression used in argument position

- If an if-then-else or match expression has the flag, then the flag is set for each branch.

- If a let, let-rec, try-finally or try-catch has the flag, then the flag is set for the body.

If an expresson has a known type `ty` with the flag set, and, after checking, the type of the expression `ty2` is different to `ty`,
then a type-directed conversion is attempted by

1. Trying to add a coercion constraint from `ty2 :> ty`. If this succeeds, a coercion operation is added to the elaborated expression.

2. Trying to convert a function type to a delegate type by the standard rules. If this succeeds, a delegate construction operation is added to the elaborated expression.

3. Trying the special conversions `int32` to `int64`, `int32` to `single`, `int32` to `double`. If this succeeds, a numeric conversion is added to the elaborated expression.

4. Searching for an `op_Implicit` method on either `ty` or `ty2`. If this succeeds, a call to the `op_Implicit` is added to the elaborated expression.

For existing cases where an inference variable and flexibility constraint is added for a known type (e.g. when checking an element of a list),
the checking process is unchanged.

These conversions also apply for optional arguments, for example:

```fsharp
type C() = 
    static member M1(?x:int64) = 1
C.M1(x=2)
```

TODO: show the more subtle ramifications of these rules

TODO: check the details with the implementation - there are some cases we pre-asset a subsumption constraint

NOTE: not all C# numeric widenings are supported, by design.

NOTE: Branches of an if-then-else or `match` may have different types even when checked against the same known type, e.g. 

```fsharp
let f () : A = if true then B() else C()
```
is elaborated to
```fsharp
let f () : A = if true then (B() :> A) else (C() :> A)
```


### Not covered - upcast without known type information

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

Despite appearances, the approach to type checking without an "implicit conversion" rule has significant advantages:

1. When a sub-expression is extracted to a `let` binding for a value or function, its inferred type rarely changes (it may just become more general)

2. Information loss is made explicit

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

- Don't do this and maintain status quo

# Compatibility

This is not a breaking change. The elaboration of existing code that passes type checking is not changed.

This doesn't extend the F# metadata format.

There are no additions to FSharp.Core.

# Unresolved questions

TODO: consider the exact range of type-directed conversions to be included, specifically whether numeric widenings
and `op_Implicit` are to be included.

