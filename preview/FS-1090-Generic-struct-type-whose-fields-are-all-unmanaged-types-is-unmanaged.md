# F# RFC FS-1088 - (Generic struct type whose fields are all unmanaged types is unmanaged)

The design suggestion [Generic struct type whose fields are all unmanaged types is unmanaged](https://github.com/fsharp/fslang-suggestions/issues/692) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/692)
* [ ] Implementation: [In progress](https://github.com/dotnet/fsharp/pull/12154)

# Summary

[summary]: #summary

Allow generic structs to be `unmanaged` at construction if all fields are `unmanaged` types.

# Motivation

[motivation]: #motivation

* Make it easier to author low-level code.
* Improve interoperability with C#.

# Detailed design

[design]: #detailed-design

Currently, it is not possible to define a generic struct with the `unmanaged` constraint and substitute the type with another generic struct, even if its type is also constrained to be `unmanaged`.

Consider the following code:

```fsharp
[<Struct>]
type MyStruct(x: int, y: int) =
    member _.X = x
    member _.Y = y

[<Struct>]
type MyStructGeneric<'T when 'T: unmanaged>(x: 'T, y: 'T) =
    member _.X = x
    member _.Y = y

[<Struct>]
type MyStructGenericWithNoConstraint<'T>(x: 'T, y: 'T) =
    member _.X = x
    member _.Y = y

[<Struct>]
type Test<'T when 'T: unmanaged> =
    val element: 'T

let works = Test<int>()
let works2 = Test<MyStruct>()

// error FS0001: A generic construct requires that the type 'MyStructGeneric<int>' is an unmanaged type
let error = Test<MyStructGeneric<int>>()

// error FS0001: A generic construct requires that the type 'MyStructGenericWithNoConstraint<int>' is an unmanaged type
let error = Test<MyStructGenericWithNoConstraint<int>>()
```

Prior to this RFC, the example above will fail to compile with:

```less
let error = Test<MyStructGeneric<int>>()
  ------------^^^^^^^^^^^^^^^

stdin(6,13): error FS0001: A generic construct requires that the type 'MyStructGeneric<int>' is an unmanaged type
```

This proposal aims to resolve this inconsistency by treating any generic struct type as `unmanaged` if all its fields are unmanaged.

For example, consider the following code:

```fsharp
[<Struct>]
type MyStructGeneric<'T when 'T: unmanaged>(x: 'T, y: 'T) =
    member _.X = x
    member _.Y = y

[<Struct>]
type MyStructGenericWithNoConstraint<'T>(x: 'T, y: 'T) =
    member _.X = x
    member _.Y = y
```

Instances of both `MyStructGeneric<'T>` and `MyStructGenericWithNoConstraint<'T>` will be treated as unmanaged type as long as `'T` is being unmanaged.

In other words, any generic struct type, with all of its fielads are known to be unmanaged, can be considered unmanaged, _with_ or _without_ the `unmanaged` constraint on type parameter(s)

## Adjusted definition of an unmanaged type

__The existing definition of an unmanaged type is any type that isn't a reference-type and contains no fields whose type is not an unmanaged type:__

* `sbyte, byte, short, ushort, int, uint, long, ulong, char, float, double, decimal, or bool`.
* Any enum-type.
* Any pointer-type.
* Any non-generic user-defined struct type that contains fields of unmanaged-types only.

__We adjust this definition by modifying the last section:__

* Any user-defined struct type that can be statically determined to be 'unmanaged' at construction.

Examples:

```fsharp
// Always unmanaged (forced by constraint).
[<Struct>]
type MyStructGeneric<'T when 'T: unmanaged>(x: 'T, y: 'T) =
    member _.X = x
    member _.Y = y

// Can be considered unmanaged, as long as 'T is unmanaged.
[<Struct>]
type MyStructGenericWithNoConstraint<'T>(x: 'T, y: 'T) =
    member _.X = x
    member _.Y = y

// Can be considered unmanaged, as long as 'T is unmanaged.
// Note, that despite constructor having managed argument (obj), it is not used as part of the backing field.
[<Struct>]
type MyStructGenericWithUnusedUnmanagedParameter<'T>(x: 'T, y: 'T, z: obj) =
    member _.X = x
    member _.Y = y

// Not unmanaged, since it has a field with a managed type.
[<Struct>]
type MyStructGenericWithUnmanagedField<'T>(x: 'T, y: 'T, z: obj) =
    member _.X = x
    member _.Y = y
    member _.Z = z
```

## Implementation details

* Any existing generic struct type will be considered unmanaged when all of its fields are unmanaged types.
* `IsUnmanagedAttribute` should be emitted on type arguments with the `unmanaged` constraints (for interop).
* Treat type arguments with `IsUnmanagedAttribute` as if they have the `unmanaged` constraint (for interop).
* [`UnmanagedType`](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.unmanagedtype?view=netcore-3.1) modreq should be emitted on type argument (for interop).
* An `unmanaged` constraint cannot be used together with `not struct` constraint (not the case right now).

## Supported types

* Generic struct types whose fields are unmanaged
* Struct records with unmanaged fields
* Anonymous struct records with unmanaged fields
* Struct tuples with unmanaged values
* Struct unions (single and multi-case) whose fields are all unmanaged

## Examples

The following examples are now possible with this feature:

Basic examples:

```fsharp
[<Struct>]
type S<'T, 'U> =

    [<DefaultValue(false)>] val X : 'T

let test (x: 'T when 'T : unmanaged) = ()

let passing () =
    test (S<float, int>())

let passing2 () =
    test (S<float, obj>()) // passes as the 'obj' type arg is not used as part of the backing field of a struct

let passing3<'T when 'T : unmanaged> () =
    test (S<'T, obj>())

let not_passing () =
    test (S<obj, int>())

let not_passing2<'T> () =
    test (S<'T, obj>())
```

Constructor:

```fsharp
[<Struct>]
type S<'T> =

    val X : 'T

    new (x) = { X = x }

let test (x: 'T when 'T : unmanaged) = ()

let passing () =
    test (S(1))

let not_passing () =
    test (S(obj()))
```

Nested generics:

```fsharp
[<Struct>]
type W<'T> = { x: 'T }

[<Struct>]
type S<'T> = { x: W<'T> }

let test (x: 'T when 'T : unmanaged) = ()

let passing () =
    test Unchecked.defaultof<S<int>>

let not_passing () =
    test Unchecked.defaultof<S<obj>>
```

Union types:

```fsharp
[<Struct>]
type Container<'a> = Container of 'a

let test (x: 'T when 'T : unmanaged) = ()

let passing () = test (Container 1)

let not_passing () = test (Container "string")
```

Tuples:

```fsharp
let x = struct(1,2,3)

let y = struct("s", "t", "r", "i", "n", "g")

let z = struct(1, 2, 3, "s", "t", "r")

let test (x: 'T when 'T : unmanaged) = ()

let passing () = test (x)

let not_passing () = test (y)

let not_passing2 () = test (z)
```

Records:

```fsharp
[<Struct>]
type Point = { X: float; Y: float; Z: float; }

[<Struct>]
type Person = { Name: string; Age: int; }

let test (x: 'T when 'T : unmanaged) = ()

let mypoint = { X = 1.0; Y = 1.0; Z = -1.0; }

let passing () = test (mypoint)

let passing2 () = test (struct {| A= 1 |})

let person = { Name = "Joe"; Age = 42 }

let not_passing () = test (person)

let not_passing2 () = test (struct {| S = "str" |})
```

## Documentation

'Unmanaged Constraint' part of [F# constraints documentation](https://docs.microsoft.com/en-us/dotnet/fsharp/language-reference/generics/constraints) should be updated to reflect new supported cases.

# Drawbacks
[drawbacks]: #drawbacks

The major drawback of the feature is that it serves a relatively small number of developers, since most of the F# projects are not focused on a low-level programming.

# Alternatives
[alternatives]: #alternatives

There are currently no alternatives or workarounds for this.

# Unresolved questions
[unresolved]: #unresolved-questions

None.
