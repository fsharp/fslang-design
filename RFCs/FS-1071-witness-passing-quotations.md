# F# RFC FS-1071 - Witness passing for quotations

The design suggestion [Witness passing for quotations](https://github.com/fsharp/fslang-suggestions/issues/TBD) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion and some related design relaxations.

* [x] Approved in principle
* [ ] Discussion
* [x] [Implementation](https://github.com/Microsoft/visualfsharp/pull/6314)


# Summary
[summary]: #summary

F# quotations of code using SRTP constraint trait calls doesn't carry sufficient information
to represent the semantic intent of the code, specifically it is missing any record of the
resolution of SRTP constraints. This RFC addresses this problem by incorporating the necessary
information into both quotations and the code laid down for dynamic interpretation of quotations.

# Detailed Design

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

This PR shifts us to use witness passing.  This means that the compiled stub (used only for quotations) is now:
```
.method public static !!a  negate<a>(class FSharpFunc`2<!!a,!!a> op_UnaryNegation, !!a x) 
{
  IL_0000:  ldarg.0
  IL_0001:  ldarg.1
  IL_0004:  callvirt   instance !1 class [FSharp.Core]Microsoft.FSharp.Core.FSharpFunc`2<!!a,!!a>::Invoke(!0)
  IL_0009:  ret
} 
```
That is, 

1. In each quotation using a generic inlined function, a witness is automatically passed for the solution of each trait constraint. 

2 The emitted IL for the witness-accepting version of the generic inlined function invokes or passes the witnesses as necessary. 

3. Thus quotations now contain information about the witnesses passed, you can access this information via the new `CallWithWitnesses` node

Note that this PR and RFC is currently relevant only to quotations - no actual code generation changes for non `let inline` code (and the actual code generation for `inline` code is irrelevant for regular F# code, since, well, everything is inlined), and the PR is careful that existing quotations and quotation processing code will behave precisely as before.    



### Code samples

TBD

### Resources

Relevant issues
* https://github.com/fsharp/fsharp/issues/18
* https://github.com/Microsoft/visualfsharp/issues/1951
* https://github.com/Microsoft/visualfsharp/issues/865#issuecomment-170399176

Relevant code
* There is a host of code in FSharp.Core associated with the non-witness-passing implementation of some primitives like "+" and "Zero".  e.g. [this](https://github.com/Microsoft/visualfsharp/blob/44c7e10ca432d8f245a6d8f8e0ec19ca8c72edaf/src/fsharp/FSharp.Core/prim-types.fs#L2557).  Essentially all this code becomes redundant after this PR.

* There will be many workarounds in existing quotation evaluators


# Drawbacks
[drawbacks]: #drawbacks

TBD

# Alternatives
[alternatives]: #alternatives

Although the RFC and PR is ony relevant to reflection and quotations, the witness passing could also be leveraged by future implementations of type-class like functionality, e.g. simply allow generic math code without the use of `inline`, (e.g. `let generic add x = x + x`).  However such code would be substantially slower if not inlined, which is why we always currently require it to be inlined. 


# Compatibility
[compatibility]: #compatibility

The major problem with a simple version of this fix is backwards
compat: the fix adds extra witness arguments to the compiled form of generic `inline`
functions (one new argument is added for each SRTP constraint).  For this reason in this PR we carefully continue to
emit the old methods as well (the ones not taking witness arguments) and precisely preserve their semantics.
This each generic `inline` function now results in two IL methods being emitted - the first a legacy method
that doesn't accept witness arguments, and the second the go-forward method that accepts the extra arguments.

Code that uses .NET reflection calls that expect only one method to have been emitted for an F# function may
fail after this change.  For example
```
namespace A
module B = 
    let MyFunction x = x
    let inline MyInlineFunction x = x + x
    
type C() = 
    member x.P = 1

let bty = typeof<C>.Assembly.GetType("A.B")

bty.GetMethod("MyFunction") // succeeds
bty.GetMethod("MyInlineFunction") // now fails with ambiguity, there are now two MyInlineFunction methods 
```


# Unresolved questions
[unresolved]: #unresolved-questions

* [ ] Decide whether the change in behaviour w.r.t. reflection is acceptable.  If not, we may need to codegen the additional method differently.
* [ ] Adjust the LeafExpressionEvaluator to actually use the extra `CallWithWitnesses` node to do better evaluation.
* [ ] Complete the witnesses passed for primitives (they are currently broken). The witnesses passed for SRTP constraints over user-defined types are accurate
