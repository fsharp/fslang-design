# F# RFC FS-1147-non-string-directive-args

The design suggestion [1368](https://github.com/fsharp/fslang-suggestions/issues/1368) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1368)
- [x] Approved in principle
- [X] [Implementation](https://github.com/dotnet/fsharp/pull/17206)
- [ ] Discussion (-)

# Summary

For the `#nowarn` and `#time` compiler directives, allow certain non-string arguments.

# Motivation

For more concise and simpler directives, the quotation marks can be dropped and warning codes like `"FS0025"` can be used instead of just `"25"`.

Examples (taken from the language suggestion):

Before:
```
#nowarn "0070"
#nowarn "FS1140"  // error
#nowarn "0025" "1140" "1234"
#time "on"
```

After:

```
#nowarn 70
#nowarn 25 1140 1234
#nowarn "FS1140" // string is still allowed
#nowarn FS1140
#time on
```



# Detailed specification

The F# syntax, which currently says

*compiler-directive-decl : # ident string ... string*

will be extended to

*compiler-directive-decl : # ident compiler-directive-arg ... compiler-directive-arg*

*compiler-directive-arg : string | ident | long-ident | int*

The definition of the `#nowarn` compiler directive (§12.4) will be extended to accept

- *string* arguments that either contain a warning number or a warning number prefixed by `FS`, such as `"25"` or `"FS0025"` or `"FS25"`.
- *ident* arguments that start with `FS`, followed by a warning number, such as `FS0025` or `FS25`.
- *int* arguments of version number, such as `25`.

The definition of the `#time` compiler directive (§12.4) will be extended to accept, next to *string* arguments `"on"` and `"off"`, also *ident* arguments `on` and `off`.


# Drawbacks

Different ways to express the same directive.


# Compatibility

This is not a breaking change.

Previous versions of the F# compiler will not accept the new syntax.

Compiler directives are not visible in binaries, so there is no binary compatibility issue.

## Diagnostics

Invalid arguments will produce errors.

The newly allowed arguments will produce a specific error if used with the new compiler, configured to use a previous language version.

## Tooling

No need for tooling specific to this change is expected.

## Performance and Scaling

No performance or scaling impact is expected.

## Culture-aware formatting/parsing

n/a

# Unresolved questions

None