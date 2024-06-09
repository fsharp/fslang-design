# F# RFC FS-1069 - Implicit yields

The design suggestion [Implicit yields](https://github.com/fsharp/fslang-suggestions/issues/643) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [ ] Discussion
* [x] [Implementation](https://github.com/dotnet/fsharp/pull/6304)


# Summary
[summary]: #summary

This allows implicit `yield` in list, array, sequence and those computation expressions supporting Yield/Combine/Zero/Delay.
This makes F# a nicer "templating" language. This applies especially for "views" in F# frameworks such as Fable and Fabulous.

The feature uses a type directed rule to detect side-effect statements such as `printfn` inside a computation expression,
and is subject to some possible corner-case backwards-compat concerns, discussed below.

Example:
```fsharp
let view onLogout (model:Model) =
    div [ centerStyle "row" ] [
          yield viewLink Page.Home "Home"
          if model <> None then
              yield viewLink Page.WishList "Wishlist"
          if model = None then
              yield viewLink Page.Login "Login"
          else
              yield buttonLink "logout" onLogout [ str "Logout" ]
        ]
```
becomes
```fsharp
let view onLogout (model:Model) =
    div [ centerStyle "row" ] [
          viewLink Page.Home "Home"
          if model <> None then
              viewLink Page.WishList "Wishlist"
          if model = None then
              viewLink Page.Login "Login"
          else
              buttonLink "logout" onLogout [ str "Logout" ]
        ]
```

This has a benefit that some irregularities in incrementally adjusting F# code are ironed out.  For example if you start with 
```fsharp
[ "Monday"
  "Tuesday"
  "Wednesday"
  "Thursday"
  "Friday"
  "Saturday"
  "Sunday"] 
```
and want to make the last two conditional, then with the feature **on** you just do this:
```fsharp
[ "Monday"
  "Tuesday"
  "Wednesday"
  "Thursday"
  "Friday"
  if includeWeekend then 
      "Saturday"
      "Sunday"] 
```
Without implicit yields you have to scatter `yield` across the expression:
```fsharp
[ yield "Monday"
  yield "Tuesday"
  yield "Wednesday"
  yield "Thursday"
  yield "Friday"
  if includeWeekend then 
      yield "Saturday"
      yield "Sunday"] 
```


#### Detailed Design

Implicit yields are activated for

1. List, array and sequence expressions that have no explicit yield (i.e. you can't mix implicit and explicit yields).  Using `yield!` is allowed.

2. Computation expressions that have no explicit `yield` and where the builder supports the necessary methods for yielding, i.e. `Yield`, `Combine`, `Delay` and `Zero`.

When implicit yields are activated, 

1. for a construct `expr1; expr2` in a computation, `expr1` is checked without an expected type (as today).  If the resulting type of the expression unifies with `unit` without warning, then `expr1` is interpreted as a sequential expression.  Otherwise, it is interpreted as an implicit yield.

2. similarly, for non-computation construct `expr` in a leaf position of a computation expression, `expr` is checked without an expected type (as today).  If the resulting type of the expression unifies with `unit` without warning, then `expr` is interpreted as the expression followed by yielding no results.  Otherwise, it is interpreted as an implicit yield of a singleton result.
 
There is no corresponding "implicit return".

#### Possible compat concerns

Some existing F# code generates a warning when values are ignored. For example, currently:
```
[ 1; yield 2 ] 
```

correctly gives
```
      [ 1; yield 2 ]
  ------^

stdin(1,7): warning FS0020: The result of this expression has type 'int' and is implicitly ignored. Consider using 'ignore' to discard this value explicitly, e.g. 'expr |> ignore', or 'let' to bind the result to a name, e.g. 'let result = expr'.
```

Because an explicit yield is present, this continues to generate a warning saying the `1` is ignored and
discarded (the expression is treated like a statement).  

There is still a (presumably very rare) backwards compat concern for cases where values are currently
being ignored/discarded *and* the  list/array/sequence/computations only uses `yield!`.  For example

    [ doSomethingThatReturnsAValueButCurrentlyDiscardsIt(); yield! someThingsToYield() ] 

In this case, implicit yields are activated because there is no explicit `yield`.  However the
expression `doSomethingThatReturnsAValueButCurrentlyDiscardsIt()` would now be interpreted as a yield.  This is
likely to give rise to a type error (it's unlikely that the function returns the same element type as `someThingsToYield()`),
but if there is no type error it will yield an additional element.

The initial working assumption is that such cases will be extraordinarily rare - given that the code reports a warning today.
For this reason, in the balance it seems ok to change the interpretation of these cases, subject to
a `/langversion:5.0` flag.

## Interaction with generating unit-values

F# fixed-list syntax can be used to generate unit values:
```fsharp
let xs = [ (); () ]
```
Explicit yield syntax can likewise be used:
```fsharp
let xs = [ yield (); yield () ] // result is [ (); () ]
```
When `match` is used in a computed list expression, with or without explicit yields, the syntax `-> ()` is used to indicate "don't yield anything on this branch".  For example
```fsharp
let f x =
    [ yield 1
      match x with
      | 1 -> ()
      | 2 -> yield 2 ]
```
This also applies to unit-generating lists:
```fsharp
let f x =
    [ yield ()
      match x with
      | 1 -> ()
      | 2 -> yield () ]

f 1 // generates [()]
f 2 // generates [(); ()]
```
Additionally, unit-valued sequential statements can occur, with explicit yields these do not generate values:
```fsharp
let f x =
    [ yield ()
      match x with
      | 1 -> 
          printfn "nothing"
          ()
      | 2 -> yield () ]

f 1 // generates [()]
f 2 // generates [(); ()]
```

When yields are implicit, these cases are less obvious. In general, **implicit yields are not recommended for use with unit-generating lists**. No
warning is given to this effect (though one may be added in later releases). To understand what happens if implicit yields are used
with unit-generating lists, the rules to remember are

1. `-> ()` still generates no values
2. Given a sequential `expr1; expr2`, if the type of `expr1` unifies with `unit` then no values are produced

Thus this makes it essentially impossible to actually generate unit-values from lists that contain control constructs and which
attempt to use implicit yields:
```fsharp
let f x : unit list =
    [ ()
      match x with
      | 1 -> 
          printfn "nothing"
          ()
      | 2 ->
          () 
      | _ -> 
          ()
          ()
          ]

f 1 // generates []
f 2 // generates []
f 3 // generates []
```
From the results, you can see that when implicit yields and control constructs are used, the interpretation of unit-typed
elements of the control structure is "execute and ignore" - that is, unit-typed expressions are treated as statements
rather than expressions-with-yield-of-value-effect.  Hence explicit yields should always be used when generating unit results.
Thus there is a mixed-interpretation of "control" constructs in the F# syntax.

In practice, generating unit-values computationally seems not to arise, and if it does the option of using explicit yields for clarity is available.


## Interaction with recursive functions

When yields are implicit, in some cases expressions in computed list expressions which might appear to be yields can be given "statement" interpretation instead of "yield" interpretation, giving rise to computed list expressions that yield nothing.   Any useful code that does this will almost always give a later type checking error. However that error can be hard to understand.

For example, consider this code, transforming a tree labelled with integers to a tree labelled with strings:

```fsharp
type Tree1 = Node1 of int * Tree1 list
type Tree2 = Node2 of string * Tree2 list

let rec generateThing (Node1 (n, children)) =
    let things =
        [ for child in children do 
             generateThing child ]
    Node2 (string n, things)
```

Here the call to `generateThing` in the definition of `things` is given statement interpretation because its return type (otherwise unknown at that point) unifies with `unit`, and an error is reported at the end of the function.  

The solution here is to annotate the return type or add an explicit `yield`, e.g. 

```fsharp
let rec generateThing (Node1 (n, children)) =
    let things =
        [ for child in children do 
             yield generateThing child ]
    Node2 (string n, things)
```

While sound, this can be confusing, which is why it's being called out here. See https://github.com/dotnet/fsharp/issues/12194

## Code samples

See above

# Drawbacks
[drawbacks]: #drawbacks

1. This feature may cause confusion about what is a `yield` and what is a side-effecting operation

2. This feature still requires the use of `yield!` and the loss of symmetry between `yield` and `yield!` may make the
   use of these features confusing for beginners.
   
   Response: belief is that `yield!` is rare, and the benefits of clarity from implicit yield are greater than the potential
   for loss of symmetry.
   
3. The use of a type-directed rule may cause problems

4. The decision to allow implicit yields only when there is no explicit yield means adding a single `yield` to a computation
   expression may cause a substantial "non-incremental" change in the interpretation and checking of the construct.

5. The backwards compat corner cases mean that a `/langversion:5.0` flag is needed.  Adding this flag is plan-of-record but
   it still needs to be done, and must be done carefully.

# Alternatives
[alternatives]: #alternatives

The main alternative is "don't do this" and continue to require explicit `yield`

# Compatibility
[compatibility]: #compatibility

This is a non-breaking change.

# Unresolved questions
[unresolved]: #unresolved-questions

TBD
