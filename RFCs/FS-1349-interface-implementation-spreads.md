# F# RFC FS-1349 - Interface implementation spreads

The design suggestion [Spread operator for F#](https://github.com/fsharp/fslang-suggestions/issues/1253) has been marked "approved in principle". This RFC covers its second subset, after [FS-1151 Record spreads](https://github.com/fsharp/fslang-design/blob/main/RFCs/FS-1151-record-spreads.md) (F# 11).

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1253)
- [x] Approved in principle
- [ ] Implementation (not started)
- [x] [Discussion](https://github.com/fsharp/fslang-design/discussions/847)

# Summary

`...src` is a member definition in an object expression and in an `interface I with` block of a class or struct. It implements each open abstract slot by forwarding it to the member of `src` that has the same name and a compatible signature.

```fsharp
type IA = abstract A : int -> int
type IB =
    abstract A : int -> int
    abstract B : int -> int

let b (a : IA) =
    { new IB with
        ...a                    // IB.A forwards to a.A
        member _.B x = x - 1 }
```

# Motivation

Suggestions [#132](https://github.com/fsharp/fslang-suggestions/issues/132), [#524](https://github.com/fsharp/fslang-suggestions/issues/524), [#555](https://github.com/fsharp/fslang-suggestions/issues/555) and [#1245](https://github.com/fsharp/fslang-suggestions/issues/1245) ask for delegation of an interface to an object; #1253 subsumes them. Today each member is forwarded by hand, which for `IList<'T>` (13 members) is boilerplate that must change with the interface. Uses are decorators, adapters to interfaces that the source does not implement nominally, test doubles, and `...this` for explicit interface implementations (the need behind [#195](https://github.com/fsharp/fslang-suggestions/issues/195)).

# Detailed design

## Syntax

Grammar: `member-defn += '...' expr`.

A spread is allowed in the member list of an object expression, for the primary type and for each `interface ... with` clause, and in an `interface ... with` block of a class or struct with a primary constructor. It follows the layout rules of `member` and can be interleaved with explicit members. A source that is not a simple path should be parenthesized: `...(createInner ())`. Every other member list reports FS3902: class members outside `interface` blocks, type extensions and interface definitions. Other uses of `...` stay as FS-1151 specifies.

## Open slots

Dispatch slot inference ([§14.7](https://fsharp.github.io/fslang-spec/#147-dispatch-slot-inference)) assigns each abstract slot of the implemented types to one member list. A slot of a member list is *open* unless:

- an explicit member of the same list implements it;
- a base class implementation is inherited ([§8.14.3](https://fsharp.github.io/fslang-spec/#8143-interface-implementations));
- it has a `default` implementation in the implemented class or a base class;
- a default interface member covers it ([FS-1074](https://github.com/fsharp/fslang-design/blob/main/FSharp-5.0/FS-1074-default-interface-member-consumption.md), [FS-1336](https://github.com/fsharp/fslang-design/blob/main/RFCs/FS-1336-Implement-Equally-Named-Abstract-Slots.md));
- it is static abstract.

Property getters and setters, and event adders and removers, are separate slots.

## Resolution

A spread `...src`, with `src : τ`, *provides* an open slot when the forwarding member type-checks under the rules for member bodies ([§8.13](https://fsharp.github.io/fslang-spec/#813-members), [§14.4](https://fsharp.github.io/fslang-spec/#144-method-application-resolution)):

```fsharp
member _.M<'T..>(x1 : p1, ..., xN : pN) : r = src.M(x1, ..., xN)   // method
member _.M (x1 : p1) (y1 : q1) : r = src.M x1 y1                   // method, several argument groups
member _.P with get () : r = src.P                                 // property getter
member _.P with set (v : r) = src.P <- v                           // property setter
member _.Item with get (x : i) : r = src.Item(x)                   // indexer, setter likewise
[<CLIEvent>] member _.E = src.E                                    // CLI event
```

`p1 ... pN`, `q1`, `i` and `r` are the slot types, instantiated with the type arguments of the implemented type. `'T..` are fresh type parameters with the slot's constraints. Two rules make resolution narrower than for a hand-written member:

1. Only intrinsic instance members of `τ` participate: declared and inherited members (record fields included), members of inherited interfaces, and members from the subtype constraints of a type parameter (`'T :> I`). Extension members are ignored, as in FS-1151, so a spread does not depend on open modules, and LINQ `Contains` cannot fill a slot.
2. The only conversion of the result is coercion to a supertype, including boxing; `op_Implicit` and numeric widening do not apply. Arguments convert as in a hand-written forwarder, type-directed conversions included.

Overloads, generic methods (the slot's constraints must satisfy the method's), byrefs, optional and `ParamArray` parameters and accessibility work as in the hand-written member. An ambiguous overload, or a member with the right name but an incompatible signature, does not provide the slot. When `inner : IList<'T>` is spread into `IList<'T>`, `inner.GetEnumerator ()` provides both `IEnumerable<'T>.GetEnumerator` and, coerced to `IEnumerator`, `IEnumerable.GetEnumerator`; for a `ResizeArray<'T>` source the coercion boxes the struct enumerator.

The static type of `src` must be known at the spread; otherwise an error asks for an annotation. With nullness checking on, a nullable `src` is an error, as in FS-1151. The interface implementations of an F# class are explicit and not visible through the class type; `...(src :> I)` spreads them.

```fsharp
type IName = abstract Name : string
type INamed =
    abstract Name : string
    abstract Id : int

type Item () as this =
    member _.Name = "intrinsic"
    interface INamed with
        member _.Name = "explicit"
        member _.Id = 1
    interface IName with ...this                // Name = "intrinsic"
    // interface IName with ...(this :> INamed) // Name = "explicit"
```

## Composition

An explicit member closes its slot, so it wins over every spread, whatever its position. This differs from FS-1151, where a spread to the right of an explicit field shadows it with a warning: the order of F# member definitions has no meaning. Each open slot is implemented by the rightmost spread that provides it.

```fsharp
type ILog = abstract Log : string -> unit
type IService =
    abstract Log : string -> unit
    abstract Run : unit -> unit
    abstract Stop : unit -> unit

let service (log : ILog) (impl : IService) =
    { new IService with
        member _.Stop () = ()   // wins over impl.Stop
        ...impl                 // implements Run; its Log is shadowed
        ...log }                // implements Log
```

## Evaluation and elaboration

Each source is evaluated exactly once, also when it implements no slot. A struct source is captured by value, so a mutating call works on a defensive copy, as in a hand-written forwarder.

- **Object expressions**: before construction, after the base constructor arguments, in textual order across all clauses. The value is bound to a generated immutable local that the members capture.
- **Classes**: once per instance, as a `let` binding after the explicit `let` and `do` bindings, in textual order. The source follows the initialization rules of `let` bindings; `...this` requires `as this` and gets its runtime initialization checks.
- **Structs**: a struct has no `let` bindings (FS0901) and no `as this` (FS0658), so a source that needs storage is an error. A constructor parameter, upcast or not, or an immutable module-level value is allowed.

A `let mutable` or `val mutable` source is an error: a spread captures the value, and capturing a value that is later reassigned is the best-known pitfall of Kotlin `by`. Explicit members can still forward to a mutable value.

The forwarders are ordinary object expression methods or explicit interface implementations, compiled as hand-written ones and marked with `CompilerGeneratedAttribute` and `DebuggerNonUserCodeAttribute`.

## Interactions

- **Signature files**: unaffected, because `interface IB` in a signature lists no members.
- **Quotations**: object expressions cannot be quoted (FS0449). Under `[<ReflectedDefinition>]`, the forwarders of a class get reflected definitions, as hand-written members do.
- **Type providers**: members of provided types participate as intrinsic members.
- **C#** sees ordinary interface implementations.
- **Tooling**: `SynMemberDefn.Spread` holds the `SynExprSpread` of FS-1151. Go-to-implementation on a slot, and find-all-references on a slot or a source member, include the spreads that use them. Hover on `...src` lists the implemented slots. Forwarders have no sequence points and the generated local or field is hidden, so stepping goes into the source member; a breakpoint on a spread binds to the source evaluation. An unprovided slot does not stop checking.

# Changes to the F# spec

- §6.3.8 and §6.9.13 Object Expressions, §8.14.3 Interface Implementations: the grammar, and [Evaluation and elaboration](#evaluation-and-elaboration).
- §14.7 Dispatch Slot Inference: [Open slots](#open-slots), [Resolution](#resolution) and [Composition](#composition), after inference for explicit members and before member bodies are checked.
- §14.8 Dispatch Slot Checking: the dispatch map includes the generated mappings; the one-to-one requirement is unchanged.

# Drawbacks

- `...` copies fields of records but forwards calls of interfaces.
- Matching by name and signature is implicit: a source member that matches by accident is used.
- A changed source member changes the target silently. A removed one gives an error at the spread, or silently moves the slot to an earlier spread that provides it.

# Alternatives

- **Header form** `interface IB by a with ...` (#132) and the forms of the other suggestions: one source per interface and no relation to `...`. `{ list with member ... }` (#524) is also ambiguous with record copy-and-update.
- **Require `src :> I`**: excludes adapters; `...(src :> I)` already expresses this subset.
- **Replace virtual members too**, or `override ...src`: a spread could silently replace `Equals` and `GetHashCode`. An opt-in form can be added later.
- **Bare `...` for `...this`**: a second way to refer to the object; `as this` stays the only one. The optimizer may later remove its runtime checks when `this` is used only in spreads.
- **Spreads outside `interface` blocks** for abstract base classes: object expressions cover this case.
- **Evaluate `src` on each call**: allows live delegation to a mutable value, but contradicts FS-1151 and repeats side effects of `...(createInner ())`.
- **Reflection proxies or source generators**: no static checks, or code that drifts from the interface.

# Prior art

- **Kotlin** [`by`](https://kotlinlang.org/docs/delegation.html): evaluated once and stored; explicit overrides win; the delegate must implement the interface.
- **Scala 3** [`export`](https://docs.scala-lang.org/scala3/reference/other-new-features/export.html): forwarders implement deferred members and never override concrete ones, as here.
- **Go** [embedding](https://go.dev/doc/effective_go#embedding): outer methods shadow promoted ones.
- **Delphi** [`implements`](https://docwiki.embarcadero.com/RADStudio/Athens/en/Using_Implements_for_Delegation): the delegate wins over the class's own methods, a known source of confusion.
- **C#** ([#234](https://github.com/dotnet/csharplang/discussions/234), [#5514](https://github.com/dotnet/csharplang/discussions/5514)) and **Rust** ([RFC #2393](https://github.com/rust-lang/rfcs/pull/2393)): requests only.

# Compatibility

Not a breaking change. The feature `InterfaceImplementationSpreads` is in preview until it is stable. Under an earlier language version, a spread reports that the feature needs a later one; earlier compilers report FS0010 (or FS0058 for some layouts). Compiled code contains only ordinary classes and interface implementations. FSharp.Core does not change.

# Interop

See [Interactions](#interactions).

# Pragmatics

## Diagnostics

| Number | Kind | Condition |
|---|---|---|
| FS3902, FS3899 | error | spread in an unsupported member list; no source after `...` |
| new | error | type of the source not known at the spread |
| new | error | nullable source |
| new | error | no primary constructor; struct source that needs storage |
| new | error | `let mutable` or `val mutable` source |
| new | error | open slot not provided; names the spread sources (replaces FS0365/FS0366 when the list has spreads) |
| new | warning | spread implements no slot, e.g. `{ new obj () with ...x }`, or every slot it provides is shadowed |
| new | info, off by default | slot provided by more than one spread, at the shadowed spread |
| new | info, off by default | source member with the slot's name but an incompatible signature, for a slot that is not provided |

The existing dispatch slot diagnostics (FS0017, FS0357–FS0361, FS0365–FS0367, FS0370, FS3213) still apply to explicit members.

## Tooling

See [Interactions](#interactions).

## Performance

Code without spreads does not change. Each spread costs one member lookup and one method application resolution per open slot, as hand-written members do. At run time a forwarder costs one call; a class spread may add one field, but needs none for a constructor parameter, an immutable `let` value or `this`.

## Scaling

Up to about 50 slots per interface (COM interfaces 100 or more) and 3 spreads per member list.

## Culture-aware formatting/parsing

Not applicable.

# Unresolved questions

1. **Extension members**: ignored here; in [#847](https://github.com/fsharp/fslang-design/discussions/847), T-Gro and brianrourkeboll prefer to include them, because the target slot directs resolution.
2. **Mutable sources**: an error here; T-Gro and brianrourkeboll prefer to allow them, with a warning or info about the copy.
3. **Records and other types without a primary constructor**: not supported here; T-Gro asks for records. Sources that need no storage, such as module values, could work without a primary constructor.
4. **SRTP**: whether a source of an inline type parameter with member constraints provides slots through those constraints.
5. **Shadowing diagnostic**: an off-by-default info at the shadowed spread here. FS-1151 reports FS3905 and FS3907 at the shadowing spread, and in #1245 dsyme agreed that a warning on conflicts is essential.
6. **Style**: whether a spread after an explicit member gets a warning.
