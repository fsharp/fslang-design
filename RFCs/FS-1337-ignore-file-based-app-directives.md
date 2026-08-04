# F# RFC FS-1337 - Ignore file-based-app directives

The design suggestion [Ignore file-based-app directives](https://github.com/fsharp/fslang-suggestions/issues/1440) has been [marked](https://github.com/fsharp/fslang-suggestions/issues/1440#issuecomment-3932712300) "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1440)
- [x] Approved in principle
- [ ] Implementation
- [ ] Discussion

# Summary

This RFC specifies that source lines starting with `#:` will be ignored by the compiler.

# Motivation

As of SDK 10.0, the `dotnet run` command supports "file based apps". Currently, only C# is supported. A prerequisite for F# support is that the F# compiler ignores the necessary directives for such apps. If file-based apps are ever introduced for F#, they should use the same syntax as C# does, namely source code lines that start with `#:`. Therefore, this RFC specifies that source lines starting with `#:` will (for now) be ignored by the compiler. This will enable experimenting with F# file-based apps.

This RFC does not specify syntax or semantics of potential future directives starting with that prefix.


# Detailed design

Source lines starting with `#ident` for any identifier `ident` beyond the known F# hash directive identifiers (like `if`, `line`, `nowarn`, `r` etc) are ignored already by the compiler.
This RFC specifies that also directives starting with `#:` are ignored.


```fsharp
#: whatever follows in this line is ignored
```

# Changes to the F# spec

An additional entry should be added to section [12.4](https://fsharp.github.io/fslang-spec/program-structure-and-execution/#124-compiler-directives) of the F# spec.

# Drawbacks

None

# Alternatives

Hash-ident directives like `#r_project` could be used. They are already ignored today. However, this would mean an unnecessary deviation from the way file-based-apps work in C#.

# Prior art

C# file-based apps.

# Compatibility

None.

# Interop

None

# Pragmatics

## Diagnostics

This RFC is only about ignoring the directives. Diagnostics would possibly be provided by actual use of them.

## Tooling

None so far.

## Performance

None.

## Scaling

No issue.

## Culture-aware formatting/parsing

No issues.

# Unresolved questions

None.