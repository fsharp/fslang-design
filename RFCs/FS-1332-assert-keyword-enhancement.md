# F# RFC FS-1332 - `assert` keyword enhancement

The design suggestion [`assert` keyword: have compiler emit call to `Debug.Assert` overload with CallerArgumentExpression when possible](https://github.com/dotnet/fsharp/issues/18489) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/dotnet/fsharp/issues/18489)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/17519)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

# Summary

When an `assert` expression failed, it will shows the text of the expression in the exception message.

# Motivation

When the `assert` expression failed, it does not provide the details of the expression, but the `System.Diagnostics.Debug.Assert` does.

# Detailed design

`assert <bool expression>` will be translated into `System.Diagnostics.Debug.Assert(<bool expression>, "<bool expression>")`.

```fsharp
assert (1 + 1 = 2) // This will be translated into
System.Diagnostics.Debug.Assert((1 + 1 = 2), "(1 + 1 = 2)")
```

Since the [Debug.Assert(Boolean, String)](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.debug.assert?view=net-8.0#system-diagnostics-debug-assert(system-boolean-system-string)) overload has the same supporting runtime as the [Debug.Assert(Boolean)](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.debug.assert?view=net-8.0#system-diagnostics-debug-assert(system-boolean)), changing the `assert` expression to the former overload will not cause runtime errors.

# Changes to the F# spec

In [6.5.12 Assertion Expressions](https://github.com/fsharp/fslang-spec/blob/main/releases/FSharp-Spec-latest.md#6512-assertion-expressions), `System.Diagnostics.Debug.Assert(expr)` changes to `System.Diagnostics.Debug.Assert(<expr>, "<expr>")`.

# Drawbacks

No.

# Alternatives

By supporting [`[<OverloadResolutionPriorityAttribute>]` introduced in .NET 9](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.overloadresolutionpriorityattribute?view=net-9.0), the compiler can auto take the overload with `CallerArgumentExpression` when possible, and will not need this particular RFC. See [the comment](https://github.com/dotnet/fsharp/issues/18489#issuecomment-2831042424) from the original suggestion.

The drawback of this alternative is that it will not work with .NET 8 and below.

# Prior art

With [supporting `[<CallerArgumentExpressionAttribute>]`](./FS-1149-support-CallerArgumentExpression.md), the `System.Diagnostics.Debug.Assert(<bool expression>)` can show the text of the expression in the exception message. This RFC is making `assert` expression works the same way.

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change?
    > No
* What happens when previous versions of the F# compiler encounter this design addition as source code?
    > It will works the same as before.
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
    > It will works the same as before.
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?
    > N/A
# Interop

* What happens when this feature is consumed by another .NET language?
    > It will works the same as before.
* Are there any planned or proposed features for another .NET language (e.g., [C#](https://github.com/dotnet/csharplang)) that we would want this feature to interoperate with?
    > N/A

# Pragmatics

## Performance

This feature may impact the compilation speed when the code file is too large and has many `assert` expressions, since it needs get substrings from the file.

## Scaling

Please list the dimensions that describe the inputs for this new feature, e.g. "number of widgets" etc.  For each, estimate a reasonable upper bound for the expected size in human-written code and machine-generated code that the compiler will accept.

For example

* Expected maximum number of widgets in reasonable hand-written code: 100
* Expected reasonable upper bound for number of widgets accepted: 500

Testing should particularly check that compilation is linear (or log-linear or similar) along these dimensions.  If quadratic or worse this should ideally be noted in the RFC.
