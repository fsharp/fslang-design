# F# RFC FS-1146-scoped-nowarn

The design suggestion [278](https://github.com/fsharp/fslang-suggestions/issues/278) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/278)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/18049)
- [x] [Discussion](https://github.com/fsharp/fslang-design/discussions/786)

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

1. The compiler shall recognize a new "warnon" *compiler directive* (to be added to §12.4 of the F# spec).

    So, F# has now two *warn directives*: "nowarn" and "warnon".

2. A warn directive is a single line of source code that consists of
   - Optional leading whitespace
   - The string `#nowarn` or `#warnon`
   - Whitespace
   - One or more arguments, separated by whitespace, in one of the formats specified by RFC-1147 (integer warning number, `FS` followed by an integer warning number, both optionally surrounded by double quotes)
   - Optional whitespace
   - Optional line comment
   - Newline

    > *Note:* See the [Compatibility](#compatibility) section below for some consequences.

  
3. In the following, we use the term NOWARN for a `#nowarn` directive for a certain warning number. Similarly, we use WARNON for a `#warnon` with the same warning number.

   - NOWARN and WARNON shall disable/enable the warning until eof or a corresponding WARNON / NOWARN

   - Compiler defaults (system defaults and compiler flags) shall be valid outside of scopes defined by
     - NOWARN - WARNON pairs
     - WARNON - NOWARN pairs
     - NOWARN and eof
     - WARNON and eof

     where these pairs are identified by scanning the file linearly.

   - [Alternatives](#alternatives) have been considered but dismissed.

4. The current definition "*Compiler directives are declarations in non-nested modules or namespace declaration groups*" (§12.4) shall be relaxed for `#nowarn` and `#warnon`. 

    These directives now can appear also inside modules anywhere on a separate single line.

    The style guide shall recommend the same indentation as the following line (for an opening directive) or previous line (for a closing directive).

    "Multi-line" #nowarn directives (possible with the current compiler) shall no longer be accepted.

    Warning 236 shall no longer be issued. 

5. All of the above rules shall be valid for `.fs` source files, `.fsi` signature files, `.fsx` script files and shall also be reflected in the compiler service.

   > *Note:* This leads to some breaking changes as described in the [Compatibility](#compatibility) section below.

6. For the interactive `fsi` REPL, the warn directives shall be processed across all interactions.

7. The warn directives shall be processed independently of any `#line` directives (§3.9 of the spec).

   The warn directives shall work only against the actual source file being compiled, the #line directive shall only impact the file and line number of error messages. For nowarn they are irrelevant.

   > *Note:* The interaction of `#nowarn` and `#line` directives is not specified in the F# spec.
    Due to the way the #line directives are implemented in the compiler, the new specification can be reliably implemented only if the same #line target file is not targeted from multiple source files. This should, however, not be a significant limitation.

### Examples:

```
module A
match None with None -> ()     // warning
let x =
    #nowarn 25
    match None with None -> 1  // no warning
    #warnon 25
match None with None -> ()     // warning
#nowarn 25
match None with None -> ()     // no warning
```

# Drawbacks

Again more logic to maintain in the compiler.

# Alternatives

## Functionality

The following alternatives have been considered in terms of functionality (item 3 of the [Detailed Specification](#detailed-specification) section).

Alternative 1
- NOWARN and WARNON disable/enable the warning until a corresponding WARNON / NOWARN
- Compiler defaults (system defaults and compiler flags) shall be valid outside of scopes defined by
     - NOWARN - WARNON pairs
     - WARNON - NOWARN pairs

Alternative 2
- NOWARN and WARNON disable/enable the warning
- Defaults are valid from the beginning of the file to the first NOWARN or WARNON

Alternative 3
- NOWARN disables the warning (independent of the defaults), until eof or a WARNON
- WARNON is allowed only after a NOWARN and restores the defaults for the warning

Alternative 1 would have been nice and simple, but is not backwards compatible.

Alternative 2 has no way of going back to the default settings.

Alternative 3 is not symmetrical and therefore more difficult to learn.


## Script files

Item 5 of the [Detailed Specification](#detailed-specification) section extends the new functionality to script files, thereby introducing a breaking change. Alternatively, we could disallow `#warnon` for script files. Or define rules for `#warnon` before the `#nowarn` that are different from the rules for `.fs` source files and necessarily complicated.


# Compatibility

Warn scopes will be introduced under a feature flag.
When the feature flag is off (for earlier language versions), `#nowarn` will work as before (with the exceptions mentioned below) and `#warnon` will produce an error (see the [Diagnostics](#diagnostics) section below).

## Fixes that can affect old code when compiled with the new compiler, feature flag off

a) There were a following inconsistencies in the previous behavior that cannot easily be replicated with the new code or that are just being fixed now.

- Multiline and empty warn directives are no longer allowed
- Whitespace between the hash character and the `nowarn` is no longer allowed.
- Simple string warning numbers continue to be valid, but no triple quoted, interpolated or verbatim strings.
- Some invalid arguments used to be ignored. They now produce warnings.
- Two nowarn directives for the same warning number, without a warnon directive inbetween, produce a warning.

```
#nowarn 25                          // valid (was valid before)
#nowarn "25"                        // valid (was valid before)
#nowarn "FS0025"                    // valid (was valid before)
#nowarn FS25                        // valid (was valid before)
#nowarn                             // error (was allowed before)
   25                       
#nowarn                             // error (was allowed before)
# nowarn 25                         // error (was allowed before)
#nowarn """25"""                    // error (was allowed before)
#nowarn $"25"                       // error (was error before)
#nowarn @"25"                       // error (was allowed before)
#nowarn "FS0xy"                     // error (was error before)
#nowarn "FSxy"                      // error (was ignored before)
#nowarn 0xy                         // error (was error before)
#nowarn "0xy"                       // error (was warning before)
#nowarn xy                          // error (was ignored before)
#nowarn 20                          // valid (was valid before)
#warnon 20                          // error (for old langversion only) (was ignored before) (see d) below)
#nowarn 20                          // valid (was valid before)
#nowarn 20                          // warning (in the context of the previous line) (was valid before)
```

b) Previously, when using the compiler proper (fsc), a `#nowarn` correctly disables warnings from that directive until end of file. The compiler service (as used by the IDEs), however, considers a `#nowarn` anywhere in a file as valid everywhere in this file. We consider the latter a bug that will be fixed so that the IDEs display the same warnings as the compiler.


```
""                  // warning 20 when compiled, but no squiggles in the IDE
#nowarn 20
```

c) The interaction with #line directives might in rare cases now correctly suppress a warning that was visible before.

d)  Previously, any accidental `#warnon` directive in the code was an unknown hash directive and as such ignored. With the new compiler and "-langversion=9.0", it will produce a "new feature" error (see the [Diagnostics](#diagnostics) section below).


## Additional changes that can affect old code when compiled as F# 10

e) Previously, a `#nowarn` anywhere in a script file affected *the whole compilation*. As from F# 10, this will no longer be the case. Warn directives in scripts will now work as specified in this RFC, in the same way as in ".fs" files. This is a breaking change, see also the [Alternatives](#alternatives) section above.

f) Previously, any accidental `#warnon` directive in the code was an unknown hash directive and as such ignored. Now it will have an effect and might produce errors.


## Binary compatibility

Since warnings are a compile time feature, there is no binary compatibility issue.


# Pragmatics

## Diagnostics

Today there are no warnings for empty or repeated `#nowarn` directives (or only in very specific situations). Most invalid arguments are ignored.

With the new feature, there shall be errors for all syntactically invalid directives. See also the [Compatibility](#compatibility) section above.

Warn directives shall under earlier language versions continue to produce warning 236 if used inside a sub-module.

Use of the new `#warnon` directive under earlier language versions shall produce error 3350 (language feature error).

## Tooling

Tooling (fsac, vs, fantomas) will need to adapt to correctly color the warn directives.

More tools might be affected by the changes in the compiler service surface area.
 

## Performance and Scaling

No performance and scaling impact is expected.

## Culture-aware formatting/parsing

There is no interaction with culture-aware formatting and parsing of numbers, dates and currencies.


## Documentation

In the ["Line Directives" section](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/compiler-directives#line-directives) of the language reference, an additional paragraph on the interaction between line and nowarn/warnon directives should be inserted.

In the ["Preprocessor Directives"](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/compiler-directives#preprocessor-directives) section, the `#nowarn` entry should be extended to include `#warnon` and the functionality defined in this RFC. For the interaction with the `#line` directive, the "Line Directives" section should be referenced. Finally, the text should be updated to reflect RFC FS-1147. 

In the [F# code formatting guidelines](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/formatting), the recommendation of the above Detailed Specification, item 4, should be added.
