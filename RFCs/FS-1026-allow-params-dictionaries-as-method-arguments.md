# F# RFC FS-1026 - Allow "params" dictionary as method argument

The design suggestion [Allow "params" dictionaries as method arguments](https://fslang.uservoice.com/forums/245727-f-language/suggestions/5975840-allow-params-dictionaries-as-method-arguments) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] [User Voice Request](https://fslang.uservoice.com/forums/245727-f-language/suggestions/5975840-allow-params-dictionaries-as-method-arguments)
* [ ] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/164)
* [ ] Implementation: Not started.

# Summary
[summary]: #summary

Introduce an argument attribute `ParamDictionary` similar to `ParamArray` which allows callers to pass in an arbitrary number of named optional arguments:

```fsharp
open System
open System.Collections.Generic
open System.Runtime.CompilerServices

type C() =
  static member DoSomething1([<ParamArray>] args: 'T[]) = ()
  static member DoSomething2([<ParamDictionary>] args: IDictionary<string,obj>) = ()

C.DoSomething2(arg1=1, arg2=3)
C.DoSomething2(anotherarg=10.0, yetanother="foo")
```

Ideally this would work for type providers too, i.e. methods with this kind of argument can be provided.

# Motivation
[motivation]: #motivation

When interoperating with languages that are more dynamic than F# (like Python, Matlab or R), we can often get the list of available methods/functions, but we cannot always get the names and types of parameters. For example, in R provider, you sometimes have to write:

```fsharp
namedParams [ ("xval", days), ("yval", prices), ("another", box 1) ]
|> R.plot
```

It would be nice if I could put the `ParamDictionary` attribute on a `IDictionary<string, 'T>` and let the compiler provide all additional named parameters as a dictionary:

```fsharp`
R.plot(xval=days, yval=prices, another=1)
```

This has other uses - for example, when creating a Deedle data frame, you need to specify names of columns. Deedle a custom operator and function for that:

```fsharp
frame [ "prices" => prices; "days" => days ]
```

But with the new feature, we could let you write:

```fsharp
Frame.ofColumns(prices = [...], days, = [...])
```

This is not only shorter and more readable, but also makes this syntax uniform across libraries.

# Detailed design
[design]: #detailed-design

There are a number of moving parts to this, implementation could be quite subtle.

The easy part is adding a new attribute class called `ParamDictionaryAttribute` to FSharp.Core.

Then we'll describe:
1. Interaction with existing kinds of arguments - in particular positional arguments, named arguments, optional arguments, `ParamArray` and setting fields or properties on method results.
2. Expansion of the method application resolution rules (section 14.4 of the F# 4.0 spec - point d refers to `ParamArray`).
3. Elaboration of method call code to pack a number of named arguments into a dictionary and pass them as such.

## Interaction with existing kinds of arguments

In this part we will ignore overload resolution - that's for the next paragraph.

### At the definition site

The following "kinds" of arguments allowed in F# at the definition site are relevant:
1. Positional arguments
2. Optional arguments
3. `ParamArray` arguments

It's instructive to look at what is currently allowed:

```fsharp
type Ex() =
    //ok.
    static member A(a:string, b:float, ?opt:int) = ()
    //error: Optional arguments must come at the end of the argument list,
    //after any non-optional arguments.
    static member B(a:string, b:float, ?opt:int, [<ParamArray>] rest:string[]) = ()
    //ok.
    static member C(a:string, b:float, [<ParamArray>] rest:string[], ?opt:int) = ()
    //ok - note can only pass rest argument as explicit array..
    static member D(a:string, b:float, [<ParamArray>] rest:string[], [<ParamArray>] rest2:string[]) = ()
    //ok - but makes little sense since a is not even an array type.
    static member E([<ParamArray>] a:string, b:float) = ()
```
In summary as long as the optional arguments come last, the compiler doesn't complain. Checking of `ParamArray` attributes at the definition site are very lax.

A similarly lax approach allows the `ParamDictionary` anywhere on any argument regardless of its type, and regardless of the other parameters, except for the existing check that optional arguments always need to come last.

(If some more checking is desired around this I suggest it is defined in a separate RFC.)

[fair-use]: #fair-use
Regardless of whether they cause a compiler error or warning, to limit the design possibilities we assume the following "fair use"  restrictions (these are up for discussion):
* `ParamDictionary` argument comes last.
* There should be only one `ParamDictionary` argument.
* `ParamDictionary` argument can't be mixed with `ParamArray` arguments.
* The type of the formal argument should be `IDictionary<string,'T>`.

