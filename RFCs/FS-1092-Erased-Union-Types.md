# F# RFC FS-1092 - Erased Union Types

This RFC covers the detailed proposal for this suggestion. [Erased type-tagged anonymous union types](https://github.com/fsharp/fslang-suggestions/issues/538).

* [ ] Approved in principle
* [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/538)
* [ ] Details: TBD
* [ ] Implementation: [Preliminary Prototype](https://github.com/dotnet/fsharp/pull/10566)


# Summary
[summary]: #summary

Add erased union types as a feature to F#. Erased Union types provide some of the benefits of structural ("duck") typing, within the confines of a nominative type system.

# Motivation
[motivation]: #motivation

Supporting erased union types in the language allows us to move more type information with the usual advantages this brings:

* They serve as an alternative to function overloading.
* They obey subtyping rules.
* They allow representing subset of protocols as a type without needing to resort to the lowest common denominator like `obj`.
* Types are actually enforced, so mistakes can be caught early.
* They allow representing more than one type
* Because they are enforced, type information is less likely to become outdated or miss edge-cases.
* Types are checked during inheritance, enforcing the Liskov Substitution Principle.

```fsharp
let distance(x: (Point|Location), y: (Point|Location)) = ...
```

```fsharp
type RunWork = RunWork of args: string
type RequestProgressUpdate = RequestProgressUpdate of workId: int
type SubscribeProgressUpdate = SubscribeProgressUpdate of receiver: string
type WorkerMessage = (RunWork | RequestProgressUpdate)
type WorkManagerMessage = (RunWork | SubscribeProgressUpdate)

let processWorkerMessage (msg: WorkerMessage) =
    match msg with
    | :? RunWork as m -> ...
    | :? RequestProgressUpdate m -> ...
```

```fsharp
type Username = Username of string
type Password = Password of string
type UserOrPass = (Password | UserName) // UserOrPass is a type alias

// `getUserOrPass` is inferred to `unit -> UserOrPass`
let getUserOrPass () = if (true) then name :> UserOrPass else password :> UserOrPass

// `getUserOrPass2` binding is inferred to `unit -> (UserOrPass | Error)`
let getUserOrPass2 () = if isErr() then err :> (UserOrPass | Error) else getUserOrPass() :> _
```

The definition of operators for types becomes simpler.

```fsharp
type Decision =
    // Fields
    
    abstract member (*) (a: float, b: Decision) : LinearExpression =
        // member body
    abstract member (*) (a: Decision, b: Decision) : LinearExpression =
        // member body
```

Becomes

``` fsharp
type Decision =
    // Fields
    
    abstract member (*) (a: (float|Decision), b:Decision) : LinearExpression =
        match a with
        | :> float as f -> // float action
        | :> Decision as d -> // Decision action
```

The maintenance of libraries with large numbers of operator-overloads becomes simpler because the behavior is defined in one place.

# Detailed design
[design]: #detailed-design

## Subtyping rules
[subtyping]: #subtyping-rules

* Erased union are commutative and associative:

    ```fsharp
    (A | B) =:= (B | A)
    (A | (B | C)) =:= (( A | B ) | C)
    ```

    *`=:=` implies type equality and interchangable in all context*

* If `A :> C` and `B :> C` then `(A | B) :> C` where `T :> U` implies T is subtype of C;

### Hierarchies in Types
[hierarchy]: #hierarchy-types

For cases where, all cases in the union are disjoint, all cases must be exhaustively checked during pattern matching.
However in situations where one of the case is a supertype of another case, the super type is chosen discarding the derived cases.

For example:
`I` is the base class, which class `A` and class `B` derives from. `C` and `D` subsequently derives from `B`

```fsharp
   ┌───┐
   │ I │
   └─┬─┘
  ┌──┴───┐
┌─┴─┐  ┌─┴─┐
│ A │  │ B │
└───┘  └─┬─┘
      ┌──┴───┐
    ┌─┴─┐  ┌─┴─┐
    │ C │  │ D │
    └───┘  └───┘

type (A|B|I) // equal to type definition for I, since I is supertype of A and B
type (A|B|C) // equal to type (A|B), since B is supertype of C
type (A|C)   // disjoint as A and C both inherit from I but do not have relationship between each other.
```

## Type inference
[inference]: #type-inference

Erased Union type is explicitly inferred meaning that at least one of the types in an expression must contain the erased union type.

i.e something like the following is invalid:

```fsharp
let intOrString = if true then 1 else "Hello" // invalid
```

However the following is valid:

```fsharp
// inferred to (int|string)
let intOrString = if true then 1 :> (int|string) else "Hello" :> _
```

This respects the rules around where explicit upcasting is required including cases despite where type information being available. Although the latter might change depending on the outcome of [fslang-suggestion#849](https://github.com/fsharp/fslang-suggestions/issues/849)

## Exhaustivity checking
[exhaustivity]: #exhaustivity-checking

If the selector of a pattern match is an erased union type, the match is considered exhaustive if all parts of the erased union are covered. There would be no need for fallback switch.

```fsharp
let prettyPrint (x: (int8|int16|int64|string)) =
    match x with
    | :? (int8|int16|int64) as y -> prettyPrintNumber y
    | :? string as y -> prettyPrintNumber y
```

The above is the same as F# in current form:

```fsharp
let prettyPrint (x: obj) =
    match x with
    | :? int8 | :? int16 | :? int64 as y -> prettyPrintNumber y
    | :? string as y -> prettyPrintNumber y
```

Similarly the following would also be considered exhaustive:

```fsharp
let prettyPrint (x: (int8|int16|int64|string)) =
    match x with
    | :? System.ValueType as y -> prettyPrintNumber y // int8, int16 and int64 are subtype of ValueType
    | :? string as y -> prettyPrintNumber y
```

## Erased Type
[erasedtype]: #erased-type

The IL wrapping type for `(A | B)` is the _smallest intersection type_ of base
types of `A` and `B`. For example:

```fsharp
// wrapping type is System.Object
type IntOrString = (int|string)
// wrapping type is System.ValueType
type IntOrString = (int8|int16|float)
type I = interface end
type A = inherit I
type B = inherit I
// I is the wrapping type
type AorB = (A|B) 

type I2 = interface end
type C = inherit I inherit I2
type D = inherit I inherit I2
// Both I or I2 could be potential wrapping type. The compiler would choose I2 since its the earliest ancestor
type CorD = (C|D) 
```

# Drawbacks
[drawbacks]: #drawbacks

TBD

# Alternatives
[alternatives]: #alternatives

TBD

# Unresolved questions
[unresolved]: #unresolved-questions

* Initial implementation should not allow for using uom in erased unions when the underlying primitive is already part of union ?

    ```fsharp
    type [<Measure>] userid
    type UserId = int<userid>
    type IntOrUserId = (int|UserId)
    ```

    Alternatively we could just warn when such constructs are used.

* Initial implementation should not allow using static or generic type arguments in erased unions?

    ```fsharp
    type StringOr<'a> = ('a | string)
    ```

* Initial implementation should not allow for common members of the erased unions to be exposed without upcasting?

    ```fsharp
    type IShape =
        abstract member What: string

    type Circle =
        | Circle of r: float
        interface IShape with
            member _.What = "Circle"

    type Square =
        | Square of l: float
        interface IShape with
            member _.What = "Square"

    /// example
    let shape = Circle(1.0) :> (Circle | Square) // erased type IShape
    let what = shape.What // error
    let what = (shape :> IShape).What // ok
    ```

* Should exhaustive check in instance clause be implemented in normal circumstances? https://github.com/dotnet/fsharp/issues/10615
