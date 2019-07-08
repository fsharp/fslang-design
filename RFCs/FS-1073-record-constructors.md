# F# RFC FS-1073 - Allow access to Record type constructors

The design suggestion [Allow access to Record type constructors](https://github.com/fsharp/fslang-suggestions/issues/722) has been marked "approved in principle". This RFC covers the detailed proposal.

# Summary

A record type created in F#, such as
```fsharp
type PensionData =
    { Name:string; ProbableNumberOfYearsUntilRetirement:int }
```

generates a constructor which can be used in C#: 

```csharp
var r = PensionData("Adam",10)
```

Using this constructor is not currently allowed in F#:
```fsharp
let r = PensionData("Adam",10) // FS0039 The value or constructor 'PensionData' is not defined.
```

This RFC proposes that this constructor should be accessible from F# code.

# Motivation

Records can of course be created in F# currently, using special F# syntax:

```fsharp
let r = { Name = "Adam"; ProbableNumberOfYearsUntilRetirement = 10 }
```

This has a few disadvantages:
- Verbose syntax compared to using the constructor, with all field names needing to be written out.
- Lack of explicit specification of the name of the type. Without the binding to `r` plus intellisense it would be hard to determine the type of the record.
- In order to enter the record, you have to know the field names, and intellisense is not as helpful as it is for constructors.
- The type inference system around record creation makes guesses based on field names and is arguably not as reliable as when the type is specified.

Therefore it would be good to have, **in addition to** the existing F#-specific syntax, access to the standard constructor for the record type.

# Detailed design

See summary.

Note that the existing constructor has named arguments, as in the following currently valid C#, which would automatically apply to F#:

```csharp
var r = PensionData(name: "Adam",10)
```

If [optional record fields](https://github.com/fsharp/fslang-suggestions/issues/617) are implemented, they should be implemented in the constructor as optional named arguments.

# Drawbacks

Organizations preferring using the existing syntax and wanting to enforce this will need to do so through a style guide.
They may wish to do so because:
- While the type is less clear, the field names are more clear than with constructors, when looking at code without intellisense.
- They often like to change the order of fields in a record, which is safer using the existing syntax if the field share the same type.

# Alternatives

Do nothing.

# Compatibility

This should not be implemented as a breaking change.

The type name should only be treated as the constructor where there is no other binding with the same name in scope.

For example, similarly to other classes:
```fsharp
type C(a:int) = member t.A = a
let C(x:int) = ()
C // the second C takes precedence

type R = { A: int }
let R(x:int) = ()
R // the second R should take precedence
```

Unlike other classes:
```fsharp
let C(x:int) = ()
type C(a:int) = member t.A = a
let z = C 3 // z has type C

let R(x:int) = ()
type R = { A: int }
let z = R 3 // Currently this is valid code and z has type unit.
// This should remain the case to avoid breaking changes.
```


# Unresolved questions

This proposal could be extended to include pattern matching:

```fsharp
match r with
| PensionData(name, years) -> sprintf "%s will probably retire in %i years." name years
```

