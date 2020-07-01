# F# RFC FS-1088 - (Generic struct type whose fields are all unmanaged types is unmanaged)

The design suggestion [Generic struct type whose fields are all unmanaged types is unmanaged](https://github.com/fsharp/fslang-suggestions/issues/692) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/692)
* [ ] Implementation: [In progress](https://github.com/dotnet/fsharp/pull/6064)

# Summary

[summary]: #summary

Allow generic structs to be `unmanaged` at construction if all fields are `unmanaged` types.

# Motivation

[motivation]: #motivation

* Make it easier to author low level interop code.
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
type Test<'T when 'T: unmanaged> =
    val element: 'T
        
let works = Test<int>()
let works2 = Test<MyStruct>()

// error FS0001: A generic construct requires that the type 'MyStructGeneric<int>' is an unmanaged type
let error = Test<MyStructGeneric<int>>()
```

Prior to this RFC, the example above will fail to compile with:

```less
let error = Test<MyStructGeneric<int>>()
  ------------^^^^^^^^^^^^^^^

stdin(6,13): error FS0001: A generic construct requires that the type 'Test<int>' is an unmanaged type
```

This proposal aims to eliminate this restriction by treating generic struct type `unmanaged` if all its fields are `unmanaged`.

## Definition

An unmanaged-type is any type that isn't a reference-type and contains no fields whose type is not an unmanaged-type.

In other words, an unmanaged-type is one of the following:

* `sbyte, byte, short, ushort, int, uint, long, ulong, char, float, double, decimal, or bool`.
* Any enum-type.
* Any pointer-type.
* Any generic user-defined struct-type that can be statically determined to be 'unmanaged' at construction.

## Implementation details

* Any existing generic struct type will be considered unmanaged when all of its fields are unmanaged types.
* `IsUnmanagedAttribute` should be emitted on type arguments with the `unmanaged` constraints (for interop).
* Treat type arguments with `IsUnmanagedAttribute` as if they have the `unmanaged` constraint (for interop).

## Supported types

* Generic struct types.
* Struct records.
* Anonymous struct records.
* Struct tuples.
  * Struct tuples are thread as unmanaged as long as all the elements are unmanaged;
* Struct unions.
  * Single-case unions types are treated as unmanaged as long as underlying type is unmanaged.
  * Multi-case unions are treated as unmanaged as long as all underlying types are unmanaged.

## Examples

The following examples are not possible now and will be once feature is implemented.

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

test (Container 1)
```

Tuples:

```fsharp
let x = struct(1,2,3);;

let test (x: 'T when 'T : unmanaged) = ()

test (x)
```

Records:

```fsharp
[<Struct>] type Point = { X: float; Y: float; Z: float; }

let test (x: 'T when 'T : unmanaged) = ()

let mypoint = { X = 1.0; Y = 1.0; Z = -1.0; }

test (mypoint)
test (struct {| A= 1 |})
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
