# F# RFC FS-1100 - Printf format as binary number

The design suggestion [Printf format as binary number](https://github.com/fsharp/fslang-suggestions/issues/1008) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] Approved in principle [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1008)
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/11603)
- [x] ~~Design Review Meeting(s) with @dsyme and others invitees~~ ([Overridden by @cartermp](https://github.com/fsharp/fslang-design/pull/570#issuecomment-848940573))
- [Discussion](https://github.com/fsharp/fslang-design/discussions/568)

# Summary

An additional format specifier `%B` will be added to F# which formats a basic integer type
(currently `byte`|`int16`|`int32`|`int64`|`sbyte`|`uint16`|`uint32`|`uint64`|`nativeint`|`unativeint`)
as an unsigned binary number.

# Motivation

There are 4 number bases that are supported in `Convert.ToString`: 2, 8, 10 and 16.
Of these, only base 2 does not have a corresponding printf format, as 8 has `%o`,
10 has `%d` and `%i`, and 16 has `%x` and `%X`.

Moreover, when introducing newcomers to programming, it is important to explain the
binary number system before octal and hexadecimal, and this printf format provides
a convenient way to view the representation of numbers in binary form before being
introduced to octal and hexadecimal forms. This also avoids `Convert.ToString` boilerplate
which gets in the way.

As a result, not only is a consistent experience offered across the standard number bases,
it also offers convenience when learning F#.

# Detailed design

This should be implemented by referencing the existing implementation of `%o`, along with
its limitations (no signed numbers) to be consistent and be within expectations.

The following F# code:

```fs
printf "%o" 123
printf "%B" 123
```

will print

```
173
1111011
```

# Drawbacks

This will be the first additional printf format specifier introduced to F# since 1.0, so
existing documentation will become outdated and may surprise users referencing old
documentation.

# Alternatives

- Redefine `%b` to mean binary and define `%B` for booleans to be consistent with `%o` which is lowercase.

This is a breaking change and will not be accepted. [OCaml went down this path](https://stackoverflow.com/a/39965066)
and ended up with both `%b` and `%B` taking booleans with the original `%b` deprecated.
This is not desirable from a maintenance perspective. 

- Using other characters instead of `%b`.

Other characters with desirable properties (lowercase and has a connection to binary numbers)
are either taken already (`%t` for two) or has a complicated design which is inconsistent with
`%o`, `%x` and `%X` (`%_2d` where 2 is the base and `_` signifies that `2` is the base).

- Not doing anything.

The existing inconsistency and inconvenience will be preserved.

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change? No
* What happens when previous versions of the F# compiler encounter this design addition as source code? Error as expected.
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
  For exposed printf formats, users should make sure they have the correct FSharp.Core referenced, otherwise exceptions will occur.
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?
  There is no risk of exposing unsupported language features to previous versions of the F# compiler since the API surface of FSharp.Core stays constant.


# Unresolved questions

Should we take this chance to support arbitrary bases as mentioned above as well?
