# F# RFC FS-1068 - Open static classes

The design suggestion [Open static classes](https://github.com/fsharp/fslang-suggestions/issues/383) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [ ] [Discussion thread](https://github.com/fsharp/fslang-design/issues/352)
* [x] [Implementation](https://github.com/Microsoft/visualfsharp/pull/6309)


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
*  There is a question as to whether we add `[<Static>]` or `static type C = `... 

* The RFC will explain the pros and cons of this feature (and the potential for its abuse).

* The implementation is not large but intrudes a little on name resolution and we should take care to assess potential ramifications of those changes

* The design has an interaction with type providers: type providers can provide static classes, hence this would allow type providers to provide "unqualified" names.  This is no doubt useful for some projections where the natural thing is unqualified, e.g. the "R" type provider always required `R.plot` etc.

* The design has an interaction with the F#-only feature "static" extension members, e.g. this works:

```fsharp
type System.Math with 
    static member Pi = 3.1415

open System.Math
Pi
```


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

The main alternative is "don't do this"

# Compatibility
[compatibility]: #compatibility

This is a non-breaking change.

# Unresolved questions
[unresolved]: #unresolved-questions

* [ ] Resolve how static classes are defined in F#
* [ ] Respect and test for RequireQualifiedNameAttribute(true) on a static class, that prevents it being opened, or at least gives a warning, as for F# modules.


