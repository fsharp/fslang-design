# F# RFC FS-1092 - Anonymous Type-tagged Unions

This RFC adds [anonymous type-tagged unions](https://github.com/fsharp/fslang-suggestions/issues/538).

* [x] [Suggestion approved in principle](https://github.com/fsharp/fslang-suggestions/issues/538)
* [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/519)
* [ ] Implementation: [Early Prototype](https://github.com/dotnet/fsharp/pull/10566), [Latest Draft]([https://github.com/dotnet/fsharp/pull/10896](https://github.com/dotnet/fsharp/pull/12265))

An implementation prototype and test baseline is being maintained on a fork
in [VMikulasek/fsharp#1](https://github.com/VMikulasek/fsharp/pull/1). This is
not the upstream implementation proposal; it is intended to inform a future
public pull request.

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

6. There is no possibility that the type will end up being consumed by .Net languages other than F#.

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

  Directly nested anonymous-union syntax is rejected; an alias is required
  when a union is used as an alternative. Aliased unions are flattened after
  type resolution.

* Immediately after such a type is elaborated, overlap is checked using runtime representations. Directly duplicated cases are errors. If one case is subsumed by another, the compiler reports an overlap warning and ignores the subsumed case. For example:

  - `(int|int)` is a directly duplicated case and is an error.
  - `(System.ValueType|int)` reports an overlap warning; the `int` case is ignored.
  - `(System.IComparable|string)` reports an overlap warning; the `string` case is ignored.
  - `(obj|int)` reports an overlap warning; the `int` case is ignored.

  A duplicate introduced by expanding an aliased anonymous union is allowed
  with an overlap warning, rather than being treated as a directly duplicated
  case:

  ```fsharp
  type IntOrString = (int|string)
  type IntOrStringOrBool = (IntOrString|int|bool)
  ```

  The `int` case written directly in `IntOrStringOrBool` overlaps with the
  `int` case obtained from `IntOrString`; the aliased case is retained and the
  direct case is ignored.

  If this leaves one effective case, the compiler also reports a degradation warning
  and the union degrades to that case.
  
* Naked generic type parameters are permitted in erased unions. A wildcard is
  not permitted. Non-naked generic cases are permitted when their use identifies
  one case; a use for which multiple cases match is rejected at the use site.
  For example:

  ```fsharp
  let decide<'T, 'U> (x: ('T|'U)) =
      match x with
      | :? 'T -> 0
      | :? 'U -> 1

  let ambiguous<'T, 'U>(x: (list<'T>|list<'U>)) = x

  ambiguous<int, string> []
  // Error: both list<int> and list<string> match the argument.
  ```

  The common ancestor stored in an erased union must be remapped together with
  its cases during generic instantiation.

* Erased union are commutative and associative and internally are immediately flattened and normalised.

    ```fsharp
    (A|B) =:= (B|A)
    (A|(B|C)) =:= ((A|B)|C)
    ```

    *`=:=` implies type equality and interchangeable in all context*

* Erasure takes into account units-of-measure, tuple elimination and `FSharpFunc` elimination. For example all of these are disallowed:

  - `(int|int<userid>)`
  - `((int -> int)|FSharpFunc<int,int>)` (one type is fully included in another w.r.t. runtime type tests)
  - `((int * int)|System.Tuple<int,int>)`

* `null` may be used only as the final case of the outermost, directly written
  anonymous union. It belongs to the union as a whole, not to an individual
  case:

  ```fsharp
  type TextOrInt = (int|string|null)
  let x: (float|TextOrInt) = null
  ```

  The nullable alias is hoisted, so the effective type of `x` is
  `(float|int|string|null)`. This also applies when aliases are chained.
  Directly nested forms such as `(int|(string|null))` and
  `((int|string)|null)` are rejected; write `(int|string|null)`.
  `(T|null)` with one non-null case normalizes to an ordinary nullable `T`.
  A nullable anonymous union must have a reference-type common ancestor.
  Therefore `(int|float|null)` is rejected. `System.Nullable<_>` is not
  an alternative; use a null case instead.

## Type relations
[subtyping]: #subtyping-rules

* Two anonymous type-tagged unions are equivalent if their constituent parts are all equivalent.

* If `A :> C` and `B :> C` then `(A|B) :> C` where `T :> U` implies T is subtype of C;

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

For a nullable anonymous union, `null` must also be covered:

```fsharp
let decide (x: (int|string|null)) =
    match x with
    | :? int -> 1
    | :? string -> 2
    | null -> 3
```

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

The compiled representation type for `(A|B)` is the first common
class ancestor of `A` and `B`. Interfaces are not considered when selecting
the erased representation. For example:

```fsharp
// compiled representation type is System.Object
type IntOrString = (int|string)
// compiled representation type is System.ValueType
type Num = (int8|int16|float)
type Base = class end
type A = inherit Base
type B = inherit Base
// Base is the compiled representation type
type AorB = (A|B)
```

Consequently, common interfaces such as `System.IComparable` do not affect the erased representation.

## Resolved issues

It is tempting to have anonymous tagged unions should support the "equality" and "comparison" constraints if every element supports this constraint (https://github.com/fsharp/fslang-design/discussions/519#discussioncomment-345984).   However, we must be careful here, `(int|string)` may not be comparable to `(int|string)` because in .NET `int` is not comparable to `string`.


# Drawbacks
[drawbacks]: #drawbacks

See "Guidance" above.

1. This adds an alternative to label-tagged unions.  This can lead to confusion about which to use.

2. The mechanism relies on type annotations and implicit inference conversions which can make certain parts of code harder to understand (though the presence of tags may also decrease code reusability).

3. The mechanism can encourage "hierarchy thinking" where the user wastes precious thought time on trying to find a perfect "classification" of disparate cases into a set of hierarchically organised types.  This kind of activity is normally unproductive, leading to fragile code and "false" attempts at finding commonality.

4. The method requires struct values to be boxed when participating in a union.  This can lead to performance degradation.

5. Users can falsely rely on anonymous type-tagged union types to ascribe additional semantics to union types, e.g. using `(int|unit)` to represent a database value (including `unit` for `NULL`), with the expectation that these values can be combined algebraically.

6. The mechanism relies on allowing additional implicit conversions in F# code, which itself can have drawbacks.

7. Type erasure means that lowest common denominator types are exposed to non-F# .Net consumers, removing type safety.

# Alternatives
[alternatives]: #alternatives

# Out of scope
[outofscope]: #out-of-scope

The following related features are out of scope for this version:

* Extending anonymous unions to combine ordinary types with specific atomic
  values, such as `(int|float|"auto"|"*")`. This would require additional
  type-system design beyond anonymous unions.

## C# unions
C# union is essentially aliased anonymous union. Therefore the interoperability could be implemented for example as follows:
```csharp
public union SomeUnion(int, string)
```

```fsharp
[<CSharpUnion>]
type SomeUnion = (int|string)
```
Interoperability with C# unions is out of scope for this version.