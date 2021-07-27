# F# RFC FS-1110 - Allow `expr[idx]` as index/slice syntax

The design suggestion [Allow `expr[idx]` as index/slice syntax](https://github.com/fsharp/fslang-suggestions/issues/1053) has been approved in principle.
This RFC covers the detailed proposal for all above suggestions and issues

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1053)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/11749)
- [ ] Design Review Meeting(s) with @dsyme and others invitees
- [ ] Discussion

# Summary

This RFC allows `expr[idx]` as indexer/slicing syntax and normalize this as the preferred indexing syntax from F# 6.0 onwards.

> NOTE: the full implementability of this RFC is still being assessed.

# Principles

Any existing F# code will continue to check, possibly with warnings. The warnings can be suppressed by using `--langversion:5.0`.

F# well-typed code can have `.[` replaced by `[` throughout expressions and will check. There are some exceptions where the use
of `.[` will still be required, a warning message will indicate this.

# Motivation

This is to make learning F# easier (less to learn) and allow simpler migration of existing code from Python, C# and other languages.

# Background

Up to and including F# 5.0, F# has used `expr.[idx]` as indexing syntax.  This was inherited from OCaml, which uses
it for string indexing lookup.  In F# the notation has always been type-directed, so a well-known strong indexable type has been needed for `expr`.

# Detailed Design

The expression form 

```fsharp
expr1[expr2]
```

is now parsed as a high-precedence application if `expr1` and `[` are adjacent. This is already the case for `ident[expr]`, and
is additionally the case for any expression immediately applied to an adjacent list argument.

When checked:
1. If `expr1` is of function type, a warning is emitted recommending the addition of a space.
2. If `expr1` is not of function type, `expr2` is interpreted as indexing or slicing notation and the indexing/slicing is resolved in the usual way

### Special consideration for argument expressions

Special consideration is needed for cases of the following form:

```fsharp
expr1 expr2[expr3]
```

where `expr2` is not an identifier and `[expr3]` immediately adjacent to `expr2`.  This case is problematic because of this ambiguity:

1. Existing code: The aboev form is allowed in F# 5.0 and before as a curried function application of `expr1` to `expr2` then `[expr3]`
2. Future code: The above form may arise when taking existing code and changing `.[` to `[`. 

Note that the case where `expr2` is an identifier has already been disallowed in prior versions of F#. So we need only
consider the cases where `expr2` is not an identifier.

For "existing code", ideally such code should have been written with a space in application:

```fsharp
expr1 expr2 [expr3]
```

Here are some examples of "existing code":

```fsharp
let f x y = ()
let someFunction x = x
f (someFunction arr)[2] 
f [2][2]                
```

We want to advise the used to adjust such code to curried function application with appropriate spaces, as was always intended, and is better code and clearer:
```fsharp
let f x y = ()
let someFunction x = x
f (someFunction arr) [2]  // add a space
f [2] [2]                 // add a space
```

Here are some examples of "future code" (that is, where changing `.[` to `[` would create code of the form above):

```fsharp
Some (Lazy.force ILCmpInstrRevMap).[cmp]

opt.Split([|':'|]).[0]

FSharpType.GetTupleElements(ty).[i]

escape (lexeme lexbuf).[1]
```

In these cases, if the user blindly replaces `.[` to `[` then we get code of the form above.

As a result, special consideration is needed for code of this form, compatibility reasons. 

* For adjacent expressions `someFunction (expr)[idx]` in non-high-precedence-application argument position, a warning is given that either a space must be inserted for application, or `.[` is required.

* No informational warning is given when `someFunction (expr).[idx]` is used in argument position

# Examples 

All the usual examples of indexer lookup and slicing with `.[` replaced by `[`:

```fsharp
let arr = [| 1;2;3 |]
arr[0] <- 2
arr[0]
arr[0..1]
arr[..1]
arr[0..]
```

And for higher-dimensional arrays:

```fsharp
let arr = Array4D.create 3 4 5 6 0
arr[0,2,3,4] <- 2
arr[0,2,3,4]
```

### Examples of warnings for Existing Code

The following code is valid in F# 5.0 and each line will now generate a warning:
```fsharp
let f1 a = ()
let f2 a b = ()

let v1 = f1[1]            // applies f1 to a list, now emitting a warning to insert a space
let v2 = f2[1][2]         // applies f2 to two lists, now emitting a warning to insert a space
let v3 = f2 [1][2]        // applies f2 to two lists, now emitting a warning to insert a space
let v4 = f2 (id [1])[2]   // applies f2 to two lists, now emitting a warning to insert a space
```

# Compatibility

If a precise language version less than or equal to `5.0` is specified, such as `--langversion:5.0`, then warnings to insert spaces are not emitted.

# Unresolved questions

The full implementability of "Special consideration for existing curried function applications" is still being assessed.


