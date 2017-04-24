# F# RFC FS-1032 - Additional String module functions

The design suggestion [112](https://github.com/fsharp/fslang-suggestions/issues/112) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [ ] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/112)
* [ ] Details: [under discussion](https://github.com/fsharp/fsharp-design/issues/FILL-ME-IN)
* [ ] Implementation: [In progress](https://github.com/Microsoft/visualfsharp/pull/FILL-ME-IN)

# Summary
[summary]: #summary

FSharp.Core already contains useful functions for interacting with BCL types such as String and IEnumerable, but the String module is particularly lacking. It's certainly not uncommon to either need to write your own library of tiny string wrappers or pull in a third-party library such as FSharpX. This RFC is to add several of the most popular String methods to the FSharp.Core String module. 

# Motivation
[motivation]: #motivation

String functions are very commonly used, as detailed in the [.NET API Catalog](https://apisof.net/catalog/System.String). The String module already exists in the F# standard library, although it is particularly bare. Adding more functions to the String library allows strings to be used fluently with pipes and as transformers to functions like List.map.

# Detailed design
[design]: #detailed-design

Philip Carter detailed the usages of String class methods in the wider .NET ecosystem in [a comment on the suggestion issue](https://github.com/fsharp/fslang-suggestions/issues/112#issuecomment-260506490):

```
replace : string -> string -> string -> string   - 32.1%
startsWith/endsWith : string -> bool             - 25.4%
split : seq<char> -> string -> seq<string>       - 31.1%
toUpper/toLower(Invariant) : string -> string    - 12.3%/22.7%
trim : string -> string                          - 29.4%
trimStart/trimEnd : string -> string             - 12.5%/15.5%
```

Note that Philip mentioned the empty, isEmpty and isWhitespace functions, but I do not believe they are necessary to add to the F# module. String.Empty, String.IsNullOrEmpty and String.IsNullOrWhiteSpace already exist in the BCL and would otherwise just be direct aliases on the FSharp.Core String module.

The functions themselves will not be difficult to implement, as they are merely 'one-liners' around members on the String class. Some discussion and consensus is required on the signature of some functions in particular:

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
