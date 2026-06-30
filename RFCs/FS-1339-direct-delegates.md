# F# RFC FS-1339 - Direct delegate construction

The design suggestion [Delegate construction should point directly at the target method](https://github.com/fsharp/fslang-suggestions/issues/1083) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1083) approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/19993)

# Summary
[summary]: #summary

When a delegate (e.g. `System.Func<_,_>`, `System.Action<_>`) is constructed from a method or function, the F# compiler currently always generates an intermediate *closure* class with a synthesized `Invoke` method, and binds the delegate to that `Invoke`. This RFC makes the compiler, where it is provably equivalent, emit the delegate so that it points **directly** at the target method:

```il
// static target
ldnull
ldftn      <target>
newobj     <Delegate>::.ctor(object, native int)

// instance target
<load receiver>
box        <struct>            // value-type receiver only; runtime binds the unboxing stub
ldftn      <target>            // ldvirtftn (with dup) for a virtual target
newobj     <Delegate>::.ctor(object, native int)

// static target with a bound leading argument (extension-member receiver, or a one-argument
// partial application) — the CLR's "closed over the first argument" delegate
<load bound argument>
ldftn      <static target>
newobj     <Delegate>::.ctor(object, native int)
```

# Motivation
[motivation]: #motivation

F#'s closure-based delegate emission means that `delegate.Method` always reports a compiler-generated `Invoke` on an anonymous closure type, and `delegate.Target` is the closure instance rather than the receiver the user supplied. This is surprising and diverges from C#/VB:

* **Reflection / framework interop.** Many .NET frameworks inspect `Delegate.Method` and `Delegate.Target`: ASP.NET routing/minimal APIs, dependency-injection containers, mocking libraries, serializers, and logging/diagnostics that print the bound method name. With F# they see `Invoke` on a closure rather than the intended method, which breaks or degrades these scenarios.
* **Delegate identity.** `Delegate.Equals`, `GetHashCode`, and therefore `Delegate.Combine`/`Delegate.Remove` (event subscribe/unsubscribe) compare by `(Method, Target)`. Closure-wrapped delegates built from the same method are never equal, so F# event-handler removal and de-duplication behave differently than expected.
* **Allocation.** The closure form requires generating a closure type and, for capturing (instance) delegates, allocating an instance of it at every construction. A direct delegate allocates only the delegate object itself.

The suggestion asks that F# emit delegates that point straight at the target method like other .NET languages do.

# Detailed design
[design]: #detailed-design

## Trigger shape

The optimization fires only on the canonical "transparent forwarding" shape that the type checker produces for a delegate built from a method or function value. Concretely, inside the delegate's `Invoke` body the compiler looks for a saturated call

```
target  <leading args…>  p0 p1 … pn
```

whose **trailing** arguments are exactly the delegate's `Invoke` parameters `p0…pn`, forwarded verbatim and in order, where `target` is one of:

* a known F# value reference — a module-level function or a member (`Expr.App(Expr.Val …)`); or
* a direct IL/BCL method call (`TOp.ILCall`, e.g. `System.Math.Max`).

For an F# value reference the forwarding call is *de-tupled by the target's arity* before matching. A tupled target (e.g. `accTupled (x, y)`, a value of arity `[2]`) carries each tupled argument group as a single tuple node — `accTupled (a, b)` is one `(a, b)` argument — exactly the shape the code generator itself de-tuples by the value's arity when it emits the `call`. The recognizer mirrors that de-tupling, so a tupled target compiles to a method taking the elements as separate IL parameters and is as direct-able as its curried counterpart.

Any **leading** arguments (everything before the trailing `Invoke` parameters) are handled as follows:

* **No leading argument** — a static method or module-level function; the delegate carries a `null` `Target`.
* **One leading argument** — bound to the delegate's `Target`. For an instance method this is the receiver (not part of the IL signature); a **value-type** receiver is *boxed* (a copy) at the construction site and the runtime binds the method's unboxing stub, which re-derives the `this` pointer from the box on each invocation — matching the closure's by-value capture. For a static method the leading argument is the first IL parameter, bound via the CLR's "closed over the first argument" delegate mechanism (`newobj Delegate::.ctor(object, native int)` with that argument as `Target`); this is how an **extension member**'s receiver, and a **one-argument partial application** of a static method / module function, become direct. In every case the bound argument must be side-effect-free and must not reference the delegate's `Invoke` parameters. For a **static** target it must additionally be a **reference type**: the delegate thunk passes `Target` (an `object`) straight into the method's first by-value parameter with no unboxing, so a value-type first parameter has no closed form at all. (This is *not* the same as a value-type instance receiver above, where the unboxing stub means boxing works.)
* **Two or more leading arguments** — a partial application that also fixes a receiver, or fixes more than one argument; the CLR closed delegate binds only one argument, so there is no closed form and a closure is kept.

When the shape matches and all the conditions below hold, the compiler emits the direct form and **no closure class is generated**. Otherwise it falls back to the existing closure path, which is byte-for-byte unchanged.

## Gating: language feature and optimization level

| Condition | Behavior |
|---|---|
| Feature **off** (default `--langversion`) | Always the closure form. **No change from today.** |
| Feature **on**, *non-eta* delegate (`Func<_,_>(f)`) | Direct in both debug and release builds. |
| Feature **on**, *eta-expanded* delegate (`Func<_,_>(fun a b -> f a b)`) | Direct in **release** (`--optimize+`) only; **closure in debug** (`--optimize-`). |

The eta/non-eta distinction is made by whether the `Invoke` parameters are compiler-generated. A *non-eta* delegate's parameters are synthesized by `BuildNewDelegateExpr` (compiler-generated), so the closure carries no user-meaningful names and the delegate is always made direct. An *eta-expanded* delegate's parameters are the user's own lambda variables; in unoptimized builds the closure is kept so those names survive for debugging (locals window, parameter tips). In optimized builds debuggability is not a concern and the forwarding call generally survives the optimizer, so the direct form is used (unless the target is inlined away first — see [The inline-race](#the-inline-race)).

### The inline-race

The F# optimizer inlines small function bodies *before* code generation runs. If a delegate target is small and not annotated `[<NoCompilerInlining>]`, in release builds its body is inlined into the `Invoke` body and the forwarding call no longer exists for the recognizer to fire on — so such a target becomes a **closure in release but is direct in debug**. This is the one place the policy table above does not hold uniformly, and is an intentional, documented consequence rather than a bug. Annotating the target `[<NoCompilerInlining>]` (or making it large enough not to be inlined) guarantees the direct form in both configurations. See *Unresolved questions*.

The recognizer has no locality guard, so a target imported from a **referenced assembly** takes the same direct path as a local one (the BCL `ILCall` cases already exercise this; cross-assembly *F# value* targets are covered by execution tests). Cross-assembly inlining therefore widens the race in two ways. A target marked `inline` has its body serialized into the referenced assembly and is **always** inlined at the use site, independent of `--optimize` — so a delegate over it is *always* a closure (a deterministic bail, not a race). A small, unannotated, non-`inline` target can be inlined cross-assembly through the referenced assembly's embedded optimization data, so whether it lands direct or closure in the consumer's release build depends on **the referenced assembly's** compilation (whether it shipped optimization data, and the target's size) rather than on anything the consumer wrote. `[<NoCompilerInlining>]` on the target suppresses this across the boundary just as it does locally.

## Scenarios

Each row groups the delegate-construction cases in the `tests/.../EmittedIL/DirectDelegates` baseline suite that share a shape and outcome; the **Test cases** column lists the canonical case indices folded into that row, also carried in the test-source comments. `‡` marks the **inline-race**: the target is small and unannotated, so the optimizer inlines it before codegen and the forwarding call vanishes (cases 1–5 eta, 17–20 non-eta, 31–35 eta-tupled). The `[<NoCompilerInlining>]` cases (6–9 eta, 22–25 non-eta, 36 eta-tupled) are the same shapes with inlining suppressed, proving they emit direct in release once the race is removed.

Within a row none of the following affect the outcome, so they are folded together: the target kind (module fn / static / instance / generic), whether the target is an F# value or an IL/BCL method (`TOp.ILCall`), the delegate type (a built-in `Func`/`Action` or a user-defined delegate), the return type (a `unit`-returning / `void` target), the argument shape (a `unit`-argument delegate), and the receiver's boxity (a value-type receiver, boxed as `Target`).

| Description | Delegate construction | Debug | Release | Test cases |
|---|---|---|---|---|
| eta forwarding to an *inlinable* method (inline-race) | `Action<int,int>(fun a b -> handlerCurried a b)` | closure | closure‡ | 1, 2, 3, 4, 5 |
| eta forwarding to a non-inlined method | `Func<int,int,int>(fun a b -> accCurried a b)` | closure | direct | 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 47, 49, 51 |
| eta *tupled* application to an *inlinable* method (inline-race) | `Action<int,int>(fun a b -> handlerTupled (a, b))` | closure | closure‡ | 31, 32, 33, 34, 35 |
| eta *tupled* application to a non-inlined method | `Func<int,int,int>(fun a b -> accTupled (a, b))` | closure | direct | 36 |
| non-eta forwarding to an *inlinable* method (inline-race) | `Action<int,int>(handlerCurried)` | direct | closure‡ | 17, 18, 19, 20 |
| non-eta forwarding to a non-inlined method | `Func<int,int,int>(accCurried)` | direct | direct | 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 46, 48, 50 |
| extension member (receiver bound as `Target`) | `Func<int,int,int>(fun a b -> h.Combine(a, b))` | closure | direct | 52 |
| partial application — no CLR closed form (fixes ≥2 leading args, or a single *value-type* argument) | `Action<int,int>(handler3 1)` | closure | closure | 37, 38, 39, 40, 41 |
| not a transparent forwarding — first-class value, computed arg, reordered args, upcast coercion | `Action<int,int>(handler)` | closure | closure | 42, 43, 44, 45 |
| byref-parameter delegate (mutating body) | `DByref(fun x -> x <- x + 1)` | closure | closure | 53 |
| extension member on a value-type receiver (no closed form) | `Func<int,int,int>(fun a b -> (3).Echo(a, b))` | closure | closure | 54 |

### Receiver evaluation — correctness guard

For an *explicit eta-lambda* like `Func<_,_>(fun a -> recv.M a)`, the closure form re-evaluates `recv` on **every** `Invoke`, whereas a direct delegate evaluates it **once**, at construction, as the `Target`. The rewrite is therefore only applied when that difference is unobservable: the receiver must be side-effect-free (`Optimizer.ExprHasEffect` is `false` — a non-mutable value, a constant, a pure field read) and must not reference the delegate's own `Invoke` parameters. A side-effecting receiver (`getObj().M`), a mutable value, or a fresh allocation keeps the closure. This is verified by an execution test: a counter-bumping receiver remains a closure and is re-evaluated per invocation.

Binding the receiver at construction also changes **when a `null` receiver faults**. The closure form captures the receiver and only dereferences it inside `Invoke`, so a `null` receiver faults (`NullReferenceException`) at the first call — or never, if the target body does not touch `this`. The direct form binds the receiver into the delegate at construction, where the CLR delegate constructor rejects a `null` `this` for an instance method: a **non-virtual** target throws **`ArgumentException`** ("a delegate to an instance method cannot have null 'this'") from `newobj Delegate::.ctor`, and a **virtual** target throws **`NullReferenceException`** from `ldvirtftn` — both at the construction site, before any call. This matches C#, whose `new Func<…>(o.M)` emits the same `ldftn`/`newobj` and faults identically at the creation site.

Note that the related "lazy function" case is *not* affected: `new Action<int>(failwith "")` is eta-expanded by the type checker to `fun arg -> (failwith "") arg`, whose function position is an application of `failwith`, not a plain method value, so the recognizer does not match it and it stays a closure with its existing per-invocation semantics.

## Argument / parameter name impact

The closure form names the `Invoke` parameters: for an eta delegate they are the user's lambda variable names; for a non-eta delegate they are synthesized (`delegateArg0`, … or, with `ImprovedImpliedArgumentNames`, the target's declared argument names). With the direct form there is **no generated `Invoke`** — the delegate points at the target method, so `delegate.Method.GetParameters()` reports the **target method's own declared parameter names**. This is an outwardly-visible change for reflection over parameter names. Keeping eta delegates as closures in debug builds specifically preserves the user's chosen lambda parameter names for the debugging experience.

## Quotations and FCS

The recognizer runs only in `IlxGen` (IL emission). Quotations, `FSharp.Compiler.Service` typed-tree consumers, and `Expr` conversions are unaffected — a delegate-construction quotation produces the same quotation node before and after this feature. This is covered by the existing quotation tests.

# Drawbacks
[drawbacks]: #drawbacks

* It introduces a code-generation difference that depends on language version *and* optimization level, plus the inline-race, so the precise output for a given source is less uniform than today (see *Unresolved questions*).
* The direct path adds a recognizer and emission branch to `IlxGen`, increasing compiler surface area.

# Alternatives
[alternatives]: #alternatives

* **Do nothing.** F# continues to differ from C#/VB; the interop and identity problems above persist.
* **Always emit direct (no eta/debug distinction).** Rejected because eta-expanded delegates in debug builds would lose the user's lambda parameter names and a friendly stepping experience.
* **Run the recognizer inside the optimizer (before inlining).** Would remove the inline-race and make debug/release consistent, but is a more invasive change (see *Unresolved questions*).

# Compatibility
[compatibility]: #compatibility

* **Is this a breaking change?** Not at the source or type level, and it is opt-in via `--langversion:preview`. It *is* an observable **runtime behavior change** for code that inspects delegates produced by F# compiled with the feature:
  * `Delegate.Method`
  * `Delegate.Target`
  * `delegate.Method.GetParameters()`
  * `Delegate.Equals`/`GetHashCode` (and thus `Delegate.Combine`/`Delegate.Remove`, event add/remove, and delegate de-duplication) now compare by the real `(Method, Target)`. Two delegates built from the same method+receiver may now be equal where previously they were not. Removing an event handler created from the same method now succeeds where it might previously have failed.
  * **Evaluation timing.** For the cases made direct the receiver is provably effect-free, so evaluating it once at construction (the direct form) rather than on every `Invoke` (the closure form of an eta-lambda) is unobservable — the *observable* evaluation count is unchanged. Throwing function positions and side-effecting/mutable receivers are deliberately kept as closures. The one shift is a **`null` instance receiver**, which now faults at *construction* rather than at first `Invoke` — `ArgumentException` for a non-virtual target, `NullReferenceException` for a virtual one, matching C#. This cannot be guarded (nullness is not statically known); see *Receiver evaluation — correctness guard*.
* **Previous compilers encountering this as source code:** there is no new syntax; the feature only changes code generation under the preview flag, so older compilers compile the same source to the existing closure form.
* **Previous compilers encountering compiled binaries:** delegates in such binaries have a different `Method`/`Target`, but no surface/type-level change — consumers compile and run unchanged.
* **FSharp.Core:** no change.

# Pragmatics

## Diagnostics

No new diagnostics. The feature introduces no new syntax and no new error or warning conditions; it is a silent, provably-equivalent code-generation change. When the shape or a guard does not permit the direct form, the compiler silently uses the existing closure form.

## Tooling

* **Debugging.** Unaffected.
* **Auto-complete / tooltips / navigation / colorization / brace matching.** Unaffected — no syntax change.
* **FCS / analyzers / quotations.** Unaffected — the change is confined to IL emission.

## Performance

* **Generated code.** Direct delegate construction is cheaper: no closure type is generated and, for instance delegates, no closure instance is allocated at construction (only the delegate object). Static delegates avoid the static-closure indirection. Invocation cost is unchanged (one indirected call either way).
* **Compilation.** A small additional recognizer runs per delegate-construction expression. Receiver analysis (`ExprHasEffect`, free-variable check) is evaluated lazily and only after cheaper structural checks have passed, and short-circuits, so disqualified delegates do little work. The recognizer operates on tiny lists (delegate arity, a single receiver).

## Scaling

The relevant dimension is the number of delegate-construction sites and the delegate arity. Both are small in practice (arities of 0–16, the .NET `Func`/`Action` limit; typically 0–3). No scaling concerns.

## Culture-aware formatting/parsing

Not applicable. This RFC does not affect formatting, parsing, numbers, dates, or currencies.

# Unresolved questions
[unresolved]: #unresolved-questions

1. **The inline-race (debug/release inconsistency).** Small, inlinable, unannotated targets are inlined by the optimizer before code generation, so they become closures in release while remaining direct in debug — `delegate.Method` then differs between configurations. Fully resolving this would require running the recognizer **inside the optimizer**, before the head call is inlined, or otherwise suppressing inlining of delegate targets. Is the current behavior acceptable, or should it be addressed? Note this extends *across assemblies* (see [The inline-race](#the-inline-race)): for a non-`inline` target in a referenced assembly, the direct/closure outcome depends on that assembly's optimization data rather than the consumer's source, so the inconsistency is not fully under the consumer's control.
