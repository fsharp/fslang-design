# F# RFC FS-1068 - Open static classes

The design suggestion [Open static classes](https://github.com/fsharp/fslang-suggestions/issues/383) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.


# Summary
[summary]: #summary

Add support for opening static classes, e.g. `open System.Math`. For example

```fsharp
> open System.Math;;
> Min(1.0, 2.0);;
val it : float = 1.0

> Min(2.0, 1.0);;
val it : float = 1.0

> open System.Math;;
> Min(2.0, 1.0);;
val it : float = 1.0
```

# Motivation
[motivation]: #motivation

This greatly increases the expressivity of F# DSLs by allowing method-API facilities such as named arguments, optional
arguments and type-directed overloading to be used in the DSL design.

Additionally, important C# DSL APIs are starting to appear that effectively require this.

# Detailed design
[design]: #detailed-design

*  In .NET "static" classes  are abstract and sealed, containing only static members.  
* In F# these are currently declared through
```fsharp
[<AbstractClass; Sealed>] 
type C =
    static member Pi = 3.14
open C
Pi
```

TBD - rules on:

* Resolving overloads so that shadowing does not apply
* Resolving methods vs. properties and who wins
* Resolving methods vs. properties for type extensions

## OLD DESIGN NOTES - NOT A DESIGN FOLLOWS

* The implementation is not large but intrudes a little on name resolution and we should take care to assess potential ramifications of those changes

* The design has an interaction with type providers: type providers can provide static classes, hence this would allow type providers to provide "unqualified" names.  This is no doubt useful for some projections where the natural thing is unqualified, e.g. the "R" type provider always required `R.plot` etc.

* Adding `RequireQualifiedNameAttribute(true)` on a static class, prevents it being opened, as for F# modules.

* The design has an interaction with the F#-only feature "static" extension members, e.g. this works:

```fsharp
type System.Math with 
    static member Pi = 3.1415

open System.Math
Pi
```

* Only static classes may be opened.  In C#, any class can be opened and its static content made available.  This is not the case in F#.

## Code samples

See above
# Drawbacks
[drawbacks]: #drawbacks

This feature should only be used very very carefully. 
1. Code using this feature may be substantially less clear and harder to understand
2. If multiple APIs are `open`'d with conflicting method sets then severe usability problems will occur.
3. Adding methods to static classes can cause breaking changes or source code incompatibilities in client code using this feature.

# Alternatives
[alternatives]: #alternatives

* The main alternative is "don't do this"

* We have an alternative to allow any classes to be opened.  In C#, any class can be opened using `using static System.String`, and its static content made available.  We decided to restrict the feature to static classes only in F# as this appears to be its intended use case.

# Compatibility
[compatibility]: #compatibility

This is a non-breaking change.

# Unresolved questions
[unresolved]: #unresolved-questions

none

