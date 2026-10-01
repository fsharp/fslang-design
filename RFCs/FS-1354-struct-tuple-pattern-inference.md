# F# RFC FS-1354 - Struct-ness inference for tuple patterns

The design suggestion [More struct tuple inference](https://github.com/fsharp/fslang-suggestions/issues/988) has been marked ["approved in principle"](https://github.com/fsharp/fslang-suggestions/issues/988#issuecomment-799798848). This RFC follows [the design of Don Syme](https://github.com/fsharp/fslang-suggestions/issues/988#issuecomment-809501581).

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/988)
- [x] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

# Summary

A tuple pattern without `struct` gets its kind (struct or reference) from type inference, so later code can decide it. If nothing decides it, the kind is reference, as today. This implements the [struct-ness inference extension of FS-1006](../FSharp-4.1/FS-1006-struct-tuples.md#possible-extension-structness-inference) for patterns, with a per-declaration default. The preview feature is `TupleKindInference`.

```fsharp
let s, c = Math.SinCos 1.0                              // today FS0193
xs |> Array.iter (fun (x, y) -> printfn "%d %d" x y)    // xs: struct (int * int)[]; today FS0001
```

# Motivation

The F# spec (§5.1.3.1) says that the struct-ness of tuple patterns "is inferred in the F# type inference process". The compiler infers it only from a tuple type known when the pattern is checked, and never for `fun (a, b) ->`. Thus `match` and `for` work on struct tuples, but `let a, b = e` does not: the pattern is checked before `e` (§14.6.3). C# APIs return `ValueTuple`, so interop meets this often.

# Detailed design

Every tuple type has a *kind*: struct or reference. During inference, a kind can be *undetermined*. A tuple type written in source is determined: `t1 * t2` is reference, `struct (t1 * t2)` is struct.

## Tuple patterns

When a tuple pattern without `struct` is checked against a tuple type, it takes that kind, determined or not. Otherwise, its type is a tuple type with a new undetermined kind. `struct (p1, ..., pn)` is struct, as today.

Lambda parameter groups `fun (a, b) ->` are tuple patterns. The fields of a union case or exception with two or more fields (`Case (a, b)`) are not tuple patterns. Anonymous records are out of scope: their struct-ness is part of the type identity.

## Argument lists

A parameter written `(p1, ..., pn)` directly in the head of a let-bound function, member, constructor or primary constructor is an argument list. It stays a reference tuple of named parameters. Any other parameter, such as `((a, b))` or `((a, b) as t)`, is one parameter whose tuple patterns follow [Tuple patterns](#tuple-patterns).

## Tuple expressions

A tuple expression without `struct` takes the kind of a tuple expected type, determined or not; otherwise it is reference, as today. Thus `let ((a, b) as t) = 1, 2 in takesStruct t` is struct.

## How a kind is decided

1. **Unification.** When two tuple types unify, or one must subsume the other, their kinds unify. An undetermined kind takes the other kind; two undetermined kinds become one. Struct against reference gives today's error there (FS0001, or FS0193 at a subsumption).
2. **Constraints.** `'T : struct`, `'T : unmanaged`, `'T : (new: unit -> 'T)`, and a coercion or subtype constraint to `System.ValueType`, decide struct. `'T : not struct` decides reference.
3. **Observation.** Any other step whose result depends on the kind first makes an undetermined kind reference. A test whether two tuple types could unify does not depend on it. Such steps include member lookup (`v.Item1`), an SRTP constraint whose support is the tuple type, the argument count of an overloaded method used as a function value (`v |> Math.Max`), the `op_Implicit` search (FS-1093) between a tuple type and a non-tuple type, coercions, type tests and subtype constraints to a non-tuple type that is not a type variable (`:> obj`, `:?`, `#I`, a method parameter of such a type), and a use of a function value whose tuple domain has a non-sealed element type that is not a type variable (§14.4.3). Condensation (§14.6.8) reads an undetermined tuple domain as reference without deciding it.
4. **Overload resolution.** Phase 1 reads every undetermined kind in the call as reference: in argument and parameter types and, where resolution compares return types (`op_Explicit`, `op_Implicit`, `[<AllowOverloadOnReturnType>]` in preview, unfilled out arguments), in return types. If a candidate applies, these kinds become reference. Otherwise, phase 2 resolves again with the kinds undetermined, and rules 1 to 3 apply to the selected candidate. If phase 2 also fails, the phase 1 error is reported. The same two phases resolve SRTP member constraints. Lambda argument inference from candidates filters with phase 1, then phase 2 if none remains; this filtering decides no kinds. With one candidate and no return-type comparison, rules 1 to 3 apply.

## When an undetermined kind becomes reference

Kinds are never generalized, including in `inline` functions (FS-1006). An undetermined kind becomes reference when its declaration has been checked: a module-level `let`, `let rec` or `do`, or a type definition group with its `let` bindings and members; in a `module rec` or `namespace rec`, it is the recursive group. Until then, any later code in the declaration can decide the kind, even after a local function is generalized.

## Compiled form

A lambda parameter group of struct kind compiles to one `ValueTuple<...>` parameter, as `fun ((a, b)) ->` does today. A group of reference kind keeps today's IL. `let f v = let a, b = v in a + b` compiles to two `int` parameters as today, or to one `ValueTuple<int, int>` parameter if the declaration decides struct.

## Behaviour

`make: unit -> struct (int * int)`, `takesStruct: struct (int * int) -> int`, `takesRef: int * int -> int`; `v` is an unannotated parameter.

| Code | Today | With this RFC |
|---|---|---|
| `let a, b = make ()` | FS0001 | struct |
| `List.map (fun (a, b) -> a + b) [ struct (1, 2) ]` | FS0001 | struct |
| `let g v = (let a, b = v in a) + takesStruct v` | FS0001 | struct |
| `let r = (let h v = (let a, b = v in a + b) in h (struct (1, 2)))` | FS0001 | struct |
| `type C() =`<br>`member _.M v = (let a, b = v in a + b)`<br>`member x.N () = x.M (struct (1, 2))` | FS0001 | struct |
| `let g v = let a, b = v in a + b`; a later declaration calls `g (struct (1, 2))` | FS0001 | FS0001 |
| `let f (a, b) = a + b`, then `f (struct (1, 2))` | FS0001 | FS0001 |
| `let t = (1, 2) in takesStruct t` | FS0001 | FS0001 |
| `O.M(x: struct (int * int))`, `O.M(x: int * int)`; `let a, b = v in O.M v` | reference overload | unchanged |
| `O.M(x: struct (int * int))`, `O.M(x: string)`; `let a, b = v in O.M v` | FS0041 | struct |
| `let a, b = v in takesStruct v + takesRef v` | FS0001 at `takesStruct v` | FS0001 at `takesRef v` |

## Interactions

- **Quotations**: a struct pattern or lambda group quotes as `match` on a struct tuple or `fun ((a, b))` does today; a reference lambda group keeps `Lambda (tupledArg, ...)`.
- **SRTP**: [rules 3 and 4](#how-a-kind-is-decided). With [FS-1342](https://github.com/fsharp/fslang-design/pull/844), `fst v` on an undetermined kind selects the reference overload in phase 1.
- **Type providers**: provided types have determined kinds; provided methods are ordinary overload candidates.
- **Signature files** do not decide kinds ([deadline](#when-an-undetermined-kind-becomes-reference)): `val f: struct (int * int) -> int` against `let f v = let a, b = v in a + b` stays FS0034.
- **[FS-1344](https://github.com/fsharp/fslang-design/pull/840)** (`Deconstruct` patterns): its unknown-input case gets an undetermined kind, and this RFC rejects its right-hand-side-first option ([Alternatives](#alternatives)).

# Changes to the F# spec

- §5.1.3.1: point "is inferred" to a new §7 section with [Tuple patterns](#tuple-patterns) and [Argument lists](#argument-lists).
- §6.3.2: replace the pseudo-type `S` with [Tuple expressions](#tuple-expressions). §14.11: a pattern or lambda parameter group of struct kind has tuple length 1 ([Compiled form](#compiled-form)).
- §14.5: rules 1 to 3. §14.4 and §14.5.4: rule 4. §14.1.5, §14.2.3, §14.4.3 and §14.6.8: the cases of rule 3.
- §14.6.7 Generalization: add [the deadline](#when-an-undetermined-kind-becomes-reference).

# Drawbacks

- Code after a pattern can decide its kind. A step of rules 3 or 4 before that code fixes reference: `let a, b = v in Console.WriteLine v; takesStruct v` still fails.
- The compiled signature of a member or function can depend on later code in its declaration, such as another member's body. Public APIs should annotate tuple parameters. A body edit can be a rude edit for Hot Reload.

# Alternatives

- **Status quo**: write `struct (a, b)`; `fun ((a, b)) ->` works only for a known domain.
- **Infer only lambda parameter groups from a known type** (Don Syme's first step; closed draft [dotnet/fsharp#18194](https://github.com/dotnet/fsharp/pull/18194)). This misses `let`, `List.map (fun (a, b) -> ...) xs` and later uses.
- **Decide after the right-hand side**, or **check it first**. The first misses `let g v = let a, b = v in takesStruct v`. The second breaks `let (a: int64), b = 1, 2`, which gives `1L` today.
- **Also defer tuple expressions** (§6.3.2 `S` for all of them): outside the scope of #988.
- **Overload resolution without phase 2**: simpler and equally compatible, but `O.M v` with `struct (int * int)` and `string` overloads stays FS0041.
- **Default at the end of the file**, as FS-1006 proposes: a later declaration could change an earlier compiled signature.
- **Default at every local generalization.** Local helpers could not take struct tuples.

# Prior art

- [FS-1030](../FSharp-4.6/FS-1030-anonymous-records.md#structness-inference) added inference from a known type for tuple expressions. [dotnet/fsharp#14473](https://github.com/dotnet/fsharp/issues/14473) closed known-type pattern inference as By Design; T-Gro asked that such a feature also cover `let a, b = struct (1, 2)`.
- Inference variables with defaults: byref kinds ([FS-1053](../FSharp-4.5/FS-1053-span.md)) and nullness ([FS-1060](../FSharp-9.0/FS-1060-nullable-reference-types.md)).
- C# types the right-hand side of `var (a, b) = e` first and has one tuple literal kind. OxCaml `#(a, b)` and GHC `(# a, b #)` mark unboxed tuple patterns explicitly.

# Compatibility

- **Not breaking.** (1) In a program that compiles today, nothing decides struct for a pattern without a known tuple type: rule 1 with a struct tuple, rule 2 and phase 2 of rule 4 all fail on a reference tuple. In overload resolution, where such a failure only removes a candidate, phase 1 keeps today's candidates. (2) Every other step, and the deadline, treats an undetermined kind as reference.
- Programs that fail today with FS0001, FS0193 or FS0041 can compile. Older compilers, and this one with the feature off, report these errors as today.
- **Binaries**: kinds are determined before code generation; the metadata format does not change.

# Interop

C# and reflection see `ValueTuple` in a signature or value only where struct is inferred ([Compiled form](#compiled-form)). No planned C# feature is known to interact with this one.

# Pragmatics

## Diagnostics

No new diagnostics; [rule 1](#how-a-kind-is-decided) says where a conflict is reported.

## Tooling

- **Tooltips and FCS queries** show final kinds. Inference messages and kinds left undetermined by errors show reference.
- **Parenthesis analyzers and formatters** ([#988](https://github.com/fsharp/fslang-suggestions/issues/988#issuecomment-1754880263)): the FCS unnecessary-parentheses check reports the inner parentheses of `fun ((a, b))` today, though removing them breaks struct arguments. It must report them only when the feature is on. Tools must not remove `struct` from a pattern without type information.
- **Refactorings** between `let f = fun (a, b) ->` and `let f (a, b) =` can change the kind. An incomplete `v.` decides reference until completed; completion, navigation and colorization do not change.
- **Debugging**: patterns bind the same locals. Parameters follow [Compiled form](#compiled-form).

## Performance

One kind variable per tuple pattern; no code is checked again. Phase 2 of rule 4 runs only where phase 1 fails. A tuple expression that shares an inferred struct kind allocates no heap object; large struct tuples are copied by value.

## Scaling

Dimension: tuple patterns per declaration, a few hundred by hand and tens of thousands in generated code. Compile time is linear.

## Culture-aware formatting/parsing

Not applicable.

# Unresolved questions

- Should a kind conflict show, as a related location, the code that decided the kind?
- Should a public member with an inferred tuple parameter kind get an informational diagnostic?
