# F# RFC FS-1337 - Optional Automatic File Order (`--file-order-auto+`)

- [x] [Suggestion: Syntactically describe dependencies between files (#309)](https://github.com/fsharp/fslang-suggestions/issues/309), see Alternatives
- [ ] Approved in principle
- [x] [Implementation: PR #19647](https://github.com/dotnet/fsharp/pull/19647)
- [ ] Discussion

## Summary

Add an opt-in compiler flag, `--file-order-auto+` (off by default), that lets the compiler figure out the file order itself by reading what each file declares and what it references. List your source files (impl `.fs` and signature `.fsi`) in any order in the `.fsproj`, and the compiler topologically sorts them before type checking. Files that mutually reference each other get wrapped in a synthesised `namespace rec` on the build path, so cross-file mutual recursion just works without `and`-chains. Wire it through MSBuild with `<FSharpAutoFileOrder>true</FSharpAutoFileOrder>`. F# Compiler Service (FCS) hosts (Ionide and friends) opt in via `FSharpProjectOptions.OtherOptions`. F# Interactive (FSI) is not targeted by this RFC; multi-file `#load` semantics are untouched.

The flag changes nothing about type inference, module/namespace semantics, accessibility, signature files, or `module rec`/`namespace rec`. With the flag off, this is byte-identical to upstream.

## Motivation

I love F#. I've also bounced off using it more, for years, mainly because of one thing: having to maintain a topological order of source files in `.fsproj`, and the resulting reliance on `and`-joined recursive type chains to work around it. It's accidental complexity in a language that otherwise does so much right.

Issue [#309](https://github.com/fsharp/fslang-suggestions/issues/309) has been tracking this whole space since 2014, with a bunch of distinct proposals: explicit file-to-file imports (TypeScript-style), `#require` directives, `fileorder.txt`, project-file changes. They all share a goal (get rid of the manual ordering tax) and they all require some user-visible change (new syntax, new files, manual migration).

This RFC takes the lowest-friction path: opt in, and the compiler figures it out. No new syntax, no edits to source files, no new project conventions beyond a single boolean property. People who want the pain to go away flip the property; people who want explicit control keep manual ordering, or layer a future explicit-import feature on top.

The framing matches what @dsyme and @nojaf were sketching in #309 [in 2022](https://github.com/fsharp/fslang-suggestions/issues/309#issuecomment-1290876076): an opt-in mechanism that, when adopted in large codebases, unlocks parallelization and finer-grained incremental checking. This RFC delivers that mechanism via auto-inference. An explicit-import RFC could deliver the same mechanism via manual annotation. Both are useful for different audiences, and they shouldn't compete.

## Design

The flag inserts a dependency-ordering pass between parse and check:

```
parsedInputs ──▶ [ enter phase: stub TcEnv ] ──▶
              ──▶ [ symbol collection: extract decls + refs ] ──▶
              ──▶ [ dep graph + Tarjan SCC ] ──▶
              ──▶ [ apply file order, synthesise cycle groups ] ──▶
              ──▶ check
```

Three sub-systems do the work:

1. Symbol collection walks each parsed AST and pulls out `(top-level modules, opens, identifier references)` per file. It reuses the existing `FileContentMapping` walker that powers `--graphBased` compilation, with one additive variant on `FileContentEntry` (`FullPathIdentifier`) carrying the trailing segment that graph-based intentionally truncates.

2. Enter phase pre-populates `TcEnv` with type stubs (with type parameters; no module stubs because module stubs collide with real declarations and produce `FS0245`) so cross-file type references resolve regardless of file order. Conceptually similar to Dotty's Enter phase.

3. File ordering runs Tarjan's SCC over the dependency graph. Single-file SCCs get topologically sorted; multi-file SCCs become cycle groups. Tie-breaking is deterministic by original `.fsproj` index. The export map is keyed on qualified names with kind tagging (`Module | Type | Value | Member`) so the analyser can tell the difference between a `Random.X` cross-file reference and a phantom `Result.X` collision.

`[<AutoOpen>]` aliases live in a separate `aliasMap` consulted only as a resolution fallback; never mixed into the main map. The first three attempts at putting AutoOpened content into the main `exportMap` regressed Suave (30 → 200 errors) and Expecto (0 → 6) because aliases share prefixes and the cycle detector saw phantom mutual deps. The split fixes it.

`.fsi`/`.fs` pairs are collapsed to one logical contributor for export-map purposes. A sig→impl edge redirect ensures consumers depending on a sig file get sorted after the impl, keeping the pair adjacent and consumers correct.

### Cycle group synthesis (build path only)

Files in an SCC of size > 1 are wrapped into one `ParsedImplFileInput` whose top-level `SynModuleOrNamespace` entries get `isRecursive = true`, effectively a `namespace rec` covering the original modules. This is what makes cross-file `type Tree ↔ Forest` compile without `and`-chains. `open` declarations inside the synthesised block are hoisted to the front of each module/namespace (the FS3200 fix). Cross-namespace cycle groups fall back to original order to avoid FS0247 (a synthesised `module Y` inside `namespace rec X` would conflict with the original `namespace X.Y`).

FCS does not synthesise cycle groups in this RFC. IDE diagnostics for cycle-heavy projects show the cycle as a normal type error; the build path resolves it. This is called out in the migration docs. Adding FCS-level cycle synthesis is a follow-on RFC because it requires designing incremental graph invalidation.

### `and` keyword deprecation (FS3887)

When `--file-order-auto+` is set, `and`-joined type chains emit warning FS3887 ("The 'and' keyword for mutually recursive types is unnecessary when using `--file-order-auto`. Consider placing types in separate declarations. This keyword may be removed in a future version."). Suppressable via `--nowarn:3887` or `<NoWarn>3887</NoWarn>`. Silent in manual mode. The `and` keyword itself is not deprecated globally; only its use as a workaround for cross-file ordering becomes redundant.

## Example #1 - basic auto-order

```fsharp
// Program.fs (listed FIRST in the .fsproj)
module Program

[<EntryPoint>]
let main _ =
    Geometry.area 2.5 |> printfn "area = %f"
    0

// Geometry.fs
module Geometry
let area r = MathHelpers.pi * r * r

// MathHelpers.fs
module MathHelpers
let pi = 3.141592653589793
```

`.fsproj`:

```xml
<PropertyGroup>
  <FSharpAutoFileOrder>true</FSharpAutoFileOrder>
</PropertyGroup>
<ItemGroup>
  <Compile Include="Program.fs" />
  <Compile Include="Geometry.fs" />
  <Compile Include="MathHelpers.fs" />
</ItemGroup>
```

Builds and runs. Without the property, this is the canonical "Geometry is not defined" upstream failure.

## Example #2 - cross-file mutual recursion (cycle group)

```fsharp
// Tree.fs
module Tree
type Tree =
    | Leaf
    | Branch of Forest.Forest

// Forest.fs
module Forest
type Forest = Tree.Tree list
```

Under `--file-order-auto+` these two files form a cycle group and get wrapped in a synthesised `namespace rec`. Compiles cleanly; no `and` keyword needed.

## Example #3 - `.fsi`/`.fs` pair, listed out of order

```fsharp
// B.fs (listed first)
module B
let b = A.a 42

// A.fs
module A
let a x = x + 1

// A.fsi (listed last)
module A
val a: int -> int
```

The auto-order pass redirects sig→impl dependency edges, so `A.fsi` and `A.fs` end up adjacent before `B.fs`. Compiles regardless of input order.

## Spec changes

- **`--file-order-auto[+|-]`** added to compiler options. Off by default.
- **`<FSharpAutoFileOrder>` MSBuild property.** SDK passes `--file-order-auto+` when the property is `true`.
- **New `FullPathIdentifier of LongIdent` variant on `FileContentEntry`** in the existing `GraphChecking` module. Graph-based type checking ignores this entry; `--file-order-auto+` keys dependencies on it. Single source of truth for the AST walker, shared between graph-based and auto-mode.
- **Warning FS3887** (`chkAndKeywordDeprecatedWithFileOrderAuto`), gated on auto-mode.
- **Docs.** Document the flag in the F# Language Reference under "Compiler Options" with a cross-reference to a new "File Order" section explaining the auto-mode behaviour, cycle group semantics, and known limitations.

No grammar changes. No syntax additions. No changes to type inference, accessibility, or signature semantics.

## Drawbacks

- **A second valid mode for F# projects.** Users may encounter codebases that compile under one mode and not the other. The off-by-default policy and the diagnostic-parity guarantee mitigate this: auto-mode never produces a new error category that manual mode wouldn't also produce. Only the file-order errors disappear.
- **FCS does not synthesise cycle groups.** A project that compiles on the build path because of cycle synthesis will show a type error in the IDE. Follow-on RFC will address it once incremental invalidation is designed.
- **`dotnet fsi` is not wired.** FSI multi-file invocations are unaffected. Existing `#load` semantics are untouched.
- **One-time per-project pre-parse cost** when the flag is on. No parsing changes for incremental rebuilds.
- **Resolution layer complexity.** Kind-aware matching, AutoOpen aliasMap, surgical single-ident capture at function-application heads, sig→impl redirect, cross-namespace cycle guard. Each of these exists because the F# language is subtle, and each was driven by a specific OSS failure mode (see PR #19647's `docs/file-order-auto-design.md`). Future maintainers will need to understand them.

## Alternatives

### Explicit file-to-file imports (#309)

The big alternative is the TypeScript/JS-style explicit import approach @dsyme prototyped in #309:

```fsharp
import * from "../AbstractIL/il"
// or
from "../AbstractIL/il" open FSharp.Compiler.AbstractIL
```

This makes the dep graph explicit and editor-navigable, at the cost of new syntax and a manual migration. Pros: better IDE navigation, more control, aligns with TypeScript (Fable benefit). Cons: every file needs to be touched on adoption, and it opens a separate large design space (selective imports, aliasing, project-relative vs. file-relative paths) that's been hard to converge on.

This RFC doesn't preclude that. An explicit `import` syntax could layer on top of `--file-order-auto+`: explicit edges would refine the inferred graph (overriding auto-mode for ambiguous cases, or adding cross-project edges auto-mode can't see). Two different audiences ("I want zero migration cost" vs. "I want fine-grained navigable imports") both well-served if both exist.

### `fileorder.txt` / `fileorder.fsx`

External order file. Solves "where does the ordering live" without compiler changes. Rejected here because it's a workaround, not a fix: the user still has to maintain a topological order somewhere. Auto-inference removes the maintenance burden entirely.

### `#require` / `#load`-style in-source declarations

Same trade-offs as the explicit-import approach above; same conclusion (orthogonal to this RFC, defer).

### Per-file `module rec` / `namespace rec` everywhere

Not actually an alternative; those are file-internal recursion mechanisms. They don't address cross-file ordering.

## Prior art

- **Python, JS/TS, Rust, Haskell** all derive build order from import graphs. F# is the outlier.
- **F#'s own `--graphBased`** mode already extracts a per-file dep graph for parallelization. This RFC reuses that infrastructure (`FileContentMapping`) with one additive variant; resolution rules and consumer code are separate.
- **Scala 3 (Dotty)'s Enter phase** is the inspiration for the symbol-collection pre-pass: pre-populate type info before sequential checking so cross-file references resolve.

## Validation

The implementation in PR #19647 has been validated against:

- **Targeted ComponentTests:** 21 [<Fact>]s (`TypeChecks.FileOrderAutoTests` × 13, `FSharpChecker.FileOrderAutoIncremental` × 8) covering misordered files, cycle synthesis, `.fsi` pairing, SRTP/record/union/operator-overload inference, edit propagation, transitive deps, signature-file shielding, file add/remove, edge addition, fsproj reorder being a no-op.
- **Diagnostic parity** (3 [<Fact>]s): the same FS0001 / FS0003 / FS0039 errors fire under both modes for the same broken source.
- **Existing graph-based ComponentTests:** 209 / 209 pass after the FCM unification (3 unit-test goldens updated for the new `FullPathIdentifier` entries).
- **Per-namespace ComponentTests sweep:** ~6,500 tests across 16 namespaces (Conformance, EmittedIL, ErrorMessages, Language, Signatures, etc.), 0 failures. Single-process full sweep OOMs on the dev's 4 GB heap-cap; per-namespace runs cover the same surface.
- **Real-world OSS sweep:** Argu, FsCheck, FSharpPlus (86 .fs files, heavy SRTP + AutoOpen + nested modules), FsToolkit.ErrorHandling, Expecto, FSharp.Data.Json.Core, Fable.Promise. Auto-mode adds zero errors over baseline on every buildable target. Suave's pre-existing .NET 10 errors reproduce byte-identically under auto-mode (`diff` is empty).

## Open questions

1. Should explicit `import` / `from "..." import ...` syntax (per #309) eventually layer on top of this? This RFC defers; a separate RFC should evaluate the explicit-import direction with this auto-mode in place.
2. FCS-level cycle group synthesis is a known gap. In this RFC, follow-on, or out of scope? My lean: follow-on, because it requires incremental graph-invalidation design.
3. `dotnet fsi` integration. Should the flag be wired into FSI's multi-file mode? My lean: yes, but as a follow-on RFC; FSI's `#load` semantics interact with auto-ordering in non-obvious ways.
4. Performance characterisation on a 1k+ file project. Not yet measured. The auto-order pass adds a one-time per-project parse + walk; subsequent rebuilds use existing caches. A benchmark on the F# compiler itself (the obvious large F# project) is the natural next validation step.
