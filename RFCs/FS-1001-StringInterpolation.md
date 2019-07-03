
# F# RFC FS-1001 - String Interpolation

There is an approved-in-principle [proposal](http://fslang.uservoice.com/forums/245727-f-language/suggestions/6002107-steal-nice-println-syntax-from-swift) to extend the existing printf functionality in the F# language design with [string interpolation][2]. To discuss this design please us [design discussion thread][7].

  * [ ] Discussion: [under discussion](https://github.com/fsharp/FSharpLangDesign/issues/6)
  * [ ] Implementation: [Proof of concept submitted](https://github.com/Microsoft/visualfsharp/pull/921)

### Summary

This adds a new expresssion form called an interpolated string:

```fsharp
let x = 1
let pi = 3.1414
let text = "cats"
$"I say {x} is one and %0.2f{pi} is pi and %20s{text} are dogs"
```

Further, some extensions, alignments and adjustments are made to existing format strings.

### Links

* [F# printf formats](https://msdn.microsoft.com/en-us/visualfsharpdocs/conceptual/core.printf-module-%5Bfsharp%5D?f=255&MSPPError=-2147217396)
* [C# string interpolation docs](https://msdn.microsoft.com/en-us/library/dn961160.aspx)
* [Swift string interpolation](https://developer.apple.com/library/ios/documentation/Swift/Conceptual/Swift_Programming_Language/StringsAndCharacters.html)

### Motivation

String interpolation improves readability and reduces errors by staying visually closer to the end result.

In existing `printf` based formatting, the format string can contain format specifiers, which are embedded in the string, after which the expressions to embed are given in order. As a result, the format specification does not read naturally - it is broken up by specifiers like `%s` and `%A`, and to figure out which one corresponds to which argument the reader needs to do some mental scanning and counting. This can lead to some confusing results, e.g. arguments reversed by mistake.

String interpolation embeds the arguments into the format string, which results in one easier to understand overall expression. Also it makes the format specifiers optional.

Use cases include any for which printf and variants are currently used: console output, pretty printing, custom `ToString` implementations, construction of SQL statements, and so on.

### Detailed Design

1. `$"....{}..."` is a new form called an "interpolation string", and can contain either:

   * Type-checked printf style fills: `%printfFormat{<interpolationExpression>}`, e.g. `%d{x}` or `%20s{text}`.
 
   * Unchecked ".NET-style" fills: `{<interpolationExpression>[,<dotnetAlignment>][:<dotnetFormatString>]}`, e.g. `{x}` or `{y:N4}`.

   .NET-style fills are actually a shortcut for a `%O` printf pattern .....`%(dotnetAlignment,dotnetFormatString)O{interpolationExpression}..."`.

2. An interpolation string is checked as type `string`, `PrintfFormat`, `FormattableString` or `IFormattable`. The choice is based on the known type against which the expression is checked. We first try to unify to `string` and, if that fails, test for the other known types without unifying.

   Existing string literals continue to be interpreted as type `string` or `PrintfFormat` unchanged. The choice is based on the known type against which the expression is checked.

3. Interpolation fills (e.g `%d{x}`) may only be used in interpolation strings.  Unfilled printf-style placeholders (e.g. `%d`) may only be present for existing string literals.

4. The elaborated form of an interpolated string is as follows:

   - If interpreted as type `string` an implicit call to `sprintf` is inserted along with the creation of a `PrintfFormat` capturing the interpolated fills.

   - If interpreted as type `PrintfFormat` then an implicit creation of a `PrintfFormat` capturing the interpolated fills.
   
   - If interpreted as type `FormattableString` or `IFormattable` then an implicit creation of a `PrintfFormat` capturing the interpolated fills followed by `.ToFormattableString()` and an upcast if necessary.

5. The set of acceptable printf formats is extended to include `%O` patterns with .NET formatting, using `%(dotnetAlignment,dotnetFormatString)O`.  Once a value is available such an expression is formatted using `System.String.Format("{0:dotnetAlignment,dotnetFormatString}", value)`.  If `dotnetAlignment` and `dotnetFormatString` are both missing then this is equivalent to `value.ToString()` if `value` is non-null.  If `value` is `null` this is rendered as `<null>`.

6. The `PrintfFormat` type in FSharp.Core is extended as follows:

```
open System

type PrintfFormat<'Printer,'State,'Residue,'Result>
    // Existing
    new: value: string -> PrintfFormat<'Printer,'State,'Residue,'Result>
    member Value: string
    
    // New
    new: value: string * captures: obj[] * types: Type[] -> PrintfFormat<'Printer,'State,'Residue,'Result>
    member Captures: obj[]
    member Types: Type[]
    member ToFormattableString: unit -> FormattableString

type PrintfFormat<'Printer,'State,'Residue,'Result,'Tuple>
    //Exisiting
    new: value: string -> PrintfFormat<'Printer,'State,'Residue,'Result,'Tuple>
    
    // New
    new: value: string * captures: obj[] * types: Type[] -> PrintfFormat<'Printer,'State,'Residue,'Result,'Tuple>
```

We disallow a mix of `%d` and `%d{expr}` specifiers in a single format string.


### Tooling

The compiler service tooling is adjusted to account for understanding when we're in an interoplated context (and complete the `}` with brace completion).

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


### Open questions:

* Should the embedded expressions be restricted to some subset of possible F# expressions to prevent abuse? If so, how are the expression restricted?
    * One proposal is to restrict to identifiers and dotted names.
    * Depending on the restriction, this may exclude valid use cases. Many examples given include simple computations and function calls. Also a restriction increases complexity, as parsing and checking in an embedded expression has different rules which need to be checked and implemented separately.
    
* The format specifier `%a` doesn't really work with `IFormatProvider`  - perhaps we can alter the expected input signature of a custom formatting function, from `'State -> 'a -> 'Result` to `'State -> IFormatProvider -> 'a -> 'Result`

[2]:http://en.wikipedia.org/wiki/String_interpolation
[4]:http://msdn.microsoft.com/en-us/library/system.string.concat(v=vs.110).aspx
[5]:http://msdn.microsoft.com/en-us/library/system.object.tostring(v=vs.110).aspx
[6]:http://msdn.microsoft.com/en-us/library/ee370560.aspx
[7]:https://github.com/fsharp/FSharpLangDesign/issues/6
