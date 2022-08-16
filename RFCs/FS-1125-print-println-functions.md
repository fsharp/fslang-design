# F# RFC FS-1125 - Add `print` and `println` functions to FSharp.Core

The design suggestion [print and printn alongside printf and printfn](https://github.com/fsharp/fslang-suggestions/issues/1092) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1092)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/13597)
- [ ] Design Review Meeting(s) with @dsyme and others invitees
- [x] [Discussion](https://github.com/fsharp/fslang-design/discussions/675)

# Summary

This RFC introduces two new functions:
- `print<'T> : 'T -> unit`
- `println<'T> : 'T -> unit`

`print` writes the exact input to stdout (analogous to `System.Console.Write`) and `println` appends a newline when writing to stdout (analogous to `System.Console.WriteLine`).

# Motivation

These functions will simplify the user experience when working with non-formatted strings and other values. Currently, beginners to the language are introduced to `printf` and `printfn` as the default method for writing strings to the console. However, truly understanding these functions require understanding the `TextWriterFormat` type, which is generally out-of-scope for people initially learning the language. For example, the error generated from the following snippet would confuse many beginners.
```fs
let str = "A string"
printfn str // Error FS0001: The type 'string' is not compatible with the type 'Printf.TextWriterFormat<'a>'
```
Particularly after the introduction of interpolated strings, many users do not need the extra power of format strings. In these cases, `print` and `println` would provide a more user-friendly default for printing strings and other values.

# Detailed design

This RFC would add the two functions described above to the `FSharp.Core` library to supplement the existing `printf` and `printfn` functions. The `print` and `println` functions each take a generic type and write that string to stdout. `println` appends a newline to the output while `print` does not. The fact that these functions are capable of accepting a `string` argument means that string interpolation can still be used to provide formatting and variable capture.

Example code:

```fsharp
let str = "A string"
let num = 3
print str // prints "A string" with no trailing newline
println str // prints "A string" with a trailing newline
println 3 // prints "3" with a trailing newline
println () // prints a newline
println $"The value in str is %s{str}" // prints "The value in str is A string" with a trailing newline
println $"%0.3f{System.Math.PI}" // prints "3.142" with a trailing newline
```

# Drawbacks

The addition of these functions brings the core library to four different `print*` functions that are automatically available and an even higher number of `*print*` functions. There is some additional cognitive load to differentiating between these functions, particularly for experienced F# users who are already accustomed to `printf` and `printfn`.

# Alternatives

- Continuing to use `printf`, `printfn`, and `System.Console` for printing to stdout. This is the current solution, which is subject to the shortcomings described in the Motivation section.
- Providing these functions in a non-core library. While providing these functions in an external library, the fundamental problem is the ease of access to these functions. They should be accessible by default in a new F# project (possibly even used in the F# project template), not require referencing an external library.

# Compatibility

* Is this a breaking change?
  * No, it is an addition of two new functions to the `FSharp.Core` library.
* What happens when previous versions of the F# compiler encounter this design addition as source code?
  * It works as designed, assuming that the `FSharp.Core` version is current.
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
  * It works as designed, assuming that the `FSharp.Core` version is current.
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?
  * It works as designed, assuming that the `FSharp.Core` version is current.

# Unresolved questions

* **Are `print` and `println` the best names for these functions?**
  * The `print` and `println` names were used in writing this RFC as they seem to be commonly used in other languages, analogous to the `printf` and `printfn` names, and allow for the mnemonic "print line" for `println`. However, the naming is still up for discussion and subject to change. Some of the options discussed in the suggestion thread were: `print`/`printn`, `print`/`printline`, `write`/`writeline`, and `put`/`putln`. One advantage to the `print`/`printn` option is closer consistency with `printf`/`printfn`.
* **Should we provide a `print` function without the trailing newline at all?**
  * Printing output without a newline is a fairly uncommon need and could make the `print` function surprising for some users who expected it to behave like `println`.
* **Should we allow clr globalization mechanisms influence print formats?**
  * Formatting the value according to the current culture is the default for `Console.Write`, `Console.WriteLine`, and string interpolation without an F# format specifier (`printfn $"{x}"`). F# format specifiers such as `%f`, and the `string` function always format in an invariant fashion.
