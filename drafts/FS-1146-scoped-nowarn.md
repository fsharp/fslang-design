# F# RFC FS-1146-scoped-nowarn

The design suggestion [278](https://github.com/fsharp/fslang-suggestions/issues/278) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/278)
- [x] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/17507) (draft / PoC for now)
- [ ] Discussion (t.b.d.)

# Summary

Allow the #nowarn directive to become scoped (per warning number) by a corresponding #warnon directive.

# Motivation

Today, the #nowarn directive disables the specified warnings for all code from the directive to the end of the file. Usually, however, people want to see all warnings except for some very specific situations. Therefore, a way to disable warnings for some fragment of the code is a much-requested feature.

Quotes form the language suggestion thread:

- "being able to nowarn a few lines is most critical for my workplace"
- "it would be huge if we could get this"
- "it would be great to do only small sections that we know we want to ignore rather than the whole file"

Motivating examples, again from the suggestion thread:

```
// We know f is never called with a None.
let f (Some a) =    // creates warning 25, which we want to suppress
    // 2000 loc, where the incomplete match warning is beneficial
 ```

```
// We have a union with an obsolete case
type MyErrors =
| CoolError1 of CoolError1
| CoolError2 of CoolError2
| [<Obsolete>] OldError1 of OldError1

let handler e =
    match e with
    | CoolError1 ce -> ...
    | CoolError2 ce -> ...
    | OldError ce -> ...   // warning 44, which we want to suppress only here
```

# Detailed specification

1. The compiler shall recognize a new *compiler directive* (see §12.4 of the F# spec) `#warnon`.
2. `#warnon` has warning numbers as arguments in the same way as `#nowarn` (including non-string arguments (RFC-1147)).
3. Currently, `#nowarn` disables warnings from the directive to the end of the file. This will be changed as follows.
If a certain warning has been disabled by a `#nowarn` directive, then it can be re-enabled by a `#warnon` directive later in the file, carrying the same warning number as an argument. So, the "WarningOff" code checking for that warning number is scoped from the `#nowarn` to the corresponding `#warnon`.
4. If no corresponding `#warnon` is encountered for a `#nowarn` for a certain warning, the warning is disabled from the `#nowarn` to the end of the file.
5. Multiple "WarningOff" scopes can be defined in a file by `#nowarn` / `#warnon` pairs.
6. To use `#warnon` for a warning number without a previous `#nowarn` for that warning number is not allowed (error).
7. A `#nowarn` directive without arguments is not allowed (error).
8. `--nowarn` compiler flags are not considered for processing `#warnon` directives. This means that for a warning number `n` that is disabled by a compiler flag

    a) `#warnon` for `n` without previous `#nowarn` for `n` is an error.
    
    b) A `#warnon` for `n` after a `#nowarn` for `n` enables the warning for the rest of the file (or the next `#nowarn` for `n`).

9. The current definition "*Compiler directives are declarations in non-nested modules or namespace declaration groups*" (§12.4) shall be relaxed

    > Note: *This will be detailed after draft implementation*

10. Indentation rules (not defined in the spec, except for (obsolete) no-indentation of conditional compilation directives) shall be relaxed.

    > Note: *This will be detailed after draft implementation*
11. For scripts and interactive fsi, the same rules shall apply.
12. For the language service, the same rules shall apply.

    > Note: *This is currently not the case. Currently, any `#nowarn` for a warning number will disable the warning in the whole file.*

    > Note: *This will be detailed after draft implementation*

### Examples:

```
module A
match None with None -> ()     // warning
#nowarn 25
match None with None -> ()     // no warning
#warnon 25
match None with None -> ()     // warning
#nowarn 25
match None with None -> ()     // no warning
```

```
//TODO: example covering items 9 and 10 of the spec
```

# Drawbacks

Again more logic to maintain in the compiler.

# Alternatives

A number of different design have been discussed in the language suggestion, but the discussion converged on the current one.

# Compatibility

This is not a breaking change for the language.

Technically, it is a breaking change for the F# compiler, because the compiler swallows unknown compiler directives without error or warning (which btw one might consider a bug). So, if somebody has used `#warnon` "illegally" after a `nowarn` AND relies on NOT getting a warning, they will be suprised to get a warning with the new compiler. Probably a scenario that can be neglected.

Since previous versions of the compiler ignore unknown compiler directives, they continue to work as before.

Since warnings are a compile time feature, there is no binary compatibility issue.


# Pragmatics

## Diagnostics

Errors and Warnings on misuse of the feature are mentioned in the detailed specification above.

## Tooling

No specific tooling change is expected.
(Except for checking regarding item 12 of the spec.)

## Performance and Scaling

No performance and scaling impact is expected.

## Culture-aware formatting/parsing

There is no interaction with culture-aware formatting and parsing of numbers, dates and currencies.

# Unresolved questions

The items with notes in the detailed specification above are still to be detailed.
