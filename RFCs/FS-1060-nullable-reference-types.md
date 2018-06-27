# F# RFC FS-1060 - Nullable Reference Types

The design suggestion [Add non-nullable instantiations of nullable types, and interop with proposed C# 8.0 non-nullable reference types](https://github.com/fsharp/fslang-suggestions/issues/577) has been marked "approved in principle". This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [ ] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/577)
* [ ] Details: [TODO](https://github.com/fsharp/fslang-design/issues/FILL-ME-IN)
* [ ] Implementation: [TODO](https://github.com/Microsoft/visualfsharp/pull/FILL-ME-IN)

## Summary
[summary]: #summary

The main goal of this feature is to address the pains of dealing with implicitly nullable reference types by providing syntax and behavior that distinguishes between nullable and non-nullable reference types. This will be done in lockstep with C# 8.0, which will also have this as a feature, and F# will interoperate with C# by using and emitting the same metadata adornment that C# emits emit.

Conceptually, reference types can be thought of as having two forms:

* Normal reference types
* Nullable reference types

These will be distinguished via syntax, type signatures, and tools. Additionally, there will be flow analysis to allow you to determine when a nullable reference type is non-null and can be "viewed" as a reference type.

Warnings are emitted when reference and nullable reference types are not used according with their intent, and these warnings are tunable as errors.

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
7. All existing F# projects compile with warnings turned off by default. Only new projects have warnings on by default. Compiling older F# code with newer F# code may opt-in the older F# code.
8. F# non-nullness is reasonably "strong and trustworthy" today for F#-defined types and routine F# coding. This includes the explicit use of `option` types to represent the absence of information. No compile-time guarantees today will be "lowered" to account for this feature.
9. The F# compiler will strive to provide flow analysis such that common null-check scenarios guarantee null-safety when working with nullable reference types.
10. This feature is useful for F# in other contexts, such as interoperation with `string` in JavaScript via [Fable](http://fable.io/).
11. F# programmers can start to phase out their use of `[<AllowNullLiteral>]` and the `null` type constraint when they need ad-hoc nullability for reference types declared in F#.
12. Syntax for the feature feels "baked in" to the type system, and should not be horribly unpleasant to use.

## Detailed design
[design]: #detailed-design

## Metadata

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

## F# concept and syntax

Nullable reference types can conceptually be thought of a union between a reference type and `null`. For example, `string` in F# 4.5 or C# 7.3 is implicitly either a `string` or `null`. More concretely, they are a special case of [Erased type-tagged anonymous union types](https://github.com/fsharp/fslang-suggestions/issues/538).

This concept is lifted into syntax:

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

Because it is a design goal to flow **non-null** reference types, and **avoid** flowing nullable reference types unless absolutely necessary, it is considered a feature that the syntax can get a bit unweildy.

### Nullable reference type declarations in F\#

There will be no way to declare an F# type as follows:

```fsharp
type (NotSupportedAtAll | null)() = class end
```

## Checking of nullable references

There are a few common ways that F# programmers check for null today. Unless explicitly mentioned as unsupported, support for flow analysis is under consideration for certain null-checking patterns.

### Likely supported: Pattern matching with alias

```fsharp
let len (str: string | null) =
    match str with
    | null -> -1
    | s -> s.Length // OK - we know 's' is string
```

This is by far the most common pattern in F# programming. In the previous code sample, `str` is of type `string | null`, but `s` is now of type `string`. This is because we know that based on the `null` has been accounted for by the `null` pattern.

### Possibly supported: Pattern matching with wildcard

```fsharp
let len (str: string | null) =
    match str with
    | null -> -1
    | _ -> str.Length // OK - 'str' is string
```

A slight riff that is less common is treating `str` differently when in a wildcard pattern's scope. To reach that code path, `str` must indeed by non-null, so this could potentially be supported.

### Possibly supported: If check

```fsharp
let len (str: string | null) =
    if str = null then
        -1
    else
        str.Length // OK - 'str' is a string
```

Although less common, this is a valid way to check for `null` and could be enabled by the compiler. We know for certain that `str` is a `string` in the `else` branch. The inverse check is also true.

### Possibly supported: After null check within boolean expression

```fsharp
let len (str: string | null) =
    if (str <> null && str.Length > 0) then // OK - `str` must be null
        str.Length // OK here too
    else
        -1
```

Note that the reverse (`str.Length > 0 && str <> null`) would give a warning, because we attempt to dereference before the `null` check. This would only hold with AND checks. More generally, if the boolean expression to the left of the dereference involves a `x <> null` check, then the dereference is safe.

### Likely unsupported: isNull

```fsharp
let len (str: string | null) =
    if isNull str then
        -1
    else
        str.Length // WARNING: could be null
```

The `isNull` function is considered to be a good way to check for `null`. Unfortunately, because it is a function that returns `bool`, the logic needed to support this is deeply foreign to F# and troublesome to implement. For example, consider the following:

```fsharp
let len (str: string | null) =
    let someCondition = getCondition()
    if isNull str && someCondition then
        -1
    else
        str.Length // WARNING: could be null
```

There is no telling what `someCondition` would evaluate to, so we cannot guarantee that `str` will be `string` in the `else` clause.

### Likely unsupported: boolean-based checks for null

Generally speaking, beyond highly-specialized cases, we cannot guarantee non-nullability that is checked via a boolean. Consider the following:

```fsharp
let myIsNull item = isNull item

let len (str: string | null) =
    if myIsNull str then
        -1
    else
        str.Length // WARNING: could be null
```

Although this simple example could potentially work, it could quickly get out of hand and complicate the compiler. Also, other languages that support nullable reference types (C# 8.0, Scala, Kotlin, Swift, TypeScript) do not do this. Instead, non-nullability is asserted when the programmer knows something will not be `null`.

### Likely unsupported: nested functions accessing outer scopes

```fsharp
let len (str: string | null) =
    let doWork() =
        str.Length // WARNING: maybe too complicated?

    match str with
    | null -> -1
    | _ -> doWork()
```

Although `doWork()` is called in a place where `str` would be non-null, this may too complex to implement properly.

**Note:** C# supports the equivalent of this with local functions.

### Asserting non-nullability

To get around scenarios where flow analysis cannot establish a non-null situation, a programmer can use `!` to assert non-nullability:

```fsharp
let len (str: string | null) =
    if isNull str then
        -1
    else
        str!.Length // OK: 'str' is asserted to be 'null'
```

This will not generate a warning, but it is unsafe and can be the cause of a `NullReferenceException` if the reference was indeed `null` under some condition.

### Warnings

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

## Checking of non-null references

### Null assignment and passing

A warning is given if `null` is assigned to a non-null value or passed as a parameter where a non-null reference type is expected:

```fsharp
let mutable s: string = "hello"
s <- null // WARNING

let s2: string = null // WARNING

let f (s: string) = ()
f null // WARNING
```

### Nullable parameterization

A warning is given if a nullable reference type `P` is used to parameterize another type `C` that  does not accept nullable reference types:

```fsharp
type CNullParam<'T>() = class end

let c1 = C<string | null> // WARNING
```

Today, it is already a compile error if all fields in an F# class are not initialized by constructor calls. This behavior is unchanged. See [Compatibility with Microsoft.FSharp.Core.DefaultValueAttribute](FS-1060-nullable-reference-types.md#compatibility-with-microsoft.fsharp.core.defaultValueAttribute) for considerations about code that uses `[<DefaultValue>]` with reference types today, which produces `null`.

### F# Collection initialization

A warning is given if `null` is used in an F# array/list/seq expression, where the type is already annotated to be non-nullable:

```fsharp
let xs: string[] = [| ""; ""; null |] // WARNING
let ys: string list = [ ""; ""; null ] // WARNING
let zs: seq<string> = seq { yield ""; yield ""; yield null } // WARNING
```

### Unchecked.defaultOf<'T>

Today, `Unchecked.defaultof<'T>` will generate `null` when `'T` is a reference type. Given this, it is reasonable to do either of the following:

* The return type of `Unchecked.defaultof<'T>` is now `'T | null`. Calling `Unchecked.defaultof<'T | null>` produces the same output.
* Give a warning when `Unchecked.defaultof<'T>` is called when `'T` is a non-nullable reference type, indicating that you must call `Unchecked.defaultof<'T | null>`.

### Array.zeroCreate<'T>

Today, `Array.zeroCreate<'T>` will generate `null`s when `'T` is a reference type. Given this, it is reasonable to do either of the following:

* The return type of `Array.zeroCreate<'T>` is now `('T | null) []`. Calling `Array.zeroCreate<'T | null>` produces the same output.
* Give a warning when `Array.zeroCreate<'T>` is called where `'T` is a non-nullable reference type, indicating that you must call `Array.zeroCreate<'T | null>`.

## Type inference

### F# Collection initialization

Using `null` will "infect"

## Tooling considerations

### F# tooltips

TODO

### Signatures in FSI

TODO

### F# scripting

TODO

## Breaking changes

There are two forms of breaking changes with this feature:

* Non-null warnings (i.e., not checking for `null`) on a `string | null` type
* Nullable warnings

TODO

## Drawbacks
[drawbacks]: #drawbacks

Why should we *not* do this?

## Alternatives
[alternatives]: #alternatives

What other designs have been considered? What is the impact of not doing this?

### Syntax

#### Question mark

```fsharp
let ns: string? = "hello"

type C<'T?>() = class end
```

Issue: looks horrible with F# optional parameters

```fsharp
type C() = 
    memeber __.M(?x: string?) = "BLEGH"
```

But it is a lot easier to implement since it doesn't require ad-hoc, structurally-typed unions.

#### Nullable

Find a way to make semantics with the existing `System.Nullable<'T>` type.

Issues:

* Back-compat issue?
* Really ugly nesting
* No syntax for this makes F# be "behind" C# for such a cornerstone thing

#### Option

Find a way to make this work with the existing Option type.

Issues:

* Back compat issue?
* `None` already emits `null`, but it's a distinct case and not erased
* References and Nullable references are fundamentally the same thing, just one has an assembly-level attribute

### Warnings

TODO

### Assering non-nullability

TODO

### Unresolved questions
[unresolved]: #unresolved-questions

## Compatibility with existing F# nullability features

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

### Compatibility with existing null constraint

The existing `null` constraint for generic types prevents programmers from parameterizing a type with an F# reference type that does not have `null` as a proper value (i.e., decorated with `[<AllowNullLiteral>]`).

The following class `D`:

```fsharp
type D<'T when 'T: null>() = class end
```

will compile into this:

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

## Compatibility with Microsoft.FSharp.Core.DefaultValueAttribute

Today, the `[<DefaultValue>]` attribute is required on explicit fields in classes that have a primary constructor. These fields must support zero-initialization, which in the case of reference types, is `null`.

Consider the following code:

```fsharp
type C() =
    [<DefaultValue>]
    val mutable Whoops : string
    
printfn "%d" (C().Whoops.Length)
```

Today, this produces a `NullReferenceException` at runtime. This means that `Whoops` is actually a nullable reference type, but it will be annotated as if it were a non-nullable reference type. It is the presence of the attribute that changes this, which we'll have to rationalize somehow.