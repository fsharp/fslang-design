# F# RFC FS-1349 - Interface implementation spreads

The design suggestion [Spread operator for F#](https://github.com/fsharp/fslang-suggestions/issues/1253) has been marked "approved in principle."

This RFC covers the detailed proposal for **interface implementation spreads** in object expressions and type definitions. It is the second independently useful subset of the suggestion, following [RFC FS-1151 - Record spreads](https://github.com/fsharp/fslang-design/blob/main/RFCs/FS-1151-record-spreads.md), which shipped in F# 11.

- [x] [Spread operator for F#](https://github.com/fsharp/fslang-suggestions/issues/1253)
- [x] Approved in principle
- [ ] Implementation (not started)
- [x] [Discussion](https://github.com/fsharp/fslang-design/discussions/847)

# Summary

An interface implementation spread `...source` inside the member list of an object expression, or inside an `interface ... with` block of a type definition, implements every abstract member that is not otherwise implemented by forwarding it to the member of `source` that has the same name and a compatible signature.

```fsharp
type IA =
    abstract A : int -> int

type IB =
    abstract A : int -> int
    abstract B : int -> int

let b (a : IA) =
    { new IB with
        ...a                      // IB.A is implemented as a.A
        member _.B x = x - 1 }
```

Explicit members always take precedence over spreads. Several spreads compose left to right, and when more than one spread can implement the same abstract member, the rightmost wins. A spread source expression is evaluated exactly once per constructed object.

The result is an ordinary object expression or an ordinary class: the compiler synthesizes the forwarding members that the user would otherwise have written by hand. No new runtime construct is introduced.

This RFC covers:

- Spreads in object expressions, for the abstract members of the type being implemented and of each additional interface:

    ```fsharp
    { new IB with ...a }

    { new IB with
        ...a
        member _.B x = x - 1 }

    { new AbstractBase (args) with
        ...impl
      interface IDisposable with
        ...disposable }
    ```

- Spreads in interface implementation blocks of class and struct definitions:

    ```fsharp
    type B (a : IA) =
        interface IB with
            ...a
            member _.B x = x - 1
    ```

- Spreading the object under construction itself, via the self identifier of the primary constructor:

    ```fsharp
    type B () as this =
        member _.A x = x + 1
        interface IA with ...this
    ```

Spreads to or from records, spreads in collection or computation expressions, spreads in patterns, and spreads in the member lists of class definitions outside `interface` blocks are left for later RFCs.

# Motivation

Delegating an interface implementation to an existing object is one of the oldest and most repeated requests against the F# object model:

- [#132 Allow interfaces to be implemented by expressions](https://github.com/fsharp/fslang-suggestions/issues/132) (2016, `interface I by delegator with`)
- [#524 Copy-and-update syntax for interfaces (decorator pattern)](https://github.com/fsharp/fslang-suggestions/issues/524) (2016)
- [#555 Implicit interface implementation from an object expression](https://github.com/fsharp/fslang-suggestions/issues/555) (2017)
- [#1245 Object expressions implemented by a value](https://github.com/fsharp/fslang-suggestions/issues/1245) (2023, with a [proof-of-concept branch](https://github.com/Nhowka/visualfsharp/tree/interface-from-value))

All four were closed in favor of #1253, whose original text lists object expressions and interface implementations as target applications of the spread operator, and whose [design discussion](https://github.com/fsharp/fslang-design/discussions/806) ranks "interface implementation spreads in type definitions and object expressions" second among the subsets the FS-1151 author favors, directly after record type spreads.

The existing way to approach this problem in F# is explicit re-delegation, one member at a time. For a small interface this is tolerable; for `IList<'T>` (13 members), `IDictionary<'TKey, 'TValue>`, `DbConnection`, or any real-world service interface it is a wall of boilerplate that has to be maintained whenever the interface changes.

Envisioned use cases:

- **Decorators and proxies.** Wrap an existing implementation and override a few members: bounding, logging, caching, retrying, synchronizing, instrumenting.

    ```fsharp
    let bounded max (list : IList<'T>) =
        { new IList<'T> with
            ...list
            member _.Add item =
                if list.Count >= max then invalidOp "The list is full."
                list.Add item }
    ```

- **Adapters.** Expose an object that already has the right members as an interface it does not nominally implement, with full static checking rather than reflection-based proxies.

    ```fsharp
    type IContainer<'T> =
        abstract Contains : 'T -> bool

    let xs = ResizeArray [ 1 .. 5 ]
    let container = { new IContainer<int> with ...xs } // uses ResizeArray.Contains
    ```

- **Test doubles.** Take a real implementation, or a previously built fake, and override just the members under test.

- **Explicit interface implementations without repetition.** F# interfaces are always implemented explicitly. When a class already exposes the members as ordinary members, `...this` avoids restating each of them:

    ```fsharp
    type IPoint =
        abstract X : float
        abstract Y : float

    type Point (x : float, y : float) as this =
        member _.X = x
        member _.Y = y
        interface IPoint with ...this
    ```

    This addresses the ergonomic pain behind [#195 Support implicit interface implementation](https://github.com/fsharp/fslang-suggestions/issues/195) without changing the explicit implementation model.

- **Composition over inheritance.** Build an object from several collaborating parts, each spread into the interface it serves.

# Detailed design

## Syntax

We add the spread form `...sourceExpression` as a member definition, allowed in two places:

1. The member list of an object expression, both the members of the primary type and the members of each additional `interface ... with` clause.
2. The member list of an `interface ... with` block in a class or struct type definition.

### Object expressions

```fsharp
{ new IB with ...a }
```

```fsharp
{ new IB with
    ...a
    member _.B x = x - 1 }
```

```fsharp
{ new AbstractBase (arg) with
    ...impl
    override _.Describe () = "custom"
  interface IDisposable with
    ...disposable }
```

### Interface implementations in type definitions

```fsharp
type B (a : IA) =
    interface IB with ...a
```

```fsharp
type B (a : IA, c : IC) =
    interface IB with
        ...a
        ...c
        member _.B x = x - 1
```

```fsharp
type B () as this =
    member _.A x = x + 1
    interface IA with ...this
    interface IB with
        ...this
        member _.B x = x - 1
```

A spread is a member definition and follows the same layout rules as a `member` definition: it may follow `with` on the same line, or start a new line at the member indentation. Spreads and explicit members may be interleaved in any order. A source expression that is not a simple path should be parenthesized: `...(this :> IB)`, `...(createInner ())`.

In the untyped syntax tree, `SynMemberDefn` gains a `Spread` case carrying the existing `SynExprSpread` node introduced by FS-1151. The parser accepts a spread wherever it parses a member definition list so that tooling sees the user's text; the type checker reports an error for every position other than the two listed above (class member lists outside `interface` blocks, type extensions, interface and abstract class definitions, signature files, and so on).

The `...source` syntax remains disallowed in all other expression, type, and pattern contexts, as specified by FS-1151.

## Terminology

For an object expression `{ new ty0 args with members0 interface ty1 with members1 ... }` or an interface implementation `interface ty with members` in a type definition, dispatch slot inference ([§14.7](https://fsharp.github.io/fslang-spec/#147-dispatch-slot-inference)) assigns every abstract member of the implemented types to the most specific implemented type. The *required slots* of a member list are the slots assigned to its type.

A required slot is *already implemented* if any of the following holds:

- an explicit member in the same member list implements it;
- an implementation is inherited from a base class (interface implementations may be inherited, [§8.14.3](https://fsharp.github.io/fslang-spec/#8143-interface-implementations));
- the slot is a virtual member with a `default` implementation in the class being implemented or in one of its base classes;
- the slot is covered by a default interface member, as defined by [RFC FS-1336](https://github.com/fsharp/fslang-design/blob/main/RFCs/FS-1336-Implement-Equally-Named-Abstract-Slots.md).

The *open slots* of a member list are its required slots that are not already implemented and are not static abstract members.

Property slots count as separate getter and setter slots, and CLI event slots as separate adder and remover slots, exactly as they do for dispatch slot checking today.

## Spread resolution

A spread `...src` with source expression `src` of static type `τ` *provides* an open slot `S` when the forwarding member below is well-typed under the ordinary rules for member bodies ([§8.13](https://fsharp.github.io/fslang-spec/#813-members), [§14.4 Method Application Resolution](https://fsharp.github.io/fslang-spec/#144-method-application-resolution)):

| Slot kind | Slot | Forwarding member |
|---|---|---|
| Method | `abstract M<'T1, ..., 'Tk> : p1 * ... * pN -> r` | `member _.M<'T1, ..., 'Tk>(x1 : p1, ..., xN : pN) : r = src.M(x1, ..., xN)` |
| Property getter | `abstract P : r` / `abstract P : r with get` | `member _.P with get () : r = src.P` |
| Property setter | `abstract P : r with set` | `member _.P with set (v : r) = src.P <- v` |
| Indexed property getter | `abstract Item : i -> r with get` | `member _.Item with get (x : i) : r = src.Item(x)` |
| Indexed property setter | `abstract Item : i -> r with set` | `member _.Item with set (x : i) (v : r) = src.Item(x) <- v` |
| CLI event | `[<CLIEvent>] abstract E : IEvent<'D, 'A>` | `[<CLIEvent>] member _.E = src.E` |

where `p1 ... pN` and `r` are the slot's parameter and return types after instantiating the type arguments of the implemented type, and `'T1 ... 'Tk` are fresh method type parameters carrying the slot's constraints.

Two restrictions narrow "well-typed" relative to a hand-written member:

1. **Only intrinsic instance members** of `τ` participate in resolving `src.M`: members declared by `τ`, inherited from its base types or inherited interfaces, and members available through the constraints of a type parameter. Extension members, whether F#-style optional extension members or C#-style extension methods, are ignored. This mirrors FS-1151, where only record fields participate in record spreads, and keeps the meaning of a spread independent of which modules happen to be open.
2. **The only type-directed conversion** applied to the result of `src.M(...)` is coercion to a supertype (an implicit upcast, including boxing). The additional F# 6 conversions (`op_Implicit`, numeric widening) do not apply. Arguments are passed through method application resolution as usual, which already permits coercing an argument to a parameter type.

Consequences of defining resolution this way:

- **Overloads** on the source are resolved by method application resolution against the slot's parameter types; an ambiguous resolution means the slot is not provided.
- **Generic methods** work when the source's type parameters can be inferred from the slot's parameters and the source's constraints are satisfied by the slot's constraints.
- **Byref parameters** (`byref`, `inref`, `outref`) are passed through by reference.
- **Optional and `ParamArray` parameters** on the source behave as in any other call with the slot's arity.
- **Accessibility** is checked at the spread site, exactly as for a hand-written call.
- **Name-only matches are not matches.** A source member with the right name but an incompatible signature does not provide the slot.

If the static type of the source expression is not yet known when the spread is checked (for example, an unannotated function parameter), an error asks for a type annotation. Object expression and member checking proceeds in source order and cannot wait for later inference to fix the type, which is the same restriction that applies to `src.M` today.

The source expression must not have a nullable type when nullness checking is enabled, matching FS-1151.

### Which members are seen

Member lookup uses the *static* type of the source expression, so:

- If `src : IA`, the members of `IA` and its inherited interfaces are seen.
- If `src : C` for a class `C`, the class's intrinsic members and inherited members are seen. Interface implementations of an F# class are explicit and therefore *not* seen through `C`; to delegate to them, spread the interface view: `...(src :> IA)`. C# classes usually implement interfaces implicitly, so their public members are seen directly.
- If `src : 'T when 'T :> IA`, the members of `IA` are seen through the constraint.
- `...this` in a type definition sees the intrinsic members of the type being defined, and, per the previous point, not its explicit interface implementations unless upcast: `...(this :> IB)`.

```fsharp
type IA = abstract A : int
type IB =
    abstract A : int
    abstract B : int

type B () as this =
    member _.A = 1
    interface IB with
        member _.A = 2
        member _.B = 3
    interface IA with ...this          // IA.A = B.A = 1

type B' () as this =
    member _.A = 1
    interface IB with
        member _.A = 2
        member _.B = 3
    interface IA with ...(this :> IB)  // IA.A = IB.A = 2
```

### Composition

For a member list containing explicit members and spreads `...s1; ...; ...sn` in textual order:

1. Explicit members implement their slots. They are never shadowed by a spread, regardless of position.
2. For each open slot, the spreads that provide it are considered from left to right, and the rightmost one implements the slot. If more than one spread provides the same slot, an informational diagnostic (off by default) is reported at the shadowed spread, mirroring the spread-shadows-spread diagnostics of FS-1151.
3. A spread that implements no slot at all produces a warning: it is dead code.
4. Every open slot that no spread provides remains unimplemented, and the usual dispatch slot checking reports it.

Rule 1 is the one place this RFC deliberately differs from FS-1151, where a spread to the right of an explicit field shadows it with a warning. Member definitions in F# are unordered: the position of a `member` in a type or object expression carries no meaning, and interleaving spreads with members must not change which implementation wins. "Explicit always wins" is also what Kotlin, Scala, and Go do; Delphi's opposite rule is a documented source of confusion (see [Prior art](#prior-art)).

Spreads never replace an implementation that already exists: not an explicit member, not an inherited implementation, not a `default` implementation, and not a default interface member. Replacing existing behavior remains the job of an explicit `member` or `override`. In particular, `{ new obj() with ...x }` implements nothing, because `System.Object` has no abstract members, and triggers the dead-spread warning.

### Elaboration

The forwarding members are synthesized into the typed tree as ordinary object expression methods (`TObjExprMethod`) or ordinary explicit interface implementation members of the class, with the slot recorded in their implemented slot signatures. The compiled form is that of hand-written forwarders: an object expression yields the same closure class, and a class yields the same explicit interface method implementations. Synthesized members are marked compiler-generated so that the debugger steps through them (see [Tooling](#tooling)).

```fsharp
let b (a : IA) =
    { new IB with
        ...a
        member _.B x = x - 1 }
```

elaborates to

```fsharp
let b (a : IA) =
    let spreadSrc = a
    { new IB with
        member _.A x = spreadSrc.A x   // synthesized
        member _.B x = x - 1 }
```

## Evaluation and runtime behavior

### Object expressions

- Spread source expressions are evaluated exactly once, when the object expression is evaluated, before the object is constructed.
- Evaluation order is textual: base constructor arguments (if any) first, then the spread sources of the primary member list, then those of each additional interface clause, left to right.
- Each result is bound to a compiler-generated immutable local that the synthesized members capture, in the same way that hand-written members capture locals. A struct-typed source is captured by value.
- A source is evaluated even if the spread ends up implementing no slot, consistent with FS-1151.

### Type definitions

- Each spread source expression is evaluated exactly once per instance, during construction, as if by a `let` binding placed after the type's explicit `let` and `do` bindings, in the textual order of the spreads. Its value is stored in a compiler-generated field. The implementation may elide the field when the source is a constructor parameter, an immutable `let` field, or `this`, since these are already available to members.
- Because the spread is a construction-time binding, the enclosing type must have a primary constructor. Spreads in the interface implementations of types without a primary constructor, including records, unions, and exception types, are not supported by this RFC.
- The source expression may refer to constructor parameters, `let` bindings of the type, and the self identifier introduced by `as this`, with the same initialization-soundness rules that apply to `let` bindings today. Using `...this` requires an `as this` self identifier and carries the same runtime initialization checks as any other use of that identifier.
- A source that is a `let mutable` or `val mutable` binding is an error. A spread captures a value, and silently capturing the initial value of a binding that is later reassigned is the best-known pitfall of Kotlin's `by` delegation. Live delegation to a mutable field is still available by writing explicit forwarding members.

```fsharp
type Bounded<'T> (max : int, inner : IList<'T>) =
    interface IList<'T> with
        ...inner
        member _.Add item =
            if inner.Count >= max then invalidOp "The list is full."
            inner.Add item
```

is equivalent to

```fsharp
type Bounded<'T> (max : int, inner : IList<'T>) =
    interface IList<'T> with
        member _.Add item =
            if inner.Count >= max then invalidOp "The list is full."
            inner.Add item
        member _.Clear () = inner.Clear ()
        member _.Contains item = inner.Contains item
        member _.CopyTo (array, index) = inner.CopyTo (array, index)
        member _.Count = inner.Count
        member _.GetEnumerator () : IEnumerator<'T> = inner.GetEnumerator ()
        member _.GetEnumerator () : IEnumerator = inner.GetEnumerator () :> IEnumerator
        member _.IndexOf item = inner.IndexOf item
        member _.Insert (index, item) = inner.Insert (index, item)
        member _.IsReadOnly = inner.IsReadOnly
        member _.Item with get index = inner.[index] and set index value = inner.[index] <- value
        member _.Remove item = inner.Remove item
        member _.RemoveAt index = inner.RemoveAt index
```

Note the two `GetEnumerator` slots: `IEnumerable<'T>.GetEnumerator` and the non-generic `IEnumerable.GetEnumerator` are both assigned to `IList<'T>` by dispatch slot inference and both have zero arguments, so they are distinguished by return type only. Resolving `inner.GetEnumerator ()` finds `IEnumerable<'T>.GetEnumerator` in both cases; its result has the first slot's return type directly and is coerced to `IEnumerator` for the second slot by the coercion rule. Both slots are provided. The same holds when the source is a class with a struct enumerator, such as `ResizeArray<'T>`, where the coercion boxes the enumerator.

## Examples

### Adapting an unrelated object

```fsharp
type IContainer<'T> =
    abstract Contains : 'T -> bool
    abstract Count : int

let xs = ResizeArray [ 1 .. 5 ]
let c = { new IContainer<int> with ...xs } // ResizeArray.Contains and ResizeArray.Count
```

### Multiple sources and an explicit override

```fsharp
type IReader = abstract Read : unit -> string
type IWriter = abstract Write : string -> unit
type IReadWrite =
    inherit IReader
    inherit IWriter
    abstract Flush : unit -> unit

let readWrite (r : IReader) (w : IWriter) =
    { new IReadWrite with
        ...r
        ...w
        member _.Flush () = () }
```

### Right-biased composition between spreads

```fsharp
type ILogger = abstract Log : string -> unit
type IService =
    abstract Log : string -> unit
    abstract Run : unit -> unit

let service (defaultLogger : ILogger) (impl : IService) =
    { new IService with
        ...defaultLogger   // provides Log
        ...impl }          // provides Log and Run; Log from impl wins (informational diagnostic, off by default)
```

### Properties, indexers, and events

```fsharp
type IObservableCounter =
    abstract Count : int with get, set
    abstract Item : int -> string with get
    [<CLIEvent>]
    abstract Changed : IEvent<EventHandler, EventArgs>

type Counter () =
    let changed = Event<EventHandler, EventArgs> ()
    let mutable count = 0
    let names = [| "zero"; "one"; "two" |]
    member _.Count with get () = count and set v = count <- v
    member _.Item with get i = names.[i]
    [<CLIEvent>]
    member _.Changed = changed.Publish

let observable (counter : Counter) =
    { new IObservableCounter with ...counter }
```

### Generic methods and constrained sources

```fsharp
type IMapper =
    abstract Map<'T> : 'T -> 'T

type Wrapper<'M when 'M :> IMapper> (inner : 'M) =
    interface IMapper with ...inner   // resolved through the constraint on 'M
```

### What is not provided

```fsharp
type IA = abstract A : int -> int
type IB =
    abstract A : int -> int
    abstract B : int -> int

type Src () =
    member _.A (x : string) = x.Length  // wrong parameter type
    member _.B (x : int) = x            // matches IB.B

[<AutoOpen>]
module SrcExtensions =
    type Src with
        member _.A (x : int) = x        // extension member: ignored

let b = { new IB with ...Src () }
// error: No implementation was given for 'abstract IB.A: int -> int'.
//        The spread source of type 'Src' does not provide a member with this name and signature.
```

```fsharp
type Base () =
    abstract Describe : unit -> string
    default _.Describe () = "base"

type Impl () =
    member _.Describe () = "impl"

let o = { new Base () with ...Impl () }
// warning: This spread does not implement any abstract members.
// Base.Describe has a default implementation and is not replaced by the spread.
```

## Relationship to FS-1151

Interface implementation spreads reuse the token, the `SynExprSpread` syntax node, the "evaluate the source exactly once" rule, and the left-to-right, right-biased composition rule of record spreads. They differ in what a spread *does* with a matched member: a record spread copies field values into a new record, while an interface implementation spread forwards calls to the source object. The [discussion of FS-1151](https://github.com/fsharp/fslang-design/discussions/806#discussioncomment-13979964) anticipated this distinction: "Interface implementation spreads (in class definitions or object expressions) would delegate, but there would be an obvious target interface (literally) in that case."

## Signature files

Interface implementations in signature files (`interface IB`) list no members and are unaffected. Whether an implementation file uses spreads or explicit members does not change the signature.

## Quotations

Spreads are not visible in quotations. An object expression containing spreads is quoted, if at all, as the equivalent object expression with explicit members.

## Static abstract members

Static abstract members cannot be implemented by forwarding to an instance and are never provided by a spread. They must be implemented explicitly, as today.

# Changes to the F# spec

- [§6.3.8 Object Expressions](https://fsharp.github.io/fslang-spec/#638-object-expressions): extend `object-members` so that `member-defns` may include `'...' expr`. Add a checking step between dispatch slot inference for explicit members and the checking of member bodies: for each open slot, determine the rightmost spread that provides it and synthesize the forwarding member. Specify that spread sources are evaluated once before construction, in textual order after the base constructor arguments.
- [§6.9.13 Evaluating Object Expressions](https://fsharp.github.io/fslang-spec/#6913-evaluating-object-expressions): add the evaluation of spread sources.
- [§8.14.3 Interface Implementations](https://fsharp.github.io/fslang-spec/#8143-interface-implementations): allow `'...' expr` among the members of an interface implementation in a class or struct with a primary constructor; specify evaluation as a construction-time binding placed after the explicit `let` and `do` bindings.
- [§14.7 Dispatch Slot Inference](https://fsharp.github.io/fslang-spec/#147-dispatch-slot-inference): define open slots and spread resolution as specified above.
- [§14.8 Dispatch Slot Checking](https://fsharp.github.io/fslang-spec/#148-dispatch-slot-checking): the dispatch map includes the synthesized mappings; the one-to-one requirement is unchanged.

Grammar additions:

```
member-defn   +=  '...' expr      // in object expressions and interface implementation blocks
```

# Drawbacks

- **A second meaning for `...`.** Record spreads copy; interface implementation spreads forward. The two are consistent in syntax, evaluation, and composition, but a reader must know which one applies. The target type makes it unambiguous, since records and interfaces cannot be confused.
- **Implicit matching by name and signature.** A spread silently binds each open slot to whichever source member matches. A source member that happens to have the right name and signature is used even if it was not intended to implement the interface. This is the same risk as calling `src.M` by hand, and the compile-time checks are stronger than those of reflection-based proxies, but the matching is less visible than an explicit member.
- **Source changes propagate.** Renaming or removing a source member turns into a compile error at the spread; changing its behavior silently changes the target. This is exactly the compile-time dependency that FS-1151 describes for record spreads, and it is the point of the feature, but users should not spread when they do not want that dependency.
- **Evaluate-once semantics can surprise.** A source such as `this.Inner` is read once at construction. Kotlin users know this pitfall from `by`. The error on mutable bindings removes the most common instance; the rest is documented.
- **Implementation complexity.** Dispatch slot inference and checking are among the more intricate parts of the type checker, and object expressions and class interface implementations follow different code paths. Synthesizing members that satisfy both paths, and producing good diagnostics, is real work.
- **Encourages object-oriented patterns.** Decorators and adapters are exactly the patterns that benefit, and some F# users would prefer the language not make them more convenient. The counterargument is that these patterns already exist in F# code bases that interoperate with .NET, and the feature only removes boilerplate.

# Alternatives

## Related F# language proposals

- [#132](https://github.com/fsharp/fslang-suggestions/issues/132): `interface I by delegator with ...`. A single source, header-level. Closed in favor of #1253.
- [#524](https://github.com/fsharp/fslang-suggestions/issues/524): `{ list with member ... }` copy-and-update for interfaces. Ambiguous with record copy-and-update, requires the source type to equal the target type, and cannot add interfaces. Closed in favor of #1253.
- [#555](https://github.com/fsharp/fslang-suggestions/issues/555): `{ new IContainer<int> xs }`. Closed in favor of #1253.
- [#1245](https://github.com/fsharp/fslang-suggestions/issues/1245): `{ new I = value with ... }`. Closed in favor of #1253.
- [#195](https://github.com/fsharp/fslang-suggestions/issues/195): implicit interface implementation. Marked "probably not"; `...this` gives most of the ergonomic benefit without changing the explicit implementation model.

## A `by` keyword

```fsharp
type B (a : IA) =
    interface IB by a with
        member _.B x = x - 1

let b = { new IB by a with member _.B x = x - 1 }
```

Pros: the source is visibly a header-level declaration, and the evaluate-once semantics are obvious. This is the Kotlin and Delphi shape.

Cons: a new contextual keyword; one source per interface, so composing several objects requires nested object expressions; no relationship to the spread syntax already in the language. The `...` form composes, interleaves with explicit members, and keeps a single mental model across records and interfaces.

## Require the source to implement the target interface

Restrict `...src` to sources whose type is a subtype of the implemented interface, so that forwarding is always "the same interface, on another object".

Pros: no signature matching; the source's own implementation is what runs.

Cons: excludes the adapter use case (#555), where the whole point is that the source does not nominally implement the interface. The subset is already expressible as `...(src :> I)`.

## Spread virtual members too

Let a spread also override virtual members with existing implementations, or add an `override ...src` form that does. This RFC restricts spreads to open slots so that a spread never silently replaces behavior, which keeps `{ new obj() with ...x }` from forwarding `Equals` and `GetHashCode`. An opt-in form can be added later without breaking changes.

## Per-call evaluation of the source

Define `...src` as pure syntactic sugar for `member _.M(...) = src.M(...)`, re-evaluating `src` on every call. This supports live delegation to a mutable field and needs no hidden storage, but it contradicts FS-1151's evaluate-once rule, diverges from Kotlin, and turns side-effecting sources such as `...(createInner ())` into a surprise. Rejected.

## Include extension members

Resolving `src.M` with extension members in scope would make a spread's meaning depend on the open modules at the spread site, and would let unrelated extensions (for example LINQ's `Contains` on any `IEnumerable<'T>`) satisfy slots silently. FS-1151 made the same call for record spreads. Adding extension members later is non-breaking.

## Library or tooling solutions

- Reflection proxies (ImpromptuInterface, Castle DynamicProxy): runtime cost, no compile-time checking.
- Source generators (Myriad) or IDE "generate delegating members" refactorings: generated code drifts from the interface and still has to be reviewed and maintained.

# Prior art

## Kotlin

[Kotlin delegation](https://kotlinlang.org/docs/delegation.html): `class Derived(b: Base) : Base by b`. The delegate expression is evaluated once and stored in the object; the compiler generates forwarders for all members of `Base`; explicit overrides in `Derived` take precedence; the delegate object does not see those overrides. Delegation is available for interfaces only. This is the closest match to the semantics proposed here, with the difference that F# spreads match members by name and signature rather than requiring the source to implement the interface.

## Delphi

Delphi has had the [`implements` directive](https://docwiki.embarcadero.com/RADStudio/Athens/en/Using_Implements_for_Delegation) since Delphi 4: `property Stream: IStream read FStream implements IStream;` delegates the interface to a field or property. Unlike every other feature surveyed, the *delegate* takes precedence: "if the delegate property is of a class type, that class and its ancestors are searched for methods implementing the specified interface before the enclosing class and its ancestors are searched", and a method declared on the enclosing class is silently ignored. Overriding a single method while delegating the rest requires a derived class with method resolution clauses, a well-known source of confusion. This RFC deliberately takes the opposite rule.

## Scala 3

[Export clauses](https://docs.scala-lang.org/scala3/reference/other-new-features/export.html): `export inner.*` defines forwarders for the members of a stable path. Term member aliases are always `final` method definitions; they "can implement deferred members of base classes" but "cannot override concrete members in base classes". Exports are not tied to a particular interface, but the forwarders are elaborated the same way as proposed here, and the rule that they fill abstract slots without replacing concrete ones is the rule this RFC adopts.

## Go

[Struct embedding](https://go.dev/doc/effective_go#embedding) promotes the methods of an embedded field to the outer struct, which then satisfies any interface those methods implement. Methods declared on the outer struct shadow promoted ones.

## Rust

Rust has no delegation feature; [RFC PR #2393](https://github.com/rust-lang/rfcs/pull/2393) was postponed, and the topic continues in [issue #3108](https://github.com/rust-lang/rfcs/issues/3108) and [issue #3133](https://github.com/rust-lang/rfcs/issues/3133), with the `delegate` and `ambassador` crates filling the gap through macros.

## C#

C# has no delegation feature. Requests for Kotlin-style delegation include [csharplang discussion #234 "Allow auto implementing delegated interface"](https://github.com/dotnet/csharplang/discussions/234) (2017, `private implicit IInterface myInterface;`) and [csharplang discussion #5514 "Syntax for interface delegation"](https://github.com/dotnet/csharplang/discussions/5514) (2021, `class C : IMyInterface by _impl`). Neither has been championed; default interface members were suggested as a partial workaround.

## Others

Groovy `@Delegate` and Lombok `@Delegate` (Java) generate forwarders from annotations. Ruby's `Forwardable` and D's `alias this` provide forwarding at the library or language level.

## JavaScript and TypeScript

Object spread copies own enumerable properties by value and does not forward method calls; methods on class prototypes are not spread at all. JavaScript's behavior is therefore prior art for record spreads (FS-1151), not for this RFC.

## Design observations

- Kotlin, Scala, and Go let explicit members win over delegated ones, regardless of position. Delphi is the exception, and its rule is the one users trip over.
- Kotlin evaluates the delegate once and stores it; Delphi delegates to a field or property; Scala requires a stable path. None re-evaluates an arbitrary expression per call.
- Scala's exports fill abstract members but never replace concrete ones, the same restriction to open slots proposed here.
- No surveyed feature forwards to extension members.

# Compatibility

* Is this a breaking change?
  * No. `...source` in a member list is new syntax that does not parse today.
* What happens when previous versions of the F# compiler encounter this design addition as source code?
  * Older compilers report a parse error: "FS0010: Unexpected symbol '..' in object expression. Expected 'member', 'override', 'static' or other token." for object expressions, and "FS0010: Unexpected symbol '..' in member definition. Expected 'member', 'override', 'static' or other token." for interface implementations in type definitions.
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
  * They consume it without issue. The compiled form consists of ordinary closure classes, explicit interface method implementations, and overrides.
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?
  * N/A. FSharp.Core is unchanged.

The feature is gated behind a language feature flag (`InterfaceImplementationSpreads`) and ships in `preview` until stabilized.

# Interop

* What happens when this feature is consumed by another .NET language?
  * Other languages see ordinary types and ordinary interface implementations. Nothing distinguishes a synthesized forwarder from a hand-written one, apart from the compiler-generated marking described under [Tooling](#tooling).
* Are there any planned or proposed features for another .NET language (e.g., C#) that we would want this feature to interoperate with?
  * C# has no delegation feature and none is planned. If one is added, it would elaborate to the same kind of forwarders and would not introduce interop concerns.

# Pragmatics

## Diagnostics

Diagnostic numbers are proposed; the implementation assigns the final numbers.

| Scenario | Expected diagnostic |
|---|---|
| `...` occurs in a member list other than an object expression or an interface implementation block (class body, type extension, interface definition, signature file) | **FS3902 — error** (existing): "Spreading is not supported in this construct." |
| `...` has no following source expression | **FS3899 — error** (existing): "Missing spread source expression after '...'." |
| The type of the source expression is not known at the spread | **FS3916 — error:** "The type of a spread source in an object expression or interface implementation must be known at this point. Consider adding a type annotation." |
| The source expression has a nullable type | **FS3917 — error:** "The source expression of a spread into an object expression or interface implementation cannot be nullable." |
| A spread appears in an interface implementation of a type without a primary constructor | **FS3918 — error:** "A spread in an interface implementation requires the enclosing type to have a primary constructor." |
| The source is a `let mutable` or `val mutable` binding | **FS3919 — error:** "A spread source cannot be a mutable binding. Bind the value to an immutable name, or write the forwarding members explicitly." |
| An open slot is not provided by any spread and not implemented explicitly | **FS3920 — error** (replaces FS0365/FS0366 when the member list contains spreads): "No implementation was given for 'abstract IB.A: int -> int'. The spread source of type 'IA' does not provide a member with this name and signature." Several sources are listed when several spreads are present. |
| A spread implements no slot | **FS3921 — warning:** "This spread does not implement any abstract members." |
| A slot is provided by more than one spread | **FS3922 — informational, off by default:** "Member 'A' from spread source 'impl' shadows the implementation of the same abstract member from an earlier spread." Reported at the shadowed spread. |
| A source has a member with the slot's name but an incompatible signature, and the slot is unimplemented | **FS3923 — informational, off by default:** "The spread source of type 'Src' has a member 'A' whose signature does not match 'abstract IB.A: int -> int'." Emitted alongside FS3920 to speed up diagnosis. |

Existing dispatch slot diagnostics (FS0017, FS0357 to FS0361, FS0365 to FS0367, FS0370, FS3213) continue to apply to explicit members unchanged.

## Tooling

* Debugging
  * Breakpoints/stepping
    * Synthesized forwarders carry no sequence points and are marked with `CompilerGeneratedAttribute` and `DebuggerNonUserCodeAttribute`, so that stepping into `b.A x` lands in `a.A` rather than in the forwarder.
    * A breakpoint on the line of a spread binds to the evaluation of the source expression.
  * Expression evaluator
    * N/A.
  * Data displays for locals and hover tips
    * The compiler-generated field or local holding the source is hidden, as other compiler-generated locals are.
* Auto-complete
  * After `...` in an object expression or interface implementation, offer values in scope, with values whose type is an interface or class first.
* Tooltips
  * Hovering over `...src` shows the type of the source and the list of slots it implements, for example "Implements IB.A, IB.Count (via 'src : IA')".
  * Hovering over the implemented type shows the ordinary type; no relationship to the spread sources is shown.
* Navigation and go-to-definition
  * Go-to-definition on `...src` navigates to the definition of `src`.
  * Go-to-implementation on an abstract member should list spreads that implement it. The compiler already records an `Implemented` name resolution occurrence for each implemented slot; synthesized implementations record it at the range of the spread.
  * Find-all-references on an abstract member includes the spreads that implement it, and find-all-references on a source member includes the spreads through which it implements a slot.
* Renaming
  * Renaming an abstract member of the target interface does not need to touch the spread; the source member must be renamed by the user, and the compiler reports FS3920 until it is.
  * Renaming a source member that currently implements a slot causes FS3920 at the spread; find-all-references shows the spread so the user can see the consequence before renaming.
* Error recovery (wrong, incomplete code)
  * A missing source expression after `...` recovers as in FS-1151.
  * A spread in an unsupported position is reported and otherwise ignored.
  * An unimplemented slot does not prevent the rest of the object expression or type from being checked.
* Colorization
  * `...` is the existing `DOT_DOT_DOT` token and is colorized as in FS-1151.
* Brace/parenthesis matching
  * N/A.
* Formatting
  * Fantomas must learn the new `SynMemberDefn` case and format it as a member.
* Analyzers/code fixes
  * A refactoring that expands a spread into the explicit forwarding members it stands for, symmetric to the refactoring proposed for FS-1151.
  * A code fix that collapses a run of hand-written forwarders `member _.M(args) = src.M(args)` into `...src`.

## Performance

### Compilation performance

* For existing code
  * No effect. Code without spreads goes through the existing dispatch slot machinery unchanged.
* For the new feature
  * Spread resolution performs, per spread and per open slot, one intrinsic member lookup and one method application resolution, so it is linear in the number of spreads times the number of open slots. This is the same work the compiler would do for the equivalent hand-written members.

### Runtime performance

* For existing code
  * Unchanged.
* For the new feature
  * Identical to the equivalent hand-written forwarders: one extra call per forwarded member, plus one field per spread source in a class.

## Scaling

* Expected maximum number of abstract members in an implemented interface or class in hand-written code: 50. Large COM and UI automation interfaces reach 100 or more.
* Expected reasonable upper bound for the number of slots the compiler accepts: unbounded; compilation is linear in the number of slots.
* Expected maximum number of spreads per member list in hand-written code: 3.
* Expected reasonable upper bound accepted: unbounded; compilation is linear in spreads times slots.

## Culture-aware formatting/parsing

* No. The feature has no text output.

# Unresolved questions

1. **Implicit self.** Should a bare `...` inside an `interface ... with` block mean `...this`, as proposed in the [FS-1151 discussion](https://github.com/fsharp/fslang-design/discussions/806#discussioncomment-13738145)? This RFC requires an explicit `as this` self identifier, which is more obvious but brings the runtime initialization checks of `as this` with it. Should the compiler avoid those checks when `this` is used only in spreads?
2. **Base class abstract members in class definitions.** Should `...src` be allowed in the member list of a class that inherits an abstract base class, outside `interface` blocks, to implement the base class's abstract members? Object expressions already cover the equivalent case; the class form needs a grammar decision.
3. **`override ...src`.** Should there be an opt-in form that also replaces virtual members with existing implementations, for decorators over abstract classes such as `TextWriter` or `Stream`?
4. **Types without a primary constructor.** Records, unions, and classes with only explicit constructors cannot host construction-time bindings. Is a `this`-based form (`...this.Inner`) worth supporting for them?
5. **Mutable sources.** Is the error on `let mutable` and `val mutable` sources the right call, or should the compiler warn and capture the initial value?
6. **Compiler-generated marking.** FS-1151 treats spreads as fully interchangeable with their expanded form. Marking synthesized forwarders as compiler-generated helps debugging but makes the compiled output distinguishable. Which matters more?
7. **Extension members.** Revisit inclusion if real-world usage shows a need.
8. **Diagnostic placement.** FS-1151 reports spread-over-spread shadowing at the shadowing spread; this RFC reports FS3922 at the shadowed spread, since that is the dead code. Align the two if the FS-1151 placement is preferred.
