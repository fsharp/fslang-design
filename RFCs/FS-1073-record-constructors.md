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
PensionData r = PensionData("Adam",10)
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

# Drawbacks

Organizations preferring using the existing syntax and wanting to enforce this will need to do so through a style guide.
They may wish to do so because:
- While the type is less clear, the field names are more clear than with constructors, when looking at code without intellisense.
- They often like to change the order of fields in a record, which is safer using the existing syntax if the field share the same type.

# Alternatives

Do nothing.

# Compatibility

This is not a breaking change.

# Unresolved questions

None.
