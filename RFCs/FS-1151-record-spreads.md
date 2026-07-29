# F# RFC FS-1151 - Record spreads

The design suggestion [Spread operator for F#](https://github.com/fsharp/fslang-suggestions/issues/1253) has been marked "approved in principle."

This RFC covers the detailed proposal for **record-to-record** spreads as the first independently useful subset of the suggestion.

- [x] [Spread operator for F#](https://github.com/fsharp/fslang-suggestions/issues/1253)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/18927)
- [x] [Discussion](https://github.com/fsharp/fslang-design/discussions/806)

# Summary

Record spreads are a concise, declarative, and general mechanism for expressing field-by-field projection, composition, and update while maintaining ordinary F# record typing.

Record spreads provide parallel composition mechanisms for record declarations and record construction. A record type spread composes field declarations from one or more source record types to define a fresh record type. A record expression spread composes candidate field values from one or more source records to construct a new record value. Both type and expression spreads use ordered, right-biased union by field name.

The result of a spread remains a concrete nominal or anonymous record type, or an instance of such a type. Record spreads do imply a compile-time relationship — specifically, a source-level dependency used during elaboration — between the spread sources and the target; they do not introduce or imply structural subtyping or an inheritance relationship.

This RFC covers:

- Record-to-record spreads in nominal record type definitions:

    ```fsharp
    type R2 = { ...R1; B : int }
    ```

- Record-to-record spreads in nominal record expressions:

    ```fsharp
    let target = { ...source; B = 2 }
    ```

- Record-to-record spreads in anonymous record expressions:
    
    ```fsharp
    let target = {| ...source; B = 2 |}
    ```

Spreads to or from non-records are left for a later RFC, as are any non-record spread applications in [the original language suggestion](https://github.com/fsharp/fslang-suggestions/issues/1253).

# Motivation

<!-- Why are we doing this? What use cases does it support? What is the expected outcome? -->

Record expression spreads generalize or replace most uses of `with` and several other requested or proposed record features. `with` only supports copying values from a single source record, and, for nominal records, the source and target types must be the same; spreads enable copying values from multiple sources, from anonymous records to nominal records, and from an instance of one nominal record type to another.

Record expression spreads facilitate evolving or extending a record model without (1) altering the original type and (2) manually copying each field individually from an instance of the old type to an instance of the new one.

Record type spreads facilitate the same domain modeling evolution or extension at the type level without introducing an inheritance relationship.

Spreads simplify composition: where nesting might have been used before to avoid hand-copying fields, spreads make it easy to compose types together while keeping the object graph flat.

Envisioned use cases for record spreads include:

- Domain modeling. Spreads make it practical to model the same domain more precisely in multiple contexts, adding fields or strengthening specific fields' types while avoiding the need to manually redefine and remap unchanged fields or to reuse types where the model does not quite fit.
- Codebase evolution and API versioning. Spreads can help adapt old code to new without needing to change the old model.
- Tests. It is common to need to generate slight variations of records when writing property-based tests: generate record `A`, but override one subset of fields with invalid values in this case, another subset in another case, etc.
- Composing independently produced data.

# Detailed design

<!-- This is the bulk of the RFC. Explain the design in enough detail for somebody familiar
with the language to understand, and for somebody familiar with the compiler to implement.
This should get into specifics and corner-cases, and include examples of how the feature is used.
 -->

## Syntax

We add support for the spread syntax `...SourceType` in nominal record type definitions, and `...sourceExpression` in nominal and anonymous record construction expressions.

### Nominal record type definitions

```fsharp
type R2 = { ...R1 }
```

```fsharp
type R3 = { ...R1; ...R2; Y : decimal; Z : string }
```

```fsharp
type R3 =
  { ...R1
    ...R2
    Y : decimal
    Z : string }
```

### Nominal record construction expressions

```fsharp
{ ...r1 }
```

```fsharp
{ ...r1; ...r2; Y = 1.2m; Z = "z" }
```

```fsharp
{ ...r1
  ...r2
  Y = 1.2m
  Z = "z" }
```

### Anonymous record construction expressions

```fsharp
{| ...r1 |}
```

```fsharp
{| ...r1; ...r2; Y = 1.2m; Z = "z" |}
```

```fsharp
{| ...r1
   ...r2
   Y = 1.2m
   Z = "z" |}
```

In the abstract syntax tree, this means that where record field declarations or bindings were allowed before, spreads are now also allowed; where record nodes had a list of fields before, they now have a list of fields and spreads.

The `...source` syntax remains disallowed in all other contexts.

Spreads used in a `with` copy-and-update record expression will be parsed, but the combination of `with` and spreads will be forbidden during typechecking.

While secondary constructors in object types also use record construction syntax (`new () = { X = 1 }`), spreads are currently disallowed in that context.

## Valid sources and targets

This RFC covers record-to-record spreads.

Record type spreads support spreading both nominal and anomymous record types into nominal record type definitions. The source and target types do not need to have the same structness, i.e., the source can be a struct record and the target a reference type and vice versa.

Support for spreading into anonymous record type definitions could be added in the future, but it is not included in this RFC, as it is expected to have lower utility (anonymous records are most commonly used without type annotations; nominal record types are usually used instead when the type definition is complex and must be reused). Spreading non-record types into record types is not supported; spreading record types into non-record types is not supported.

### Record type spreads

#### Nominal to nominal

```fsharp
type R1 = { A : int }
type R2 = { ...R1; B : int } // { A : int; B : int }
```

#### Anonymous to nominal

```fsharp
type R1 = {| A : int |}
type R2 = { ...R1; B : int } // { A : int; B : int }
```

### Structness

```fsharp
[<Struct>]
type R1 = { A : int }
type R2 = { ...R1; B : int } // (class) { A : int; B : int }
```

```fsharp
type R1 = { A : int }
[<Struct>]
type R2 = { ...R1; B : int } // (struct) { A : int; B : int }
```

### Record expression spreads

Record expression spreads support spreading nominal or anonymous record source values into nominal or anonymous record targets. The source and target of a spread do not need to have the same type, but they must both be record (nominal or anonymous) types. The source and target values do not need to have the same structness, i.e., the source can be a struct record and the target a reference type and vice versa.

Spreading from non-record objects or to non-record objects is not covered by this RFC.

#### Nominal to nominal

```fsharp
type R1 = { A : int }
type R2 = { A : int; B : int }

let r1 = { A = 1 }
let r2 = { ...r1; B = 2 } // { A = 1; B = 2 }
```

#### Anonymous to nominal

```fsharp
type R2 = { A : int; B : int }

let r1 = {| A = 1 |}
let r2 = { ...r1; B = 2 } // { A = 1; B = 2 }
```

#### Nominal to anonymous

```fsharp
type R1 = { A : int }

let r1 = { A = 1 }
let r2 = {| ...r1; B = 2 |} // {| A = 1; B = 2 |}
```

#### Anonymous to anonymous

```fsharp
let r1 = {| A = 1 |}
let r2 = {| ...r1; B = 2 |} // {| A = 1; B = 2 |}
```

#### Structness

```fsharp
let r1 = struct {| A = 1 |}
let r2 = {| ...r1; B = 2 |} // {| A = 1; B = 2 |}
```

```fsharp
let r1 = {| A = 1 |}
let r2 = struct {| ...r1; B = 2 |} // struct {| A = 1; B = 2 |}
```

## Set algebra

Record spreads are read from left to right and use right-biased shadowing by field name.

Brief example:

```fsharp
type R3 = { ...R1; ...R2; C : int }
```

- Fields that are only in `R1`: use `R1`.
- Fields that are only in `R2`: use `R2`.
- Fields that are in both: use `R2`.
- `C`: use `int`.

> [!NOTE]
> The following should be read more as an informal description than a formal specification.

Typing for record type spreading is source-directed: a record type definition's spread sources help define the new type's shape.

Typing for record expression spreading is bidirectional, but target-biased: a known target type fixes the required shape, while the spread sources participate in target type inference in the absence of a known target.

Only record fields participate in record-to-record spreads: other instance, static, or extension members are ignored.

### The field composition model for spreads

To determine which field contribution in a record spread wins, model a record shape as a finite partial mapping from field names to field contributions:

```math
R : \mathit{FieldName}
    \rightharpoonup
    \mathit{FieldContribution}.
```

This mapping abstracts over field order, which is specified separately below.

Composition via spread is a left-to-right, right-biased union:

```math
\mathrm{dom}(R_1 \oplus R_2)
=
\mathrm{dom}(R_1) \cup \mathrm{dom}(R_2),
```

and, for field name $n \in \mathrm{dom}(A \oplus R_2)$,

```math
(R_1 \oplus R_2)(n)
=
\begin{cases}
R_2(n), & n \in \mathrm{dom}(R_2), \\
R_1(n), & n \in \mathrm{dom}(R_1)
      \setminus \mathrm{dom}(R_2).
\end{cases}
```

Later (rightward) contributions thus shadow earlier (leftward) contributions with the same field name; this includes explicit fields:

```fsharp
{ ...r1; ...r2; A = 3 }
```

The corresponding field-selection mapping is:

```math
R_1
\oplus
R_2
\oplus
\{ A \mapsto 3 \}.
```

Explicit fields and spreads may be interleaved in any order:

```fsharp
{ ...r1; A = 3; ...r2 }
```

The same selection logic applies:

```math
R_1
\oplus
\{ A \mapsto 3 \}
\oplus
R_2.
```

This gives spreads a simple left-to-right reading:

- Disjoint fields accumulate.
- Fields from a later spread shadow fields with the same name from an earlier spread.
- A later explicit field shadows a field from a spread.
- A field from a spread that shadows an earlier explicit field is allowed with a warning.
- Explicit duplicate field declarations or bindings (that are not [nested updates](#nested-field-updates)) remain errors.

This algebra determines which field contribution wins. It does not describe [elaboration and runtime behavior](#elaboration-and-runtime-behavior).

### Record type spreads

A record type spread composes field declarations into a new record type:

```fsharp
type R1 = { A : int; B : int }
type R2 = { C : string }
type R3 = { ...R1; ...R2; A : string }
```

Conceptually:

```math
\mathrm{fields}(R3)=\mathrm{fields}(R1)
\oplus
\mathrm{fields}(R2)
\oplus
\{A \mapsto \texttt{string}\}
```

where $\oplus$ is left-to-right, right-biased union by field name. `R3` thus has the fields:

```fsharp
{ B : int
  C : string
  A : string }
```

A record type spread is principally source-directed: its source types contribute field declarations (and potentially generic constraints) to the newly declared target type. The target type does not already have a shape against which the sources are projected; its shape is being defined by the composition.

The result of a record type spread is a fresh nominal record type.

Winning fields retain the order of their contribution, otherwise retaining their source order. That is, fields contributed by a spread are inserted at the spread's position in the order in which they occur in the source record type. When a field is shadowed, its earlier occurrence is removed, so the winning contribution determines the field's position in the resulting record type.

Record type spreads do not introduce inheritance, inclusion, or structural subtyping between `R1`, `R2`, and `R3` in the example above.

### Record expression spreads

A record spread expression consists of spread-sourced candidate fields and zero or more explicit field bindings. Right-biased composition determines the winning contribution for each field name.

Typechecking for expression spreads is bidirectional:

- When the expression has a known target type (whether nominal or anonymous), checking is target-directed.
- When no target type information is known, the spread sources and explicit fields drive target inference.

| Expression | Target type/shape | Meaning |
| - | - | - |
| `{ source with A = x }` | Fixed by `source` | Construct a new nominal record of the same type; copy unassigned fields from `source`, then assign `A` |
| `{\| source with A = x \|}` | Derived from `source` and the explicit updates | Construct a new anonymous record; copy `source` (except for `A`, if present), then assign `A` |
| `{ ...source; A = x } : Target` | Fixed by `Target` | Construct a new `Target`; copy matching unassigned fields from `source` and assign `A` |
| `{\| ...source; A = x \|} ` | Inferred from the contributions | Construct a new anonymous record whose shape is determined by the fields of `source` and the explicit `A` binding |
| `{\| ...source; A = x \|} : {\| A : int; B : int; C : int \|}` | Fixed by the annotation | Construct a new anonymous record of the known target type; copy matching unassigned fields from `source`, then assign `A` |

Example:

```fsharp
type Source = { A : int; B : int }
type Target = { A : int }

let source  : Source        = {  A = 1; B = 2 }
let target1 : Target        = {  ...source }    // Only Source.A is copied over; extra fields are ignored.
let target2 : {| A : int |} = {| ...source |}   // Only Source.A is copied over; extra fields are ignored.
let target3                 = {| ...source |}   // Source.A and Source.B are copied over because the target type is unconstrained; results in {| A = 1; B = 2 |}.
```

#### Known target type

Let

```math
D_T = \mathrm{dom}(\mathrm{fields}(T)).
```

For spread-sourced candidate mappings $S_1,\ldots,S_n$, the contributions applicable to target type $T$ are restricted to the domain of its fields $D_T$:

```math
\left(
S_1 \oplus \cdots \oplus S_n
\right)
\restriction D_T.
```

That is, a record expression spread contributes field values to the construction of a new record value:

```fsharp
let r : Target = { ...source1; ...source2; A = value }
```

Each spread-sourced mapping is restricted to the target domain. The resulting candidate fields and explicit bindings are then composed in their original source order. Explicit bindings are checked directly against the target and are not discarded when their names are absent from it.

Given:

```fsharp
type Source = { A : int; B : string; Extra : bool }
type Target = { A : obj; B : string }

let source = { A = 1; B = "two"; Extra = true }
let target : Target = { ...source }
```

the spread expression means:

> Construct a `Target`, using `source` to satisfy any applicable target fields.

It does not mean:

> Clone `source` and then convert the resulting record to `Target`.

Thus, in the example above:

- `A` and `B` are selected because `Target` requires them.
- `Extra` is ignored because it is not part of `Target`.
- `source.A` is checked as an assignment to `Target.A`, including any applicable coercion.
- Every target field must ultimately be supplied.
- The constructed runtime value has type `Target`.

#### Unknown target type

For a nominal record spread expression, when the target type is not already known via annotation or other context:

```fsharp
let result = { ...source; A = 42 }
```

the compiler must first infer which nominal record type is being constructed. The types of the spread sources and the explicitly bound fields contribute information to ordinary F# record-type inference.

This is source-directed: information flows outward from the spread sources and explicit fields to determine a target. Once a target type has been inferred, the expression is checked as a construction of that type.

For an anonymous record spread expression with no target type information:

```fsharp
let result = {| ...source; A = 42 |}
```

the composition of the spread-sourced and explicit fields determines the resulting anonymous record's shape.


### Subtraction

This RFC does not propose any explicit means of expressing field subtraction, e.g., a [`without`](https://github.com/fsharp/fslang-design/discussions/616) keyword or [`\` set difference notation](https://github.com/fsharp/fslang-design/discussions/616#discussioncomment-12269450).

### Relationship to `with`

`with` copy-and-update expressions remain supported.

Using `with` and spreads in the same expression is not allowed.

There is one notable difference in type inference between `with` and spread expressions for nominal records. When the type of both the source and target of a nominal `with` copy-and-update expression is unknown, but the copy-and-update expression's explicit field bindings intersect with the fields of an in-scope nominal record type, the copy-and-update expression's overall type (and thus the source type) is inferred to have that nominal record type:

```fsharp
type R = { A : int; B : int }
let f x = { x with B = 3 } // x is inferred to have type R.
```

We do not do this for the spread equivalent, even if we annotate the target type, because, unlike in the case of `with`, the source of a spread is not bound to have the same type as the target:

```fsharp
type R = { A : int; B : int }
let f x     = { ...x; B = 3 } // This does not compile.
let g x : R = { ...x; B = 3 } // This also does not compile.
```

This behavior is in line with how `with` already works for anonymous records, where the source of `with` likewise need not have the same type as the target:

```fsharp
let f x                          = {| x with B = 3 |} // This does not compile.
let g x : {| A : int; B : int |} = {| x with B = 3 |} // This also does not compile.
```

### Nested field updates

Record spread expressions support nested field updates ([RFC](https://github.com/fsharp/fslang-design/blob/d08534a0f9e7cd5f80d68e51dd11e4e21afdda67/archived/FS-1049-nested-record-field--copy-and-update-expression.md), [discussion](https://github.com/fsharp/fslang-design/discussions/733), [implementation](https://github.com/dotnet/fsharp/pull/14821)) similar to `with`:

```fsharp
let r1 = {| X = {| Y = 1; Z = 2 |} |}
let r2 = {| ...r1; X.Y = 3 |} // {| X = {| Y = 3; Z = 2 |} |}
```

Unlike `with`, spreads permit multiple sources. If there are multiple sources, the same right-biased field selection is applied to the nested updates as to the rest of the fields:

```fsharp
type NestedRecord = { A : string; B : string }
type OuterRecord1 = { Nested : NestedRecord; Other : NestedRecord }
type OuterRecord2 = { Nested : NestedRecord }

let orig1 = { Nested = { A = "value1"; B = "value1" }; Other = { A = "value2"; B = "value2" } }
let orig2 = { Nested = { A = "value3"; B = "value3" } }

let actual = { ...orig1; Nested.B = "value4"; ...orig2; Other.B = "value5" }
let expected = { Nested = { A = "value3"; B = "value3" }; Other = { A = "value2"; B = "value5" } }
```

Multiple nested updates to the same outer field are grouped the same way they are for `with`:

```fsharp
let r1 = {| X = {| Y = 1; Z = 2 |} |}
let r2 = {| ...r1; X.Y = 3; X.Z = 4 |} // Equivalent to {| r1 with X = {| r1.X with Y = 3; Z = 4 |} |}
```

## Elaboration and runtime behavior

### Record type spreads

- A spread source type must be accessible at the spread site.
- The accessibility of a spread-derived field is that of the target type.
- Structness of the target type comes from its own definition (presence or absence of `[<Struct>]`), not from any spread source types.
- Type-level attributes are _not_ copied from the original type definition.
- Field-level attributes _are_ copied from the original field declaration.
- Field mutability is copied from the original field declaration.
- Generic parameter kinds (measure or not) and constraints required by the spread source type must be satisfied by its supplied type arguments.
- Record type spreads are a compile-time construct: use of a type in a spread does not trigger static initialization (`static let`, `static do`) in the source type at runtime. This is similar to how simply using a type as a generic type parameter instantiation does not trigger static initialization of that type.

### Record expression spreads

- Spread source expressions are evaluated in source order, from left to right.
- Each spread source expression is evaluated exactly once, even if all of its fields end up unused or shadowed.

## Generic constraints

The generic instantiation for a record type spread must satisfy any constraints present on the source type, e.g.:

```fsharp
type R1<'a when 'a : comparison> = { A : 'a }
type R2<'a when 'a : comparison> = { ...R1<'a> }
```

```fsharp
type R1<[<Measure>] 'a> = { A : int<'a> }
type R2<[<Measure>] 'b> = { ...R1<'b> }

type [<Measure>] m
type R3 = { ...R1<m> }
```

## Conversions and coercions

Once the set of fields that a record expression spread contributes to the target value is determined, the same conversions and coercions are applied to each field as would be applied if each field were mapped explicitly.

This differs from the existing behavior of `with` with anonymous records: in that case, conversions and coercions are _not_ applied to fields in the original expression of the `with`, while they _are_ applied if the same original expression is used with a spread instead.

## Recursion

Mutually recursive record type definitions that are cyclic via spreads are specifically disallowed. Mutually recursive record type definitions that are _not_ cyclic are allowed.

## Signature files

Record type spreads are allowed in signature files. The same syntactic form need not be used for a type definition in an implementation file and its corresponding signature file: one can use spreads and the other explicit field declarations, as long as they are equivalent.

Whether spreads or explicit field declarations are used in a signature file does not affect the compiled form: the compiled form of the type is always comprised of the elaborated set of fields.

## Quotations

Spreads are not visible in quotations; quotations will see the elaborated, ordinary F# record constructs.

## Nullability

Nullable records are not allowed as spread sources.

```fsharp
type R1 = { A : int }
type R2 = { ...(R1 | null) } // Error.
```

```fsharp
let maybeR : {| A : int |} | null = f ()
let r2 = {| ...maybeR |} // Error.
```

## C# records

This proposal does not include support for using C# records with spreads. If the proposal [Support for F# record syntaxes for C# defined records](https://github.com/fsharp/fslang-suggestions/issues/1138) is implemented, spreads should be considered as part of it.

# Changes to the F# spec

<!-- List the necessary changes or additions to the F# spec (4.1 pdf [here](https://fsharp.org/specs/language-spec/4.1/FSharpSpec-4.1-latest.pdf), latest markdown doc [here](https://github.com/fsharp/fslang-spec/blob/main/releases/FSharp-Spec-latest.md)).
For each change, mention the section numbers and the required change.
Here are two examples:
- [Class names as functions](https://github.com/fsharp/fslang-design/blob/main/FSharp-4.0/ClassNamesAsFunctionsDesignAndSpec.md#specification)
- [Scoped nowarn](https://github.com/fsharp/fslang-design/blob/main/RFCs/FS-1146-scoped-nowarn.md#detailed-specification) -->

A record field declaration and a record/anonymous-record expression binding each gain a spread form:

```
recd-field-decl  +=  '...' type      // in record type definitions
recd-binding     +=  '...' expr      // in record and anonymous record expressions
```

# Drawbacks

<!-- Why should we *not* do this? -->

As with any language feature, spreads have the potential to be abused or misunderstood:

- Spreads are powerful: they enable concise expression of complex compositions. Users may use them in confusing or overcomplicated ways.
- There are [many potential applications of spreads beyond records](https://github.com/fsharp/fslang-suggestions/issues/1253): once record spreads are supported, users may expect others, but not all may be feasible, desirable, or practical to implement.
- Spreads imply a compile-time relationship between spread source types or values and their targets: users may use spreads out of convenience even where establishing such a relationship is not appropriate. For example, changing anything about a spread source's type — changing field types, field names, adding or removing fields — can affect the target type or value:

    - Adding or removing a field to the source type or value can add a field to or remove it from the target, or create a new collision or shadowing warning.
    - Changing mutability, attributes, or generic type constraints on the source can change the target.
    - The target's public binary shape can change without its own declaration being directly edited.

    Spreads are meant to be used when such a relationship is desired. They should not be used when such a relationship is not desired, or indeed the drawbacks are likely to outweigh the advantages.

    Note that this is not really a new kind of phenomenon: changing anything about the type definition for _any_ type can have most of the same effects.
- Record type spreads bring over the source fields' attributes; doing this with certain kinds of attributes, like [`FieldOffsetAttribute`](https://learn.microsoft.com/dotnet/api/system.runtime.interopservices.fieldoffsetattribute), could cause problems.

# Alternatives

<!-- What other designs have been considered? What is the impact of not doing this? -->

## Related F# language proposals

- https://github.com/fsharp/fslang-suggestions/issues/11: row polymorphism. [Rejected as being infeasible to implement within the constraints of the .NET type system](https://github.com/fsharp/fslang-suggestions/blob/d48c35ce216e2bff148937ec028ad61e5c273fdf/archive/suggestion-9633858-structural%2C-extensible-records-like-elm-concrete.md?plain=1#L210-L211).
- https://github.com/fsharp/fslang-suggestions/issues/100: embeddable records. Similar to Go-style inclusion (see [Prior art](#prior-art)). Rejected for being too close to establishing an inheritance relationship.
- https://github.com/fsharp/fslang-suggestions/issues/140: enable changing generic type instantiation via `with`.
- https://github.com/fsharp/fslang-suggestions/issues/164: allow the record pattern to be implemented explicitly (by non-record classes/structs).
- https://github.com/fsharp/fslang-suggestions/issues/291: make `with` copy-and-update expressions target-type-directed. Rejected at the time in favor of https://web.archive.org/web/20190825220330/https://fslang.uservoice.com/forums/245727-f-language/suggestions/5663704-copy-and-update-on-class-types-and-on-records-of-d, i.e., https://github.com/fsharp/fslang-suggestions/issues/164.
- https://github.com/fsharp/fslang-suggestions/issues/325: duplicate of https://github.com/fsharp/fslang-suggestions/issues/164.
- https://github.com/fsharp/fslang-suggestions/issues/744: `with` in record type definitions. Conceptually similar to record type spreads as described in this RFC, although perhaps not understood as such at the time and thus rejected as being too close to inheritance.
- https://github.com/fsharp/fslang-suggestions/issues/1251: essentially record expression spreads using `with` syntax; led to https://github.com/fsharp/fslang-suggestions/issues/1253.
- https://github.com/fsharp/fslang-suggestions/issues/1266: essentially record expression spreads, but only nominal ↔ anonymous where the source and target are structurally identical. Rejected in favor of spreads.

## Use `..` (two dots) instead of `...` (three dots)

### Pros

- F# already uses `..` for slicing and ranges.
- `..` is used for "spread elements" in [C# collection expressions](https://github.com/dotnet/csharplang/blob/1c2f0c89a2fc33607e92514abee2abc78b9d6210/proposals/csharp-12.0/collection-expressions.md).
- Avoids confusion of `...` in spreads with its existing wide use as in an informal placeholder in comments, examples, etc., to indicate elided code; it is likely that the `...` could now be interpreted as a valid spread instead in at least some of these contexts.

### Cons

- `..` is a user-definable infix operator.
- Reusing `..` for spreads could conflict with other potential applications, like [currying/uncurrying arguments](https://github.com/fsharp/fslang-suggestions/issues/1253#issuecomment-2764787371), spreading tuples into other tuples, spreading records or tuples as arguments, spreading into param arrays, etc., [when combined with existing F# support for user-defined slicing via `GetSlice`](https://github.com/fsharp/fslang-design/discussions/806#discussioncomment-14095251). Changing the effect of `..` on its argument(s) in indexers would be a breaking change.
- Unary prefix `..` is used in C# in expressions like `..10` to produce a `System.Range` with no start. If we ever [add support for `System.Range`](https://github.com/fsharp/fslang-suggestions/issues/1317#issuecomment-2251126932), we may want to reserve the meaning of prefix `..` for that.

## Use `with`

```fsharp
type R1 = { A : int; B : int }
type R2 = { R1 with C : int; D : int }
type R3 = { R1 with R2 } // ?

let r1 = { A = 1; B = 2 }
let r2 = { r1 with r2 }
```

### Pros

* The `with` keyword already exists and its usage overlaps with that proposed for `...`.
  ```fsharp
  let r1 = {| A = 1; B = 2 |}
  let r2 = {| ...r1; C = 3 |} // = {| r1 with C = 3 |}
  ```

### Cons

* Spreads have a natural corresponding pattern syntax, if we choose to implement that in the future; `with` does not.
* `with` is both more syntactically ambiguous and less composable:
  ```fsharp
  let r1 = {| A = 3; B = 4 |}
  let r2 = {| C = 5; D = 6 |}
  let r3 = {| ...r1; ...r2 |}         // Would this be {| r1 with r2 |}? How would that be distinguishable from an incomplete field binding {| r1 with r2 = 7 |}?
  let r4 = {| ...r1; E = 99; ...r2 |} // How would `with` work with interleaved explicit bindings?
  ```

## Type providers

https://github.com/fsharp/fslang-suggestions/issues/212 together with https://github.com/fsharp/fslang-suggestions/issues/154 could in theory be used to achieve something similar to type spreads.

## Mapped types

An F# analogue to TypeScript's mapped types could likely cover many use-cases of both record type spreads and type-transforming type providers, but is almost certainly a non-starter.

# Prior art

<!-- Has a similar feature been proposed or implemented for other programming languages (e.g., C#, OCaml, etc.)? Is there any wisdom to be gained from these proposals, designs, implementations, or from real-world usage? -->

Record spreads overlap with two existing families of programming language features:

1. Object or record composition, where fields from several types or values are combined.
2. Functional record update, where values for omitted fields are obtained from an existing value of the same type.

The F# feature proposed in this RFC includes both uses but differs from existing systems by combining nominal (as opposed to structural) types, multiple sources, target-directed projection, and type-level record composition.

## ReScript record spreads

ReScript has [record type spreads](https://rescript-lang.org/docs/manual/record/#record-type-spread) and uses spread syntax for [immutable update expressions](https://rescript-lang.org/docs/manual/record/#immutable-update-1).

ReScript record type spreads produce a new nominal record type and reject duplicate field names across spread sources altogether rather than using right-biased shadowing:

```rescript
type a = {
  id: string,
  name: string,
}

type b = {
  age: int
}

type c = {
  ...a,
  ...b,
  active: bool
}
```

yields

```rescript
type c = {
  id: string,
  name: string,
  age: int,
  active: bool,
}
```

ReScript immutable updates are more akin to F#'s existing `with` expressions.

## Flow object spreads

Flow has [object type spreads](https://flow.org/en/docs/types/objects/#object-type-spread) and object (value) spreads that mirror JavaScript object spread semantics, where properties are combined left to right and later properties replace earlier properties with the same name. The resulting type in Flow is structural, however, unlike in this proposal.

## JavaScript and TypeScript object spreads

JavaScript and TypeScript have object spreads:

  - https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Operators/Spread_syntax
  - https://tc39.es/ecma262/multipage/ecmascript-language-expressions.html#prod-SpreadElement
  - https://www.typescriptlang.org/docs/handbook/variable-declarations.html#spread

The semantics of unconstrained anonymous record construction expressions involving spreads proposed in this RFC resemble this behavior relatively closely: the F# design also uses left-to-right evaluation and right-biased field type and value selection.

## Functional record update syntax

### Rust, Gleam

Rust's [functional update syntax for structs](https://doc.rust-lang.org/reference/expressions/struct-expr.html#functional-update-syntax) uses a spread-like `..source` syntax, but behaves roughly analogously to F#'s existing `with` expressions.

[Gleam's record update syntax](https://tour.gleam.run/data-types/record-updates/) is similar.

## Related but semantically different mechanisms

### Intersection and mapped types

TypeScript's [intersection types](https://www.typescriptlang.org/docs/handbook/2/objects.html#intersection-types) can be used to achieve a similar result to the record type spreads proposed in this RFC in some scenarios, although it is not equivalent. For example, in an intersection type, two fields with the same name will have their types restricted to _their_ intersection, which could result in `never`, etc.

Intersection types have been proposed for both [C#](https://github.com/dotnet/csharplang/discussions/399) and [F#](https://github.com/fsharp/fslang-suggestions/issues/600#issuecomment-1505314343), although it seems unlikely that they will be implemented in either language.

TypeScript's [mapped types](https://www.typescriptlang.org/docs/handbook/2/mapped-types.html) can express substantially more general structural type transformations than the proposed record type spreads.

### Type embedding

Go has type embedding: https://go.dev/doc/effective_go#embedding, https://go.dev/ref/spec#Struct_types.

It allows a struct to contain an embedded field whose members are promoted for selection. Unlike this proposal, embedding preserves an actual nested field; it does not copy the embedded type's fields into a fresh flat struct definition.

## Design observations

- Same-type functional update is common in nominal record systems.
- Ordered, right-biased composition is established in JavaScript, TypeScript, and Flow.
- Type-level flattening into a new nominal record type has precedent in ReScript.

This proposal combines these and adds target-directed functional _construction_ or _projection_ from multiple sources to a potentially distinct target type.

# Compatibility

* Is this a breaking change?
  * No. `...source` is new syntax. Parsing of the range and `..` range step `.. ..` operators, as well as user-defined infix operators with sequences of leading or trailing `.`s (`...+...`, etc.), is unchanged.
* What happens when previous versions of the F# compiler encounter this design addition as source code?
  * Older F# compilers will fail to parse code that uses spreads and will emit a context-dependent error, e.g., "FS0010: Unexpected symbol '..' in binding," "FS0010: Unexpected symbol '..' in field declaration," etc.
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
  * Older compiler versions will be able to consume the compiled result of this feature without issue; spreads produce normal F# nominal or anonymous record types or values.
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?
  * N/A.

# Interop

* What happens when this feature is consumed by another .NET language?
  * Spreads produce normal F# nominal or anonymous record types or values; interop will be the same as for existing record types and values.
* Are there any planned or proposed features for another .NET language (e.g., [C#](https://github.com/dotnet/csharplang)) that we would want this feature to interoperate with?
  * C# records support `with` copy-and-update in C#; F# does not currently support using `with` copy-and-update expressions with C# records, but there is an approved language suggestion for doing so: https://github.com/fsharp/fslang-suggestions/issues/1138. If we add support for treating C# records like F# records in F#, C# records should also be able to be used with spreads.
  * Various forms of record-like spreads have been proposed for C#: https://github.com/dotnet/csharplang/discussions/2879, https://github.com/dotnet/csharplang/discussions/9726, https://github.com/dotnet/csharplang/discussions/10039, etc. If record spreads are ever implemented for C#, it is likely that their exact rules and implementation will differ from those proposed for F# here, but it also seems likely that no new interop concerns would be introduced. As shown possible in this RFC for F#, it does not seem likely that C# would need any new runtime-visible constructs to implement a similar feature.

# Pragmatics

## Diagnostics

| Scenario | Expected diagnostic |
|---|---|
| Record type spread source is neither a nominal nor anonymous record type | **FS3891 — error:** "The source type of a spread into a record type definition must itself be a nominal or anonymous record type." |
| Record type spread source is a nullable record type | **FS3892 — error:** "The source type of a spread into a record type definition cannot be nullable." |
| Nominal record expression spread source is neither a nominal nor anonymous record value | **FS3893 — error:** "The source expression of a spread into a nominal record expression must have a nominal or anonymous record type." |
| Nominal record expression spread source is a nullable record value | **FS3894 — error:** "The source expression of a spread into a nominal record expression cannot be nullable." |
| Anonymous record expression spread source is neither a nominal nor anonymous record value | **FS3895 — error:** "The source expression of a spread into an anonymous record expression must have a nominal or anonymous record type." |
| Anonymous record expression spread source is a nullable record value | **FS3896 — error:** "The source expression of a spread into an anonymous record expression cannot be nullable." |
| In a record type definition, a spread occurs to the right of an explicit field with the same name | **FS3897 — warning:** "Spread field '…' from type '…' shadows an explicitly declared field with the same name." |
| In a nominal or anonymous record expression, a spread occurs to the right of an explicit field with the same name | **FS3898 — warning:** "Spread field '…' shadows an explicitly declared field with the same name." |
| `...` has no following source expression, including inside a record construction or copy-and-update | **FS3899 — error:** "Missing spread source expression after '...'." |
| `...` has no following source type in a record type definition | **FS3900 — error:** "Missing spread source type after '...'." |
| Record type definitions form a direct or indirect cycle through spread edges | **FS3901 — error:** "This type definition involves a cyclic reference through a spread." One diagnostic is emitted for each type selected by cycle traversal. |
| `...` occurs in an unsupported construct, such as a list, array, sequence/computation expression, or constructor-shaped record expression | **FS3902 — error:** "Spreading is not supported in this construct." |
| A spread is put before `with`, as in `{ ...r with A = x }` or `{\| ...r with A = x \|}` | **FS3903 — error:** "Spreading is not supported in this position. Use one of the forms `{ ...expr1; A = expr2 }` or `{ expr1 with A = expr2 }` instead." |
| A normal copy-and-update already has a `with` source and its update fields also contain a spread, e.g. `{ r with ...s }` | **FS3904 — error:** "Spread expressions and 'with' cannot be used together in the same copy-and-update expression." |
| In a record type definition, a spread occurs to the right of another spread that contributes a field with the same name | **FS3905 — informational warning:** "Spread field '…' from type '…' shadows a field with the same name from an earlier spread." |
| In a record type definition or a nominal or anonymous record expression, an explicit field occurs to the right of a spread that contributes a field with the same name | **FS3906 — informational warning:** "Explicit field '…' shadows a field with the same name from an earlier spread." |
| In a nominal or anonymous record expression, a spread occurs to the right of another spread that contributes a field with the same name | **FS3907 — informational warning:** "Spread field '…' shadows a field with the same name from an earlier spread." |

## Tooling

Please list the reasonable expectations for tooling for this feature, including any of these:

* Debugging
  * Breakpoints/stepping
    * N/A.
  * Expression evaluator
    * N/A.
  * Data displays for locals and hover tips
    * N/A: this feature does not introduce any new runtime-visible constructs; the debugger will show the flattened record fields.
* Auto-complete
  * In record type spreads, tooling should show in-scope record types when offering completions after `...`.
  * In record expression spreads, tooling should show in-scope record values when offering completions after `...`.
* Tooltips
  * Should `...` be given a descriptive symbol tooltip, like we have for keywords?
  * Hovering over a record type that includes spreads will show the resulting record type, i.e., it will show the resulting set of individual fields; the tooltip will not show any relationship to any of the spread source types.
  * Hovering over a record value constructed from spreads will show the resulting record type, i.e., it will show the resulting set of individual fields; the tooltip will not show any relationship to any of the spread source types.
* Navigation and go-to-definition
  * Invoking go-to-definition on a spread source should navigate to the corresponding source type or value.
* Error recovery (wrong, incomplete code)
  * When missing expr/ty after `...`, error
  * When `...` encountered in invalid syntactic context, error
    * See the [implementation](https://github.com/dotnet/fsharp/pull/18927) and its tests.
* Colorization
  * An unbounded sequence of `.` is already a valid prefix and suffix for user-defined infix operators, and most tooling (Visual Studio, Ionide, GitHub) already treats that correctly.
  * The [implementation](https://github.com/dotnet/fsharp/pull/18927) adds a distinct `...` `DOT_DOT_DOT` token to the lexer and uses the same colorization as `..` `DOT_DOT`.
* Brace/parenthesis matching
  * N/A.
* Renaming
  * Renaming a field will be visible in any spreads to which the field contributed; the spread algebra will be applied anew given the new field name.
* Find all references
  * Applying find-all-references to a record type should find uses of that type in spreads.
  * Applying find-all-references to a record value should find uses of that value in spreads.
  * Should find-all-references on a record field declaration in a record type definition find uses of the outer record type when that type is used in spreads?
  * Should find-all-references on a record field binding in a record construction expression find uses of the outer record value when that value is used in spreads?
* Analyzers/code fixes
  * An analyzer and code fix for converting `with` copy-and-update expressions to use spreads would be useful.
  * What about a code fix for converting spreads to their explicit equivalent?

## Performance

<!-- Please list any notable concerns for impact on the performance of compilation and/or generated code -->

### Compilation performance

* For existing code
  * The addition of this feature will not significantly affect the performance of the compiler on existing code.
* For the new feature
  * We do not foresee any major compiler performance issues inherent to the implementation in https://github.com/dotnet/fsharp/pull/18927. The added overhead when typechecking code that does not use spreads should be negligible.
  * Users could abuse the expressive power of spreads to more easily write source code that could be problematic for the compiler. For example, they could write thousands of mutually recursive record definitions (or record definitions in a recursive module or namespace) with hundreds of interleaved fields and spreads each.

### Runtime performance

* For existing code
  * The runtime performance of existing code will be unchanged.
* For the new feature
  * The runtime performance of code produced by record expression spreads will be the same as if the user had written the equivalent field-by-field bindings by hand.

## Scaling

<!-- Please list the dimensions that describe the inputs for this new feature, e.g. "number of widgets" etc.  For each, estimate a reasonable upper bound for the expected size in human-written code and machine-generated code that the compiler will accept. -->

Overall record typechecking remains approximately linearithmic in the total number of field contributions after expanding spread sources, i.e., it is in line with the complexity of checking the equivalent explicitly-written code.

The implementation does not place an upper bound on the number of spreads it will accept.

Record spreads in a mutually recursive declaration group require rechecking once all fields in the group have been established; each record type in such a group is rechecked at most once, in dependency order.

Since spreads can represent an expanded set of field declarations more compactly than the equivalent explicit source code, it is possible to make compilation cost relative to source-text size increase. For example, a user could write a linear chain of record type spreads that successively added fields, which would produce a quadratic total number of materialized fields:

```fsharp
type R₁ = { A : int }
type R₂ = { ...R₁; B : int }
// …
type Rₙ = { ...Rₙ₋₁; Ω : int }
```

## Culture-aware formatting/parsing

Does the proposed RFC interact with culture-aware formatting and parsing of numbers, dates and currencies? For example, if the RFC includes plaintext outputs, are these outputs specified to be culture-invariant or current-culture.

* No.
