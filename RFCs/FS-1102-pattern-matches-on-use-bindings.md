# F# RFC FS-1102 - Pattern matches on use bindings

The design suggestion [Allow underscore in use bindings](https://github.com/fsharp/fslang-suggestions/issues/881) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] Approved in principle
- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/881)
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [ ] Design Review Meeting(s) with @dsyme and others invitees
- [Discussion](https://github.com/fsharp/fslang-design/discussions/578)

# Summary

The left-hand-side receiver of `use` bindings outside of computation expressions should match how they are inside of computation expressions.

# Motivation

F# 4.7 implemented [wildcard self identifiers](https://github.com/fsharp/fslang-suggestions/issues/333) as two underscores were frequently
used in member definitions to denote an ignored "self" identifier and this seemed like a hack given that the language already provided a wildcard
pattern that represents an unused value. However, this same scenario still exists for `use` bindings.

Moreover, @dsyme wrote [code](https://github.com/fsharp/fslang-suggestions/issues/693#issuecomment-429268686) dependent on `use` accepting a tuple
pattern on the left-hand-side before as a workaround to F# not disposing tuple recursively. However, this code:
```fs
use device, swapChain = Dispose2(SharpDX.Direct3D11.Device.CreateWithSwapChain(DriverType.Hardware, SharpDX.Direct3D11.DeviceCreationFlags.None, chainSwapDescription))
```
when used in practice
```fs
open System
type Dispose2(x:System.IDisposable, y:System.IDisposable) =
    interface System.IDisposable with
        override _.Dispose() =
            x.Dispose()
            y.Dispose()
type C() =
    member _.M() =
        let x, y = Dispose2(new System.IO.MemoryStream(), new System.IO.MemoryStream())
        use x = x
        use y = y
        ()
```
does not actually work:
```
error FS0001: This expression was expected to have type
    ''a * 'b'    
but here has type
    'Dispose2' 
```

This indicates that until F# developers actually tried to put tuple patterns on the left hand side of `use` bindings, they expected it to work instead of it erroring.
This is unexpected since pattern matching with `let` bindings work fine but `use` bindings which are similar to `let` bindings, do not.

Moreover, there also exists an inconsistency between `use` bindings outside of computation expressions and inside computation expressions, with an example of
```fs
open System.IO
let (|Id|) =
    printfn "Hi"
    id
do
    use Id _ = new MemoryStream() // error FS0193: Type constraint mismatch. The type ''a -> MemoryStream' is not compatible with type 'System.IDisposable'
    async {
       use Id _ = new MemoryStream()
       return ()
    } |> Async.Start
```
whereas removing that line enables the code to be compiled.

As [@cartermp mentioned](https://github.com/dotnet/fsharp/issues/8570#issuecomment-684083971), the above code with the erroneous line removed will never be
disallowed since it would be a breaking change, so the only recourse is to do nothing or make it work outside the computation expression.

This RFC describes what would happen if we do take the second option.

# Detailed design

Any `use` binding that compiles fine when inside computation expressions should compile fine when the surrounding computation expression is removed.
This would result in any `{pattern}` that is valid in `let ({pattern}) = ...` be valid in `use {pattern} = ...`.

This would involve the pattern parser for function arguments, left hand side of `let` bindings, `match` expression patterns as well as `function`
expression patterns to be reused to process left hand side of `use` bindings.

Like `let` bindings, and incomplete patterns specified will produce warnings related to incompleteness:
```
warning FS0025: Incomplete pattern matches on this expression.
```

The value to be disposed at the end of the code block containing the `use` binding would be the value returned by the right hand side of the `use` binding,
instead of values returned by variable patterns at the left hand side of the `use` binding.

As a result, both code above originally producing errors will now work.

# Drawbacks

This introduces more complexity to the language.

# Alternatives

- Not doing this. Workarounds will have to be used for ignoring results from `use` bindings like
```fs
use __ = ...
__ |> ignore // Mutes the unused variable warning
```

- Only implementing the case of the underscore. This only eliminates one of the three reasons for this feature.

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change? No.
* What happens when previous versions of the F# compiler encounter this design addition as source code? Erorr as usual.
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries? The code will work.
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct? This is not a change to FSharp.Core.

# Unresolved questions

None.
