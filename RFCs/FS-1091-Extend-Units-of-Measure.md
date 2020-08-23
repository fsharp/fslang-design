# F# RFC FS-1091 - Extend Units of Measure to Include More Numeric Types

The design suggestion [Extend Units of Measure to Include Unsigned Integers](https://github.com/fsharp/fslang-suggestions/issues/901) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] Approved in principle
- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/901)
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/9978)
- [ ] Design Review Meeting(s) with @dsyme and others invitees
- [ ] [Discussion](https://github.com/fsharp/fslang-design/issues/PLEASE-ADD-A-DISCUSSION-ISSUE-AND-LINK-HERE)_(n.b. pending the acceptance of this RFC)_

# Summary

This feature builds on the existing implementation for Units of Measure, by extending it to support additional numeric types.

Consider the following example:

```fsharp
let weight = 69.275<Kg>
// val weight : float<Kg>

let age = 25<days>
// val age : int<days>

let questionable_age = -3<days>
// val questionable_age : int<days>

(* We'd really like to express `age` as a non-negative integer *)

let better_age = 3u<days>
//  -------------^^^^^^^^
// error FS0636: Units-of-measure supported only on float, float32, decimal and signed integer types
```

Were this RFC to be implemented, the final line above would compile.
Thus, allowing correct expression of both the numeric type and the units of measure.

At a minimum, this should include _unsigned integers_ (i.e. types expressing 8-bit, 16-bit, 32-bit, and 64-bit non-negative integer values).
Further, this RFC proposes also extending the support to _native integers_ (both signed and unsigned).
However, in the interest of controlling scope, this RFC only considers "primitive numeric types" (i.e. CLR value types which express numeric quantities... sorry, `BigInt`).

# Motivation

This work provides the following benefits:

1. Increase the overall "safety" by obviating the need to choose between a type or the ability to carry a measure.
1. Provide more consistency/uniformity across primitive numeric types.
1. Reduce the number of "quirks", or "gotchas!", one encounters when learning Units of Measure.

# Detailed design

The proposed approach is to simply use the same mechanism currently employed (e.g., for `float`s).
Specifically, within FSharp.Core, for each new "measure-bearing type" we add an appropriately-annotated alias to an existing numeric type.
For example, here's how a "measure-bearing" `float<m>` is currently defined (n.b. XMLDocs elided for clarity):

```fsharp
[<MeasureAnnotatedAbbreviation>]
type float<[<Measure>] 'Measure> = float
```

For a new type, such as, e.g. `uint<m>`, the work is largely a copy-paste job:

```fsharp
[<MeasureAnnotatedAbbreviation>]
type uint<[<Measure>] 'Measure> = uint
```

Additionally, for each new "measure-bearing type", we add a function to the `LanguagePrimitives` module.
For example, paired with the `uint<_>` abbreviation given above, we add:

```fsharp
let inline UInt32WithMeasure (f : uint) : uint<'Measure> = retype f
```

We then also extend the compiler (fsc) in the following ways (again, mostly copying existing values and tweaking them as needed):

+ Ensure that each new type can be resolved out of FSharp.Core (i.e. define and expose a `TyconnRef` in TcGlobals.fs).
+ Ensure the previous step is surfaced as TAST objects (in import.fs)
+ Make certain the type checker (TypeChecker.fs) considers the new TAST objects when validating measure-annotated code

Obviously, for completeness sake, an implementation of this RFC also needs to adjust various tests, documentation, and error messages as appropriate.

# Drawbacks

Main drawback? Somebody has to do the work.
It also represents a slight increase in the "surface area" of the language, which means more code to support and maintain.

# Alternatives

For the specific scope of this RFC, no other designs have been considered, as the obvious solution (of using what's already there) seems sufficient.
However, there are [other language suggestions][1] which advocate Units of Measure being evolved into a more general-purpose "type tagging" mechanism.
If any such efforts develop, it's quite likely the current implement (as detailed in this RFC) would need to be revised.

Further, if we simple choose _not_ to do this work, then we're no worse off than the current production version of F#.

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change? No. However, current code which uses hacks to simulate this feature (c.f. [here][2] or [here][3]) _may_ encounter issues.
* What happens when previous versions of the F# compiler encounter this design addition as source code? They will treat it as an invalid construct.
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries? They will treat it as an invalid construct.
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct? They will ignore it.

> **(n.b. several of the above answers are "well-educated guesses", which need proper vetting.)**

# Unresolved questions

The approach is solid. However there are two questions of scope:

1. Should "native integers" (i.e. `nativeint` and `unativeint`) be included?
1. For types which have multiple abbreviations, should _all_ abbreviations be covered?

In order to help explain/address the second question, the following table presents the current state of affairs:

 F# alias     | CLR Type                     | Currently supports units? | Target for this RFC?
--------------|------------------------------|---------------------------|-------------------------
 `float32`    | `System.Single`              | yes                       | no
 `single`     | "                            | _no_                      | _MAYBE?_
 `float`      | `System.Double`              | yes                       | no
 `double`     | "                            | _no_                      | _MAYBE?_
 `decimal`    | `System.Decimal`             | yes                       | no
 `sbyte`      | `System.SByte`               | yes                       | no
 `int8`       | "                            | _no_                      | _MAYBE?_
 `int16`      | `System.Int16`               | yes                       | no
 `int`        | `System.Int32`               | yes                       | no
 `int32`      | "                            | _no_                      | _MAYBE?_
 `int64`      | `System.In64`                | yes                       | no
 `byte`       | `System.Byte`                | _no_                      | **YES**
 `uint8`      | "                            | _no_                      | **YES**
 `uint16`     | `System.UInt16`              | _no_                      | _MAYBE?_
 `uint`       | `System.UInt32`              | _no_                      | **YES**
 `uint32`     | "                            | _no_                      | _MAYBE?_
 `uint64`     | `System.UIn64`               | _no_                      | **YES**
 `nativeint`  | `System.IntPtr`              | _no_                      | _MAYBE?_
 `unativeint` | `System.UIntPtr`             | _no_                      | _MAYBE?_


[1]: https://github.com/fsharp/fslang-suggestions/issues/563
[2]: http://www.fssnip.net/7UH/title/Generalized-Units-of-Measure-Revisited-using-method-overloading
[3]: http://www.fssnip.net/7UG/title/Generalized-Units-of-Measure-Revisited-using-SRTPs
