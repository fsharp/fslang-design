# F# RFC FS-1070 - Offside relaxations for constructors and members

The design suggestion [Allow undentation for constructors](https://github.com/fsharp/fslang-suggestions/issues/724) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion and some related design relaxations.

* [x] Approved in principle
* [ ] Discussion
* [x] [Implementation](https://github.com/Microsoft/visualfsharp/pull/6314)


# Summary
[summary]: #summary

F#'s indentation rules are overly stringent for the argument lists of implicit constructors and static methods. This relaxs the
rules for these cases by adding some permitted "undentations".

#### Code Examples

For example, currently these all give a indentation warnings:
```fsharp
type OffsideCheck(a:int,
        b:int, c:int,  // warning offside of '('
        d:int) =
```
and:
```fsharp
type OffsideCheck(a:int,
   b:int, c:int,  // warning offside of '('
   d:int) =
```
and:
```fsharp
    static member M(a:int,
        b:int, c:int, // warning offside of 'member'
        d:int) = 1
```
and:
```fsharp
type C() = 
    static member P with get() = 
      1 // warning -- offside of 'member'
```
In all the above cases an andentation is added.


#### Detailed Design

TBD. Each newly permitted undenation is declarative in the implementation, and
simply needs to be translated into the terminology used in the F# language spec.

## Code samples

See above

# Drawbacks
[drawbacks]: #drawbacks

None

# Alternatives
[alternatives]: #alternatives

The main alternative is "don't do this" and continue to require indentation.

# Compatibility
[compatibility]: #compatibility

This is a non-breaking change.

# Unresolved questions
[unresolved]: #unresolved-questions

None
