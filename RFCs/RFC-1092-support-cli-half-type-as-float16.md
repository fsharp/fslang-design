# F# RFC FS-1092 - Support CLI half type as float16

The design suggestion [Support new CLI type: half type/float16](https://github.com/fsharp/fslang-suggestions/issues/909) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] Approved in principle
- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/909)
- [ ] [Implementation] tbd
- [x] Design Review Meeting(s) with @dsyme and others invitees: n/a
- [x] [Discussion](https://github.com/fsharp/fslang-design/issues/500)

# Summary

The runtime has added the `System.Half` which implements IEEE-754 binary-16 floating point. This proposal goes over the details of adding that type as an type alias to the language akin to how `float32` and `float` are available: that is, next to the type alias, casting functions, literals and utility functions.

# Motivation

The type `System.Half` is a new primitive type particularly targeted for high performance SIMD processing where the lesser precision is not a problem. The motivation is further explained [in the Runtime proposal](https://github.com/dotnet/runtime/issues/936).

# Detailed design

The types and functions in FSharp.Core will be extended with the following:

* Two type aliases, `half` and `float16` that map to `System.Half`.
* Two conversion functions named `float16` (compiled name `ToHalf`) and `half` as alias, with the following statically available overloads (open question: should we provide overloads for other primitives [than the BCL provides](https://docs.microsoft.com/en-us/dotnet/api/system.half?view=net-5.0)?)
  * `double -> half`
  * `float32 -> half`
  * `string -> half`
* Add overloads to functions `double` and `float` for: `half -> double`
* Add overloads for functions `single` and `float32` for: `half -> float32`
* Add public let binding `nanh: half`, compiled name `NaNHalf`
* Add public let binding `infinityh: half`, compiled name `InfinityHalf`
* Make sure all arithmetic operators work when both arguments are `half`s.
* Make sure generic comparison for two `half` arguments map to `Half.GreaterThan` etc.
* Add a numeric literal postfix letter, `h`, to allow parsing of literals as `half` types (open question)

Example code:

```fsharp
// must be callable with half arguments
let add x y = x + y

// x is of type `half`
let x = half 34.56

// x is of type `half`
let x = 42.99h
```

# Drawbacks

If we don't do this, people could still use `System.Half` directly, but don't have good language support, like overloaded conversion functions and proper compilation to IL.

# Alternatives

No alternatives are known.

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change?
  * No
* What happens when previous versions of the F# compiler encounter this design addition as source code?
  * They continue to work
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
  * Unless both binaries are compiled against .NET 5.0, this may lead to assembly loading errors.
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?
  * We should take proper measures to allow FSharp.Core to be linked with other versions of .NET that do not have this type.


# Unresolved questions

1. Should we add an additional numeric literal suffix?
2. How can we ensure FSharp.Core can be used with this new type in .NET 5.0, but still link to older versions?
3. Should we add overloads like `half: int -> half` and `half: decimal -> half` even though `System.Half` does not define them?
