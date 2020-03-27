# F# RFC FS-1060 - Nullable Reference Types

The design suggestion [Add non-nullable instantiations of nullable types, and interop with proposed C# 8.0 non-nullable reference types](https://github.com/fsharp/fslang-suggestions/issues/577) has been marked "approved in principle". This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/577)
* [x] Discussion: [here](https://github.com/fsharp/fslang-design/issues/339)
* [ ] Implementation: [prototype](https://github.com/dotnet/fsharp/pull/6804)

## Summary
[summary]: #summary

This feature allows F# to interoperate with the future .NET ecosystem where reference types will be non-nullable by default.
This will be done by providing syntax and behavior that distinguishes between nullable and non-nullable reference types.

Conceptually, reference types can be thought of as having two forms:

* Normal reference types
* Nullable reference types

Warnings are emitted when reference types and nullable reference types are not used according with their intent, and these warnings are tunable as errors.

This will be done in lockstep with C# 8.0, which will also have this as a feature, and F# will interoperate with C# by respecting and emitting the same metadata that C# emits.

For the purposes of this document, the terms "reference type" and "non-nullable reference type" may be used interchangeably. They refer to the same thing.  We also use the terminology "a type does not have null as a normal value" to mean "non-nullable reference type". This is the terminology used in the existing F# language specification for the specific form of non-nullability being referred to here.

## Motivation
[motivation]: #motivation

Starting with C# 8.0, C# will distinguish between explicitly nullable reference types and non-nullable
reference types. The latter do not have `null` as a normal value. From C# 8.0 `string` will not have
`null` as a normal value, and attempting to make it `null` will result in a warning.  The type
`string?` will have null as a normal value.

F# has always taken the approach that F#-defined types do not have null as a normal value, while .NET-defined
types do. This is a longstanding tension between F# programming and the .NET ecosystem. Now that .NET assemblies
will contain information about non-nullability it is important that F# interpret and apply this information
to further improve the soundness of F# code.

The desired outcome over time is that fewer `NullReferenceException`s are produced
by code at runtime when interoperating with .NET libraries. This feature should
allow F# programmers to be more confident when interoperating with other
.NET components, by making it explicit when `null` could be a problem.

### Existing mitigations against `null` in F# code

F# has many existing mitigations against `null`. Indeed, much of the design of F# is based on the principle
that it should be difficult, unnecessary or impossible to explicitly use `null` values in normal F#
code. `null` values are also largely absent from related ML dialects such as OCaml and functional languages
such as Haskell.

Some of the existing mitigations include:

1. F#-declared reference types do not support `null` as a normal value

2. `let` and `let rec` bindings are always initialized prior to use.  This includes `let` bindings
   in classes, where checks are made to ensure that the uninitialized `this` is not leaked, e.g. via
   virtual dispatch to members in subclasses.
   
3. In F# code unboxing protects against null values. That is, the meaning of `unbox` in F# depends on
   whether the type has null as a normal value or not.  For example, `unbox<int list>(null)` raises an
   exception, where `unbox<string>(null)` does not. The same applies for type casts and pattern matching
   type tests.  This prevents unboxing being a backdoor route for creating `null`.
   
4. F# does some analysis related to whether types have default values or not, covered below.

5. The `null` literal may only be used with types that support it.
   
### Existing ways `null` can be introduced to an F# codebase

Despite the F# language design being biased very heavily against `null` values, they are a reality that F# programmers must eventually deal with. They can leak into a codebase in a few ways:

1. Interop with .NET types such as `string`, where `null` is a proper value, means that F# programmers have no way to tell if a value is `null`, requiring checks equivalent to what C# programmers need to do. As of C# 8, .NET metadata can contain information about the nullability of such a type, but F# does not understand this metadata. This can sometimes be a problem, because when you work with F# types that do not support `null` as a proper value, you need not check for `null`. Not getting into the habit of checking for `null` can result in unexpected exceptions at runtime when interoperating with .NET.

2. F# does allow F#-declared reference types to be  decorated with `[<AllowNullLiteral>]`. This attribute is useful for when an F# programmers frequently uses `null` for a particular type. However, this action "infects" any other place where the type might be used, forcing those places to also account for `null`.

3. The FSharp.Core library includes `Unchecked.defaultof<_>` and `Array.zeroCreate` functions which can generate nulls when the type specified is a reference type such as `string`.  Also F# LINQ queries (relatively rarely used) include custom operators such as `exactlyOneOrDefault`.

## Principles

The following principles guide the design of this feature.

1. On the surface, F# is "almost" as simple to use as it is today. In practice, it must feel simpler
   due to nullability of types like `string` being explicit rather than implicit.

2. The value for F# is primarily in flowing non-nullable reference types into F# code from .NET
   libraries and from F# code into .NET libraries.

3. Adding nullable annotations should not be part of routine F# programming.

4. Nullability annotations/information is carefully suppressed and simplified in tooling (tooltips, FSI) to avoid extra information overload.

5. F# users are typically concerned with this feature at the boundaries of their system so that they can flow non-null data into the inner parts of their system.

6. The feature produces warnings by default, with different classes of warnings (nullable, non-nullable) offering opt-in/opt-out mechanisms.

7. All existing F# projects compile with warnings turned off by default. Only new projects have warnings on by default. Compiling older F# code with newer F# code does not opt-in the older F# code.

8. F# non-nullness is reasonably "strong and trustworthy" today for F#-defined types and routine F# coding. This includes the explicit use of `option` types to represent the absence of information. No compile-time guarantees today will be "lowered" to account for this feature.

9. The F# compiler will strive to provide flow analysis such that common null-check scenarios guarantee null-safety when working with nullable reference types.

10. This feature is useful for F# in other contexts, such as interoperation with `string` in JavaScript via [Fable](http://fable.io/).

11. F# programmers may be able to start to phase out their use of `[<AllowNullLiteral>]`, `Unchecked.defaultof<_>`, and the `null` type constraint when they need ad-hoc nullability for reference types declared in F#.

12. Syntax for the feature feels "baked in" to the type system, and should not be horribly unpleasant to use.

## Detailed design
[design]: #detailed-design

### F# concept and syntax

Nullable reference types can conceptually be thought of a union between a reference type and `null`. This concept is lifted into syntax:

```
type = 
   ...
   reference-type
   reference-type?
```

Here

* `reference-type` is non-nullable, i.e does not support null as a normal value

* `reference-type?` supports null as a normal value

Some examples in F# code:

```fsharp
// Declared type at let-binding
let notAValue : string? = null

// Declared type at let-binding
let isAValue : string? = "hello world"

let isNotAValue2 : string = null // gives a nullability warning

let getLength (x: string?) = x.Length // gives a nullability warning since x is a nullable string

// Parameter to a function
let len (str: string?) =
    match str with
    | null -> -1
    | NonNull s -> s.Length  // binds a non-null result

// Parameter to a function
let len (str: string?) =
    let s = nullArgCheck "str" str // Returns a non-null string
    s.Length  // binds a non-null result

// Declared type at let-binding
let maybeAValue : string? = hopefullyGetAString()

// Array type signature
let f (arr: string?[]) = ()

// Generic code, note 'T must be constrained to be a reference type
let findOrNull (index: int) (list: 'T list) : 'T? when 'T : not struct =
    match List.tryItem index list with
    | Some item -> item
    | None -> null
```

> NOTE: One problem with the above syntax is that it makes nullable types a litle too easy to use, and there is concern that they may thus become mainstream in F# usage.  However alternative syntaxes are either unimplementable or heavyweight, e.g. `string | null` is not backwards compatible. However it is a design goal to support the use of non-null reference types by default, we want to encourage the programmer to **avoid** using nullable reference types unless absolutely necessary, so this issue may be reconsidered.

#### Partial alignment of nullable value types and nullable reference types

For uniformity, it is proposed that `System.Nullable<T>` on value types is now representable as follows:
```
type = 
   value-type
   value-type?
```

However `value-type?` and `reference-type?` are not uniformly represented in the underlying IL code:

* `value-type?` is represented as `System.Nullable<value-type>` in compiled IL code

* `reference-type?` is represented as `reference-type` in compiled IL code, plus a custom attribute

Hence `typeof<int?>` will report `System.Nullable<int>` while `typeof<string?>` will report `System.String`.

Because of this non-uniformity, naked use of `?` with variable types is not allowed. `'T?` is only allowed
if `'T` is either known to be `'T when 'T: not struct` or `'T when 'T : struct`

This creates a significant non-uniformity for code that seeks to be generic over any kind of nullable thing.
Code can only be generic over either nullable reference types or nullable value types.

> NOTE: This restriction could be lifted for F# inlined generic code 

> NOTE: This treatment of value types is NYI in the prototype

#### Pattern matching 

Nullness annotations are normally eliminated using pattern matching.

```fsharp
let len (str: string?) =
    match str with
    | null -> -1
    | NonNull s -> s.Length // OK - we know 's' is string
```

The `NonNull` pattern is defined in the FSharp.Core library.

In the previous code sample, `str` is of type `string?`, but `s` is now of type `string`. This is because we know that based on the `null` has been accounted for by the `null` pattern.

It is also proposed that the `NonNull` can be omitted for variable patterns preceded by an unqualified `null` pattern:
```fsharp
let len (str: string?) =
    match str with
    | null -> -1
    | s -> s.Length // OK - we know 's' is string
```

> NOTE: It is proposed that this only applies to pattern matching where the first pattern rule is exactly the `null` pattern.  It does _not_ (yet apply to column-based matching, e.g.:
> 
```fsharp
let len (str1: string?) (str2: string?) =
    match str1, str2 with
    | null, _ -> -1
    | _, null -> -1
    | s1, s2 -> s1.Length + s2.Length // in the prototype no extra information is gained about s1 and s2
```
> This is a tricky thing to implement as in the F# compiler type checking proceeds before pattern column analysis.
> For the code above, an explicit use of `NonNull` is required:
```fsharp
let len (str1: string?) (str2: string?) =
    match str1, str2 with
    | null, _ -> -1
    | _, null -> -1
    | NonNull s1, NonNull s2 -> s1.Length + s2.Length
```


#### Null ambivalence (obliviousness)

On import, an assembly that does not have `NonNullTypes` specified or an assembly scope where `NonNullTypes(false)` is active results in imported types being considered "null-ambivalent".  That is, there is no information about whether the types are nullabale or non-nullable.

Null-ambivalence is only directly expressible in F# code using `string __ambivalent` and the `__ambivalent` attribute is suppressed in routine coding.  However because null-ambivalence arises on import of legacy assemblies, it is an important additional concept in the F# typechecking rules.  For the purposes of this specification we consider the F# type system to have an additional case which we denote by `reference-type%`, indicating null-ambivalence.

```
type = 
    ...
    reference-type __ambivalent  (also written reference-type%)
```


#### Library additions

In the prototype, library functions are added to cover the basic operations associated with nullable reference types. These
corresponse to `value.HasValue`, `value.Value` and `Nullable(value)` for nullable value types.
The status of these library functions is TBD and the naming is quite hard to get right.

```fsharp
        /// Determines whether the given value is null.
        /// Equivalent to "not value.HasValue"
        val isNull: value:'T -> bool when 'T : not struct and 'T : null
        
        /// Asserts that the value is non-null. Raises a NullReferenceException when value is null, otherwise returns the value.
        val nonNull: value:'T? -> 'T when 'T : not struct

        /// Converts the value to a type that admits null as a normal value.
        val withNull: value:'T -> 'T? when 'T : not struct
        
        /// When used in a pattern asserts the value being matched is not null.
        val (|NonNull|) : value: 'T? -> 'T when 'T : not struct

        /// An active pattern which determines whether the given value is null.
        val (|Null|NotNull|) : value: 'T? -> Choice<unit, 'T>  when 'T : not struct
```

NOTE: `isNull` is already present in FSharp.Core and won't change signature. THe natural signature is:

        val isNull: value:'T? -> bool when 'T : not struct

however the above original signature is used.

        val isNull: value:'T -> bool when 'T : not struct and 'T : null
        
This matters only when `isNull` is instantiated explicitly, e.g.

    isNull< string? > "hello"
 
is required instead of 

    isNull< string> "hello"

In the prototype, parallel value-type versions of these functions are required because of the limiation for generic code mentioned above.

```fsharp
/// Get the null value for a nullable value type.
val inline nullV<'T>: 'T? when 'T : struct

/// Determines whether the given value is null.
/// Equivalent to "not value.HasValue"
val inline isNullV: value:'T? -> bool when 'T : struct

/// Asserts that the value is non-null. Raises a NullReferenceException when value is null, otherwise returns the value.
/// Equivalent to value.Value
val inline notNullV: value:'T? -> 'T when 'T : struct

/// Converts the value to a type that admits null as a normal value.
/// Equivalent to System.Nullable(value)
val inline withNullV: value:'T -> 'T? when 'T : struct

/// When used in a pattern asserts the value being matched is not null.
val (|NonNullV|) : value: 'T? -> 'T when 'T : not struct

/// An active pattern which determines whether the given value is null.
val (|NullV|NotNullV|) : value: 'T? -> Choice<unit, 'T>  when 'T : not struct
```

#### The `not null` constraint

Today, there are three relevant constraints in F# - `null`, `struct` and `not struct`:

```fsharp
    'T when 'T: null
    'T when 'T: struct
    'T when 'T: not struct
```
A new constraints is added:
```fsharp
    'T when 'T: not null
```

This constraint is checked as follows:

* An error is given if the constraint is instantiated with a type that uses null as a true value e.g. the `option` type or the `unit` type.

* A nullabliity warning is given if the constraint is instantiated with a nullable type or a type defined with `AllowNullLiteral(true)` attribute.

> NOTE: The F# 4.x `null` also constraint implies a `not struct` constraint. See [Unresolved questions](nullable-reference-types.md#unresolved-questions) for more. 

Using `'T?` adds the constraint that `'T` is non-null.  There are two exceptions to this in FSharp.Core:

* `withNull : 'T -> 'T?` doesn't add this constraint.  
* `Option.toObj : 'T option -> 'T?` doesn't add this constraint.  

In both cases this is because these can be used as "collapsing" operators, where instantiating with, for example, `string?` gives

```fsharp
withNull : string? -> string?
Option.toObj : string? option -> string?
```
Here any existing `null` in the input remains a `null` in the output.

### Type inference and checking

#### Type inference - F# type relations

* Nullable annotations on reference types are ignored when deciding type equivalence, though warnings are emitted for mismatches.

* Nullable annotations on reference types are ignored when deciding type subsumption, though warnings are emitted for mismatches. (TBD: give the exact specification in terms or expected and actual types)

* Nullable annotations on reference types are ignored when deciding method overload resolution, though warnings are emitted for mismatches in argument and return types once an overload is committed.

* Nullable annotations on reference types are ignored for abstract slot inference, though warnings are emitted for mismatches.

* Nullable annotations on reference types are ignored when checking for duplicate methods.

To re-iterate: nullable reference types are about distinguishing the implicit `null` from a reference type, but they are not a new _kind_ of reference type. They are still the same reference type and, despite warnings, can still compile when a nullability rule is violated.

#### Type inference - Constraint solving

Nullability is propagated through type inference.  Some examples for the solution of type equality constraints:

    Expected/Known    Actual
    string            string?  --> warning
    string?           string   --> no warning
    string?           T?       --> solved to T = string
    T?                U?       --> solved to T = U
    T                 U?       --> solved to T = U?

Some of these represent algorithmic type inference based on known type information of a kind that is used elsewhere by F#.  

#### Type inference - null literals and the nullness constraint

The use of the `null` literal and some other existing constructs places a nullness constraint on the
known type of the expression or pattern input.  For backwards
compatibility reasons, if the known type is a type variable `T` continues to place a declaration-site constraint on the type
variable `T : null`.  

For example, in existing F# code:
```fsharp
let f1 () = null
```
produces a generic function equivalent to
```fsharp
let f1<'T when 'T : null> () : 'T = null
```
However if an explicit type annotation is used the constraint is not placed on the declaration-site, e.g.
```fsharp
let f2 () : 'T? = null
```
results in:
```fsharp
val f2 : unit -> 'T? when 'T : not struct
```

This means type annotations may be required when the null literal is used. For example:

```fsharp
let example1 (str: string) : string? =
    match str with
    | "" -> null
    | _ -> str
```
checks correctly - the known type is `string?` and the actual type of `str` is `string?`.  However the non-type-annoted version
```fsharp
let example1 (str: string) =
    match str with
    | "" -> null
    | _ -> str
```
does not check, here the known type on the last line is `T with T : null` and the actual type is `string`.

Additional examples:

```fsharp
let xs1 = [ ""; ""; null ] // gives a nullablity warning

let xs2 : string? list = [ ""; ""; null ] 

// val f2 : string -> string?
let f2 s : string? = if s <> "" then "hello" else null
```


#### Type inference - nullness variables

Nullness variables represent uncertainty about whether constructs are null or non-null. They are unified as more information becomes available. 

Nullness variables are ignored for most purposes (just like nullability annotations) and are generalizable. They are committed to non-null in the absence of other information.

In the prototype, nullness variables are currently only introduced for type inference variables, to allow the inference variable to be solved independently of the nullness variable.  

TB: list cases where nullness variables are required and the rules for solving them

#### Type inference - Asserting non-nullability

To get around scenarios where compiler analysis cannot establish a non-null situation, a programmer can use the `nonNull` function to convert a nullable reference type to a non-nullable reference type:

```fsharp
let len (ns: string?) =
    let s = nonNull ns // unsafe, but lets you avoid a warning
    s.Length
```

This function has a signature of `nonNull: 'T? -> 'T`. It will throw a `NullReferenceException` at runtime if the input is `null`.

The function `Unchecked.nonNull` is similar but no actual check is made, in the underlying IL it is just the identity function.

#### Type inference - object arguments in member invocation

All object arguments are considered to be non-null:
```fsharp
let f (ns: string?) = ns.Length // Gives a nullability warning
```

#### Type inference - adding null as a possible value

In some rare cases it may be necessary to "add the possibility of null" to an existing value, to
make it compatible with a nullable type without warning.   We expect this to normally be done by
a type annoation, see above.  However for completeness it is also possible to do this by using the `withNull` operator.

```fsharp
val inline withNull : value:'T -> 'T? when 'T : not struct
```

> NOTE: This is akin to how `Some` makes a value compatible with an option type (though `Some` is needed more frequently).

#### Type inference - casting

A value may be safely cast to any type that is a variation of that type with nullness annotations changed.  However a warning
is given on such a cast.  For examples:

* A warning is given when casting from `'S[]` to `'T?[]` and from `'S?[]` to `'T[]`.

* A warning is given when casting from `C<'S>` to `C<'T?>` and from `C<'S?>` to `C<'T>`.

TBD: check these are implemented correctly, and check where there are cases where no warning is emittied.


#### Type inference - null assignment and passing

A warning is given if `null` is assigned to a non-null value or passed as a parameter where a non-null reference type is expected:

For example on mutation:
```fsharp
let mutable s: string = "hello"
s <- null // WARNING
```
Likewise on definition (contradicting a known type):
```fsharp
let s2: string = null // WARNING
```
Likewise on function application:
```fsharp
let f (s: string) = ()
f null // WARNING
```
Likewise on function return value:
```fsharp
let g (s: string) = if s.Length > 0 then s else null

let x: string = g "Hello" // WARNING
```

#### Type inference - F# collection expressions

Via the application of the above rules, a warning is given if `null` is used in an F# array/list/seq expression, where the type is already annotated to be non-nullable:

```fsharp
let xs: string[] = [| ""; ""; null |] // WARNING
let ys: string list = [ ""; ""; null ] // WARNING
let zs: seq<string> = seq { yield ""; yield ""; yield null } // WARNING
```

Annotations can be used to suppress these warnings:

```fsharp
let xs: string?[] = [| ""; ""; null |] 
let ys: string? list = [ ""; ""; null ] 
let zs: seq< string? > = seq { yield ""; yield ""; yield null } 
```

When explicit type annotations are not used (or other sources of known type information), rules for [Type Inference](FS-1060-nullable-reference-types.md#type-inference) are applied.

```fsharp
let xs = [| ""; ""; null |] // WARNING, inferred type string[]
let ys = [ ""; ""; null ] // WARNING, inferred type string list
let zs = seq { yield ""; yield ""; yield null } // WARNING inferred type seq<string>
```

#### Type inference - 'obj' type

Nullability warnings are never emitted for the `obj` type. 

> NOTE: The rationale for this is that reflection-based APIs are generally much simpler if nullability is not tracked For example consdier
>     static member PreComputeRecordReader : recordType:Type  * ?bindingFlags:BindingFlags -> (obj -> obj[])
> versus
>     static member PreComputeRecordReader : recordType:Type  * ?bindingFlags:BindingFlags -> (obj? -> obj?[])
> If type information has been thrown away by using `obj` then optional nullability information is better not tracked.

> NOTE: this means is not possible in F# to express "a non-nullable value of type obj" and have that checked.  If the "obj" type is being used it is assumed that nullability will be checked and handled by the user.

> TODO: the use of `obj?` should be disallowed.


#### Type checking - Default values and Microsoft.FSharp.Core.DefaultValueAttribute

In F# 4.5: 

* .NET reference types such as `string` are considered to have default values. 

In F# 5.0 (i.e. as of this RFC):

* a non-nullable type such as `string` is no longer considered to have a default value

* a nullable reference type such as `string?` is considered to have a default value

Nullability warnings are emitted whenever there is a difference between the cases of old and new-nullability rules with respect to default values.  Where possible, errors are produced where they were also produced in F# 4.5.

The F# analysis for default values also has these effects:

1. The `[<DefaultValue>]` attribute is required on explicit fields in classes that have a primary constructor and these fields must support default values.

2. determine if a type containing a field of this type itself has a default value

3. determine if the default struct constructor can be called, e.g. `DateTime()`.  For F#-defined types this can only be called if 
   the struct type has a default value

4. For struct types, to determine if the `'T when 'T : (new : unit -> 'T)` is satisfied.
 
For example, consider the following code:

```fsharp
[<Struct>]
type C =
    [<DefaultValue>]
    val mutable Whoops : string

printfn "%d" (C().Whoops.Length)
```

Today, this produces a `NullReferenceException` at runtime.  This will now give a nullability warning.


### Metadata

#### .NET Metadata on Import

> NOTE: import of nullness annotations from .NET assemblies is NYI in the prototype

F# will import .NET metadata that concerns to the following:

* If a type is a nullable reference type (**NOTE:** C# has no notion of this, so an assembly-level marker would be pending discussion)

* If an assembly has nullability as a concept (regardless of what it was compiled with)

* If a generic type constraint is nullable

* If a given method/function "handles null", such as `String.IsNullOrWhiteSpace`

C# 8.0 will emit and respect metadata for these scenarios, with a well-known set of names for each attribute. These attributes will also be what F# uses, though the behavior of F# in the face of these attributes is not necessarily identical to how C# 8.0 behaves in the face of these attributes.

> TBD: give an exact specification of the rules for importing .NET metadata.

#### Representation of nullable types in .NET Metadata

The following attribute will be used to represent a type that is marked as nullable:

```csharp
namespace System.Runtime.CompilerServices
{
    public sealed class NullableAttribute : Attribute
    {
        public NullableAttribute() { }
        public NullableAttribute(bool[] b) { }
    }
}
```

Note: the `bool[]` is used to represent nested types. So `ResizeArray<string?>` would be represented with `[| false; true |]`, where `ResizeArray` is non-null, but `string?` is `null`.

For example, the following code:

```csharp
public class D
{
    public void M(string? s) {}
}
```

Is the same as:

```csharp
public class D
{
    public void M([System.Runtime.CompilerServices.Nullable] string s)
    {
    }
}
```

Languages that are unaware of this attribute will see `M` as a method that takes in a `string`. That is, to be unaware of and failing to respect this attribute means that you will view incoming reference types exactly as before.

C# 8.0 will also [distinguish between nullable and non-nullable constraints](https://github.com/dotnet/roslyn/blob/features/NullableReferenceTypes/docs/features/nullable-reference-types.md#type-parameters). This is accomplished with the same attribute.

F# will be aware of and respect this attribute, treating reference types as nullable reference types if they have this attribute. If they do not, then those reference types will be treated as non-nullable.

F# will also emit this attribute when it produces a nullable reference type for other languages to consume.

#### Representing nullability assumptions in .NET metadata

On import, type references in .NET metadata are interpreted as with nullable, non-nullable or null-oblivious. 
This interpretation depends on the scope in which the reference occurs and the attributes governing that scope.

In particular C# 8.0 code will emit the `[NonNullTypes(true|false)]` attribute over various scopes:

```csharp
namespace System.Runtime.CompilerServices
{
    public sealed class NonNullTypesAttribute : Attribute
    {
        public NonNullTypesAttribute(bool enabled = true) { }
    }
}
```
From the C# spec on this:

> Unannotated reference types are non-nullable or [null-oblivious](FS-1060-nullable-reference-types.md#nullability-obliviousness)) depending on whether the containing scope includes `[NonNullTypes(true|false)]`.
>
> `[NonNullTypes(true|false)]` is not synthesized by the compiler. If the attribute is used explicitly in source, the type declaration must be provided explicitly to the compilation.

In other words, this attribute specifies a way to mark a class, constructor, delegate, enum, event, field, interface, method, module, property, or struct as having nullability expressible or not in a containing scope.

This attribute could potentially be used in F# to allow for opt-in/opt-out nullability at a fine-grained level.

On import F# will respect this attribute, with the `true` case indicating that the scope distinguishes between nullable and non-nullable reference types. If `false` is used, then nullability is not a concept and F# treats the reference types exactly as previous versions of the language would (i.e., not complain on unsafe dereference).

Note that both the `NonNullTypesAttribute` and `NonNullAttribute` can be abused. For example, if a method is annotated with `[NonNullTypes(true)]` and does not actually perform a `null` check, further code can still produce a `NullReferenceException` and not have a warning associated with it.

The following details how an F# compiler with nullable reference types will behave when interacting with different kinds of components.

#### F# 5.0 consuming non-F# assemblies that do not have nullability

All non-F# assemblies built with a compiler that does not understand nullability are treated such that all of their reference types are nullable.

Example: a `.dll` coming from a NuGet package built with C# 7.1 will be interpreted as if all reference types are nullable, including constraints, generics, etc.

This means that warnings will be emitted when `null` is not properly accounted for.

#### F# 5.0 consuming F# assemblies that do not have nullability

All F# assemblies built with a previous F# compiler will be treated as such:

* All reference types that are _not_ declared in F# are nullable.

* All F#-declared reference types that have `null` as a proper value are treated as if they are nullable.

* All F#-declared reference types that do not have `null` as a proper value are treated as non-nullable reference types.

#### Ignoring F# 5.0 assemblies that do have nullability

Users may want to progressively work through `null` warnings by ignoring any warnings coming from a given assembly. To respect this, F# 5.0 would have to ignore any potentially unsafe dereference of `null` until such a time that warnings are turned back on for types coming from that assembly. Although not an explicitly supported part of the design, we may consider this.

**Potential issue:** How are these types annotated in code? `string` or `string | null`, with the latter simply not triggering any warnings?

#### Older F# components consuming non-F# assemblies that do have nullability

Because the nullability attribute is only understood by F# 5.0, their presence has no impact on existing F# codebases. Nothing is different.

#### Older F# components consuming F# assemblies that do have nullability

F# components are no different than non-F# components when it comes to being consumed by an older F# codebase. The nullability attribute is simply ignored by older F# compilers.


#### Emit of `NonNullTypesAttribute` and `NonNullAttribute` attributes

> NOTE: emit of nullness annotations from .NET assemblies is NYI in the prototype

The F# compiler will synthesize/embed the `NonNullTypesAttribute` and `NonNullAttribute` upon usage. See a note from the C# team:

> FYI, we’re currently working to have the C# compiler embed this attribute whenever it is used in source. As a result, we do not plan to include that attribute into frameworks. That means the F# compiler will likely have to synthesize/embed this attribute upon usage as well.


### F# Metadata Blob Additions

The F# metadata blob gets a secondary blob added storing nullability information.  

The F# metadata exporter is backwards compatible: old compilers can import the metadata because they simply ignore the presence of the
secondary blob (they never enquire for it).

The F# metadata importer is backwards compatible: when importing old assemblies, this stream does not exist, and assumptions are made that all corresponding nullability constructs are ambivalent.

The details of this secondary blob are documented in TastPickle.fs in the prototype.

TBD: review the design of this secondary blob and consider future-proofing it so that we don't have to add third blobs and s on.


### FSharp.Core

TBD: this section needs considerable work.

The additions to FSharp.Core are listed above.

Older compilers must be able to reference newer versions of FSharp.Core. 

FSharp.Core will be selective annotated, applying nullability to things only when we actually intend `null` values to be accepted or come out of a function. Specifically, we can:

* Apply `[<NonNullTypes(true)>]` at the module level for the assembly

* Apply `[<Nullable>]` on every input type we wish to accept `null` values for

* Apply `[<Nullable>]` on every output type (this implies we may need to explicitly annotate a function)

* Apply `[<NotNullWhenTrue>]` and/or `[<NotNullWhenFalse>]` on public functions that test for `null`

* Apply `[<EnsuresNotNull>]` if applicable to anything that may throw on `null` (if that exists)

* Apply `[<AssertsTrue>]` and/or `[<AssertsFalse>]` if applicable to anything that may assert

This also means that some internal helper functions could be done away with. For example, [`String.emptyIfNull`](https://github.com/Microsoft/visualfsharp/blob/master/src/fsharp/FSharp.Core/string.fs#L16) is not publically consumable, and is effectively a way to enforce that incoming `string` types aren't `null`. This would make all `String.` functions now only accept non-nullable strings (should such a decision be made).

This will be a nontrivial effort, not unlike efforts to selectively annotate CoreFX libraries.

We need to take care that doing this does not affect binary compatibility in any way. It shouldn't, since the attributes being applied will just be ignored by an earlier compiler, but this still needs to be verified.


### Tooling considerations

To remain in line our first principle:

> On the surface, F# is "almost" as simple to use as it is today. In practice, it must feel simpler due to nullability of types like `string` being explicit rather than implicit.

We'll consider careful suppression of nullability in F# tooling so that the extra information doesn't appear overwhelming. For example, imagine the following signature in tooltips:

```fsharp
member Join : source2: (Expression<seq<string?>?>?) * key1:(Expression<Func<string?, T?>?>?)) * key2:(Expression<Func<string?, T?>?>?))
```

This signature is challenging enough already, but now nullability has made it significantly worse.

#### F# tooltips

In QuickInfo tooltips today, we reduce the length of generic signatures with a section below the signature. We can do a similar thing for saying if something is nullable:

```
member Foo : source: seq<'T> * predicate: ('T -> 'U -> bool) * item: 'U

    where 'T, 'U are nullable

Generic parameters:
'T is string
'U is int
```

Although this uses more vertical space in the tooltip, it reduces the length of the signature, which can already be difficult to parse for many members in .NET today.

In Signature Help tooltips today, we embed the concrete type for a generic type parameter in the tooltip. We may consider doing a similar thing as with QuickInfo, where nullability is expressed separately from the signature. However, this would also likely require a change where we factor out concrete types from the type signature at the parameter site. For example:

```
List(System.Collections.Generics.IEnumerable<'T>)

    where 'T is string?

Initializes an instance of a [...]
```

#### Signatures in F# Interactive

To have parity with F# signatures (note: not tooltips), signatures printed in F# interactive will have nullability expression as if it were in code:

```fsharp
> let s: string? = "hello";;

val s : string? = "hello"
```

#### F# scripting

F# scripts will now also give warnings when using the compiler that implements this feature. This also necessitates a new feature for F# scripting that allows you to opt-out of any nullability warnings. Example:

```fsharp
#nowarn "nullness"

let s: string = null // OK, since we don't track nullability warnings
```

By default, F# scripts that use a newer compiler via F# interactive will understand nullability.


#### Project configurability

There are a few concerns here described further in the [Interaction Model](FS-1060-nullable-reference-types.md#interaction-model). When working in an IDE such as visual studio, someone may wish to adopt this feature with any of the following configuration toggles:

1. Turn off nullability for an entire solution

2. Turn off nullability on a per-project basis

3. Turn off nullability for a given assembly (e.g., a package reference)

4. Turn off warnings for checking of nullable references

5. Turn off warnings for checking of non-nullable references

6. Make checking of nullable references an error, separately from `warnaserror`

7. Make checking of non-nullable references an error, separately from `warnaserror`

8. Make all nullability warnings an error, separately from `warnaserror`

This implies that there are toggles in the tooling to support this.

* The project properties page for F# projects will require toggles for 2, 4, 5, 6, 7, and 8.
* There may need to be a right-click gesture that you can use to turn off nullability for a referenced assembly.
* Turning it off globally is not something that we can control, so (1) is not possible unless there is a solution-level switch.


### Breaking changes and tunability

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
* The rest of the .NET ecosystem simply does not adopt non-nullability

Although we cannot control the third point, ensuring that the F# implementation is complete is the only way to do our part in helping the ecosystem produce less `null` values.

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

### Alternative: Do not do this

The primary alternative is to simply not do this.

That said, it will become quite evident that this feature is necessary if F# is to continue advancing on the .NET platform. Despite originating as a C# feature, this is fundamentally a **platform-level shift** for .NET, and in a few years, will result in .NET looking very different than it is today.

### Alternative: Overall approach to nullability

* Design a fully sound (apart from runtime casts/reflection, akin to F# units of measure) and fully checked non-nullness system for F# (which gives errors rather than warnings), then interoperate with C# (giving warnings around the edges for interop).

---------> Possibly, though there would potentially untractable compatibility concerns.

* Allow code to be generic w.r.t nullness. For example, so you can express, "if you give me a `null` in, I might give you a `null` back; if you give me a non-`null` in, I will give you a non-`null` back".

---------> Questions about usability arise, including if we do null obliviousness

#### Alternative: Use of `|` like an ad-hoc union type

An ideal syntax would be reminiscent of ad-hoc union types:


```fsharp
let ns: string | null = "hello"
```

However, this would be a breaking change, as these are all valid representations of code in F# today:

```fsharp
let f2 (_ : string | null) = 1

type X = A of string | B | C

type X = A of string | null | C
```

Finding a way to adopt `|` syntax without breaking the previously-mentioned samples (and untold other examples) may not be possible.


#### Alternative: Other union-like representations

A riff of the previously-mentioned alternative is `or`:


```fsharp
let ns: string or null = "hello"
```

As is `||`:

```fsharp
let ns: string || null = "hello"
```

However, both are well-understood to be used for boolean operations, so "overloading" their meaning might be confusing. This may be something we revisit.


#### Alternative: Nullref<'T> or Nullable<'T>

Have a wrapper type for any reference type `'T` that could be null.

Issues:

* Very ugly nesting would occur
* No syntax for this makes F# seen as "behind" C# for such a cornerstone feature of the .NET ecosystem

#### Alternative: Option or ValueOption

Find a way to make this work with the existing Option type.

Issues:

* Massively breaking change for any F# code consuming existing reference types. In many cases, this break is so severe that entire routines would need to be rewritten.
* Massively breaking change for any C# code consuming F# option types. This simply breaks C# consumers no matter which way you swing it.

### Alternative: Non-nullability Assertions

**`!` operator?**

```fsharp
let myIsNull item = isNull item

let len (str: string | null) =
    if myIsNull str then
        -1
    else
        str!.Length // OK: 'str' is asserted to be 'null'
```

Specialized syntax for this sort of thing is not really the F# way.

**`!!` postfix operator?**

Why two `!` when one could suffice?

**`.get()` member?**

No idea if this could even work. Probably not.

**Explicit cast required?**

Not possible with current plan of emitting warnings with casting.

**New keyword with a scope?**

Something like this may be considered:

```console
notnull { expr }
```

Where `notnull` returns the type of `expr`. But this may be viewed as too complicated when compared with a function postfix operator, especially since a postfix operator is already a standard established by C#, Kotlin, TypeScript, and Swift.

##### Alternative: Pattern matching with wildcard

```fsharp
let len (str: string?) =
    match str with
    | null -> -1
    | _ -> str.Length // OK - 'str' is string
```

A slight riff that is less common is treating `str` differently when in a wildcard pattern's scope. To reach that code path, `str` must indeed by non-null, so this could potentially be supported.

This would actually inconsistent with typing rules today, so this may not be supported. For example, the following is a compile error

```fsharp
let f (x: obj) =
    match x with
    | :? string -> x.Length // Error, as 'length' is not defined on obj
```

You must give it a different name to get the code to compile:

```fsharp
let f (x: obj) =
    match x with
    | :? string as s -> s.Length // OK
```

Breaking this rule for F# would be inconsistent with behavior that already exists.

TBD: we will consider offering a warning that suggests giving the type a new name. This would improve the existing error message today, so it's probably worth doing with this feature.

### Alternative: nested functions accessing outer scopes

```fsharp
let len (str: string?) =
    let doWork() =
        str.Length // WARNING: maybe too complicated? Doesn't feel natural to F#?

    match str with
    | null -> -1
    | _ -> doWork() // But after this line is written it's fine? Not natural to F# though.
```

Although `doWork()` is called only in a scope where `str` would be non-null, this may too complex to implement properly.

**Note:** C# supports the equivalent of this with local functions. TypeScript does not support this. There is no precedent for this kind of support to further motivate the work.

#### Alternative: boolean-based checks for null that lack an appropriate well-known attribute

No type-relevant information is gained via an arbitrary check using user-defined code. Consider the following:

```fsharp
let myIsNull item = isNull item

let len (str: string?) =
    if myIsNull str then
        -1
    else
        str.Length // WARNING: could be null
```

At a minimum an annotation for `myIsNull` is needed, otherwise a programmer will have to rewrite their code or assert non-nullability and risk a `NullReferenceException`.

#### Alternative: Respect C# methods marked as handling null

There will be several attributes C# 8.0 code can emit:

* `[NotNullWhenTrue]` - Indicates a method handles `null` if the result is `true`, e.g., a `TryGetValue` call.
* `[NotNullWhenFalse]` - Indicates a method handles `null` if the result is `false`, e.g., a `string.IsNullOrEmpty` call.
* `[EnsuresNotNull]` - Indicates that the program cannot continue if a value is `null`, for example, a `ThrowIfNull` call.
* `[AssertsTrue]` and `[AssertsFalse]` - Used in assertion cases where `null` is concerned.

These attributes can be used to aid in checking if `null` is properly accounted for in the body of a function or method. Common .NET methods and F#-specific functions can be annotated with these attributes. Respecting these attributes will be necessary to accurately and efficiently determine if `null` is properly accounted for before someone attempts to dereference a reference type.

It is worth noting that these attributes can be abused and accidentally misused by applying them to something that does not account for `null` somehow, which can result in a `NullReferenceException` despite the compiler not indicating that it could be possible. That makes them dangerous, and third-party authors will need to be careful in applying them to their code. However, they do enable a class of checking that would otherwise be too computationally complex to do.

#### Alternative: isNull and others using null-handling decoration

```fsharp
let len (str: string?) =
    if isNull str then
        -1
    else
        str.Length // OK
```

The `isNull` function is considered to be a good way to check for `null`. We can annotate it in FSharp.Core with `[<NotNullWhenTrue>]`.

Similarly, CoreFX will annotate methods like `String.IsNullOrEmpty` as handling `null`. This means that the most common of null-checking methods can be respected by the compiler.

#### Alternative: If check

```fsharp
let len (str: string?) =
    if str = null then
        -1
    else
        str.Length // OK - 'str' is a string
```

More generally, `value = null` or `value <> null`, where `value` is an F# expression that is a nullable reference type, is a pattern that will likely be expected to be respected. Although less common, `=` and `<>` are valid way to check for `null`, after all. Logically, we know for certain that `value` is either null (`=`) or non-null (`<>`).

#### Alternative: After null check within boolean expression

```fsharp
let len (str: string?) =
    if (str <> null && str.Length > 0) then // OK - `str` must be non-null
        str.Length // OK here too
    else
        -1
```

Note that the reverse (`str.Length > 0 && str <> null`) would give a warning, because we attempt to dereference before the `null` check. This would only hold with AND checks and if the value is immutable (thus cannot be assigned to in an expression). More generally, if the boolean expression to the left of the dereference involves a `x <> null` check, then the dereference is safe.



### Alternative: Do not do the feature

This is untenable, unless we also implement [the ability to disable a warning just in one place within a file](https://github.com/fsharp/fslang-suggestions/issues/278). Otherwise, users will either:

* Have a nonzero number of warnings they cannot disable via a non-nullability assertion
* Ignore all warnings in a file

Both options will likely be considered unacceptable by F# programmers.


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

#### Alternative - adjust the effect of the CLIMutable attribute w.r.t. nullability

Today, `CLIMutable` is typically used to decorate F# record types so that the labels can be "filled in" by .NET libraries that cannot normally work with immutable data. One such set of libraries are ORMs, which require the mutation to be able to do change tracking.

However, the following code is perfectly valid today and can produce `null`:

```fsharp
[<CLIMutable>]
type R = { StringVal: string }
```

It is very likely that `StringVal` can be given a `null` value. But with this feature, the type of `StringVal` is a non-nullable string! Similar to `[<DefaultValue>]`, this once valid code is now nonsense. This means we'll have to either:

* Do nothing and leave this slightly confusing signature as-is, warning at the call site when you attempt to dereference `StringVal`.
* Warn at the declaration site, saying that type annotation for `StringVal` needs to be a nullable reference type.

We will continue to respect the fact that `[<CLIMutable>]` can result in `null` values.

**Recommendation:** Make no change for F# 5.0. The types are interpreted as written in the code and the user must explicitly annotate with `string?` if the ORM may leave a `null` value in the construct.


### Notes

#### Some null

Today, you can write this perfectly valid F# expression:
```fsharp
let x = Some null
```
This continues to give type:
```fsharp
val x : 'a option when 'a : null
```
For example:
```fsharp
let xs: string option list = [ Some null; Some "a" ] // gives a warning
let xs2: string? option list = [ Some null; Some "a" ] // no warning
```

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

**Recommendation**: Relax the restriction where F#-declared reference types cannot have `null` as a proper value from an error to a warning. Conceptually, any reference to type decorated with `[<AllowNullLiteral>]` will be seen as equivalent to a nullable reference type. This is not a breaking change, but it does mean that F#-declared reference types are now a bit different once this feature is in place.

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

**TBD:** These imply a nullability inference variable to allow to work with either kind

### Interaction with `UseNullAsTrueValue`

F# option types use the obscure attribute `UseNullAsTrueValue` to ensure that `None` gets compiled as `null`.  This was a design choice made early in F# and has been problematic for many reasons, e.g. `None.ToString()` raises an exception, and baroque special compilation rules are needed for `opt.HasValue`.  However, this choice has been made and we must live with it.

In theory the `UseNullAsTrueValue` attribute can be used with other F#' option types subject to limitations, e.g. this error message:
```
1196,tcInvalidUseNullAsTrueValue,"The 'UseNullAsTrueValue' attribute flag may only be used with union types that have one nullary case and at least one non-nullary case"
```

IMPORTANT: Types that use `UseNullAsTrueValue` may *not* be made nullable, so `option<int> | null` is **not** allowed.

Additionally the semantics of type tests are adjusted slightly to account for the possibility that `null` is a legitimate value, e.g. so that `match None with :? int option -> true | _ -> false` returns `true`.  Here `None` is represented as `null`. This is done through helpers `TypeTestGeneric` and `TypeTestFast`. We should consider whether this needs documenting or adjusting in the same way as `UnboxGeneric` and `UnboxFast`, though on first glance I don't believe it does.

In more detail, F# options types emit `None` as `null`. For example, considering the following public F# API:

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

**Recommendation:** discuss further with the C# team to see if something can be done about this.

### Meaning of `unbox`

Here's something tricky.  For an F# type C consider

1. `unbox<C>(s)`

2. `unbox<string>(s)`

3. `unbox<C?>(s)`

4. `unbox<string?>(s)`

There are two unbox helpers - [`UnboxGeneric`](https://github.com/Microsoft/visualfsharp/blob/92247b886e4c3f8e637948de84b6d10f97b2b894/src/fsharp/FSharp.Core/prim-types.fs#L613) and [`UnboxFast`](https://github.com/Microsoft/visualfsharp/blob/92247b886e4c3f8e637948de84b6d10f97b2b894/src/fsharp/FSharp.Core/prim-types.fs#L620). Normally `unbox(s)` just gets inlined to become `UnboxGeneric`, e.g. this is what (1) becomes.  However the F# optimizer converts calls to `UnboxFast` for (2), see [here](https://github.com/Microsoft/visualfsharp/blob/99c667b0ee24f18775d4250a909ee5fdb58e2fae/src/fsharp/Optimizer.fs#L2576)

`UnboxGeneric` guards against the input being `null` and refuses to unbox a `null` value to `C` for example.   It uses a runtime lookup on typeof<T> to do this

That is, the meaning of `unbox` in F# depends on whether the type carries a null value or not.  If the target type *doesn't* carry null and is not a value type (i.e. is an F# reference type), the *slower* helper is used to do an early guard against a null value leaking in.  If the target type *does* carry null, the *fast* helper is used (since null can leak through ok).  For generic code the slow helper is used

The problem of course is that once (3) is allowed we have an issue - using the slow helper is now no longer correct and will raise an exception, i.e. `unbox<C?>(null)` will raise an exception.

