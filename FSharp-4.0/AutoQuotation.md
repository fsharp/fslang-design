F# 4.0 Speclets related to Quotation Values

# Contents

- Speclet: Auto-Quotation of Arguments at Method Calls 

- Speclet: ValueWithName quotation nodes


# F# 4.0 Speclet: Auto-Quotation of Arguments at Method Calls ([Pull Request](https://visualfsharp.codeplex.com/SourceControl/network/forks/dsyme/cleanup/contribution/7638), [F# Language User Voice](https://fslang.uservoice.com/forums/245727-f-language/suggestions/5975797-allow-implicit-quotation-of-expressions-used-as-a)) 


### Aims

Add a facility to allow auto-quotation of arguments at method calls, in a way that is consistent with the existing “Auto-Lifting-To-LINQ-Expressions” facility of F# 3.0

## Background

F# 3.0 allows an F# TAST  LINQ Expression tree conversion for lambda method call arguments, similarly to C#. As in C# this is instigated by giving a method argument a type

    Expression<Func<_,_>>

The facility is used by LINQ APIs to “grab” micro-fragments of code as LINQ expression trees which are later stitched together into a larger meta-program (e.g. converted to an SQL query). In C#, the feature is a popular and successful way to blend a modicum of implicit meta-programming into APIs.

This “auto-expression” facility is an “interop-class” feature and has a somewhat awkward status in the F# language. 

-	It the only way to produce LINQ expression trees in the F# language implicitly, and indeed the only place where LINQ expression trees are referenced in the F# language specification at all.  

-	But given the overall design of F#, why do LINQ expression trees have a privileged position here?  After all, F# quotations are the “natural” meta-programming format for F# - supporting debugging annotations, richer quotations and the like. However there is no matching way to implicitly produce F# quotations implicitly at method calls. 

-	This means the F# API designer can be led towards a situation where it is natural to use LINQ expression trees for one thing, and F# quotations for others.  They should at least have the option of using F# quotations for everything.

### Design Overview 

In F# 4.0 we regularize this by also allowing the implicit auto-quotation of arguments at method calls.  The facility is instigated by adding the ReflectedDefinition attribute to a method argument of ``type FSharp.Quotations.Expr<_>``:

    static member Plot([<ReflectedDefinition>] values:Expr<int>) = (...)

The intention is that this gives an implicit quotation from X --> <@ X @> at the callsite. So for

    Chart.Plot(f x + f y)

the caller becomes:

    Chart.Plot(<@ f x + f y @>)

Additionally, the method can declare that it wants both the quotation and the evaluation of the expression, by giving "true" as the "includeValue" argument of the ``ReflectedDefinitionAttribute``.

    static member Plot([<ReflectedDefinition(true)>] values:Expr<X>) = (...)

Sofor

    Chart.Plot(f x + f y)

the caller becomes:

    Chart.Plot(Expr.WithValue(f x + f y, <@ f x + f y @>))

and the quotation value Q received by Chart.Plot matches:

    match Q with 
    | Expr.WithValue(v, ty) --> // v = f x + f y

### Design Detail: First-class uses of methods with auto-quotation arguments

-	Methods with ReflectedDefinition arguments may be used as first class values (including pipelined uses), but it will not normally be useful to use them in this way.  This is because, according to the F# spec, a first-class use of the method ``C.Plot`` is considered shorthand for ``(fun x -> C.Plot(x))`` for some compiler-generated local name “x”, which will become ``(fun x -> C.Plot( <@ x @> ))``, so the implicit quotation will just be a local value substitution.  This means a pipelines use ``expr |> C.Plot`` will _not_ capture a full quotation for ``expr``, but rather just its value.  

-	The same dissonance applies to C#-style auto-expression: if you pipeline a method accepting Expression<Func<_,_>> arguments, then you almost certainly won’t get the expression trees you expect. This is in effect part of the intrinsic cost of having an auto-quotation meta-programming facility in the language at all: auto-quotation is not a particularly “nice” mechanism, and like all quotation-style meta-programming it needs to be used carefully and rarely by API designers.  


### Design Detail: Notes on Applicability:

-	Auto-quotation of arguments only applies at method calls, and not function calls.  This is because method calls are used as the place in F# where conversions like this are supported

-	The conversion only applies if the called-argument-type is type Expr<T> for some type T, and if the caller-argument type is _not_ of the form Expr<U>  for any U.  

-	The caller-argument-type is determined as already documented in the F# spec, with the addition that a caller argument of the form ``<@ … @>`` is always considered to have a type of the form Expr<_>, in the same way that caller arguments of the form ``(fun x -> …)`` are always assumed to have type of the form ``_ -> _`` (i.e. a function type)

