# F# RFC FS-1334 - Add `#elif` Preprocessor Directive

- [x] [Suggestion: Preprocessor Directives: #elif missing (#1370)](https://github.com/fsharp/fslang-suggestions/issues/1370)
- [x] Approved in principle
- [x] [Implementation: PR #19045](https://github.com/dotnet/fsharp/pull/19045)
- [x] [Discussion](https://github.com/fsharp/fslang-design/discussions/821)

## Summary
Add `#elif` to F# conditional compilation so multiple mutually exclusive branches can be written linearly (`#if ... #elif ... #elif ... #else ... #endif`) instead of nesting or repeating `#if` blocks. Aligns F# with C# and other languages; improves readability.

## Motivation
Without `#elif`, developers either:
- Nest `#if` inside `#else` (deep indentation, harder to scan), or
- Write separate `#if` blocks plus a final negated catch-all (`#if !A && !B && !C`), which is verbose and error-prone.
- C# parity

`#elif` removes redundancy, reduces logical mistakes, and eases cross-language sharing.

## Design
Grammar change:

```fsharp
#if <cond>
  group
#elif <cond>
  group
... (0+ more #elif)
#else
  group
#endif
```

First matching condition’s group is included; at most one group is compiled. `#elif` must follow an `#if` and precede an optional `#else`. No change to symbol definition semantics.

## Example #1
```fsharp
#if WIN64
let path = "/library/x64/runtime.dll"
#elif WIN86
let path = "/library/x86/runtime.dll"
#elif MAC
let path = "/library/iOS/runtime-osx.dll"
#else
let path = "/library/unix/runtime.dll"
#endif
```

## Example #2
```fsharp
module test =
 // Should evaluate to x = 3
 let x = 
#if false
   1
#elif false
   2     
#elif (true || false)
   3
#elif true
   4
#else
   5
#endif
```
     
## Spec Changes
Update conditional compilation section to:
- Add `{ #elif pp-expression group }` to grammar.
- Document ordering and “first true wins” semantics.
- State errors: `#elif` after `#else`, `#elif` without `#if`, missing condition.

## Drawbacks
Small parser/editor updates (fantomas, etc); possibility of long chains (already possible).

## Alternatives
Keep nesting; use multiple guarded blocks; introduce more complex compile-time constructs (unnecessary).

## Prior Art
VB.NET, C/C++, C#, Swift, Objective-C all support `#elif` (or equivalent). Aligns F# with ecosystem norms.

## Compatibility
Non-breaking. Older compilers will error on `#elif`. No IL or FSharp.Core changes.

## Diagnostics
Errors for:
- `#elif` without preceding `#if`
- `#elif` after `#else`
- Missing condition
- Unbalanced directives

## Tooling
Add keyword highlighting (or darken un-used branch), folding across entire block, completion for `#elif`. No runtime tooling differences.
Tools like Fantomas must update their logic that evaluates the #if conditions to handle #elif.

## Performance
Negligible; linear evaluation of conditions. No impact on generated code beyond selected branch.

## Scaling
Typical branch count ≤ 8; acceptable upper bound ≈ 50; linear processing.

## Migration Example
Before:
```fsharp
#if A
let mode = 1
#else
#if B
let mode = 2
#else
let mode = 3
#endif
#endif
```
After:
```fsharp
#if A
let mode = 1
#elif B
let mode = 2
#else
let mode = 3
#endif
```
