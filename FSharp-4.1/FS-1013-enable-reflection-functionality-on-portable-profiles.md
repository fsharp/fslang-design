# F# RFC FS-1013 - Enable FSharp.Reflection functionality on Portable 78, 259 and .NET Standard 1.5 profiles

The design suggestion [Enable FSharp.Reflection functionality on Portable Profiles](https://fslang.uservoice.com/forums/245727-f-language/suggestions/14264544-support-fsharptype-and-fsharpvalue-methods-on-all) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] Details: No discussion needed, change has been implemented
* [x] Implementation: Implemented 


# Summary
[summary]: #summary


The functionality ``FSharpValue.MakeRecord`` and other similar methods  are missing from FSharp.Core reflection support for Profile78, 259 and .NET Core (i.e. .NET Standard 1.5). This is because the signatures of these use the  ``BindingFlags`` type and that type is not available in those profiles.  

This functionality is really part of the basic F# programming model. This is a frustrating problem because the ``BindingFlags`` is really only used to support ``BindingFlags.NonPublic,`` and could always just as well have always been a Boolean flag.

This RFC is to make this functionality available, especially on .NET Core but also on the portable profiles.


# Detailed design
[design]: #detailed-design

The solution to this has already been implemented, and is the addition of these extension methods:

```fsharp
module FSharp.Reflection.FSharpReflectionExtensions

type FSharpValue with 
    static member MakeRecord: recordType:Type * values:obj [] * ?nonPublic:bool  -> obj
    static member GetRecordFields:  record:obj * ?nonPublic:bool  -> obj[]
    static member PreComputeRecordReader : recordType:Type  * ?nonPublic:bool  -> (obj -> obj[])
    static member PreComputeRecordConstructor : recordType:Type  * ?nonPublic:bool  -> (obj[] -> obj)
    static member PreComputeRecordConstructorInfo: recordType:Type * ?nonPublic:bool -> ConstructorInfo
    static member MakeUnion: unionCase:UnionCaseInfo * args:obj [] * ?nonPublic:bool -> obj
    static member GetUnionFields:  value:obj * unionType:Type * ?nonPublic:bool -> UnionCaseInfo * obj []
    static member PreComputeUnionTagReader          : unionType:Type  * ?nonPublic:bool -> (obj -> int)
    static member PreComputeUnionTagMemberInfo : unionType:Type  * ?nonPublic:bool -> MemberInfo
    static member PreComputeUnionReader       : unionCase:UnionCaseInfo  * ?nonPublic:bool -> (obj -> obj[])
    static member PreComputeUnionConstructor : unionCase:UnionCaseInfo  * ?nonPublic:bool -> (obj[] -> obj)
    static member PreComputeUnionConstructorInfo: unionCase:UnionCaseInfo * ?nonPublic:bool -> MethodInfo
    static member GetExceptionFields:  exn:obj * ?nonPublic:bool -> obj[]

type FSharpType with

    static member GetRecordFields: recordType:Type * ?nonPublic:bool -> PropertyInfo[]
    static member GetUnionCases: unionType:Type * ?nonPublic:bool -> UnionCaseInfo[]
    static member IsRecord: typ:Type * ?nonPublic:bool -> bool
    static member IsUnion: typ:Type * ?nonPublic:bool -> bool
    static member GetExceptionFields: exceptionType:Type * ?nonPublic:bool -> PropertyInfo[]
    static member IsExceptionRepresentation: exceptionType:Type * ?nonPublic:bool -> bool
```

The user would opt-in to by using

```fsharp
open FSharp.Reflection.FSharpReflectionExtensions 
```


# Drawbacks
[drawbacks]: #drawbacks

TBD (Why should we *not* do this?)

# Alternatives
[alternatives]: #alternatives

TBD (What other designs have been considered? What is the impact of not doing this?)

# Unresolved questions
[unresolved]: #unresolved-questions

TBD

