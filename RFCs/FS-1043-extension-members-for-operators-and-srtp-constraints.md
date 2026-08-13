# F# RFC FS-1043 - Extension members become available to solve operator trait constraints

These design suggestions:
* https://github.com/fsharp/fslang-suggestions/issues/230
* https://github.com/fsharp/fslang-suggestions/issues/29
* https://github.com/fsharp/fslang-suggestions/issues/820

have been marked "approved in principle". This RFC covers the detailed proposal for these

* [x] Approved in principle
* [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)
* [x] [Implementation](https://github.com/dotnet/fsharp/pull/19602) (continues the original prototype [#8404](https://github.com/dotnet/fsharp/pull/8404))


# Summary
[summary]: #summary

Extension members were previously ignored by SRTP constraint resolution.  This RFC means they are taken into account.

For example, consider
```fsharp

type System.String with
    static member ( * ) (foo, n: int) = String.replicate n foo

let r4 = "r" * 4
let spaces n = " " * n
```
Prior to this RFC the result is:
```
foo.fs(2,21): warning FS1215: Extension members cannot provide operator overloads.  Consider defining the operator as part of the type definition instead.
foo.fs(4,16): error FS0001: The type 'int' does not match the type 'string'
```
With this RFC, the code compiles. A type extension may declare an operator for a type of restricted accessibility, including an `internal` type, which is the second half of suggestion #230.

In addition, this RFC adds an attribute `AllowOverloadOnReturnTypeAttribute` to FSharp.Core to implement suggestion [Consider the return type in overload resolution](https://github.com/fsharp/fslang-suggestions/issues/820). If this is present on any applicable overloads in a method overload resolution, then the return type is also checked/unified when determining overload resolution.  Previously, only methods named `op_Explicit` and `op_Implicit` were given this treatment.

This RFC also modifies the process of solving SRTP constraints, as described in the sections below.

# Motivation
[motivation]: #motivation

It is reasonable to use extension methods to retrofit operators onto existing types. This removes the asymmetry where ordinary members could be added via a type extension but operators could not participate in the generic inline code that consumes them.

This RFC also bundles two smaller changes that share the same resolution machinery: return-type-directed overload resolution (previously reserved for `op_Explicit`/`op_Implicit`), exposed through the `AllowOverloadOnReturnTypeAttribute`; and the ability to write type extensions directly on tuple types.


# Detailed design
[design]: #detailed-design


## Adding extension members to SRTP constraint solving

The proposed change is as follows, in the internal logic of the constraint solving process:

1. During constraint solving, the record of each SRTP constraint incorporates the relevant extension methods in-scope at the point the SRTP constraint is asserted. That is, at the point a generic construct is used and "freshened".  The accessibility domain (i.e. the information indicating accessible methods) is also noted as part of the constraint.  Both of these pieces of information are propagated as part of the constraint. We call these the *trait possible extension solutions* and the *trait accessor domain*

2. When checking whether one unsolved SRTP constraint A *implies* another B (note: this is a process used to avoid asserting duplicate constraints when propagating a constraint from one type parameter to another), both the possible extension solutions and the accessor domain of A are ignored, and those of the existing asserted constraint are preferred.

3. When checking whether one unsolved SRTP constraint is *consistent* with another (note: this is a process used to check for inconsistency errors amongst a set of constraints), the possible extension solutions and accessor domain are ignored.

4. When attempting to solve the constraint via overload resolution, the possible extension solutions which are accessible from the trait accessor domain are taken into account.  An extension member that is not accessible at the use site (for example a `private` or `internal` member outside its declaring scope) is never selected, so extension members do not act as hidden witnesses for code that cannot access them.

5. Built-in constraint solutions for things like `op_Addition` constraints are applied if and when the relevant types match precisely, and are applied even if some extension methods of that name are available.

## Return-type-directed overload resolution

This RFC adds `AllowOverloadOnReturnTypeAttribute` to FSharp.Core:

```fsharp
[<AttributeUsage(AttributeTargets.Method, AllowMultiple = false)>]
[<Sealed>]
type AllowOverloadOnReturnTypeAttribute =
    inherit System.Attribute
    new: unit -> AllowOverloadOnReturnTypeAttribute
```

When at least one applicable overload in a method group carries this attribute and an expected
return type is available at the call site, that expected type is unified with each candidate's
declared return type during overload resolution, and candidates whose return type does not unify
are discarded. This is the treatment previously reserved for `op_Explicit` and `op_Implicit`, now
available to any method. When no expected return type is known, resolution is unchanged.

```fsharp
type Converter =
    [<AllowOverloadOnReturnType>] static member Convert (x: string) : int = int x
    [<AllowOverloadOnReturnType>] static member Convert (x: string) : float = float x

let a : int   = Converter.Convert "42"   // selects the int-returning overload
let b : float = Converter.Convert "42"   // selects the float-returning overload
```

The same resolution applies when the call is reached through an inline SRTP constraint.

Unlike the rest of this RFC, this attribute is not gated by `--langversion:preview`: it takes
effect wherever the referenced FSharp.Core defines it.

## Tuple type extensions

As part of this implementation, type extensions may be written directly on tuple types using
tuple syntax. The tuple type in the augmentation position is rewritten to its underlying named
type: a reference tuple `type ('T1 * 'T2) with ...` extends `System.Tuple<'T1, 'T2>`, and a
struct tuple `type struct ('T1 * 'T2) with ...` extends `System.ValueTuple<'T1, 'T2>`. This lets
extension operators/members (including those participating in SRTP resolution) be attached to
tuple types. The capability is gated behind the same preview language feature as the rest of this
RFC; below preview the syntax parses but is rejected with a feature-availability diagnostic.

```fsharp
type (int * string) with
    static member Combined (t: int * string) = fst t + (snd t).Length
```

Tuples of any arity are supported by mapping to the corresponding `System.Tuple` or
`System.ValueTuple` shape, including arities above seven where the underlying type is nested.

## Weak resolution no longer forces overload resolution for SRTP constraints prior to generalizing `inline` code

Prior to this RFC, for generic inline code we apply "weak resolution" to constraints prior to generalization.

Consider this:
```
open System
let inline f1 (x: DateTime) y = x + y;;
let inline f2 (x: DateTime) y = x - y;;
```
The relevant available overloads are:
```fsharp
type System.DateTime with
    static member op_Addition: DateTime * TimeSpan -> DateTime
    static member op_Subtraction: DateTime * TimeSpan -> DateTime
    static member op_Subtraction: DateTime * DateTime -> TimeSpan
```
Prior to this RFC, `f1` is generalized to **non-generic** code, and `f2` is correctly generalized to generic code, as seen by these types:
```
val inline f1 : x:DateTime -> y:TimeSpan -> DateTime
val inline f2 : x:DateTime -> y: ^a ->  ^b  when (DateTime or  ^a) : (static member ( - ) : System.DateTime * ^a ->  ^b)
```
This happens because prior to this RFC, generalization invokes "weak resolution" for both inline and non-inline code.  This caused
overload resolution to be applied even though the second parameter type of "y" is not known.

* In the first case, overload resolution for `op_Addition` succeeded because there is only one overload.

* In the second case, overload resolution for `op_Subtraction` failed because there are two overloads. The failure is ignored, and the code is left generic.

For non-inline code and primitive types this "weak resolution" process is reasonable.  But for inline code it was incorrect, especially in the context of this RFC, because future extension methods may now provide additional witnesses for `+` on DateTime and some other type.  

In this RFC, we disable weak resolution for inline code for cases that involve true overload resolution. This changes
inferred types in some situations, e.g. with this RFC the type is now as follows:
```
> let inline f1 (x: DateTime) y = x + y;;
val inline f1 : x:DateTime -> y: ^a ->  ^b when (DateTime or  ^a) : (static member ( + ) : DateTime * ^a ->  ^b)
```

Some signatures files may need to be updated to account for this change.





# Drawbacks
[drawbacks]: #drawbacks

* This slightly strengthens the "type-class"-like capabilities of SRTP resolution. This means that people may increasingly use SRTP code as a way to write generic, reusable code rather than passing parameters explicitly.  While this is reasonable for generic arithmetic code, it has many downsides when applied to other things.

# Alternatives
[alternatives]: #alternatives

1. Don't do it. Extension operators would continue to be rejected as SRTP witnesses (FS1215), and return-type-directed overload resolution would remain available only through the dummy-parameter workaround described in suggestion #820.


# Examples

The Summary shows the core case: an in-scope extension operator solves an operator application that
previously failed with FS1215/FS0001.

## Out of scope: cross-type widening and return-type-polymorphic conversion

Two patterns that extension SRTP might appear to enable are deliberately not supported, because an
applicable built-in operator solution is always preferred to an extension member (design point 5).

Cross-type numeric widening, such as making `1 + 2L` check by adding `(+)` overloads through
extensions, does not work: `1 + 2L` resolves the built-in `int (+)`, which forces both operands to
`int` and rejects `2L` with `error FS0001: The type 'int64' does not match the type 'int'`.

Populating a return-type-polymorphic `op_Implicit` through extensions is likewise out of scope:

```fsharp
let inline implicitConv (x: ^T) : ^U = ((^T or ^U) : (static member op_Implicit : ^T -> ^U) (x))
```

A single-overload `op_Implicit` with a fixed return type does resolve through an extension member;
only the return-type-polymorphic `(^T or ^U)` form above stays out of scope.

# Interop

* C# consumers see no difference: extension SRTP constraints are an F#-only concept resolved at compile time. The emitted IL is standard .NET.
* Extension members solve structural SRTP constraints but do *not* make a type satisfy nominal static abstract interface constraints (`INumber<'T>`, `IAdditionOperators<'T,'T,'T>`, etc.). IWSAMs ([FS-1124](https://github.com/fsharp/fslang-design/blob/main/FSharp-7.0/FS-1124-interfaces-with-static-abstract-members.md)) and extension SRTP solving are orthogonal resolution mechanisms.

# Pragmatics

## Diagnostics

* **FS1215** ("Extension members cannot provide operator overloads"): no longer emitted when the feature is enabled, because extension operators are now valid SRTP witnesses. Fires as before when the feature is disabled.
* Overload ambiguity introduced by extension methods uses existing error codes; no additional diagnostics for that case.

## Tooling

* **Tooltips**: show the more generic inferred type for inline functions (e.g., `^a -> ^b when ...` instead of `int -> int`).
* **Auto-complete**: extension operators now appear in SRTP-resolved member lists when the feature is enabled.
* No changes to debugging, breakpoints, colorization, or brace matching.

## Performance

* Extension method lookup during SRTP constraint solving adds overhead proportional to the number of extension methods in scope. Compiler performance degrades when resolving heavily overloaded constraints.
* Resolution cost is proportional to the candidate set size per constraint. Libraries such as FSharpPlus that define many overloads per operator family may observe measurable slowdown; profiling is ongoing.
* No impact on generated code performance: the resolved call sites are identical.

## Witnesses

Extension members now participate as witnesses for SRTP constraints (see [RFC FS-1071](https://github.com/fsharp/fslang-design/blob/main/FSharp-5.0/FS-1071-witness-passing-quotations.md)). Quotations of inline SRTP calls may now capture extension methods as witnesses:

```fsharp
type [<Struct>] MyNum = { V: int }

[<AutoOpen>]
module MyNumExt =
    type MyNum with
        static member inline (+) (a: MyNum, b: MyNum) = { V = a.V + b.V }

let inline add x y = x + y
let q = <@ add { V = 1 } { V = 2 } @>  // witness is MyNumExt.(+)
```

## Binary compatibility (pickling)

The *trait possible extension solutions* and *trait accessor domain* (design points 1–4) are **not** serialized into compiled DLLs. They exist only during in-process constraint solving and are discarded before metadata emission. Consequently:

* Cross-version binary compatibility is unaffected: no new fields are added to the pickled SRTP constraint format.
* When an `inline` function is consumed from a compiled DLL, extension operators available at the *consumer's* call site are used for constraint solving, not those that were in scope when the library was compiled. This is consistent with how SRTP constraints are freshened at each use site.



# Compatibility
[compatibility]: #compatibility

Status: This RFC **is** a breaking change. The extension-SRTP, tuple-extension, and weak-resolution changes are gated behind `--langversion:preview`. The `AllowOverloadOnReturnTypeAttribute` behavior is gated only by the presence of that attribute in the referenced FSharp.Core, not by the language version.

**What breaks**:
- Inferred types of inline SRTP functions become more generic when extension operators are in scope (e.g., `val inline f : int -> int` becomes `val inline f : x: ^a -> ^b when ...`). Signature files need updating.
- A point-free binding such as `let g : int -> int = f` may stop compiling when `f` is now generic; rewrite it in expanded form, for example `let g x = f x`.
- Extension methods in SRTP resolution may introduce new overload ambiguity at call sites, including concretely-typed ones where a new extension candidate is in scope.
- The set of functions whose non-inline invocation throws `NotSupportedException` at runtime grows: previously, weak resolution eagerly picked a concrete implementation; now the constraint may stay open, and the non-witness fallback method body throws.
- `AllowOverloadOnReturnTypeAttribute` changes overload resolution behavior: when present on any applicable overload, the return type is unified during resolution. Existing code relying on the current resolution order (which ignores return types except for `op_Explicit`/`op_Implicit`) may select a different overload or become ambiguous.

**What does NOT break**:
- Concrete operator uses with no extension operators in scope are unaffected. (Inline function *definitions* may still gain a more generic inferred signature, as described above.)
- Call-site operator resolution for primitive types without in-scope extensions.

**FSharpPlus coordination**: Deferred; see workarounds documented below.

**Risk mitigation**:

1. Extension methods are lower priority in overload resolution.
2. For built-in operators like `(+)`, there will be relatively few candidate extension methods in F# code.
3. Nearly all SRTP constraints for built-in operators are on static members, and C# code can't introduce static extension members.

The weak resolution change corrects the generalization behavior described above. One case has been identified where complex SRTP code such as found in FSharpPlus no longer compiles under this change.

### Example

Here is a standalone repro reduced substantially, and where many types are made more explicit. Given these definitions:
```fsharp
let inline InvokeMap (mapping: ^F) (source: ^I) : ^R =
    ((^I or ^R) : (static member Map : ^I * ^F -> ^R) source, mapping)

let inline InvokeApply (f: ^F) (x: ^X) : ^R =
    ((^F or ^X or ^R) : (static member Apply : ^F * ^X -> ^R) f, x)

// A simulated collection carrying both a 'Map' and an 'Apply' witness
type ZipList<'T>() =
    static member Map (source: ZipList<'a>, mapping: 'a -> 'b) : ZipList<'b> = ZipList<'b>()
    static member Apply (f: ZipList<'a -> 'b>, x: ZipList<'a>) : ZipList<'b> = ZipList<'b>()
```
the following generic inline function fails to compile with this RFC activated:
```fsharp
let inline AddZipLists (x: ZipList<'a>) (y: ZipList<'a>) : ZipList<'a> =
    InvokeApply (InvokeMap (+) x) y
```

### Explanation

The characteristics are
1. There is no overloading directly, but this code is generic and there is the *potential* for further overloading by adding further extension methods.

2. The definition of the member constraint allows resolution by **return type**, e.g. `(^I or ^R)` for `Map` .  Because of this, the return type of the inner `InvokeMap` call is **not** known to be `ZipList` until weak resolution is applied to the constraints. This is because extra overloads could in theory be added via new witnesses mapping the collection to a different collection type.

3. The resolution of the nested member constraints will eventually imply that the type variable `'a` support the addition operator.
   However after this RFC, the generic function `AddZipLists` now gets generalized **before** the member constraints are fully solved
   and the return types known.  The process of generalizing the function makes the type variable `'a` rigid (generalized).  The
   member constraints are then solved via weak resolution in the final phase of inference, and the return type of `InvokeMap`
   is determined to be a `ZipList`, and the `'a` variable now requires an addition operator.  Because the code has already
   been generalized the process of asserting this constraint fails with an obscure error message.

### Workarounds

There are numerous workarounds, shown here on the `ZipList` setup above:

1. sequentialize the constraint problem rather than combining the resolution of the `Apply` and `Map` methods, e.g.
```fsharp
let inline (+) (x: ZipList<'a>, y: ZipList<'a>) : ZipList<'a> =
    let f = InvokeMap (+) x
    InvokeApply f y
```
   This works because using `let f = InvokeMap (+) x` forces weak resolution of the constraints involved in this construct (whereas passing `InvokeMap (+) x` directly as an argument to `InvokeApply f y` leaves the resolution delayed). 

2. Another approach is to annotate, e.g.
```fsharp
let inline (+) (x: ZipList<'a>, y: ZipList<'a>) : ZipList<'a> =
    InvokeApply (InvokeMap ((+): 'a -> 'a -> 'a) x) y
```
   This works because the type annotation means the `op_Addition` constraint is immediately associated with the type variable `'a` that is part of the function signature.

3. Another approach (and likely the best) is to **no longer use return types as support types** in this kind of generic code.  (Using return types as support types in such cases was basically "only" to delay weak resolution anyway.)  This means defining `InvokeMap` with the return type removed from the support-type list:

```fsharp
let inline InvokeMap (mapping: ^F) (source: ^I) : ^R =
    (^I : (static member Map : ^I * ^F -> ^R) source, mapping)
```
instead of
```fsharp
let inline InvokeMap (mapping: ^F) (source: ^I) : ^R =
    ((^I or ^R) : (static member Map : ^I * ^F -> ^R) source, mapping)
```

   With this change the code compiles.

This is the only known example of this pattern in FSharpPlus. However, client code of FSharpPlus may also encounter this issue. In general, this may occur whenever there is

```
     let inline SomeGenericFunction (...) =
        ...some composition of FSharpPlus operations that use return types to resolve member constraints....
```

We expect this pattern to occur in client code of FSharpPlus. The recommendation is:

1. We keep the change to avoid weak resolution as part of the RFC 

2. We adjust FSharpPlus to no longer use return types as resolvers unless absolutely necessary

3. We apply workarounds for client code by adding further type annotations

As part of this RFC we should also deliver a guide on writing SRTP code that documents cases like this and
gives guidelines about their use.

# Unresolved questions
[unresolved]: #unresolved-questions

None outstanding. One point was investigated during design: whether constraints that flow together from different accessibility domains could observe an inconsistent set of candidate extension members. This does not arise in practice, because SRTP constraints are freshened and solved within a single scope where the available members and accessibility are consistent. Two modules that each provide extension operators on the same type resolve correctly when both are opened.


