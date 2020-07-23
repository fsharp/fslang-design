# F# RFC FS-1089 - Prevent FS0670 with 'string' and optimize generated IL

The design suggestion [Optimize 'string', prevent FS0670, and generate less bloated IL](https://github.com/fsharp/fslang-suggestions/issues/890) has been marked "approved in principle" (but no tag has been added yet).

This RFC covers the detailed proposal for this suggestion.

- [x] Approved in principle (in comment, tag was not added)
- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/890)
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/9549) _In progress_
- Design Review Meeting(s) with @dsyme and others invitees (n/a)
- [Discussion](https://github.com/fsharp/fslang-suggestions/issues/890)

# Summary

* Prevent `FS0670` to be thrown when using the `string` function.
* Optimize for known struct types, remove boxing.
* Remove redundant casts and null checks.

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

# Detailed design

The current implementation has dead code:

```f#
let inline anyToString nullStr x = 
    match box x with 
    | null -> nullStr
    | :? System.IFormattable as f -> f.ToString(null,System.Globalization.CultureInfo.InvariantCulture)
    | obj ->  obj.ToString()

let inline string (value: ^T) = 
    anyToString "" value
    when ^T struct = anyToString "" value  // always true for any struct type
    when ^T : float = (# "" value : float #).ToString("g",CultureInfo.InvariantCulture)  // dead code
    when ^T : float32    =  ...  // dead code
    ... etc  // all dead code
```

The following changes will be implemented:

* `^T` changes to `'T`.
* Restructuring to allow optimizations for known types and remove the current dead code.
* Add known `System` struct types to prevent boxing: `Guid`, `DateTime`, `DateTimeOffset`.

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

Users can implement their own `string` version if they want better optimized code.

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
