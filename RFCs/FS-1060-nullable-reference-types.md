# F# RFC FS-1060 - Nullable Reference Types

The design suggestion [Add non-nullable instantiations of nullable types, and interop with proposed C# 8.0 non-nullable reference types](https://github.com/fsharp/fslang-suggestions/issues/577) has been marked "approved in principle". This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/577)
* [ ] Details: [TODO](https://github.com/fsharp/fslang-design/issues/FILL-ME-IN)
* [ ] Implementation: [TODO](https://github.com/Microsoft/visualfsharp/pull/FILL-ME-IN)

## Summary
[summary]: #summary

The main goal of this feature is to address the pains of dealing with implicitly nullable reference types by providing syntax and behavior that distinguishes between nullable and non-nullable reference types. This will be done in lockstep with C# 8.0, which will also have this as a feature, and F# will interoperate with C# by respecting and emitting the same metadata that C# emits.

Conceptually, reference types can be thought of as having two forms:

* Normal reference types
* Nullable reference types

These will be distinguished via syntax, type signatures, and tools. Additionally, there will be flow analysis to allow you to determine when a nullable reference type is non-null and can be treated as a reference type.

Warnings are emitted when reference and nullable reference types are not used according with their intent, and these warnings are tunable as errors.

For the purposes of this document, the terms "reference type" and "non-nullable reference type" may be used interchangeably. They refer to the same thing.

## Motivation
[motivation]: #motivation

Today, any reference type (e.g., `string`) could be `null`, forcing developers to account for this with special checks at the top-level of their functions or methods. Especially in F#, where types are almost always assumed to be non-null, this is a pain that can often be forgotten. Moreover, one of the primary benefits in working with F# is working with immutable and **non-null** data. By this statement alone, nullable reference types are in line with the general goals of F#.

The `[<AllowNullLiteral>]` attribute allows you to declare F# reference types as nullable, but this "infects" any place where the type might be used. In practice, F# types are not normally checked for `null`, which could mean that there is a lot of code out there that does not account for `null` when using a type that is decorated with `[<AllowNullLiteral>]`.

Additionally, nullable reference types is a headlining feature of C# 8.0 that will have a dramatic impact on .NET and its ecosystem. It is vital that we interoperate with this change smoothly and can emit the appropriate information so that C# consumers of F# components can consume things as if they were also C# 8.0 components.

The desired outcome over time is that significantly less `NullReferenceException`s are produced by code at runtime. This feature should allow F# programmers to more safely eject `null` from their concerns when interoperating with other .NET components, and make it more explicit when `null` could be a problem.

## Principles

The following principles guide all further considerations and the design of this feature.

1. On the surface, F# is "almost" as simple to use as it is today. In practice, it must feel simpler due to nullability of types like `string` being explicit rather than implicit.
2. The value for F# is primarily in flowing non-nullable reference types into F# code from .NET Libraries and from F# code into .NET libraries.
3. Adding nullable annotations should not be part of routine F# programming.
4. Nullability annotations/information is carefully suppressed and simplified in tooling (tooltips, FSI) to avoid extra information overload.
5. F# users are typically concerned with this feature at the boundaries of their system so that they can flow non-null data into the inner parts of their system.
6. The feature produces warnings by default, with different classes of warnings (nullable, non-nullable) offering opt-in/opt-out mechanisms.
7. All existing F# projects compile with warnings turned off by default. Only new projects have warnings on by default. Compiling older F# code with newer F# code does not opt-in the older F# code.
8. F# non-nullness is reasonably "strong and trustworthy" today for F#-defined types and routine F# coding. This includes the explicit use of `option` types to represent the absence of information. No compile-time guarantees today will be "lowered" to account for this feature.
9. The F# compiler will strive to provide flow analysis such that common null-check scenarios guarantee null-safety when working with nullable reference types.
10. This feature is useful for F# in other contexts, such as interoperation with `string` in JavaScript via [Fable](http://fable.io/).
11. F# programmers can start to phase out their use of `[<AllowNullLiteral>]` and the `null` type constraint when they need ad-hoc nullability for reference types declared in F#.
12. Syntax for the feature feels "baked in" to the type system, and should not be horribly unpleasant to use.

## Detailed design
[design]: #detailed-design

### Metadata

Nullable reference types are emitted as a reference type with an assembly-level attribute in C# 8.0. That is, the following code:

```csharp
public class D {
    public void M(string? s) {}
}
```

Is the same as the following:

```csharp
public class D
{
    public void M([System.Runtime.CompilerServices.Nullable] string s)
    {
    }
}
```

Languages that are unaware of this attribute will see `M` as a method that takes in a `string`. That is, to be unaware of and failing to respect this attribute means that you will view incoming reference types exactly as before.

F# will be aware of and respect this attribute, treating reference types as nullable reference types if they have this attribute. If they do not, then those reference types will be non-nullable.

F# will also emit this attribute when it produces a nullable reference type for other languages to consume.

#### Nullability obliviousness

It is desireable for some assemblies to declare themselves as nullability-oblivious, even if they were compiled with a compiler that has this as a concept. This is a top-level attribute that will be respected by the C# compiler. F# should also respect this as an attribute.

#### Handling nullability

C# 8.0 will also respect a new attribute that can mark a method as "handling null". This is to get around the non-determinism in checking if any arbitrary method call (and its calls) check for `null` and do not re-assign it somehow.

This attribute (we'll call it `[<HandlesNull>]`) will also be respected by F#, so that obvious calls such as `String.IsNullorEmpty` that wrap a dereference don't trigger a warning.

It's worth noting that this attribute could be abused.

### F# concept and syntax

Nullable reference types can conceptually be thought of a union between a reference type and `null`. For example, `string` in F# 4.5 or C# 7.3 is implicitly either a `string` or `null`. This concept is lifted into syntax:

```
reference-type | null
```

Examples in F# code:

```fsharp
// Parameter to a function
let len (str: string | null) =
    match str with
    | null -> -1
    | s -> s.Length

// Return type
let findOrNull (index: int) (list: 'T list) : 'T | null =
    match List.tryItem index list with
    | Some item -> item
    | None -> null

// Declared type at let-binding
let maybeAValue : int | null = hopefullyGetAnInteger()

// Generic type parameter
type IMyInterface<'T | null, > =
    abstract DoStuff<'T | null> : unit -> 'T | null

// Array type signature
let f (arr: (float | null) []) = ()

// Nullable types with nullable generic types
type C<'T | null>() =
    member __.M(param1: List<List<'T | null> | null> | null) = ()
```

The syntax accomplishes two goals:

1. Makes the distinction that a nullable reference type can be `null` clear in syntax.
2. Becomes a bit unweildy when nullability is used a lot, particularly with nested types.

Because it is a design goal to flow **non-null** reference types, and **avoid** flowing nullable reference types unless absolutely necessary, it is considered a feature that the syntax can get a bit unweildy if used everywhere. That is, we want to discourage the use of `null` unless it is absolutely necessary.

#### Nullable reference type declarations in F\#

There will be no way to declare an F# type as follows:

```fsharp
// This will not be possible!
type (SomeClass | null)() = class end
```

And in existing F# today, the only way to declare an F# reference type that could be `null` in F# code is to use `[<AllowNullLiteral>]`. This would not change with this proposal; that is, the only way to declare a reference type in F# that could have `null` as a value in F# code is `[<AllowNullLiteral>]`.

### Checking of nullable references

There are a few common ways that F# programmers check for null today. Unless explicitly mentioned as unsupported, support for flow analysis is under consideration for certain null-checking patterns.

#### Likely supported: Pattern matching with alias

```fsharp
let len (str: string | null) =
    match str with
    | null -> -1
    | s -> s.Length // OK - we know 's' is string
```

This is by far the most common pattern in F# programming. In the previous code sample, `str` is of type `string | null`, but `s` is now of type `string`. This is because we know that based on the `null` has been accounted for by the `null` pattern.

#### Likely supported: isNull and others using null-handling decoration

```fsharp
let len (str: string | null) =
    if isNull str then
        -1
    else
        str.Length // OK
```

The `isNull` function is considered to be a good way to check for `null`. We can annotate it in FSharp.Core with the attribute used for functions handling null (we'll call it `[<HandlesNull>]`).

Similarly, CoreFX will annotate methods like `String.IsNullOrEmpty` as handling `null`. This means that the most common of null-checking methods can be respected by flow analysis and reduce the amount of nullability assertions.

##### Possibly supported: Pattern matching with wildcard

```fsharp
let len (str: string | null) =
    match str with
    | null -> -1
    | _ -> str.Length // OK - 'str' is string
```

A slight riff that is less common is treating `str` differently when in a wildcard pattern's scope. To reach that code path, `str` must indeed by non-null, so this could potentially be supported.

#### Possibly supported: If check

```fsharp
let len (str: string | null) =
    if str = null then
        -1
    else
        str.Length // OK - 'str' is a string
```

Although less common, this is a valid way to check for `null` and could be enabled by the compiler. We know for certain that `str` is a `string` in the `else` branch. The inverse check is also true.

#### Possibly supported: After null check within boolean expression

```fsharp
let len (str: string | null) =
    if (str <> null && str.Length > 0) then // OK - `str` must be null
        str.Length // OK here too
    else
        -1
```

Note that the reverse (`str.Length > 0 && str <> null`) would give a warning, because we attempt to dereference before the `null` check. This would only hold with AND checks. More generally, if the boolean expression to the left of the dereference involves a `x <> null` check, then the dereference is safe.

#### Likely unsupported: boolean-based checks for null that lack handlesnull decoration

Generally speaking, beyond highly-specialized cases, we cannot guarantee non-nullability that is checked via a boolean. Consider the following:

```fsharp
let myIsNull item = isNull item

let len (str: string | null) =
    if myIsNull str then
        -1
    else
        str.Length // WARNING: could be null
```

Although this simple example could potentially work, it could quickly get out of hand. Also, other languages that support nullable reference types (C# 8.0, Scala, Kotlin, Swift, TypeScript) do not do this. Instead, non-nullability is asserted when the programmer knows something will not be `null`.

#### Likely unsupported: nested functions accessing outer scopes

```fsharp
let len (str: string | null) =
    let doWork() =
        str.Length // WARNING: maybe too complicated?

    match str with
    | null -> -1
    | _ -> doWork() // But after this line is written it's fine?
```

Although `doWork()` is called only in a scope where `str` would be non-null, this may too complex to implement properly.

**Note:** C# supports the equivalent of this with local functions. TypeScript does not support this.

#### Asserting non-nullability

To get around scenarios where flow analysis cannot establish a non-null situation, a programmer can use `!` to assert non-nullability:

```fsharp
let myIsNull item = isNull item

let len (str: string | null) =
    if myIsNull str then
        -1
    else
        str!.Length // OK: 'str' is asserted to be 'null'
```

This will not generate a warning, but it is unsafe and can be the cause of a `NullReferenceException` if the reference was indeed `null` under some condition.

#### Warnings

In all other situations not covered by flow analysis, a warning is emitted by the compiler if a nullable reference is dereferenced or casted to a non-null type:

```fsharp
let f (ns: string | null) =
    printfn "%d" ns.Length // WARNING: 'ns' may be null

    let mutable ns2 = ns
    printfn "%d" ns2.Length // WARNING: 'ns2' may be null

    let s = ns :> string // WARNING: 'ns' may be null
```

A warning is given when casting from `'S[]` to `('T | null)[]` and from `('S | null)[]` to `'T[]`.

A warning is given when casting from `C<'S>` to `C<'T | null>` and from `C<'S | null>` to `C<'T>`.

### Checking of non-null references

#### Null assignment and passing

A warning is given if `null` is assigned to a non-null value or passed as a parameter where a non-null reference type is expected:

```fsharp
let mutable s: string = "hello"
s <- null // WARNING

let s2: string = null // WARNING

let f (s: string) = ()
f null // WARNING
```

#### F# Collection initialization

A warning is given if `null` is used in an F# array/list/seq expression, where the type is already annotated to be non-nullable:

```fsharp
let xs: string[] = [| ""; ""; null |] // WARNING
let ys: string list = [ ""; ""; null ] // WARNING
let zs: seq<string> = seq { yield ""; yield ""; yield null } // WARNING
```

#### Constructor initialization

Today, it is already a compile error if all fields in an F# class are not initialized by constructor calls. This behavior is unchanged. See [Compatibility with Microsoft.FSharp.Core.DefaultValueAttribute](FS-1060-nullable-reference-types.md#compatibility-with-microsoft.fsharp.core.defaultValueAttribute) for considerations about code that uses `[<DefaultValue>]` with reference types today, which produces `null`.

### Generics

A type parameter is assumed to have non-nullable constraints when declared "normally":

```fsharp
type C<'T>() = // 'T is non-nullable
class end
```

A warning is given if a nullable reference type is used to parameterize another type `C` that  does not accept nullable reference types:

```fsharp
type C<'T>() = class end

let c = C<string | null> // WARNING
```

#### Constraints

Today, there are two relevant constraints in F# - `null` and `not struct`:

```fsharp
type C1<'T when 'T: null>() = class end
type C2<'T when 'T: not struct>() = class end
```

Both are actually the same thing for interoperation purposes, as they emit `class` as a constraint. See [Unresolved questions](nullable-reference-types.md#unresolved-questions) for more.

If an F# class `B` has nullability as a constraint, then an inheriting class `D` also inherits this constraint, as per existing inheritance rules:

```fsharp
type B<'T | null>() = class end
type D<'T | null>()
    inherit B<'T>
```

That is, nullabulity constraints propagate via inheritance.

If an unconstrained type parameter comes from an older C# assembly, it is assumed to be nullable, and warnings are given when non-nullability is assumed. If it comes from F# and it does not have the `null` constraint, then it is assumed to be non-nullable. If it does, then it is assumed to be nullable.

Additionally, C# will introduce the `class?` constraint. This syntax sugar will compile into the `class` constraint, but the class itself will be decorated with an attribute that tells consumers that the `'T` is a nullable reference type. This attribute will need to be respected by the F# compiler.

### Type inference

Nullability is propagated through type inference:

```fsharp
// Inferred signature:
//
// val makeNullIfEmpty : str:string -> string | null
let makeNullIfEmpty (str: string) =
    match str with
    | "" -> null
    | _ -> str

 // val xs : (string | null) list
let xs = [ ""; ""; null ]

// val ys : (string | null) []
let ys = [| ""; null; "" |]

// val zs : seq<string | null>
let zs = seq { yield ""; yield null }

// val x : 'a | null
let x = null

// val s : string
let mutable s = "hello"

// s is now (s | null), despite the warning
s <- null
```

This includes function composition:

```fsharp
// val f1 : 'a -> 'a
let f1 s = s

// val f2 : string -> (string | null)
let f2 s = if s <> "" then "hello" else null

// val f3 : string -> string
let f3 (s: string) =
    match s with
    | null -> ""
    | _ -> s

// val pipeline : string -> (string | null)
let pipeline = f1 >> f2 >> f3 // WARNING - flowing possible null as input to function that assumes a non-null input
```

Existing nullability rules for F# types hold (i.e., you cannot suddenly assign `null` to an F# record).

### Tooling considerations

To remain in line our first principle:

> On the surface, F# is "almost" as simple to use as it is today. In practice, it must feel simpler due to nullability of types like `string` being explicit rather than implicit.

We'll consider careful suppression of nullability in F# tooling so that the extra information doesn't appear overwhelming. For example, imagine the following signature in tooltips:

```fsharp
member Join : source2: (Expression<seq<string | null> | null> | null) * key1:(Expression<Func<(string | null), T | null> | null> | null)) * key2:(Expression<Func<(string | null), T | null> | null> | null))
```

AGH! This signature is challenging enough already, but now nullability has made it significantly worse.

#### F# tooltips

In QuickInfo tooltips today, we reduce the length of generic signatures with a section below the signature. We can do a similar thing for saying if something is nullable:

```
member Foo : source: (seq<'T>) * predicate: ('T -> 'U- > bool) * item: 'U

    where 'T, 'U are nullable

Generic parameters:
'T is string
'U is int
```

Although this uses more vertical space in the tooltip, it reduces the length of the signature, which can already difficult to parse for many members in .NET.

In Signature Help tooltips today, we embed the concrete type for a generic type parameter in the tooltip. We may consider doing a similar thing as with QuickInfo, where nullability is expressed separately from the signature. However, this would also likely require a change where we factor out concrete types from the type signature at the parameter site. For example:

```
List(System.Collections.Generics.IEnumerable<'T>)

    where 'T is (string | null)

Initializes an instance of a [...]
```

#### Signatures in F# Interactive

To have parity with F# signatures (note: not tooltips), signatures printed in F# interactive will have nullability expression as if it were in code:

```
> let s: string | null = "hello";;

    val s : string | null = "hello"
```

#### F# scripting

F# scripts will now also give warnings when using the compiler that implements this feature. This also necessitates a new feature for F# scripting that allows you to opt-out of any nullability warnings. Example:

```fsharp
#nullability "false"

let s: string = null // OK, since we don't track nullability warnings
```

By default, F# scripts that use a newer compiler via F# interactive will understand nullability.

### Interaction model

The following details how an F# compiler with nullable reference types will behave when interacting with different kinds of components.

#### F# 5.0 consuming non-F# assemblies that do not have nullability

All non-F# assemblies built with a compiler that does not understand nullability are treated such that all of their reference types are nullable.

Example: a `.dll` coming from a NuGet package built with C# 7.1 will be interpreted as if all reference types are nullable, including constraints, generics, etc.

This means that warnings will be emitted when `null` is not properly accounted for. Additionally, when older non-F# components are eventually recompiled with C# 8.0, it will likely be an API-breaking change that will force new F# code to go back and do away with any redundant `null` checks.

#### F# 5.0 consuming F# assemblies that do not have nullability

All F# assemblies built with a previous F# compiler will be treated as such:

* All reference types that are _not_ declared in F# are nullable.
* All F#-declared reference types that have `null` as a proper value are treated as if they are nullable.
* All F#-declared reference types that do not have `null` as a proepr value are treated as non-nullable reference types.

#### Ignoring F# 5.0 assemblies that do have nullability

Users may want to progressively work through `null` warnings by treating a given assembly as "nullability obliviousness". To respect this, F# 5.0 will have to ignore any potentially unsafe dereference of `null` until sucha  time that "nullability obliviousness" is turned off for that assembly.

**Potential issue:** How are these types annotated in code? `string` or `string | null`, with the latter simply not triggering any warnings?

#### Older F# components consuming non-F# assemblies that do have nullability

Because the nullability attribute is only understood by F# 5.0, their presence has no impact on existing F# codebases. Nothing is different.

#### Older F# components consuming F# assemblies that do have nullability

F# components are no different than non-F# components when it comes to being consumed by an older F# codebase. The nullability attribute is simply ignored.

### Breaking changes and tunability

Non-null warnings are an obvious breaking change with existing code, and thus will be opt-in for existing projects. New projects will have non-null warnings be on by default.

Type inference will infer a reference type to be nullable in cases where it can be the result of an F# expression. This will generate warnings for existing code utilizing type inference. It will obviously also do so when dereferencing a reference type inferred to be nullable. Thus, warnings for nullability must be off by default for existing projects and on by default for new ones.

Additionally, adding nullable annotations to an API will be a breaking change for users who have opted into warnings when they upgrade a library to use the new API. This also warrants the ability to turn off warnings for specific assemblies.

Finally, F# code in particular is quite aggressive with respect to non-null today. It is expected that a lot of F# users would like these warnings to actually be errors, independently of turning all warnings into errors. This should also be possible.

In summary:

| Warning kind | Existing projects | New projects | Tun into an error? |
|--------------|-------------------|--------------|--------------------|
| Nullable warnings | Off by default | On by default | Yes |
| Non-nullable warnings | Off by default | On by default | Yes |
| Warnings from other assemblies | Off by default | On by default | Yes |

These kinds of warnings should be individually tunable on a per-project basis.

## Drawbacks
[drawbacks]: #drawbacks

### Complexity

Although it could be argued that this simplifies reference types by making the fact that they could be `null` explicit, it does give programmers another thing that must explicitly account for. Although it fits within the "spirit" of F# to make nullability of reference types explicit, it's arguable that F# programmers need to account for enough things already.

This is also a very complicated feature, with lots of edge cases, that has significantly reduced utility if any of the following are true:

* There is insufficient flow analysis such that F# programmers are asserting non-nullability all over the place just to avoid unnecessary warnings
* Existing, working code is infected without the programmer explicitly opting into this new behavior
* The rest of the .NET ecosystem simply does not adopt non-nullability, thus making adornments the new normal

Although we cannot control the third point, ensuring that the implementation is complete and up to spec with respect to what C# also does (while accounting for F#-isms) is the only way to do our part in helping the ecosystem produce less nulls.

Additionally, we now have the following possible things to account for with respect to accessing underlying data:

* Nullable reference types
* Option
* ValueOption
* Choice
* Result

These are a lot of ways to send data to various parts of the system, and there is a danger that people could use nullable reference types for more than just the boundary levels of their system.

### Unsoundness

This feature is inherently unsound. For starters, warnings don't prevent people from flowing `null` through their code like errors do. Compare this with the F# option type, which offers zero way to attempt to coerce `None` into a `Some x`.

More subtly, there is no way to extract nullability information with reflection today without a change in the .NET runtime. This means that it can be impossible to know, at least by reflection, if the type of something is actually nullable or non-nullable. This means that reflection-based libraries (such as JSON.NET) may be able to define a contract that they cannot fulfill; i.e., make an API's return type a `'T` when it could still be `null`.

Finally, the "handles null" attribute can be trivially abused, and it may be likely that the F# compiler could be "tricked" into thinking something will no longer be `null`.

All of this means that this feature is entrusting acting in good faith on the greater .NET ecosystem, rather than preventing acting in bad faith with compile-time guarantees.

## Alternatives
[alternatives]: #alternatives

The primary alternative is to simply not do this.

That said, it will become quite evident that this feature is necessary if F# is to continue advancing on the .NET platform. Despite originating as a C# feature, this is fundamentally a **platform-level shift** for .NET, and in a few years, will result in .NET looking very different than it is today.

### Syntax

#### Question mark nullability annotation

```fsharp
let ns: string? = "hello"

type C<'T?>() = class end
```

Issue: looks horrible with F# optional parameters

```fsharp
type C() = 
    member __.M(?x: string?) = "BLEGH"
```

#### Nullref<'T>

Have a wrapper type for any reference type `'T` that could be null.

Issues:

* Really ugly nesting is going to happen
* No syntax for this makes F# seen as "behind" C# for such a cornerstone feature of the .NET ecosystem

#### Option or ValueOption

Find a way to make this work with the existing Option type.

Issues:

* Massively breaking change for any F# code consuming existing reference types. In many cases, this break is so severe that entire routines would need to be rewritten.
* Massively breaking change for any C# code consuming F# option types. This simply breaks C# consumers no matter which way you swing it.

### Asserting non-nullability

**`!!` postfix operator?**

Why two `!` when one could suffice?

**`.get()` member?**

No idea if this could even work.

**Explicit cast required?**

Not possible with current plan of emitting warnings with casting.

**New keyword with a scope?**

Something like this may be considered:

```console
notnull { expr }
```

Where `notnull` returns the type of `expr`. But this may be viewed as too complicated when compared with a postfix operator, especially since a postfix operator is already a standard established by C#, Kotlin, TypeScript, and Swift.

**Not have it?**

This is untenable, unless we also implement [the ability to disable a warning just in one place within a file](https://github.com/fsharp/fslang-suggestions/issues/278). Otherwise, users will either:

* Have a nonzero number of warnings they cannot disable via a non-nullability assertion
* Ignore all warnings in a file

Both options will likely be considered unacceptable by F# programmers.

### Unresolved questions
[unresolved]: #unresolved-questions

#### Handling warnings

What about the ability to turn off nullability warnings when annotations are coming from a particular file? Or only have nullability warnings on a per-file basis? Answer: that would be weird.

#### Compatibility with existing F# nullability features

Today, classes declared in F# are non-null by default. To that end, F# has a way to express opt-in nullability with `[<AllowNullLiteral>]`. Attempting to assign `null` to a class without this attribute is a compile error:

```fsharp
type CNonNull() = class end

let mutable c = CNonNull()
c <- null // ERROR: 'CNonNull' does not have 'null' as a proper value.

[<AllowNullLiteral>]
type CNullable() = class end

let mutable c2 = CNullable()
c2 <- null // OK
```

This behavior is quite similar to nullable reference types, but is unfortunately a bit of a cleaver when all you need is a paring knife. It's arguable that the value of nullability for F# programmers is _ad-hoc_ in nature, which is something that is squarely accomplished with nullable reference types. Instead, `[<AllowNullLiteral>]` sort of "pollutes" things by making any instantiation a nullable instantiation.

**Recommendation**: Relax this restriction from an error to a warning in F# 5.0 only, and then treat any reference to types decorated with `[<AllowNullLiteral>]` as if they were nullable reference types. This is not a breaking change, but it does mean that F#-declared reference types are now a bit different once this feature is in place.

#### Compatibility with existing null constraint

The existing `null` constraint for generic types prevents programmers from parameterizing a type with an F# reference type that does not have `null` as a proper value (i.e., decorated with `[<AllowNullLiteral>]`).

The following class `D`:

```fsharp
type D<'T when 'T: null>() = class end
```

Is equivalent to this:

```csharp
[Serializable]
[CompilationMapping(SourceConstructFlags.ObjectType)]
public class D<T> where T : class
{
    public D()
        : this()
    {
    }
}
```

Which is fine in today's world where nullable reference types are not explicit. After all `class` requires `T` to be a reference type, which can implicitly be `null`.

However, this will be highly confusing in C# 8.0 as per the [C# 8.0 proposal](https://github.com/dotnet/csharplang/blob/master/proposals/nullable-reference-types.md#generics):

> The `class` constraint is **non-null**. We can consider whether `class?` should be a valid nullable constraint denoting "nullable reference type".

What this means is that F# syntax that declares a type as accepting only types that have `null` as a proper value as type parameters actually accepts _only_ non-nullable types! This behavior is the exact opposite of what programmers likely believe what this syntax does, and is not compatible with a world where nullable reference types exist.

Additionally, C# 8.0 will introduce the `class?` constraint. This is syntax sugar for an attribute that will decorate the class, informing consumers that `T` is a nullable reference type. So `class` and `class?` are distinctly different things in C# now.

**Recommendation:** Change this in F# 5.0 to emit as a `class` constraint but with the same attribute decorating the class that C# uses.

#### Compatibility with existing not struct constraint

The existing `not struct` constraint for generic types requires the parameterizing type to be a .NET reference type.

The following class `C`:

```fsharp
type C<'T when 'T : not struct>() = class end
```

Is equivalent to this:

```csharp
[Serializable]
[CompilationMapping(SourceConstructFlags.ObjectType)]
public class C2<T> where T : class
{
    public C2()
        : this()
    {
    }
}
```

Which is fine in today's world. However, the `class` constraint will now mean **non-null** reference type in C# 8.0:

> The `class` constraint is **non-null**. We can consider whether `class?` should be a valid nullable constraint denoting "nullable reference type".

Today in F# you cannot assign `null` as a proper value to `C` when it is constrained with `not struct` within F#. So it is already semantically similar to how things will work in C# 8.0. What this does mean is that C# 8.0 code that consumes this class will see `T` as a non-nullable reference type. This is consistent with the changes being made to constraints in C# 8.0.

**Recommendation:** Make no change for F# 5.0

#### Compatibility with Microsoft.FSharp.Core.DefaultValueAttribute

Today, the `[<DefaultValue>]` attribute is required on explicit fields in classes that have a primary constructor. These fields must support zero-initialization, which in the case of reference types, is `null`.

Consider the following code:

```fsharp
type C() =
    [<DefaultValue>]
    val mutable Whoops : string

printfn "%d" (C().Whoops.Length)
```

Today, this produces a `NullReferenceException` at runtime.

`Whoops` is annotated as a reference type, but it is actually a nullable reference type due to being decorated with the attribute. This means that we'll either have to:

* Do nothing and leave this as a confusing thing that emits a warning at the call site, but not at the declaration site
* Emit a warning saying that decoration with `[<DefaultValue>]` means that the type annotation is incorrect

Either way, we'll need to respect the `[<DefaultValue>]` attribute generating a `null` to remain compatible with existing code.

#### Unchecked.defaultOf<'T>

Today, `Unchecked.defaultof<'T>` will generate `null` when `'T` is a reference type. However, `'T` means non-null, and it's obvious that this will return `null` when parameterized with a .NET reference type such as `string`. We have some options:

* Keep the existing signature, despite being a bit confusing, and have it act as an unconstrained generic function that will return `null` for reference types that have `null` as a proper value
* Create a variant of this function that works in terms of nullable reference types, and somehow convey that you need to call this for reference types, and the existing function for non-reference types

The latter option is terrible, so the former is probably better.

#### Array.zeroCreate<'T>

The same issue for `Unchecked.defaultof<'T>` exists for `Array.zeroCreate<'T>`.

#### typeof<'T> and typedefof<'T>

We cannot actually guarantee that the underlying `System.Type` derived from `'T` is nullable or not. This is due to there being no mechanism in reflection today to understand the nullability attribute. This represents an inherent unsoundness of the nullability feature unless some approach for dealing with this is derived.

#### Format specifiers

Today, the following specifiers work with reference types:

* `%s`, for strings
* `%O`, which calls `ToString()`
* `%A`/`%+A`, for anything, using reflection to find values to print
* `%a` and `%t`, which work with `System.IO.TextWriter`

Making these work with non-nullable reference types would be a breaking change, so it's likely that the underlying type be the appropriate `T | null` for each specifier. For example, `%s` would work with `string | null`.

**Recommendation:** These now operate assuming incoming types are nullable reference types.

#### Emitting F# options types for C# consumption

F# options types emit `None` as `null`. For example, considering the following public F# API:

```fsharp
type C() =
    member __.M(doot) = if doot then Some "doot" else None
```

This is equivalent to the following C# 7.x code:

```csharp
[Serializable]
[CompilationMapping(SourceConstructFlags.ObjectType)]
public class C
{
    public C()
    {
        ((object)this)..ctor();
    }

    public FSharpOption<string> M(bool doot)
    {
        if (doot)
        {
            return FSharpOption<string>.Some("doot");
        }
        return null;
    }
}
```

This means that `FSharpOption` is a nullable reference type if it were to be consumed by C# 8.0. Indeed, there is definitely C# code out there that checks for `null` when consuming an F# option type to account for the `None` case. This is because there is no other way to safely extract the underlying value in C#!

**Recommendation:** ROLL INTO A BALL AND CRY