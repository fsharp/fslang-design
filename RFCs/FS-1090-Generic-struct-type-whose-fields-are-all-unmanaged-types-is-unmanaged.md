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

* Feature parity with C#.
* Improve interoperability with C#.

# Detailed design

[design]: #detailed-design

Currently, it is not possible to have an `unmanaged` constraint on generic struct types.

Consider following code:

```fsharp
[<Struct>]
type Test<'T when 'T: unmanaged> =
        val element: 'T

let works = Test<int>()
let error = Test<Test<int>>()

```

Prior to this RFC, the example above will fail to compile with:

```less
stdin(27,1): error FS0001: A generic construct requires that the type 'Test<int>' is an `unmanaged` type
```

This proposal aims to eliminate this restriction by treating generic struct type `unmanaged` if all its fields are `unmanaged`.

The

## Examples

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

# Drawbacks
[drawbacks]: #drawbacks

No drawbacks.

# Alternatives
[alternatives]: #alternatives

There are currently no alternatives or workarounds for this.

# Unresolved questions
[unresolved]: #unresolved-questions

None.
