# F# RFC FS-1150—Allow ',' as a separator for pattern matching on named discriminated union fields

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/957)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/18881)
- [x] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

This RFC covers the detailed proposal for this suggestion.

# Summary

This RFC proposes allowing `,` as a separator when matching on named fields of a discriminated union case. 

# Motivation
Today, when naming multiple fields within a discriminated union case pattern, the parser requires `;` to separate the named field patterns.

```fsharp
type Rect = Rect of width:int * height:int
match rect with
| Rect(width = w; height = h) -> w + h
```

This RFC allows `,` in addition to `;`

```fsharp
type Rect = Rect of width:int * height:int
match rect with
| Rect(width = w, height = h) -> w + h
```

This is consistent with the existing syntax for creating discriminated unions, where fields are separated by `,`.

```fsharp
type Rect = Rect of width:int * height:int
let rect = Rect(width = 10, height = 20)
```

After this RFC, the following code is also valid:

```fsharp
type Rect = Rect of width:int * height:int
let rect = Rect(width = 10, height = 20)

let res =
    match rect with
    | Rect(width = w, height = h) -> w + h
```

# Detailed design

- Permit `,` as an alternative separator to `;` between named field sub-patterns within a single DU case pattern.
- Scope: Only applies to named sub-patterns for a single case, such as `Case(name1 = pat1, name2 = pat2, ...)`. The existing `;` separator remains supported.
- Separator consistency: Mixing them is a parse error: `Inconsistent separators in a pattern. Use either all commas or all semicolons, but not both.`
- No changes to runtime semantics. This is a syntactic enhancement only.

Grammar changes
- Where the grammar previously recognized `name = pattern` separated by `;`, the production is extended to accept `,` as a separator token as well.
- This applies in the DU case argument pattern position for named fields only; it does not introduce `,` as a separator for record patterns (which already support `;`) nor does it change tuple or list pattern grammar.

Dealing with tuple ambiguity
- Disambiguation rule: In the argument pattern position of a discriminated union case, as soon as the parser sees an identifier (or qualified identifier) followed immediately by `=` (e.g., `width = ...`), it enters “named-field pattern list” mode for that case. In this mode, separators between sibling named-field patterns must be consistent: use either all `,` or all `;`. Mixing separators is a parse error.
- No mixing of named and positional siblings: Once any named field is present for a given case pattern, you can't add separate positional siblings. A pattern like `Rectangle(width = w, h)` is parsed as `width = (w, h)` (a tuple bound to the single named field) and will only type-check if that field is tuple-typed; otherwise it is rejected during type checking. This preserves clarity and avoids ambiguity between tuple commas and field separators.
- Parentheses and nesting: Commas inside a nested pattern remain scoped to that nested pattern. For example, `A(x = (a, b), y = c)` is valid; the comma inside `(a, b)` forms a tuple pattern for the value of `x`, while the outer commas separate named fields `x =` and `y =`.
- Commas after `name =` bind to the field’s pattern: The sequence `name = p1, p2` is parsed as `name = (p1, p2)` (a tuple pattern bound to that single named field) unless another `name2 =` starts a new sibling named field.
- Single tuple-typed field: If a case has a single field whose type is a tuple, e.g., `| P of (int * int)`, then `P(a, b)` is the standard tuple pattern (no names involved). Since there are no named fields, `P(x = a, y = b)` is not permitted unless the case fields were declared with names (e.g., `| P of x:int * y:int`). When names exist, `P(x = a, y = b)` parses in named-field mode; the commas between `x =` and `y =` are named-field separators, not tuple commas.
- Newlines and trailing separators: Newlines may separate named-field patterns as today. This RFC does not introduce trailing separators after the last named field (no change to existing rules).
- Practical intuition: “If you write `name =` anywhere inside the case’s parentheses, every sibling must also be `name = ...`, and commas between them are just like semicolons used today.”


# Backward compatibility and historical parsing behavior
- Tuple without parentheses after name =: When a comma follows a named field assignment, e.g., name = p1, p2, it is parsed as a tuple pattern bound to that single field: name = (p1, p2). This preserves historical behavior and is important for compatibility.
- Rectangle(width = w, h): This form is parsed as width = (w, h) (a tuple without parentheses). It will only type-check if the Rectangle case’s width field is itself of a tuple type; otherwise it is rejected by the type checker. The syntactic shape is accepted to remain backward compatible with existing code that used semicolons between fields and commas inside tuple patterns.
- Historical example using semicolons between named fields and commas inside tuple patterns (still valid):
- With this RFC, commas are additionally allowed as the separator between sibling-named fields, provided you use them consistently within that list (all commas or all semicolons). Commas found inside a single field’s pattern (such as tuple elements) don’t count towards this consistency rule.

# Drawbacks

- Two equivalent syntaxes (`;` and `,`) can lead to mixed styles across a codebase. Note: within a single pattern, mixing separators is a parse error. Teams may want to standardize via formatting configuration.

# Alternatives

- Maintain status quo (require `;`). This keeps one canonical form but preserves the current surprise factor and parses errors for intuitive code samples.
- Introduce a formatter-only rule to rewrite `,` to `;` (or vice versa) without changing the compiler. This relies entirely on tooling rather than enabling intuitive code to compile.

# Compatibility

Please address all necessary compatibility questions:

- Is this a breaking change?
  - No. This is an additive syntax change. Existing code continues to compile unchanged.

- What happens when previous versions of the F# compiler encounter this design addition as source code?
  - They will fail to parse patterns that use `,` as a separator between named discriminated union fields and report a parse error. To maintain compatibility with older compilers, authors should use `;` separators or upgrade the toolchain.

- What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
  - There is no impact. This is a purely syntactic change affecting only parsing. The generated IL for pattern matches is unchanged, so existing binaries remain fully compatible across compiler versions.

- If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?
  - Not applicable. This RFC does not change or extend FSharp.Core.

# Pragmatics

## Diagnostics

- New parse error when mixing separators within a single named-field pattern list:

  `Inconsistent separators in a pattern. Use either all commas or all semicolons, but not both.`

- Example that triggers this error:

```fsharp
// Not allowed: mixing `;` and `,` between sibling named fields
let area r =
    match r with
    | Rect(width = w; height = h, depth = d) -> w * h * d
```

## Tooling

See Notes on tooling and formatting above. Both `;` and `,` are valid syntaxes; formatters and editor tooling may choose a preferred style or offer configuration. No additional compiler tooling changes are required beyond parsing support.

## Performance

Negligible impact expected. This RFC introduces an alternative separator token in the parser; runtime semantics are unchanged.

## Scaling

No specific scaling dimensions beyond the general source size. The feature scales with typical pattern-matching constructs without introducing superlinear behaviors.

## Culture-aware formatting/parsing

- May need to decide on a preferred style (`;` vs `,`). This RFC does not mandate a formatter preference; both are valid syntaxes.

# Unresolved questions

N/A