# F# RFC FS-1061 Allow intrinsic type extensions for provided types

The design suggestion [Allow intrinsic type extensions for provided types](https://github.com/fsharp/fslang-suggestions/issues/509) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/509)
* [x] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/323)
* [ ] Implementation: [In-progress but shelved](https://github.com/dotnet/fsharp/pull/882)


# Summary
[summary]: #summary

The aim of this feature is to allow provided types from [type providers](https://docs.microsoft.com/en-us/dotnet/fsharp/tutorials/type-providers/) to be extended using the [**intrinsic type extension**](https://docs.microsoft.com/en-us/dotnet/fsharp/language-reference/type-extensions) syntax where the extensions become part of the types definition.  

>An intrinsic extension is an extension that appears in the same namespace or module, in the same source file, and in the same assembly (DLL or executable file) as the type being extended. An optional extension is an extension that appears outside the original module, namespace, or assembly of the type being extended. Intrinsic extensions appear on the type when the type is examined by reflection, but optional extensions do not. Optional extensions must be in modules, and they are only in scope when the module that contains the extension is open.

# Motivation
[motivation]: #motivation

The motivation for this change comes from the inherent limitations with the way that provided types can be used.  With C# code generation it's very common for the generated code to be produced as a [partial class](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/partial-classes-and-methods) where the user is free to extend the type in another file by using the partial class syntax, in F# this is not possible as it has no concept of a partial class.  F# does have intrinsic and optional type extensions, it makes sense to allow this syntax to be used on provided types to allow the generated code more customisation by the user.  Frameworks such as Xamarin.iOS require certain methods to be implemented as overloads which rules out using optional type extensions, indeed its common for frameworks to have these kind of constraints as C# is known to use partial classes for these situations.  Having intrinsic type extension syntax greatly simplifys the construction on these types.  

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

An addition change to language specification also needs to be changed so that an override can be applied as an intrinsic type extension too.   Imagine that BasicProvider.Generative.MyType has an abstract method as follows:

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

The final part of this  proposal is to allow override implementations produced by generative type providers **only**, as it is not always possible to provide the implementation as part of the initial declaration of the type, especially if you are only the consumer of the type provider.  

## Examples of current limitations that this RFC addresses

As an example of the limitations imposed by not allowing intrinsic type extensions or override implementations on generative type providers I present the following examples.

In the iOS designer type provider the usage looks like this:

```fsharp
type container = Xamarin.UIProvider 

[<Register(container.myViewControllerBase.CustomClass)>] 
type ViewController(handle : IntPtr) =  
    inherit container.myViewControllerBase(handle)

    override x.DidReceiveMemoryWarning() = 
        base.DidReceiveMemoryWarning()
        
    override x.ViewDidLoad() =
        x.DoNotPressAction <- Some (fun _ -> x.View.BackgroundColor <- UIColor.Purple)
        base.ViewDidLoad()
```

This has several drawbacks that makes the user facing code more complex and unweidy than it should be with additions mentioned in the RFC.

### Attributes have to be defined on outward facing type

Many frameworks require the generation of attributes to be applied to generated type, Xamarin.iOS is such an example.  The `Register` attribute has to be applied to `ViewControllers` to supply the mapping between a user class and the storyboard name.  As attributes do not propagate with inheritance you cannot generate them on the base or abstract type.  The best option available to a type provider designer is to provide a helper to be generated that the consumer has to learn how to apply, of course this can be detailed in documentation but its an example of unnecessary work.  You can see this in the iOS UI provider above:

```
[<Register(container.myViewControllerBase.CustomClass)>]
type ViewController(handle : IntPtr) =  
```

### Inheritance required

The generated type **has** to be sub-classed to apply method overloads which are required by certain framework, iOS being the example again. Xamarin.iOS requires that the users custom `ViewController` to inherit from `UIViewController` or a derivative, these base types had a multitude of methods that can be overridden such as `ViewDidLoad` and `DidReceiveMemoryWarning` in the example above.  This means any constructors present on the type must also be detailed using the inherit syntax:

```fsharp
type ViewController(handle : IntPtr) =  
    inherit container.myViewControllerBase(handle)
```

In the case of multiple constructors being required the record syntax must be used which starts to become cumbersome and ugly to define.  An example of this can be seen if you take this example from the Android ViewUI type provider:

```fsharp
type container2 = Xamarin.Android.ViewUI<"Test.axml"> 

type MyView =
    inherit container2
    new() as x =
        { inherit container2()} then x.Initialise()
    new(context) as x = 
        { inherit container2(context) } then x.Initialise()
    new(context:Context, attr:Android.Util.IAttributeSet) as x =
        { inherit container2(context, attr) } then x.Initialise()
```

All of the duplicated constructor propagation can be avoided by allowing intrinsic type extensions to occur on generative type providers.

Method overloads are applied in the usual manner but if they could be applied as part of the 
```fsharp
    override x.ViewDidLoad() =
        x.DoNotPressAction <- Some (fun _ -> x.View.BackgroundColor <- UIColor.Purple)
        base.ViewDidLoad()
```

If this RFC was implemented the usage of the iOS type provider would look like this instead:

```fsharp
type container = Xamarin.UIProvider 

type container.myViewController with
    override x.DidReceiveMemoryWarning() = 
        base.DidReceiveMemoryWarning()
        
    override x.ViewDidLoad() =
        x.DoNotPressAction <- Some (fun _ -> x.View.BackgroundColor <- UIColor.Purple)
        base.ViewDidLoad()
```

Notice that no attribute is needed on `myViewController`, no constructor overloads are required, and `myViewController` is no longer required to generated as a abstract base type for the user to derive.  This is only the smallest example, many more overrides and members are normally present on iOS custom `ViewControllers`.


# Drawbacks
[drawbacks]: #drawbacks

The only drawback is the complexity of the addition, but the addition makes provided types a lot more flexible especially as F# has no partial types, the only alternative without this is inheriting the provided types and adding implementations there which is not ideal.

# Alternatives
[alternatives]: #alternatives

The alternative is to add features to the provided type by alternative means such as inheriting from the provided type or by augmenting another type which defers to the provided type internally.  These drawbacks are detailed in [Examples of current limitations that this RFC addresses](##examples-of-current-limitations-that-this-frc-addresses).

Another alternative would be to add partial classes to F# which is really what intrinsic type extensions are albeit limited to extensions in the same file.  The biggest downside to this alternative is it would have a large impact on tooling such as changes required to *Go to Definition* etc.

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
In compiled binaries provided types are emitted as IL and embedded in the user assembly so this does not affect them. 
```
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?  
```
This does not effect FSharp.Core
```

# Unresolved questions
[unresolved]: #unresolved-questions
