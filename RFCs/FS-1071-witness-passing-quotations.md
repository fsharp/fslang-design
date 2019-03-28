# F# RFC FS-1071 - Witness passing for quotations

The design suggestion [Witness passing for quotations](https://github.com/fsharp/fslang-suggestions/issues/TBD) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion and some related design relaxations.

* [x] Approved in principle
* [ ] [Discussion](https://github.com/fsharp/fslang-design/issues/357)
* [x] [Implementation](https://github.com/Microsoft/visualfsharp/pull/6345)


# Summary
[summary]: #summary

F# quotations of code using SRTP constraint trait calls doesn't carry sufficient information
to represent the semantic intent of the code, specifically it is missing any record of the
resolution of SRTP constraints. This RFC addresses this problem by incorporating the necessary
information into both quotations and the code laid down for dynamic interpretation of quotations.

# Detailed Description of Problem

F# quotations using SRTP-constrained generic code (such as `+` or `List.sumBy`) does not carry any information about
how an SRTP constraint has been resolved, requiring adhoc re-resolution of SRTP
constraints at runtime.  

The problem affects quotation processing or dynamic invocation of all `inline` code, and thus particularly affects evaluating or otherwise processing the following:

a. quotations of code that uses calls to generic inlined math code.

b. quotations of code that uses user-defined SRTP operators (e.g. anything using FSharpPlus or anything like it, or just plain user-defined code).  RFCs like [#4726](https://github.com/Microsoft/visualfsharp/pull/4726) make this kind of code more common.

c. quotations of code that uses any future extensions of SRTP features such as [RFC FS-1043](https://github.com/fsharp/fslang-design/blob/24d871a30b5c384579a27fd49fdf9dfb29b1080d/RFCs/FS-1043-extension-members-for-operators-and-srtp-constraints.md), see [#3582](https://github.com/Microsoft/visualfsharp/pull/3582)

d. quotations that uses implicit operators, discussed in [#6344](https://github.com/Microsoft/visualfsharp/pull/6344)

We worked around many of these problems in FSharp.Core in F# 2.0 but did not solve its
root cause, and haven't addressed the problem since. This problem spreads through any tools that process quotations
(e.g. evaluators, or transpilers), requiring many special-case workarounds when operators are encountered, and causes
FSharp.Core to contain a bunch of (sometimes half-implemented) [reflection-based primitives](https://github.com/Microsoft/visualfsharp/blob/44c7e10ca432d8f245a6d8f8e0ec19ca8c72edaf/src/fsharp/FSharp.Core/prim-types.fs#L2557)
to re-solve SRTP constraints at runtime in order to support quotation evaluation. 

This RFC (TBD) and PR solves this issue at its core by changing the quotations to include "witnesses" for trait constraints
as seen by quotations.

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
   <@ negate TimeSpan.Zero @> |> eval
```
These both give exceptions like this:
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

1. In each quotation using a generic inlined function, a witness is recorded for the solution of each trait constraint. 

2. You can access this information via the new `Quotations.Patterns.CallWithWitnesses` active pattern, and reconstruct the node using `Quotations.Expr.CallWithWitnesses`.

3. The method signature for each SRTP-constrained generic inlined function has one extra argument for each SRTP constraint

4. The emitted IL for each SRTP-constrained generic inlined function either invokes of passes the witnesses as necessary, and now never emits the `NotSUpportedException` code.

### Witnesses

A witness is a lambda term that represents the solution to an SRTP constraint. For example, if you use `+` in generic inline math code, then there will be an extra hidden parameter in the compiled form of that generic code. If you examine quotation witnesses using `CallWithWitnesses`, you will see a type-specialized lambda passed as that argument at callsites where the generic
function is called at a non-generic, specific type.

For example, for an SRTP-constraint `when  ^a : (static member (+) :  ^a * ^a ->  ^a)`:

* You will see `(fun (a: double) (b: double) -> a + b)` passed in at the place where the code is specialized at type `double`.

* You will see `(fun (a: TimeSpan) (b: TimeSpan) -> TimeSpan.op_Addition(a,b))` passed in at the place where the code is specialized at type `TimeSpan`.

Because this is only about quotations, you only see these witnesses by matching on the quotation using the new
`CallWithWitnesses` active pattern when processing the quotation.

This measn that each witness effectively records the resolution/implementation of a trait constraint
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
the witness-carrying version of the method which has `WithWitnesses` added to the name
```
.method public static !!a  negate<a>(!!a x) cil managed
{
  IL_0000:  ldstr      "Dynamic invocation of op_UnaryNegation is not supported"
  IL_0005:  newobj     instance void [mscorlib]System.NotSupportedException::.ctor(string)
  IL_000a:  throw
} 

.method public static !!a  negateWithWitnesses<a>(class FSharpFunc`2<!!a,!!a> op_UnaryNegation, !!a x) 
{
  IL_0000:  ldarg.0
  IL_0001:  ldarg.1
  IL_0004:  callvirt   instance !1 class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<!!a,!!a>::Invoke(!0)
  IL_0009:  ret
} 
```

### Accessing witness information in quotations

Library additions:

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
### Code samples

For example:
```fsharp
let q = <@ 1 + 2 @> 

match q with 
| CallWithWitnesses(None, minfo1, minfo2, witnessArgs, args) -> 
    printfn "minfo1 = %A" minfo1.Name // T3 op_Addition<int32,int32,int32>(T1 x, T2 y)
    printfn "minfo2 = %A" minfo2.Name // T3 op_AdditionWithWitnesses<int32,int32,int32>(FSharpFunc<T1,FSharpFunc<T2,T3>> op_Addition, T1 x, T2 y)
    printfn "witnessArgs = %A" witnessArgs // [ Lambda(Call(op_Addition(1,1)) ]
    printfn "args = %A" args
| _ ->
    failwith "fail"
```
gives
```fsharp
minfo1: T3 op_Addition<int32,int32,int32>(T1 x, T2 y)
minfo2: T3 op_AdditionWithWitnesses<int32,int32,int32>(FSharpFunc<T1,FSharpFunc<T2,T3>> op_Addition, T1 x, T2 y)
witnessArgs = [ Lambda(Call(op_Addition(1,1)) ]
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

Although the RFC is only relevant to reflection and quotations, the witness passing could also be leveraged
by future implementations of type-class like functionality, e.g. simply allow generic math code without
the use of `inline`, (e.g. `let generic add x = x + x`).  However such code would be substantially slower
if not inlined, which is why we always currently always require it to be inlined. 

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

* [ ] The `CallWithWitnesses` node requires both the legacy and updated MethodInfos to be provided. This may be painful if people
      attempt to create this node from nothing.

* [ ] Adjust the LeafExpressionEvaluator to actually use the extra `CallWithWitnesses` node to do better evaluation.

* [ ] Complete the witnesses passed for primitives (they are currently broken). The witnesses passed for
      SRTP constraints over user-defined types are accurate
