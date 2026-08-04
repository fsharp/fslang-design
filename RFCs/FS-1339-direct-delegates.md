# F# RFC FS-1339 - Direct delegate construction

The design suggestion [Emit direct delegates when possible](https://github.com/fsharp/fslang-suggestions/issues/1083) has been marked "approved in principle".
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
* **Delegate identity.** `Delegate.Equals`, `GetHashCode`, and therefore `Delegate.Remove` (event subscribe/unsubscribe) compare by `(Method, Target)`. Closure-wrapped delegates built from the same method are never equal, so F# event-handler removal and de-duplication behave differently than expected.
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

The argument count must equal the target's arity *exactly* — a saturated call to the target itself. Fewer arguments is a partial application of an argument group, and more is an over-application whose extra trailing arguments are consumed by the target's *result* rather than the target; both fail the match. So does a target with no arity information (a parameter or local function value), which has no compiled method to point at — though this is judged on the tree the recognizer actually sees: if inlining has already substituted the value away (e.g. an `inline` function's `[<InlineIfLambda>]` parameter replaced by a lambda whose body forwards to a named function), the recognizer sees the named target and binds it directly.

The recognizer is normalization-independent: before matching, it sees through debug points, effect-free let-bindings (the elided-unit binding of a zero-parameter `Invoke`, the receiver binding of a non-eta delegate over an instance method), applications of immediate lambdas (the method-group coercion and partial-application shells the elaborator produces around member calls), and curried application nesting. Each peeled binding is recorded as an alias from the bound value to the expression flowing into it, and arguments are resolved through the aliases, so the match sees the values that actually flow into the call. This matters because the recognizer also runs in the optimizer on the elaborated tree *before* any optimizer normalization (see below); anything not covered fails the match and conservatively keeps the closure.

Any **leading** arguments (everything before the trailing `Invoke` parameters) are handled as follows:

* **No leading argument** — a static method or module-level function; the delegate carries a `null` `Target`.
* **One leading argument** — bound to the delegate's `Target`. For an instance method this is the receiver (not part of the IL signature); an **immutable** value-type receiver is *boxed* (a copy) at the construction site and the runtime binds the method's unboxing stub, which re-derives the `this` pointer from the box on each invocation — matching the closure's by-value capture. (A struct with mutable fields is instead kept as a closure — see below.) For a static method the leading argument is the first IL parameter, bound via the CLR's "closed over the first argument" delegate mechanism (`newobj Delegate::.ctor(object, native int)` with that argument as `Target`); this is how an **extension member**'s receiver, and a **one-argument partial application** of a static method / module function, become direct. In every case the bound argument must be side-effect-free and must not reference the delegate's `Invoke` parameters. For a **static** target it must additionally be a **known reference type**: the delegate thunk passes `Target` (an `object`) straight into the method's first by-value parameter with no unboxing, so a value-type first parameter has no closed form at all. (This is *not* the same as a value-type instance receiver above, where the unboxing stub means boxing works.) A bare **type parameter** does not qualify — it may be instantiated with a value type, and it is not *known* to be a reference type.
* **Two or more leading arguments** — a partial application that also fixes a receiver, or fixes more than one argument; the CLR closed delegate binds only one argument, so there is no closed form and a closure is kept.

A few structural bails also keep the closure: calls requiring witness arguments, constructor / base / self-init calls, a value-type receiver reachable only as a byref (e.g. through `&someField` rather than a local, which the boxing emission cannot consume), and a `Target` — instance receiver or static first argument — typed as a bare type parameter (an unboxed `!!T` cannot be pushed as the `object` `Target`, and generic code sharing means the emitted IL would be invalid for a value-type instantiation).

An instance receiver whose value type has **mutable fields** is also kept as a closure. Boxing it once as the `Target` would give a single mutable cell that a mutating method updates in place, so mutations would *accumulate* across invocations — whereas the closure calls through a fresh by-value copy each time (an immutable `let`-bound struct is defensively re-copied per call), leaving the receiver frozen. Only an immutable (`[<IsReadOnly>]`, an enum, or a struct with no mutable fields) value-type receiver is boxed directly, where no method can mutate `this` and the two forms cannot be told apart. This preserves the pre-feature by-value semantics; a reference-type receiver is unaffected, since it is shared by identity in both forms.

When the shape matches and all the conditions below hold, the compiler emits the direct form and **no closure class is generated**. Otherwise it falls back to the existing closure path, which is byte-for-byte unchanged.

## Gating: language feature and optimization level

| Condition | Behavior |
|---|---|
| Feature **off** (default `--langversion`) | Always the closure form. **No change from today.** |
| Feature **on**, *non-eta* delegate (`Func<_,_>(f)`) | Direct in both debug and release builds. |
| Feature **on**, *eta-expanded* delegate (`Func<_,_>(fun a b -> f a b)`) | Direct in **release** (`--optimize+`) only; **closure in debug** (`--optimize-`). |

The eta/non-eta distinction is made by whether the `Invoke` parameters are compiler-generated, with one carve-out: a unit-argument eta delegate (`Action(fun () -> f ())`) binds only the compiler-generated elided unit parameter, yet is still classified as eta and follows the eta row above. A *non-eta* delegate's parameters are synthesized by `BuildNewDelegateExpr` (compiler-generated), so the closure carries no user-meaningful names and the delegate is always made direct. An *eta-expanded* delegate's parameters are the user's own lambda variables; in unoptimized builds the closure is kept so those names survive for debugging (locals window, parameter tips). In optimized builds debuggability is not a concern and the forwarding call is preserved through the optimizer (see below), so the direct form is used.

### Preserving the forwarding call through the optimizer

The F# optimizer inlines small function bodies *before* code generation runs. Left unchecked, that would dissolve the forwarding call in optimized builds — the recognizer would have nothing to fire on, and a small target would emit a closure in release while being direct in debug (an "inline race"), with the outcome depending on the target's size. To prevent this, the recognition also runs in the optimizer: when a delegate construction's `Invoke` body is a recognized forwarding call to a directly-bindable target, the target is added to the optimizer's do-not-inline set for the extent of that body, so the call survives to code generation intact. The recognizer is a single module (`DelegateForwarding`) shared by the optimizer and `IlxGen`; the suppression runs only when the language feature is on and optimizations are enabled, and `IlxGen` remains the sole place that validates and emits the direct form.

Only optimizer-*chosen* inlining is suppressed. A target marked `inline` is expanded through the mandatory inlining path, which takes precedence: a delegate over an `inline` function is always a closure, locally and cross-assembly — a deterministic outcome.

The recognizer has no locality guard, so a target imported from a **referenced assembly** takes the same direct path as a local one (the BCL `ILCall` cases already exercise this; cross-assembly *F# value* targets are covered by execution tests). Cross-assembly inlining of a non-`inline` target through the referenced assembly's embedded optimization data goes through the same optimizer path, so it is suppressed in the consuming compilation just as local inlining is.

## Scenarios

Each row groups the delegate-construction cases in the `tests/.../EmittedIL/DirectDelegates` baseline suite that share a shape and outcome; the **Test cases** column lists the canonical case indices folded into that row, also carried in the test-source comments (the indices are stable identifiers and not contiguous).

Within a row none of the following affect the outcome, so they are folded together: the target kind (module fn / static / instance / generic), whether the target is an F# value or an IL/BCL method (`TOp.ILCall`), the delegate type (a built-in `Func`/`Action` or a user-defined delegate), the return type (a `unit`-returning / `void` target), the argument shape (a `unit`-argument delegate), and the receiver's boxity (a value-type receiver, boxed as `Target`).

| Description | Delegate construction | Debug | Release | Test cases |
|---|---|---|---|---|
| eta forwarding to a method | `Func<int,int,int>(fun a b -> accCurried a b)` | closure | direct | 1–5, 10–16, 47, 49, 51 |
| eta *tupled* application to a method | `Func<int,int,int>(fun a b -> accTupled (a, b))` | closure | direct | 31–35 |
| non-eta forwarding to a method | `Func<int,int,int>(accCurried)` | direct | direct | 17–21, 26–30, 46, 48, 50 |
| extension member (receiver bound as `Target`) | `Func<int,int,int>(fun a b -> h.Combine(a, b))` | closure | direct | 52 |
| partial application — no CLR closed form (fixes ≥2 leading args, or a single *value-type* argument) | `Action<int,int>(handler3 1)` | closure | closure | 37–41 |
| not a transparent forwarding — first-class value, computed arg, reordered args, upcast coercion, over-application | `Action<int,int>(handlerParameter)` | closure | closure | 42–45, 55 |
| byref-parameter delegate (mutating body) | `DByref(fun x -> x <- x + 1)` | closure | closure | 53 |
| extension member on a value-type receiver (no closed form) | `Func<int,int,int>(fun a b -> (3).Echo(a, b))` | closure | closure | 54 |

### Receiver evaluation — correctness guard

For an *explicit eta-lambda* like `Func<_,_>(fun a -> recv.M a)`, the closure form re-evaluates `recv` on **every** `Invoke`, whereas a direct delegate evaluates it **once**, at construction, as the `Target`. The rewrite is therefore only applied when that difference is unobservable: the receiver must be side-effect-free (`Optimizer.ExprHasEffect` is `false` — a non-mutable value, a constant, a pure field read) and must not reference the delegate's own `Invoke` parameters. A side-effecting receiver (`getObj().M`), a mutable value, or a fresh allocation keeps the closure. This is verified by an execution test: a counter-bumping receiver remains a closure and is re-evaluated per invocation.

Binding the receiver at construction also changes **when a `null` receiver faults**. The closure form captures the receiver and only dereferences it inside `Invoke`, so a `null` receiver faults (`NullReferenceException`) at the first call — or never, if the target body does not touch `this`. The direct form binds the receiver into the delegate at construction, where the CLR delegate constructor rejects a `null` `this` for an instance method: a **non-virtual** target throws **`ArgumentException`** ("a delegate to an instance method cannot have null 'this'") from `newobj Delegate::.ctor`, and a **virtual** target throws **`NullReferenceException`** from `ldvirtftn` — both at the construction site, before any call. This matches C#, whose `new Func<…>(o.M)` emits the same `ldftn`/`newobj` and faults identically at the creation site.

Note that the related "lazy function" case is *not* affected: `new Action<int>(failwith "")` is eta-expanded by the type checker to `fun arg -> (failwith "") arg` — an *over-application*, since `failwith` itself takes only the message and it is the *returned function* that consumes `arg`. The recognizer's exact-arity requirement (see *Trigger shape*) rejects over-applications, so the construct stays a closure with its existing per-invocation semantics.

## Argument / parameter name impact

The closure form names the `Invoke` parameters: for an eta delegate they are the user's lambda variable names; for a non-eta delegate they are synthesized (`delegateArg0`, … or, with `ImprovedImpliedArgumentNames`, the target's declared argument names). With the direct form there is **no generated `Invoke`** — the delegate points at the target method, so `delegate.Method.GetParameters()` reports the **target method's own declared parameter names**. This is an outwardly-visible change for reflection over parameter names.

## Quotations and FCS

The recognizer runs in the optimizer (where it only withholds an inlining step — it introduces no new tree shapes) and in `IlxGen` (IL emission). Quotations, `FSharp.Compiler.Service` typed-tree consumers, and `Expr` conversions are unaffected — a delegate-construction quotation produces the same quotation node before and after this feature.

# Drawbacks
[drawbacks]: #drawbacks

* It introduces a code-generation difference that depends on language version *and* optimization level, so the precise output for a given source is less uniform than today.
* The direct path adds a recognizer and emission branch to `IlxGen`, plus a recognizer-driven inlining suppression in the optimizer, increasing compiler surface area.

# Alternatives
[alternatives]: #alternatives

* **Do nothing.** F# continues to differ from C#/VB; the interop and identity problems above persist.
* **Always emit direct (no eta/debug distinction).** Rejected because eta-expanded delegates in debug builds would lose the user's lambda parameter names and a friendly stepping experience.

# Compatibility
[compatibility]: #compatibility

* **Is this a breaking change?** Not at the source or type level, and it is opt-in via `--langversion:preview`. It *is* an observable **runtime behavior change** for code that inspects delegates produced by F# compiled with the feature:
  * `Delegate.Method`
  * `Delegate.Target`
  * `delegate.Method.GetParameters()`
  * `Delegate.Equals`/`GetHashCode` (and thus `Delegate.Remove`, event removal, and delegate de-duplication) now compare by the real `(Method, Target)`. Two delegates built from the same method+receiver may now be equal where previously they were not. Removing an event handler created from the same method now succeeds where it might previously have failed.
  * **Evaluation timing.** For the cases made direct the receiver is provably effect-free, so evaluating it once at construction (the direct form) rather than on every `Invoke` (the closure form of an eta-lambda) is unobservable — the *observable* evaluation count is unchanged. Throwing function positions and side-effecting/mutable receivers are deliberately kept as closures. The one shift is a **`null` instance receiver**; see *Receiver evaluation — correctness guard*.
* **Previous compilers encountering this as source code:** there is no new syntax; the feature only changes code generation under the preview flag, so older compilers compile the same source to the existing closure form.
* **Previous compilers encountering compiled binaries:** delegates in such binaries have a different `Method`/`Target`, but no surface/type-level change — consumers compile and run unchanged.
* **FSharp.Core:** no change.

# Pragmatics

## Diagnostics

No new diagnostics. The feature introduces no new syntax and no new error or warning conditions; it is a silent, provably-equivalent code-generation change. When the shape or a guard does not permit the direct form, the compiler silently uses the existing closure form.

## Tooling

* **Debugging.** Unaffected.
* **Auto-complete / tooltips / navigation / colorization / brace matching.** Unaffected — no syntax change.
* **FCS / analyzers / quotations.** Unaffected — the change is confined to the optimizer's inlining choices and IL emission.

## Performance

* **Generated code.** Direct delegate construction is cheaper: no closure type is generated and, for instance delegates, no closure instance is allocated at construction (only the delegate object). Static delegates avoid the static-closure indirection. Invocation is one call shorter: the closure form dispatches the delegate to the closure's `Invoke`, which then calls the target (a forwarding frame the JIT cannot inline away, since the delegate call is indirect), whereas the direct form dispatches straight to the target.
* **Compilation.** A small additional recognizer runs per delegate-construction expression — in the optimizer and in `IlxGen`. Receiver analysis (`ExprHasEffect`, free-variable check) is evaluated lazily and only after cheaper structural checks have passed, and short-circuits, so disqualified delegates do little work.

## Scaling

The relevant dimension is the number of delegate-construction sites and the delegate arity. Both are small in practice (arities of 0–16, the .NET `Func`/`Action` limit; typically 0–3). No scaling concerns.

## Culture-aware formatting/parsing

Not applicable. This RFC does not affect formatting, parsing, numbers, dates, or currencies.

# Unresolved questions
[unresolved]: #unresolved-questions

None.
