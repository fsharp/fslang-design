
# F# 4.0 Speclet - Class Names as Functions

[User Voice Request](https://fslang.uservoice.com/forums/245727-f-language/suggestions/5663317-allow-to-use-class-constructors-as-functions), [Pull Request](https://visualfsharp.codeplex.com/SourceControl/network/forks/dsyme/cleanup/contribution/7104), [Commit](https://github.com/dotnet/fsharp/commit/e3e46c49858ee39ddbb61ef1134089626e19e6fb)

### Aim

Implements the proposed F# feature 'Class names as functions'.

### Background

IN F# 2.x+, union cases can be used as standalone functions, which means I can use them in partial application:

x |> List.map Some

The same isn't currently allowed on class names when there is a corresponding constructors:

x |> List.map Uri

For this examples is not a problem, but in some real life scenarios is annoying to have to create wrapper functions

### Design 

With this feature, names resolved to an object constructor (or to an overloaded set of object constructors) 
can now be used in expressions even when they are not subsequently applied to any expression or type arguments. 
In this case the expression is interpreted as a lambda expression representing the use of the constructor 
as a first-class function-typed value.  

Normal F# overload resolution rules are applied in this case, just as for any other case of an overloaded method. 

For example, after this change System.Uri can be used as follows:

    "http://google.com" |> System.Uri

or

    open System
    ["http://google.com"] |> List.map Uri

This removes a considerable irregularity in F# where explicit lambdas were needed for first-class uses 
names resolved to object constructors. Explicit lambdas are not needed for other similar cases.  
Removing this kind of corner case makes coding simpler, more regular and more fluent.

It is worth noting that after this extension, the names "Set" and "Map" can be used, since they are type names in an auto-opened part of the FSharp.Core library where the types have a corresponding constructor, for example:

  let f3() = ["a"] |> Set
  let f4() = [("a", 4); ("b", 5) ] |> Map

### Specification 

In Section "14.2.2	Item-Qualified Lookup" of the F# Language Specification, the table is given a new case 
"A  unique type name _C_ where projs is empty".  In this case	process the types using a new instantiation for _C_, 
thus generating a type ty, and process the object construction (fun v -> new ty(v)) as an object constructor call.



### Testing

Particular care is needed for the cases where the type name 
is a generic type (or one of a number of generic types overloaded by generic arity).

The IDE tests need care, since there are some code paths that may potentially affect the IntelliSense results given for cases like

     System.Collections.Generic.List   <-- press . after this, trying to access static members

### Detailed Examples

These examples are taken from the testing added in the implementation pull request.  

Basic Examples:

    let ss1 = System.String
    let ss2 = System.Guid
    
    type ClassWithOneConstructor(x:int) = 
        member x.P = 1
    
    let ss3 = ClassWithOneConstructor

Examples where the type name is overloaded by generic arity:

    type OverloadedClassName<'T>(x:int) = 
        new (y:string) = OverloadedClassName<'T>(1)
        member __.P = x
        static member S() = 3


    type OverloadedClassName<'T1,'T2>(x:int) = 
        new (y:string) = OverloadedClassName<'T1,'T2>(1)
        member __.P = x
        static member S() = 3

    let t3 = 3 |> OverloadedClassName // expected error - multiple types exist
    let t3s = "3" |> OverloadedClassName // expected error - multiple types exist


Examples where the type name is overloaded by generic arity but only some of the types have constructors:

    type OverloadedClassName<'T>(x:int) = 
        new (y:string) = OverloadedClassName<'T>(1)
        member __.P = x
        static member S() = 3


    type OverloadedClassName<'T1,'T2> = 
        member __.P = 1
        static member S() = 3

    let t2 = 3 |> OverloadedClassName<int,int> //  CHANGE IN ERROR MESSAGE IN F# 4.x: Was "Invalid use of a type name", now "The value or constructor 'OverloadedClassName' is not defined"
    let t3 = 3 |> OverloadedClassName // expected error - multiple types exist
    let t2s = "3" |> OverloadedClassName<int,int> //  CHANGE IN ERROR MESSAGE IN F# 4.x: Was "Invalid use of a type name", now "The value or constructor 'OverloadedClassName' is not defined"
    let t3s = "3" |> OverloadedClassName // expected error - multiple types exist

Examples where the type name is overloaded by generic arity but none of the types have constructors:

    type OverloadedClassName<'T> = 
        static member S(x:int) = 3

    type OverloadedClassName<'T1,'T2> = 
        static member S(x:int) = 3

    let t3 = 3 |> OverloadedClassName.S // expected error - multiple types exist
    let t4 = 3 |> OverloadedClassName.S2 // expected error -  The field, constructor or member 'S2' is not defined



Examples where the type name is overloaded by generic arity, including a non-generic type, where all of the types have a constructor:

    type OverloadedClassName(x:int) = 
        new (y:string) = OverloadedClassName(1)
        member __.P = x
        static member S() = 3

    type OverloadedClassName<'T>(x:int) = 
        new (y:string) = OverloadedClassName<'T>(1)
        member __.P = x
        static member S() = 3


    type OverloadedClassName<'T1,'T2>(x:int) = 
        new (y:string) = OverloadedClassName<'T1,'T2>(1)
        member __.P = x
        static member S() = 3

    let t3 = 3 |> OverloadedClassName // expected error - multiple types exist
    let t3s = "3" |> OverloadedClassName // expected error - multiple types exist


Examples where the type name is overloaded by generic arity, including a non-generic type, where some of the types have a constructor:

    type OverloadedClassName(x:int) = 
        new (y:string) = OverloadedClassName(1)
        member __.P = x
        static member S() = 3

    type OverloadedClassName<'T>(x:int) = 
        new (y:string) = OverloadedClassName<'T>(1)
        member __.P = x
        static member S() = 3


    type OverloadedClassName<'T1,'T2> = 
        member __.P = 1
        static member S() = 3

    let t2 = 3 |> OverloadedClassName<int,int> //  CHANGE IN ERROR MESSAGE IN F# 4.x: Was "Invalid use of a type name", now "The value or constructor 'OverloadedClassName' is not defined"
    let t3 = 3 |> OverloadedClassName // NO ERROR EXPECTED
    let t2s = "3" |> OverloadedClassName<int,int> //  CHANGE IN ERROR MESSAGE IN F# 4.x: Was "Invalid use of a type name", now "The value or constructor 'OverloadedClassName' is not defined"
    let t3s = "3" |> OverloadedClassName // expected error - multiple types exist

Examples where the type name is overloaded by generic arity, including a non-generic type, where some none of the types have a constructor:

    type OverloadedClassName = 
        static member S(x:int) = 3

    type OverloadedClassName<'T> = 
        static member S(x:int) = 3

    type OverloadedClassName<'T1,'T2> = 
        static member S(x:int) = 3

    let t3 = 3 |> OverloadedClassName.S // NO ERROR EXPECTED
    let t4 = 3 |> OverloadedClassName.S2 // expected error -  The field, constructor or member 'S2' is not defined


