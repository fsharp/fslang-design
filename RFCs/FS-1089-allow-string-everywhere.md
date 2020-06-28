# F# RFC FS-1089 - Allow use of 'string' everywhere and optimize generated IL

The design suggestion [Allow use of 'string' everywhere, and optimize generated IL](https://github.com/fsharp/fslang-suggestions/issues/890) has been marked "approved in principle" (only in issue comment).

This RFC covers the detailed proposal for this suggestion.

- [ ] Approved in principle (only in issue comment, so: not yet)
- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/890)
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/9549) under development
- Design Review Meeting(s) with @dsyme and others invitees (n/a)
- [Discussion](https://github.com/fsharp/fslang-suggestions/issues/890)

# Summary

* Remove `inline` from the definition of `string` to allow its use in places that would otherwise be forbidden.
* Optimize the IL generation.
* Original semantics remain identical.

# Motivation

Consider the following code:

```f#
type Either<'L, 'R> =
    | Left of 'L 
    | Right of 'R
    override this.ToString() =
        match this with
        | Left x -> string x    // not possible
        | Right x -> string x   // not possible
```

After this change, the error FS0670 will not be raised anymore, allowing the use of the omnipresent `string` function in more places, and aiding in rapid proto-typing of DU's.

Motivation for optimizing the IL generation is simple: currently, the statically inlined function `string` creates a lot of non-optimized code, leading to slow execution and less chance for optimizations. Examples of this are in the [original language suggestion](https://github.com/fsharp/fslang-suggestions/issues/890) and several user-reported issues, like [#9153](https://github.com/dotnet/fsharp/issues/9153).

A further motivation is re-instating the original intended mode of operation for `string`. There's a lot of statically-resolvable code that is never hit, which appears to be there to special-case the native types `int`, `float` etc. After this change, these optimizable cases will be re-instated, while respecting `enum` special treatment.

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

* The keyword `inline` will be droppped.
* The special-case for `struct` will be placed lower, so that native types that cannot also be `Enum` will get special treatment.
* Instead of the `when ^T: type` syntax, we'll adopt `when 'T: type` syntax, which will still lead to inlined code when the compiler encounters the special cases.
* The calls to `anyToString` lead to redundant IL, this will be dropped in favor of a `struct`-specific call, since this can never be `null`.
* Using `"g"` will be dropped in favor of `null`, which has the same effect and requires a more optimized IL instruction.
* The fall-through case for any other object will remain: anything that implements `IFormattable` will continue to call `IFormattable::ToString(null, CultureInfo.InvariantCulture)`. This ensures compatibility.
* Possibly other optimizations or special-casing for common `System` types, to allow optimal IL generation.


New code will be structured something like this:

```f#
[<CompiledName("ToString")>]
let string (value: 'T) = 
    anyToString "" value
    when 'T: float      = (# "" value : float      #).ToString("g",CultureInfo.InvariantCulture)
    when 'T: float32    = (# "" value : float32    #).ToString("g",CultureInfo.InvariantCulture)
    .... more cases here
    when 'T: int = ...    //code to call struct-specific variant of anyToString
    when 'T: byte = ...   //same (these allow enums to print properly)
    ... more cases
    when 'T: struct = ... //code to call struct-specific variant that tests for IFormattable first
```


# Drawbacks

While currently there already is a bug that creates bloated code, one might be tempted to keep the `inline` behavior. This would have the advantage that the code would indeed always inline.

However, since the generated code, after this change, will be small, it is expected that the JIT will inline this anyway. Experimental tests with this have already shown that both F# and the JIT indeed do inline the code, even after we drop the `inline`.

Another drawback is that an `inline` function has slightly different semantics in F#:

### Before: 

```f#
// val myStr : x:obj -> string
let myStr x = string x
```

### After:

```f#
// val string : x:'a -> string
let myStr x = string x
```

Though the effect will be minimal, as once the function `myStr` is called in code, it will take on the actual type.

# Alternatives

Some alternatives can be considered:

1. Not doing this. Functionally it is currently correct and most users won't notice the sub-optimal code being generated.
2. Keep it `inline`, but apply the other changes. That would not solve the issue that `string` currently cannot be used in certain generic context as shown above.
3. Drop special-casing `Enum`. This would be a backward-compatibility issue, although it would allows for better optimized code.
4. Drop special-casing altogether. Essentially this is the status-quo, we would only remove the dead code.
5. Add ability to detect `Enum` statically to the compiler. While this would certainly help, it may not be for a while until this is possible with the compiler. By that time we can revisit this code.
6. Special-case `string` itself for not raising FS0670. I believe this should be done on a broader scale, where the compiler can detect that it is safe to ignore FS0670 cases.
7. Broaden the scope to allow `string` on refs as well. Several suggestions are underway to allow refs in more scenarios, it is better to wait until these materialize.

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change?

  No

* What happens when previous versions of the F# compiler encounter this design addition as source code?

  n/a, this doesn't change syntax.

* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?

  n/a, this doesn't change syntax.


* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?

  They continue to work, since previously, the `string` function was always inlined, so no reference to the function exists.


Any existing compiled binaries will continue to work without recompilation, even when linked to newer versions of `FSharp.Core.dll`.

If existing code is recompiled, the resulting IL will be different for the places where the `string` function is used.

# Unresolved questions

* What parts of the design are still TBD?

  There are no unresolved questions.
