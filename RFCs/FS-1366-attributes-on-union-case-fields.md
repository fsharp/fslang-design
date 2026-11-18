# F# RFC FS-1366 - Attributes on union case fields

The design suggestion [Allow attributes for discriminated union case fields](https://github.com/fsharp/fslang-suggestions/issues/684) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/684)
- [x] Approved in principle
- [ ] Implementation (not started)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

# Summary

A field of a union case or of an exception definition can have custom attributes.
The compiler emits them on the CLI property of the field.

```fsharp
type ErrorMessage =
    | Code of [<MaxLength(4)>] code: string
    | Text of [<MaxLength(150); JsonPropertyName("message")>] text: string
```

# Motivation

.NET libraries read attributes from properties to control serialization and validation. A record field can have attributes. A union case field cannot, so these libraries cannot be configured for it.

`System.Text.Json` serializes F# unions since [dotnet/runtime#125610](https://github.com/dotnet/runtime/pull/125610). It reads `JsonPropertyName`, `JsonConverter` and `JsonNumberHandling` from the property of each case field, but that pull request notes that "F# currently has no syntax for applying attributes to individual union case field parameters". Validators that read `System.ComponentModel.DataAnnotations` attributes from properties have the same gap.

# Detailed design

## Syntax

```
union-type-field :=
    attributes? type             -- unnamed union field
    attributes? ident ':' type   -- named union field
```

Union cases and exception definitions use this production, in implementation files and in signature files.
The attributes apply to the one field that follows them. An attribute list can be on the line before its field. An XML documentation comment for the field comes before the attributes.

```fsharp
type Shape =
    | Circle of [<Range(0.0, 100.0)>] float * string  // on `Item1` only
    | Label of
        /// The text to show.
        [<MaxLength(20)>]
        text: string

exception ParseError of [<JsonPropertyName("pos")>] position: int
```

The case form `ident ':' uncurried-sig` is for FSharp.Core only and does not change: the compiler continues to ignore attributes on its arguments.

## Target

1. The compiler MUST emit each attribute on the CLI property that represents the field.
2. The compiler MUST NOT emit the attribute on a backing field, or on a parameter of a generated member such as `New<Case>`.
3. The explicit target `property:` is permitted and has no effect. Any other explicit target is an error.
4. An attribute type whose `AttributeUsage` does not include `AttributeTargets.Property` is an error.
5. The attributes do not change how F# code constructs, matches, compares or prints the value.

```fsharp
type U =
    | A of [<property: Required>] a: string    // same as [<Required>]
    | B of [<field: NonSerialized>] b: string  // error: target
    | C of [<FieldOffset(0)>] c: int           // error: `FieldOffset` is valid on fields only
```

## Struct unions

A struct union has one CLI type for all cases, so fields of different cases that have the same name share one property. Today, the fields can share only when they have the same type (§8.8). With this RFC, they MUST also have equal attributes: each attribute of one field has an exact match (§13.1.1) on the other field, in any order. Fields with the same name that cannot share are an error, because one CLI type cannot have two properties with the same name. The rule includes the generated names `Item`, `Item1`, and so on.

```fsharp
[<Struct>]
type S =
    | A of [<MaxLength(4)>] code: string
    | B of [<MaxLength(4)>] code: string * count: int  // one property `code` with one `MaxLength(4)`

[<Struct>]
type T =
    | A of [<MaxLength(4)>] code: string
    | B of code: string                                // error: the attributes of `code` differ
```

## Signatures

§13.1.1 applies to these attributes as it applies to attributes on record fields.

## Other language features

Quotations, SRTP constraints and type providers do not observe these attributes.

# Changes to the F# spec

- **§8 Type Definitions**, grammar: `union-type-field` gets `attributes?`, as in [Syntax](#syntax).
- **§8.8 Struct Type Definitions**: next to the rule that fields with the same name have the same type, add the rule of [Struct unions](#struct-unions).
- **§13.1 Custom Attributes**, mapping to CLI metadata: add the rules of [Target](#target) for a field of a union case or of an exception definition.

# Drawbacks

- An attribute that is valid on fields only is accepted on a record field, where it goes to the backing field, but is an error on a union case field.
- Attribute lists make case declarations long.

# Alternatives

- **The rule for record fields.** Emit the attribute on the backing field when its target is `field` or its usage excludes properties. The compiler chooses the storage of a union, so attributes such as `FieldOffset`, `DefaultValue` and `NonSerialized` need more rules. This RFC makes these programs errors, so a later RFC can permit them.
- **A `param` target** that emits the attribute on the parameter of `New<Case>`. No consumer is known. A later RFC can add it.
- **Struct unions: merge the attributes** of fields that share a property. An attribute on one case then applies to the other cases, and attributes with `AllowMultiple = false` still need an error.
- **Struct unions: a generated property name** for a field that cannot share. Reflection and serializers then see a name that is not in the source.
- **No change.** Users replace the union with classes, or write a converter or validator for each type.

# Prior art

- C# [positional records](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-9.0/records.md#properties): the `property:` and `field:` targets move an attribute from a positional parameter to the generated property or its backing field.
- Rust: [fields of enum variants](https://doc.rust-lang.org/reference/items/enumerations.html) take attributes. [serde](https://serde.rs/field-attrs.html) reads them to rename, skip or convert a field.
- OCaml: [attributes](https://ocaml.org/manual/attributes.html) attach to the argument types of a constructor and to the fields of an inline record. ppx derivers read them.
- Kotlin: [use-site targets](https://kotlinlang.org/docs/annotations.html#annotation-use-site-targets) select the parameter, property, field or getter of a constructor property.

# Compatibility

* This is not a breaking change. The syntax is a syntax error today.
* The feature needs a new language version. With an earlier `--langversion`, the compiler reports that the feature is not available.
* An earlier compiler reads a binary that uses the feature. The attributes are ordinary custom attributes on properties, and the F# metadata format already stores property attributes for each field.
* FSharp.Core does not change.

# Interop

C# and reflection see the attributes on the existing CLI property of the field (§8.5.4). `UnionCaseInfo.GetFields()` already returns these properties. No planned C# feature interacts with this.

# Pragmatics

## Diagnostics

The compiler reports the errors of [Target](#target) at the attribute, and the error of [Struct unions](#struct-unions) at each field that has the name. There are no new warnings.

## Tooling

* FSharp.Compiler.Service: `SynField` already has an attribute list, and the parser now fills it for these fields. `FSharpField.PropertyAttributes` returns the checked attributes. Formatters must print the attributes.
* Completion, navigation and colorization in the attribute list work as for attributes on record fields.

## Performance

No notable change for compilation or for generated code.

## Scaling

Not applicable.

## Culture-aware formatting/parsing

Not applicable.

# Unresolved questions

- The suggestion names union case fields only. Exception fields use the same grammar production, so this RFC includes them. Is this in scope?
- Should `Obsolete`, `Experimental` and `CompilerMessage` on a field give a diagnostic where F# code names the field in a pattern or in a constructor call?
