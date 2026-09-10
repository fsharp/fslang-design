---
name: authoring-rfcs
description: Use when writing or shortening an F# RFC.
---

# Authoring F# RFCs

**Write the smallest complete specification for experts, not a tutorial or compiler patch plan.**

Read full suggestions, approval decisions, and related RFCs. Follow the [template](../../../RFC_template.md).
State approved scope and unresolved choices. Use precise Simplified Technical English. Prefer code to prose.

- Would deleting this word or sentence lose important information from the document as a whole? If not, delete it.
- Is each rule stated once across the document?
- Are examples minimal, each showing a different concept rather than different syntax for the same concept?

## Common mistakes

- Length from repetition and equivalent examples, not design content.
- Length without completeness: missing relevant interactions with quotations, structs, type providers, SRTP, C# interop, or FSharp.Compiler.Service tooling.

Preserve semantic distinctions, safety, and compatibility when cutting.

## Size calibration

| RFC | Complexity | Characters |
| --- | --- | ---: |
| [Dotless float32 literals](https://github.com/fsharp/fslang-design/blob/2214fd07959e9cd8e48ecd499c0f3c83252218ab/FSharp-5.0/FS-1080-float32-without-dot.md) | Lexical change | 2,709 |
| [uint abbreviation](https://github.com/fsharp/fslang-design/blob/2214fd07959e9cd8e48ecd499c0f3c83252218ab/FSharp-5.0/FS-1082-uint-type-abbreviation.md) | Small API addition | 2,734 |
| [try/with in sequences](https://github.com/fsharp/fslang-design/blob/2214fd07959e9cd8e48ecd499c0f3c83252218ab/FSharp-8.0/FS-1134-try-with-in-sequence-expressions.md) | Control flow | 7,329 |
| [Byref and span](https://github.com/fsharp/fslang-design/blob/2214fd07959e9cd8e48ecd499c0f3c83252218ab/FSharp-4.5/FS-1053-span.md) | Lifetimes and safety | 25,242 |
| [Additional conversions](https://github.com/fsharp/fslang-design/blob/2214fd07959e9cd8e48ecd499c0f3c83252218ab/FSharp-6.0/FS-1093-additional-conversions.md) | Inference and overloads | 27,522 |
| [Nullable references](https://github.com/fsharp/fslang-design/blob/2214fd07959e9cd8e48ecd499c0f3c83252218ab/FSharp-9.0/FS-1060-nullable-reference-types.md) | Type-system change | 65,030 |

Full Markdown, LF-normalized Unicode characters. Compare semantic complexity, not just length. These are not quotas.

Compactness references: [NoBloat](https://github.com/dotnet/fsharp/blob/main/.github/instructions/NoBloat.instructions.md), [Compaction](https://github.com/dotnet/fsharp/blob/main/.github/skills/code-compaction/SKILL.md). Apply prose principles, not code-specific restrictions.