-	The ``this`` argument on C#-style extension methods may have a ReflectedDefinition argument, but this will be ignored when the method is used as an extension method (rather than being called directly).

-	Parameters on C# methods and provided methods may have the ReflectedDefinition attribute. 

-	WithName nodes will only be inserted into quotation data when using F# 4.0+ and targeting FSharp.Core 4.4.0.0+ (or equivalent F# 4.0+ PCL facades). 

-	Libraries declaring methods using the ReflectedDefinition feature must target FSharp.Core 4.4.0.0 or above (or equivalent F# 4.0+ PCL facades). This is because the "ReflectedDefinition" attribute has been updated to allow it to be used on parameters. That means the libraries will only be consumable using F# 4.0+. 

-	An early prototype of this feature allowed auto-quotation for arguments of type “FSharp.Quotations.Expr” (so-called “raw” or “dynamically-typed” quotations).  This has now been removed.

### Design Detail: Overloading of methods with auto-quotation arguments

-	Overloading of methods with auto-quotation arguments is resolved through the existing, normal F# overload resolution rules and gives results similar to overloads involving LINQ auto-expression arguments.  For example, you can overload by type:

    type FirstClassTests() = 
        // Not generally advisable: avoid using this kind of overloading
        static member PlotExprOverloadedByType ([<ReflectedDefinition>] x: Expr<int>) = x.ToString()
        static member PlotExprOverloadedByType ([<ReflectedDefinition>] x: Expr<string>) = x.ToString()

    FirstClassTests.PlotExprOverloadedByType 1
    FirstClassTests.PlotExprOverloadedByType "a"
    FirstClassTests.PlotExprOverloadedByType <@ 1 @>
    FirstClassTests.PlotExprOverloadedByType <@ "a" @>

If you overload simply by whether the argument is a ReflectedDefinition or not, then the overloads will be ambiguous – neither is more specific than the other:

    // Not advisable: the overloads will be ambiguous
    static member PlotExprOverloadedByShape (x:int) = x.ToString()
    static member PlotExprOverloadedByShape ([<ReflectedDefinition>] x: Expr<int>) = x.ToString()

This matches the existing behaviour of F# 3.0 where the following overloads are ambiguous:

    static member PlotLinqOverloadedByShape (x: Func<int,'T>) =  x.ToString()
    static member PlotLinqOverloadedByShape (x: Expression<Func<int,'T>>) =  x.ToString()

when called with 

    FirstClassTests.PlotLinqOverloadedByShape (fun x -> x)



#	F# 4.0 Speclet: ValueWithName quotation nodes

### Aims

For F# quotation literals, capture the textual names of local variables along with their values, for debugging and display scenarios

### Background

In F#, quotation literals can refer to local values, e.g.

    let f (x:int) = <@ x + x @>

For F# 3.0, in the resulting quotation value, the “x” becomes a “Value(n)” node where “n” is the integer value of “x” at runtime.  No trace of the name “x” remains at runtime - quotation values are closed and don’t contain logical references to local values, and the quotation value produced by “f 1” is called is indistinguishable from one produced by

    let f (x:int) = <@ x + 1 @>

In some meta-programming scenarios (e.g. debugging and display), it is useful for a quotation value to also include the name of the local value where a local-value-substitution occurred.  For example, a charting API may accept quotations where local values have been substituted:

    let drawLine (m:double) (c:double) = Chart.Plot (fun x y -> <@ x = m * y + c @>)

Even when called with “f 3.0 4.0”, the charting API may wish to show the names “m” and “c” for local values, along with the knowledge that m=3.0 and c=4.0 in this particular case, even if only for debugging and display purposes.

### Design Overview

In F# 4.0 we add a new active pattern to quotations that optionally reveals the textual name of a local variable (and nothing else about its identity) associated with a value-substitution in a quotation literal. Specifically, the quotation tree node for “m” and “c” can be examined using the ValueWithName active pattern:

    match q with 
    | ValueWithName(obj,ty,nm) -> true 
    | _ -> false

The new ``ValueWithName`` active pattern in FSharp.Core will match a quotation Q if and only if 

-	Q arose from local value substitutions in a quotation literal, or

-	Q was constructed using Expr.ValueWithName

The local value names embedded in quotation literals are essentially for debug and display purposes only, and are ignored for the purposes of quotation equality.  Code manipulating quotation values should where possible ignore the names associated with of local value substitutions.



