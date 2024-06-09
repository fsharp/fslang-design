# F# RFC FS-1127 - Init-only setters and required properties support in F# #

The design suggestion [C# record interop (including init only properties of .net 5 and required of .net 7)](https://github.com/fsharp/fslang-suggestions/issues/904) has been approved in principle.  This RFC covers the detailed proposal for `init` and `requried` properties.

- [x] Implementation: [Completed](https://github.com/dotnet/fsharp/pull/13490)
- [x] [Discussion](https://github.com/fsharp/fslang-design/discussions/695)

## Summary #

- [Init-only setters](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-9.0/init) is a property setter, which can only be called at the time of object initialization. Currently, F# ignores it and allows caller to call an init-only setter after an object has been created.
  - F# compiler will restrict the call of an init-only setter to the object initialization only.

- [Required members](https://github.com/dotnet/csharplang/blob/main/proposals/required-members.md) feature was recently added to C# and is a way of specifying that a property or field is required to be set during object initialization, forcing the caller to provide initial values for all required members at the creation side.
  - In this RFC, F# support will be limited to consuming classes with required members and enforcing the initialization at the creation side in the compile time.

## Motivation ##

F# compiler should support and respect the contracts, which are implied by the CIL and metadata produced by C#.

<sub>C# motivation for adding required members can be found [here](https://github.com/dotnet/csharplang/blob/main/proposals/required-members.md#motivation).</sub>

## Detailed Design ##

### Init-only property setters ###

F# compiler will be restricting the call of the init-only setter to the object initialization, for example:

Given the following C# type:

```csharp
public sealed class InitOnly
{
    public int GetInit { get; init; }
}
```

And the following F# code:

```fsharp
let initOnly = InitOnly()
initOnly.GetInit <- 42
```

**Before the change**, the code above will compile and mutate the property successfully.

**After the change**, the code above will produce the following compile-time error diagnostic:

> Init-only property 'GetInit' cannot be set outside the initialization code. See <https://aka.ms/fsharp-assigning-values-to-properties-at-initialization>

### Required members representation in `IL` ###

Every member, marked as `required` has `RequiredMemberAttribute` applied to them, for example, the following C# code :

```csharp
class RequiredProperty
{
    public required int GetInit { get; init; }
}
```

will result in the following codegen for the property:

```csharp
.property instance int32 GetInit()
{
    .custom instance void System.Runtime.CompilerServices.RequiredMemberAttribute::.ctor() = (
        01 00 00 00
    )
    .get instance int32 RequiredProperty::get_GetInit()
    .set instance void modreq([System.Runtime]System.Runtime.CompilerServices.IsExternalInit) RequiredProperty::set_GetInit(int32)
}
```

### Constructors representation, `CompilerFeatureRequired`, and `SetsRequiredMembersAttribute` ###

Any constructor in a type with `required` members,
If the type has at least one `required` member, its constructors will be marked with two attributes:

   1. `System.Runtime.CompilerServices.CompilerFeatureRequiredAttribute` with feature name `RequiredMembers`
   2. `System.ObsoleteAttribute` with the string `"Types with required members are not supported in this version of your compiler"`, and the attribute is marked as an error, to prevent any older compilers from using these constructors.

> **Note**
> If the constructor is marked with `System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute`, neither `CompilerFeatureRequiredAttribute` nor `ObsoleteAttribute` will be applied.

**Examples:**
The following C# code:

```csharp
class RequiredProperty
{
    public RequiredProperty() {}
    [SetsRequiredMembers]
    public RequiredProperty(int a)
    {
        GetInit = a;
    }
    public required int GetInit { get; init; }
}
```

will result in the following codegen for the default or user-defined constructor without `SetsRequiredMembers`:

```csharp
.method public hidebysig specialname rtspecialname instance void .ctor () cil managed
    {
        .custom instance void [System.Runtime]System.ObsoleteAttribute::.ctor(string, bool) = (
            01 00 5f 43 6f 6e 73 74 72 75 63 74 6f 72 73 20
            6f 66 20 74 79 70 65 73 20 77 69 74 68 20 72 65
            71 75 69 72 65 64 20 6d 65 6d 62 65 72 73 20 61
            72 65 20 6e 6f 74 20 73 75 70 70 6f 72 74 65 64
            20 69 6e 20 74 68 69 73 20 76 65 72 73 69 6f 6e
            20 6f 66 20 79 6f 75 72 20 63 6f 6d 70 69 6c 65
            72 2e 01 00 00
        )
        .custom instance void System.Runtime.CompilerServices.CompilerFeatureRequiredAttribute::.ctor(string) = (
            01 00 0f 52 65 71 75 69 72 65 64 4d 65 6d 62 65
            72 73 00 00
        )
        .maxstack 8

        IL_0000: ldarg.0
        IL_0001: call instance void [System.Runtime]System.Object::.ctor()
        IL_0006: ret
    }
```

And in the following codegen for the custom constructor with `SetsRequiredMembers`:

```csharp
.method public hidebysig specialname rtspecialname instance void .ctor (int32 a) cil managed
{
    .custom instance void System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute::.ctor() = (01 00 00 00)
    .maxstack 8
    // <skipped>
    ret
}
```


## Detailed design ##

### Implementation details ###

> **Note**
> The following applies to types with `required` members. Behaviour for the types with no `required` members remains unchanged.

- When implicitly calling the default constructor, via the initializer, F# compiler will perform the following:
  - If constructor has `ObsoleteAttribute` on it and `CompilerFeatureRequiredAttribute` with value of `RequiredMembers`:
         The compiler will ignore the `ObsoleteAttribute`, call the constructor and will proceed to the initializer.
  - If the constructor doesn't have the `CompilerFeatureRequiredAttribute` or the langauge version does not support it, a compile-time diagnostic will be produced.

- When the object initializer is being invoked, compiler will ensure that implicit constructor will be invoked if supported (see above).
  - After that, compiler will ensure, that all members with `RequiredMemberAttribute` are present in the object initializer, and produce the error if anything is missing.
  - Constructors with `SetsRequiredMembersAttribute` will be invoked normally, without any changes.

## Drawbacks ##

None

## Alternatives ##

No alternatives

## Compatibility ##

- Is this a breaking change?

    Yes. In a way. Currently, init-only properties can be set outside the initializer in F#, it will be a compile-time error after this change is introduced. However this code should never have been allowed.

- What happens when previous versions of the F# compiler encounter this design addition as source code?

    Nothing. No changes to syntax and/or no additional constructs are introduced.

- What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?

  - Previous versions of the compiler will still be able to change init-only properties.
  - Previous versions of the compiler will not be able to use default constructor or object initializers for any type with required members in it, since default constructor will have an `ObsoleteAttribute` on it.

- If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?

    No changes will be introduced to the `FSharp.Core`

## Unresolved questions ##

* [ ] Shall both of `init-only` setters and `required` members support be under language feature?
* [ ] Shall `required` members support be tied to runtime (technically, it doesn't require any runtime features)?
* [ ] What happens if you explicitly declare the properties on F# classes or record fields?  Is that even possible?
