# F# RFC FS-1070 - Offside relaxations for function/constructor/member definitions

The design suggestion [Allow undentation for constructors](https://github.com/fsharp/fslang-suggestions/issues/724) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion and some related design relaxations.

* [x] Approved in principle
* [ ] Discussion
* [x] [Implementation](https://github.com/Microsoft/visualfsharp/pull/6314)


# Summary
[summary]: #summary

F#'s indentation rules are overly stringent for the argument lists of functions, implicit constructors, and static methods. This RFC relaxes the
rules for these cases by adding some permitted "undentations".

## Code Examples

Currently these all give the indentation warning `FS0058 Possible incorrect indentation: this token is offside...`:

```fsharp
let f(a:int,
    b:int, c:int, // warning
    d:int) =
```
and:
```fsharp
type OffsideCheck(a:int,
        b:int, c:int, // warning
        d:int) =
```
and:
```fsharp
    static member M(a:int,
        b:int, c:int, // warning
        d:int) = 1
```

In each case `a` sets the offside line and `b` needs to be aligned with `a` to avoid the warning.

Allowing undentation in these three cases would remove the warning.

It would also allow 

```fsharp
let f(
    a:int, // warning: `a` needs to be aligned with or after `(`.
    b:int, c:int) =

type OffsideCheck(
        a:int, // warning: `a` needs to be aligned with or after `(`.
        b:int, c:int) =
    static member M(
        a:int,
        b:int, c:int, // warning: `a` needs to be aligned after `(`.
        d:int) = 1
```

#### Possibly related: `with get` and `with set`
The following example may also be relaxed:

```fsharp
type C() = 
    static member P with get() = 
      1 // warning -- offside of 'member'
```
In all the above cases an andentation is added.


## Detailed Design

Function definitions, constructor definitions, and member definitions taking inputs, should be added to the list of permitted undentations in the F# language spec.
Undentation when applying functions, and when defining curried functions, is already possible but should be added to the spec to confirm this.

In the language of the spec, the undentation is premitted from the bracket starting a sequence of arguments in a definition, but the block must not undent past other offside lines.

One example from the examples above will suffice to add to the spec.

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
