# F# RFC FS-1039 - Struct representations for active patterns 

The design suggestion [Struct representation for active patterns](https://github.com/fsharp/fslang-suggestions/issues/612) has been marked "approved in principle".

This RFC covers the detailed proposal starting from this suggestion

# Summary
[summary]: #summary

It should be possible to compile partial `(|A|_|)` active patterns to use struct options

# Motivation

Powerful and extensible features such as active patterns and optional arguments should be near-zero-cost abstractions. 

We should be able to provide better performance just via a simple attribute addition!

# Detailed design

How to use - add the attribute and put the `ValueSome`/`ValueNone` instead of `Some`/`None` cases.

```fsharp
[<return: Struct>]
let (|Int|_|) str =
   match System.Int32.TryParse(str) with
   | (true,int) -> ValueSome(int)
   | _ -> ValueNone
```

# Drawbacks

- More tricks for F# programmers to learn

# Alternatives

- Don't do any of this

- Require programmers to code complex matching by hands without expressiveness of active patterns

- Provide better inlining and optimization for active patterns. It can be _hard_ to achieve.

- Add a modality for "use structness for things declared in this scope", e.g.
  * use struct representations for return results for all active pattern declared in this scope
  * use struct representations for all syntactic tuples in this assembly in this scope
  * use struct representations for all optional arguments declared in this scope

# Compatibility

TBD

# Unresolved questions

TBD
