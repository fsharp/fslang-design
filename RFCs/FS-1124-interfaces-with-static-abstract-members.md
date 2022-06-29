# F# RFC FS-1124 - Interfaces with static abstract members (IWSAMs)

The design suggestion [Support static abstract members in interfaces](https://github.com/fsharp/fslang-suggestions/issues/1151) has been marked "approved in principle". This RFC covers the detailed proposal for this suggestion.

Two other suggestions/RFCs are effectively incorporated into this RFC:
* [Allow static constraints to be named and reused](https://github.com/fsharp/fslang-suggestions/issues/1089)
* [Simplify constrained call syntax](https://github.com/fsharp/fslang-design/blob/main/RFCs/FS-1024-simplify-constrained-call-syntax.md)

* Discussion: https://github.com/fsharp/fslang-design/discussions/677
* Implementation: [In progress](https://github.com/dotnet/fsharp/pull/13119)

## Summary
[summary]: #summary

.NET 7 and C# 11 are [adding the ability to define interfaces with static abstract members](https://github.com/dotnet/csharplang/blob/main/proposals/static-abstracts-in-interfaces.md) and to use these from generic code.  

To match this in F#, we add the capability to specify abstract static members that implementing classes and structs are then required to provide an explicit
or implicit implementation of. The members can be accessed off of type parameters that are constrained by the interface.

## Motivation
[motivation]: #motivation

See motivation at https://github.com/dotnet/csharplang/issues/4436 and https://github.com/dotnet/csharplang/blob/main/proposals/static-abstracts-in-interfaces.md.

Static abstract members allow statically-constrained generic code. This is being utilised heavily in the [generic numeric code](https://visualstudiomagazine.com/articles/2022/03/14/csharp-11-preview-feature.aspx) library feature of .NET 7.

This feature sits uncomfortably in F#.  Its addition to the .NET object model has been driven by C#, and its use in .NET libraries, and thus consuming and, to some extent, authoring IWSAMs is necessary in F#.  However there are many drawbacks to its addition, documented below.

Because of this, we will require a special opt-in before new IWSAMs are declared and implemented in F#. The form of this opt-in is TBD.

## Considerations

F# has an existing mechanism for statically-constrained generic code called SRTP (statically resolved type parameters). These have considerable advantages and disadvantages:
* SRTP constraints can only be used in inlined code.
* SRTP constraints are "structural", that is they do not relate to any particular nominal interface.
* Many SRTP constraints such as `op_Addition` are special to the F# compiler, and F# retrofits solution witnesses for these constraints onto base types such as `System.Double`.  It also adjusts the solving of these constraints for units-of-measure.

This RFC must consider any interactions between SRTP constraints and static interface constraints.  

## Detailed design
[design]: #detailed-design

Interface types (defined or imported) can define static abstract members.  These types are called "Interfaces with static abstract members" or IWSAM for short.

Interfaces with static abstract members can be implemented by classes, structs, unions and records.

Generic code can be written where generic parameters are constrained by these interfaces.

### Implementing IWSAMs

A class or struct can declare implementations of interfaces with static abstract members. The requirements and inference/checking rules are the same as for instance members.

```fsharp
type IAdditionOperator<'T> =
    static abstract op_Addition: 'T * 'T -> 'T

type C() =
    interface IAdditionOperator<C> with
         static member op_Addition(x: C, y: C) = C()
```

Static abstract methods may not be declared in classes.

### Invoking constraint members

The syntax of expressions is extended with
```fsharp
    'T.<identifier>
    ^T.<identifier>
```

> NOTE: The status of `^T.<identifier>` is being evaluated.  In some cases, parenthesization may be needed.

`typar.M` is resolved to either

* A static abstract method when `'T` is constrained by an interface `I` and `M` is an accessible static abstract member of `I`.
  These are processed as normal member calls.


  ```fsharp
  let someFunction<'T when 'T : I<'T>>() =
      'T.M()
      let t = 'T.P
      t + 'T.P
  ```

* A static member trait constraint when `'T` is constrained by an SRTP constraint with name `M`. Constrained calls on SRTP constraints are limited. Property and member calls are allowed:

  ```fsharp
  let inline f_StaticProperty<^T when ^T : (static member StaticProperty: int) >() : int = ^T.StaticProperty

  let inline f_StaticMethod<^T when ^T : (static member StaticMethod: int -> int) >() : int = ^T.StaticMethod(3)
  ```

  However indexing, slicing and property-setting are not allowed and explicit forms must be used instead.  This is because SRTP constraints calls are not processed using method overloading rules.

  ```fsharp
  let inline f_set_StaticProperty<^T when ^T : (static member StaticProperty: int with set) >() =
      ^T.set_StaticProperty(3)

  let inline f_set_Length<^T when ^T : (member Length: int with set) >(x: ^T) =
      x.set_Length(3)

  let inline f_Item1<^T when ^T : (member Item: int -> string with get) >(x: ^T) =
      x.get_Item(3)
  ```

### Interfaces with static abstract members are constraints, not types

Interfaces with static abstract members should never generally be used as **types**, but rather as **constraints on generic type parameters**.

TBD: decide if/how extensively we enforce this.

As one concrete instance, when a type parameter `'T` is constrained by an interface that has static abstract members, any instantiation of `'T` must be either a class, struct or constrained type parameter. It may not be an interface.

For instance:

```fsharp
type IAdditionOperator<'T> =
    static abstract op_Addition: 'T * 'T -> 'T

let someFunction<'T when 'T : IAdditionOperator<'T>>() = 1
someFunction<C>();  // Allowed: C is not an interface
someFunction<IAdditionOperator<C>>(); // Disallowed: I is an interface and is here being used as a type, not a constraint on a generic type parameter.
```

The specific condition above must be checked for soundness.  Another example of a misuse is:

```fsharp
let badFunction(x: IAdditionOperator<C>) = 1   // This is not useful code
```

This code is useless as the static addition operator may not be invoked via the object `x`.  However because interfaces may contain both static abstract methods and instance abstract methods, it is likely that we won't specifically rule out writing the code above. Equally, methodologically it is almost certainly wise to distinguish between IWSAMs (constraining types) and interfaces with instance members (constraining types and values).

### Interaction with SRTP constraints

If a generic type parameter is constrained by an IWSAM, then

1. The type parameter is considered a statically-known, nominal type for the purposes of SRTP resolution.
2. The static members from the interface can be used as solutions for any SRTP constraints.

```fsharp
type IAdditionOperator<'T> =
    static abstract op_Addition: 'T * 'T -> 'T
   
type IZeroProperty<'T> =
    static abstract Zero: 'T

let someFunction1<'T when 'T : IAdditionOperator<'T>>(x: 'T, y: 'T) = x + y

let someFunction2<'T when 'T : IZeroProperty<'T>>() = LanguagePrimitives.GenericZero<'T>
```

Note that in these examples neither function is inlined.  The non-static type parameter `'T` is considered a type suitable for static resolution of the SRTP constraint.

### Interaction with object expressions

Object expressions may not be used to implement interfaces that contain static abstract methods.  This is because the only use for such an implementation is to pass as a type argument to a generic construct constrained by the interface.

### Self type constraints

A new syntax shorthand for self-constraints is added to F#. Specifically 

```fsharp
let f<'T when IComparable<'T>>() =
```
can be used.

The meaning is identical to 
```fsharp
let f<'T when 'T :> IComparable<'T>>() =
     ...
```
The type must be instantiated with a generic type parameter in first position. 

### Abbreviations that name collections of constraints

F# already allows the definition of abbreviations that "attach" constraints to types. For example an abbreviation that attaches the operations required for `List.average` can be defined like this.:

```fsharp
type WithAverageOps<^T when ^T: (static member (+): ^T * ^T -> ^T)
                       and  ^T: (static member DivideByInt : ^T*int -> ^T)
                       and  ^T: (static member Zero : ^T)> = ^T
```

In this RFC, we allow using such abbreviations as self-constraints. For example:

```fsharp
type WithStaticProperty<^T when ^T : (static member StaticProperty: int)> = ^T
type WithStaticMethod<^T when ^T : (static member StaticMethod: int -> int)> = ^T
type WithBoth<^T when WithStaticProperty<^T> and WithStaticMethod<^T>> = ^T

let inline f<^T when WithBoth<^T>>() =
    let v1 = ^T.StaticProperty
    let v2 = ^T.StaticMethod(3)
    v1 + v2
```

The interpretation here is slightly different.  The abbreviation **must** have the form

```fsharp
type SomeAttachingAbbreviation<^T, ^U ... when constraints> = ^T
```

Then 
```fsharp
let inline f<^T, ^U when SomeAttachingAbbreviation<^T, ^U, ...>>() =
```
is interpreted as the inlining of `constraints` after substituting. The first actual type parameter must be a type variable.

### SRTP adjustments

SRTP constraints now give an error if they declare optional, inref, outref, ParamArray, CallerMemberName or AutoQuote atttributes on parameters.  These were ignored and should never have been allowed.

## Drawbacks
[drawbacks]: #drawbacks

This feature sits uncomfortably in F#.  Its addition to the .NET object model has been driven by C#, and its use in .NET libraries, and thus consuming and, to some extent, authoring IWSAMs is necessary in F#.  However there are many drawbacks to its addition, documented below.

### General drawbacks

Statically-constrained qualified genericity is strongly distortive of the practical experience of using a programming language, whether in personal, framework-building, team or community situations. The effect of these distortions are well known from:
* Standard ML in the 1990s (e.g. SML functors and "fully functorized programming")
* C++ templates
* Haskell (type classes and their many technical extensions, abstract uses, generalizations and intensely intricate community discussions)
* Scala (implicits, their uses and abuses)
* Swift (traits, their uses and abuses)

While beguilingly simple as language design elements, these features are contested in programming communities. Their presence is deeply attractive to programmers who desire them - in many cases they become part of the fundamental organisational machinery applied to code and composition. They are equally problematic for those who don't see overall value in the complexity their use brings. We should also note the relative success, simplicity and practical productivity of languages that omit these features including Elm, Go and even Python.

.NET has historically avoided this space. This was a deliberate decision in C# 2.0 by Anders Hejlsberg, who rejected proposals for statically-constrained genericity on the grounds of the complexity introduced v. the benefit achieved - a decision the initial author of this RFC (Don Syme) was involved with and agreed with. C# 11 and .NET 6/7 has since revisted this decision, primarily because reflective code of any kind is now considered more expensive in static compilation scenarios, and in C# reflection had frequently been used as a workaround for the absence of qualified genericity (other practical workarounds are available in F#, including the use of SRTP).

The following summarises the well-known drawbacks of these features, based on the author's experience with all of the above. Emphatic language is used to act as a corrective.

**Encouraging the Max-Abstraction impulse.** It is highly likely this feature will encourage the practice of "max-abstraction" in C# and F# - that is, using more and more abstraction (in this case over types constrained by IWSAMs) to try to get maximal code reuse, even at the expense of code readability and simplicity. This kind of programming can be enormously enjoyable - it seems to satisfy a powerful desire in the human mind to abstract and generalise, and almost never loses its attraction. However, these techniques can also be an enormous waste of time, as very often the amount of code successfully reused is small, while the complexity in learning, comprehending, using, debugging and code-reviewing the corresponding frameworks is high, and the frameworks are often fragile.

**Subsequent demands for more type-level computation.**  This feature will lead to demands for more features for type-level computation, giving ever more obscure code that is abstract, general and impenetrable. This in turn can give demand for more abstraction capabilities in the language. These will in turn feed the productivity-burning bonfire of max-abstraction.

**Subsequent demand for compiler support for type-level debugging, profiling etc.** Features in this space will lead to requests for tooling to support compile-time type-level computation (compile-time debugging, profiling etc.).  They can also be very difficult to debug at runtime too, due to the numerous indirections and concepts encountered in even simple generic code. Being landed in generic code using `TSelf: IMultiplicativeIdentity<TSelf,TResult>` may not be intuitive to a beginner programmer trying to multiply a number by one.

**A proliferation of micro-interfaces.** The use of nominal, declared interfaces means there will be a proliferation of interfaces, even to the granularity of one fully-generic interface for each method. The need to implement these explicitly will make the feature "rigid" and there will be complaints that the .NET Framework is not regular enough - that not enough interfaces are defined to allow maximally generic code, and that not enough types implement those interfaces. Knowing the role and meaning of these interfaces is a cost inflicted on more or less every .NET programmer - they can occur in error messages, and discussed in code review.

**No stable point in library design.**  How many micro-interfaces are "enough"?  And how generic are they? In truth there can never be enough of these - ultimately you end up with one method for every single categorizable concept in the entire .NET Framework. The appetite for such abstractions is never-ending, and risks defeating other reasonable goals in software engineering.

**The 'correct' degree of genericity becomes contested.** These features bring a cultural attitude that "code is better if it is made more generic". Some people will even claim such code is 'simpler' - meaning nothing more that it can be reused, and ignoring the fact that it is by no measure cognitively simpler. In code review, for example, one reviewer may say that the code submitted can be made more generic by constraining with  the `IBinaryFloatingPointIeee754<T>` interface instead of `INumber<T>` - regardless of whether this genericity is needed. Another reviewer may have different point of view,and suggest a refactoring into two parts, one constrained by `ILogarithmicFunctions<T>` and `IDecrementOperators<T>`, another suggests switching to `ISubtractionOperators<TSelf, TOther, TResult>`. Note there is no effective measure of "goodness" here except max-abstraction.  The end result is that the amount of genericity to use becomes unproductively contested.

> As an aside, for F# SRTP code, the degree of genericity is automically computed. For IWSAMs, in type-inferred languages with HM-type inference adjusting the amount of genericity is relatively simple. But the contested nature of generic code is still not productive outside of framework design.

**Compiler and tooling slow-downs on large interface lists.**  The presence of enormous generic interface lists can cause compiler slow-down. No one ever expected such lists of interfaces in .NET, this is a volcano of hidden complexity lying under the simple type `double` or `decimal`.

To show that the above drawbacks are not imagined, consider the `System.Double` type in .NET 7. This now has the following list of interfaces:
```csharp
public readonly struct Double :
    IComparable<double>
    IConvertible
    IEquatable<double>
    IParsable<double>
    ISpanParsable<double>
    System.Numerics.IAdditionOperators<double,double,double>
    System.Numerics.IAdditiveIdentity<double,double>
    System.Numerics.IBinaryFloatingPointIeee754<double>
    System.Numerics.IBinaryNumber<double>
    System.Numerics.IBitwiseOperators<double,double,double>
    System.Numerics.IComparisonOperators<double,double>
    System.Numerics.IDecrementOperators<double>
    System.Numerics.IDivisionOperators<double,double,double>
    System.Numerics.IEqualityOperators<double,double>
    System.Numerics.IExponentialFunctions<double>
    System.Numerics.IFloatingPoint<double>
    System.Numerics.IFloatingPointIeee754<double>
    System.Numerics.IHyperbolicFunctions<double>
    System.Numerics.IIncrementOperators<double>
    System.Numerics.ILogarithmicFunctions<double>
    System.Numerics.IMinMaxValue<double>
    System.Numerics.IModulusOperators<double,double,double>
    System.Numerics.IMultiplicativeIdentity<double,double>
    System.Numerics.IMultiplyOperators<double,double,double>
    System.Numerics.INumber<double>
    System.Numerics.INumberBase<double>
    System.Numerics.IPowerFunctions<double>
    System.Numerics.IRootFunctions<double>
    System.Numerics.ISignedNumber<double>
    System.Numerics.ISubtractionOperators<double,double,double>
    System.Numerics.ITrigonometricFunctions<double>
    System.Numerics.IUnaryNegationOperators<double,double>
    System.Numerics.IUnaryPlusOperators<double,double>
```

At this point, any reader should stop to consider carefully the pros and cons here.  Each and every new interface adds conceptual overhead, and what was previously comparatively simple and compelling has become complex and curious. This complexity is potentially encountered by any and all users of .NET - beginner users trained in abstract math seem particularly fond of such numeric hierarchies, and are drawn to them like a moth to the flame.  Yet these abstractions are useful only to the extent that writing generic code is successfully and regularly instantiated at many types - yet this is not known to be a significant real-world limiting problem for .NET today in practice, and for which many practical, enacpsulated workarounds exist.

### Drawback - Interfaces with static abstract methods will get misunderstood as types, not type-constraints

Consider this code:
```fsharp
let addThem (x: INumber<'T>) (y: INumber<'T>) = x + y
```

This code will not compile, and example error for corresponding C# code is shown below.

![image](https://user-images.githubusercontent.com/7204669/175952813-7f5c4c57-a0d8-422e-a1d5-29e37febd37f.png)

To understand why, note that the relevant static abstract method in the `INumber<'T>` hierarchy is this (simplified):
```fsharp
type IAdditionOperators<'T when 'T : IAdditionOperators<'T>> =
    static abstract (+): x: 'T * y: 'T -> 'T
```

The operator takes arguments of type `'T`, but the arguments to `addThem` are of type `INumber<'T>`, and not `'T` (`'T` implies `INumber<'T>` but not the other way around).  Instead the code should be as follows:

```fsharp
let addThem (x: 'T) (y: 'T) when 'T :> INumber<'T> = x + y
```

This is really very, very subtle - beginner users are often drawn to generic arithmetic, and any beginner will surely think that `INumber<'T>` can be used as a type for a generic number.  But it can't - it can only be used as a type-constraint in generic code. Perhaps analyzers will check this, or special warnings added.

Fortunately in F# people can just use the simpler SRTP code on most beginner learning paths:
```fsharp
let inline addThem x y = x + y
```

### Drawback - Type-generic code is less succinct and less general than explicit function-passing code

Type-generic code relying on IWSAMs (and SRTP) can only be used with types that satisfy the constraints. If the types don't satisfy, you have a lot of trouble.

```fsharp
type ISomeFunctionality<'T when 'T :> ISomeFunctionality<'T>>() =
    static abstract DoSomething: 'T -> 'T

let SomeGenericThing<'T :> ISomeFunctionality<'T>> arg = 
    //...
    'T.DoSomething(arg)
    //...

type MyType1 =
    interface ISomeFunctionality<MyType1> with
        static member DoSomething(x) = ...
       
type MyType2 =
    static member DoSomethingElse(x) = ...
       
SomeGenericThing<MyType1> arg1
SomeGenericThing<MyType2> arg2 // oh no, MyType2 doesn't have the interface! Stuck!
```

When the number of methods being abstracted over is small (e.g. up to, say, 10) an alternative is to do away with the IWSAMs and simply to pass a `DoSomething` function explicitly:

```fsharp
let SomeGenericThing doSomething arg =
    //...
    doSomething arg
    //...
```
with callsites:
```fsharp
type MyType1 =
    static member DoSomething(x) = ...
       
type MyType2 =
    static member DoSomethingElse(x) = ...

SomeGenericThing MyType1.DoSomething arg1
SomeGenericThing MyType2.DoSomethingElse arg2
```

Note that explicit function-passing code is shorter and more general - it works with both `MyType1` and `MyType2`.  In F# this kind of code is incredibly safe and succinct because of Hindley-Milner type inference - passing functions and making code generic are two of the very easiest things to do in F#, the language is almost made for exactly those activities.

For the vast majority of generic coding in F# explicit function-passing is perfectly acceptable, with the massive benefit that the programmer doesn't burn their time trying to create or use a cathedral of perfect abstractions. SRTP handles most other cases.

### Drawback - Implementations of static abstract methods are not parameterizable, they can't close over anything

F# is driven by parameterization, for example functions:

```fsharp
let f x = 
   ...
```

or classes:
```fsharp
type C(x) = 
   ...
```

These constructs are parameterizable: they can close over arbitrary new dependencies by adding them to the parameter lists. For example:

```fsharp
let f newArg x = 
   ...newArg...
```

or classes:
```fsharp
type C(newArg , x) = 
   ...
```


This is at the heart of F# programming. It is powerful because requirements can change: what is initially unparameterized may later need to become dependent on something new. In F#, when this happens, the adjustments are relatively straight-forward. That's the whole point.

It is obvious-yet-crucial that **implementations of static abstract methods are not parameterizable: they are static**. If an implementation of a static abstract method later needs something new, something unavailable from the inputs or global state, you are stuck.   Normal static methods can become instance methods in this situation, or take additional parameters.  But implementations of static abstract methods can't do this, since they **must be forever static** and **must always take exactly the necessary arguments**.  You are stuck, totally stuck. You literally have no way of plumbing information from A to B.

Another way of looking at this is that IWSAM implementations live at a different "level" than the rest of application - the level of static composition. This means using IWSAMs has some of the same characteristics as Standard ML functors or C++ templates, which partitions software into two layers - the core language and the composition framework. It also means that starting to use IWSAMs is a major risk within your own code, especially if you can't adjust the IWSAM definitions (e.g. are implementing framework IWSAMs): if at **any** later time part of your code becomes dependent on a new parameter, you likely have no choice but to entirely remove your use of IWSAMs. (If you can edit the IWSAM definitions you can adjust to regular interfaces. Or use a global mutable variable or thread local, ugh).

To see why this matters, let's continue the example above and assume `MyType1.DoSomething` needs a new parameter `newArg`.  As expected it now becomes an instance member. When using explicit function passing, the generic code doesn't need to change at all and can simply be reused:
```fsharp
type MyType1(newArg) =
    member _.DoSomething(x) = ...newArg...
       
let SomeEntryPoint newArg =
    ...
    let ctxt = MyType1(newArg)
    SomeGenericThing ctxt.DoSomething 1
    ...
```

This is simple capture and it is the routine way of propagating and tracking new requirements in F# or any other functional-object language. In contrast, if using IWSAMs, you are stuck (unless you have the ability to adjust `ISomeFunctionality` to become a non-IWSAM):

```fsharp
type MyType1 =
    interface ISomeFunctionality<MyType1> with
        static member DoSomething(x) = ...  // It is not possible to plumb `newArg` to `MyType1.DoSomething`

let SomeEntryPoint newArg =
    SomeGenericThing<MyType1> arg1 
```

As another example, consider `IParseable<T>`. Let's assume you have a set of 100 domain object classes using `IParseable<T>` and a framework to compose these. Now assume the specification of your parsing implementations changes so that, for example, there are now **two** textual formats you need to parse, and you want a parameter to control which ones are accepted.  In this case, there is literally  no way to communicate that control parameter to your 100 implementations of `IParseable<T>`. This means your composition framework built on `IParseable<T>` may become entirely useless to you, simply due to this one small unexpected change in requirement. You now have to remove all use of `IParseable<T>`, shifting to another technnique.  What's gone wrong?  Perhaps the initial use of `IParseable<T>` should carry a warning, saying "one day you or your users may  regret this, and have to undo everything you've done".

To recap, IWSAM implementations are not within the parameterizable "core" portion of the langauge. Plumbing parameters to IWASM implementations is not possible without changing IWSAM definitions. This means using existing abstractions in your own code exposes you to removal should your requirements change.

> ASIDE: The same problem applies to a very small extent when using some other C#/F# constructs such as operators, which must have static implementations.  However it is fairly routine to remove the use of these, and they rarely need to capture, and they do not participate in compositional framework design.

> NOTE: Haskell's type classes have this problem.  Scala's 'implicits' do **not** suffer this problem - implicit implementations can be local and can capture, something seen as adding major flexibility and built on long experience. F# SRTPs have this problem but are not widely used in user code.

### Drawback - Three ways to abstract

In F# there are now three mechanisms to do type-level abstraction: Explicit function passing, IWSAMs and SRTP.

Within F#, explicit function passing should generally be preferred. SRTP and IWSAMs can be used as needed.

**Explicit function passing:**

```fsharp
let f0 add x y =
    (add x y, add y x)

```

**SRTP**

```fsharp
let inline f0<^T when ^T : (static member Add: ^T * ^T -> ^T)>(x: ^T, y: ^T) = 
    let res1 = ^T.Add(x, y)
    let res2 = ^T.Add(x, y)
    res1, res2
```
> NOTE: this simplified syntax is part of this RFC

**IWSAMs**

```fsharp
type IAddition<'T when 'T :> IAddition<'T>> =
    static abstract Add: 'T * 'T -> 'T

let f0<'T when 'T :> IAddition<'T>>(x: 'T, y: 'T) =
    let res1 = 'T.Add(x, y)
    let res2 = 'T.Add(y, x)
    res1, res2
```

These have pros and cons and can actually be used perfectly well together:

|  **Technique** | **What constraints** | **Satisfying constraints** | **Limitations** |
|:----:|:----:|:----:|:----:|
| Explicit function/interface passing | No constraints | Find a suitable function/interface | None |
| IWSAMs | Interfaces with static abstract methods | The interfaces must be defined on the type |  The entire scope of the generic code is subject to the constraint |
| SRTP | Member trait constraints | Member must be defined on the type ([FS-1043](https://github.com/fsharp/fslang-design/blob/main/RFCs/FS-1043-extension-members-for-operators-and-srtp-constraints.md) proposes to extend these to extension members.) |  SRTP can only be used in inlined F# code |

## Alternatives

### Invoking static abstract member implementations directly

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

However, that begs the question about how we call `MyRepeatSequence.Next` "directly", without using constrained generic code to do it. For instance methods we don't have this problem because the user can always write 

```fsharp
     (someObject :> ISomeInterface).CallInterfaceMethod()
```

to make the interface call explicit. 

**Option A - expect people to make a call via a type parameter**

Option A is to do nothing, and expect users to write constrained generic code to make the call:

```fsharp
type MyRepeatSequence() =

    interface IGetNext<MyRepeatSequence> with
        static member Next(other) = MyRepeatSequence.Next(other)
...
let CallNext<'T when 'T : IGetNext<'T>> (str: 'T) =
    'T.Next(str)

CallNext<MyRepeatSequence>(str)
```

This option is a bit unfortunate as it means extra obfuscating generic helper functions when there is actually no generic code around.  It also means users have no way of manually inlining such a helper function, and it makes another case where you don't get "closure under substitution" for F# generic code (which also happens for some SRTP calls, where generic helpers are also needed).

**Option B - expect the type authors to resolve this by authoring an explicitly accessible method.**

Option B is to expect the type authors to resolve this by authoring an explicitly accessible method:

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


**Option C - make an exception for statics and have those be name-accessible.**

Option C is to make an exception for static abstract implementations and have those be name-accessible.

However ambiguities can arise in the name resolution if several different unrelated interfaces implement that same named method - we could likely resolve those ambiguities but it does expose us to this kind of problem in a different way than currently.

**Option D - give some kind of explicit call syntax**

```fsharp
    (MyRepeatSequence :> IGenNext<MyRepeatSequence>).Next(str)
```

This is a bit ugly and undiscoverable. However it has the huge advantage of allow very precise concretization of generic code, e.g. imagine the user writes:

```fsharp
let GenericMathCode<'T  when 'T : IMath<'T>> ( .... ) =
   blah //100s of lines
   'T.Sin(...)
   'T.Cos(...)
```

Then wants to accurately make this concrete to some specific type 

```fsharp
let ConcreteMathCode ( .... ) =
   blah //100s of lines
   (Double :> IMath<'T>).Sin(...)
   (Double :> IMath<'T>).Cos(...)
```

In this RFC we go with Option A+B, with the possibility of adding Option D at some later point. 


## Compatibility
[compatibility]: #compatibility

* Is this a breaking change?

No.

## Unresolved questions
[unresolved]: #unresolved-questions

* [ ] Decide the relationship between this and [RFC-1024](https://github.com/fsharp/fslang-design/blob/main/RFCs/FS-1024-simplify-constrained-call-syntax.md)

* [ ] Check carefully against https://github.com/dotnet/csharplang/blob/main/proposals/static-abstracts-in-interfaces.md

* [ ] Decide if the feature must be explicitly enabled before declaring new IWSAMs.

* [ ] We need to look carefully at a few things - e.g. IWSAM that define op_Implicit and op_Explicit. @dsyme says: I've done "the right thing" in the code but we will need to test it.

* [ ] `^T.X` may have a conflict with `^expr` in [from-the-end-of-collection-slicing](https://github.com/fsharp/fslang-design/blob/main/preview/FS-1076-from-the-end-slicing.md); That feature is only in preview so we can change this if needed.

```yacc
  | INFIX_AT_HAT_OP declExpr
    { if not (parseState.LexBuffer.SupportsFeature LanguageFeature.FromEndSlicing) then 
        raiseParseErrorAt (rhs parseState 1) (FSComp.SR.fromEndSlicingRequiresVFive())
      if $1 <> "^" then reportParseErrorAt (rhs parseState 1) (FSComp.SR.parsInvalidPrefixOperator())
      let m = (rhs2 parseState 1 2)
      SynExpr.IndexFromEnd($2, m) }
```

* [ ] Carefully check this case.  For example, check that we do not apply the "condensation" rule that removes the implied type ariable in this case (we shouldn't)

```fsharp
let addThem (x: #INumeric<'T>) y = x + y
```

* [ ] Spec name resolution of `^T.Name` when `^T` has both SRTP and IWSAM members with the same name `Name` (SRTP is preferred)

* [ ] We have precedence woes for cases like this:


```fsharp
    let inline f<^T when ^T : (static member StaticMethod: int -> int)>() =
        ^T.StaticMethod(3)
        ^T.StaticMethod(3)
```

Here the `^` on the last line is causing the expression to be interpreted as an infix expression `a^b`. This is unfortunate - while there is a workaround of simply parenthesizing it is non-obvious.

```fsharp
    let inline f<^T when ^T : (static member StaticMethod: int -> int)>() =
        (^T.StaticMethod(3))
        (^T.StaticMethod(3))
```

A possible resolution is to require the use of `'T' for invocations or to use this as a workaround, e.g.
```fsharp
    let inline f<^T when ^T : (static member StaticMethod: int -> int)>() =
        'T.StaticMethod(3)
        'T.StaticMethod(3)
```
This actually works today (`^T` and `'T` are not considered different names).  



