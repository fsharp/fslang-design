# F# RFC FS-1125 - Add `print` and `printn` functions to FSharp.Core

The design suggestion [print and printn alongside printf and printfn](https://github.com/fsharp/fslang-suggestions/issues/1092) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1092)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/13597)
- [ ] Design Review Meeting(s) with @dsyme and others invitees
- [x] [Discussion](https://github.com/fsharp/fslang-design/discussions/675)

# Summary

This RFC introduces two new functions:
- `print : string -> unit`
- `printn : string -> unit`

`print` writes the exact input to stdout (analogous to `System.Console.Write`) and `printn` appends a newline to the string before writing to stdout (analogous to `System.Console.WriteLine`).

> NOTE: we've received significant feedback that we should strongly consider generic `print`/`printn` functions. See "unresolved issues" below.

# Motivation

These functions will simplify the user experience when working with non-formatted strings. Currently, beginners to the language are introduced to `printf` and `printfn` as the default method for writing strings to the console. However, truly understanding these functions require understanding the `TextWriterFormat` type, which is generally out-of-scope for people initially learning the language. For example, the error generated from the following snippet would confuse many beginners.

```fs
let str = "A string"
printfn str // Error FS0001: The type 'string' is not compatible with the type 'Printf.TextWriterFormat<'a>'
```

Particularly after the introduction of interpolated strings, many users do not need the extra power of format strings. In these cases, `print` and `println` would provide a more user-friendly default for printing strings.

# Detailed design

This RFC would add the two functions described above to the `FSharp.Core` library to supplement the existing `printf` and `printfn` functions. The `print` and `println` functions each take a string and write that string to stdout. `println` appends a newline to the string while `print` does not. The fact that these functions take a `string` argument means that string interpolation can still be used to provide formatting and variable capture.

Example code:

```fsharp
let str = "A string"
print str // prints "A string" with no trailing newline
printn str // prints "A string" with a trailing newline
printn $"The value in str is %s{str}" // prints "The value in str is A string" with a trailing newline
printn $"%0.3f{System.Math.PI}" // prints "3.142" with a trailing newline
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

**Should we provide a `print` function without the trailing newline at all?**

Printing output without a newline is a fairly uncommon need and could make the `print` function surprising for some users who expected it to behave like `println`.

**Should the `print` functions be generic, and if so what is the specification?**

The initial suggestion approved as that `string` be non-generic.  In [the discussion in the initial PR](https://github.com/dotnet/fsharp/pull/13597), a suggestion was made to make the print functions generic.  This is based on a separate motivation to use the functions to rapidly write code to output any data - rather than the educational scenarios. This is seen as important enough that the suggestion is we shouldn't proceed with adding `print` taking just a string if that would preclude adding the generic one later.
 
Adding a generic `print` raises many significant design questions and exposes some existing design flaws in F#. For example, a generic print can be implemented today using any of these:
 
```fsharp
let print x = printfn "%s" (string x)
let print x = printfn "%A" x
let print x = printfn "%0A" x
let print x = printfn "%80A" x
let print x = printfn "%+A" x
let print x = printfn "%-A" x
let print x = printfn "%O" x
let print x = printfn "${x}"
let print x = printfn "%s" (x.ToString())
```
    
These all differ.  The basic dimensions are:

* safety
* locale/culture
* multi-line formatting and line width
* what structure is revealed in structured multi-line formatting
* what happens with large objects
* what happens with infinite objects
* what happens with nulls and None
 
If we add a generic `print`, it should have a clear specification with regard to culture formatting. Unfortunately, anything relying on unadjusted `%A` formatting is inconsistent with regard to culture formatting - sometimes using invariant culture, and sometimes current culture.  These issues and possible paths forward on it are discussed in https://github.com/fsharp/fslang-suggestions/issues/897

If we add a generic `print`, we should restrict it so it can't be used with function types, e.g. some future "warn if instantiated to be a function type" thing:

```fsharp
let print<[<NotAFunction>] 'T> (x: 'T) = printf $"%$A{x}"
let printn<[<NotAFunction>] 'T> (x: 'T) = printfn $"%$A{x}"
```

This alleviates the primary safety concern:

```fsharp
let f x y = x + y
print (f 1)
```
    
The same logic should be applied to warn on under-applied interpolations:
    
```fsharp
let f x y = x + y
printfn $"result = {f 1}"
```

In summary, @dsyme writes:  "if we add `print: string -> unit` then it means we strongly encourage people to use `print $"{x}"` and `print $"%A{x}"` and `print $"%d{x}"`. These are generally good but have some inconsistent and quirky culture formatting. So let's press pause on this, iron out these problems and think over how to bring more uniformity before shooting off our one and only shot for a `print` learning path that is going to be quirk-free, and using up the possibility of a generic `print`. Sure some of the problems are really problems with interpolated strings and stem from the mix of `printf` (culture-invariant) and .NET formatting (culture-aware), but let's go over that space again first to get things tidied up.    
