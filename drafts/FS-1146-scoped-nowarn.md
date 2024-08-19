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

     with no other NOWARN or WARNON inbetween.

   - [Alternatives](#alternatives) have been considered but dismissed.

4. The current definition "*Compiler directives are declarations in non-nested modules or namespace declaration groups*" (§12.4) shall be relaxed for `#nowarn` and `#warnon`. They now can appear also inside modules anywhere on a separate line. 

    Warning 236 will no longer be issued. 

    We considered and dismissed a more restrictive [alternative](#alternatives).

5. Indentation rules for directives are currently not defined in the spec, except for (obsolete) no-indentation of conditional compilation directives. For compatibility reasons, the following shall hold.

   - `#nowarn` and `#warnon` directives have to start on the offside line of the enclosing offside context.

   [Alternatives](#indentation) have been considered and dismissed.

6. All of the above rules are valid for `.fs` source files, `.fsi` signature files and `.fsx` script files.

7. For the interactive `fsi` REPL, the functionality as described in (3) above is valid across all interactions, while the indentation rules (5) are valid within a single input. (4) is not applicable.

8. The compiler service shall implement the changes (3) to (5) above.

9. Interaction with `#line` directives.

   - The sections that in terms of the `#line` directives belong to a certain file shall be considered by `#nowarn` / `#warnon` as a separate file. This means that, in a generated file, a `#nowarn` in a line pointing to the generating file is effective only in other such lines, and not in lines that are not affected by any `#line` directive.

   - See also the [alternative](#interaction-with-line-directives) that was considered and dismissed.

> *Note:* Currently, the spec (§12.4) specifies for `#nowarn`:
    <br/>"For signature (.fsi) files and implementation (.fs) files, turns off warnings within this lexical scope.
    For script (.fsx or .fsscript) files, turns off warnings globally."
    <br/>The current compiler, however, ignores the lexical scope and disables the warnings until end of file. For compatibility reasons, we keep it that way. <br/>For script files, we propose to use the new rules, which technically is a breaking change, see also the [Alternatives](#alternatives) section.

> *Note:* Currently, the compiler services considers a `#nowarn` somewhere in a file as valid everywhere in this file. We consider this a bug that will be fixed.

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

## Allowed places

When opening the directives to places inside modules (see item 4 of the [Detailed Specification](#detailed-specification) section), we considered requesting the following for the pair of directives that define a warning scope according to item 4 of that section.

- Both directives have to be in the same lexical scope (e.g. inside the same let binding). 

However, this would have led to complicated rules. And to a much more invasive implementation.

## Indentation

An alternative would be to have `#nowarn` and `#warnon` start new offside contexts with new offside lines, so that code related to them would be further indented. However, this would be incompatible with the current use of `#nowarn`.

## Script files

Item 6 of the [Detailed Specification](#detailed-specification) section extends the new functionality to script files, thereby introducing a breaking change. Alternatively, we could disallow `#warnon` for script files. Or define rules for `#warnon` before the `#nowarn` that are different from the rules for `.fs` source files and necessarily complicated.

## Interaction with Line Directives

A (perhaps more user-friendly) alternative to item 9 of the [Detailed Specification](#detailed-specification) section would be to have `#nowarn` / `#warnon` operate independently of the `#line` directives. For the implementation, however, this would mean book-keeping of the original filename and line numbers in the `range` struct. Which is, both in terms of implementation effort and runtime cost, probably prohibitive.

# Compatibility

Technically, the introduction of the `#warnon` compiler directive is a breaking change, because the compiler swallows unknown compiler directives without error or warning. So, if somebody has used `#warnon` "illegally" after a `#nowarn` AND relies on NOT getting a warning, they will be suprised to get a warning with the new compiler. Probably a scenario that can be neglected.

There is also a breaking change for script files in that `#nowarn` directives are no longer effective *before* their occurence. This is probably a "feature" that has been rarely used (if at all) and its removal wouldn't surprise anybody.

Fixing the [checkfile bug](#related-issues-in-the-current-implementation) leads to a potentially breaking change. It does actually for the compiler's fsyacc-generated files. The fix should therefore be put under a feature flag.

Since previous versions of the compiler ignore unknown compiler directives, they continue to work as before.

Since warnings are a compile time feature, there is no binary compatibility issue.


# Pragmatics

## Diagnostics

Since `#nowarn` and `#warn` are defined in a symmetric way, warnings and errors should also be symmetric. Since there are no warnings today for empty or repeated `#nowarn` directives, we should also refrain from introducing such warnings for `#warnon`.

There will be the usual errors and warnings regarding correct indentation.

Warning 236 ("Directives inside modules are ignored") will be removed.

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

# Unresolved questions

... might come up in the discussion.
