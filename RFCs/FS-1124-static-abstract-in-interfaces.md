# F# RFC FS-1124 - Static abstract methods in interfaces

The design suggestion [Support static abstract methods in interfaces](https://github.com/fsharp/fslang-suggestions/issues/1151) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [ ] Details: [under discussion](FILL-ME-IN)
* [ ] Implementation: [In progress](https://github.com/dotnet/fsharp/pull/13119)

# Summary
[summary]: #summary

We add the capability to specify abstract static members that implementing classes and structs are then required to provide an explicit
or implicit implementation of. The members can be accessed off of type parameters that are constrained by the interface.

# Motivation
[motivation]: #motivation

See motivation at https://github.com/dotnet/csharplang/issues/4436

# Detailed design
[design]: #detailed-design

* Interface types can define static abstract methods, including imported interface types

* These can be implemented

* Generic code can be written where generic parameters are constrained by these interfaces.

## Implementing static abstract members

The rules for when a static member declaration in a class or struct is considered to implement a static abstract interface member, and for what requirements apply when it does, are the same as for instance members.

## Interface constraints with static abstract members are only satisfiable by classes, structs and constrained type parameters

When a type parameter `'T` is constrained by an interface that has static abstract members, any instantiation of `'T` must be either a class, struct or constrained type parameter.
It may not be an interface.

For instance:

```fsharp
// I and C as above
let someFunction<'T when 'T : I<'T>>() = 1
someFunction<C>();  // Allowed: C is not an interface
someFunction<I<C>>(); // Disallowed: I is an interface
```

## Invoking static abstract interface members in generic code

A static abstract interface member `M` may be accessed on a type parameter `'T` using the expression `'T.M` when `'T` is constrained by an interface `I` and `M` is an accessible static abstract member of `I`.

```fsharp
let someFunction<'T when 'T : I<'T>>() =
    'T.M();
    let t = 'T.P;
    return t + 'T.P
```

The syntax of expressions is extended with
```fsharp
    'T.<identifier>
    ^T.<identifier>
```



## Calling implemented functionality

Consider:
```csharp
namespace StaticsInInterfaces
{
    public interface IGetNext<T> where T : IGetNext<T>
    {
        static abstract T Next(T other);
    }
}
```
And then:      
```fsharp
type MyRepeatSequence() =
    interface IGetNext<MyRepeatSequence> with
        static member Next(other: MyRepeatSequence) : MyRepeatSequence = other 
```

Under the name resolution rules of F#, interface implementations are not available via the implementing type, so following that principle `MyRepeatSequence.Next` shouldn't resolve. 

However, that begs the question about how we call `MyRepeatSequence.Next`.   For instance methods we don't have this problem because the user can always write 

```fsharp
     (someObject :> ISomeInterface).CallInterfaceMethod()
```

to make the interface call explicit. 

### Option A - expect the type authors to resolve this by authoring an explicitly accessible method, e.g. like this:

```fsharp
type MyRepeatSequence() =
    static member Next(other: MyRepeatSequence) =
           <the implementation>

    interface IGetNext<MyRepeatSequence> with
        static member Next(other) = MyRepeatSequence.Next(other)
...
MyRepeatSequence.Next(str)
```

Note that C# code using implicit interface impls will automatically have such a method available.  We should check if most of the math stuff in System.* will use explicit or implicit impls - we assume implicit.

### Option B - expect people to make a call via a type parameter

This can be used in conjunction in Option A

```fsharp
type MyRepeatSequence() =

    interface IGetNext<MyRepeatSequence> with
        static member Next(other) = MyRepeatSequence.Next(other)
...
let CallNext<'T when 'T : IGetNext<'T>> (str: 'T) =
    'T.Next(str)

CallNext<MyRepeatSequence>(str)
```

This second option is a bit unfortunate as it means extra obfuscating generic helper functions when there is actually no generic code around.  It also means users have no way of manually inlining such a helper function, and it makes another case where you don't get "closure under substitution" for F# generic code (which also happens for some SRTP calls, where generic helpers are also needed).

### Option C - make an exception for statics and have those be name-accessible.

However ambiguities can arise in the name resolution if several different unrelated interfaces implement that same named method - we could likely resolve those ambiguities but it does expose us to this kind of problem in a different way than currently.

### Option D - give some kind of explicit call syntax, e.g.

```fsharp
    (MyRepeatSequence :> IGenNext<MyRepeatSequence>).Next(str)
```

This is a bit ugly and undiscoverable. However it has the huge advantage of allow very precise concretization of generic code, e.g. imagine the user writes:

```
GenericMathCode<'T  when 'T : IMath<'T>> ( .... ) {
   blah //100s of lines
   'T.Sin(...)
   'T.Cos(...)
}
```
Then wants to accurately make this concrete to some specific type 
```
ConcreteMathCode ( .... ) {
   blah //100s of lines
   (Double :> IMath<'T>).Sin(...)
   (Double :> IMath<'T>).Cos(...)
}
```


In this RFC we go with Option A+B, with the possibility of adding Option D at some later point. 


# Drawbacks
[drawbacks]: #drawbacks

It is highly likely this feature will be used and abused to perform type-level programming in F#.

# Alternatives
[alternatives]: #alternatives

Not add anything

# Compatibility
[compatibility]: #compatibility

* Is this a breaking change?

No.

# Unresolved questions
[unresolved]: #unresolved-questions

* Decide the relationship between this and https://github.com/fsharp/fslang-design/blob/main/RFCs/FS-1024-simplify-constrained-call-syntax.md

