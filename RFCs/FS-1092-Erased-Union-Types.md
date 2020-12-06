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
type UserOrPass = (Password | UserName)

// userOrPass binding is inferred to UserOrPass type
let userOrPass = if (true) name :> UserOrPass else password :> UserOrPass
```

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

* If `A :> C` and `B :> C` then `(A | B) :> C` where `T :> U` implicies T is subtype of C;

## Type inference
[inference]: #type-inference

Erased Union type is explicitly inferred meaning that at least one of the type in an expression must contain the erased union type.

i.e something like the following is invalid:

```fsharp
let intOrString = if true then 1 else "Hello" // invalid
```

However the following is valid:

```fsharp
// inferred to (int|string)
let intOrString = if true then 1 :> (int|string) else "Hello" :> _ 
```

This respects the rules around where explicit upcasting is required including cases despite where type information being available. Although the later might change depending on the outcome of [fslang-suggestion#849](https://github.com/fsharp/fslang-suggestions/issues/849)

## Exhaustivity checking
[exhaustivity]: #exhaustivity-checking

If the selector of a pattern match is an erased union type, the match is considered exhaustive if all parts of the erased union are covered. There would be no need for fallback switch.

```fsharp
let prettyPrint (x: (int8|int16|int64|string)) =
    match x with
    | :? (int8|int16|int64) as y -> prettyPrintNumber y
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

The erased type for `(A | B)` is the _smallest intersection type_ of base
types of `A` and `B`.

# Drawbacks
[drawbacks]: #drawbacks

TBD

# Alternatives
[alternatives]: #alternatives

TBD

# Unresolved questions
[unresolved]: #unresolved-questions


* Initial implementation should not allow for using uom in erased unions? 

    ```fsharp
    type [<Measure>] userid
    type UserId = int<userid>
    type IntOrUserId = (int|UserId)
    ```

* Initial implementation should not allow using generic type arguments in erased unions?

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
* Should exhaustive check in instance check be implemented across normal circumstances? https://github.com/dotnet/fsharp/issues/10615