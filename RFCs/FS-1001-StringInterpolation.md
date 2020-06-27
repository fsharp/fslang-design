
# F# RFC FS-1001 - String Interpolation

There is an approved-in-principle [proposal](http://fslang.uservoice.com/forums/245727-f-language/suggestions/6002107-steal-nice-println-syntax-from-swift) to extend the existing printf functionality in the F# language design with string interpolation. 

* [Discussion](https://github.com/fsharp/fslang-design/issues/368)
* [x] Implementation: [ready for review](https://github.com/dotnet/fsharp/pull/8907)
* [x] Design review meeting (26/06/2020, online, @dsyme, @cartermp, @TIHan, @jonsequitur)

### Summary

A new expresssion form called an interpolated string is added. 

```fsharp
let x = 1
let pi = 3.1414
let text = "cats"

let s = $"I say {x} is one and %0.2f{pi} is pi and %10s{text} are dogs"
//val s : string =  "I say 1 is one and 3.14 is pi and       cats are dogs"

printfn $"I say again {x} is one and %0.2f{pi} is pi"
// output: I say again 1 is one and 3.14 is pi
```

Interpolation fills can be both untyped `{x}` or typed `%d{x}`.  Untyped locations are interpreted as `%O{x}`, except that `null` values are formatted as the empty string.


### Motivation

String interpolation improves readability and reduces errors by staying visually closer to the end result.

In existing `printf` based formatting, the format string can contain format specifiers, which are embedded in the string, after which the expressions to embed are given in order. As a result, the format specification does not read naturally - it is broken up by specifiers like `%s` and `%A`, and to figure out which one corresponds to which argument the reader needs to do some mental scanning and counting. This can lead to some confusing results, e.g. arguments reversed by mistake.

String interpolation embeds the arguments into the format string, which results in one easier to understand overall expression. Also it makes the format specifiers optional.

Use cases include any for which printf and variants are currently used: console output, pretty printing, custom `ToString` implementations, construction of SQL statements, and so on.

### Design Principles

* Support the same syntax, use cases and technical features as C# interpolated strings.

* Unify printf formatting and interpolation strings.  They are the same thing, in as many way as possible, they become one integrated feature.  You can use knowledge of printf formatting when writing interpolation strings.  You can use interpolations in printf/fprintf/sprintf formatting.

* Keep the value of strong typing of printf formatting as an option, so changing `sprintf "the number is %d today" result` to `$"the number is %d{result} today"` is just as strongly typed.  

* Allow incremental typesafe adoption in any circumstance where printf formatting is supported, e.g. given a library using printf formatting:

      let log fmt = Printf.kprintf (fun s -> System.Console.Error.WriteLine("LOG: " + s)) fmt
  
  then 
  
      log "hello!"
      log "the number is %d today" result
 
  can be incrementally and accurately changed to this:

      log $"hello!" 
      log $"the number is %d{result} today" 

   without changing the library `log` function to take strings as arguments (it continues to take a PrintfFormat<...>), and without adjust all callsites of `log` all at once,
   and without losing type safety.

* Do not regress performance of `sprintf`, and have interpoaltion formatting be at least as fast as `printf`. (It is a non-goal to always be as performant as C# for all interpolation formatting)

### Detailed Design

`$"....{}..."` is a new form called an "interpolation string", and can contain either:

* Type-checked "printf-style" fills: `%printfFormat{<interpolationExpression>}`, e.g. `%d{x}` or `%20s{text}`.
 
* Unchecked ".NET-style" fills: `{<interpolationExpression>[,<dotnetAlignment>][:<dotnetFormatString>]}`, e.g. `{x}` or `{y:N4}` of `{z,10:N4}`
   
A _verbatim interpolation string_ `$@"...{}..."` or `@$"...{}...` is the interpolated counterpart of a verbatim string.
   
A _triple-quote interpolation string_ `$"""...{}..."""` is the interpolated counterpart of a triple-quote string.

A literal `{` or `}` character, paired or not, must be escaped (by doubling) in an interpolation string.

Expression fills for single-quote or verbatim interpolation strings **may not** include further string literals.
Expression fills for triple-quote interpolation strings **may** include single quote or verbatim string literals but not triple-quote literals.
   
Byte strings, such as `"abc"B` do not support interoplation.

An interpolation string is checked as:

1. type `string` or,
2. if that fails, as type `System.FormattableString` or,
3. if that fails, as type `System.IFormattable`, or
4. if that fails, as type `PrintfFormat<'Result, 'State, 'Residue, 'Result>`.
   
The choice is based on the known type against which the expression is checked. 

The following restrictions apply:

* Printf-style fills such as `%a{...}` implying multiple arguments may not be used in interpolation strings.
   
* Printf-style fills such as `%d{x}`) may not be used in interpolation strings typed as type `FormattableString` or `IFormattable`

* Printf-style fills such as `%d{x}` may not use .NET alignment or formats `%d{x:3,N}`.  To align use, for example `%6d`.

* Printf-style fills such as `%d` without a fill expression may not be used in interpolation strings.

A mix of type-checked and unchecked fills **is** allowed in a single format string when typed as type `string`. For example `$" abc %d{3} def {5}"` is allowed.

### Detailed Design - Elaborated Form

The elaborated form of an interpolated string depends on its type:

* An interpolated string with type `string` is elaborated to a call to `Printf.sprintf` with a format string where interpolation holes have been replaced by `%P(dotnetFormatString)` and the format string is built with a call to `new PrintfFormat<...>(format, array-of-captured-args, array-of-typeof-for-parcent-A-fills)`

* An interpolated string with the type `FormattableString` or `IFormattable` is elaborated to a call to the `FormattableStringFactory.Create` method.

* An interpolated string with type `PrintfFormat<...>` is elaborated to a call to `new PrintfFormat<...>(format, array-of-captured-args, array-of-typeof-for-parcent-A-fills)`

Some examples:

    $"abc{x}" --> Printf.sprintf (new PrintfFormat("abc%P()", [| x |], null))
       
    $"abc{x,5}" --> Printf.sprintf (new PrintfFormat("abc%5P()", [| x |], null))
       
    $"abc{x:N3}" --> Printf.sprintf (new PrintfFormat("abc%P(N3)", [| x |], null))
       
    $"abc %d{x}" --> Printf.sprintf (new PrintfFormat("abc%P()", [| x |], null))
       
    $"1 %A{x: int option} 2" --> Printf.sprintf (PrintfFormat("1 %P() 2", [| x |], [| typeof<int option> |]))

    printfn $"abc {x} {y:N}" --> printfn (new PrintfFormat("abc %P() %P(N)", [| box x; box y |]))

    ($"abc {x} {y:N}" : FormattableString) 
        --> FormattableStringFactory.Create("abc {0} {1:N}", [| box x; box y |])

Note that if `%A` patterns are used then `array-of-typeof-for-parcent-A-fills` is filled with the relevant static types, one for each `%A` in the pattern. These are used to correctly print `null` values with respect to their static type, e.g. `None` of type `option`, so 

    $"1 %A{x: int option} 2"

evaluates to

    $"1 None 2"

The runtime behaviour of `sprintf` is augmented with the following (note, this is not directly visible to the user since it deals with hidden `%P` patterns introduced by the compiler for interpolation fills):
   
* `%P` patterns generate the string produced by `System.String.Format("{0,dotnetAlignment:dotnetFormatString}", value)`
   
* a `%P()` pattern immediately following a `%d` or other `printf` format is ignored (the processing of the fill will have been completed via the `%d`).
   
* for `%P` patterns an interpolated value whose runtime representation is `null` is formatted as the empty string


### Activation

The feature is only activated when both:

1. The appropriate `--langversion` is selected, initially `--langversion:preview`

2. An FSharp.Core library supporting the feature is referenced at compile-time.  This is determined by the presence of the following constructor:

```fsharp
type PrintfFormat<'Printer,'State,'Residue,'Result,'Tuple> = 

    new: value:string * captures: obj[] -> PrintfFormat<'Printer,'State,'Residue,'Result,'Tuple>
```


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

### Performance expectations

* The performance when used at type "string" will be about the same as `sprintf`, which will be slower than C#.  I think any performance work we do should be to do compile-time optimizations that apply to both `sprintf` and interpolated strings.

* The performance when used at type "FormattableString" will be the same as C# as the code generated is the same.


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

> Do we want to perform this codegen?  *"If an interpolated string has the type string, it's typically transformed into a `String.Format` method call. The compiler may replace `String.Format` with `String.Concat` if the analyzed behavior would be equivalent to concatenation." 

  Resolution: no, we won't do this, it would be irregular and an explicit call to String.Format can be used instead.


### Links

* [F# printf formats](https://msdn.microsoft.com/en-us/visualfsharpdocs/conceptual/core.printf-module-%5Bfsharp%5D?f=255&MSPPError=-2147217396)
* [C# string interpolation docs](https://msdn.microsoft.com/en-us/library/dn961160.aspx)
* [.NET FormattableString class](https://docs.microsoft.com/en-us/dotnet/api/system.formattablestring?view=netframework-4.8)
* [.NET IFormatProvider example](https://docs.microsoft.com/en-us/dotnet/standard/base-types/how-to-define-and-use-custom-numeric-format-providers?view=netframework-4.8)
* [Swift string interpolation](https://developer.apple.com/library/ios/documentation/Swift/Conceptual/Swift_Programming_Language/StringsAndCharacters.html)
* http://en.wikipedia.org/wiki/String_interpolation

