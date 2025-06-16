# F# RFC FS-1329 - Allow typed bindings in CE let!s without parentheses

The design suggestion [Allow typed bindings in CE let!s without parentheses](https://github.com/fsharp/fslang-suggestions/issues/1329) has been marked "approved in principle."

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1329)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/10697)
- [x] [Discussion](https://github.com/fsharp/fslang-design/discussions/XXXX)

# Summary

Allow computation expression (CE) bindings type annotations without requiring parentheses:
* `let!` `and!` to accept type annotations on simple patterns without requiring parentheses, making them consistent with regular `let` bindings. 
* `use!` to accept type annotations on simple patterns without requiring parentheses, making them consistent with regular `use` bindings.

# Motivation

Currently, when using type annotations with `let!`, `use!`, and `and!` bindings in computation expressions, parentheses are always required around the pattern with its type annotation, even for simple identifiers. This differs from regular `let` and `use` bindings where parentheses are only required for complex patterns.

This inconsistency is:
- **A syntactical foot-gun** that is frustrating and confusing to users
- **Inconsistent** with the general design of F# where `let!` should behave similarly to `let` where possible
- **A common source of compiler errors** for developers working with computation expressions
- **Particularly problematic for F# newcomers** who are already grappling with computation expressions
- **Unhelpful error messages**: Getting "Unexpected symbol ':'" doesn't immediately suggest that parentheses are needed

# Detailed design

## Scenario: `let!` bindings

`let!` bindings with type annotations on simple identifiers require parentheses:

```fsharp
// Before
async {
    let! (data: byte[]) = readAsync()
    let! (x: int) = async { return 42 }
}

// After  
async {
    let! data: byte[] = readAsync()
    let! x: int = async { return 42 }
}
```

## Scenario: `use!` bindings

`use!` bindings with type annotations on simple identifiers require parentheses:

```fsharp
// Before
task {
    use! (conn: SqlConnection) = openConnectionAsync()
    use! (resource: IDisposable) = openResourceAsync()
}

// After
task {
    use! conn: SqlConnection = openConnectionAsync()
    use! resource: IDisposable = openResourceAsync()
}
```

## Scenario: `and!` bindings

`and!` bindings with type annotations on simple identifiers require parentheses:

```fsharp
// Before
async {
    let! (userId: int) = getUserIdAsync()
    and! (permissions: Permission list) = getPermissionsAsync()
    and! (profile: UserProfile) = getProfileAsync()
}

// After
async {
    let! userId: int = getUserIdAsync()
    and! permissions: Permission list = getPermissionsAsync()
    and! profile: UserProfile = getProfileAsync()
}
```

## Scenario: Tuple patterns

Tuple patterns with type annotations require parentheses (same as regular `let`):

```fsharp
// Before (and still required after)
async {
    let! (a, b): int * string = asyncTuple()
    and! (x, y): float * bool = asyncPair()
}

// After - parentheses still required (consistent with regular let)
async {
    let! (a, b): int * string = asyncTuple()
    and! (x, y): float * bool = asyncPair()
}

Tuple tuples with each element having direct type annotations require parentheses (same as regular `let`):

```fsharp
let a: int, b: string  = (5, 3) // Not allowed
let! a: int, b: string  = (5, 3) // Not allowed

let (a, b): int * int = (5, 3) // Allowed
let! (a, b): int * int = (5, 3) // Allowed
let a, b: int * int = (5, 3) // Allowed
let! a, b: int * int = (5, 3) // Allowed
let (a: int, b: string): int * int  = (5, 3) // Allowed
let! (a: int, b: string): int * int  = (5, 3) // Allowed

// Before (and still required after)
async {
    let! (a: int, b: string): int * string = asyncTuple()
    and! (x: int, y: bool): float * bool = asyncPair()
}

// After - parentheses still required (consistent with regular let)
async {
    let! (a: int, b: string): int * string = asyncTuple()
    and! (x: int, y: bool): float * bool = asyncPair()
}
```

## Scenario: Record patterns

Record patterns with type annotations don't require parentheses:

```fsharp
// Before - parentheses required
async {
    let! ({ Name = name; Age = age }: Person) = asyncPerson()
    and! ({ Id = id }: User) = asyncUser()
}

// After - no parentheses needed
async {

    let! { Name = name; Age = age }: Person = asyncPerson()
    and! { Id = id }: User = asyncUser()
}
```

## Scenario: Union patterns

Union patterns with type annotations require parentheses (same as regular `let`):

```fsharp
// Before - parentheses required
async {
    let! (Union value: int option) = asyncOption()
    and! ((Union result): Result<string, Error>) = asyncResult()
}

// After - no parentheses needed (consistent with regular let)
async {
    let! Union value: int option = asyncOption()
    and! Union result: Result<string, Error> = asyncResult()
}
```

## Scenario: As patterns

As patterns with type annotations require parentheses (same as regular `let`):

```fsharp
// Before - require parentheses
async {
    let! ((x as y): int) = asyncInt()
    let! (x as y: int) = asyncInt()
    and! ((a as b): string) = asyncString()
    and! (x as y: int) = asyncInt()
}

// After - no parentheses needed (consistent with regular let)
async {
    let! (x as y): int = asyncInt()
    let! x as y: int = asyncInt()
    and! (a as b): string = asyncString()
    and! x as y: int = asyncInt()
}
```

## Scenario: Array and list patterns

Array and list patterns with type annotations don't require parentheses:

```fsharp
// Before - parentheses required
async {
    let! ([| first; second |]: int array) = asyncArray()
    and! (head :: tail: string list) = asyncList()
}

// After - no parentheses needed (consistent with regular let)
async {
    let! [| first; second |]: int array = asyncArray()
    and! head :: tail: string list = asyncList()
}
```

**Key principle**: After this change, CE bindings follow the exact same parenthesization rules as regular `let` bindings.

## Grammar Changes

The parser has been extended to accept type annotations in CE binding patterns without parentheses. The grammar for CE bindings has been updated to accept:

```
cexpr = 
    | ...
    | 'let!' pat ':' type '=' expr 'in' cexpr
    | 'use!' pat ':' type '=' expr 'in' cexpr
    | ...

moreBinders =
    | 'and!' pat ':' type '=' expr 'in' moreBinders
    | ...
```

Where previously only parenthesized patterns with type annotations were accepted:

```
    | 'let!' '(' pat ':' type ')' '=' expr 'in' cexpr
    | 'and!' '(' pat ':' type ')' '=' expr 'in' moreBinders
```

## Implementation Details

The implementation in the parser (`pars.fsy`) adds new rules for each binding type:

1. For `let!` and `use!` bindings:
   - Regular CE bindings with `opt_topReturnTypeWithTypeConstraints`
   - Offside-sensitive CE bindings with `opt_topReturnTypeWithTypeConstraints`

2. For `and!` bindings:
   - Regular `and!` bindings with `opt_topReturnTypeWithTypeConstraints`
   - Offside-sensitive `and!` bindings with `opt_topReturnTypeWithTypeConstraints`

## Compatibility

* Is this a breaking change?
  * No. This change only allows syntax that was previously rejected by the compiler.
  
* What happens when previous versions of the F# compiler encounter this design addition as source code?
  * Older compiler versions will continue to emit error FS0010 when encountering type annotations without parentheses in CE bindings.

* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
  * This is a purely syntactic change that doesn't affect the compiled output. Older compiler versions will be able to consume binaries without issue.

* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?
  * N/A - This is a syntactic change only.

# Pragmatics

## Diagnostics

The existing error message (FS0010: Unexpected symbol ':' in expression) could be improved when encountered in the context of a CE binding to suggest adding parentheses (for older compilers) or updating the compiler version.

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
  - [UnnecessaryParenthesesDiagnosticAnalyzer](https://github.com/dotnet/fsharp/blob/main/vsintegration/src/FSharp.Editor/Diagnostics/UnnecessaryParenthesesDiagnosticAnalyzer.fs) should be updated to recognize that parentheses are not needed for type annotations in CE bindings, and should not suggest adding them.

## Performance

<!-- Please list any notable concerns for impact on the performance of compilation and/or generated code -->

* No performance or scaling impact is expected.

## Scaling

<!-- Please list the dimensions that describe the inputs for this new feature, e.g. "number of widgets" etc.  For each, estimate a reasonable upper bound for the expected size in human-written code and machine-generated code that the compiler will accept. -->

* N/A.

## Culture-aware formatting/parsing

Does the proposed RFC interact with culture-aware formatting and parsing of numbers, dates and currencies? For example, if the RFC includes plaintext outputs, are these outputs specified to be culture-invariant or current-culture.

* No.

# Unresolved questions

* None.