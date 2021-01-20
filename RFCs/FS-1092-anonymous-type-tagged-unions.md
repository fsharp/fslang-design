# F# RFC FS-1092 - Anonymous Type-tagged Unions

This RFC adds [anonymous type-tagged unions](https://github.com/fsharp/fslang-suggestions/issues/538).

* [x] [Suggestion approved in principle](https://github.com/fsharp/fslang-suggestions/issues/538)
* [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/519)
* [ ] Implementation: [Early Prototype](https://github.com/dotnet/fsharp/pull/10566), [Latest Prototype](https://github.com/dotnet/fsharp/pull/10896)

This RFC builds on a separate RFC for [additional implicit conversions guided by type annotations](https://github.com/fsharp/fslang-design/discussions/525).

# Summary
[summary]: #summary

Adds "anonymous type-tagged unions" for representing disjoint unions of data where each case is fully described by the type of data carried by that case

# Motivation
[motivation]: #motivation

F# already supports discriminated unions.  Additionally, generic "Choice" discriminated union types are available in FSharp.Core. In both cases these use labels (e.g. `Some` or `Choice1Of2`) for tags.

In some use-cases, especially in DSLs, the burden of requiring labels to inject into a discriminated union type is significant, as is the burden of requiring an explicit nominal type definition for the union at all - especially when all cases are distinguished fully and sufficiently by the type of data carried by each case.  This RFC addresses this by adding an additional option to represent disjoint unions of data: anonymous type-tagged unions. 

One primary use-case is for reducing method overloading.  For example, consider a styling API, implemented as follows when using this feature:

```fsharp
type View =
    static member Font(name: (string|FontFamily), ?size: (float|int|string)) = ...

View.Font("Sans Serif", 12.0)
View.Font(FontFamily.SansSerif, "10px")
```

Prior to this RFC the API may have used a large number of method overloads, e.g.

```fsharp
type View =
    static member Font(name: string, ?size: float) = ...
    static member Font(name: string, ?size: int) = ...
    static member Font(name: string, ?size: string) = ...
    static member Font(name: FontFamily, ?size: float) = ...
    static member Font(name: FontFamily, ?size: int) = ...
    static member Font(name: FontFamily, ?size: string) = ...

View.Font("Sans Serif", 12.0)
View.Font(FontFamily.SansSerif, "10px")
```

Alternatively the API may have used tagging or other forms of labelling via object types, e.g.

```fsharp
type FontSize =
    | Float of float
    | String of string

type View =
    static member Font(name: FontFamily, size: FontSize) = ...

View.Font(FontFamily("Sans Serif"), FontSize.Float 12.0)
View.Font(FontFamily.SansSerif, FontSize.String "10px")
```

> NOTE: If written in C# the API may have used `op_Implicit` conversions, which are not well supported in F# and tend to lead to
> significant type inference problems (though note this may be improved by other future design additions).

Another use-case is for describing heterogeneous data, e.g.

```fsharp
let data: (string * (int|float|string)) list = 
    [ "name", "Joe"
      "age", 16
      "address", "here"
      "height", "5'10''" ]
```

# Guidance

In general, discriminated unions using labels should be preferred for the majority of F# code, especially implementation code.

An anonymous type-tagged union type should only be considered for when a union is made up of disjoint cases where:

1. Each case carries one item of significant data 

2. An existing nominal type is available for the data carried by each case and fully describes each case

3. The union type is non-recursive

4. There is no possibility that future evolution of the type will involve new cases overlapping with the existing types.

5. There is some identified, concrete, simply explained benefit over using labelled discriminated unions, e.g. "we have a simpler API with fewer overloads".

For example, an anonymous type-tagged union should **not** be considered for the following union type:

```fsharp
type Syntax = 
    | Const of int
    | Empty
    | Combination of Syntax * Syntax
```

This violates the above on many grounds:

❌ The type `int` is insufficient to characterise the `Const` node

❌ The labels carry meaning, e.g. `Const` is important information in constituting that case.

❌ Future additions to the syntax could easily add a new, different case carrying `int`.

❌ The type is recursive

❌ The case `Combination` carries multiple data elements

In contrast the following type is a reasonable candidate for replacing with `(int|string|float)`:

```fsharp
type FontSize = 
    | Int of int
    | String of string
    | Float of float
```

✔️ Each case carries one item of significant data 

✔️ The labels are essentially meaningless given the types

✔️ An existing nominal type is available for the data carried by each case and fully describes each case

✔️ The union type is non-recursive

✔️ There is no possibility that future evolution of the type will involve new cases overlapping with the existing types.

✔️ There is some identified, concrete, simply explained benefit over using labelled discriminated unions, e.g. "we have a simpler API with fewer overloads".


# Detailed design
[design]: #detailed-design

The syntax of types is extended with an anonymous type-tagged unions:

```
type =
    | ...
    | '(' type '|' ... '|' type ')'
```

The parentheses are always required.  

## Type elaboration and well-formedness

* An anonymous type-tagged type is elaborated by elaborating its constituent parts and flattening contained unions.

* Immediately after such a type is elaborated, no possibility of overlap or runtime-type-identity ambiguity (after erasure) is permitted.  For example all of these are disallowed:

  - `(int | int)`  (one type is fully included in another  w.r.t. runtime type tests)
  - `(System.ValueType | int)`  (one type is fully included in another  w.r.t. runtime type tests)
  - `(System.IComparable | string)`  (one type is fully included in another  w.r.t. runtime type tests)
  - `(obj | int)` (one type is fully included in another w.r.t. runtime type tests)
  
* Generic type arguments may not be used as naked in erased unions. For these purposes each type variable or wildcard occurring syntactically in the types is considered separately and independently. For example all of these are disallowed:
  - `('T | int)` 
  - `(_ | int)`
  - `(list<'T> | list<'U>)`
  - `(list<'T> | list<int>)`
  - `type StringOr<'a> = ('a | string)`

* Erased union are commutative and associative and internally are immediately flattened and normalised.

    ```fsharp
    (A | B) =:= (B | A)
    (A | (B | C)) =:= (( A | B ) | C)
    ```

    *`=:=` implies type equality and interchangeable in all context*

* Erasure takes into account units-of-measure, tuple elimination and `FSharpFunc` elimination. For example all of these are disallowed:

  - `(int|int<userid>)`
  - `((int -> int) | FSharpFunc<int,int>)` (one type is fully included in another w.r.t. runtime type tests)
  - `((int * int) | System.Tuple<int,int>)`

## Type relations
[subtyping]: #subtyping-rules

* Two anonymous type-tagged unions are equivalent if their constituent parts are all equivalent.

* If `A :> C` and `B :> C` then `(A | B) :> C` where `T :> U` implies T is subtype of C;

## Type inference
[inference]: #type-inference

This RFC builds on a separate RFC for [additional implicit conversions guided by type annotations](https://github.com/fsharp/fslang-design/discussions/525).

Assuming this, a new implicit conversion is added for expressions where, if the known type information for of an expression is "must convert to" an erased union type,
and the type of the expression is a nominal type prior to its commitment point, then that type must convert to one of the constituent types of the 
erased union type.

This means the following is valid because the known type information for expressions `true` and `"Hello"` is in both cases "must convert to `(int|string)`".

```fsharp
// inferred to 
let intOrString : (int|string) = if true then 1 else "Hello"
```
The following is invalid, because there is no known type information for any of the sub-expressions on the right-hand-side of the binding.

```fsharp
let intOrString = if true then 1 else "Hello" // invalid
```

## Pattern matching

Values having an anonymous type-tagged union type may be used to either passed to other functions or methods, or eliminated
by using pattern matching:

```fsharp
let prettyPrint (x: (int8|int16|int64|string)) =
    match x with
    | :? int8 -> prettyPrintInt8 x
    | :? int16 -> prettyPrintInt16 x
    | :? int64 -> prettyPrintInt64 x
    | :? string as y -> prettyPrintString y
```

The match is considered exhaustive if all parts of the erased union are covered. 

Similarly the following would also be considered exhaustive:

```fsharp
let prettyPrint (x: (int8|int16|int64|string)) =
    match x with
    | :? System.ValueType as y -> prettyPrintNumber y // int8, int16 and int64 are subtype of ValueType
    | :? string as y -> prettyPrintNumber y
```

EDITOR NOTE (Don Syme): I'm not convinced the complexity added by this last case is worth it but I suppose we should do it.

## Erased Type
[erasedtype]: #erased-type

The compiled representation type for `(A | B)` is the best or first common ancestor of `A` and `B`. For example:

```fsharp
// compiled representation type is System.Object
type IntOrString = (int|string)
// compiled representation type is System.ValueType
type Num = (int8|int16|float)
type I = interface end
type A = inherit I
type B = inherit I
// I is the compiled representation type
type AorB = (A|B) 

type I2 = interface end
type C = inherit I inherit I2
type D = inherit I inherit I2
// Both I or I2 could be potential compiled representation type. The compiler would choose I since its the earliest ancestor
type CorD = (C|D) 
```

NOTE: we need to be more precise here.  For example `int` and `string` both support many common interfaces like `System.IComparable`.  It is possible that
for compilation stability we should always only use `obj`.


# Drawbacks
[drawbacks]: #drawbacks

See "Guidance" above.

1. This adds an alternative to label-tagged unions.  This can lead to confusion about which to use.

2. The mechanism relies on type annotations and implicit inference conversions which can make certain parts of code harder to understand (though the presence of tags may also decrease code reusability).

3. The mechanism can encourage "hierarchy thinking" where the user wastes precious thought time on trying to find a perfect "classification" of disparate cases into a set of hierarchically organised types.  This kind of activity is normally unproductive, leading to fragile code and "false" attempts at finding commonality.

4. The method requires struct values to be boxed when participating in a union.  This can lead to performance degradation.

5. Users can falsely rely on anonymous type-tagged union types to ascribe additional semantics to union types, e.g. using `(int|unit)` to represent a database value (including `unit` for `NULL`), with the expectation that these values can be combined algebraically.

6. The mechanism relies on allowing additional implicit conversions in F# code, which itself can have drawbacks.

# Alternatives
[alternatives]: #alternatives

# Unresolved questions
[unresolved]: #unresolved-questions

* Should pattern matching type tests have to match the available type structure explicitly or should subtype-inclusion be permitted?

* There is a slippery slope where an additional typing mechanism may be desired for some APIs for types representing atomic values, e.g. `(int|float|"auto"|"*")` representing specific allowed string values.

* See above: "NOTE: we need to be more precise here. For example int and string both support many common interfaces like System.IComparable."

