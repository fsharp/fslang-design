# F# RFC FS-1126 - Allow lower-case DU cases when `[<RequireQualifiedAccess>]` is specified

The design suggestion [Allow lower-case DU cases when `[<RequireQualifiedAccess>]` is specified](https://github.com/fsharp/fslang-suggestions/issues/131) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion]((https://github.com/fsharp/fslang-suggestions/issues/131)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/13432)
- [ ] Design Review Meeting(s) with @dsyme and others invitees
- [X] [Discussion](https://github.com/fsharp/fslang-design/discussions/685)

# Summary
Allow lower-case DU cases when `[<RequireQualifiedAccess>]` is specified

# Motivation
Currently is not allowed to define a lower-case DU. This is to prevent ambiguity in pattern matching between matching a union case and binding to an identifier.
However, this is not an issue if the `[<RequireQualifiedAccess>]` attribute is specified on the DU.

```fs
[<RequireQualifiedAccess>]
type DU = a // error FS0053: Discriminated union cases and exception labels must be uppercase identifiers

type DU = a // error FS0053: Discriminated union cases and exception labels must be uppercase identifiers

[<RequireQualifiedAccess>]
type DU = | a // error FS0053: Discriminated union cases and exception labels must be uppercase identifiers

type DU = | a // error FS0053: Discriminated union cases and exception labels must be uppercase identifiers
```
type DU = | ``not.allowed`` // error FS0883: Invalid namespace, module, type or union case name

Note: the above example is not a valid type name for .NET, so it will remain as an error.

# Detailed design
This RFC will avoid a compiler check for union case names if the `[<RequireQualifiedAccess>]` is used at the type level.

Example code:

```fsharp
[<RequireQualifiedAccess>]
type DU = a 

type DU = a

[<RequireQualifiedAccess>]
type DU = | a

type DU = | a

[<RequireQualifiedAccess>]
type DU =
     | a of int
     | B of string
     | C
     | ``D`` of bool
     | ``d``
     
 [<RequireQualifiedAccess>]
 type DU = ``a``

 [<RequireQualifiedAccess>]
 type DU = ``A``

 [<RequireQualifiedAccess>]
 type DU = | ``a``

 [<RequireQualifiedAccess>]
 type DU = | ``A``
```

# Drawbacks
None

# Alternatives
The alternative is to do nothing

# Compatibility

* Is this a breaking change?
  * No
* What happens when previous versions of the F# compiler encounter this design addition as source code? 
  * It will work as designed as this wasn't allowed before
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
  * It will work as designed as this wasn't allowed before
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?
  * It will work as designed as this is not a change or extension to FSharp.Core
