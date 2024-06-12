# F# RFC FS-1110 - Allow `expr[idx]` as index/slice syntax

The design suggestion [Allow `expr[idx]` as index/slice syntax](https://github.com/fsharp/fslang-suggestions/issues/1053) has been approved in principle.
This RFC covers the detailed proposal for all above suggestions and issues

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1053)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/11900)
- [ ] Design Review Meeting(s) with @dsyme and others invitees
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/611)

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

When processing the expression form 

```fsharp
expr1[expr2]
```

where `expr1` and `[` are adjacent, first `expr1` is checked as usual. Then:

1. If `expr1` unifies with a function type, a warning is emitted recommending the addition of a space, and the construct is processed as an application

2. If `expr1` fails to unify with a function type, the construct is interpreted as indexing or slicing notation `expr1.[expr2]` and resolved in the usual way

In parsing and the untyped syntax tree, the grammar of slicing is merged with the grammar of expressions.

### Special consideration for argument expressions

Special consideration is needed for cases of the following form:

```fsharp
expr1 expr2[expr3]
```

where `expr2` is not an identifier and `[expr3]` immediately adjacent to `expr2`.  This case is problematic because of this ambiguity:

1. Existing code: The above form is allowed in F# 5.0 and before as a curried function application of `expr1` to `expr2` then `[expr3]`
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

escape (lexeme lexbuf).[1]
```

In these cases, if the user blindly replaces `.[` to `[` then we get code of the form above.
As a result, special consideration is needed for code of this form, compatibility reasons. 

* For adjacent expressions `someFunction (expr)[idx]` in non-high-precedence-application argument position, a warning is given that either a space must be inserted for application, or `.[` is required.

* No informational warning is given when `someFunction (expr).[idx]` is used in argument position

> NOTE: The following are not examples, because the first application is high-precedence application.
> ```fsharp
> opt.Split([|':'|]).[0]
> 
> FSharpType.GetTupleElements(ty).[i]
> ```

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

let v0 = f1[]             // applies f1 to a list, now emitting a warning to insert a space
let v1 = f1[1]            // applies f1 to a list, now emitting a warning to insert a space
let v2 = f2[1][2]         // applies f2 to two lists, now emitting a warning to insert a space
let v3 = f2 [1][2]        // applies f2 to two lists, now emitting a warning to insert a space
let v4 = f2 (id 1)[2]     // applies f2 to two arguments (id 1) and [2], now emits a warning to insert a space
```

Some specific examples in the wild:
```fsharp
makeReadOnlyCollection[]
seq[1;2;3;4;5]
```

#### `--langversion:default` (while feature is in preview)

```fsharp
let f1 a = ()
let v1 = f1[1]            // applies f1 to a list, now emitting a warning to insert a space
```
gives
```
a.fs(2,10,2,15): typecheck warning FS3367: The syntax 'expr1[expr2]' is now reserved for indexing and slicing (in preview). Use 'expr1.[expr2]'. If you intend multiple arguments to a function, add a space between arguments 'someFunction expr1 [expr2]'.
```
and
```fsharp
let f2 a b = ()
let v2 = f2 [1][2]         // applies f2 to two lists, now emitting a warning to insert a space
```
gives
```
a.fs(2,13,2,19): typecheck warning FS3368: The syntax '[expr1][expr2]' is now reserved for indexing/slicing (in preview) and is ambiguous when used as an argument. If you intend multiple arguments to a function, add a space between arguments 'someFunction [expr1] [expr2]'.
```
and
```fsharp
let f2 a b = ()
let v4 = f2 (id 1)[2]     // applies f2 to two arguments (id 1) and [2], now emits a warning to insert a space
```
gives
```
a.fs(2,13,2,22): typecheck warning FS3368: The syntax '(expr1)[expr2]' is now reserved for indexing/slicing (in preview) and is ambiguous when used as an argument. If you intend multiple arguments to a function, add a space between arguments 'someFunction (expr1) [expr2]'.
```


#### `--langversion:preview` (when feature is in preview)

```fsharp
let f1 a = ()
let v1 = f1[1]            // applies f1 to a list, now emitting a warning to insert a space
```
gives
```
a.fs(2,10,2,15): typecheck warning FS3365: The syntax 'expr1[expr2]' is used for indexing and slicing. Consider adding a type annotation or, if two function arguments are intended, then add a space, e.g. 'someFunction expr1 [expr2]'.
```
and
```fsharp
let f2 a b = ()
let v4 = f2 (id 1)[2]     // applies f2 to two arguments (id 1) and [2], now emits a warning to insert a space
```
gives
```
a.fs(2,13,2,22): typecheck warning FS3369: The syntax '(expr1)[expr2]' is ambiguous when used as in an argument list. If you intend indexing or slicing then you must use '(expr1).[expr2]' in argument position. If you intend multiple arguments to a function, add a space between arguments 'someFunction (expr1) [expr2]'.
```


# Compatibility

If a precise language version less than or equal to `5.0` is specified, such as `--langversion:5.0`, then warnings to insert spaces are not emitted.

If the default language version is selected, the feature is compatible but extra warnings may be emitted.

# Resolved questions

* Should a warning be emitted for neighbouring-arguments in pattern syntax, e.g.

```fsharp
let (|A1|) (ys: int list) (xs: int list) = List.append xs ys
let (|A2|) (x: int) (ys: int list) = x :: ys

let a1 (ys: int list) = ys.Length + 1
let a2 x (ys: int list) = ys.[x]

let f1 (A1[4]ys) = ys.Length   // no warning before or after
let f2 (A1[4] ys) = ys.Length  // no warning before or after
let f3 (A2 0[y]) = y + 2       // no warning before or after
let f4 (A2 (0)[y]) = y + 2     // no warning before or after

let g1 y = a1[4] + 2  // now gives warning
let g2 y = a2 0[4] + 2 // now gives warning
```
  
Resolution: not as part of this RFC.  We could consider warning on the above in the future
  
# Unresolved questions
  

