# F# RFC FS-1089 - Prevent FS0670 with 'string' and optimize generated IL

The design suggestion [Allow use of 'string' everywhere, and optimize generated IL](https://github.com/fsharp/fslang-suggestions/issues/890) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] Approved in principle ([PR was accepted](https://github.com/dotnet/fsharp/pull/9549))
- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/890)
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/9549) _Completed_
- Design Review Meeting(s) with @dsyme and others invitees (n/a)
- [Discussion](https://github.com/fsharp/fslang-suggestions/issues/890)

# Summary

* Prevent `FS0670` to be thrown when using the `string` function in overrides.
* Optimize IL for known struct types, remove boxing.

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

Motivation for optimizing the IL generation is simple: currently, the statically inlined function `string` always emits the same boxing code, leading to slow execution and less chance for JIT optimizations. See reports:

* [Original language suggestion](https://github.com/fsharp/fslang-suggestions/issues/890) 
* [Issue #9153](https://github.com/dotnet/fsharp/issues/9153).
* [Issue #7958](https://github.com/dotnet/fsharp/issues/7958)

# Detailed design

Three things need to happen:

* Rewrite the function `string` such that the error is not raised anymore. This can be done by removing the statically resolved `^T` for the dynamically resolved `'T`. The function remains to be `inline`.
* Restructure the code such that the dead code becomes live code
* Add known optimizable types like `string`, `DateTime` etc.

Current situation with dead code:

```f#
let inline anyToString nullStr x = 
    match box x with 
    | null -> nullStr
    | :? System.IFormattable as f -> f.ToString(null,System.Globalization.CultureInfo.InvariantCulture)
    | obj ->  obj.ToString()

let inline string (value: ^T) = 
    anyToString "" value
    when ^T struct = anyToString "" value  // always true for any struct type
    // any following line is never hit, because `struct` hits first
    when ^T : float = (# "" value : float #).ToString("g",CultureInfo.InvariantCulture)  // dead code
    when ^T : float32    =  ...  // dead code
    ... etc  // all dead code
```

New code will be structured like this:

```f#
[<CompiledName("ToString")>]
let inline string (value: 'T) = 
    anyToString "" value
    when 'T: float      = (# "" value : float      #).ToString("g",CultureInfo.InvariantCulture)
    when 'T: float32    =  ...
    .... more cases here
    when 'T: int = ...    // optimized call
    when 'T: byte = ...   // optimized call
    ... more cases
    when 'T: struct = ... //last case
```


This will allow most system types, when used with `string`, to emit non-boxing optimized code.

# Alternatives

There are no alternatives other than to "roll your own string function".

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change?

  No

* What happens when previous versions of the F# compiler encounter this design addition as source code?

  n/a, this doesn't change syntax.

* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?

  n/a, this doesn't change syntax.


* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?

  They continue to work.


Any existing compiled binaries will continue to work without recompilation, even when linked to newer versions of `FSharp.Core.dll`.

# Unresolved questions

None
