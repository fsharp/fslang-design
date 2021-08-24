# F# RFC FS-1033 - Extend String module

The design suggestion [#112](https://github.com/fsharp/fslang-suggestions/issues/112) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/112)
* [x] Approved
* [x] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/187)
* [x] Implementation: [in progress](https://github.com/dotnet/fsharp/pull/12026)

# Summary
[summary]: #summary

FSharp.Core already contains useful functions for interacting with BCL types such as String and IEnumerable, but the String module is particularly lacking. This is by-design, as the surface area of string processing is deemed too large.

This RFC proposes to change this design point and include the most common forms of string processing in the ready-for-pipelining-programming section of the F# core library.

# Motivation
[motivation]: #motivation

String functions are very commonly used, as detailed in the [.NET API Catalog](https://apisof.net/catalog/System.String). The String module already exists in the F# standard library, although it is particularly bare.

Adding more functions to the String library allows strings to be used fluently with pipes and as transformers to functions like List.map.

# Design Principles (under discussion)

The selected functions should:

* Be frequently used (>= 15% usage across applications)
* Be 100% intuitive
* Be useful in beginner scenarios
* Be idiomatic F# and not need .NET abstractions
* Not be a simple wrapper around an equally convenient .NET API (like String.Empty, String.IsNullOrEmpty, and String.IsNullOrWhitespace)

Philip Carter detailed the usages of String class methods in the wider .NET ecosystem in [a comment on the suggestion issue](https://github.com/fsharp/fslang-suggestions/issues/112#issuecomment-260506490). The following is a table of the most frequently used String APIs according to this data (everything that sees >= 15% usage in all applications):

String method | API Port Telemetry (% all apps)
--- | ---
String.Trim() | 29%
String.Substring(int, int) | 32% and 35%
String.Replace(String, String) | 32%
String.Split(char[]) | 31%
String.StartsWith(string) | 25%
String.Contains(String) | 24%
String.ToUpper() | 23 %
String.EndsWith(String) | 19%
String.StartsWith(string, StringComparison) | 18%
String.Equals(String, StringComparison) <br> (ordinary String.Equals purposefully missed as it already exists with `((=) other)`) | 18%
String.IndexOf(String) | 16%
String.TrimEnd(char[]) | 16%
String.Compare(String, String, StringComparison) | 15%
String.ToLowerInvariant() | 15%

# Detailed design (under discussion)
[design]: #detailed-design

Given the above considerations, the following functions are proposed to be added:

```fsharp
contains : string -> string -> bool 
replace : string -> string -> string -> string 
split : string -> string -> string seq
startsWith : string -> string -> bool
endsWith : string -> string -> bool 
trim : string -> string 
```


# Drawbacks
[drawbacks]: #drawbacks

1. Adding more functions to modules in FSharp.Core can start a slippery slope - where should the line be drawn? The proposal is to refer to hard evidence (such as the API catalog usage statistics), to ensure that only the most commonly used functions are added and so their usefulness is guaranteed.
  
2. Adding the functions means users don't learn how to use the .NET libraries, which are generally better documented, have code samples and so on.
  
3. The lack of overloading in module-defined functions makes it hard for string processing to deal with culture-comparison and optional arguments
  
# Alternatives
[alternatives]: #alternatives

1. Not implement the functions and use the .NET APIs directly.

2. Write your own library of tiny string wrappers or pull in a third library.
  
# Unresolved questions
[unresolved]: #unresolved-questions

 Some discussion and consensus is required on the signature of some functions. In particular:

- split: Should this function take a seq<char> or seq<string>?
