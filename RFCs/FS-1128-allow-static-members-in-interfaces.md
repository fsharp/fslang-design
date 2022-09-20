# F# RFC FS-1128 - Allow static members in interfaces

The design suggestion [Allow static members in interfaces](https://github.com/fsharp/fslang-suggestions/issues/1191) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1191)
- [x] Approved in principle
- [ ] Implementation
- [ ] Design Review Meeting(s) with @dsyme and others invitees ???
- [ ] Discussion ???

# Summary
Allow static members in interfaces

# Motivation
Currently is not allowed to define a static member within interfaces, however the CLR supports this and C# as well since C# 8.

F# can consume those static members and SRTPs can "see" them (as opposed to extension members).

```fs

[<Interface>]
type IAdditionOperator<'T> =
    abstract myMember: 'T * 'T -> 'T
    static member myStaticMember x = x  // error FS0868: Interfaces cannot contain definitions of concrete members. You may need to define a constructor on your type to indicate that the type is a class.

```

# Detailed design
This RFC will avoid a compiler check for static members in interfaces and make sure the member is properly compiled.


# Drawbacks
None

# Alternatives
The alternative is to do nothing

# Compatibility

* Is this a breaking change?
  * No
* What happens when previous versions of the F# compiler encounter this design addition as source code? 
  * They will keep failing as they do now
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
  * It's expected to work as it does currently with code coming from C#
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?
  * It will work as designed as this is not a change or extension to FSharp.Core
