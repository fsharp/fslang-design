# F# RFC FS-1084 - The `UnknownEnum` pattern

<!--The design suggestion [The `UnknownEnum` pattern](https://github.com/fsharp/fslang-suggestions/issues/822) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.
-->

- [ ] Approved in principle
- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/822)
- [x] [RFC Discussion](https://github.com/fsharp/fslang-design/issues/446)
- [ ] Implementation (not started)

# Summary

A new active pattern should be added to FSharp.Core, named `UnknownEnum`, to match against undefined enum values.

# Motivation

Currently, for `match` expressions on enumerations (enums), the warning FS0104 which warns against unknown enum cases not being matched exists.

However, to silence this warning, one has to either use a wildcard pattern, which has the downside of preventing the warning FS0025 from being emitted when a new enum case is added to the enum type, or disable this warning entirely, which has the downside of a `MatchFailureException` being raised once an invalid value is matched against, potentially as a result of deserialization which the developer cannot control.

An `UnknownEnum` pattern would be an ideal solution to this, as it matches all cases that FS0104 would warn against, while keeping FS0025 warnings when a new case is added to the enum type. As a result, using `match` expressions on enums can be both safe, as in matching all potential values, and future-proof, as in keeping FS0025 warnings when a new case is added.

# Detailed design

A new active pattern, named `UnknownEnum` will be added to FSharp.Core.

In the compiler's view, it will match all enum members statically available. Therefore, the compiler will need to be updated to recognize this pattern and consider all unknown enum cases handled in match completeness analysis and IL generation. As a result, when this pattern is used, FS0104 will be silenced.

This pattern will also deconstruct the underlying value of the enum for concise logging and error reporting.

`match` expressions on enums can then use this pattern:
```fs
type E = A = 1 | B = 2
// No warnings
match enum<E> 0 with
| E.A -> "Case A"
| E.B -> "Case B"
| UnknownEnum x -> sprintf "Unknown case: %d" x // Unknown case: 0
```

FS0025 warnings will still be emitted as usual:
```fs
type E = A = 1 | B = 2 | C = 3
// FS0025: Incomplete pattern matches on this expression. For example, the value 'E.C' may indicate a case not covered by the pattern(s).
match enum<E> 0 with 
| E.A -> "Case A" 
| E.B -> "Case B" 
| UnknownEnum x -> sprintf "Unknown case: %d" x // Unknown case: 0
```

The warning message for incomplete matches on an enum value will be updated to explicitly guide the user to add an UnknownEnum case. Otherwise no one will ever know to use this.

To support dynamically invoking this active pattern, e.g. through reflection, a dynamic implementation will be provided in FSharp.Core.

```fs
type private UnknownEnumLookup<'Enum>() =
    static member val Values = typeof<'Enum>.GetEnumValues() :?> 'Enum[]
let (|UnknownEnum|_|) (enum:'Enum when 'Enum : enum<'Underlying>) =
    if Array.IndexOf(UnknownEnumLookup<'Enum>.Values, enum) < 0 then
        Some <| LanguagePrimitives.EnumToValue enum
    else None
```

For enumerations with the `Flags` attribute, use of this pattern will result in a warning.
_Unresolved: Is this necessary?_
```fs
[<Flags>]
type E = A = 1 | B = 2
// Warning: The UnknownEnum pattern will match all products of flags if they are not declared in the enumeration.
match enum<E> 0 with 
| E.A -> "Case A" 
| E.B -> "Case B" 
| UnknownEnum x -> sprintf "Unknown case: %d" x
```

# Drawbacks

This may promote usage of enums inside F#, whereas discriminated unions (DUs) are promoted as a safe alternative. However, enums still have valid usage in serialization and C# interoperation, and providing a safe and future-proof way of handling unknown enum cases will not encourage F# users to use enums instead of DUs, as every `match` expression on enums still have one more branch than DUs for unknown cases, which is less concise.  

# Alternatives

Also implementing `UnknownEnum` for enums with `FlagsAttribute` (flag enums). This will be added along with `HasFlag` and `EnumZero` active patterns to achieve complete matching on flag enums.
```fs
let inline (|EnumZero|_|) (value:'Enum when 'Enum :> enum<'Underlying>) = 
    if value = LanguagePrimitives.EnumOfValue LanguagePrimitives.GenericZero then Some () else None
let (|HasFlag|_|) (flag:'Enum) (value:'Enum when 'Enum :> System.Enum) =
    if value.HasFlag flag then Some () else None
let inline (|UnknownEnum|_|) (value:'Enum when 'Enum : enum<'Underlying>) =
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
match enum<E> 3 with
| UnknownEnum x -> sprintf "Unknown value: %d" x 
| HasFlag E.A & HasFlag E.B -> "Correct" // Correct
| HasFlag E.A
| HasFlag E.B
| HasFlag E.C 
| HasFlag E.D -> "Has flag" 
| EnumZero -> "No flag"
```
However, this results in a more complex implementation with `inline` code. Also, flag enums are typically used in high performance scenarios and such an implementation is slower than with bit operators combined with `if` expressions, thus discouraging its use.

Also, some flag enums have an `All` value which has all the flags in the enum set, like `0xffffffff` for a flag enum with `int32` as the underlying type. The presence of this value makes `UnknownEnum` useless as all values will be considered defined. Having a special case for these values only complicate the implementation for little added benefit.

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

Is the warning emitted when `UnknownEnum` is used with flag enums necessary? Will an error be better? Should a warning/error even be raised at all?
