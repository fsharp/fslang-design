# F# RFC FS-1132 - Extended interpolation syntax for triple quoted string literals

The design suggestion [Allow double dollar signs for interpolated strings as in C# 11](https://github.com/fsharp/fslang-suggestions/issues/1150) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1150)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/14640)
- [ ] Design Review Meeting(s) with @dsyme and others invitees
- [Discussion](https://github.com/fsharp/fslang-design/discussions/716)

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

Triple quoted strings are already quite handy for embedding e.g. XML in string literals, but not as convenient for embedding some other languages that use `{` and `}` in their syntax, e.g. JSON or CSS.
Currently every occurrence of a curly brace in an interpolated string needs to be escaped, which can quickly become tedious.
With this extended syntax, only the braces around interpolated expression need special attention, and that is where programmer's focus already is.

The motivation is not to enable any novel use cases, but to make working with interpolated string literals a much more seamless experience in the use cases mentioned above.

This change should:

1. be backwards compatible with existing support for triple quoted strings
2. align with C# [raw string literals](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/raw-string-literal.md) reasonably well

# Detailed design

The existing form of interpolated triple quoted strings: `$"""...{}..."""` is extended to allow multiple dollar signs at the beginning of the string literal.
The count of these `$` characters indicates how many `{` and `}` characters are used to delimit interpolation expression within the content of the literal.

Behavior of triple quoted string literals starting with a single `$` remains unchanged.

Triple quoted string literals starting with `N` `$` (where `N` > 1) behave as follows:
- No escaping mechanism for any characters.
- A sequence of `N` `{` indicates the beginning of an interpolation expression and a sequence of `N` `}` indicates the end of interpolation expression.
- A sequence of `2*N` or more `{` or `}` is not allowed and will result in a compilation error.
- In any sequence of more than `N` `{`, the outer braces are treated as content of the string, while the innermost `N` delimit the interpolation (and analogously for `}`). In that case, the outer braces don't have to be balanced, as they are just content of the string.

Note: This design is taken from C#'s [raw string literals](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/raw-string-literal.md).
It is an elegant solution that will already be familiar to a portion of dotnet developers.
However, there is one significant difference - in raw strings, these rules also extend to literals with single `$`.
Unfortunately, in F# that would be a breaking change,
so single `$` case remains an exception in terms of alignment with C#.

Example:

```
let text = "3 curly braces delimit interpolation expression"

$$$"""In a string literal with 3x$, { or {{ are simply treated as content..."""
// val it: string = "In a string literal with 3x$, { or {{ are simply treated as content..."

$$$"""...whereas {{{text}}}."""
// val it: string = "...whereas 3 curly braces delimit interpolation expression."

// It is also allowed to have {, {{, } or }} adjacent to the interpolation delimiters
// In the string below, these inner braces delimit interpolation, while others are just content
//      vvv    vvv
$$$"""{{{{{41+1}}}} = {{42}"""
// val it: string = "{{42} = {{42}"
"""
```

In addition, `%` characters, specifically with regards to format specifiers, will follow analogous rules. For example:

```
let successRate = 0.6m

// With single dollar, a single `%` character is reserved for format specifiers, and to add `%` in content, it has to be doubled.

$"""%.1f{successRate*100.0m}%% of the time, it works every time."""
// val it: string = "60.0% of the time, it works every time."

// Adding more dollars, format specifiers need to match with that many `%` characters, but `%` in content don't need escaping.

$$"""%%.1f{{successRate*100.0m}}% of the time, it works every time."""
// val it: string = "60.0% of the time, it works every time."

```

# Drawbacks

Even though this only extends already existing syntax, it still can be considered yet another way of doing string interpolation.

Moreover, as mentioned in [Detailed design section](#detailed-design) it can't be fully aligned with analogous C# feature (raw string literals - [docs](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-11#raw-string-literals)) and stay backward compatible at the same time, so this could be a source of confusion
(note that triple quoted string literals are already not fully aligned with C#'s raw string literals anyway - see [next section](#comparison-with-raw-strings).

# Comparison with raw strings

Raw string literals is a feature in C# corresponding to this proposal see - [detailed spec for raw strings](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/raw-string-literal.md)).

For convenience, here is a list of features that raw strings support and that are missing/different from triple quoted strings:
- Multi-line raw string literals in C# must start with a new line. They ignore the first and last new line, so the below literal will be 2 lines in C#, but 4 lines (including two "empty" lines) in F#:
```
"""
This is the first line in C#, but second line in F#.
This is the second and last line in C#, but third and second-to-last line in F#.
"""
```
- Raw strings in C# de-indent their content based on the indentation of the last line. This is done to make literals in code more readable. For example:
```
var xml = """
          <element attr="content">
            <body>
            </body>
          </element>
          """;
```
Since last line in the literal is indented by 10 spaces, each line in the contents gets de-indented by that much, resulting in:
```
<element attr="content">
  <body>
  </body>
</element>
```

In F#, the indentation remains in place, that is, the compiler will not remove it.

- Raw strings in C# can start with more than three `"` characters. It allows for sequences of more `"` in the contents of the string (similarly to `$` and curly braces), which isn't allowed in F#:
```
""""
This literal starts with 4x" so it can have """ in its contents.
It has to end with 4x" to match the opening.
""""
```

# Alternatives

Alternatives:

1. Try to fully align with C#'s raw string literals at the cost of backward compatibility.

  Such change would break any code that has at least one escaped `{` or `}` in an interpolated (triple quoted) string literal. This feature does not warrant such a breaking change.

2. Introduce alternative syntax for delimiting expression fills that does not clash with `{` and `}` which are common in JSON and certain other contexts.

  Discussed [here](https://github.com/fsharp/fslang-design/discussions/716#discussioncomment-4039580).

# Compatibility

Please address all necessary compatibility questions:

- Is this a breaking change?
*No*
- What happens when previous versions of the F# compiler encounter this design addition as source code?
*A compiler error*
- What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
*It just works, changes are only limited to lexing/parsing but compile into the same classes used for interpolated strings as before.*
- If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?
*It is not a change/extension to FSharp.Core*

# Pragmatics

## Diagnostics

- New compiler error for interpolated string literals with too many consecutive `{` or `}` characters.
- New compiler error for interpolated string literals with too many consecutive `%` characters.
- Otherwise, same compiler errors as for regular (single-`$`) interpolated strings.

Example of the new compiler error (error number still subject to change):
```
> $$"""{{{{hello""";;

  $$"""{{{{hello""";;
  -----^^^^

stdin(1,6): error FS3375: The interpolated triple quoted string literal does not start with enough '$' characters to allow this many consecutive opening braces as content.

> $$"""42 %%%%""";;

  $$"""42 %%%%""";;
  --------^^^^

stdin(16,9): error FS1250: The interpolated triple quoted string literal does not start with enough '$' characters to allow this many consecutive '%' characters.
```

## Tooling

Compiler tooling should offer the same support for the extended form of interpolated string literals as it currently does for interpolated triple quoted literals.
This includes colorization, autocompletion, and navigational features in an interpolated context.

## Performance

This feature has no impact on performance.

## Scaling

We do not set an explicit limit on the number of `$` characters allowed at the beginning of a string literal.
In a hand-written code it will typically not exceed 3.

## Culture-aware formatting/parsing

N/A

# Resolved questions

1. Do we want to also provide a way to easily input percent signs in interpolated string literals?

Yes, that is inline with the goal of having a string literal that does not require escaping.
Relevant [discussion thread](https://github.com/fsharp/fslang-design/discussions/716#discussioncomment-4022757)

2. Should we extend this feature to regular (single-quoted) string literals as well in addition to triple quoted strings?

No, for single-quoted string literals, verbatim strings are enough.
Relevant [discussion thread](https://github.com/fsharp/fslang-design/discussions/716#discussioncomment-4089195)

# Links

- [F# string interpolation RFC](https://github.com/fsharp/fslang-design/blob/main/FSharp-5.0/FS-1001-StringInterpolation.md)
- [C# raw string literals feature proposal](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/raw-string-literal.md)
- [C# docs on raw string literals](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-11#raw-string-literals)