Typically the effect of one or more of these not holding, is that the `ParamDictionary` argument needs to be passed as an explicit dictionary instead of using the named argument syntax, or in other words the attribute is ignored.

### At the call site

At the call site it needs to be checked that all the given named arguments have a unique name.

We need to consider how `ParamDictionary` interacts with passing named arguments (optional or not), and with calling setters on results.

In order of decreasing preference, named arguments are passed as:
1. formal (maybe optional) arguments,
2. dictionary arguments,
3. fields/setters on the result type.

For example:

```fsharp
type Foo() =
    member val P1 = 1 with get,set
    member val P2 = 1 with get,set

type S() =
    static member A() : Foo = new Foo()
    static member B([<ParamDictionary>] args:IDictionary<string,int>) : Foo = new Foo()
    static member C(P1:int, b:int, [<ParamDictionary>] args:IDictionary<string,int>) : Foo = new Foo()
    static member D(P1:int, b:int, [<ParamDictionary>] args:IDictionary<string,int>, ?P2:int) : Foo = new Foo()

S.A(P1=3, P2=4) //calls A() then setter P1 and P2 on Foo
S.B(P1=3, P2=4) //calls B(dict ["P1",3; "P2",4])
S.C(P2=5, P1=3, b=4) //calls C(3, 4, dict ["P2",5])
S.D(P2=5, P1=3, b=4) //calls C(3, 4, dict [], 5)
```

A dictionary argument is always optional, in which  case an empty dictionary is passed in by the compiler:

```fsharp
S.B() //calls B(dict [])
S.C(P1=3, b=4) //calls C(3, 4, dict [])
```

Also, it must in all cases be possible to pass a dictionary explicitly, i.e. to avoid using the named argument syntax.

```fsharp
S.B(dict[ "1",1])
S.C(P1=3, args=dict[ "1",1], b=4)
S.D(P2=5, P1=3, b=4, args=dict["1",1])
```

## Expansion of method application rules

TBD

## Elaboration of method calls

The translation of the named arguments into a dictionary happens at the call sites.

Calling code can be translated as follows.

```fsharp
type S() =
    static member A([<ParamDictionary>] args:IDictionary<string,obj>) : () = ()

S.A(a=1, b="2",c=3.0)
//becomes:
let createDict() =
  let tmp = new Dictionary<string,obj>()
  tmp.Add("a", 1 :> obj)
  tmp.Add("b", "2" :> obj)
  tmp.Add("c", 3.0 :> obj)
  tmp
S.A(createDict())
```
Note that this allocates a new dictionary each time, which is necessary because IDictionary is a mutable type. This may cause performance surprises in certain situations.

The local function is introduced so that the evaluation order stays right to left:

```fsharp
type S() =
    static member B(x:int, [<ParamDictionary>] args:IDictionary<string,obj>) : () = ()

S.A(3+5, a=1+3, b="2",c=3.0*2.0)
//becomes:
let createDict() =
  let tmp = new Dictionary<string,obj>()
  tmp.Add("a", 1 + 3:> obj)
  tmp.Add("b", "2" :> obj)
  tmp.Add("c", 3.0 * 2.0 :> obj)
S.A(3+5, createDict())
```
And it is clear that `1+3` and `3.0 * 2.0` are evaluated after `3+5`. If we'd just create the dictionary and add the arguments, the dictionary arguments would be evluated before the formal argument `x`. In this case it makes no observable difference, but it's easy to construct a case where the difference is observable.

# Drawbacks
[drawbacks]: #drawbacks

Since this introduces a new attribute, libraries would need to take a dependency on an updated FSharp.Core to use this feature.

As can be seen in the detailed design, interaction with existing features is subtle. It is possible that some unforeseen interactions appear in the wild.

# Alternatives
[alternatives]: #alternatives

* An alternative is to make the type of the argument `KeyValuePair<string,'T>[]` instead of a dictionary, and attribute it with `ParamArray` instead of a new attribute. This would avoid introducing a dependency on an updated FSharp.Core, and would also capture the ordering of the arguments at the call site (although it would be weird if two calls with identical arguments but different ordering would give a different result.)
* Another suggestion is to use a type of `ExpandoObject` to improve interop with C#'s dynamic and the DLR, but that would require at least .NET 4.0.

# Unresolved questions
[unresolved]: #unresolved-questions

* Are [the "fair use" constraints](#fair-use) appropriate?
* It's not clear what needs to happen for this to be supported on provided methods - is providing a method with the required signature and attribute enough or is a compiler modification needed?
