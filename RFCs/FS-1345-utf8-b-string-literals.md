# F# RFC FS-1345 - UTF-8 `B`-suffix string literals

The design suggestion [Extending `B` string suffix to be UTF-8 strings](https://github.com/fsharp/fslang-suggestions/issues/1421) is approved in principle. This RFC is the focused successor to section **r** of [#800](https://github.com/fsharp/fslang-design/pull/800), as requested by its [design review](https://github.com/fsharp/fslang-design/pull/800#issuecomment-3852354596).

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1421)
- [x] Approved in principle
- [ ] Implementation
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

# Summary

Extend the existing regular and verbatim `B`-suffix string forms from ASCII-only byte arrays to compile-time UTF-8 byte arrays.

```fsharp
let text = "你好"B
let path = @"C:\資料"B
```

Both values have type `byte array`.

# Motivation

Today, non-ASCII protocol, file-format, parser, and test data must use runtime encoding or manually maintained byte values. The existing `B` syntax already denotes a byte array, so extending it to UTF-8 provides readable source, compile-time validation, and deterministic bytes without introducing target-typed strings or a new result type.

# Detailed design

## Forms and encoding

The change applies only to the existing tokens:

```fsgrammar
token bytearray = " string-char * "B
token verbatim-bytearray = @" verbatim-string-char * "B
```

The compiler:

1. decodes the contents using the existing rules for the corresponding regular or verbatim string;
2. rejects unpaired UTF-16 surrogates;
3. encodes each Unicode scalar value as standard shortest-form UTF-8.

The output contains no byte-order mark or null terminator, and the compiler performs no replacement or Unicode normalization.

```fsharp
"é"B       // [| 0xC3uy; 0xA9uy |]
"\u4F60"B  // UTF-8 for U+4F60
```

Escapes denote text before encoding. For example, `"\250"B` denotes U+00FA and produces `C3 BA`; it does not insert raw byte `FA`. Raw protocol bytes use an explicit byte array.

## Result and evaluation

Each evaluation produces a fresh mutable `byte array`, as existing `B` literals do. The compiler may copy from static data or emit equivalent element initialization, but it must not return shared mutable storage.

Explicit quotations and `[<ReflectedDefinition>]` represent the literal as an ordinary fresh byte-array construction containing the encoded elements. No new quotation node is introduced.

## Compatibility boundary

All currently valid ASCII `B` literals are unchanged because ASCII maps identically to UTF-8.

Under the new language version, a non-ASCII string that previously received the ASCII-range diagnostic is instead accepted when its decoded text is well formed. Code that suppressed the old diagnostic and relied on legacy one-byte output must use the older language version or explicit bytes to preserve that behavior.

The byte-character form remains a single ASCII byte:

```fsharp
'A'B // valid
'é'B // invalid: one character would require multiple UTF-8 bytes
```

This RFC does not add `B` to interpolated or triple-quoted strings and does not change ordinary strings, format strings, `[<Literal>]` values, optional defaults, or constant patterns.

# Changes to the F# spec

In **Lexical analysis / Strings and Characters**, retain the token grammar above but replace the ASCII-only semantic restriction with the decode, validation, and UTF-8 rules in this RFC. Keep `bytechar` ASCII-only.

In **Simple constant expressions**, clarify that a byte-array string is an array-valued literal expression, not a CLI constant.

In **Code generation**, require a fresh array for each evaluation while permitting equivalent lowerings. Gate the new non-ASCII behavior by the corresponding preview language version.

# Drawbacks

- `B` may have been understood as “ASCII” rather than “byte array”; documentation must explain the broader UTF-8 meaning.
- Compile-time encoding removes encoder work but not the allocation required by fresh mutable-array semantics.
- Escape values are Unicode text, not raw bytes, which may surprise code working with binary protocols.
- Interpolated and triple-quoted byte strings remain unsupported.

# Alternatives

- Keep ASCII-only behavior: preserves the restriction but requires runtime encoding or manual bytes.
- Add a C#-style `u8` suffix returning `ReadOnlySpan<byte>`: avoids array allocation but breaks the established type and mutability of `B`; it can be proposed separately.
- Replace malformed text with U+FFFD: hides source errors and makes output less predictable.
- Normalize before encoding: changes user-provided text and requires choosing a normalization policy.

# Prior art

C# `u8` literals validate and encode UTF-8 at compile time but return `ReadOnlySpan<byte>`. .NET's strict UTF-8 encoding gives the same payload for well-formed text. Other languages provide byte-string forms with different type and escape rules.

# Compatibility

This is a language-versioned source extension. Older compilers diagnose the newly accepted source, while produced assemblies contain only ordinary `System.Byte[]` construction and remain consumable by older tools. No FSharp.Core or metadata change is required.

# Interop

The result is directly consumable as `System.Byte[]` by any CLI language.

# Pragmatics

## Diagnostics and tooling

Diagnostics identify an invalid escape, an unpaired surrogate, an unsupported `B` string form, or a non-ASCII byte character. The source range should cover the offending character or escape where possible. Hover continues to report `byte array` and may show encoded length.

## Performance and scaling

Validation and encoding are linear in decoded input size. Runtime cost is fresh-array construction; large repeated literals may warrant a cached binding.

## Culture-aware formatting/parsing

The encoding is independent of culture, locale, code page, and operating system.

# Unresolved questions

Whether interpolated or triple-quoted UTF-8 byte strings should be added is left to separate proposals.
