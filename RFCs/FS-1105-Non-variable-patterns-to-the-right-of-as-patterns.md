# F# RFC FS-1105 - Non-variable patterns to the right of `as` patterns

The design suggestion [Enable pattern matching on more specific types from type test patterns](https://github.com/fsharp/fslang-suggestions/issues/1025) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1025)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/11674)
- [ ] Design Review Meeting(s) with @dsyme and others invitees
- [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

# Summary

The right hand side of `as` patterns will accept patterns instead of matching the name of an identifier.

# Motivation

With [FS-1092 - Anonymous Type-tagged Unions](https://github.com/fsharp/fslang-design/discussions/519), type test patterns are expected to used more frequently. However, matching on the derived types is more difficult due to the right hand side of `as` patterns only accepting an identifier interpreted as a variable instead of actual patterns. As a result, a second level of matching, or a local active pattern with an additional cast with regards to the argument which is prone to being outdated, is needed.

# Detailed design

Code like
```fs
type DU = DU of int * int
let (|Id|) = id
match box (1, 2) with
| :? (int * int) as (x, y) -> printfn $"A tuple: {x}, {y}"
| :? struct(int * int) as (x, y) -> printfn $"A struct tuple: {x}, {y}"
| :? DU as DU (x, y) -> printfn $"A DU: {x}, {y}"
| :? int as Id i -> printfn $"An int: {i}"
| _ -> printfn "Nope"
```
are now valid.

It will act similarly as the `&` operator requiring both sides of the operator to have a successful match before succeeding.

Although [the `as` operator has the lowest precedence of all operators](https://docs.microsoft.com/en-us/dotnet/fsharp/language-reference/symbol-and-operator-reference/#operator-precedence), the right hand side of `as` operators currently only parse one identifier, which means that `,`, `|`, `&`, `::`, `:`, and `as` operators can still appear to the right of identifiers after `as` operators and be regarded as valid code. Therefore, the right side of `as` operators should parse a pattern with an equivalent precedence to DU case matching and active patterns, which means that patterns with lower precedence will require parentheses.

The existing double purpose of
```fs
// SyntaxTree.fs
type SynPat =
...
    | Named of
        pat: SynPat *
        ident: Ident *
        isSelfIdentifier: bool *
        accessibility: SynAccess option *
        range: range
```
that represents both variable patterns and `as` patterns will be split into
```fs
type SynPat =
...
    | Name of
        ident: Ident *
        isSelfIdentifier: bool *
        accessibility: SynAccess option *
        range: range
    | As of
        lhsPat: SynPat *
        rhsPat: SynPat *
        range: range
```
, 
```fs
// pars.fsy
headBindingPattern:
  | headBindingPattern AS ident 
      { SynPat.Named ($1, $3, false, None, rhs2 parseState 1 3) }
...
```
will be changed to
```fs
headBindingPattern:
  | headBindingPattern AS constrPattern 
      { SynPat.As($1, $3, rhs2 parseState 1 3) }
...
```
and
```fs
// pars.fsy
  | parenPattern AS ident 
      { SynPat.Named ($1, $3, false, None, rhs2 parseState 1 3) }
```
will be changed to
```fs
  | parenPattern AS constrPattern 
      { SynPat.As($1, $3, rhs2 parseState 1 3) }
```

The syntax for `as` patterns for `type Type(...) as this =` and `new(...) as this = ...` will be unchanged because of [this comment from @dsyme](https://github.com/fsharp/fslang-suggestions/issues/1015#issuecomment-852089676).

# Drawbacks

[@charlesroddie pointed out that](https://github.com/fsharp/fslang-suggestions/issues/1025#issuecomment-856287392)
> I don't think F# should add features to make type tests easier. They should not be part of the standard toolkit.

However, [@dsyme pointed out that](https://github.com/fsharp/fslang-suggestions/issues/1025#issuecomment-857005525)
> For me this is more about language uniformity. It's reasonable to make these things more orthogonal

# Alternatives

- Not doing anything. Users continue to seek workarounds.
- Introducing a new operator with a better precedence which allows more parentheses to be omitted. This requires more learning and fragments the language.

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change? No
* What happens when previous versions of the F# compiler encounter this design addition as source code? Error as usual
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries? Work as usual
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct? Not a change to FSharp.Core


# Unresolved questions

Should we require parentheses for all patterns with whitespaces as well? This will be annoying as more often than not DU case matching and active pattern matching themselves will contain additional parentheses which clutter reading.
