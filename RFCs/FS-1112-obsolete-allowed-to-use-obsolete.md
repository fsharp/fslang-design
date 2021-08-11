# F# RFC FS-1112 - Obsolete allowed to use Obsolete

<!--The design suggestion [Obsolete allowed to use Obsolete](https://github.com/fsharp/fslang-suggestions/issues/1055) has been marked "approved in principle".
-->

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1055)
- [x] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [ ] Design Review Meeting(s) with @dsyme and others invitees
- [Discussion](https://github.com/fsharp/fslang-design/discussions/618)

# Summary

The general idea is to allow types and functions marked with `System.ObsoleteAttribute` to use other `Obsolete`-marked members without
having the FS0044 warning emitted.

No warning is emitted when `B` is obsolete and `A` addresses `B` *if* `A` is an obsolete function or type or module, or when `A` is a local function and its enclosing function or type or module is obsolete, or when `A` is a member of a type and the type or its enclosing module is obsolete, or when `A` is a type and its enclosing module is obsolete.

In other words, an obsolete member can be addressed if the addressing happens somewhere down in the hierarchy from an obsolete member. If no obsolete member exists up in the hierarchy, the old warning is emitted.

# Motivation

Assume we completely redesigned API of some library. Then, to keep the backward compatibility we keep the old members and mark
them with `Obsolete`. But if there is no new equivalent functions to old functions, we have to use obsolete functions
and members in that old obsolete API. Since the member is already marked as `Obsolete`, and hence, unsupported, there is no reason
to warn the developer of the function/type about using obsolete members in it.

# Detailed design

#### 1. Functions marked as Obsolete can use functions marked as Obsolete:

```fs
[<Obsolete>]
let add a b = a + b

[<Obsolete>]
let func a b =
    add a b    // no warning
```

#### 2. Types marked as Obsolete can use functions marked as Obsolete

```fs
[<Obsolete>]
let add a b = a + b

[<Obsolete>]
type Aaa () =
    static member someMember a b =
        add a b  // no warning
```

#### 3. Functions marked as Obsolete can use types marked as Obsolete

```fs
[<Obsolete>]
type Aaa () =
    static member someMember a b =
        a + b

[<Obsolete>]
let add a b =
    Aaa.someMember a b    // no warning
```

#### 4. Any function can use an obsolete function or type if its enclosing function or type is Obsolete

Using obsolete type in a local function:
```fs
[<Obsolete>]
type Aaa () =
    static member someMember a b =
        a + b

[<Obsolete>]
let outerFunc a b =
    let add a b =
        Aaa.someMember a b    // no warning because the outer function is obsolete
    add a b
```

Using obsolete function in a local function:
```fs
[<Obsolete>]
let someAdd a b = a + b

[<Obsolete>]
let outerFunc a b =
    let add a b =
        someAdd a b    // no warning
    add a b
```

#### 5. Modules

Modules behave identically to types at this point.

#### 6. Behaviour under different values of the `IsError` property of `System.ObsoleteAttribute`

The value of `IsError` does not affect whether the warning is emitted or not.

#### 7. New warning message

New text for the warning will indicate that the outer member should be marked `Obsolete`, like we do it
with `async` in C#. For example, `The construct is deprecated and should not be used. Consider marking the outer function or type as Obsolete.`

# Drawbacks

# Alternatives

# Compatibility

* Is this a breaking change?

**No**
* What happens when previous versions of the F# compiler encounter this design addition as source code?

**It emits the warning**
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?

**It emits the warning**

# Unresolved questions
