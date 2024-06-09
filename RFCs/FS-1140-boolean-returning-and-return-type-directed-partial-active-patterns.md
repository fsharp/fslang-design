# F# RFC FS-1140 - Boolean-returning and return-type-directed partial active patterns

The design suggestion [Allow returning bool instead of unit option for partial active patterns](https://github.com/fsharp/fslang-suggestions/issues/1041) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1041)
- [x] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/16473)
- [ ] Design Review Meeting(s) with @dsyme and others invitees
- [Discussion](not yet)
<!-- - [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN) -->

# Summary

Allow returning `bool` instead of `unit option` for partial active patterns.

Allow omitting the `[<return: Struct>]` when writing voption-returning active patterns.

# Motivation

By implementing this feature, we can make unit-return partial active patterns even faster.

This will also bring a simpler way to write active patterns that just check a condition without explicit returning a value.


# Detailed design

1. When checking the definition or name of an active pattern, check if the return type is `_ voption` or `bool`. If so, use the type, otherwise assume it's returning a `_ option`. These new cases will be allowed.

```fsharp
// returning bool
let (|OddBool|_|) x = x % 2 = 1

// returning voption without [<return: Struct>]
let (|OddVOption|_|) x = if x % 2 = 1 then ValueSome() else ValueNone

// passing by parameter
let usingAP ((|IsA|_|): _ -> bool) = match "A" with IsA -> "A" | _ -> "Not A"
usingAP ((=) "A")
```

2. As bool will only bring "matched" or "not matched" results, the following cases will be disallowed:

```fsharp
// ----------- AP pass by parameter 

// current allowed: inferred as `string -> 'a option`, where `result` is the result of 
// (|IsA|_| "A")
let usingAP ((|IsA|_|): _ -> _) = 
  match "A" with 
  | IsA result -> "A" 
  | _ -> "Not A"

// disallowed, since `bool` will not wrap a result
let usingAP ((|IsA|_|): _ -> bool) = 
  match "A" with 
  | IsA result -> "A" 
  | _ -> "Not A"

// disallowed, special case of the previous one
let usingAP ((|IsA|_|): _ -> bool) = 
  match "A" with 
  | IsA result -> result
  | _ -> "Not A"

// current allowed: inferred as `string -> string option`, where `"to match return value"` 
// will be used to compare with the result of (|IsA|_| "A")
let usingAP ((|IsA|_|): _ -> _) = 
  match "A" with 
  | IsA "to match return value" -> "Matched"
  | _ -> "not Matched"

// disallowed, since `bool` will not wrap a result
let usingAP ((|IsA|_|): _ -> bool) = 
  match "A" with 
  | IsA "to match return value" -> "Matched"
  | _ -> "not Matched"

// ----------- AP using directly

let (|IsA|_|) x = x = "A"
// disallowed, same as above
match "A" with 
| IsA result -> "A" 
| _ -> "Not A"

// disallowed, same as above
match "A" with 
| IsA result -> result
| _ -> "Not A"

// disallowed, same as above
match "A" with 
| IsA "to match return value" -> "Matched"
| _ -> "not Matched"
```

The new following cases will be allowed:

```fsharp
// current not allowed: will raise a compiler error
let usingAP ((|IsA|_|): _ -> _ -> _) = 
  match "A" with
  | IsA "argument" -> "A"
  | _ -> "Not A"

// allowed, inferred as `string -> string -> bool`
let usingAP ((|IsA|_|): _ -> _ -> bool) = 
  match "A" with
  | IsA "argument" -> "A"
  | _ -> "Not A"
```

# Drawbacks

- More tricks for F# programmers to learn

# Alternatives

- Add a new partial-active-pattern-only attribute `BoolReturn` or `SimpleReturn` or other things to distinguish between `bool` returns and `option/voption` returns, and works like current `voption` approaching.

```fsharp
// returning bool
[<return: BoolReturn>]
let (|OddBool|_|) x = x % 2 = 1

// returning voption without [<return: Struct>]
[<return: Struct>]
let (|OddVOption|_|) x = if x % 2 = 1 then ValueSome() else ValueNone

// passing by parameter
let usingAP ((|IsA|_|): _ -> bool) = match "A" with IsA -> "A" | _ -> "Not A"
usingAP ((=) "A")
```

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change?
  > No.

* What happens when previous versions of the F# compiler encounter this design addition as source code?
  > Will not compile.

* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
  > Cannot use the new `bool`-return partial active patterns in pattern matching, but can directly calling them.

* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?

# Pragmatics

## Diagnostics

Please list the reasonable expectations for diagnostics for misuse of this feature.

## Tooling

Please list the reasonable expectations for tooling for this feature, including any of these:

* Debugging
  * Breakpoints/stepping
  * Expression evaluator
  * Data displays for locals and hover tips
* Auto-complete
* Tooltips
* Navigation and Go To Definition
* Colorization
* Brace/parenthesis matching

## Performance

Please list any notable concerns for impact on the performance of compilation and/or generated code

* For existing code
* For the new features

## Scaling

Please list the dimensions that describe the inputs for this new feature, e.g. "number of widgets" etc.  For each, estimate a reasonable upper bound for the expected size in human-written code and machine-generated code that the compiler will accept.

For example

* Expected maximum number of widgets in reasonable hand-written code: 100
* Expected reasonable upper bound for number of widgets accepted: 500

Testing should particularly check that compilation is linear (or log-linear or similar) along these dimensions.  If quadratic or worse this should ideally be noted in the RFC.

## Culture-aware formatting/parsing

Does the proposed RFC interact with culture-aware formatting and parsing of numbers, dates and currencies? For example, if the RFC includes plaintext outputs, are these outputs specified to be culture-invariant or current-culture.

# Unresolved questions

What parts of the design are still TBD?
