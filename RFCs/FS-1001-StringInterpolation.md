
# F# RFC FS-1001 - String Interpolation

There is an approved-in-principle [proposal](http://fslang.uservoice.com/forums/245727-f-language/suggestions/6002107-steal-nice-println-syntax-from-swift) to extend the existing printf functionality in the F# language design with string interpolation. 

* [ ] Discussion: [under discussion](https://github.com/fsharp/fslang-design/issues/368)

* [ ] Implementation: [in progress](https://github.com/dotnet/fsharp/pull/8907)

### Summary

A new expresssion form called an interpolated string is added. 

```fsharp
let x = 1
let pi = 3.1414
let text = "cats"
$"I say {x} is one and %0.2f{pi} is pi and %20s{text} are dogs"
```

Interpolation fills can be both untyped `{x}` or typed `%d{x}`.  Untyped locations are interpreted as `%O{x}`, except that `null` values are formatted as the empty string.

### Motivation

String interpolation improves readability and reduces errors by staying visually closer to the end result.

In existing `printf` based formatting, the format string can contain format specifiers, which are embedded in the string, after which the expressions to embed are given in order. As a result, the format specification does not read naturally - it is broken up by specifiers like `%s` and `%A`, and to figure out which one corresponds to which argument the reader needs to do some mental scanning and counting. This can lead to some confusing results, e.g. arguments reversed by mistake.

String interpolation embeds the arguments into the format string, which results in one easier to understand overall expression. Also it makes the format specifiers optional.

Use cases include any for which printf and variants are currently used: console output, pretty printing, custom `ToString` implementations, construction of SQL statements, and so on.

### Detailed Design

1. `$"....{}..."` is a new form called an "interpolation string", and can contain either:

   * Type-checked "printf-style" fills: `%printfFormat{<interpolationExpression>}`, e.g. `%d{x}` or `%20s{text}`.
 
   * Unchecked ".NET-style" fills: `{<interpolationExpression>[,<dotnetAlignment>][:<dotnetFormatString>]}`, e.g. `{x}` or `{y:N4}` of `{z,10:N4}`
   
   A verbatim interpolation string `$@"...{}..."` or `@$"...{}...` is the interpolated counterpart of a verbatim string.
   
   A triple-quote interpolation string `$"""...{}..."""` is the interpolated counterpart of a triple-quote string.

   A literal `{` or `}` character, paired or not, must be escaped (by doubling) in an interpolation string.

2. An interpolation string is checked as type `string` or, if that fails, as type `System.FormattableString`. The choice is based on the known type against which the expression is checked. 

   Printf-style fills (e.g `%d{x}`) may only be used in interpolation strings typed as type `string`.
   
   .NET fills (e.g `{x}` or `{x:N}`) may be used in either interpolation strings typed as type `string` or `FormattableString`
   
   Unfilled printf-style (e.g. `%d`) may not be used in interpolation strings.

2. The elaborated form of an interpolated string is to a call to `Printf.isprintf` (for type `string`) and `Printf.ifsprintf` (for `FormattableString`) with a format string where interpolation holes have been replaces by `%P(dotnetFormatString)`. Some examples:

       "abc{x}" --> Printf.isprintf "abc%P()" x
       "abc{x,5}" --> Printf.isprintf "abc%5P()" x
       "abc{x:N3}" --> Printf.isprintf "abc%P(N3)" x
       "abc %d{x}" --> Printf.isprintf "abc%d%P()" x

3. `Printf.isprintf` executes as for `sprintf` except
   
   a. `%P` patterns generate the string produced by `System.String.Format("{0:dotnetAlignment,dotnetFormatString}", value)`
   
   b. a `%P()` pattern immediately following a `%d` or other `sprintf` format is ignored (since the processing of the fill will have been completed via the `%d`).
   
   c. If a value is `null` then it is formatted as the empty string.

4. Expressions used in fills for single-quote strings or verbatim strings **may not** include further string literals.

   Expressions used in fills for triple-quote strings **may** include single quote or verbatim string literals but not triple-quote literals.

   A mix of type-checked and unchecked fills **is** allowed in a single format string when typed as type `string`.

   A type-checked fill (such as `%d{x}`) may not use .NET alignment and format strings.  To align use, for example `%6d`.

   Byte strings, such as `"abc"B` do not support interoplation.

### Indentation

An interpolated string fill expression creates a new offside context for the purposes of indentation processing.  For example:

```fsharp
    $"abc {let x = 3
           x + x} def {let x = 3
                       x + x} xyz"
```
is a legitimate expression.

### Tooling

The compiler service tooling is adjusted to account for understanding when we're in an interoplated context (and complete the `}` with brace completion). It is expected that autocompletion will work in an interoplated context, as will any navigational features that work with symbols in a document.

### Drawbacks

This adds yet another way of formatting strings, on top of `string.Format` and adding to the complexity of `printf`.

### Alternatives

#### Syntax

An initial version of this RFC proposed `sprintf "%d(expression)"` as the form for interpolated strings.

There are many other options, mostly taken from other languages: `${expr}`, `${expr}`,`#expr`, `#{expr}`.

#### No printf

An initial version of this RFC proposed the interpolated be an argument to `printf`. In most other languages, this need not be the case. E.g. compare to C# where the string is simply prepended with a `$` sign. Similarly in F# `printf` could be optional or shortened using a symbol in front of the string.

#### Extensibility

An interesting alternative is to go for extensible string interpolation like Scala. The general idea in Scala is to allow a prefix to a string, which determines the formatting rules for the following interpolated string:

`f"$name%s is $height%2.2f meters tall"`

The leading `f` indicates that `sprintf` like formatting is to be used, but we could prefix with say `s` to indicate `string.Format` like formatting.

There is also some overlap here with extensible `sprintf` formatting so perhaps a middle ground is to allow the leading character to specify the processing of the arguments like you can do with `kprintf` as for example described [here](https://bugsquash.blogspot.co.uk/2010/07/abusing-printfformat-in-f.html).


### Resolved issues

> Should the embedded expressions be restricted to some subset of possible F# expressions to prevent abuse? If so, how are the expression restricted?

  One proposal was to restrict to identifiers and dotted names. However we decided to follow the C# spec and allow more complex expressions.

> Do we want to perform this codegen?  *"If an interpolated string has the type string, it's typically transformed into a `String.Format` method call. The compiler may replace `String.Format` with `String.Concat` if the analyzed behavior would be equivalent to concatenation." "If an interpolated string has the type `IFormattable` or `FormattableString`, the compiler generates a call to the `FormattableStringFactory.Create` method."* 

  Resolution: no, we won't do this, it would be irregular and an explicit call to String.Format can be used instead.

### Open questions:

* Should code generation for interpolation strings not using printf-style patterns be simpler (and much more efficient)?


### Links

* [F# printf formats](https://msdn.microsoft.com/en-us/visualfsharpdocs/conceptual/core.printf-module-%5Bfsharp%5D?f=255&MSPPError=-2147217396)
* [C# string interpolation docs](https://msdn.microsoft.com/en-us/library/dn961160.aspx)
* [.NET FormattableString class](https://docs.microsoft.com/en-us/dotnet/api/system.formattablestring?view=netframework-4.8)
* [.NET IFormatProvider example](https://docs.microsoft.com/en-us/dotnet/standard/base-types/how-to-define-and-use-custom-numeric-format-providers?view=netframework-4.8)
* [Swift string interpolation](https://developer.apple.com/library/ios/documentation/Swift/Conceptual/Swift_Programming_Language/StringsAndCharacters.html)
* http://en.wikipedia.org/wiki/String_interpolation

