# F# RFC FS-1347 - `reraise` in computation expressions

The design suggestion [`return reraise()` under an `async{}` context should be possible](https://github.com/fsharp/fslang-suggestions/issues/660) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/660)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/20405)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

# Summary

Permit `reraise ()` in computation expression handlers.

# Motivation

Computation expression handlers reject `reraise ()` with FS0413. Preserving exception identity and the original stack trace requires calling `ExceptionDispatchInfo.Capture(e).Throw()` explicitly.

```fsharp
async {
    try
        return! callService ()
    with e ->
        logger.LogError(e, "call failed")
        return reraise ()
}
```

# Detailed design

## Semantics

In a computation expression handler, `reraise ()` refers to the exception passed to that handler throughout the lexical extent of its `with` clause. This applies whether or not the pattern names the exception.

Each invocation translates to `ExceptionDispatchInfo.Capture(e).Throw()`. It throws the same object, retains the existing stack trace, and adds an exception-dispatch boundary and rethrow frame. Direct-handler `reraise ()` keeps its existing IL `rethrow` semantics.

A closure can invoke `reraise ()` after its handler returns, repeatedly, or concurrently. Repeated calls can add exception-dispatch boundaries. Concurrent calls do not guarantee a deterministic trace shape.

In quotations, `reraise ()` produces a call to `ExceptionDispatchInfo.Capture(e).Throw()`.

## Applicability

The feature applies to builder-based computation expressions and to `seq`, list, and array expressions. The handler input type must be inferred as exactly `exn`.

| Position | Result |
|---|---|
| Handler body or guard, including after a bind | Allowed |
| Nested computation expression without a handler, local function, lambda, or `finally` | Inherits the enclosing computation expression handler |
| Protected body of a nested `try ... with` | Inherits the enclosing computation expression handler |
| `with` clause of a nested direct `try ... with` | Uses existing direct-handler rules and does not inherit the outer binding |
| `with` clause of a nested computation expression handler | Refers to that handler's exception |
| Outside any handler | FS0413 |
| Handler input type not inferred as exactly `exn` | FS0413 |
| First-class use such as `let f = reraise` | FS0417 |

In `seq`, list, and array expressions, a `when` guard that executes `reraise ()` runs once and skips subsequent clauses. It propagates the same exception with the guard frame and an exception-dispatch boundary.

# Changes to the F# spec

- **Expressions / Control Flow Expressions / Reraise Expressions**: permit `reraise ()` in the body and guards of a computation-expression or `seq`/list/array handler whose input type is inferred as exactly `exn`.
- **Expressions / Computation Expressions**: define exception identity, stack trace, lexical scope, quotation, and sequence-guard semantics.

# Drawbacks

The computation-expression and direct-handler forms have different stack-trace shapes.

# Alternatives

- Keep FS0413 and require explicit exception threading.
- Lower to IL `rethrow`. This is invalid outside a CLR catch block.

# Prior art

C# preserves `throw;` across compiler-generated suspension in an `async` catch handler, but rejects it in nested lambdas and local functions.

# Compatibility

This is an additive, language-version-gated source feature. Earlier compilers report FS0413. Compiled code uses existing BCL calls and adds no FSharp.Core dependency.

The language-version check applies to producer source. A consumer using an earlier language version can inline a public definition compiled with this feature.

# Interop

Other .NET languages observe an ordinary throw of the same exception object. The feature adds no public API or metadata.

# Pragmatics

## Diagnostics

Earlier language versions report the standard language-version diagnostic.

## Tooling

No changes.

## Performance

One `ExceptionDispatchInfo` capture and throw per invocation.

## Scaling

Not applicable.

## Culture-aware formatting/parsing

Not applicable.

# Unresolved questions

None.
