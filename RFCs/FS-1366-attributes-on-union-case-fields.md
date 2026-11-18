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

.NET libraries such as `System.Text.Json` and the `System.ComponentModel.DataAnnotations` validators read attributes from properties to control serialization and validation. A record field can have attributes. A union case field cannot, so these libraries cannot be configured for it.

`System.Text.Json` serializes F# unions since [dotnet/runtime#125610](https://github.com/dotnet/runtime/pull/125610). It reads `JsonPropertyName`, `JsonConverter` and `JsonNumberHandling` from the property of each case field, but that pull request notes that "F# currently has no syntax for applying attributes to individual union case field parameters".

# Detailed design

## Syntax

```
union-type-field :=
    attributes? type             -- unnamed union field
    attributes? ident ':' type   -- named union field
```

Union cases and exception definitions use this production, in implementation files and in signature files.
An attribute list can be on the line before its field. An XML documentation comment for the field comes before the attributes.

```fsharp
type Shape =
    | Circle of [<Range(0.0, 100.0)>] float * string  // on `Item1` only
    | Label of
        /// The text to show.
        [<MaxLength(20)>]
        text: string
```

The case form `ident ':' uncurried-sig` is for FSharp.Core only and does not change: the compiler continues to ignore attributes on its arguments.

## Target

1. The compiler MUST emit each attribute on the CLI property that represents the field, next to the attributes that it generates there today. When the source has an attribute type that the compiler also generates and that does not allow multiple instances (for example `DebuggerNonUserCode`), the compiler MUST emit one instance.
2. The compiler MUST NOT emit the attribute on a backing field, or on a parameter of a generated member such as `New<Case>`.
3. The compiler MUST accept the explicit target `property:`, which has no effect, and MUST report an error for any other explicit target.
4. The compiler MUST report an error for an attribute type whose `AttributeUsage` does not include `AttributeTargets.Property`.
5. The compiler MUST NOT change construction, pattern matching, comparison or printing because of these attributes.
6. F# code can read an exception field as `e.field`. The compiler MUST check the attributes of the field at this access as it does for a record field, so `Obsolete`, `Experimental` and `CompilerMessage` give their diagnostics.

```fsharp
type U =
    | A of [<property: Required>] a: string    // same as [<Required>]
    | B of [<field: NonSerialized>] b: string  // error: only the `property` target is permitted
    | C of [<FieldOffset(0)>] c: int           // error: `FieldOffset` is valid on fields only

exception ParseError of [<Obsolete "Use offset">] position: int * offset: int

let f (e: ParseError) = e.position             // warning FS0044, as for a record field
```

## Struct unions

A struct union has one CLI type for all cases, so fields of different cases that have the same name share one property. Today, the fields can share only when they have the same type (§8.8). With this RFC, they can share only when they also have the same attribute list: the same attributes in the same order. Two attributes are the same when they have the same attribute type, constructor arguments with equal values (the compiler compares arrays by element), and the same named arguments with equal values, in any order. The rule applies separately to the signature file and to the implementation file. The compiler MUST report an error for fields with the same name that cannot share, and MUST emit the list once on a shared property. The rule includes the generated names `Item`, `Item1`, and so on. It comes from the [suggestion discussion](https://github.com/fsharp/fslang-suggestions/issues/684#issuecomment-4565114924).

```fsharp
[<Struct>]
type S =
    | A of [<MaxLength(4)>] code: string
    | B of [<MaxLength(4)>] code: string * count: int  // shares `code` with A: one property, one `MaxLength(4)`
    | C of code: string                                // error at each `code`: the attribute lists differ
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

- The target rule differs from the rule for record fields (see [Alternatives](#alternatives)).
- In a struct union, each value has the properties of all cases, and a property of another case returns a default value. A consumer that reads all properties of the type, such as the `System.ComponentModel.DataAnnotations` `Validator`, applies an attribute to the values of other cases: `[<Range(1, 10)>] count: int` on one case fails for a value of another case, where `count` is 0. A consumer that reads `UnionCaseInfo.GetFields()`, such as `System.Text.Json`, does not.

# Alternatives

- **The rule for record fields.** A record field accepts the target `field` and attributes that are valid on fields only, and emits them on the backing field. The compiler chooses the storage of a union, so attributes such as `FieldOffset`, `DefaultValue` and `NonSerialized` need more rules. This RFC makes these programs errors, so a later RFC can permit them.
- **A `param` target** that emits the attribute on the parameter of `New<Case>`. No consumer is known. A later RFC can add it.
- **Struct unions: merge the attributes** of fields that share a property. The source of one case then does not show all attributes of its field, and attributes with `AllowMultiple = false` still need an error.
- **Struct unions: a generated property name** for a field that cannot share. Reflection and serializers then see a name that is not in the source.
- **No change.** Users replace the union with classes, or write a converter or validator for each type.

# Prior art

- C# [positional records](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-9.0/records.md#properties): the `property:` and `field:` targets move an attribute from a positional parameter to the generated property or its backing field.
- Rust: [fields of enum variants](https://doc.rust-lang.org/reference/items/enumerations.html) take attributes. [serde](https://serde.rs/field-attrs.html) reads them to rename, skip or convert a field.

# Compatibility

* This is not a breaking change. The syntax is a syntax error today.
* The feature needs a new language version. With an earlier `--langversion`, the compiler reports that the feature is not available.
* An earlier compiler reads a binary that uses the feature. The attributes are ordinary custom attributes on properties, and the F# metadata format already stores property attributes for each field.
* FSharp.Core does not change.

# Interop

C# and reflection see the attributes on the existing CLI property of the field (§8.5.4). `UnionCaseInfo.GetFields()` already returns these properties. No planned C# feature interacts with this.

# Pragmatics

## Diagnostics

The errors of [Target](#target) rules 3 and 4 are new: the existing diagnostic for an invalid attribute target, FS0842, is a warning. The compiler reports them at the attribute. It reports the error of [Struct unions](#struct-unions) at each field that has the name. There are no new warnings.

## Tooling

* FSharp.Compiler.Service: `SynField` already has an attribute list, and the parser now fills it for these fields. `FSharpField.PropertyAttributes` returns the checked attributes. Formatters need an update to print the attributes.
* Signature generation prints the attributes, also for unnamed fields.
* Completion, navigation and colorization in the attribute list work as for attributes on record fields.
* Debugging: the compiler SHOULD copy the attributes to the properties of the debugger proxy type of the case, as it copies the generated attributes today, so that `DebuggerBrowsable` on a field works.

## Performance

No notable change for compilation or for generated code.

## Scaling

Not applicable.

## Culture-aware formatting/parsing

Not applicable.

# Unresolved questions

- The suggestion names union case fields only. Are exception fields in scope?
- The suggestion discussion [asked](https://github.com/fsharp/fslang-suggestions/issues/684#issuecomment-4573142687) whether an attribute goes on the field, the property or both. Only the suggester answered: the property. Do maintainers accept the errors of [Target](#target) rules 3 and 4?
- Should `Obsolete`, `Experimental` and `CompilerMessage` on a field also give a diagnostic where F# code names the field in a pattern or in a constructor call? This RFC specifies a diagnostic only for `e.field` on an exception ([Target](#target) rule 6).
