# F# RFC FS-1128 - Extended interpolation syntax for triple quoted string literals

The design suggestion [Allow double dollar signs for interpolated strings as in C# 11](https://github.com/fsharp/fslang-suggestions/issues/1150) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1150)
- [x] Approved in principle
- [ ] [Implementation]() (no implementation yet, but a proof of concept [here](https://github.com/dotnet/fsharp/compare/main...abonie:fsharp:poc_improved_interpolation))
- [ ] Design Review Meeting(s) with @dsyme and others invitees
- [Discussion]()

# Summary

Extend current form of interpolated triple quoted string to allow specifying how many braces should start an interpolation (equal to the number of dollar signs at the start of the literal). For example:
```
let json = $$"""
{
  "id": "Some guid",
  "name": "{{user.Name}}",
};
"""
```
In the above example, `$$` starting the string literal indicates that interpolated expression will be enclosed in `{{...}}` and single `{` and `}` will be treated as content.

# Motivation

Triple quoted strings are already quite handy for embedding e.g. XML in string literals, but not as convenient for embedding some other languages that use `{` and `}` in their syntax, e.g. JSON or CSS. Currently every occurrence of a curly brace in an interpolated string needs to be escaped, which can quickly become tedious. With this extended syntax, only the braces around interpolated expression need special attention, and that is where programmer's focus already is.

The motivation is not to enable any novel use cases, but to make working with interpolated string literals a much more seamless experience in the use cases mentioned above.

This change should:
1. be backwards compatible with existing support for triple quoted strings
2. align with C# raw string literals reasonably well

# Detailed design

The existing form of interpolated triple quoted strings: `$"""...{}..."""` is extended to allow multiple dollar signs at the begining of the string literal. The count of these initiatory `$` characters indicates how many `{` and `}` characters are used to delimit interpolation expression within the content of the literal.

Important note: To maintain backward compatibility, there are no changes in semantics for a single-`$` case. This means that it is an edge case in terms of how this feature behaves. It would be nice to have a clean design that covers all cases, but it is not worth breaking backward compatibility for. Therefore the below description applies to the literals starting with 2 or more `$`.

Triple quoted string literals starting with 2 or more `$` characters have no escaping mechanism for `{` or `}`. Instead, the literal can always be constructed in such a way as to ensure that interpolation delimiters will not collide with other curly braces in the content. In a literal that starts with `N` `$` characters, a sequence of consecutive `{` can be at most `2*N-1` characters long and for any sequence of more than `N`, the innermost `N` `{` characters delimit the interpolation, while the outter up to `N-1` are treated as content (analogously for `}`). A sequence of `2*N` or more `{` (or `}`) will result in a compilation error.

Example:
```
let text = "3 curly braces delimit interpolation expression"
$$$"""
In a string literal with 3x$, { or {{ are simply treated as content
whereas {{{text}}}.
It is also allowed to have {, {{, } or }} adjacent to the interpolation delimiters as so:
{{{{41+1}}}} = {42}
"""
```

# Drawbacks

Even though this only extends already existing syntax, it still can be considered yet another way of doing string interpolation. Moreover, it can't be fully aligned with analogous C# feature (raw string literals) and stay backward compatible at the same time, so this could be a source of confusion (but note that triple quoted string literals are already similar but not really aligned with C#'s raw string literals anyway).

# Alternatives

Alternatives:
1. Try to fully align with C#'s raw string literals at the cost of backward compatiblity.
2. Introduce alternative syntax for delimiting expression fills that does not clash with `{` and `}` which are common in JSON and certain other contexts.

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change?
*No*
* What happens when previous versions of the F# compiler encounter this design addition as source code?
*A compiler error*
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
*AFAICT it should work, changes will hopefully be limited to lexing stage*
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?
*It is not a change/extension to FSharp.Core*

# Pragmatics

## Diagnostics

* New compiler error for interpolated string literals with too many consecutive `{` or `}` characters.
* Otherwise, same compiler errors as for regular (single-`$`) interpolated strings.

## Tooling

Compiler tooling should offer the same support for the extended form of interpolated string literals as it currently does for interpolated triple quoted literals. This includes colorization, autocompletion, and navigational features in an interpolated context.

## Performance

AFAICT there should be no impact on performance (although there might be if using certain constructs in regexes within .fsl files can have a big impact `XXX TODO`)

## Scaling

Please list the dimensions that describe the inputs for this new feature, e.g. "number of widgets" etc.  For each, estimate a reasonable upper bound for the expected size in human-written code and machine-generated code that the compiler will accept.

For example

* Expected maximum number of widgets in reasonable hand-written code: 100
* Expected reasonable upper bound for number of widgets accepted: 500

Testing should particularly check that compilation is linear (or log-linear or similar) along these dimensions.  If quadratic or worse this should ideally be noted in the RFC.

`TODO` *No clue really, but I expect more than 3x$ will be very rarely used though*

## Culture-aware formatting/parsing

Does the proposed RFC interact with culture-aware formatting and parsing of numbers, dates and currencies? For example, if the RFC includes plaintext outputs, are these outputs specified to be culture-invariant or current-culture.

*No* `TODO: remove this section?`

# Unresolved questions

1. Do we want to also provide a way to easily input procent signs in interpolated string literals?

# Links
- [F# string interpolation RFC](https://github.com/fsharp/fslang-design/blob/main/FSharp-5.0/FS-1001-StringInterpolation.md)
- [C# raw string literals feature proposal](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/raw-string-literal.md)
- [C# docs on raw string literals](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-11#raw-string-literals)