# F# RFC FS-1124 - Interfaces with static abstract members (IWSAMs)

The design suggestion [Support static abstract members in interfaces](https://github.com/fsharp/fslang-suggestions/issues/1151) has been marked "approved in principle". This RFC covers the detailed proposal for this suggestion.

* [Discussion](https://github.com/fsharp/fslang-design/discussions/677)
* Implementation: [In progress](https://github.com/dotnet/fsharp/pull/13119)

Two other suggestions/RFCs are effectively incorporated into this RFC:
* [Allow static constraints to be named and reused](https://github.com/fsharp/fslang-suggestions/issues/1089)
* [Simplify constrained call syntax](https://github.com/fsharp/fslang-design/blob/main/RFCs/FS-1024-simplify-constrained-call-syntax.md)

## Summary

.NET 7 and C# 11 are [adding the ability to define interfaces with static abstract members](https://github.com/dotnet/csharplang/blob/main/proposals/static-abstracts-in-interfaces.md) and to use these from generic code.  

To match this in F#, we add the capability to specify abstract static members that implementing classes and structs are then required to provide an explicit
or implicit implementation of. The members can be accessed off of type parameters that are constrained by the interface.

## Motivation

See motivation at https://github.com/dotnet/csharplang/issues/4436 and https://github.com/dotnet/csharplang/blob/main/proposals/static-abstracts-in-interfaces.md.

Static abstract members allow statically-constrained generic code. This is being utilised heavily in the [generic numeric code](https://visualstudiomagazine.com/articles/2022/03/14/csharp-11-preview-feature.aspx) library feature of .NET 7.

This feature sits uncomfortably with F# methodology. Its addition to the .NET object model has been driven by C#, and its use in .NET libraries, and thus consuming and, to some extent, authoring IWSAMs is necessary in F#.  However there are many drawbacks to its addition, documented below. Because of this, we will emit a warning when IWSAMs are declared and implemented in F#. This warning can be suppressed.

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

Static abstract methods may not be declared in classes, abstract classes, or structs.

Static virtual methods can be called directly on interfaces.

Static abstract methods may not be called directly on interfaces.

A static abstract interface member can be accessed only on a type parameter.

### Invoking constraint members

The syntax of expressions is extended with
```fsharp
    'T.<identifier>
```

> NOTE: An earlier proposal also allowed  `^T.<identifier>`. However this was found to be a breaking change.

`'T.M` is resolved to either

* A static abstract method when `'T` is constrained by an interface `I` and `M` is an accessible static abstract member of `I`.
  These are processed as normal member calls, or
  the most derived static method when interface `I` provides a default implementation of `M` (i.e. a virtual static method).


  ```fsharp
  let someFunction<'T when 'T : I<'T>>() =
      'T.M()
      let t = 'T.P
      t + 'T.P
  ```

* A static member trait constraint when `'T` is constrained by an SRTP constraint with name `M`. Constrained calls on SRTP constraints are limited. Property and member calls are allowed:

  ```fsharp
  let inline f_StaticProperty<'T when 'T : (static member StaticProperty: int) >() : int = 'T.StaticProperty

  let inline f_StaticMethod<'T when 'T : (static member StaticMethod: int -> int) >() : int = 'T.StaticMethod(3)

  let inline f_InstanceProperty<'T when 'T : (member InstanceMethod: int -> int) >(x: 'T) : int = x.InstanceProperty

  let inline f_InstanceMethod<'T when 'T : (member InstanceMethod: int -> int) >(x: 'T) : int = x.InstanceMethod(3)
  ```

  Indexing, slicing and property-setting are not allowed via SRTP constraints and explicit forms must be used instead.  This is because the syntax of SRTP constraint calls is not processed using method overloading rules.

  ```fsharp
  let inline f_set_StaticProperty<'T when 'T : (static member StaticProperty: int with set) >() =
      'T.set_StaticProperty(3)

  let inline f_set_Length<^T when 'T : (member Length: int with set) >(x: 'T) =
      x.set_Length(3)

  let inline f_Item1<'T when 'T : (member Item: int -> string with get) >(x: 'T) =
      x.get_Item(3)
  ```

* For `'T.Name`, if `'T` has both SRTP and IWSAM members  `Name` then the SRTP members are preferred.

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

### Constraint Abbreviations

F# already allows the definition of abbreviations that "attach" constraints to types. For example an abbreviation that attaches the operations required for `List.average` can be defined like this.:

```fsharp
type WithAverageOps<'T when 'T: (static member (+): 'T * 'T -> 'T)
                       and  'T: (static member DivideByInt : 'T * int -> 'T)
                       and  'T: (static member Zero : 'T)> = 'T
```

In this RFC, we allow using such abbreviations as self-constraints. For example:

```fsharp
type WithStaticProperty<'T when 'T : (static member StaticProperty: int)> = ^T
type WithStaticMethod<'T when 'T : (static member StaticMethod: int -> int)> = ^T
type WithBoth<^T when WithStaticProperty<'T> and WithStaticMethod<'T>> = ^T

let inline f<'T when WithBoth<'T>>() =
    let v1 = 'T.StaticProperty
    let v2 = 'T.StaticMethod(3)
    v1 + v2
```

The interpretation here is slightly different.  The abbreviation **must** have the form

```fsharp
type SomeAttachingAbbreviation<'T, 'U ... when constraints> = 'T
```

Then 
```fsharp
let inline f<'T, 'U when SomeAttachingAbbreviation<^T, 'U, ...>>() =
```
is interpreted as the inlining of `constraints` after substituting. The first actual type parameter must be a type variable.

### Warning when declaring IWSAMs

Because of the drawbacks and considerations concerning IWSAMs (see [Drawbacks](#drawbacks)), declaring an IWSAM will give the following warning:

```
warning FS3535: Declaring "interfaces with static abstract methods" is an advanced feature. See https://aka.ms/fsharp-iwsams for guidance. You can disable this warning by using '#nowarn "3535"' or '--nowarn:3535'.
```

### Warning when using IWSAMs as types

Interfaces with static abstract members should never generally be used as **types**, but rather as **constraints on generic type parameters**. As one concrete instance, when a type parameter `'T` is constrained by an interface that has static abstract members, any instantiation of `'T` must be either a class, struct or constrained type parameter. It may not be an interface. For instance:

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

Because of this, using an IWSAM in a non-generic-constraint position will give the following warning:

```
warning FS3536: This type is an interface with a static abstract method. These are normally used as type constraints in generic code, e.g. "'T when ISomeInterface<'T>" "'T when ISomeInterface<'T>" or "let f (x: #ISomeInterface<_>)". See https://aka.ms/fsharp-iwsams for guidance. You can disable this warning by using '#nowarn "3536"' or '--nowarn:3536'.
```

The syntactic places where this warning is **not** emitted are:

* When declaring implemented interfaces on types
* When declaring the right-hand-side of a type abbreviation
* When declaring a constraint `when 'T :> ISomeInterface<...>`
* When declaring a self-constraint `when ISomeInterface<...>`
* When declaring a flexible-constraint `#ISomeInterface<...>`

At all other locations the use of an IWSAM constraint results in the warning. Note this includes `typeof<ISomeInterface<...>>` - reflecting over IWSAM csontraints is sufficiently rare that this is not given a special dispensation.

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

Technically, the adjustments to constraint solving are as follows:

1. consider solving an SRTP constraint:

   ```fsharp
       ((tys) : static member Method: argty1 * ... * argtyN -> retty)
   ```

   Here `tys` may be length 1 or 2. We determine if any of `tys` has a matching constraint `ty :> ISomeInterface` with a static abstract member `Method`. If so, all such available methods are used during overload resolution.

2. If any of `tys` has a matching IWSAM constraint it is considered solved for constraint-resolution purposes. Further, it does not need static resolution.

3. Such a constraint is eligible for so-called "weak resolution" (that is, before all of `tys` are solved), if the first element of `tys` is the one with the matching constraint and static abstract method.

> NOTE: this rule allows IWSAM type information to be applied for the SRTP resolution as soon as the signficant `TSelf` type is determined, which is the common case. This is a practical tradeoff - in practice, `IWSAM` methods are rarely overloaded, and are "biased" towards the `TSelf` type. Allowing weak resolution 

Because of (2), type inference variables now only need static resolution when

1. They are one of the support types of an unsolved static member SRTP constraint, or
2. They are associated with a `printf` format string placeholder `%s`, `%d`
3. They are explicitly declared with `^T`

In particular, type inference variables no longer automatically get the "needs static resolution" flag, rather this condition now arises from the constraints they are associated with.  This means some type inference variables are now inferred to be "not needs static resolution". If a signature declares such a type variable to be "needs static resolution" an error is no longer given.

### Interaction with object expressions

Object expressions may not be used to implement interfaces that contain static abstract methods.  This is because the only use for such an implementation is to pass as a type argument to a generic construct constrained by the interface.

## Implementation slot signature inference

Implementation slot signature inference happens as for instance members.  For example:

```fsharp
type IAdditionOperator<'T> =
    static abstract op_Addition: 'T * 'T -> 'T

type C() =
    interface IAdditionOperator<C> with
        static member op_Addition(x, y) = C()
```

Here `x` and `y` are eagerly inferred to have type `C` because there is only one slot that is being implemented, and the types of the parameters for that slot are known.

### Interaction with Units of Measure

* Discussion: https://github.com/dotnet/fsharp/issues/12881#issuecomment-1081269462

* Discussion: https://github.com/dotnet/fsharp/issues/12881#issuecomment-1081292469

If no changes are made, `double<m>` is considered to implement:
```fsharp
IAdditionOperators<double<m>, double<m>, double<m>>
IMultiplyOperators<double<m>, double<m>, double<m>>
```

For addition that's fine, and means 

```fsharp
    let f (x: #IAdditionOperators<_,_,_>) = x + x

    f 3.0<m>
```

will return the right thing.  However 

```fsharp
    let f (x: 'T when 'T :> IMultiplyOperators<'T,'T,'T>) = x * x

    f 3.0<m>
```

will return `9.0<m>` instead of `9.0<m^2>`, because `op_Multiply` in `IMultiplyOperators<double<m>>` would, without doing anything, have signature

```fsharp
op_Multiply: double<m> * double<m> -> double<m>
```

To prevent this happening, a feature restriction will be used where unitized types are simply not considered to implement `System.Numerics.I*` interfaces at all.

| Type | Interfaces reported |
|:-----|:-----|
| `double` | `IAdditionOperators<double,double,double>` |
|  | `IMultiplyOperators<double,double,double>` |
|  | etc. |
| `double<m>` | None from System.Numerics.* |
|  | |
| `double<'u>` | None from System.Numerics.* |
|  |  |

See also "Alternatives for Units of Measure" below

### SRTP adjustments

SRTP constraints now give an error if they declare optional, inref, outref, ParamArray, CallerMemberName or AutoQuote attributes on parameters.  These were ignored and should never have been allowed.

## Drawbacks
[drawbacks]: #drawbacks

This feature sits uncomfortably in F#.  Its addition to the .NET object model has been driven by C#, and its use in .NET libraries, and thus consuming and, to some extent, authoring IWSAMs is necessary in F#.  However there are many drawbacks to its addition, documented below.

### General drawbacks

Statically-constrained qualified genericity is strongly distortive of the practical experience of using a programming language, whether in personal, framework-building, team or community situations. The effect of these distortions are well known from:
* Standard ML in the 1990s (e.g. SML functors and "fully functorized programming")
* C++ templates
* Haskell (type classes and their many technical extensions, abstract uses, generalizations and intensely intricate community discussions)
* Scala (implicits)
* Swift (traits)

While sometimes beautiful and simple as language design elements, these features are contested in programming communities. Their presence is deeply attractive to programmers who desire them - in many cases they become part of the fundamental organisational machinery applied to code and composition. They are equally problematic for those who don't see overall value in the complexity their use brings. We should also note the relative success, simplicity and practical productivity of languages that omit these features including Elm, Go and even Python.

.NET has historically avoided this space. This was a deliberate decision in C# 2.0 by Anders Hejlsberg, who rejected proposals for statically-constrained genericity on the grounds of the complexity introduced v. the benefit achieved - a decision the initial author of this RFC (Don Syme) was involved with and agreed with. C# 11 and .NET 6/7 has since revisted this decision, primarily because reflective code of any kind is now considered more expensive in static compilation scenarios, and in C# reflection had frequently been used as a workaround for the absence of qualified genericity (other practical workarounds are available in F#, including the use of SRTP).

The following summarises the well-known drawbacks of these features, based on the author's experience with all of the above. Emphatic language is used to act as a corrective.

**Encouraging the Max-Abstraction impulse.** Features in this space can encourage the practice of "max-abstraction" in C# and F# - that is, using more and more abstraction (in this case over types constrained by IWSAMs) to try to get maximal code reuse, even at the expense of code readability and simplicity. This kind of programming can be enormously enjoyable - it seems to satisfy a powerful desire in the human mind to abstract and generalise, and almost never loses its attraction. However, these techniques can also be an enormous waste of time, as very often the amount of code successfully reused is small, while the complexity in learning, comprehending, using, debugging and code-reviewing the corresponding frameworks is high, and the frameworks are often fragile.

**Subsequent demands for more type-level computation.**  Features in this space can lead to demands for more features for type-level computation, giving ever more obscure code that is abstract, general and impenetrable. This in turn can give demand for more abstraction capabilities in the language. These will in turn feed the productivity-burning bonfire of max-abstraction.

**Subsequent demand for compiler support for type-level debugging, profiling etc.** Features in this space can lead to requests for tooling to support compile-time type-level computation (compile-time debugging, profiling etc.).  They can also be very difficult to debug at runtime too, due to the numerous indirections and concepts encountered in even simple generic code. Being landed in generic code using `TSelf: IMultiplicativeIdentity<TSelf,TResult>` may not be intuitive to a beginner programmer trying to multiply a number by one.

**A proliferation of micro-interfaces.** For the specific case of IWSAMs, the use of nominal, declared interfaces means there will be a proliferation of interfaces, even to the granularity of one fully-generic interface for each method. The need to implement these explicitly will make the feature "rigid" and there will be complaints that the .NET Framework is not regular enough - that not enough interfaces are defined to allow maximally generic code, and that not enough types implement those interfaces. Knowing the role and meaning of these interfaces is a cost inflicted on more or less every .NET programmer - they can occur in error messages, and discussed in code review.

**No stable point in library design.**  For the specific case of IWSAMs, how many micro-interfaces are "enough"?  And how generic are they? In truth there can never be enough of these - ultimately you end up with one method for every single categorizable concept in the entire .NET Framework. The appetite for such abstractions is never-ending, and risks defeating other reasonable goals in software engineering.

**The 'correct' degree of genericity becomes contested.** Features in this space can bring a cultural attitude that "code is better if it is made more generic". Some people will even claim such code is 'simpler' - meaning nothing more that it can be reused, and ignoring the fact that it is by no measure cognitively simpler. In code review, for example, one reviewer may say that the code submitted can be made more generic by constraining with  the `IBinaryFloatingPointIeee754<T>` interface instead of `INumber<T>` - regardless of whether this genericity is needed. Another reviewer may have different point of view,and suggest a refactoring into two parts, one constrained by `ILogarithmicFunctions<T>` and `IDecrementOperators<T>`, another suggests switching to `ISubtractionOperators<TSelf, TOther, TResult>`. Note there is no effective measure of "goodness" here except max-abstraction.  The end result is that the amount of genericity to use becomes unproductively contested.

> As an aside, for F# SRTP code, the degree of genericity is automatically computed. For IWSAMs, in type-inferred languages with HM-type inference adjusting the amount of genericity is relatively simple. But the contested nature of generic code is still not productive outside of framework design.

**Compiler and tooling slow-downs on large interface lists.**  For the specific case of IWSAMs, the presence of enormous generic interface lists can cause compiler slow-down. No one ever expected such lists of interfaces in .NET, this is a volcano of hidden complexity lying under the simple type `double` or `decimal`. [3/6/2024: as prophesized, an example of how this feature led to this in real life can be seen [here](https://github.com/fsprojects/Fleece/issues/146)]

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

### Drawback - Type-generic code is less general than explicit function-passing code

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

F# is driven by explicit parameterization, for example functions:

```fsharp
let someFunction x = 
   ...x...
```

or classes:
```fsharp
type SomeClass(x) = 
   ...x...
```

These constructs are explicitly parameterizable and capture their parameters: whatever they return - or the methods they provide - can close over arbitrary new dependencies by adding them to the parameter lists. For example:

```fsharp
let someFunction newArg x = 
   ...x...newArg...
```

or classes:

```fsharp
type SomeClass(newArg , x) = 
   ...x...newArg...
```

This is at the heart of F# programming and all functional programming, and it is powerful and accurate. Additionally, requirements change: what is initially independent may later need to become dependent on something new. In F#, when this happens, 99% of the time you plumb a parameter through: perhaps organising them via tuples, or records, or objects. Either way the adjustments are relatively straight-forward. That's the whole point.

It is obvious-yet-crucial that **implementations of static abstract methods have a fixed signature and are static**. If an implementation of a static abstract method  later needs something new - something unavailable from the inputs or global state or implicit thread/task context - you are stuck. Normal static methods can become instance methods in this situation, or take additional parameters.  But implementations of static abstract methods can't become instance, and they can't take additional explicit parameters: since they **must be forever static** and **must always take specific arguments**.  You are stuck: you literally have no way of explicitly plumbing information from A to B. Your options are switching to a new set of IWSAMs (plus a new framework to compose them), or removing the use of IWSAMs and returning to the land of objects and functions.

To see why this matters, consider a basic `IParseable<T>` (the actual `IParseable<T>` has some additional options, see below).

```fsharp
type IParseable<'T> =
    static abstract Parse: string -> 'T
```

So far so good.  Now let's assume you have a set of 100 domain classes implementing `IParseable<T>` and you've written a framework to compose these, e.g. extension methods to generically parse arrays and grids of things, read files and so on - whatever the guidance says you've gone deep, really deep into the whole `IParseable<T>` thing and it feels good, really good. Each implementation looks like this:

```fsharp
type C =
    interface IParseable<C> with
        static abstract Parse(input) = ....
```

Now assume it's the last night of your project - you're submitting your code tomorrow - and the specification of your parsing changes so that, for several types your parsers now need to selectively and dynamically parse multiple versions at different points in the data stream  - say v1 and v2. You try to write this:

```fsharp
type C =
    interface IParseable<C> with
        static abstract Parse(input) =
            if version1 then 
                ....
            else
                ....
```

But how to get `version1` into `Parse`?  In this case, there is literally no way to explicitly communicate that parameter to those two implementations of `IParseable<T>`. This means your composition framework built on the IWSAM `IParseable<T>` may become useless to you, due to nothing but this one small (yet predictable) change in requirements. You will now have to remove all use of `IParseable<T>` and shift to another technnique, or else rely on implicit communication of parameters (global state, thread locals...). This is no theoretical exercise - format parsers often change, by necessity over time, and need variations.

What's gone wrong? Well, IWSAMs should never have been used for parsing domain objects - and a compositional framework should never use IWSAMs. But `IParseable<'T>` sounded so compelling to implement, didn't it? And those IWASMs combining them were so mathematically elegant and beautiful, weren't they? Well, none of that matters now. Specifically, **IWSAMs like `IParseable<T>` should only be implemented on types where the parsing implementation is forever "closed" and "incontrovertible"**. By this we mean that their implemententations will never depend on external information (beyond culture/date/number formatting, see below), and there will be no variations on how the implementations should act.

What should you do instead?  Well, you should always have used regular interfaces, objects and functions - the world of normal functional-object programming - that is, parser combinators. For composition you should use explicit composition of parser functions and objects. All this is standard functional-object programming. For example:

```fsharp
type Parser<'T> = Parser of (string -> 'T)

module Parsers =
    let SomeClassParser1 = Parser (fun s -> ...)

    let SomeClassParser2 = Parser (fun s -> ...)
    
    let SomeClassParser version =
        if version = V1 then SomeClassParser1 else SomeClassParser2
```

With this approach you can write and compose parsers happily - the parsers are first-class objects. You can also lift any value of type `T :> IParseable<T>` into a `Parser` and then compose happily. For example:

```fsharp
module Parsers =
    let LiftParseable<'T when IParseable<'T>> =
        Parser p.Parse

    let IntParser = LiftParseable<int>
    let DoubleParser = LiftParseable<double>
    
    let OverallParser = CombineParsers (IntParser, DoubleParser, SomeClassParser1)
```

See [FParsec](https://github.com/stephan-tolksdorf/fparsec) for a full high-performance compositional parser framework.

This doesn't make implementing `IParseable<'T>` wrong - it's just very important to understand that you should only implement it on types where the parsing implementation is forever "closed" and "incontrovertible", as defined above. And it's very important to understand that your composition frameworks should not use IWSAMs as the primary unit of composition.  See "Guidance" below.

Another way of looking at this is that, unlike actual functions and objects, IWSAM implementations live and are composed at a different "level" than the rest of application - the level of static composition with generics - they are immune to regular parameterization. This means using IWSAMs has some of the same characteristics as original Standard ML functors or C++ templates, both of which partition software into two layers - the core language and the composition language. Another way to look at it is to note that IWSAMs are not a first class thing - a function or method can't return an IWSAM implementation.

> Note: `IParseable` does have an *implicit* way of passing extra information, through the optional `IFormatProvider` argument. However in practice that's pretty much nightmare-unusable for such purposes as propagating information - using an bespoke `IFormatProvider` is notoriously difficult and these are best left only for numeric, date and culture formatting parameters. So in this section we are discussing *explicit* parameterization of additional information that informs the action of parsing domain objects.

> Note: It could be said "`IParseable` is only for parsing numbers, dates and so on, and it's simply not for parsing domain objects where parsing different variations requires significant code". That's fine. But you can see why people might think otherwise.

> Note: As [pointed out on twitter](https://twitter.com/Savlambda/status/1542141589551779841) it is possible to plumb "statically known" information to an IWSAM implementation by passing additional IWSAM constrained type parameters. This means doubling down on more type-level parameterization, and any further IWSAMs passed are subject to the same problems.

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
    let res1 = 'T.Add(x, y)
    let res2 = 'T.Add(x, y)
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
| IWSAMs | Interfaces with static abstract methods | The interfaces must be defined on the type |  IWSAM implementations are static |
| SRTP | Member trait constraints | Member must be defined on the type ([FS-1043](https://github.com/fsharp/fslang-design/blob/main/RFCs/FS-1043-extension-members-for-operators-and-srtp-constraints.md) proposes to extend these to extension members.) |  SRTP implementations are static. SRTP can only be used in inlined F# code. |

## Guidance 

A summary of guidance from the above:

* **Understand the inherent limitations of IWSAMs.** IWSAM implementations are not within the "core" portion of the F#: they are not first-class objects, can't be produced by methods and, can't be additionally parameterized. IWSAM implementations must be intrinsic to the type, they can't be added after-the-fact.

* **Don't give in to type-categorization impulse.**  With IWSAMs, you can happily waste years of your life carefully categorising all the concepts in your codebase. Don't do it. Throw away the urge to categorise. Forget that you can do it. It almost certainly isn't helpful to categorise the types in your application code using these.

* **Don't give in to max-abstraction impulse.**  With IWSAMs and other generic code, you can happily waste even more years of your life max-abstracting out every common bit of code across your codebase. Don't do it. Throw away the urge to max-abstract just for its own sake. Forget that you can do it, and if you try don't use IWSAMs, since using explicit function passing will likely result in more reusable generic code (see below).

* **Using IWSAMs in application code carries a strong risk you or your team will later remove their use.** Explicitly plumbing new parameters to IWSAM implementations is not possible without changing IWSAM definitions. Because of this, using IWSAMs exposes you to the open-ended possibility that you will have to use implicit information plumbing, or remove the use of IWSAMs. Given that F# teams generally prefer explicit information plumbing, teams will often remove the use of devices that require implicit information plumbing.  

* **Only implement IWSAMs on types where their implementations are stable, closed-form, and incontrovertible.** IWSAMs work best on highly stable types and operations where there is essentially no future possibility of requirements changing to include new parameters, dependencies or variations of implementation. This means that you should only implement IWSAMs on types where their implementation is forever "closed" and "incontrovertible" - that is, unarguable. Numerics are a good example: these types and operations are highly semantically stable, require little additional information and have very stable contracts. However your application code almost certainly isn't like this.

* **Do not use IWSAMs as the basis for a composition framework.**  You should not write composition frameworks using IWSAMs as the unit of composition. Instead, use regular programming with functions and objects for composition, and write helpers to lift leaf types that have IWSAM definitions into your functional-object composition framework. See examples above.

* **Prefer explicit function passing for generic code.** In F# there are now three mechanisms to do type-level abstraction: Explicit function passing, IWSAMs and SRTP. Within F#, when writing generic code, explicit function passing should generally be preferred. SRTP and IWSAMs can be used as needed. See examples above.

* **Go light on the SRTP.**  Some of the changes in this RFC enable nicer SRTP programming. Some of the above guidance applies to SRTP - for example do not use SRTP as a composition framework.

* **For generic math, use SRTP or IWSAM.** Generic math works well with either.  F# SRTP code is often quicker to write, requires less thought to make generic and has better performance due to inlining. If C#-facing, use IWSAMs for generic math code.

* **For generic math using units-of-measure, use SRTP.** The .NET support for generic math does not propagate units of measure correctly. Rely on F# SRTP code for these.

* **If defining IWSAMs, put static members in their own interface.**  Do not mix static and non-static interfaces in IWSAMs.

## Alternatives

### Alternatives for invoking static abstract member implementations directly

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

#### Option A - expect people to make a call via a type parameter

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

#### Option B - expect the type authors to resolve this by authoring an explicitly accessible method.

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


#### Option C - make an exception for statics and have those be name-accessible.

Option C is to make an exception for static abstract implementations and have those be name-accessible.

However ambiguities can arise in the name resolution if several different unrelated interfaces implement that same named method - we could likely resolve those ambiguities but it does expose us to this kind of problem in a different way than currently.

#### Option D - give some kind of explicit call syntax

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

#### Conclusion

In this RFC we go with Option A+B, with the possibility of adding Option D at some later point. 

### Alternatives for Invocation Syntax

`^T.X` has a conflict with `^expr` in [from-the-end-of-collection-slicing](https://github.com/fsharp/fslang-design/blob/main/preview/FS-1076-from-the-end-slicing.md). We have precedence woes for cases like this:


```fsharp
        call()
        ^T.call()
```

Here the `^` on the last line is causing the expression to be interpreted as an infix expression `a^b`. The resolution is to require the use of `'T' for invocationse.g.
```fsharp
    let inline f<^T when ^T : (static member StaticMethod: int -> int)>() =
        'T.StaticMethod(3)
        'T.StaticMethod(3)
```
Note that `^T` and `'T` are not considered different names.

### Alternatives for Units of Measure

We have five options:

1. Don't do anything and accept the unsoundness
2. Give a warning so that each time a unitized type is solves a constraint `'T :> `IMultiplyOperators<_,_,_>` for a specific set of interface types known to have this problem
3. Do not report any `System.Numerics.I*` interfaces for unitized types.
4. Adjust the F# compiler so that `double<m>` is considered to implement `IMultiplyOperators<double<m>,double<m>,double<m*m>` for a specific set of interface types
5. Erase 4. Ask the BCL team to add metadata about the unitization signatures of each of the numeric abstractions.

We decided to do (3).

##### Alternative: Unsound approaches to Units of Measure + IWSAM

A previous version of this RFC proposed to use rewriting of the interfaces considered to be supported by unitized types, e.g.

| Type | Interfaces reported |
|:-----|:-----|
| `double` | `IAdditionOperators<double,double,double>` |
|  | `IMultiplyOperators<double,double,double>` |
|  | etc. |
| `double<m>` | `IAdditionOperators<double<m>,double<m>,double<m>>` |
|  |`IMultiplyOperators<double<m>,double<m>,double<m^2>>`  |
|  | etc. |
| `double<'u>` | `IAdditionOperators<double<'u>,double<'u>,double<'u>>` |
|  |`IMultiplyOperators<double<'u>,double<'u>,double<'u^2>>`  |
|  | etc. |

Note the `m^2` on the `IMultiplyOperators`.  However, this technique is obviouisly flawed, because it only allows `double<m> * double<m>` and not, say `double<m> * double<kg>`.  The links above have further commentary.

[ Aside: This is an example of why nominal concept modelling of the kind used in `System.Numerics.*` is problematic: concepts are fragile as new genericity requirements are placed on them. ]


##### Alternative: Possible future options for Units of Measure + IWSAM generic math code

F# SRTP constraints allow the definition of code the is generic over both numeric type and, eventually, unit of measure.  

First, it's worth noting that the vast majority of .NET math code in .NET libraries is not unitized, e.g. `Math.Sqrt` is not unitized.  Only certain primitive like `*` are automagically given a unitized signature.  

For generic unitized math code, consider the following simple method:

```fsharp
let inline f x = x * x  + x * x
```

We can use this as follows:

```fsharp
> open FSharp.Data.UnitSystems.SI.UnitSymbols;;
> f 1.0<m>;;
val it: float<m ^ 2> = 1.0

> let g (x: double<'u>) = f x;;
val g: x: double<'u> -> float<'u ^ 2>
```

Let's constrain the types a little for simplicity:

```fsharp
> let inline f (x: 'T) : 'U =
    let v1 : 'U = x * x 
    let v2 : 'U = x * x
    v1 + v2;;
```

and examine the inferred types:

```fsharp
val inline f:
  x:  ^T ->  ^U
    when  ^T: (static member (*) :  ^T *  ^T ->  ^U) and
          ^U: (static member (+) :  ^U *  ^U ->  ^U)
```

Note that in the generic signature for `f: ^T ->  ^U`, the input and output types are different - yet also related by SRTP constraints:
The necessary unitized relationship is established when solving these constraints.  That is, when solving an SRTP constraint such as:

```fsharp
double<m> : (static member (*): '?1 * '?2 -> '?3)
```

the F# compiler rewrites the potential solution methods for a fixed number of operations, using the following target signature as the basis for a solution:

```fsharp
type double<m> with
    static member (*): double<m> * double<'?> -> double<m * '?>
```

and usually eventually some concrete type, e.g.

```fsharp
type double<m> with
    static member op_Multiply: double<m> * double<m> -> double<m ^ 2>
```

This is considered a "built in" trait solution and only applies to those on well-known primitive types such as `double` and `single`.

All this means SRTP generic math doesn't have the feature interaction described earlier: you can write generic math code that is unit-aware, and the constraints carry sufficient information to establish the necessary unitized relationships at each callsite.

It's worth asking why and seeing if the same can be applied to IWSAM, though it is well beyond the scope of this RFC to do so.  

For the generic case, the starting point is, say

```fsharp
let f (x: #INumber<'T>) = x * x + x * x;;

val f : 'T -> 'T when 'T :> INumber<'T>
```
The problem here is that the input and output types are identical.  Could they be made different-yet-related? That is difficult.

1. The rewrite is not simple - we would need to rewrite the actual `op_Multiply` signature provided by `IMultiplyOperators<_,_,_>` but establishing the relationship between the original unitized type and the signature is not possible. We'd need a new internal flag or concept here to establish this relationship 

2. There are many interfaces and methods in `System.Numerics` and they are not necessarily generic enough, e.g. [`IRootFunction::Sqrt` and `IRootFunctions::Cbrt`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Numerics/IRootFunctions.cs) have different unitizations.   We would need to go through each signature and establish the correct unitization.  It could be done but probably shouldn't be baked into the compiler, but rather be a new set of unit-friendly interfaces and metadata in FSharp.Core.  

3. The whole thing would be fragile to additions and changes in the `Systems.Numeric` methods and hierarchy.  We would have to at least wait until the final release of .NET 7

For (1), the possible communication of the extra information might happen as follows:

| Type | Interfaces reported |
|:-----|:-----|
| `double` | `IAdditionOperators<double,double,double>` |
|  | `IMultiplyOperators<double,double,double>` |
|  | etc. |
| `double<m>` | `IAdditionOperators<double[m],double['v],double[m*'v]>` |
|  |`IMultiplyOperators<double[1],double,double>[m]`  |
|  | etc. |
| `double<'u>` | `IAdditionOperators<double['u],double['u],double['u]>` |
|  |`IMultiplyOperators<double['u],double['v],double['u 'v]>` |
|  | etc. |

Here `[m]` is some kind of soft unit annotation used tracked through inference.  Then, when `IMultiplyOperators<_,_,_>::op_Multiply` is used, the annotations would come into play, rewriting:

```fsharp
IAdditionOperators<double[m],double['v],double[m 'v]>::op_Multiply: double -> double -> double
```
to
```fsharp
                                        ......op_Multiply: double<'m> -> double<'v> -> double[m 'v]
```

A unitizing rewrite would have to be added for each construct in System.Numerics.  Further the utility of this is limited unless it truly allows a way to write  generic math code that is generic both over type an unit.

Given the complexity of this, overall it is better for this RFC to simply say that unitized types are simply not considered to support the `System.Numeric.*` interfaces at all.  A halfway-house where they support some unit-uniform interfaces like `IAdditionOperators<double<m>, double<m>, double<m>` doesn't seem worth it, as in practice there will be almost no useful generic math code that is constrained only by `T : IAdditionOperators<T,TOther,TReturn>`, so what's the point?

Thus for unitized types we filter any interface that derives from anything in the `System.Numeric` namespace.


## Compatibility
[compatibility]: #compatibility

* Is this a breaking change?

No.

## Unresolved questions
[unresolved]: #unresolved-questions

* [ ] Document that IWSAMs that define op_Implicit and op_Explicit do not, practiclly speaking, result in implicit conversions being usable, because of F# restrictions on activating implicit conversions in the presence of generic type variables
* [ ] Document that some code becomes more generic, e.g.

  ```fsharp
  let inline f0 (x: ^T) = x
  let g0 (x: 'T) = f0 x
  ```
  
  now gives
  
  ```fsharp
  val g0: x: 'T -> 'T
  ```
  
  instead of
  
  ```fsharp
  val g0: x: obj -> obj
  ```
  
  This may in theory require signature files to be revised.

* [ ] Consider whether FSharp.Core should define log2, log2P1 etc.  If it's done it should use SRTP. Note these are based on the IEEE standard and that represents a max point of what we should have.

* [ ] Consider factoring out self constraints and collections-of-constraints into separate RFCs.
