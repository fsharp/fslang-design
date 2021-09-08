# F# RFC FS-1115 - XML-documentation collecting mechanism

The design suggestion [Redesign XML-documentation collecting mechanism](https://github.com/fsharp/fslang-suggestions/issues/1071)

This RFC covers the detailed proposal for this suggestion.

* [ ] Approved in principle
* [ ] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1071)
* [ ] [Implementation](https://github.com/dotnet/fsharp/pull/11973)


# Summary
[summary]: #summary

This RFC describes redesigning the XML-documentation collecting mechanism to allow more predictable and C#-consistent positions for XML-docs.

# Motivation
[motivation]: #motivation

At the moment, XML-documentation comments are sequentially collected and attached to the nearest (after comments) declaration that supports XmlDoc. This leads to errors when comments that should not be considered attached to declarations at all:

```F#
let f x = ///g
    ()

let g = 5 // xmlDoc: g
```

Also, at the moment, it is allowed to place comments up to the name of the binding/type definition, which seems redundant:
```F#
/// f1
let /// f2
    rec /// f3
        inline /// f4 
               private f x = ...  // xmlDoc: f1 f2 f3 f4
```

In addition, collection of XML-documentation for union case fields is not currently supported.

This RFC describes redesigning the XML-documentation collecting mechanism to allow more predictable and C#-consistent positions for XML-docs, the advantages of which are:

- Bug fixes in the current mechanism for collecting XML-documentation
- XML-documentation support for union case fields
- More predictable and C#-consistent positions for XML-documentation

# Detailed design
[design]: #detailed-design

The basic idea is to set a grab point for documentation after a non-comment expression is encountered. Then we can assume that the documentation comment block belongs to the declaration if its grab point matches the correct position in the declaration (for example, the beginning of the declaration).
The correct positions for setting grab points are those that comply with the rules described below:
## Common
It is allowed to place documentation only at the beginning of the declarations (types, type members, bindings, etc.):
```F#
/// A1
type /// A2 - [discarded]
     internal /// A3 - [discarded]
              A
```
```F#
/// f1
let /// f2 - [discarded]
    rec /// f3 - [discarded]
        inline /// f4 - [discarded]
               private f x = ...
```
```F#
/// B1
static /// B2 - [discarded]
       member /// B3 - [discarded]
              inline /// B4 - [discarded]
                     B() = ...
```
```f#
///E1
exception ///E2 - [discarded]
          E of string 
```
```f#
/// B1
val /// B2 - [discarded]
    mutable /// B3 - [discarded]
            private /// B4 - [discarded]
                    B: int
```

## Delimiters
Similar to C#, if another expression is encountered when collecting documentation,
then documentation before this expression cannot be attached to the declaration:
```F#
/// A1 - [discarded]
1 + 1
/// A2
type A
```

After documentation, it is allowed to place a group of simple comments ( `//`, `////`, `(* ... *)` ):

```F#
/// X
// simple comment
//// simple comment
(* simple multiline comment *)
let x = ... 
```

But it is still forbidden to separate documentation blocks with simple comments:

```F#
/// X - [discarded]
// simple comment
/// Y
let x = ...
```

## Attributes
Similar to C#, if there are attributes in front of the declaration, then documentation before the attributes are attached to the declaration:
```F#
/// A1
[<Attribute>]
/// A2 - [discarded]
type A
```
```F#
/// F1
[<Attribute>]
/// F2 - [discarded]
let f x = ...
```
```f#
/// E1
[<DllImport("")>]
/// E2 - [discarded]
extern void E()
```
This also means that if the declaration contains attributes after its beginning, then documentation before such attributes are discarded:
```F#
type /// A - [discarded]
     [<Attribute>] A
```
```F#
let /// X - [discarded]
    [<Attribute>] X
```
```F#
module /// M - [discarded]
       [<Attribute>] M
```
```F#
/// A1
[<Attribute1>]
type /// A2 - [discarded]
     [<Attribute2>] A 
```
```F#
/// F1
[<Attribute1>]
let /// F2 - [discarded]
    [<Attribute2>] f x = ...
```
```F#
/// M1
[<Attribute>]
module /// M2 - [discarded]
       [<Attribute2>] M = ...
```

## Recursive declarations group
It is allowed to add documentation before `and` keyword:
```F#
type A

/// B
and B
```
```F#
let rec f x = g x

/// G
and g x = f x
```

Also, documentation is allowed to be left after `and`, taking into account the rule for attributes:

```F#
type A

and /// B
    B
```
```F#
type A

and /// B1
    [<Attribute>]
    /// B2 - [discarded]
    B
```
```F#
let rec f x = g x

and  /// G
     g x = f x
```

However, if documentation blocks before and after `and` are present at the same time, then the priority is given to the documentation before `and`:
```F#
type A

/// B1
and /// B2 - [discarded]
    B
```

## Primary constructor
It is allowed to place documentation before the declaration of the primary constructor, taking into account the rule for attributes:
```F#
type A /// CTOR1
       [<Attribute>]
       /// CTOR2 - [discarded]
       ()
```

## Discriminated Unions
It is allowed to place documentation for union cases, taking into account the rule for attributes:
```F#
 type A =
    ///One
    One
    ///Two
    | Two
```
```f#
type A =
    ///One1
    | [<Attr>]
      ///One2 - [discarded]
      One
    ///Two1
    | [<Attr>]
      ///Two2 - [discarded]
      Two
```

## Union case/record fields
It is allowed to place documentation for fields, taking into account the rule for attributes:
```F#
type A =
    {
        ///B1
        [<Attr>]
        ///B3 - [discarded]
        B: int 
    }
```
```F#
type Foo =
    | Thing of
       ///a1
       a: string *
       ///b1
       bool
```

## Function parameters
The documentation for the function parameters should be described in the documentation for the function itself (for example, in the `<param>` tags)
and not allowed above parameter declarations:
```F#
/// function
/// <param name='x'>...</param>
/// ...
let f x (/// x - [discarded]
         y, 
         /// y - [discarded]
         z) = ...
```

## Property accessors
It is allowed to place documentation only for the property itself,
property documentation will also apply to accessors:
```f#
member ///A
       x.A /// GET - [discarded]
           with get () = ...
           /// SET - [discarded]
           and set (_: int) = ...
```

## Compiler diagnostic
To detect invalid documentation blocks positions, a new compiler diagnostic will be required.
We should mark each documentation block when grabbing it and then report syntax warning `'XML comment is not placed on a valid language element.'` for blocks that haven't been grabbed after parsing finishes.

## XML-documentation fixes in FSharp.Core and FSharp.Compiler.Service

`FSharp.Core` and `FSharp.Compiler.Service` contain XML-documentation that is incompatible with this RFC and needs to be fixed.

# Drawbacks
[drawbacks]: #drawbacks

Already existing projects may contain XML-documentation incompatible with this RFC. Such documentation will be discarded.

# Compatibility
[compatibility]: #compatibility

- Already existing projects may contain XML-documentation incompatible with this RFC. For such documentation, a corresponding warning will be displayed by the compiler.
But if the warning is ignored, this documentation will be discarded.


- This RFC restricts the list of possible positions for XML-documentation and adds XML-documentation support for union case fields.
This means that documentation that complies with this RFC will be backward compatible with previous versions of the compiler, with the exception of previously unsupported documentation for union case fields.
