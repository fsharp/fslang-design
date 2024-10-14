# F# RFC FS-1146-scoped-nowarn

The design suggestion [278](https://github.com/fsharp/fslang-suggestions/issues/278) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/278)
- [x] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/17507) (draft PR)
- [X] [Discussion](https://github.com/fsharp/fslang-design/discussions/786)

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

1. The compiler shall recognize a new *compiler directive* `#warnon` (to be added to §12.4 of the F# spec).

2. `#warnon` shall have warning numbers as arguments in the same way as `#nowarn` (including non-string arguments (RFC-1147)).

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

    These directives now can appear also inside modules anywhere on a separate single line. The directive can be preceded by whitespace and followed by whitespace and a single-line comment.

    The style guide shall recommend the same indentation as the following line (for an opening directive) or previous line (for a closing directive).

    "Multi-line" #nowarn directives (possible with the current compiler) shall no longer be accepted.

    Warning 236 shall no longer be issued. 

5. All of the above rules shall be valid for `.fs` source files, `.fsi` signature files, `.fsx` script files and shall also be reflected in the compiler service.

6. For the interactive `fsi` REPL, the warn directives shall be processed across all interactions.

7. The warn directives shall be processed independently of any `#line` directives (§3.9 of the spec).

   The warn directives shall work only against the actual source file being compiled, the #line directive shall only impact the file and line number of error messages. For nowarn they are irrelevant.


> *Note:* Currently, the spec (§12.4) specifies for `#nowarn`:
    <br/>"For signature (.fsi) files and implementation (.fs) files, turns off warnings within this lexical scope.
    For script (.fsx or .fsscript) files, turns off warnings globally."
    <br/>The current compiler, however, ignores the lexical scope and disables the warnings until end of file. For compatibility reasons, we keep it that way. <br/>For script files, we propose to use the new rules, which technically is a breaking change, see also the [Alternatives](#alternatives) section.

> *Note:* Currently, the compiler service considers a `#nowarn` somewhere in a file as valid everywhere in this file. We consider this a bug that will be fixed.

> *Note:* The interaction of `#nowarn` and `#line` directives is not specified in the F# spec.
   In the current compiler, it is broken (see [details below](#issues-in-the-current-implementation)).

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
When the feature flag is off (for earlier language versions), the F# 9.0 behavior will be implemented.
An exception to this compatibility is the previous inconsistent behavior in connection with #line directives (see [below](related-issues-in-the-current-implementation)), which will be replaced by considering, for files with #line directives, any #nowarn for a warning number to be valid for the whole file.

Since previous versions of the compiler ignore unknown compiler directives, they continue to work as before.

Since warnings are a compile time feature, there is no binary compatibility issue.


# Pragmatics

## Diagnostics

Today there are no warnings for empty or repeated `#nowarn` directives (or only in very specific situations). Most invalid arguments are ignored.

With the new feature (i.e. under feature flag), there shall be warnings for invalid arguments to the warn directives. There shall also be warnings for repeated directives for the same number (except when there is the counterpart directive inbetween).

Use of the new `#warnon` directive under earlier language versions shall throw error 3350 (language feature error).

Warning 236 ("Directives inside modules are ignored") shall be removed.

## Tooling

No specific tooling change is expected.

## Performance and Scaling

No performance and scaling impact is expected.

## Culture-aware formatting/parsing

There is no interaction with culture-aware formatting and parsing of numbers, dates and currencies.

## Related issues in the current implementation

The interaction between `#line` and `#nowarn` is broken in the current compiler.

This following file (named `lineNoWarn.fs`)

```
namespace X
#line 10 "xyz.fs"
#nowarn "25"
#line 5 "lineNoWarn.fs"
module B = match None with None -> ()
```

shows a warning FS0025 for line 5. If you replace the `10` in the first `#line` directive by `1`, no warning is shown.

Reason is the [`checkFile` flag](https://github.com/dotnet/fsharp/blob/d37a8ae8aea915e1819b34c7fcd49749d11a3723/src/Compiler/Driver/CompilerDiagnostics.fs#L2241), introduced more than 10 years ago. Originally probably a hack in a debugging situation, it survived reviews since, and ended up being used in further modules. When this flag is set to `false` (which it is in most situations), `#nowarn` processing relies on comparing line numbers of one file with line numbers of another file. Accidentally, it sometimes works.

We are mentioning this here since it explains why we have no real compatibility reference for the `#line` / `#nowarn` interaction.

## Documentation

In the ["Line Directives" section](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/compiler-directives#line-directives) of the language reference, an additional paragraph on the interaction between line and nowarn/warnon directives should be inserted.

In the ["Preprocessor Directives"](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/compiler-directives#preprocessor-directives) section, the `#nowarn` entry should be extended to include `#warnon` and the functionality defined in this RFC. For the interaction with the `#line` directive, the "Line Directives" section should be referenced. Finally, the text should be updated to reflect RFC FS-1147. 

# Unresolved questions

... might come up in the discussion.
