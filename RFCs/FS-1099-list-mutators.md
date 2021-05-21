# F# RFC FS-1099 - Compiler helpers for list mutators

During work on FS-1087 and FS-1097, it became obvious that it's possible to build a substantially fast `list { ... }` builder
if we allow FSharp.Core to include compiler/builder-use-only helpers for implementing tail-cons mutation on F# lists.

Any variation on this will require FSharp.Core library intrinsic helpers.

This suggestion has been "approved in principle".
This RFC covers the detailed proposal for the resumable state machine support needed for this and other features.

- [x] Approved in principle
- [x] [RFC Discussion](https://github.com/fsharp/fslang-design/issues/455)
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/6811)

# Summary

During work on FS-1087 and FS-1097, it became obvious that it's possible to build a substantially fast `list { ... }` builder
if we allow FSharp.Core to include compiler/builder-use-only helpers for implementing tail-cons mutation on F# lists.

Any variation on this will require FSharp.Core library intrinsic helpers.

We add `SetFreshConsTail` and `FreshConsNoTail` helpers to RuntimeHelpers for this scenario


# Detailed Design 
```fsharp
namespace FSharp.Core.CompilerServices

module RuntimeHelpers = 

   [<MethodImpl(MethodImplOptions.NoInlining)>]
   [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
   val SetFreshConsTail: cons: 'T list -> tail: 'T list -> unit

   [<CompilerMessage("This function is for use by compiled F# code and should not be used directly", 1204, IsHidden=true)>]
   val inline FreshConsNoTail: head: 'T -> 'T list
```

# Drawbacks

This makes FSharp.Core lists mutable, from a purist logical perspective.  However
* the warnings given in the CompilerMessage warn against it's use

* other CompilerMessage-like library intrinsics exist (e.g. `(# " ... " #)` assembly code) that can be used to hack objects

* in practice users can actually mutate lists by using reflection, and we can't stop that.

* the long-term performance gains for list-building operations will be a bigger gain given how performance-critical these can be.

* Since .NET Core there is no longer any pretence that the .NET type system implements an absolute trust-boundary - it
  is there to establish software engineering properties. For example code access
  security has been removed, as has partial trust execution. Further, F# the logical immutability of F# lists isn't part of
  any trust boundary even on .NET Framework aren't beyond the level of a programming team's expectations.  There are numerous
  low-level mechanisms that can be abused (e.g. private reflection and mutation), this adds another but for good reasons.

# Unresolved questions

None
