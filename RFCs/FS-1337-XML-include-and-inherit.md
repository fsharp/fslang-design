# F# RFC FS-1337 - XML Documentation `<include>` and `<inheritdoc>` Support

The design suggestion [XML comments - support "include" and "inherit"](https://github.com/fsharp/fslang-suggestions/issues/1460) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1460)
- [x] Approved in principle
- [ ] Implementation:
  - [`<include>` support](https://github.com/dotnet/fsharp/pull/19186)
  - [`<inheritdoc>` support](https://github.com/dotnet/fsharp/pull/19188)
- [ ] Discussion

# Summary

Add support for the [`<include>`](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/recommended-tags#include) and [`<inheritdoc>`](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/recommended-tags#inheritdoc) XML documentation tags in F#, aligning with C#'s capabilities.

# Motivation

F# currently [explicitly does not support](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/xml-documentation#limitations) these tags. Adding support:
- Enables documentation reuse and inheritance
- Reduces duplication across overrides and repeated API patterns
- Achieves parity with C# documentation features

# Detailed design

Both features follow the C# semantics documented at [Recommended XML tags for C# documentation comments](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/recommended-tags).

## `<include>` Tag

References external XML files for documentation content. Expanded at XML doc generation time (when using `--doc:<file>`).

```fsharp
/// <include file="docs/MyDocs.xml" path="/docs/member[@name='MyFunction']/*"/>
let myFunction x = x
```

**Key scenarios:**
1. Basic expansion from external file with XPath selection
2. Circular include detection (prevents infinite loops)
3. Multiple includes in the same doc comment

## `<inheritdoc>` Tag

Inherits documentation from base classes, interfaces, or explicitly referenced symbols.

```fsharp
type MyClass() =
    inherit BaseClass()
    
    /// <inheritdoc/>
    override this.MyMethod() = ()
    
    /// <inheritdoc cref="T:System.IDisposable"/>
    member this.Dispose() = ()
```

**Key scenarios:**
1. Basic inheritance from base type members via `cref`
2. Cycle detection in inheritance chains
3. Partial inheritance with `path` attribute for specific sections

# Drawbacks

Minor compiler complexity. Risk of hiding places where docs should be uniquely written.

# Prior art

- C#: [`<include>`](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/recommended-tags#include), [`<inheritdoc>`](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/recommended-tags#inheritdoc)
- These are standard .NET XML documentation features already supported by documentation tools.

# Compatibility

* **Not a breaking change**
* Previous compilers: Tags passed through unexpanded (existing behavior)
* No FSharp.Core changes
* No binary compatibility concerns (compile-time feature)

# Pragmatics

## Diagnostics
- Warning when include file not found or path doesn't match
- Warning for circular includes/inheritance

## Tooling
IDE integration for design-time doc inheritance. No debugger/runtime impact.

## Performance
Minimal. XML file caching for `<include>`. No impact on generated IL.
