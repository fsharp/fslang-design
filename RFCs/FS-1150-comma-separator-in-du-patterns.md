# F# RFC FS-1150—Allow ',' as a separator for pattern matching on named discriminated union fields

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/957)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/18881)
- [x] [Discussion](https://github.com/fsharp/fslang-design/discussions/819)

This RFC covers the detailed proposal for this suggestion.

# Summary

This RFC proposes allowing `,` as a separator when matching on named fields of a discriminated union case. 

# Motivation
Today, when naming multiple fields within a discriminated union case pattern, the parser requires `;` to separate the named field patterns.

```fsharp
type Rect = Rect of width:int * height:int
match rect with
| Rect(width = w; height = h) -> w + h

let (Rect(width = w; height = h)) = rect

try
    ...
with
| Rect(width = w; height = h) -> ...
```

This is inconsistent with the existing syntax for discriminated union case constructors, where fields are separated by `,`.

```fsharp
type Rect = Rect of width:int * height:int
let rect = Rect(width = 10, height = 20)
```

After this RFC, the following code will be valid:

```fsharp
type Rect = Rect of width:int * height:int
let rect = Rect(width = 10, height = 20)

let res =
    match rect with
    | Rect(width = w, height = h) -> w + h
    
let (Rect(width = w, height = h)) = rect

try
    ...
with
 | Rect(width = w, height = h) -> ...
```

# Detailed design

- Permit `,` as an alternative separator to `;` between named field sub-patterns within a single DU case pattern.
- Scope: Only applies to named sub-patterns for a single case, such as `Case(name1 = pat1, name2 = pat2, ...)`. The existing `;` separator remains supported.
- Separator consistency: Mixing them is a parse error: `Inconsistent separators in a pattern. Use either all commas or all semicolons, but not both.`
- No changes to runtime semantics. This is a syntactic enhancement only.
- Update the [language specification](https://github.com/fsharp/fslang-spec/blob/main/releases/FSharp-Spec-latest.md#721-union-case-patterns) to reflect the new syntax.
- Update the [language reference](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/pattern-matching#identifier-patterns) to reflect the new syntax.

Grammar changes
- Where the grammar previously recognized `name = pattern` separated by `;`, the production is extended to accept `,` as a separator token as well.
- This applies in the DU case argument pattern position for named fields only; it does not introduce `,` as a separator for record patterns (which already support `;`) nor does it change tuple or list pattern grammar.

Dealing with tuple ambiguity

### 1) Entering named‑field pattern list mode (seeing `name =`)

```fsharp
// DU with named fields
type Rect = Rect of width:int * height:int

let rect = Rect(width = 10, height = 20)

// Using commas between named fields (new)
let area1 =
    match rect with
    | Rect(width = w, height = h) -> w * h

// Using semicolons between named fields (existing)
let area2 =
    match rect with
    | Rect(width = w; height = h) -> w * h

// Mixing separators is a parse error
// | Rect(width = w; height = h, depth = d) -> ...
//                 ^
//   Inconsistent separators in a pattern. Use either all commas or all semicolons, but not both.
```

Notes:
- The moment `width =` is seen, the parser is in “named‑field pattern list” mode for that case.
- All sibling fields must then be named; separators must be consistent (all `,` or all `;`).

---

### 2) No mixing named and positional siblings; how `Rectangle(width = w, h)` parses

```fsharp
// Case where the first field is a tuple, so a tuple pattern inside the named field is valid
type Rectangle = Rectangle of width:(int * int) * height:int

let ok (r: Rectangle) =
    match r with
    // Parsed as width = (w1, w2); outer comma separates named fields
    | Rectangle(width = (w1, w2), height = h) -> w1 + w2 + h

let alsoOk (r: Rectangle) =
    match r with
    // Parsed as width = (w1, w2)  (tuple pattern bound to the single named field)
    | Rectangle(width = w1, w2; height = h) -> w1 + w2 + h

let alsoOk2 (r: Rectangle) =
    match r with
    // Using commas as field separators; the comma after w1 is part of the tuple bound to "width"
    | Rectangle(width = w1, w2, height = h) -> w1 + w2 + h

// Case where the first field is NOT a tuple; then `Rectangle(width = w, h)` type‑checks only if `width` is tuple‑typed
type Rectangle2 = Rectangle2 of width:int * height:int

let notOk (r: Rectangle2) =
    match r with
    // Parsed as width = (w, h)    <-- implies width is a tuple, but here width:int
    // This will be rejected during type checking
    | Rectangle2(width = w, h) -> w + h
```

Notes:
- Once any named field appears, positional siblings like `..., h)` are not allowed as “separate siblings”. They are parsed as part of the named field’s pattern: `width = (w, h)`.
- Whether that then type‑checks depends on the declared type of `width`.

---

### 3) Parentheses and nesting: commas are scoped to the nested pattern

```fsharp
type A = A of x:int * y:int * z:int * t:(int * int)

let ex1 (a: A) =
    match a with
    // Comma inside (u, v) belongs to the tuple pattern for x
    | A(x = (u, v), y = y1, z = z1, t = (p, q)) -> u + v + y1 + z1 + p + q

let ex2 (a: A) =
    match a with
    // Semicolons between named fields; inner tuple commas unaffected
    | A(x = (u, v); y = y1; z = z1; t = (p, q)) -> u + v + y1 + z1 + p + q
```

Notes:
- The tuple commas inside `(u, v)` and `(p, q)` are unrelated to the commas/semicolons separating sibling-named fields.

---

### 4) Commas after `name =` bind to that field’s pattern unless another `name2 =` appears

```fsharp
type B = B of t:(int * int) * u:int

let ex (b: B) =
    match b with
    // Interpreted as t = (a, b), u = c
    | B(t = a, b, u = c) -> a + b + c

let ex2 (b: B) =
    match b with
    // Explicit parentheses make it clear: t = (a, b), u = c
    | B(t = (a, b), u = c) -> a + b + c

let ex3 (b: B) =
    match b with
    // Using semicolons between named fields; the comma after a is part of t's tuple pattern
    | B(t = a, b; u = c) -> a + b + c

// Mixed separators in the same sibling list are not allowed (parse error):
// | B(t = a, b, u = c; v = d) -> ...
//             ^ mix of , and ; in one list
```

Notes:
- `name = p1, p2` is parsed as `name = (p1, p2)` unless and until another `name2 =` starts, in which case the preceding comma ends the tuple and starts the next named field.

---

### 5) Single tuple‑typed field vs. named fields declared on the case

```fsharp
// A single, unnamed tuple field
type P1 = P1 of (int * int)

let tuplePattern (p: P1) =
    match p with
    // Standard tuple pattern (no names exist in the case) — OK
    | P1(a, b) -> a + b

// No named fields exist, so this is NOT allowed:
// | P1(x = a, y = b) -> ...   // parse/type error

// Two named fields declared on the case
type P2 = P2 of x:int * y:int

let namedOrPositional (p: P2) =
    match p with
    // Using names — OK
    | P2(x = a, y = b) -> a + b

let positionalAlsoOk (p: P2) =
    match p with
    // Positional pattern is still OK for named fields
    | P2(a, b) -> a + b

let wrongShape (p: P2) =
    match p with
    // Parsed as x = (a, b), but x:int — type error
    | P2(x = a, b) -> a + b
```

Notes:
- If the case is declared as `P1 of (int * int)`, tuple patterns like `P1(a, b)` are the way to match; there are no names.
- If the case is `P2 of x:int * y:int`, you may use names or positional patterns. But `x = a, b` means `x = (a, b)`, which won’t type‑check since `x` is not a tuple.

---

### 6) Newlines between named fields and trailing separators

```fsharp
type R = R of a:int * b:int * c:int

let exNewlines (r: R) =
    match r with
    | R(
        a = x,  // newline as separator OK with ,
        b = y,
        c = z
      ) -> x + y + z

let exNewlinesSemicolons (r: R) =
    match r with
    | R(
        a = x;  // newline as separator OK with ;
        b = y;
        c = z
      ) -> x + y + z

Trailing semicolon was already allowed.
// | R(a = x; b = y; c = z;) -> ...   // trailing semicolon — OK
// | R(a = x, b = y, c = z,) -> ...   // trailing comma — OK
```

Notes:
- Newlines can separate fields as today. Trailing `,` or `;` after the last field are allowed.

---

### 7) Historical compatibility: semicolons between named fields and commas inside a single field’s tuple pattern

```fsharp
type RectT = RectT of width:(int * int) * height:int

let legacyStyle (r: RectT) =
    match r with
    // Semicolons separate named fields; comma inside width’s tuple pattern
    | RectT(width = x, y; height = h) -> x + y + h

// With this RFC, commas may also separate sibling named fields consistently:
let newStyle (r: RectT) =
    match r with
    | RectT(width = (x, y), height = h) -> x + y + h
```

Notes:
- The RFC keeps prior parsing: `name = p1, p2` binds as a tuple for that single field, unless another `name2 =` appears.

---

### 8) Clear “practical intuition” example

```fsharp
type C = C of left:int * right:int * payload:(int * int)

let intuition (c: C) =
    match c with
    // Because we used name = somewhere, every sibling must be name = ...
    // Commas between siblings behave like semicolons; commas inside parentheses belong to that nested pattern
    | C(left = l, right = r, payload = (x, y)) -> l + r + x + y
```

Rule of thumb:
- If you write `name =` anywhere in the case’s parentheses, every sibling must also be `name = ...`. Commas between those siblings behave like today’s semicolons; commas inside parentheses belong to tuple (or other nested) patterns.

---

Examples of various forms and whether they parse and type-check:
- `Case(x = a, y = b)` → named‑field mode, commas separate named siblings — OK when names exist.
- `Case(x = a; y = b)` → same with semicolons — OK.
- `Case(x = a, b)` → parsed as `x = (a, b)` — OK only if `x` is tuple‑typed.
- `Case(x = (a, b), y = c)` → explicit nested tuple for `x`, commas separate siblings — OK.
- `Case(x = a, y = b, z = c,)` → trailing comma — OK.
- `Case(x = a; y = b, z = c)` → mixed separators — parse error.
- `Case(a, b)` with case `Case of (int * int)` → tuple pattern — OK.
- `Case(x = a, y = b)` when a case has no names → not allowed.


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