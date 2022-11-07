# F# RFC FS-1131 - NoCompilerInliningAttribute

NOTE: new sections have been added to this template! Please use this template rather than copying an existing RFC.

[The design suggestion](https://github.com/fsharp/fslang-suggestions/issues/838) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/838)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/14235)
- [ ] Design Review Meeting(s) with @dsyme and others invitees
- [ ] Discussion

# Summary

The F# compiler has the ability to decide whether to inline user-defined values and functions. Users can enforce this behavior with the `inline` keyword. Additionally, the JIT compiler can also decide to inline methods at run-time.

`MethodImplAttribute(MethodImplOptions.NoInlining)` is a means of forcing both compilers **not** to inline. However, there is currently no way to tell the F# compiler not to inline something, while leaving the JIT compiler free to do so. The proposed `NoCompilerInliningAttribute` remedies this situation.

# Motivation

Having the F# compiler inline functions can result in performance degradation in [some scenarios](https://github.com/dotnet/fsharp/issues/5178#issuecomment-398563190) where the JIT compiler would have produced superior machine code by inlining on its own.

# Detailed design

We add `NoCompilerInliningAttribute` to FSharp.Core. The attribute can be applied to both let-bound values, functions and instance methods, which the F# compiler then guarantees not to inline:

```fsharp
let functionInlined () = 3

[<NoCompilerInlining>]
let functionNotInlined () = 3

let six () = functionInlined () + functionNotInlined ()
```

By inspecting the emitted IL we want to see that the second function is not inlined:

```
.method public static int32  six() cil managed
{

  .maxstack  8
  IL_0000:  ldc.i4.3
  IL_0001:  call       int32 MyModule::functionNotInlined()
  IL_0006:  add
  IL_0007:  ret
}
```

Using both `inline` and `NoCompilerInliningAttribute` should result in a compilation error.

# Drawbacks

Having 2 attributes and a keyword that control inlining in different ways might be confusing, especially when `NoCompilerInliningAttribute` and `MethodImplAttribute(MethodImplOptions.AggressiveInlining)` are combined. However, the target audience of these attributes are advanced users and experts, so this is not a major concern. Run-of-the-mill applications will at most make use of `inline`.

# Alternatives

What other designs have been considered?

Using a keyword instead of an attribute.

What is the impact of not doing this?

Inability to speed up certain types of highly-specialized, performance-critical code.

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change?

  No.

* What happens when previous versions of the F# compiler encounter this design addition as source code?

  Code compiles fine, but values and functions might be inlined despite the attribute.

* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?

  Values and functions from compiled binaries might be inlined despite the attribute.

* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?

  Code compiles fine, but values and functions might be inlined despite the attribute.

# Unresolved questions

None.
