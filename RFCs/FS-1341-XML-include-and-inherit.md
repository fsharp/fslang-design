# F# RFC FS-1341 - XML Documentation `<include>` and `<inheritdoc>` Support

The design suggestion [XML comments - support "include" and "inherit"](https://github.com/fsharp/fslang-suggestions/issues/1460) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1460)
- [x] Approved in principle
- [x] Implementation:
  - [`<include>` support](https://github.com/dotnet/fsharp/pull/19186)
  - [`<inheritdoc>` support](https://github.com/dotnet/fsharp/pull/19188)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

# Summary

Add support for the [`<include>`](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/recommended-tags#include) and [`<inheritdoc>`](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/recommended-tags#inheritdoc) XML documentation tags in F#, aligning with C#'s capabilities.

# Motivation

F# currently [explicitly does not support](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/xml-documentation#limitations) these tags. Adding support:
- Enables documentation reuse and inheritance
- Reduces duplication across overrides and repeated API patterns

# Detailed design

Both tags follow the [C# semantics](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/recommended-tags). They differ in when they are resolved: the compiler bakes `<include>` content directly into the emitted XML documentation file, while `<inheritdoc>` is left in the emitted XML for the IDE and documentation tooling to resolve.

## `<include>` Tag

References documentation held in an external XML file. `file` names the file (resolved relative to the source file, matching C#, falling back to the compiler's working directory) and `path` is an XPath 1.0 expression selecting the fragment to insert.

```fsharp
/// <include file="docs/MyDocs.xml" path="/docs/member[@name='MyFunction']/*"/>
let myFunction x = x
```

Multiple `<include>` tags may appear in one comment, and an included fragment may itself contain `<include>` (expanded recursively; circular includes are detected). Expansion happens when XML docs are emitted (`--doc:<file>`). In-editor Quick Info does not expand includes in this initial support; doing so in-memory is a possible follow-up (see Unresolved questions).

## `<inheritdoc>` Tag

Inherits documentation from a base class, an implemented interface, or an explicitly referenced symbol. With no `cref`, F# tooling searches the base class chain first, then interfaces in declaration order (first match wins); a `cref` inherits from the named symbol instead, and `path` selects a subset with XPath. All elements of the resolved source are inherited, and locally written tags take precedence over inherited ones.

```fsharp
type MyClass() =
    inherit BaseClass()
    
    /// <inheritdoc/>
    override this.MyMethod() = ()
    
    /// <inheritdoc cref="T:System.IDisposable"/>
    member this.Dispose() = ()
```

# Changes to the F# spec

None to the grammar. XML documentation semantics follow C#. For members declared in a signature file, the doc comment on the signature (`.fsi`) is authoritative, and its `<include>`/`<inheritdoc>` tags are resolved with the same source-relative path base.

# Drawbacks

Adds a small amount of compiler and tooling complexity, and can hide places where documentation should be written uniquely rather than inherited.

# Alternatives

- Do nothing: continued doc duplication and no C# parity.
- Support only one of the two tags: rejected, since they share the XML-doc pipeline and are commonly used together.

# Prior art

- C#'s `<include>` and `<inheritdoc>` tags (linked in Summary).
- These are standard .NET XML documentation features already supported by documentation tools.

# Compatibility

* **Not a breaking change**
* Previous compilers: Tags passed through unexpanded (existing behavior)
* No FSharp.Core changes
* No binary compatibility concerns (compile-time feature)
* Not gated behind a language version (documentation-only, no new syntax)

# Interop

F# emits the standard .NET XML documentation format. `<include>` content is expanded into that output before emission, so downstream tools (docfx, Sandcastle, IDEs) see the same expanded XML they would from C#; `<inheritdoc/>` survives in the output for those tools to resolve. `<inheritdoc cref="...">` can inherit from a C# (or any .NET) base type when that assembly's XML doc file is present.

# Pragmatics

## Diagnostics
- `<include>` diagnostics matching the C# family: CS1589 (file or path not resolved), CS1590 (missing `file`/`path` attribute), CS1592 (malformed included XML)
- Warning for circular includes

## Tooling
IDE integration for design-time doc inheritance. No debugger/runtime impact.

## Performance
No runtime or generated-IL impact. Build-time cost is limited to reading and querying the referenced XML files.

## Scaling
Include nesting and inheritance-chain depth are the only scaling dimensions; both are expected to be shallow in practice and are bounded by cycle detection.

## Culture-aware formatting/parsing
N/A. No numeric/date/currency formatting is involved.

# Unresolved questions

- Whether in-editor Quick Info should expand `<include>` in-memory (as `<inheritdoc>` requires), so the editor shows the same content as the emitted XML. This initial `<include>` support resolves only at `--doc` emit time.
- Whether to also provide C#/VS-style automatic (tag-less) inheritance in the IDE for undocumented overrides/implementations.
