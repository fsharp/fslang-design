
# F# RFC FS-1001 - String Interpolation

There is an approved-in-principle [proposal](http://fslang.uservoice.com/forums/245727-f-language/suggestions/6002107-steal-nice-println-syntax-from-swift) to extend the existing printf functionality in the F# language design with [string interpolation][2]. To discuss this design please us [design discussion thread][7].

  * [ ] Discussion: [under discussion](https://github.com/fsharp/FSharpLangDesign/issues/6)
  * [ ] Implementation: [Proof of concept submitted](https://github.com/Microsoft/visualfsharp/pull/921)

### Summary

Proposed syntax:

```fsharp
printf "some text %(expression) some more text"
```

### Links

* [C# string interpolation docs](https://msdn.microsoft.com/en-us/library/dn961160.aspx)
* [Swift string interpolation](https://developer.apple.com/library/ios/documentation/Swift/Conceptual/Swift_Programming_Language/StringsAndCharacters.html)

### Motivation

String interpolation improves readability and reduces errors by staying visually closer to the end result.

In existing `printf` based formatting, the format string can contain format specifiers, which are embedded in the string, after which the expressions to embed are given in order. As a result, the format specification does not read naturally - it is broken up by specifiers like `%s` and `%A`, and to figure out which one corresponds to which argument the reader needs to do some mental scanning and counting. This can lead to some confusing results, e.g. arguments reversed by mistake.

String interpolation embeds the arguments into the format string, which results in one easier to understand overall expression. Also it makes the format specifiers optional.

Use cases include any for which printf and variants are currently used: console output, pretty printing, custom `ToString` implementations, construction of SQL statements, and so on.

### Detailed Design

The prototype accepts arbitrary F# expressions as **embedded expressions** into an interpolated string. The whole string is given as an argument to `printf` and related functions.

The source string literal is split into chunks containing text and embedded expressions. Then chunks are joined using [String.Concat][4].

Initial string literal:

```fsharp
"%d%(foo)%d%(bar.bar)"
```

After splitting:

```fsharp
Text("%d"); Expression(foo); Text(%d); Expression(bar.bar)
```

Final result

```fsharp
String.Concat([| "%d"; box foo; "%d"; box (bar.baz) |])
```

### Drawbacks

This adds yet another way of formatting strings, on top of `string.Format` and adding to the complexity of `printf`.

### Alternatives

#### Syntax

The C# syntax uses curly braces to delineate the embedded expressions:

```fsharp
printf "some text %{expression} some more text"
```
This seems to be better liked alternative than the parens. (The original syntax was proposed before C# syntax was known.)

There are many other options, mostly taken from other languages: `${expr}`, `${expr}`,`#expr`, `#{expr}`.

Another alternative is to prefix the whole string and use simple curly braces: `printf $"some text {expr} some more text"`.

#### No printf

The interpolated string currently needs to be an argument to `printf`. In most other languages, this need not be the case. E.g. compare to C# where the string is simply prepended with a `$` sign. Similarly in F# `printf` could be optional or shortened using a symbol in front of the string.

#### Extensibility

An interesting alternative is to go for extensible string interpolation like Scala. The general idea in Scala is to allow a prefix to a string, which determines the formatting rules for the following interpolated string:

`f"$name%s is $height%2.2f meters tall"`

The leading `f` indicates that `sprintf` like formatting is to be used, but we could prefix with say `s` to indicate `string.Format` like formatting.

There is also some overlap here with extensible `sprintf` formatting so perhaps a middle ground is to allow the leading character to specify the processing of the arguments like you can do with `kprintf` as for example described [here](https://bugsquash.blogspot.co.uk/2010/07/abusing-printfformat-in-f.html).

### Open questions:

* Should the embedded expressions be restricted to some subset of possible F# expressions to prevent abuse? If so, how are the expression restricted?
    * One proposal is to restrict to identifiers and dotted names.
    * Depending on the restriction, this may exclude valid use cases. Many examples given include simple computations and function calls. Also a restriction increases complexity, as parsing and checking in an embedded expression has different rules which need to be checked and implemented separately.
* How do we add format specifiers to interpolated strings? There are several suggestions:
    * Before the embedded expression: `"text %02d(expr) text"` @latkin notes that this is a breaking change.
    * After the embedded expression `"text %(expr)02d text"`
    * Inside the embedded expression, specifier first, separated by colon `:`: `"text %(02d:expr) text"`.
    * Inside the embedded expression, expression first, then specifier separated by column `,`: `"text %(expr, 02d) text"`.
* Is the set of available format specifiers the same as the one that is allowed for `printf`? Some have proposed extra specifiers for interpolated strings. Also the full set of `printf` format strings may be overkill: effectively users already denote what they want to print.
* As opposed to the `printf` format string, in interpolated strings the format specifiers can be omitted. That means that the compiler needs to choose a reasonable default, based on the type of the embedded expression.
    * The current prototype chooses `%O` for everything. This doesn't work well with F# types, for which `%A` would be a better choice. Also, `%O` allows culture-specific behavior to seep in. But `%A` across the board doesn't work well either, for example for simple strings.
    * So it seems we need some type directed behavior here, but this needs to be specified.
* Could you use a mix of `%d` and `%(expr)` specifiers in a single format string? It seems nice to allow this, but it will likely increase implementation complexity.
* Culture needs to be considered. In C#'s string interpolation, the result can be implicitly converted to `IFormattable` which can then in turn be converted to a string by passing in a `CultureInfo`.
* Localization needs to be considered. In C#'s string interpolation, the result can be implicitly converted to `FormattableString` which allows you to inspect the objects that result from the interpolation computations, which can (among other things) be used for localization.
* As for implementation strategy, is the general idea of implementing this feature entirely on the semantic level acceptable?

### Detailed Changes to Language Specification

TBD

[2]:http://en.wikipedia.org/wiki/String_interpolation
[4]:http://msdn.microsoft.com/en-us/library/system.string.concat(v=vs.110).aspx
[5]:http://msdn.microsoft.com/en-us/library/system.object.tostring(v=vs.110).aspx
[6]:http://msdn.microsoft.com/en-us/library/ee370560.aspx
[7]:https://github.com/fsharp/FSharpLangDesign/issues/6
