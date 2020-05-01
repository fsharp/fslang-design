# Allow using `nameof` as a constant in pattern matching

The design suggestion [Allow using nameof as a constant in pattern matching](https://github.com/fsharp/fslang-suggestions/issues/841) has been marked "approved in principle". This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/841)
* [x] [RFC Discussion](https://github.com/fsharp/fslang-design/issues/451)
* [x] [Implementation: in progress](https://github.com/dotnet/fsharp/pull/8754)

# Summary

[FS-1003](https://github.com/fsharp/fslang-design/blob/master/preview/FS-1003-nameof-operator.md) introduces the `nameof` function, which creates a string literal that contains the source name of the value, type, namespace or module passed as argument. This RFC adds support for using `nameof` as a string constant in pattern matching.

# Motivation

Being able to match against the name of a language entity can be useful, for example, in serialization. Let's take the example of an event storage system in which each event has a type, which is a string, and associated data, which is binary data. It would be quite natural to represent events in the application using a discriminated union:

```fsharp
/// The event storage API
type RecordedEvent = { EventType: string; Data: byte[] }

/// A concrete type used by a given application:
type MyEvent =
    | A of AData
    | B of BData
```

Serialization can be handled by using `nameof` on the union cases:

```fsharp
let serialize (e: MyEvent) : RecordedEvent =
    match e with
    | A adata -> { EventType = nameof A; Data = JsonSerializer.Serialize<AData> adata }
    | B bdata -> { EventType = nameof B; Data = JsonSerializer.Serialize<BData> bdata }
```

And conversely, with this RFC, deserialization can be handled by matching against `nameof`:

```fsharp
let deserialize (e: RecordedEvent) : MyEvent =
    match e.EventType with
    | nameof A -> A (JsonSerializer.Deserialize<AData> e.Data)
    | nameof B -> B (JsonSerializer.Deserialize<BData> e.Data)
    | t -> failwithf "Invalid EventType: %s" t
```

Before this RFC, the existing way of approaching this problem in F# is to either:

* use string literals `"A"`, `"B"`, and risk typos or mismatch with actual union names.

* use `if e.EventType = nameof A then ... elif e.EventType = nameof B then ...` which is repetitive.

# Detailed design

A new pattern syntax is available: `nameof expr`.

The rules for resolving both the name `nameof` and the expression, type, namespace or module `expr` are identical to those for the expression `nameof expr`, [as detailed in RFC FS-1003](https://github.com/fsharp/fslang-design/blob/master/preview/FS-1003-nameof-operator.md).

Once resolved, the pattern `nameof expr` is equivalent to a literal string pattern whose value is the name of `expr` as defined in FS-1003.

Example code:

```fsharp
module SomeNamespace.SomeModule

let someFunction x = x + 1

type SomeType = { x: int }

let test (e: string) =
    match e with
    | nameof someFunction -> "e is \"someFunction\""
    | nameof e -> "e is \"e\""
    | nameof SomeType -> "e is \"SomeType\""
    | nameof SomeNamespace -> "e is \"SomeNamespace\""
    | nameof SomeNamespace.SomeModule -> "e is \"SomeModule\""
    | "e" -> """This line gives compile-time "warning FS0026: This rule will never be matched"
                because "e" was already matched by `nameof e` above"""
    | _ -> "e is something else"
```

# Drawbacks

None

# Alternatives

The main alternative is "don't do this", but since `nameof expr` is treated as a string literal in most contexts, it is an odd exception to not allow it as a pattern.

# Compatibility

This is not a breaking change:
* `nameof expr` is never a valid pattern before this RFC.
* Since `nameof expr` is compiled into a string literal, occurrences can be consumed in compiled form by older compilers transparently.

# Unresolved questions

None
