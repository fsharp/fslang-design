# F# RFC FS-1018 - Adjust extension method scope

This RFC tracks a fix which in certain circumstances can adjust the behaviour of existing F# code.

# Summary
[summary]: #summary

In F# 4.0 and before, C#-style extension methods were not in scope within their own implementation.

In F# 4.1, these extension methods are now considered in scope. In very rare circumstances, this can change
the behaviour of existing F# code.

# Motivation
[motivation]: #motivation

This is a bug fix, but being documented as an RFC entry for reference.

# Detailed design
[design]: #detailed-design

[This issue](https://github.com/Microsoft/visualfsharp/issues/1296) was the original report of code that no longer compiles.

Consider this example using C#-style extension members:

```fsharp
open System.Text
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

[<Sealed>]
type StringBuilder with 
    member x.Append (y:StringBuilder) = x.Append(y)
```

In F# 4.0, the above pathological code compiled and the ``x.Append(y)`` call resolved to ``StringBuilder::Append(object)``.  The inferred return type of the extension method is ``StringBuilder``.

In contrast, consider the following code using F#-style extension members:

```fsharp
[<Sealed>]

  type StringBuilder with 
    member x.Append (y:StringBuilder) = x.Append(y)
```
In F# 4.0, the above pathological code compiled and the ``x.Append(y)`` call resolves correctly to a recursive call to the extension member itself. Since the code never terminates, the inferred return type of the extension method is generic.

In F# 4.1, the first case is adjusted to have the same behaviour as the second casse.

Here is the original example:
```fsharp
open System.Text
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

[<Extension; Sealed>]
type T = 
    [<Extension>] static member Append (x:StringBuilder, y:StringBuilder) = StringBuilder().Append(x).Append(y)
```

Basically, the question is whether C#-style extension members are in scope within their own implementations. Clearly they should be.
This is the behaviour now implemented in F# 4.1 and is correct according to the F# language specification.

The fix was made as part of [F# RFC FS-1009 - Allow mutually referential types and modules over larger scopes within files](https://github.com/fsharp/FSharpLangDesign/blob/master/FSharp-4.1/FS-1009-mutually-referential-types-and-modules-single-scope.md).

# Drawbacks
[drawbacks]: #drawbacks

It is a bug fix that changes behaviour of existing code.

# Alternatives
[alternatives]: #alternatives

The alternative would have been to preserve F# 4.0  behaviour, requiring  fairly complicated and subtle adjustments in the compiler.

# Unresolved questions
[unresolved]: #unresolved-questions

None
