# F# RFC FS-1124 - Interfaces with static abstract members 

The design suggestion [Support static abstract members in interfaces](https://github.com/fsharp/fslang-suggestions/issues/1151) has been marked "approved in principle". This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [ ] Discussion: use implementation PR please
* [ ] Implementation: [In progress](https://github.com/dotnet/fsharp/pull/13119)

## Summary
[summary]: #summary

.NET 7 and C# 11 are [adding the ability to define interfaces with static abstract members](https://github.com/dotnet/csharplang/issues/4436) and to use these from generic code.  

To match this in F#, we add the capability to specify abstract static members that implementing classes and structs are then required to provide an explicit
or implicit implementation of. The members can be accessed off of type parameters that are constrained by the interface.

## Motivation
[motivation]: #motivation

See motivation at https://github.com/dotnet/csharplang/issues/4436.

Static abstract members allow statically-constrained generic code. This is being utilised heavily in the [generic numeric code](https://visualstudiomagazine.com/articles/2022/03/14/csharp-11-preview-feature.aspx) library feature of .NET 7.

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

### Invoking static abstract interface members in generic code

The syntax of expressions is extended with
```fsharp
    'T.<identifier>
    ^T.<identifier>
```

A static abstract interface member `M` may be accessed on a type parameter `'T` using the expression `'T.M` when `'T` is constrained by an interface `I` and `M` is an accessible static abstract member of `I`.

```fsharp
let someFunction<'T when 'T : I<'T>>() =
    'T.M()
    let t = 'T.P
    return t + 'T.P
```

TBD: See also https://github.com/fsharp/fslang-design/blob/main/RFCs/FS-1024-simplify-constrained-call-syntax.md.  It is expected that this syntax will be usable for SRTP invocations in the case that the type parameter has been explicitly constrained with an SRTP constraint.

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

### Consideration: Invoking static abstract member implementations directly

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

#### Option B - expect the type authors to resolve this by authoring an explicitly accessible method

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

#### Option D - give some kind of explicit call syntax, e.g.

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

## Drawbacks
[drawbacks]: #drawbacks

This feature sits uncomfortably in F#.  Its addition to the .NET object model has been driven by C#, and its use in .NET libraries, and thus consuming and, to some extent, authoring IWSAMs is necessary in F#.  However there are many drawbacks to its addition, documented below.

Because of this, we will consider requiring a special opt-in before new IWSAMs are declared in F#.

Some of the drawbacks are as follows:

**Encouraging the Max-Abstraction impulse.** It is highly likely this feature will encourage the practice of "max-abstraction" in F# - that is, using more and more abstraction (in this case over types constrained by IWSAMs) to try to get maximal code reuse, even at the cost of massive loss of code readability and simplicity. This kind of programming can be enormously enjoyable - it seems to satisfy a primitive and powerful desire in the human mind to abstract and generalise, and almost never loses its attraction. Perhaps it even allows us to draw closer to the divine - a world of essences untainted by reality. However, back in the land of the real, it can also be an enormous waste of time, as very often the amount of code successfully reused is very low, while the complexity in comprehending, using, debugging, extending and code-reviewing the corresponding frameworks is high.

**Subsequent demands for more type-level computation.**  This feature will lead to extensive demands for more features for type-level computation, giving ever more obscure code that is abstract, general and impenetrable. This in turn can give demand for more abstraction capabilities in the language. These will in turn feed the productivity-burning bonfire of max-abstraction.

**Subsequent demand for compiler support for type-level debugging, profiling etc.** Features in this space will lead to requests for tooling to support compile-time type-level computation (compile-time debugging, profiling etc.).  Features in this space can also be very difficult to debug at runtime too, due to the numerous indirections and concepts encountered in even simple generic code.

**A proliferation of complexifying micro-interfaces.** The use of nominal, declared interfaces means there will be a proliferation of interfaces, even to the granularity of one fully-generic interface for each method.  The need to implement these explicitly will make the feature highly "rigid" and there will be a sea of complaints that the .NET Framework is not regular enough - that not enough interfaces are defined and that not enough types implement the those interfaces.

**No stable point in library design.**  How many micro-interfaces are "enough"?  And how generic are they? In truth there can never be enough of these - ultimately you end up with one method for every single categorizable concept in the entire .NET Framework. The appetite for such abstraction is never-ending, and largely futile, defeating other reasonable goals in software engineering.

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

At this point, any reader should stop to consider carefully the pros and cons here.  Each and every new interface adds conceptual overhead, and what was previously comparatively simple and compelling has become complex and curios. This complexity is potentially encountered by any and all users of .NET - beginner users trained in abstract math seem particularly fond of such numeric hierarchies, and are drawn to them like a moth to the flame.  Yet these abstractions are useful only to the extent that writing generic math code is successfully and regularly instantiated at many types - yet this is not known to be a significant real-world limiting problem for .NET today in practice.

**Two ways to abstract.** One specific drawback applies to F# - there are now two mecahnisms to do type-level abstraction of patterns, ISWAMs and SRTP.

ISWAM: 

```fsharp
type IAddition<'T when 'T :> IAddition<'T>> =
    static abstract Add: 'T * 'T -> 'T

let f1<'T when 'T :> IAddition<'T>>(x: 'T, y: 'T) =
    'T.Add(x, y)
```

SRTP: 
```fsharp
let inline f2<'T when 'T : (static member Add: 'T * 'T -> 'T)>(x: 'T, y: 'T) = 
    'T.Add(x, y)
```

These have pros and cons and can actually be used perfectly well together:
* **What is constrained.** ISWAM constrain the interfaces on the type, while SRTP constrains the members.
* **Satisfying constraints.** ISWAM require the interface be defined in the type. This massively restricts their use, and effectively makes them primarily usable by the BCL team. SRTP also only operate on the intrinsically defined members, though FS-1043 proposes to extend these to extension members.
* **What can be generic.** SRTP can only be used in inlined F# code. This is currently enforced. For non-inlined code SRTP will only ever be implemented via witness passing. SRTP cannot realistically be used on the generic parameters of types.
* **Corner cases.** SRTP has many unusual rules for operators, and there are several known bugs with the mecahnism

Within F#, this means those seeking more type-level abstraction will likely be spending considerable time arguing about whether to use IWSAM or SRTP.

## Alternatives
[alternatives]: #alternatives

Not add anything

## Compatibility
[compatibility]: #compatibility

* Is this a breaking change?

No.

## Unresolved questions
[unresolved]: #unresolved-questions

* [ ] Decide the relationship between this and https://github.com/fsharp/fslang-design/blob/main/RFCs/FS-1024-simplify-constrained-call-syntax.md

* [ ] Check carefully against https://github.com/dotnet/csharplang/blob/main/proposals/static-abstracts-in-interfaces.md

* [ ] Decide if the feature must be explicitly enabled before declaring new IWSAMs.

* [ ] We need to look carefully at a few things - e.g. IWSAM that define op_Implicit and op_Explicit. I've done "the right thing" in the code but we will need to test it.

* [ ] This:

```
Just to note ^T.X   may have a conflict with ^expr in from-the-end-of-collection-slicing.  

  | INFIX_AT_HAT_OP declExpr
    { if not (parseState.LexBuffer.SupportsFeature LanguageFeature.FromEndSlicing) then 
        raiseParseErrorAt (rhs parseState 1) (FSComp.SR.fromEndSlicingRequiresVFive())
      if $1 <> "^" then reportParseErrorAt (rhs parseState 1) (FSComp.SR.parsInvalidPrefixOperator())
      let m = (rhs2 parseState 1 2)
      SynExpr.IndexFromEnd($2, m) }
That feature is only in  preview so we can change this if needed
```
