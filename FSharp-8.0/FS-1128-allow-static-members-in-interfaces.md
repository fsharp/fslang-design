# F# RFC FS-1128 - Allow static members in interfaces

The design suggestion [Allow static members in interfaces](https://github.com/fsharp/fslang-suggestions/issues/1191) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1191)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/14692)
- [ ] Design Review Meeting(s) with @dsyme and others invitees ???
- [x] [Discussion](https://github.com/fsharp/fslang-design/discussions/713)

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




# Questions


> Do we follow the C# approach?

Absolutely, I don't see any need to diverge from them and on the contrary, doing so can be problematic.

> Can we pick details from their approach?

Sure, below most answers are based on existing situation. The goal here is to allow them to be written in F#, not to change anything, therefore we'll keep existing behavior.


> What happens when we inherit a static interface?

Right now nothing happens when inheriting an interface with a static member  **=> we'll keep this existing behavior**.

> How will default interop here?

Right now, existing interfaces with static fields can be used to parameterize default implementations for non-static members, but in F# we can't write default implementations **=> we'll keep this existing behavior**.

> Are static properties allowed and how do we define get|set

Right now they are allowed in C#. For F# it's better to re-use the same syntax as for Abstract Classes.

> Are static indexers allowed?

Right now AFAIK they are not allowed **=> we'll keep this existing behavior**.

> Can we combine with Abstract or does it have to be an Interface

Right now Abstracts can have static members **=> we'll keep this existing behavior**.

> You use [<Interface>], I assume it also works with type TT = [...] interface end?

Yes, I don't see a reason why it shouldn't be allowed.

> How is implementing an interface with static members going to work?

Right now nothing changes whether it has static members or not **=> we'll keep this existing behavior**.

> How do we deal with the standard of F# for explicit interfaces, is that relevant here?

IMHO it's not relevant, as soon as we have an instance (which we can eventually auto-upcast or not) we're talking about instance members, not statics and since that's the current behavior **=> we'll keep this existing behavior**.

> How is the diamond problem dealt with (it's different from normal interfaces, as we don't have an instance)?

Right now it's not dealt, there is no such problem as all calls have to be explicit `Interface.method`  **=> we'll keep this existing behavior**.

> From all C# features on static interfaces, what parts are we going to support and what parts aren't we?

All, we support the same features, let's not restrict anything.

> Are all members public?

Right now they can be either public or privates **=> we'll keep this existing behavior**.

> How does the IL-compiled structure look like? (I assume we can link to C# here, there must be a design somewhere)

Yes, same as C# [it looks like this](https://sharplab.io/#v2:C4LglgNgNAJiDUAfAAgJgIwFgBQA7AhgLYCmAzgA74DGxABAJK7DEBOAZtWTgN4460DayAMy0wTVhxoMAEsQgQA9v0G9sgjUNHJ0ANloBlYC3EBzWgFkypfKboBeWgCI5CxU4DcKzbXImAbvjMQnpCACy0AML4ClYAFACUtNwhAJxxVqQ2dgketAC+fOqChdg4+UA===) **=> we'll keep this existing behavior**.

> If you implement an interface with both static and instance members in an object expression, do you need to implement the static members too? How does this extend the object expression syntax?

Right now you don't need to do anything as they can't be inherited **=> we'll keep this existing behavior**.

> We should also ensure that signature files understand the new syntax and if we generate an fsi file, that it includes these new types.

Sure, we need to ensure that.
