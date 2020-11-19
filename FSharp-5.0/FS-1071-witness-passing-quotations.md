# F# RFC FS-1071 - Witness passing for quotations

The design suggestion [Witness passing for quotations](https://github.com/fsharp/fslang-suggestions/issues/TBD) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion and some related design relaxations.

* [x] Approved in principle
* [x] [Discussion](https://github.com/fsharp/fslang-design/issues/357)
* [x] [Implementation completed](https://github.com/dotnet/fsharp/pull/6345) and [this one](https://github.com/dotnet/fsharp/pull/6810)


# Summary
[summary]: #summary

F# quotations of code using SRTP constraint trait calls don't carry sufficient information
to represent the proper semantics of the code. Specifically the quotations are missing accurate information concerning the
resolution of SRTP constraints. This means these quotations can't be accurately executed.
This RFC addresses this problem by incorporating the necessary
information into both quotations and the code laid down for dynamic interpretation of quotations.

# Motivation

One motivation for this feature is in the use of F# for AI model programming (and indeed many other language-integrated DSL programming examples).

It's reasonable to program an AI model as a module od code with `ReflectedDefinition` on it, like [this example](https://github.com/fsprojects/fsharp-ai-tools/blob/3c46023521f2456ed3748e853ff4665735626d81/examples/dsl/NeuralStyleTransfer-dsl.fsx#L48):

```fsharp
[<ReflectedDefinition>]
module NeuralStyles = 
    ...

    // Set up a convolution layer in the DNN
    let conv_layer (out_channels, filter_size, stride, name) input = 
        let filters = variable (fm.truncated_normal() * v 0.1) (name + "/weights") 
        fm.conv2d (input, filters, out_channels, stride=stride, filter_size=filter_size)
        |> instance_norm name

    ...
    
    // The style-transfer DNN
    let PretrainedFFStyleVGGCore (image: DT<double>) = 
        image
        |> conv_layer (32, 9, 1, "conv1") |> relu
        |> conv_layer (64, 3, 2, "conv2") |> relu
        |> conv_layer (128, 3, 2, "conv3") |> relu
        |> residual_block (3, "resid1")
        |> residual_block (3, "resid2")
        |> residual_block (3, "resid3")
        |> residual_block (3, "resid4")
        |> residual_block (3, "resid5")
        |> conv_transpose_layer (64, 3, 2, "conv_t1") |> relu
        |> conv_transpose_layer (32, 3, 2, "conv_t2") |> relu
        |> conv_layer (3, 9, 1, "conv_t3")
        |> to_pixel_value 
        |> clip 0.0 255.0

```

Once you have an accurate quotation of an AI model you can do a huge number of
things - translate it out of F# into PyTorch or ONNX, or transform it and compile it to
some other tensor platform, or add debugging augmentation to it, or visualise it.  
In a sense, quotations give you ultra-light meta-programming without needing to build any new analysis tools.
The form will be something like this

```fsharp
FSharpAI.Tools.Translate <@ PretrainedFFStyleVGGCore @>
```

By it's nature AI models will use generic math code like "+", that's kind of unavoidable.
Even more so when [RFC FS-1043](https://github.com/fsharp/fslang-design/blob/master/RFCs/FS-1043-extension-members-for-operators-and-srtp-constraints.md) is accepted. Without accurate information about how generic math operators like "+" really resolve, the quotation
meta-programming is inaccurate and often full of bugs. A typical manifestion will be adding an overload (e.g. `Tensor + double`) and then getting this:
```fsharp
FSharpAI.Tools.ShapeAnalysis <@ PretrainedFFStyleVGGCore @>

...Exception: unrecognised types at `+` operator `Tensor` and `double` during shape analysis ...
```

You can workaround each instance but solving the problem so the quotations contain the necessary resolution information
is much better.  In cases such as the above, the person programming the analysis will then accurately process the information.

This is especially important for tools that eventually execute the quotations using .NET or do .NET code-generation for the quotations.



# Detailed Description of Problem

F# quotations using SRTP-constrained generic code (such as `+` or `List.sumBy`) does not carry any information about
how an SRTP constraint has been resolved.
This affects quotation processing or execution, i.e. code that does any of the following:

a. evaluates quotations of code that uses calls to generic inlined math code.

b. evaluates quotations of code that uses user-defined SRTP operators (e.g. anything using FSharpPlus or anything like it, or just plain user-defined code).  RFCs like [#4726](https://github.com/Microsoft/visualfsharp/pull/4726) make this kind of code more common.

c. evaluates quotations of code that uses any future extensions of SRTP features such as [RFC FS-1043](https://github.com/fsharp/fslang-design/blob/24d871a30b5c384579a27fd49fdf9dfb29b1080d/RFCs/FS-1043-extension-members-for-operators-and-srtp-constraints.md), see [#3582](https://github.com/Microsoft/visualfsharp/pull/3582)

d. evaluates quotations that uses implicit operators, discussed in [#6344](https://github.com/Microsoft/visualfsharp/pull/6344)

We worked around many of these problems in FSharp.Core in F# 2.0 but did not solve the
root cause of the problem, and haven't addressed the problem since. This problem spreads through any tools that process quotations
(e.g. evaluators, or transpilers), requiring many special-case workarounds when operators are encountered, and causes
FSharp.Core to contain a bunch of (sometimes half-implemented) [reflection-based primitives](https://github.com/Microsoft/visualfsharp/blob/44c7e10ca432d8f245a6d8f8e0ec19ca8c72edaf/src/fsharp/FSharp.Core/prim-types.fs#L2557)
to re-solve SRTP constraints at runtime in order to support quotation evaluation. This also affects code generation for F# type providers, see https://github.com/fsprojects/FSharp.TypeProviders.SDK/pull/313.

This RFC solves this issue at its core by changing the quotations to include "witnesses" for trait constraints.

The problem can be seen in even tiny pieces of generic math code. We'll use this micro example of some generic inline math code:
```fsharp
let inline negate x = -x
```
This code is generic and the exact implementation of the `op_UnaryNegation` is left unresolved, and an SRTP constraint is added to
the type:
```
val inline negate: x: ^a ->  ^a when  ^a : (static member ( ~- ) :  ^a ->  ^a)
``` 
This SRTP constraint might end up resolved to the op_UnaryNegation for `TimeSpan`, or a compiler-mediated primitive
for `double` or `int32` and so on.

Now given:
```fsharp
open FSharp.Linq.RuntimeHelpers
let eval q = LeafExpressionConverter.EvaluateQuotation q
``` 
then consider
```fsharp
    <@ negate 1.0 @>  |> eval
```
and
```
    <@ negate (System.TimeSpan.FromHours(1.0)) @> |> eval
```
Prior to this RFC these both give exceptions like this:
```
System.NotSupportedException: Dynamic invocation of op_UnaryNegation is not supported
   at Microsoft.FSharp.Linq.RuntimeHelpers.LeafExpressionConverter.EvaluateQuotation(FSharpExpr e)
   at <StartupCode$FSI_0006>.$FSI_0006.main@()
```
Here, the quotation evaluator has attempted to dynamically invoke the `negate` function
(the quotation processor doesn't get to see the implementation of `negate` in this example).  

Note that, even though we "inline" `negate` in regular F# code, this inlining isn't done for
quotations (for good reasons, and not something we can or should change).  Thus for the purposes of
quotations we still emit an implementation method stub for `negate`:
```
.method public static !!a  negate<a>(!!a x) cil managed
{
  IL_0000:  ldstr      "Dynamic invocation of op_UnaryNegation is not supported"
  IL_0005:  newobj     instance void [mscorlib]System.NotSupportedException::.ctor(string)
  IL_000a:  throw
} 
```
Here the implementation of `-x` has been inlined - and the dynamic implementation of that primitive is
to throw an exception. This is OK for **regular** F# code, because in regular code the real implementation
it is always inlined and the proper code results: the above method simply never ever gets called for regular
F# code and only exists at all for the purposes of quotations and reflection (quotations referring to `negate`
need a `MethodInfo` for `Call` nodes, and the above provides it).

But for **quotations** referring to `negate` the actual implementation of the method is semantically useless.
For example, looking at `<@ negate 1.0 @>` we see it is nothing but a call node for `negate`
```
Call (None, negate, [Value (1.0)])
```
Any dynamic interpreter for the quotation is bound to fail here - it will dynamically invoke `negate` and an exception will be raised.

Why did we make the dynamic implementation of `-x` raise an exception?  Because in order to implement
the trait constraint, we need its resolution. This can be done by either

1. using reflection inside the FSharp.Core implementation of `op_Negate`
2.  passing a "witness" for the trait constraint to `negate` to indicate how the trait constraint is resolved, or 

Currently FSharp.Core contains partial implementations of some basic operators like `+` using reflection, however 

a. not all operators were implemented

b. the use of reflection to re-resolve SRTP constraints at runtime is slow and has many corner cases that are not correctly handled.

c. this doesn't help with any user code that makes SRTP calls

The end result is that historically quotations and generic code using SRTP simply don't work very
well together: it's a feature interaction that has never really been resolved properly.

# Detailed Design

This PR shifts us to use witness passing.   That is, 

1. In each quotation `Call` using a generic inlined function, a witness is recorded for the solution of each trait constraint. 

2. You can access this information via the new `Quotations.Patterns.CallWithWitnesses` active pattern, and reconstruct the node using `Quotations.Expr.CallWithWitnesses`.

3. The compiled method signature for each SRTP-constrained generic inlined function has one extra argument for each SRTP constraint. This is in a new method with a new distinguished suffic to the name.

4. The emitted IL for each SRTP-constrained generic inlined function either passes the necessary witnesses, and now never emits the `NotSupportedException` code.

### Witnesses

A witness is a lambda term that represents the solution to an SRTP constraint. For example, if you use `+` in generic inline math code, then there will be an extra hidden parameter in the compiled form of that generic code. If you examine quotation witnesses using `CallWithWitnesses`, you will see a type-specialized lambda passed as that argument at callsites where the generic
function is called at a non-generic, specific type.

For example, for an SRTP-constraint `when  ^a : (static member (+) :  ^a * ^a ->  ^a)`:

* You will see `(fun (a: double) (b: double) -> LanguagePrimtiives.AdditionDynamic a b)` passed in at the place where the code is specialized at type `double`.

* You will see `(fun (a: TimeSpan) (b: TimeSpan) -> TimeSpan.op_Addition(a,b))` passed in at the place where the code is specialized at type `TimeSpan`.

Because this is primarily about quotations, you only see these witnesses by matching on the quotation using the new
`CallWithWitnesses` active pattern when processing the quotation.

This means that each witness effectively records the resolution/implementation of a trait constraint
in quotations and/or compiled code.

Note that passing implicit witnesses is the standard technique for implementing Haskell type classes.


### Compiled form of SRTP-constrained generic code

For 
```fsharp
let inline negate x = -x
```
of type
```
val inline negate: x: ^a ->  ^a when  ^a : (static member ( ~- ) :  ^a ->  ^a)
``` 
the compiled form of this code is now two methods - the first is emitted for compatibility and the second is
the witness-carrying version of the method which has `$W` added to the name
```
.method public static !!a  negate<a>(!!a x) cil managed
{
  IL_0000:  ldstr      "Dynamic invocation of op_UnaryNegation is not supported"
  IL_0005:  newobj     instance void [mscorlib]System.NotSupportedException::.ctor(string)
  IL_000a:  throw
} 

.method public static !!a  negate$W<a>(class FSharpFunc`2<!!a,!!a> op_UnaryNegation, !!a x) 
{
  IL_0000:  ldarg.0
  IL_0001:  ldarg.1
  IL_0004:  callvirt   instance !1 class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<!!a,!!a>::Invoke(!0)
  IL_0009:  ret
} 
```
Functions and methods that are not inline or have no SRTP constraints have no extra entry points.

### Library additions for accessing witness information in quotations

There are library additions for accessing witness information in quotations:

```fsharp
namespace FSharp.Core.Quotations

type Expr =
    /// <summary>Builds an expression that represents a call to an static method or module-bound function</summary>
    /// <param name="methodInfo">The MethodInfo describing the method to call.</param>
    /// <param name="methodInfoWithWitnesses">The additional MethodInfo describing the method to call, accepting witnesses.</param>
    /// <param name="witnesses">The list of witnesses to the method.</param>
    /// <param name="arguments">The list of arguments to the method.</param>
    /// <returns>The resulting expression.</returns>
    static member CallWithWitnesses: methodInfo: MethodInfo * methodInfoWithWitnesses: MethodInfo * witnesses: Expr list * arguments: Expr list -> Expr

    /// <summary>Builds an expression that represents a call to an instance method associated with an object</summary>
    /// <param name="obj">The input object.</param>
    /// <param name="methodInfo">The description of the method to call.</param>
    /// <param name="methodInfoWithWitnesses">The additional MethodInfo describing the method to call, accepting witnesses.</param>
    /// <param name="witnesses">The list of witnesses to the method.</param>
    /// <param name="arguments">The list of arguments to the method.</param>
    /// <returns>The resulting expression.</returns>
    static member CallWithWitnesses: obj:Expr * methodInfo:MethodInfo * methodInfoWithWitnesses: MethodInfo * witnesses: Expr list * arguments:Expr list -> Expr

module Patterns =
    /// <summary>An active pattern to recognize expressions that represent calls to static and instance methods, and functions defined in modules, including witness arguments</summary>
    /// <param name="input">The input expression to match against.</param>
    /// <returns>(Expr option * MethodInfo * MethodInfo * Expr list) option</returns>
    [<CompiledName("CallWithWitnessesPattern")>]
    val (|CallWithWitnesses|_|) : input:Expr -> (Expr option * MethodInfo * MethodInfo * Expr list * Expr list) option
```

### Library additions for built-in witnesses

The following functions are used as witnesses for built-in operations like `+`.  Some of these already exist in FSharp.Core, the others are freshly added.

```fsharp
namespace FSharp.Core

module LanguagePrimitives = 
        val GenericZeroDynamic : unit -> 'T 
        val GenericOneDynamic : unit -> 'T 
        val AdditionDynamic : x:'T1 -> y:'T2 -> 'U
        val CheckedAdditionDynamic : x:'T1 -> y:'T2 -> 'U
        val MultiplyDynamic : x:'T1 -> y:'T2 -> 'U
        val CheckedMultiplyDynamic : x:'T1 -> y:'T2 -> 'U
        val SubtractionDynamic : x:'T1 -> y:'T2 -> 'U
        val DivisionDynamic : x:'T1 -> y:'T2 -> 'U
        val UnaryNegationDynamic : value:'T -> 'U
        val ModulusDynamic : x:'T1 -> y:'T2 -> 'U
        val CheckedSubtractionDynamic : x:'T1 -> y:'T2 -> 'U
        val CheckedUnaryNegationDynamic : value:'T -> 'U
        val LeftShiftDynamic : value:'T1 -> shift:'T2 -> 'U
        val RightShiftDynamic : value:'T1 -> shift:'T2 -> 'U
        val BitwiseAndDynamic : x:'T1 -> y:'T2 -> 'U
        val BitwiseOrDynamic : x:'T1 -> y:'T2 -> 'U
        val ExclusiveOrDynamic : x:'T1 -> y:'T2 -> 'U
        val LogicalNotDynamic : value:'T -> 'U
        val ExplicitDynamic : value:'T -> 'U
        val LessThanDynamic : x:'T1 -> y:'T2 -> 'U
        val GreaterThanDynamic : x:'T1 -> y:'T2 -> 'U
        val LessThanOrEqualDynamic : x:'T1 -> y:'T2 -> 'U
        val GreaterThanOrEqualDynamic : x:'T1 -> y:'T2 -> 'U
        val EqualityDynamic : x:'T1 -> y:'T2 -> 'U
        val InequalityDynamic : x:'T1 -> y:'T2 -> 'U
        val DivideByIntDynamic : x:'T -> y:int -> 'T

// These are pre-existing and act as suitable witnesses
module OperatorIntrinsics =
        val AbsDynamic : x:'T -> 'T 
        val AcosDynamic : x:'T -> 'T 
        val AsinDynamic : x:'T -> 'T 
        val AtanDynamic : x:'T -> 'T 
        val Atan2Dynamic : y:'T1 -> x:'T1 -> 'T2
        val CeilingDynamic : x:'T -> 'T 
        val ExpDynamic : x:'T -> 'T 
        val FloorDynamic : x:'T -> 'T 
        val TruncateDynamic : x:'T -> 'T 
        val RoundDynamic : x:'T -> 'T 
        val SignDynamic : 'T -> int
        val LogDynamic : x:'T -> 'T 
        val Log10Dynamic : x:'T -> 'T 
        val SqrtDynamic : 'T1 -> 'T2
        val CosDynamic : x:'T -> 'T 
        val CoshDynamic : x:'T -> 'T 
        val SinDynamic : x:'T -> 'T 
        val SinhDynamic : x:'T -> 'T 
        val TanDynamic : x:'T -> 'T 
        val TanhDynamic : x:'T -> 'T 
        val PowDynamic : x:'T -> y:'U -> 'T 


```
For example, for the SRTP-constraint `when  ^a : (static member (+) :  ^a * ^a ->  ^a)` you will see `(fun (a: double) (b: double) -> LanguagePrimtiives.AdditionDynamic a b)` passed at the place where the code is specialized at type `double`.

The above are used whenever a "built in" constraint solution is determined by the F# compiler, e.g. in the cases where no corresponding `op_Addition` member actually exists on a type such as `System.Double`, but rather the F# compiler simulates the existence of such a type.


### Implicit operator trait calls now permitted

Quotations can now contain implicit operator uses, including the dynamic operator, e.g. 
```fsharp
type Foo(s: string) =
     member _.S = s
     static member (?) (foo : Foo, name : string) = foo.S + name
     static member (++) (foo : Foo, name : string) = foo.S + name
     static member (?<-) (foo : Foo, name : string, v : string) = ()

let foo = Foo("hello, ")

let q2 = <@ foo ? uhh @>
let q4 = <@ foo ? uhh <- "hm" @>
let q5 = <@ foo ++ "uhh" @>

```
Previously these were not permitted.  The quotation is an application of a lambda expression which is the solution to the implied SRTP constraint.
```fsharp
val q2 : Quotations.Expr<string> =
  Application (Application (Lambda (arg0,
                                  Lambda (arg1,
                                          Call (None, op_Dynamic, [arg0, arg1]))),
                          PropertyGet (None, foo, [])), Value ("uhh"))
val q4 : Quotations.Expr<unit> =
  Application (Application (Application (Lambda (arg0,
                                               Lambda (arg1,
                                                       Lambda (arg2,
                                                               Call (None,
                                                                     op_DynamicAssignment,
                                                                     [arg0, arg1,
                                                                      arg2])))),
                                       PropertyGet (None, foo, [])),
                          Value ("uhh")), Value ("hm"))
val q5 : Quotations.Expr<string> =
  Application (Application (Lambda (arg0,
                                  Lambda (arg1,
                                          Call (None, op_PlusPlus, [arg0, arg1]))),
                          PropertyGet (None, foo, [])), Value ("uhh"))
```


### Accessing witness information of `ReflectedDefinition`

ReflectedDefinitions may contain invocations of SRTP constraints, e.g.

```fsharp
[<ReflectedDefinition>]
let inline f1 (x: ^T) = ( ^T : (static member Foo: int -> int) (3))

[<ReflectedDefinition>]
let inline f2 x y z = (x - y) + z
```
The implicit arguments are visible when accessing the reflected definition via the witness-passing MethodInfo. For example, given:
```fsharp
type C() = 
    static member Foo (x:int) = x

[<ReflectedDefinition>]
let inline f3 (x: ^T) =
    ( ^T : (static member Foo: int -> int) (3))
```
Then
```fsharp
match <@ f3 (C()) @> with
| Quotations.Patterns.Call(_, mi, _) -> Quotations.Expr.TryGetReflectedDefinition(mi)
```
returns `None` because the `Call` pattern has accessed the non-witness-passing MethodInfo.  However this:
```fsharp
match <@ f3 (C()) @> with 
| Quotations.Patterns.CallWithWitnesses(_, mi, miw, _, _) -> Quotations.Expr.TryGetReflectedDefinition(miw)
```
returns a quotation of this shape:
```fsharp
     Lambda (Foo, Lambda (x, Application (Foo, Value (3))))
```
where the `Foo` argument is the implicit witness being passed to `f3$W`.  Note the `miw` MethodInfo is the method
for `f3$W` and has a ReflectedDefinition (because it is informationally complete and actually has witnesses), where
`f3` does not.

### Witnesses not available for SRTP-constrained class type parameters

Witnesses are not available for SRTP-constrained class type parameters, e.g. 

```fsharp
    type C< ^a when ^a : (static member (+) : ^a * ^a -> ^a) >() =
        ...
```
No witnesses are available for quotations involving invocation associated with these constraints. In these cases
`null` is passed as a witness.  Instead, SRTP constraints should only be used on method and function definitions
if accurate witnesses are required.



### Code samples

For example:
```fsharp
open FSharp.Quotations.Patterns
let q = <@ 1 + 2 @> 

match q with 
| CallWithWitnesses(None, minfo1, minfo2, witnessArgs, args) -> 
    printfn "minfo1 = %A" minfo1.Name
    printfn "minfo2 = %A" minfo2.Name
    printfn "witnessArgs = %A" witnessArgs
    printfn "args = %A" args
| _ ->
    failwith "fail"
```
gives
```fsharp
minfo1: T3 op_Addition<int32,int32,int32>(T1 x, T2 y)
minfo2: T3 op_Addition$W<int32,int32,int32>(FSharpFunc<T1,FSharpFunc<T2,T3>> op_Addition, T1 x, T2 y)
witnessArgs = [Lambda (arg0_0, Lambda (arg1_0, Call (None, AdditionDynamic, [arg0_0, arg1_0])))]
args = [ Const(1); Const(2) ]
```

### Resources

Relevant issues
* https://github.com/fsharp/fsharp/issues/18
* https://github.com/Microsoft/visualfsharp/issues/1951
* https://github.com/Microsoft/visualfsharp/issues/865#issuecomment-170399176

Relevant code
* There is a host of code in FSharp.Core associated with the non-witness-passing implementation of some primitives like "+" and "Zero".  e.g. [this](https://github.com/Microsoft/visualfsharp/blob/44c7e10ca432d8f245a6d8f8e0ec19ca8c72edaf/src/fsharp/FSharp.Core/prim-types.fs#L2557).  Essentially all this code becomes redundant after this PR.

* There are many workarounds in existing quotation evaluators. 

# Drawbacks
[drawbacks]: #drawbacks

* Quotation evaluators will need to be updated to make use of witness parameters

# Alternatives
[alternatives]: #alternatives

### No longer require 'inline'

Although the RFC is only relevant to reflection and quotations, the witness passing could also be leveraged
by future implementations of type-class like functionality, e.g. simply allow generic math code without
the use of `inline`, (e.g. `let generic add x = x + x`).  However such code would be substantially slower
if not inlined, which is why we always currently always require it to be inlined. 

### Choice of name for alternative entry point

This RFC suffixes `$W` for the entry point.  Alternatives such as `WithWitnesses` were consdiered.



# Compatibility
[compatibility]: #compatibility

### Solved: continue exact same semantics for existing code

Note that this RFC is only relevant to quotations and reflection - no actual code generation changes
for non `let inline` code (and the actual code generation for `inline` code is irrelevant for regular F# code,
since, well, everything is inlined), and the PR is careful that existing quotations and quotation processing
code will behave precisely as before.    

### Solved: Presence of extra generated methods may affect existing reflection calls

One potential problem with this RFC is that the simplest version of its implementation is
simply to change the signatures for generated methods, adding extra witness arguments to the compiled form of generic `inline`
functions.  However this would not be backward-compatible:
existing code using quotations would break.

For this reason in this PR we carefully continue to
emit the old methods as well (the ones not taking witness arguments) and precisely preserve their semantics.
This each generic `inline` function now results in two IL methods being emitted - the first a legacy method
that doesn't accept witness arguments, and the second the go-forward method that accepts the extra arguments.

Further, the new `WithWitnesses` methods are given a distinct name, with the suffix added.  This is to ensure that
code that uses .NET reflection still only finds one method of the original name.  For example

```fsharp
namespace A
module B = 
    let MyFunction x = x
    let inline MyInlineFunction x = x + x
    
type C() = 
    member x.P = 1

let bty = typeof<C>.Assembly.GetType("A.B")

bty.GetMethod("MyFunction") // succeeds
bty.GetMethod("MyInlineFunction") // we want this to continue to succeed without ambiguity
```

# Unresolved questions
[unresolved]: #unresolved-questions

None

