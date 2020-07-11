# F# RFC FS-1089 - Prevent FS0670 with 'string' and optimize generated IL

The design suggestion [Optimize 'string', prevent FS0670, and generate less bloated IL](https://github.com/fsharp/fslang-suggestions/issues/890) has been marked "approved in principle" (only in issue comment).

This RFC covers the detailed proposal for this suggestion.

- [ ] Approved in principle (only in issue comment, so: not yet)
- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/890)
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/9549) _In progress_
- Design Review Meeting(s) with @dsyme and others invitees (n/a)
- [Discussion](https://github.com/fsharp/fslang-suggestions/issues/890)

# Summary

* Change `string` such that it can be used in any context, this prevents `FS0670` to be thrown.
* Special-case for known types, remove boxing.

# Motivation

Consider the following code:

```f#
type Either<'L, 'R> =
    | Left of 'L 
    | Right of 'R
    override this.ToString() =
        match this with
        | Left x -> string x    // not possible, FS0670
        | Right x -> string x   // not possible, FS0670
```

After this change, the error FS0670 will not be raised anymore, allowing the use of the omnipresent `string` function in more places, and aiding in rapid proto-typing of DU's.

Motivation for optimizing the IL generation is simple: currently, the statically inlined function `string` always emits the same boxing code, leading to slow execution and less chance for JIT optimizations. Examples of this are in the [original language suggestion](https://github.com/fsharp/fslang-suggestions/issues/890) and several user-reported issues, like [#9153](https://github.com/dotnet/fsharp/issues/9153).

Note that the current code includes optimizations, but this is dead code and are never hit, because everything fits in the first `when struct` case.

# Detailed design

The current implementation looks as follows, note the compiler-specific syntax that is never hit (the cases from `when ^T: float`), and the note on `Enum`:

```f#
let inline anyToString nullStr x = 
    match box x with 
    | null -> nullStr
    | :? System.IFormattable as f -> f.ToString(null,System.Globalization.CultureInfo.InvariantCulture)
    | obj ->  obj.ToString()

let inline string (value: ^T) = 
    anyToString "" value
    // since we have static optimization conditionals for ints below, we need to special-case Enums.
    // This way we'll print their symbolic value, as opposed to their integral one (Eg., "A", rather than "1")
    when ^T struct = anyToString "" value
    when ^T : float      = (# "" value : float      #).ToString("g",CultureInfo.InvariantCulture)
    when ^T : float32    =  ... 
    ... etc
```

The following changes will be implemented:

* `^T` changes to `'T`
* Restructuring to allow optimizations for known types and remove the current dead code

New code will be structured something like this:

```f#
[<CompiledName("ToString")>]
let inline string (value: 'T) = 
    anyToString "" value
    when 'T: float      = (# "" value : float      #).ToString("g",CultureInfo.InvariantCulture)
    when 'T: float32    = (# "" value : float32    #).ToString("g",CultureInfo.InvariantCulture)
    .... more cases here
    when 'T: int = ...    //code to call struct-specific variant of anyToString
    when 'T: byte = ...   //same (these allow enums to print properly)
    ... more cases
    when 'T: struct = ... //code to call struct-specific variant that tests for IFormattable first
```


This will allow most system types, when used with `string`, to emit non-boxing optimized code.

# Alternatives

The main alternative is: do not do this, and leave the status quo (that is, no special-casing for known types, boxing remains, and FS0670 error will be thrown).

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change?

  No

* What happens when previous versions of the F# compiler encounter this design addition as source code?

  n/a, this doesn't change syntax.

* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?

  n/a, this doesn't change syntax.


* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?

  They continue to work, since previously, the `string` function was always inlined, so no reference to the function in FSharp.Core.dll exists.


Any existing compiled binaries will continue to work without recompilation, even when linked to newer versions of `FSharp.Core.dll`.

If existing code is recompiled, the resulting IL will be different for the places where the `string` function is used.

# Unresolved questions

* What parts of the design are still TBD?

  None
