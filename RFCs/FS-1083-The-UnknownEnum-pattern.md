# F# RFC FS-1083 - The `UnknownEnum` pattern

<!--The design suggestion [The `UnknownEnum` pattern](https://github.com/fsharp/fslang-suggestions/issues/822) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.
-->

- [ ] Approved in principle
- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/822)
- [ ] [RFC Discussion](https://github.com/fsharp/fslang-design/issues/FILL-ME-IN)
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)

# Summary

A new active pattern should be added to FSharp.Core, named `UnknownEnum`, to match against undefined enum values.

# Motivation

Currently, for `match` expressions on enumerations (enums), the warning FS0104 which warns against unknown enum cases not being matched exists.

However, to silence this warning, one has to either use a wildacard pattern, which has the downside of preventing the warning FS0025 from being emitted when a new enum case is added to the enum type, or disable this warning entirely, which has the downside of a `MatchFailureException` being raised once an invalid value is matched against, potentially as a result of deserialization which the developer cannot control.

An `UnknownEnum` pattern would be an ideal solution to this, as it matches all cases that FS0104 would warn against, while keeping FS0025 warnings when a new case is added to the enum type. As a result, using `match` expressions on enums can be both safe, as in matching all potential values, and future-proof, as in keeping FS0025 warnings when a new case is added.

# Detailed design

A new active pattern, named `UnknownEnum` will be added to FSharp.Core:
```fs
let (|UnknownEnum|_|) (enum:'Enum when 'Enum : enum<'Value>) =
    if typeof<'Enum>.IsEnumDefined(enum) then None else Some <| LanguagePrimitives.EnumToValue enum
```
This pattern deconstructs the underlying value of the enum for concise logging and error reporting.

The compiler will need to be updated to recognize this pattern and consider all unknown enum cases handled in match completeness analysis. As a result, when this pattern is used, FS0104 will be silenced.

`match` expressions on enums can use this pattern:
```fs
type E = A = 1 | B = 2
// No warnings
match enum<E> 0 with E.A -> "Case A" | E.B -> "Case B" | UnknownEnum x -> sprintf "Unknown case: %d" x // Unknown case: 0
```

FS0025 warnings will still be emitted as usual:
```fs
type E = A = 1 | B = 2 | C = 3
// FS0025: Incomplete pattern matches on this expression. For example, the value 'E.C' may indicate a case not covered by the pattern(s).
match enum<E> 0 with E.A -> "Case A" | E.B -> "Case B" | UnknownEnum x -> sprintf "Unknown case: %d" x // Unknown case: 0
```

For enumerations with the `Flags` attribute, use of this pattern will result in an error.
_Unresolved: Is this necessary?_
```fs
[<Flags>]
type E = A = 1 | B = 2
// Error: The UnknownEnum pattern cannot be used on enumerations with the Flags attribute
match enum<E> 0 with E.A -> "Case A" | E.B -> "Case B" | UnknownEnum x -> sprintf "Unknown case: %d" x // Unknown case: 0
```

# Drawbacks

This may promote usage of enums inside F#, whereas discriminated unions (DUs) are promoted as a safe alternative. However, enums still have valid usage in serialization and C# interoperation, and providing a safe and future-proof way of handling unknown enum cases will not encourage F# users to use enums instead of DUs, as every `match` expression on enums still have one more branch than DUs for unknown cases, which is less concise.  

# Alternatives

Also implementing `UnknownEnum` for enums with `FlagsAttribute` (flag enums). This will be added along with `HasFlag` and `EnumZero` active patterns to achieve complete matching on flag enums.
```fs
let inline (|EnumZero|_|) (value:'Enum when 'Enum :> System.Enum) = 
    if value = LanguagePrimitives.EnumOfValue LanguagePrimitives.GenericZero then Some () else None
let (|HasFlag|_|) (flag:'Enum) (value:'Enum when 'Enum :> System.Enum) =
    if value.HasFlag flag then Some () else None
let inline (|UnknownEnum|_|) (value:'Enum when 'Enum : enum<'Value>) =
    if typeof<'Enum>.IsDefined(typeof<FlagsAttribute>, false) then
        match typeof<'Enum>.GetEnumValues() :?> 'Enum[] |> Array.reduce (|||) |> (~~~) &&& value with
        | EnumZero -> None
        | _ -> Some <| LanguagePrimitives.EnumToValue value
    elif typeof<'Enum>.IsEnumDefined(value) then
        None
    else Some <| LanguagePrimitives.EnumToValue value

[<Flags>]
type E = A = 1 | B = 2 | C = 4 | D = 8
// No warnings
match enum<E> 3 with UnknownEnum x -> sprintf "Unknown value: %d" x | HasFlag E.A | HasFlag E.B | HasFlag E.C | HasFlag E.D -> "Has flag" | EnumZero -> "No flag"
```
However, this results in a more complex implementation with `inline` code. Also, flag enums are typically used in high performance scenarios and such an implementation is slower than with bit operators combined with `if` expressions, thus discouraging its use.

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change?

  No.
* What happens when previous versions of the F# compiler encounter this design addition as source code?

  It will simply be interpreted as a normal active pattern without FS0104 suppression.
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?

  It will simply be interpreted as a normal active pattern without FS0104 suppression.
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?

  It will simply be interpreted as a normal active pattern without FS0104 suppression.

# Unresolved questions

Is the error emitted when `UnknownEnum` is used with flag enums necessary? Will a warning be enough instead? Should a warning/error even be raised at all?