# F# RFC FS-1335 - Triple-quoted byte strings

This RFC documents the change that makes triple-quoted string syntax interoperate with byte-string literal suffixes so that existing triple-quoted strings and byte-string suffxies can be combined in the usual way. There is no separate language suggestion for this change — it simply composes two long‑standing features of the language so they behave consistently together.

- [x] [Suggestion] (none — composition of existing features)
- [?] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/19182)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

# Summary

Allow byte-string literal suffix to be applied to triple-quoted string literals (and vice‑versa) so forms like `"""..."""B` are accepted and behave consistently with the corresponding non‑triple‑quoted byte string forms.

# Motivation

This improves ergonomics for code that needs multi-line byte content (for example, embedding binary blobs in a readable, multi-line form in source), test fixtures, and interop scenarios where the source representation is easier to author as a triple‑quoted literal.

# Detailed design

The change is purely lexical: allow the existing F# byte-string suffix (`B`) to appear after any of the existing string literal forms including triple-quoted forms. Semantics of the literal remain the same as before:

- The suffix indicates the literal is a byte-string literal represented as an ASCII byte array.
- Triple-quoted delimiters behave the same way they always have — they permit multi-line content and reduce the need for escaping of internal double quotes.

Examples:

```fsharp
// triple-quoted byte string (multi-line)
let bytes1 = """
First line
Second line with "quotes" and backslashes \\
"""B

let bytes2 = "Hello\n"B

// Triple-quoted non-byte string unchanged
let s = """This is a "normal" triple-quoted string"""
```

No new literal kinds are introduced — the change simply permits the familiar suffix + delimiter combinations that a user would expect.

# Changes to the F# spec

https://fsharp.github.io/fslang-spec/lexical-analysis/#35-strings-and-characters

```diff
- token triple-quoted-string = """ simple-or-escape-char* """
+ token triple-quoted-string = """ simple-or-escape-char* """
+ token bytearray-triple-quoted = """ simple-or-escape-char* """B
```

# Drawbacks

- A breaking change, but chances of the specific scenario appearing in the wild are minute.

# Alternatives

- Do nothing: continue to disallow combining the byte-string suffix with triple-quoted delimiters. This would preserve the status quo but continue to deprive users of a convenient literal form.

# Compatibility

* Is this a breaking change?
  - Technically, yes.

  ```
  > let x a b = 7;;
  val x: a: 'a -> b: 'b -> int

  > let B = ();;
  val B: unit = ()

  > x """ " """B;;
  val it: int = 7
  ```

  Implementing the suggested change would give a new meaning to this kind of program. 
* What happens when previous versions of the F# compiler encounter this design addition as source code?
  - `error FS0003: This value is not a function and cannot be applied.`
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
  - No effect; this is a source-only lexical acceptance change.

# Interop

* What happens when this feature is consumed by another .NET language?
  - Other .NET languages see the compiled artifact; the literal is a compile-time construct. No change to runtime types or IL structure beyond what existing byte-string suffix already produced.
* No extra interop work is required.

# Pragmatics

## Diagnostics

- Lexical or parse errors for malformed triple-delimited byte-string literals should be reported using the same diagnostics as for malformed string or byte-string literals.

## Tooling

- Every aspect related to triple quoted strings should apply here too.

## Performance

- No meaningful change.

## Scaling

- N/A

## Culture-aware formatting/parsing

- N/A

# Unresolved questions

- None