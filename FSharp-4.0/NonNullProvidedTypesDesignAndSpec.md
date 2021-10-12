## F# 4.0 Speclet: Non-null Provided Types

[F# Language User Voice](https://fslang.uservoice.com/forums/245727-f-language/suggestions/5841349-allow-provided-types-to-be-non-nullable-by-specify), [Pull Request](https://visualfsharp.codeplex.com/SourceControl/network/forks/dsyme/cleanup/contribution/7017#!/tab/changes), [Commit](https://github.com/dotnet/fsharp/commit/a71bbdc17a0456b95c6d6e137162f6230e31ce54)

### Aim

Allow for safer programming against provided types used to represent data schemas by allowing
provided types to be non-nullable. 

### Background

F# type providers report named types to the F# compiler in response to queries from the compiler.

In F# 3.x, these types were always considered "interop" types, and hence would always admit null literals.

### Design


A type provider can now additionally report the ``AllowNullLiteralAttribute(false)`` attribute to on a provided type.
Additionally, C# reference and delegate types can be given this attribute (this would require referencing FSharp.Core.dll)

If a type has this attribute with value "false", then the type has ``null`` as an abnormal value (see section 5.4.8 of the F# Specification).

The following are added to the AllowNullLiteralAttribute:

    type AllowNullLiteralAttribute =
    ...
        /// <summary>Creates an instance of the attribute with the specified value</summary> 
        /// <returns>AllowNullLiteralAttribute</returns> 
        new : value: bool -> AllowNullLiteralAttribute 
  
        /// <summary>The value of the attribute, indicating whether the type allows the null literal or not</summary> 
        member Value: bool 
  
### Specification

Section 5.4.8 of the F# specification is adjusted so that a  type provider with the ``AllowNullLiteralAttribute(false)`` attribute with value "false" is considered to have ``null`` as an abnormal value.

### Scope

* The feature is only available when targeting FSharp.Core 4.4.0.0 or later (or a matching PCL profile)

### Notes

In the commonly used helper file ProvidedTypes.fs, code such as the following can be added to that file to allow the construction of an appropriate provided attribute to attach to a provided type:

    let mkAllowNullLiteralValueAttributeData(value: bool) =  
        { new CustomAttributeData() with  
                member __.Constructor =  typeof<Microsoft.FSharp.Core.AllowNullLiteralAttribute>.GetConstructors().[0] 
                member __.ConstructorArguments = upcast [| CustomAttributeTypedArgument(typeof<bool>, value)  |] 
                member __.NamedArguments = upcast [| |] } 


