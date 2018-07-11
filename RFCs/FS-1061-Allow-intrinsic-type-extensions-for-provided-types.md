# F# RFC FS-1061 Allow intrinsic type extensions for provided types

The design suggestion [Allow intrinsic type extensions for provided types](https://github.com/fsharp/fslang-suggestions/issues/509) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/509)
* [x] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/323)
* [ ] Implementation: TODO


# Summary
[summary]: #summary

The aim of this feature is to allow provided types from [type providers](https://docs.microsoft.com/en-us/dotnet/fsharp/tutorials/type-providers/) to be extended using the [**intrinsic type extension**](https://docs.microsoft.com/en-us/dotnet/fsharp/language-reference/type-extensions) syntax where the extensions become part of the types definition.  

>An intrinsic extension is an extension that appears in the same namespace or module, in the same source file, and in the same assembly (DLL or executable file) as the type being extended. An optional extension is an extension that appears outside the original module, namespace, or assembly of the type being extended. Intrinsic extensions appear on the type when the type is examined by reflection, but optional extensions do not. Optional extensions must be in modules, and they are only in scope when the module that contains the extension is open.

# Motivation
[motivation]: #motivation

The motivation for this change comes from the inherant limitations with the way that provided types can be used.  With C# code generation it's very common for the generated code to be produced as a [partial class](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/partial-classes-and-methods) where the user is free to extend the type in another file by using the partial class syntax, in F# this is not possible as it has no concept of a partial class.  F# does have intrinsic and optional type extensions, it makes sense to allow this syntax to be used on provided types to allow the generated code more customisation by the user.  Frameworks such as Xamarin.iOS require certain methods to be implemented as overloads which rules out using optional type extensions, indeed its common for frameworks to have these kind of constraints as C# is known to use partial classes for these situations.  Having intrinsic type extension syntax greatly simplifys the construction on these types.  

# Detailed design
[design]: #detailed-design

The compiler needs to be modified so that instead of an error being generated when a provided type is extended the type is extended:

```
Error FS0192: internal error: FindTypeDefBuilder: <Method> not found (FS0192) (<Application>)
```

Instead the type is extended in the same way as a normal type extension.

The proposed systax is the same as the existing intrinsic type extension syntax:

Example code short form:

```fsharp
type t = BasicProvider.Generative.MyType with
    member x.Foo = 42
```

Example code long form:

```fsharp
type t = BasicProvider.Generative.MyType

type t with 
    member x.Foo = 42
```

Given that `MyType` is just a plain type with a default constructor the resulting generated code wold look like this:
```
type t =
  class
    new : unit -> t
    member Foo : int
```

An addition change to languge specification also needs to be changed so that an override can be applied as an intrinsic type extension too.   Imagine that BasicProvider.Generative.MyType has an abstract method as follows:

```fsharp
abstract member Rotate: float -> unit
```

It would be natural to describe this as follows:

```fsharp
type t = BasicProvider.Generative.MyType

type t with 
    member x.Foo = 42
    override x.Rotate _ = ()
```

This would result in the following warning:
```
[FS0060] Override implementations in augmentations are now deprecated. Override implementations should be given as part of the initial declaration of a type.
```

So the final part of this  proposal is to allow override implementations in provided types **only**, as it is not always possible to provide the implementation as part of the initial declaration of the type especially if you are only the consumer of the type provider. 

# Drawbacks
[drawbacks]: #drawbacks

The only drawback is the complexity of the addition, but the addition makes provided types a lot more flexible especially as F# has no partial types, the olny alternative without this is inheriting the provided types and adding implementations there which is not ideal.

# Alternatives
[alternatives]: #alternatives

The alternative is to add features to the provided type by alternative means such as inheriting from the provided type or by augmenting another type which deferrs to the provided type internally.  

Another alternative would be to add partial classes to F# which is really what intrinsic type extensions are albeit limited to extensions in the same file.

# Compatibility
[compatibility]: #compatibility

Please address all necessary compatibility questions:
* Is this a breaking change?  
```
No
```
* What happens when previous versions of the F# compiler encounter this design addition as source code?  
The same as happens now, an error is displayed to the user if they try to extend a provided type.
```
Error FS0192: internal error: FindTypeDefBuilder: <Method> not found (FS0192) (<Application>)
```
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?  
```
In compiled binaries provided types are emmitted as IL and embedded in the user assembly so this does not affect them. 
```
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?  
```
This does not effect FSharp.Core
```

# Unresolved questions
[unresolved]: #unresolved-questions

Would/could erasing type providers be supported in any way with this proposal?

