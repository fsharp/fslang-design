# F# RFC FS-1033 - Extend String module

The design suggestion [#112](https://github.com/fsharp/fslang-suggestions/issues/112) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/112)
* [ ] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/187)
* [ ] Implementation: not started

# Summary
[summary]: #summary

FSharp.Core already contains useful functions for interacting with BCL types such as String and IEnumerable, but the String module is particularly lacking. It's certainly not uncommon to either need to write your own library of tiny string wrappers or pull in a third-party library such as FSharpX. This RFC is to add several of the most popular String methods to the FSharp.Core String module. 

# Motivation
[motivation]: #motivation

String functions are very commonly used, as detailed in the [.NET API Catalog](https://apisof.net/catalog/System.String). The String module already exists in the F# standard library, although it is particularly bare. Adding more functions to the String library allows strings to be used fluently with pipes and as transformers to functions like List.map.

# Detailed design
[design]: #detailed-design

Philip Carter detailed the usages of String class methods in the wider .NET ecosystem in [a comment on the suggestion issue](https://github.com/fsharp/fslang-suggestions/issues/112#issuecomment-260506490). Using the same source for API usage telemetry, the following functions are proposed to be added to the String module:

Proposed Signature | String method | API Port Telemetry (% all apps)
--- | --- | ---
`contains : string -> string -> bool` | String.Contains(String) | 24%
`compare : StringComparison -> string -> string -> int` | String.Compare(String, String, StringComparison) | 15%
`endsWith : string -> string -> bool` | String.EndsWith(String) | 19%
`endsWithComparison : StringComparison -> string -> string -> bool` | String.EndsWith(String, StringComparison) | 14%
`equals : StringComparison -> string -> string -> bool` <br> (ordinary String.Equals purposefully missed as it already exists with `((=) other)`) | String.Equals(String, StringComparison) | 18%
`indexOf : string -> string -> int` | String.IndexOf(String) | 16%
`indexOfComparison : StringComparison -> string -> string -> int` | String.IndexOf(String, StringComparison) | 14%
`lastIndexOf : string -> string -> int` <br> (for symmetry with indexOf) | String.LastIndexOf(String) | 7%
`lastIndexOfComparison : StringComparison -> string -> string -> int` <br> (for symmetry with indexOfComparison) | String.LastIndexOf(String, StringComparison) | 6%
`replaceChar : char -> char -> string -> string` | String.Replace(char, char) | 14%
`replace : string -> string -> string -> string` | String.Replace(String, String) | 32%
`splitChar : StringSplitOptions -> seq<char> -> string -> string []` | String.Split(char[]) | 31%
`split : StringSplitOptions -> seq<string> -> string -> string []` | String.Split(string[]) | 10%
`startsWith : string -> string -> bool` | String.StartsWith(string) | 25%
`startsWithComparison : StringComparison -> string -> string -> bool` | String.StartsWith(string, StringComparison) | 18%
`substring : (length: int?) -> (startIndex: int) -> string -> string` | String.Substring(int, int) | 32% and 35%
`toLower : CultureInfo -> string -> string` | String.ToLower(CultureInfo) | 8%, 23% (no arguments)
`toLowerInvariant : string -> string` | String.ToLowerInvariant() | 15%
`toUpper : CultureInfo -> string -> string` | String.ToUpper(CultureInfo) | 8%, 13% (no arguments)
`toUpperInvariant : string -> string` | String.ToUpperInvariant() | 11%
`trim : string -> string` | String.Trim() | 29%
`trimChars : seq<char> -> string -> string` | String.Trim(char[]) | 11%
`trimEndChars : seq<char> -> string -> string` | String.TrimEnd(char[]) | 16%
`trimStartChars : seq<char> -> string -> string` | String.TrimStart(char[]) | 13%

The rationale for leaving some String properties/methods such as String.Empty, String.IsNullOrEmpty and String.IsNullOrWhiteSpace, is that they already exist in the BCL and would otherwise just be direct aliases on the FSharp.Core String module.

The vast majority of proposed functions are used in >15% of applications. However, for symmetry (e.g trimStartChars as well as trimEndChars), some functions that are lesser used were also added to the proposal.

The functions themselves will not be difficult to implement, as they are merely 'one-liners' around members on the String class. Some discussion and consensus is required on the signature of some functions. In particular:

- split: Should this function take a seq<char> or seq<string>, or both? How should the StringSplitOptions be handled?
- toUpper/toLowerInvariant: Should culture variant functions (such as toUpper/toLower) also be supplied?

# Drawbacks
[drawbacks]: #drawbacks

As Don mentioned, adding more functions to modules in FSharp.Core can start a slippery slope - where should the line be drawn? However, by referring to hard evidence (such as the API catalog usage statistics), we can make sure that only the most commonly used functions are added and so their usefulness is guaranteed.

# Alternatives
[alternatives]: #alternatives

The alternative is to not implement the functions and third-party libraries or hand-rolled wrapper modules to be used instead, as now.

# Unresolved questions
[unresolved]: #unresolved-questions

- Precise names of the methods. In particular, do we match the BCL's naming exactly for consistency, or choose others?
- Which functions exactly should be included in the String module.
- Should culture variant functions be supplied to those that require it? Such as String.ToUpper/Lower
- How overloads should be handled.
