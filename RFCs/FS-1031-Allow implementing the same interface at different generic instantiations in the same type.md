# F# RFC FS-1031 - (Allow implementing the same interface at different generic instantiations in the same type)

The design suggestion [Allow implementing the same interface at different generic instantiations in the same type](https://github.com/fsharp/fslang-suggestions/issues/545) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [ ] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/545)
* [ ] Details: [under discussion](https://github.com/fsharp/FSharpLangDesign/issues/185)
* [ ] Implementation: [In progress](https://github.com/Microsoft/visualfsharp/pull/2867)


# Summary
[summary]: #summary

Allow implementing the same interface at different generic instantiations in the same type.

# Motivation
[motivation]: #motivation

* Feature parity with C#.
* Reduce surprise for new developers.
* Remove boilerplate inheritance chains caused by the workaround.

# Detailed design
[design]: #detailed-design

For this we will define a generic interface:

```F#
type IGet<'T> =
    abstract member Get : unit -> 'T
```

Currently it is not allowed to implement this interface more than once with a different generic parameter, the following code fails to compile:

```F#
type MyClass() =
    interface IGet<int> with
        member x.Get() = 1
    interface IGet<string> with
        member x.Get() = "2"
```

This proposal aims to eliminate this restriction.

## Naming of members
Although IL seems to allow duplicated methods with exactly the same signature (see https://github.com/fsharp/fslang-suggestions/issues/545#issuecomment-294359583), we aim to generate unique method signatures for each different instantiation.
The current prototype creates a name based on the fully qualified interface type, the code above compiles down to

```IL

.class nested public auto ansi serializable MyClass
	extends [mscorlib]System.Object
	implements class Program/IGet`1<string>,
	           class Program/IGet`1<int32>
{
	.custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = (
		01 00 03 00 00 00 00 00
	)
	// Methods
	.method public specialname rtspecialname 
		instance void .ctor () cil managed 
	{
		.maxstack 8

		/* (15,6)-(15,13) C:\Users\lr\Documents\Visual Studio 2015\Projects\FsTest\FsTest\Program.fs */
		IL_0000: ldarg.0
		IL_0001: callvirt  instance void [mscorlib]System.Object::.ctor()
		IL_0006: ldarg.0
		IL_0007: pop
		IL_0008: nop
		IL_0009: ret
	} // end of method MyClass::.ctor

	.method private hidebysig newslot virtual 
		instance int32 'Program.IGet<System.Int32>.Get' () cil managed 
	{
		.override method instance !0 class Program/IGet`1<int32>::Get()
		.maxstack 8

		/* (17,26)-(17,27) C:\Users\lr\Documents\Visual Studio 2015\Projects\FsTest\FsTest\Program.fs */
		IL_0000: nop
		IL_0001: ldc.i4.1
		IL_0002: ret
	} // end of method MyClass::'Program.IGet<System.Int32>.Get'

	.method private hidebysig newslot virtual 
		instance string 'Program.IGet<System.String>.Get' () cil managed 
	{
		.override method instance !0 class Program/IGet`1<string>::Get()
		.maxstack 8

		/* (19,26)-(19,29) C:\Users\lr\Documents\Visual Studio 2015\Projects\FsTest\FsTest\Program.fs */
		IL_0000: nop
		IL_0001: ldstr     "2"
		IL_0006: ret
	} // end of method MyClass::'Program.IGet<System.String>.Get'

} // end of class MyClass


```

## Classes vs. Object Expressions

As far as possible, there should be orthogonality between class definitions and object expressions.

The following code is similar to the class above and also compiles correctly:

```F#
    let x = { new IGet<System.Int32> with member x.Get() = 1
              interface IGet<string> with member x.Get() = "hello"
              interface IGet<float>  with member x.Get() = 1.
```

In object expressions, the type parameter can be infered, e.g. the following is currently valid code:

```F#
let x = { new IGet<_> with member x.Get() = 1 }
```

It is an explicit non-goal to allow type unknowns when implementing an interface more than once, the following code is NOT valid, even though the types _could_ be infered in this case:

```F#
    let x = { new IGet<_> with member x.Get() = 1
              interface IGet<_> with member x.Get() = "hello"
              interface IGet<_> with member x.Get() = 1. }
```

## Generics

If the surrounding type itself is generic, then it is not allowed to implement an interface with both a concrete type and one of the generic parameters.

Example:

```F#
 type IGet<'T> =
    abstract member Get : unit -> 'T

 type MyClass<'T>() =
    interface IGet<'T> with
        member x.Get() = Unchecked.defaultof<'T>
    interface IGet<string> with
        member x.Get() = "Hello"

let x = MyClass<string>() :> IGet<string> // now what?
```


## Negative test cases

During implementation, care should be taken that the following correctly fails:

* the same interface must not be implemented twice (e.g. twice ``IGet<float>`` in the same class / oe)
  * but it should be possible to override an implementation in an inherited type:
```F#
type MyClass() =
    interface IGet<int> with
        member x.Get() = 1
    interface IGet<string> with
        member x.Get() = "2"

type MyClass2() =
    inherit MyClass()
    interface IGet<int> with
        member x.Get() = 1
    interface IGet<string> with
        member x.Get() = "2"
```
* a type alias and the underlying type must be correctly unified, e.g. we may not implement ``IGet<float>`` and ``IGet<System.Double>`` in the same class.
* similar to type aliases, units of measure must be erased.

This list is not exhaustive and basically a todo-list for implementing individual test-cases.

# Drawbacks
[drawbacks]: #drawbacks

There is non-orthogonality between object expressions which implement an interface once (can use type unknowns) and multiple times (all type parameters must be specified).

# Alternatives
[alternatives]: #alternatives

The existing workaround is to add one extra hierarchy level per generic instantiation.

# Unresolved questions
[unresolved]: #unresolved-questions

None
