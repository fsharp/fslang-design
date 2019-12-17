# F# RFC FS-1071 - Witness passing for quotations

The design suggestion [Witness passing for quotations](https://github.com/fsharp/fslang-suggestions/issues/TBD) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion and some related design relaxations.

* [x] Approved in principle
* [ ] [Discussion](https://github.com/fsharp/fslang-design/issues/357)
* [x] [Implementation](https://github.com/Microsoft/visualfsharp/pull/6345)


# Summary
[summary]: #summary

F# quotations of code using SRTP constraint trait calls (e.g. using arithmetic) doesn't carry sufficient information
to represent the full semantic intent of the code. Specifically the data carried in quotations is
missing any record of the resolution of SRTP constraints. This means that any code interpreting (or compiling)
from quotations must include 500+ lines of (often buggy) code to re-resolve SRTP constraints.
This RFC addresses this problem by incorporating the necessary
information into both quotations and ensuring that assemblies always contain code laid down
necessary for the dynamic interpretation of quotations.  It also lays the foundation for future
lifting of the `inline` restriction for generic code using generic arithmetic, and for allowing
extention methdos to satisfy SRTP constraints.

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
to re-solve SRTP constraints at runtime in order to support quotation evaluation. This also affects code generation for F# type providers, see https://github.com/fsprojects/FSharp.TypeProviders.SDK/pull/313.

This RFC solves this issue at its core by 
1. changing quotations to include "witnesses" for trait constraints as seen by quotations.
2. adding methods to FSharp.Core that provide executable code for "built in" trait constraints
3. ensure that assemblies always contain code laid down necessary for the dynamic interpretation of quotations without requiring the inlining of `inline` functions into quotations, and instead generating witness-calling code for these.

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
Here, the quotation evaluator has attempted to dynamically invoke the `negate` function but
its implementation invokes an SRTP constraint and the exception-raising code has been generated for this.

Note that, even though we "inline" `negate` in regular F# code, this inlining isn't done for
quotations.  Thus for the purposes of
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

2. passing a "witness" for the trait constraint to `negate` to indicate how the trait constraint is resolved, or 

Currently FSharp.Core contains partial implementations of some basic operators like `+` using reflection, however 

a. not all operators were implemented

b. the use of reflection to re-resolve SRTP constraints at runtime is slow and has many corner cases that are not correctly handled.

c. this doesn't help with any user code that makes SRTP calls

The end result is that historically quotations and code using anything which uses SRTP constraints simply don't work very
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

This means that each witness effectively records the resolution/implementation of a trait constraint
in quotations and/or compiled code.

Note that passing implicit witnesses is the standard technique for implementing Haskell type classes.


### Compiled form of SRTP-constrained generic code

For 

```fsharp
let inline negate x = -x
```

of type

```fsharp
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
open FSharp.Quotations.Patterns
let q = <@ 1 + 2 @> 

match q with 
| CallWithWitnesses(None, minfo1, minfo2, witnessArgs, args) -> 
    printfn "minfo1 = %A" minfo1.Name // T3 op_Addition<int32,int32,int32>(T1 x, T2 y)
    printfn "minfo2 = %A" minfo2.Name // T3 op_Addition$W<int32,int32,int32>(FSharpFunc<T1,FSharpFunc<T2,T3>> op_Addition, T1 x, T2 y)
    printfn "witnessArgs = %A" witnessArgs // [ Lambda(Call(op_Addition(1,1)) ]
    printfn "args = %A" args
| _ ->
    failwith "fail"
```
gives
```fsharp
minfo1: T3 op_Addition<int32,int32,int32>(T1 x, T2 y)
minfo2: T3 op_Addition$W<int32,int32,int32>(FSharpFunc<T1,FSharpFunc<T2,T3>> op_Addition, T1 x, T2 y)
witnessArgs = [ Lambda(Call(op_Addition(1,1)) ]
args = [ Const(1); Const(2) ]
```

### Library additions

A type `FSharp.Core.LanguagePrimitives.BuiltInWitnesses` is added to contain a collection of methods that act as executable
witnesses for the solutions of "built in" constraints.  Ideally these should in theory be present on .NET types like `System.Int32` as `op_Addition` etc. overloads on those types. However, because .NET languages always treat these types as primitive and
often have their own resolution rules for these operators, these methods have not been present, and indeed there is
no simple method available anywhere in .NET to perform even a basic operation such as adding two integers.

To make things really simple for quotation consumers encountering `CallWithWitness` nodes, it makes sense for FSharp.Core to
contain methods that complete the set of primitives available.  (An alternative would be to provide `BuiltIn` as kind of
witness and leave it up to the code interpreting the quotations to interpret/code-generate for addition etc.  However
it is much simpler to provide the quotation consumer with an actual .NET method they can invoke to implement the exact semantics
of the satisfied SRTP constraint, including the subtleties of rounding, conversion etc.)

The full list of methods is unfortunately quite long.  The implementation of each is usually one .NET bytecode like `add`.  See "Alternatives" below for list of a possible technique to reduce this down to one (generic) method for each operation.
```fsharp
/// <summary>Representative witnesses for traits solved by the F# compiler</summary>
type BuiltInWitnesses = 
    static member inline op_Addition: x: int32 * y: int32 -> int32 
    static member inline op_Addition: x: float * y: float -> float 
    static member inline op_Addition: x: float32 * y: float32 -> float32 
    static member inline op_Addition: x: int64 * y: int64 -> int64 
    static member inline op_Addition: x: uint64 * y: uint64 -> uint64 
    static member inline op_Addition: x: uint32 * y: uint32 -> uint32 
    static member inline op_Addition: x: nativeint * y: nativeint -> nativeint 
    static member inline op_Addition: x: unativeint * y: unativeint -> unativeint 
    static member inline op_Addition: x: int16 * y: int16 -> int16 
    static member inline op_Addition: x: uint16 * y: uint16 -> uint16 
    static member inline op_Addition: x: char * y: char -> char 
    static member inline op_Addition: x: sbyte * y: sbyte -> sbyte 
    static member inline op_Addition: x: byte * y: byte -> byte 
    static member inline op_Addition: x: string * y: string -> string 
    static member inline op_Addition: x: decimal * y: decimal -> decimal 

    static member inline op_Multiply: x: int32 * y: int32 -> int32 
    static member inline op_Multiply: x: float * y: float -> float 
    static member inline op_Multiply: x: float32 * y: float32 -> float32 
    static member inline op_Multiply: x: int64 * y: int64 -> int64 
    static member inline op_Multiply: x: uint64 * y: uint64 -> uint64 
    static member inline op_Multiply: x: uint32 * y: uint32 -> uint32 
    static member inline op_Multiply: x: nativeint * y: nativeint -> nativeint 
    static member inline op_Multiply: x: unativeint * y: unativeint -> unativeint 
    static member inline op_Multiply: x: int16 * y: int16 -> int16 
    static member inline op_Multiply: x: uint16 * y: uint16 -> uint16 
    static member inline op_Multiply: x: sbyte * y: sbyte -> sbyte 
    static member inline op_Multiply: x: byte * y: byte -> byte 
    static member inline op_Multiply: x: decimal * y: decimal -> decimal 

    static member inline op_UnaryNegation: value: int32 -> int32
    static member inline op_UnaryNegation: value: float -> float
    static member inline op_UnaryNegation: value: float32 -> float32
    static member inline op_UnaryNegation: value: int64 -> int64
    static member inline op_UnaryNegation: value: int16 -> int16
    static member inline op_UnaryNegation: value: nativeint -> nativeint
    static member inline op_UnaryNegation: value: sbyte -> sbyte
    static member inline op_UnaryNegation: value: decimal -> decimal

    static member inline op_Subtraction: x: int32 * y: int32 -> int32 
    static member inline op_Subtraction: x: float * y: float -> float 
    static member inline op_Subtraction: x: float32 * y: float32 -> float32 
    static member inline op_Subtraction: x: int64 * y: int64 -> int64 
    static member inline op_Subtraction: x: uint64 * y: uint64 -> uint64 
    static member inline op_Subtraction: x: uint32 * y: uint32 -> uint32 
    static member inline op_Subtraction: x: nativeint * y: nativeint -> nativeint 
    static member inline op_Subtraction: x: unativeint * y: unativeint -> unativeint 
    static member inline op_Subtraction: x: int16 * y: int16 -> int16 
    static member inline op_Subtraction: x: uint16 * y: uint16 -> uint16 
    static member inline op_Subtraction: x: sbyte * y: sbyte -> sbyte 
    static member inline op_Subtraction: x: byte * y: byte -> byte 
    static member inline op_Subtraction: x: decimal * y: decimal -> decimal 

    static member inline op_Division: x: int32 * y: int32 -> int32 
    static member inline op_Division: x: float * y: float -> float 
    static member inline op_Division: x: float32 * y: float32 -> float32 
    static member inline op_Division: x: int64 * y: int64 -> int64 
    static member inline op_Division: x: uint64 * y: uint64 -> uint64 
    static member inline op_Division: x: uint32 * y: uint32 -> uint32 
    static member inline op_Division: x: nativeint * y: nativeint -> nativeint 
    static member inline op_Division: x: unativeint * y: unativeint -> unativeint 
    static member inline op_Division: x: int16 * y: int16 -> int16 
    static member inline op_Division: x: uint16 * y: uint16 -> uint16 
    static member inline op_Division: x: sbyte * y: sbyte -> sbyte 
    static member inline op_Division: x: byte * y: byte -> byte 
    static member inline op_Division: x: decimal * y: decimal -> decimal 

    static member inline op_Modulus: x: int32 * y: int32 -> int32 
    static member inline op_Modulus: x: float * y: float -> float 
    static member inline op_Modulus: x: float32 * y: float32 -> float32 
    static member inline op_Modulus: x: int64 * y: int64 -> int64 
    static member inline op_Modulus: x: uint64 * y: uint64 -> uint64 
    static member inline op_Modulus: x: uint32 * y: uint32 -> uint32 
    static member inline op_Modulus: x: nativeint * y: nativeint -> nativeint 
    static member inline op_Modulus: x: unativeint * y: unativeint -> unativeint 
    static member inline op_Modulus: x: int16 * y: int16 -> int16 
    static member inline op_Modulus: x: uint16 * y: uint16 -> uint16 
    static member inline op_Modulus: x: sbyte * y: sbyte -> sbyte 
    static member inline op_Modulus: x: byte * y: byte -> byte 
    static member inline op_Modulus: x: decimal * y: decimal -> decimal 

    static member inline op_CheckedAddition: x: int32 * y: int32 -> int32 
    static member inline op_CheckedAddition: x: float * y: float -> float 
    static member inline op_CheckedAddition: x: float32 * y: float32 -> float32 
    static member inline op_CheckedAddition: x: int64 * y: int64 -> int64 
    static member inline op_CheckedAddition: x: uint64 * y: uint64 -> uint64 
    static member inline op_CheckedAddition: x: uint32 * y: uint32 -> uint32 
    static member inline op_CheckedAddition: x: nativeint * y: nativeint -> nativeint 
    static member inline op_CheckedAddition: x: unativeint * y: unativeint -> unativeint 
    static member inline op_CheckedAddition: x: int16 * y: int16 -> int16 
    static member inline op_CheckedAddition: x: uint16 * y: uint16 -> uint16 
    static member inline op_CheckedAddition: x: char * y: char -> char 
    static member inline op_CheckedAddition: x: sbyte * y: sbyte -> sbyte 
    static member inline op_CheckedAddition: x: byte * y: byte -> byte 

    static member inline op_CheckedMultiply: x: int32 * y: int32 -> int32 
    static member inline op_CheckedMultiply: x: float * y: float -> float 
    static member inline op_CheckedMultiply: x: float32 * y: float32 -> float32 
    static member inline op_CheckedMultiply: x: int64 * y: int64 -> int64 
    static member inline op_CheckedMultiply: x: uint64 * y: uint64 -> uint64 
    static member inline op_CheckedMultiply: x: uint32 * y: uint32 -> uint32 
    static member inline op_CheckedMultiply: x: nativeint * y: nativeint -> nativeint 
    static member inline op_CheckedMultiply: x: unativeint * y: unativeint -> unativeint 
    static member inline op_CheckedMultiply: x: int16 * y: int16 -> int16 
    static member inline op_CheckedMultiply: x: uint16 * y: uint16 -> uint16 
    static member inline op_CheckedMultiply: x: sbyte * y: sbyte -> sbyte 
    static member inline op_CheckedMultiply: x: byte * y: byte -> byte 

    static member inline op_CheckedUnaryNegation: value: int32 -> int32
    static member inline op_CheckedUnaryNegation: value: float -> float
    static member inline op_CheckedUnaryNegation: value: float32 -> float32
    static member inline op_CheckedUnaryNegation: value: int64 -> int64
    static member inline op_CheckedUnaryNegation: value: int16 -> int16
    static member inline op_CheckedUnaryNegation: value: nativeint -> nativeint
    static member inline op_CheckedUnaryNegation: value: sbyte -> sbyte

    static member inline op_CheckedSubtraction: x: int32 * y: int32 -> int32 
    static member inline op_CheckedSubtraction: x: float * y: float -> float 
    static member inline op_CheckedSubtraction: x: float32 * y: float32 -> float32 
    static member inline op_CheckedSubtraction: x: int64 * y: int64 -> int64 
    static member inline op_CheckedSubtraction: x: uint64 * y: uint64 -> uint64 
    static member inline op_CheckedSubtraction: x: uint32 * y: uint32 -> uint32 
    static member inline op_CheckedSubtraction: x: nativeint * y: nativeint -> nativeint 
    static member inline op_CheckedSubtraction: x: unativeint * y: unativeint -> unativeint 
    static member inline op_CheckedSubtraction: x: int16 * y: int16 -> int16 
    static member inline op_CheckedSubtraction: x: uint16 * y: uint16 -> uint16 
    static member inline op_CheckedSubtraction: x: sbyte * y: sbyte -> sbyte 
    static member inline op_CheckedSubtraction: x: byte * y: byte -> byte 

    static member inline op_LeftShift: value: byte * shift: int -> byte 
    static member inline op_LeftShift: value: sbyte * shift: int -> sbyte 
    static member inline op_LeftShift: value: int16 * shift: int -> int16 
    static member inline op_LeftShift: value: uint16 * shift: int -> uint16 
    static member inline op_LeftShift: value: int32 * shift: int -> int32 
    static member inline op_LeftShift: value: uint32 * shift: int -> uint32 
    static member inline op_LeftShift: value: int64 * shift: int -> int64 
    static member inline op_LeftShift: value: uint64 * shift: int -> uint64 
    static member inline op_LeftShift: value: nativeint * shift: int -> nativeint 
    static member inline op_LeftShift: value: unativeint * shift: int -> unativeint 

    static member inline op_RightShift: value: byte * shift: int -> byte 
    static member inline op_RightShift: value: sbyte * shift: int -> sbyte 
    static member inline op_RightShift: value: int16 * shift: int -> int16 
    static member inline op_RightShift: value: uint16 * shift: int -> uint16 
    static member inline op_RightShift: value: int32 * shift: int -> int32
    static member inline op_RightShift: value: uint32 * shift: int -> uint32 
    static member inline op_RightShift: value: int64 * shift: int -> int64 
    static member inline op_RightShift: value: uint64 * shift: int -> uint64 
    static member inline op_RightShift: value: nativeint * shift: int -> nativeint 
    static member inline op_RightShift: value: unativeint * shift: int -> unativeint 

    static member inline op_BitwiseAnd: x: int32 * y: int32 -> int32 
    static member inline op_BitwiseAnd: x: int64 * y: int64 -> int64 
    static member inline op_BitwiseAnd: x: uint64 * y: uint64 -> uint64 
    static member inline op_BitwiseAnd: x: uint32 * y: uint32 -> uint32 
    static member inline op_BitwiseAnd: x: int16 * y: int16 -> int16 
    static member inline op_BitwiseAnd: x: uint16 * y: uint16 -> uint16 
    static member inline op_BitwiseAnd: x: nativeint * y: nativeint -> nativeint 
    static member inline op_BitwiseAnd: x: unativeint * y: unativeint -> unativeint 
    static member inline op_BitwiseAnd: x: sbyte * y: sbyte -> sbyte 
    static member inline op_BitwiseAnd: x: byte * y: byte -> byte 

    static member inline op_BitwiseOr: x: int32 * y: int32 -> int32 
    static member inline op_BitwiseOr: x: int64 * y: int64 -> int64 
    static member inline op_BitwiseOr: x: uint64 * y: uint64 -> uint64 
    static member inline op_BitwiseOr: x: uint32 * y: uint32 -> uint32 
    static member inline op_BitwiseOr: x: int16 * y: int16 -> int16 
    static member inline op_BitwiseOr: x: uint16 * y: uint16 -> uint16 
    static member inline op_BitwiseOr: x: nativeint * y: nativeint -> nativeint 
    static member inline op_BitwiseOr: x: unativeint * y: unativeint -> unativeint 
    static member inline op_BitwiseOr: x: sbyte * y: sbyte -> sbyte 
    static member inline op_BitwiseOr: x: byte * y: byte -> byte 

    static member inline op_ExclusiveOr: x: int32 * y: int32 -> int32 
    static member inline op_ExclusiveOr: x: int64 * y: int64 -> int64 
    static member inline op_ExclusiveOr: x: uint64 * y: uint64 -> uint64 
    static member inline op_ExclusiveOr: x: uint32 * y: uint32 -> uint32 
    static member inline op_ExclusiveOr: x: int16 * y: int16 -> int16 
    static member inline op_ExclusiveOr: x: uint16 * y: uint16 -> uint16 
    static member inline op_ExclusiveOr: x: nativeint * y: nativeint -> nativeint 
    static member inline op_ExclusiveOr: x: unativeint * y: unativeint -> unativeint 
    static member inline op_ExclusiveOr: x: sbyte * y: sbyte -> sbyte 
    static member inline op_ExclusiveOr: x: byte * y: byte -> byte

    static member inline op_LogicalNot: value: int32 -> int32 
    static member inline op_LogicalNot: value: int64 -> int64 
    static member inline op_LogicalNot: value: uint64 -> uint64 
    static member inline op_LogicalNot: value: uint32 -> uint32 
    static member inline op_LogicalNot: value: nativeint -> nativeint 
    static member inline op_LogicalNot: value: unativeint -> unativeint 
    static member inline op_LogicalNot: value: int16 -> int16 
    static member inline op_LogicalNot: value: uint16 -> uint16 
    static member inline op_LogicalNot: value: sbyte -> sbyte 
    static member inline op_LogicalNot: value: byte -> byte 

    static member inline op_Explicit: value: string -> byte 
    static member inline op_Explicit: value: float -> byte 
    static member inline op_Explicit: value: float32 -> byte 
    static member inline op_Explicit: value: int64 -> byte 
    static member inline op_Explicit: value: int32 -> byte 
    static member inline op_Explicit: value: int16 -> byte 
    static member inline op_Explicit: value: nativeint -> byte 
    static member inline op_Explicit: value: sbyte -> byte 
    static member inline op_Explicit: value: uint64 -> byte 
    static member inline op_Explicit: value: uint32 -> byte 
    static member inline op_Explicit: value: uint16 -> byte 
    static member inline op_Explicit: value: char -> byte 
    static member inline op_Explicit: value: unativeint -> byte 
    static member inline op_Explicit: value: byte -> byte 
    static member inline op_Explicit: value: string -> sbyte 
    static member inline op_Explicit: value: byte -> sbyte 
    static member inline op_Explicit: value: float -> sbyte 
    static member inline op_Explicit: value: float32 -> sbyte 
    static member inline op_Explicit: value: int64 -> sbyte 
    static member inline op_Explicit: value: int32 -> sbyte 
    static member inline op_Explicit: value: int16 -> sbyte 
    static member inline op_Explicit: value: nativeint -> sbyte 
    static member inline op_Explicit: value: sbyte -> sbyte 
    static member inline op_Explicit: value: uint64 -> sbyte 
    static member inline op_Explicit: value: uint32 -> sbyte 
    static member inline op_Explicit: value: uint16 -> sbyte 
    static member inline op_Explicit: value: char -> sbyte 
    static member inline op_Explicit: value: unativeint -> sbyte 
    static member inline op_Explicit: value: byte -> sbyte 
    static member inline op_Explicit: value: string -> uint16 
    static member inline op_Explicit: value: float -> uint16 
    static member inline op_Explicit: value: float32 -> uint16 
    static member inline op_Explicit: value: int64 -> uint16 
    static member inline op_Explicit: value: int32 -> uint16 
    static member inline op_Explicit: value: int16 -> uint16 
    static member inline op_Explicit: value: nativeint -> uint16 
    static member inline op_Explicit: value: sbyte -> uint16 
    static member inline op_Explicit: value: uint64 -> uint16 
    static member inline op_Explicit: value: uint32 -> uint16 
    static member inline op_Explicit: value: uint16 -> uint16 
    static member inline op_Explicit: value: char -> uint16 
    static member inline op_Explicit: value: unativeint -> uint16 
    static member inline op_Explicit: value: byte -> uint16 
    static member inline op_Explicit: value: string -> int16 
    static member inline op_Explicit: value: float -> int16 
    static member inline op_Explicit: value: float32 -> int16 
    static member inline op_Explicit: value: int64 -> int16 
    static member inline op_Explicit: value: int32 -> int16 
    static member inline op_Explicit: value: int16 -> int16 
    static member inline op_Explicit: value: nativeint -> int16 
    static member inline op_Explicit: value: sbyte -> int16 
    static member inline op_Explicit: value: uint64 -> int16 
    static member inline op_Explicit: value: uint32 -> int16 
    static member inline op_Explicit: value: uint16 -> int16 
    static member inline op_Explicit: value: char -> int16 
    static member inline op_Explicit: value: unativeint -> int16 
    static member inline op_Explicit: value: byte -> int16 
    static member inline op_Explicit: value: string -> uint32 
    static member inline op_Explicit: value: float -> uint32 
    static member inline op_Explicit: value: float32 -> uint32 
    static member inline op_Explicit: value: int64 -> uint32 
    static member inline op_Explicit: value: nativeint -> uint32 
    static member inline op_Explicit: value: int32 -> uint32 
    static member inline op_Explicit: value: int16 -> uint32 
    static member inline op_Explicit: value: sbyte -> uint32 
    static member inline op_Explicit: value: uint64 -> uint32 
    static member inline op_Explicit: value: uint32 -> uint32 
    static member inline op_Explicit: value: uint16 -> uint32 
    static member inline op_Explicit: value: char -> uint32 
    static member inline op_Explicit: value: unativeint -> uint32 
    static member inline op_Explicit: value: byte -> uint32 
    static member inline op_Explicit: value: string -> int32 
    static member inline op_Explicit: value: float -> int32 
    static member inline op_Explicit: value: float32 -> int32 
    static member inline op_Explicit: value: int64 -> int32 
    static member inline op_Explicit: value: nativeint -> int32 
    static member inline op_Explicit: value: int32 -> int32 
    static member inline op_Explicit: value: int16 -> int32 
    static member inline op_Explicit: value: sbyte -> int32 
    static member inline op_Explicit: value: uint64 -> int32 
    static member inline op_Explicit: value: uint32 -> int32 
    static member inline op_Explicit: value: uint16 -> int32 
    static member inline op_Explicit: value: char -> int32 
    static member inline op_Explicit: value: unativeint -> int32 
    static member inline op_Explicit: value: byte -> int32 
    static member inline op_Explicit: value: string -> uint64 
    static member inline op_Explicit: value: float -> uint64 
    static member inline op_Explicit: value: float32 -> uint64 
    static member inline op_Explicit: value: int64 -> uint64 
    static member inline op_Explicit: value: int32 -> uint64 
    static member inline op_Explicit: value: int16 -> uint64 
    static member inline op_Explicit: value: nativeint -> uint64 
    static member inline op_Explicit: value: sbyte -> uint64 
    static member inline op_Explicit: value: uint64 -> uint64 
    static member inline op_Explicit: value: uint32 -> uint64 
    static member inline op_Explicit: value: uint16 -> uint64 
    static member inline op_Explicit: value: char -> uint64 
    static member inline op_Explicit: value: unativeint -> uint64 
    static member inline op_Explicit: value: byte -> uint64 
    static member inline op_Explicit: value: string -> int64 
    static member inline op_Explicit: value: float -> int64 
    static member inline op_Explicit: value: float32 -> int64 
    static member inline op_Explicit: value: int64 -> int64 
    static member inline op_Explicit: value: int32 -> int64 
    static member inline op_Explicit: value: int16 -> int64 
    static member inline op_Explicit: value: nativeint -> int64 
    static member inline op_Explicit: value: sbyte -> int64 
    static member inline op_Explicit: value: uint64 -> int64 
    static member inline op_Explicit: value: uint32 -> int64 
    static member inline op_Explicit: value: uint16 -> int64 
    static member inline op_Explicit: value: char -> int64 
    static member inline op_Explicit: value: unativeint -> int64 
    static member inline op_Explicit: value: byte -> int64 
    static member inline op_Explicit: value: string -> float32 
    static member inline op_Explicit: value: float -> float32 
    static member inline op_Explicit: value: float32 -> float32 
    static member inline op_Explicit: value: int64 -> float32 
    static member inline op_Explicit: value: int32 -> float32 
    static member inline op_Explicit: value: int16 -> float32 
    static member inline op_Explicit: value: nativeint -> float32 
    static member inline op_Explicit: value: sbyte -> float32 
    static member inline op_Explicit: value: uint64 -> float32 
    static member inline op_Explicit: value: uint32 -> float32 
    static member inline op_Explicit: value: uint16 -> float32 
    static member inline op_Explicit: value: char -> float32 
    static member inline op_Explicit: value: unativeint -> float32 
    static member inline op_Explicit: value: byte -> float32 
    static member inline op_Explicit: value: string -> float 
    static member inline op_Explicit: value: float -> float 
    static member inline op_Explicit: value: float32 -> float 
    static member inline op_Explicit: value: int64 -> float 
    static member inline op_Explicit: value: int32 -> float 
    static member inline op_Explicit: value: int16 -> float 
    static member inline op_Explicit: value: nativeint -> float 
    static member inline op_Explicit: value: sbyte -> float 
    static member inline op_Explicit: value: uint64 -> float 
    static member inline op_Explicit: value: uint32 -> float 
    static member inline op_Explicit: value: uint16 -> float 
    static member inline op_Explicit: value: char -> float 
    static member inline op_Explicit: value: unativeint -> float 
    static member inline op_Explicit: value: byte -> float 
    static member inline op_Explicit: value: decimal -> float 
    static member inline op_Explicit: value: string -> decimal 
    static member inline op_Explicit: value: float -> decimal 
    static member inline op_Explicit: value: float32 -> decimal 
    static member inline op_Explicit: value: int64 -> decimal 
    static member inline op_Explicit: value: int32 -> decimal 
    static member inline op_Explicit: value: int16 -> decimal 
    static member inline op_Explicit: value: nativeint -> decimal 
    static member inline op_Explicit: value: sbyte -> decimal 
    static member inline op_Explicit: value: uint64 -> decimal 
    static member inline op_Explicit: value: uint32 -> decimal 
    static member inline op_Explicit: value: uint16 -> decimal 
    static member inline op_Explicit: value: unativeint -> decimal 
    static member inline op_Explicit: value: byte -> decimal 
    static member inline op_Explicit: value: decimal -> decimal 
    static member inline op_Explicit: value: string -> unativeint 
    static member inline op_Explicit: value: float -> unativeint 
    static member inline op_Explicit: value: float32 -> unativeint 
    static member inline op_Explicit: value: int64 -> unativeint 
    static member inline op_Explicit: value: int32 -> unativeint 
    static member inline op_Explicit: value: int16 -> unativeint 
    static member inline op_Explicit: value: nativeint -> unativeint 
    static member inline op_Explicit: value: sbyte -> unativeint 
    static member inline op_Explicit: value: uint64 -> unativeint 
    static member inline op_Explicit: value: uint32 -> unativeint 
    static member inline op_Explicit: value: uint16 -> unativeint 
    static member inline op_Explicit: value: char -> unativeint 
    static member inline op_Explicit: value: unativeint -> unativeint 
    static member inline op_Explicit: value: byte -> unativeint 
    static member inline op_Explicit: value: string -> nativeint 
    static member inline op_Explicit: value: float -> nativeint 
    static member inline op_Explicit: value: float32 -> nativeint 
    static member inline op_Explicit: value: int64 -> nativeint 
    static member inline op_Explicit: value: int32 -> nativeint 
    static member inline op_Explicit: value: int16 -> nativeint 
    static member inline op_Explicit: value: nativeint -> nativeint 
    static member inline op_Explicit: value: sbyte -> nativeint 
    static member inline op_Explicit: value: uint64 -> nativeint 
    static member inline op_Explicit: value: uint32 -> nativeint 
    static member inline op_Explicit: value: uint16 -> nativeint 
    static member inline op_Explicit: value: char -> nativeint 
    static member inline op_Explicit: value: unativeint -> nativeint 
    static member inline op_Explicit: value: byte -> nativeint 
    static member inline op_Explicit: value: string -> char 
    static member inline op_Explicit: value: float -> char 
    static member inline op_Explicit: value: float32 -> char 
    static member inline op_Explicit: value: int64 -> char 
    static member inline op_Explicit: value: int32 -> char 
    static member inline op_Explicit: value: int16 -> char 
    static member inline op_Explicit: value: nativeint -> char 
    static member inline op_Explicit: value: sbyte -> char 
    static member inline op_Explicit: value: uint64 -> char 
    static member inline op_Explicit: value: uint32 -> char 
    static member inline op_Explicit: value: uint16 -> char 
    static member inline op_Explicit: value: char -> char 
    static member inline op_Explicit: value: unativeint -> char 
    static member inline op_Explicit: value: byte -> char 

    static member inline op_LessThan: x: bool * y: bool -> bool
    static member inline op_LessThan: x: sbyte * y: sbyte -> bool
    static member inline op_LessThan: x: int16 * y: int16 -> bool
    static member inline op_LessThan: x: int32 * y: int32 -> bool
    static member inline op_LessThan: x: int64 * y: int64 -> bool
    static member inline op_LessThan: x: byte * y: byte -> bool
    static member inline op_LessThan: x: uint16 * y: uint16 -> bool
    static member inline op_LessThan: x: uint32 * y: uint32 -> bool
    static member inline op_LessThan: x: uint64 * y: uint64 -> bool
    static member inline op_LessThan: x: unativeint * y: unativeint -> bool
    static member inline op_LessThan: x: nativeint * y: nativeint -> bool
    static member inline op_LessThan: x: float * y: float -> bool
    static member inline op_LessThan: x: float32 * y: float32 -> bool
    static member inline op_LessThan: x: char * y: char -> bool
    static member inline op_LessThan: x: decimal * y: decimal -> bool
    static member inline op_LessThan: x: string * y: string -> bool

    static member inline op_GreaterThan: x: bool * y: bool -> bool
    static member inline op_GreaterThan: x: sbyte * y: sbyte -> bool
    static member inline op_GreaterThan: x: int16 * y: int16 -> bool
    static member inline op_GreaterThan: x: int32 * y: int32 -> bool
    static member inline op_GreaterThan: x: int64 * y: int64 -> bool
    static member inline op_GreaterThan: x: nativeint * y: nativeint -> bool
    static member inline op_GreaterThan: x: byte * y: byte -> bool
    static member inline op_GreaterThan: x: uint16 * y: uint16 -> bool
    static member inline op_GreaterThan: x: uint32 * y: uint32 -> bool
    static member inline op_GreaterThan: x: uint64 * y: uint64 -> bool
    static member inline op_GreaterThan: x: unativeint * y: unativeint -> bool
    static member inline op_GreaterThan: x: float * y: float -> bool
    static member inline op_GreaterThan: x: float32 * y: float32 -> bool
    static member inline op_GreaterThan: x: char * y: char -> bool
    static member inline op_GreaterThan: x: decimal * y: decimal -> bool
    static member inline op_GreaterThan: x: string * y: string -> bool

    static member inline op_LessThanOrEqual: x: bool * y: bool -> bool
    static member inline op_LessThanOrEqual: x: sbyte * y: sbyte -> bool
    static member inline op_LessThanOrEqual: x: int16 * y: int16 -> bool
    static member inline op_LessThanOrEqual: x: int32 * y: int32 -> bool
    static member inline op_LessThanOrEqual: x: int64 * y: int64 -> bool
    static member inline op_LessThanOrEqual: x: nativeint * y: nativeint -> bool
    static member inline op_LessThanOrEqual: x: byte * y: byte -> bool
    static member inline op_LessThanOrEqual: x: uint16 * y: uint16 -> bool
    static member inline op_LessThanOrEqual: x: uint32 * y: uint32 -> bool
    static member inline op_LessThanOrEqual: x: uint64 * y: uint64 -> bool
    static member inline op_LessThanOrEqual: x: unativeint * y: unativeint -> bool
    static member inline op_LessThanOrEqual: x: float * y: float -> bool
    static member inline op_LessThanOrEqual: x: float32 * y: float32 -> bool
    static member inline op_LessThanOrEqual: x: char * y: char -> bool
    static member inline op_LessThanOrEqual: x: decimal * y: decimal -> bool
    static member inline op_LessThanOrEqual: x: string * y: string -> bool
    
    static member inline op_GreaterThanOrEqual: x: bool * y: bool -> bool
    static member inline op_GreaterThanOrEqual: x: sbyte * y: sbyte -> bool
    static member inline op_GreaterThanOrEqual: x: int16 * y: int16 -> bool
    static member inline op_GreaterThanOrEqual: x: int32 * y: int32 -> bool
    static member inline op_GreaterThanOrEqual: x: int64 * y: int64 -> bool
    static member inline op_GreaterThanOrEqual: x: nativeint * y: nativeint -> bool
    static member inline op_GreaterThanOrEqual: x: byte * y: byte -> bool
    static member inline op_GreaterThanOrEqual: x: uint16 * y: uint16 -> bool
    static member inline op_GreaterThanOrEqual: x: uint32 * y: uint32 -> bool
    static member inline op_GreaterThanOrEqual: x: uint64 * y: uint64 -> bool
    static member inline op_GreaterThanOrEqual: x: unativeint * y: unativeint -> bool
    static member inline op_GreaterThanOrEqual: x: float * y: float -> bool
    static member inline op_GreaterThanOrEqual: x: float32 * y: float32 -> bool
    static member inline op_GreaterThanOrEqual: x: char * y: char -> bool
    static member inline op_GreaterThanOrEqual: x: decimal * y: decimal -> bool
    static member inline op_GreaterThanOrEqual: x: string * y: string -> bool

    static member inline op_Equality: x: bool * y: bool -> bool
    static member inline op_Equality: x: sbyte * y: sbyte -> bool
    static member inline op_Equality: x: int16 * y: int16 -> bool
    static member inline op_Equality: x: int32 * y: int32 -> bool
    static member inline op_Equality: x: int64 * y: int64 -> bool
    static member inline op_Equality: x: byte * y: byte -> bool
    static member inline op_Equality: x: uint16 * y: uint16 -> bool
    static member inline op_Equality: x: uint32 * y: uint32 -> bool
    static member inline op_Equality: x: uint64 * y: uint64 -> bool
    static member inline op_Equality: x: float * y: float -> bool
    static member inline op_Equality: x: float32 * y: float32 -> bool
    static member inline op_Equality: x: char * y: char -> bool
    static member inline op_Equality: x: nativeint * y: nativeint -> bool
    static member inline op_Equality: x: unativeint * y: unativeint -> bool
    static member inline op_Equality: x: string * y: string -> bool
    static member inline op_Equality: x: decimal * y: decimal -> bool
    
    static member inline op_Inequality: x: bool * y: bool -> bool
    static member inline op_Inequality: x: sbyte * y: sbyte -> bool
    static member inline op_Inequality: x: int16 * y: int16 -> bool
    static member inline op_Inequality: x: int32 * y: int32 -> bool
    static member inline op_Inequality: x: int64 * y: int64 -> bool
    static member inline op_Inequality: x: byte * y: byte -> bool
    static member inline op_Inequality: x: uint16 * y: uint16 -> bool
    static member inline op_Inequality: x: uint32 * y: uint32 -> bool
    static member inline op_Inequality: x: uint64 * y: uint64 -> bool
    static member inline op_Inequality: x: float * y: float -> bool
    static member inline op_Inequality: x: float32 * y: float32 -> bool
    static member inline op_Inequality: x: char * y: char -> bool
    static member inline op_Inequality: x: nativeint * y: nativeint -> bool
    static member inline op_Inequality: x: unativeint * y: unativeint -> bool
    static member inline op_Inequality: x: string * y: string -> bool
    static member inline op_Inequality: x: decimal * y: decimal -> bool
    
    static member inline DivideByInt: x: float * y: int -> float
    static member inline DivideByInt: x: float32 * y: int -> float32
    static member inline DivideByInt: x: decimal * y: int -> decimal
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

## Witnesses for built in primitives
[unresolved-builtin-witnesses]: #witnesses-for-built-in-primitives

The list of new builtin withess solution primitives is long and bloats FSharp.Core. As an alternative it seems possible to provide one **generic** witness method for each `op_Addition` etc. and use the "type specialization" capability of .NET to ensure that efficient machine code is generated for each particular value-type instantiation.  Specifically .NET Core has a type-equality optimization for code generation that allows C# code like this:
```
 
static op_Addition<T>(T x, T y) 
{
    if (typeof(T) == typeof(int)) { return ((object) x as int) + ((object) y as int); }
    else if (type(T) == typeof(int16)) { return ((object) x as int16) + ((object) y as int16); }
    ...
    else raise (new Exception("unexpected type"))
}
```
to be optimized by the JIT for the type instantiations are generated.  

This would allow us to reduce to just adding these as witnesses for the primitive constraints, with appropriate type-cased implementations.
```
/// <summary>Representative witnesses for traits solved by the F# compiler</summary>
type BuiltInWitnesses = 
    static member inline op_Addition: x: 'T * y: 'T -> 'T
    static member inline op_Multiply: x: 'T * y: 'T -> 'T
    static member inline op_UnaryNegation: value: 'T -> 'T
    static member inline op_Subtraction: x: 'T * y: 'T -> 'T
    static member inline op_Division: x: 'T * y: 'T -> 'T
    static member inline op_Modulus: x: 'T * y: 'T -> 'T
    static member inline op_CheckedAddition: x: 'T * y: 'T -> 'T
    static member inline op_CheckedMultiply: x: 'T * y: 'T -> 'T
    static member inline op_CheckedUnaryNegation: value: 'T -> 'T
    static member inline op_CheckedSubtraction: x: 'T * y: 'T -> 'T
    static member inline op_LeftShift: value: 'T * shift: int -> 'T
    static member inline op_RightShift: value: 'T * shift: int -> 'T
    static member inline op_BitwiseAnd: x: 'T * y: 'T -> 'T
    static member inline op_BitwiseOr: x: 'T * y: 'T -> 'T
    static member inline op_ExclusiveOr: x: 'T * y: 'T -> 'T
    static member inline op_LogicalNot: value: 'T -> 'T
    static member inline op_Explicit: value: 'T1 -> 'T2
    static member inline op_LessThan: x: 'T * y: 'T -> bool
    static member inline op_GreaterThan: x: 'T * y: 'T -> bool
    static member inline op_LessThanOrEqual: x: 'T * y: 'T -> bool
    static member inline op_GreaterThanOrEqual: x: 'T * y: 'T -> bool
    static member inline op_Equality: x: 'T * y: 'T -> bool
    static member inline op_Inequality: x: 'T * y: 'T -> bool
    static member inline DivideByInt: x: 'T * y: int -> 'T
```




