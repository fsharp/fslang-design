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
ldftn      <target>            // ldvirtftn (with dup) for a virtual target
newobj     <Delegate>::.ctor(object, native int)
```

As a result `delegate.Method` is the *real* target `MethodInfo` and `delegate.Target` is the *real* receiver (or `null` for a static target), exactly as the equivalent C# `new Func<…>(obj.Method)` produces. The change is gated behind a `--langversion:preview` language feature (`DirectDelegateConstruction`); without it, code generation is unchanged.

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

Any **leading** arguments are the method's receiver (for an instance method) or, if there are leading arguments where none are expected, a partial application (which is *not* rewritten).

When the shape matches and all the conditions below hold, the compiler emits the direct form and **no closure class is generated**. Otherwise it falls back to the existing closure path, which is byte-for-byte unchanged.

## Gating: language feature and optimization level

| Condition | Behavior |
|---|---|
| Feature **off** (default `--langversion`) | Always the closure form. **No change from today.** |
| Feature **on**, *non-eta* delegate (`Func<_,_>(f)`) | Direct in both debug and release builds. |
| Feature **on**, *eta-expanded* delegate (`Func<_,_>(fun a b -> f a b)`) | Direct in **release** (`--optimize+`) only; **closure in debug** (`--optimize-`). |

The eta/non-eta distinction is made by whether the `Invoke` parameters are compiler-generated. A *non-eta* delegate's parameters are synthesized by `BuildNewDelegateExpr` (compiler-generated), so the closure carries no user-meaningful names and the delegate is always made direct. An *eta-expanded* delegate's parameters are the user's own lambda variables; in unoptimized builds the closure is kept so those names survive for debugging (locals window, parameter tips). In optimized builds debuggability is not a concern and the forwarding call survives the optimizer, so the direct form is used.

### The inline-race

The F# optimizer inlines small function bodies *before* code generation runs. If a delegate target is small and not annotated `[<NoCompilerInlining>]`, in release builds its body is inlined into the `Invoke` body and the forwarding call no longer exists for the recognizer to fire on — so such a target becomes a **closure in release but is direct in debug**. This is the one place the policy table above does not hold uniformly, and is an intentional, documented consequence rather than a bug. Annotating the target `[<NoCompilerInlining>]` (or making it large enough not to be inlined) guarantees the direct form in both configurations. See *Unresolved questions*.

## Scenarios (by test case)

Every row corresponds one-to-one to a delegate-construction case in the `tests/.../EmittedIL/DirectDelegates` baseline suite; the number is the canonical case index, also carried in the test-source comments. Cases are numbered by shape so the table reads grouped: **1–17** eta-expanded curried forwarding, **18–32** non-eta forwarding, **33–38** eta-expanded tupled forwarding, **39–43** partial application, **44–47** non-forwarding / negative, **48–49** `unit`-argument, **50–51** value-type (struct) receiver, **52** extension member, **53** byref parameter.

**Debug** / **Release** give the *preview* emit (`--optimize-` / `--optimize+`); **with the feature off every case is a closure**. "direct" = the delegate points at the real method; "closure" = the intermediate `…@NN::Invoke` is generated. `‡` marks the **inline-race**: the target is small and unannotated, so the optimizer inlines it before codegen and the forwarding call vanishes — the `[<NoCompilerInlining>]` `niXxx` cases (6–9 eta, 23–26 non-eta) are the same shapes proven direct in release, and cases 10/27 (`trivial`, inlinable) are the contrast that bails.

| # | Description | Delegate construction | Debug | Release |
|---|---|---|---|---|
| 1 | eta module function | `Action<int,int>(fun a b -> handlerCurried a b)` | closure | closure‡ |
| 2 | eta static method | `Action<int,int>(fun a b -> C.AddC a b)` | closure | closure‡ |
| 3 | eta generic static method | `Action<int,int>(fun a b -> G<string>.SMc<int> a b)` | closure | closure‡ |
| 4 | eta instance method | `Action<int,int>(fun a b -> o.AddC a b)` | closure | closure‡ |
| 5 | eta generic instance method | `Action<int,int>(fun a b -> o.IMc<int> a b)` | closure | closure‡ |
| 6 | eta module function, non-inlinable | `Func<int,int,int>(fun a b -> accCurried a b)` | closure | direct |
| 7 | eta static method, non-inlinable | `Func<int,int,int>(fun a b -> S.AccS a b)` | closure | direct |
| 8 | eta instance method, non-inlinable | `Func<int,int,int>(fun a b -> o.AccC a b)` | closure | direct |
| 9 | eta generic instance method, non-inlinable | `Func<int,int,int>(fun a b -> o.GPick<int> a b)` | closure | direct |
| 10 | eta inlinable module function (inline-race) | `Func<int,int,int>(fun a b -> trivial a b)` | closure | closure‡ |
| 11 | eta unit-returning member | `Action<int,int>(fun a b -> returnsUnit a b)` | closure | direct |
| 12 | eta generic unit-returning method | `Func<unit,unit>(fun (x: unit) -> C.Echo<unit> x)` | closure | direct |
| 13 | eta IL/BCL static method | `Func<int,int,int>(fun a b -> Math.Max(a, b))` | closure | direct |
| 14 | eta IL/BCL instance method (ref type) | `Func<string,StringBuilder>(fun s -> sb.Append s)` | closure | direct |
| 15 | eta module function, custom delegate | `DTupled(fun a b -> acc a b)` | closure | direct |
| 16 | eta instance member, custom delegate | `DTupled(fun a b -> c.M a b)` | closure | direct |
| 17 | eta generic method, generic custom delegate | `DGen<int>(fun x -> ident x)` | closure | direct |
| 18 | non-eta module function | `Action<int,int>(handlerCurried)` | direct | closure‡ |
| 19 | non-eta static method | `Action<int,int>(C.AddC)` | direct | closure‡ |
| 20 | non-eta generic static method | `Action<int,int>(G<string>.SMc<int>)` | direct | closure‡ |
| 21 | non-eta instance method | `Action<int,int>(o.AddC)` | direct | closure‡ |
| 22 | non-eta virtual instance method (`dup; ldvirtftn`) | `Action<int,int>(o.V)` | direct | direct |
| 23 | non-eta module function, non-inlinable | `Func<int,int,int>(accCurried)` | direct | direct |
| 24 | non-eta static method, non-inlinable | `Func<int,int,int>(S.AccS)` | direct | direct |
| 25 | non-eta instance method, non-inlinable | `Func<int,int,int>(o.AccC)` | direct | direct |
| 26 | non-eta generic instance method, non-inlinable | `Func<int,int,int>(o.GPick<int>)` | direct | direct |
| 27 | non-eta inlinable module function (inline-race contrast) | `Func<int,int,int>(trivial)` | direct | closure‡ |
| 28 | non-eta unit-returning member (→ `void`) | `Action<int,int>(returnsUnit)` | direct | direct |
| 29 | non-eta generic return tyvar → `Unit` | `Func<unit,unit>(C.Echo<unit>)` | direct | direct |
| 30 | non-eta module function, custom delegate | `DTupled(acc)` | direct | direct |
| 31 | non-eta instance member, custom delegate | `DTupled(c.M)` | direct | direct |
| 32 | non-eta generic method, generic custom delegate | `DGen<int>(ident)` | direct | direct |
| 33 | eta module function, tupled application | `Action<int,int>(fun a b -> handlerTupled (a, b))` | closure | closure |
| 34 | eta static method, tupled application | `Action<int,int>(fun a b -> C.AddT(a, b))` | closure | closure |
| 35 | eta generic static method, tupled application | `Action<int,int>(fun a b -> G<string>.SMt<int>(a, b))` | closure | closure |
| 36 | eta instance method, tupled application | `Action<int,int>(fun a b -> o.AddT(a, b))` | closure | closure |
| 37 | eta generic instance method, tupled application | `Action<int,int>(fun a b -> o.IMt<int>(a, b))` | closure | closure |
| 38 | eta module function, tupled, non-inlinable | `Func<int,int,int>(fun a b -> accTupled (a, b))` | closure | closure |
| 39 | partial application of module function (constant) | `Action<int,int>(handler3 1)` | closure | closure |
| 40 | partial application of static method (constant) | `Action<int,int>(C.Add3 1)` | closure | closure |
| 41 | partial application of module function (captured var) | `Action<int,int>(handler3 n)` | closure | closure |
| 42 | partial application of static method (captured var) | `Action<int,int>(C.Add3 n)` | closure | closure |
| 43 | partial application of instance method (receiver + var) | `Action<int,int>(o.Add3 n)` | closure | closure |
| 44 | first-class function value (no target method) | `Action<int,int>(handler)` | closure | closure |
| 45 | non-forwarding body (computed argument) | `Action<int,int>(fun a b -> sink (a + b + k))` | closure | closure |
| 46 | reordered arguments | `Action<int,int>(fun a b -> handler b a)` | closure | closure |
| 47 | reference-parameter contravariance (upcast coercion) | `Func<string,int>(fun s -> h.TakesObj s)` | closure | closure |
| 48 | non-eta unit-argument delegate | `Action(handler)` (`handler: unit -> unit`) | closure | closure |
| 49 | eta unit-argument delegate | `Action(fun () -> handler ())` | closure | closure |
| 50 | non-eta struct (value-type) receiver | `Func<int,int,int>(s.Add)` (`s : struct`) | closure | closure |
| 51 | eta struct (value-type) receiver | `Func<int,int,int>(fun a b -> s.Add a b)` (`s : struct`) | closure | closure |
| 52 | extension member (receiver is a leading static arg) | `Func<int,int,int>(fun a b -> h.Combine(a, b))` | closure | closure |
| 53 | byref-parameter delegate (mutating body) | `DByref(fun x -> x <- x + 1)` | closure | closure |

For the virtual case (22) the receiver is evaluated, duplicated, and `ldvirtftn` binds the function pointer to the receiver's runtime override, so virtual dispatch through the delegate is preserved; every other (non-virtual) instance target uses `ldftn` to bind the exact method.

### Receiver evaluation — correctness guard

For an *explicit eta-lambda* like `Func<_,_>(fun a -> recv.M a)`, the closure form re-evaluates `recv` on **every** `Invoke`, whereas a direct delegate evaluates it **once**, at construction, as the `Target`. The rewrite is therefore only applied when that difference is unobservable: the receiver must be side-effect-free (`Optimizer.ExprHasEffect` is `false` — a non-mutable value, a constant, a pure field read) and must not reference the delegate's own `Invoke` parameters. A side-effecting receiver (`getObj().M`), a mutable value, or a fresh allocation keeps the closure. This is verified by an execution test: a counter-bumping receiver remains a closure and is re-evaluated per invocation.

Note that the related "lazy function" case is *not* affected: `new Action<int>(failwith "")` is eta-expanded by the type checker to `fun arg -> (failwith "") arg`, whose function position is an application of `failwith`, not a plain method value, so the recognizer does not match it and it stays a closure with its existing per-invocation semantics.

## Argument / parameter name impact

The closure form names the `Invoke` parameters: for an eta delegate they are the user's lambda variable names; for a non-eta delegate they are synthesized (`delegateArg0`, … or, with `ImprovedImpliedArgumentNames`, the target's declared argument names). With the direct form there is **no generated `Invoke`** — the delegate points at the target method, so `delegate.Method.GetParameters()` reports the **target method's own declared parameter names**. This is an outwardly-visible change for reflection over parameter names. Keeping eta delegates as closures in debug builds specifically preserves the user's chosen lambda parameter names for the debugging experience.

## Quotations and FCS

The recognizer runs only in `IlxGen` (IL emission). Quotations, `FSharp.Compiler.Service` typed-tree consumers, and `Expr<_>` conversions are unaffected — a delegate-construction quotation produces the same quotation node before and after this feature. This is covered by the existing quotation tests.

# Drawbacks
[drawbacks]: #drawbacks

* It introduces a code-generation difference that depends on language version *and* optimization level, plus the inline-race, so the precise output for a given source is less uniform than today (see *Unresolved questions*).
* `delegate.Method`/`Target`/equality semantics change in outwardly-visible ways (see *Compatibility*), which, although generally an improvement and closer to C#, is a behavior change that some code may depend on.
* The direct path adds a recognizer and emission branch to `IlxGen`/`TypedTreeOps`, increasing compiler surface area.

# Alternatives
[alternatives]: #alternatives

* **Do nothing.** F# continues to differ from C#/VB; the interop and identity problems above persist.
* **Always emit direct (no eta/debug distinction).** Rejected because eta-expanded delegates in debug builds would lose the user's lambda parameter names and a friendly stepping experience.
* **Run the recognizer inside the optimizer (before inlining).** Would remove the inline-race and make debug/release consistent, but is a more invasive change; deferred (see *Unresolved questions*).

# Compatibility
[compatibility]: #compatibility

* **Is this a breaking change?** Not at the source or type level, and it is opt-in via `--langversion:preview`. It *is* an observable **runtime behavior change** for code that inspects delegates produced by F# compiled with the feature:
  * `Delegate.Method` now returns the actual target `MethodInfo` (e.g. `Max`, `add`, `AccC`) instead of `Invoke` on a generated closure type. Reflection-based dispatch, logging by `Method.Name`, mocking, and minimal-API/routing frameworks will see the real method (usually the desired result).
  * `Delegate.Target` is the real receiver for an instance target and `null` for a static target, instead of the closure instance.
  * `Delegate.Equals`/`GetHashCode` (and thus `Delegate.Combine`/`Delegate.Remove`, event add/remove, and delegate de-duplication) now compare by the real `(Method, Target)`. Two delegates built from the same method+receiver may now be equal where previously they were not. Removing an event handler created from the same method now succeeds where it might previously have failed.
  * `delegate.Method.GetParameters()` reports the target's parameter names rather than synthesized/lambda names.
  * **Evaluation timing is *not* changed**: throwing/side-effecting function positions and side-effecting/mutable receivers are deliberately kept as closures, so once-vs-per-invocation semantics are preserved.
* **Previous compilers encountering this as source code:** there is no new syntax; the feature only changes code generation under the preview flag, so older compilers compile the same source to the existing closure form.
* **Previous compilers encountering compiled binaries:** delegates in such binaries have a different `Method`/`Target`, but no surface/type-level change — consumers compile and run unchanged.
* **FSharp.Core:** no change.

# Pragmatics

## Diagnostics

No new diagnostics. The feature introduces no new syntax and no new error or warning conditions; it is a silent, provably-equivalent code-generation change. When the shape or a guard does not permit the direct form, the compiler silently uses the existing closure form.

## Tooling

* **Debugging.** Eta-expanded delegates remain closures in debug builds, preserving user lambda parameter names in the locals window and parameter tips, and the existing stepping experience. Stepping into a *direct* delegate steps into the actual target method (as with C#). Non-eta delegates carry no user names to preserve.
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

1. **Struct (value-type) receivers.** Instance methods on value types currently bail to a closure. A direct delegate would have to **box** the receiver (the delegate `Target` is `object`) and bind the function pointer accordingly, with the boxed-copy semantics that implies — which matches the current closure's capture semantics but should be confirmed against readonly/`inref`/defensive-copy expectations before enabling. Supporting this is possible but is a separate, more substantial change.

2. **The inline-race (debug/release inconsistency).** Small, inlinable, unannotated targets are inlined by the optimizer before code generation, so they become closures in release while remaining direct in debug — `delegate.Method` then differs between configurations. Fully resolving this would require running the recognizer **inside the optimizer**, before the head call is inlined, or otherwise suppressing inlining of delegate targets. Is the current behavior acceptable, or should it be addressed?

3. **`unit`-argument delegates.** `Action(handler)` for `handler: unit -> unit` stays a closure because the synthesized `unit` argument surfaces as a spurious leading argument. This could be made direct by stripping a forwarded `unit` literal that corresponds to an elided unit parameter. Worth doing?

4. **Extension members.** An extension member compiles to a static method whose first parameter is the receiver, so a use such as `Func<_,_,_>(fun a b -> h.Combine(a, b))` presents the receiver `h` as a leading argument and is currently treated as a partial application → closure. This is, however, expressible as a direct delegate: the CLR's "closed over the first argument" mechanism lets `newobj Delegate::.ctor(object, native int)` bind `Target = h` and `ftn = ldftn <static extension method>`, so invoking passes `h` followed by the delegate's arguments. Implementing it requires (a) recognizing the extension-member case (`vrefM.IsExtensionMember`) and allowing a single leading argument to become the `Target` of a *static* method, (b) offsetting the signature check by one to drop the bound first formal parameter, and (c) the same reference-type guard as instance receivers (a value-type extension receiver hits the boxing gap in question 1).
