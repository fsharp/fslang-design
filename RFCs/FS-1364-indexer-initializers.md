# F# RFC FS-1364 - Indexer initializers

The design suggestion [Indexer, collection, event and sublevel initialization](https://github.com/fsharp/fslang-suggestions/issues/969) has been marked "approved in principle".
This RFC covers the indexer part of the suggestion. The collection, event and sublevel parts are out of scope.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/969)
- [x] [Approved in principle](https://github.com/fsharp/fslang-suggestions/issues/969#issuecomment-764638387)
- [ ] Implementation (not started)
- [ ] Discussion

# Summary

A method call or a constructor call can set indexer items on its result, as it can set properties today:

```fsharp
let ages = Dictionary<string, int>(StringComparer.Ordinal, ["Ann"] <- 31, ["Bob"] <- 27)
```

The feature is the F# equivalent of the C# index initializer `new T(args) { [key] = value }`.

# Motivation

F# can set properties of a call result in the argument list: `Button(Text = "OK")`. It cannot set indexer items there.
The code must bind the object, assign each item, and return the object. Each nested object needs its own binding:

```fsharp
let forecast =
    let cold = JsonObject()
    cold["High"] <- 20
    cold["Low"] <- -10
    let o = JsonObject()
    o["Summary"] <- "Hot"
    o["Cold"] <- cold
    o
```

With indexer initializers, the code has the shape of the data:

```fsharp
let forecast =
    JsonObject(
        ["Summary"] <- "Hot",
        ["Cold"] <- JsonObject(["High"] <- 20, ["Low"] <- -10))
```

The workarounds in the suggestion pass `KeyValuePair` values to a constructor, or use an extension property for each type.
Both allocate a sequence, and many types have no such constructor.

# Detailed design

## Form

In the argument list of a method application, an argument of the form `[idx] <- value` is an _indexer initializer_.
`idx` is one expression. For an indexer with more than one index parameter, `idx` is a tuple: `[i, j] <- value`.

1. Indexer initializers MUST come after all unnamed arguments. They MAY come before, after or between named arguments.
2. The value ends at the next comma at the top level of the argument list, as the value of a named argument does. A tuple value MUST be in parentheses: `["k"] <- (1, 2)`.
3. An initializer in parentheses, `([idx] <- value)`, is an unnamed argument. Today `(name = value)` is an unnamed argument in the same way.
4. The brackets MUST contain exactly one expression. `[]`, `[a; b]`, a range and a list comprehension are errors.
5. Indexer initializers are valid in each method application that accepts named property setters: method calls, constructor calls, `new`, object expressions and `inherit`. They are not valid in custom attributes.

## Meaning

Let `r` be the result of the call. If the result type is a value type, `r` is a mutable copy, as for named property setters.
An initializer `[idx] <- value` means the same as the assignment `r[idx] <- value` ([Assignment Expressions](https://github.com/fsharp/fslang-spec/blob/main/spec/expressions.md#assignment-expressions)).
The current rules for this assignment select the setter: indexer overloads, extension indexers, arrays and type-directed conversions of `value`.

```fsharp
T(x, ["a"] <- f (), P = g ())
// means
let r = T(x) in r["a"] <- f (); r.P <- g (); r
```

1. Indexer initializers MUST NOT take part in the overload resolution of the method. The compiler checks them after it selects the method. Thus the indexer can supply type arguments of the result: `Dictionary(["a"] <- 1)` has the type `Dictionary<string, int>`.
2. The call runs first. Then the property setters and the indexer initializers run in source order. Each initializer evaluates `idx`, then `value`, and then calls the setter.
3. An init-only indexer setter is valid only when the method is a constructor. The same rule applies to init-only properties today.

## Interactions

- Quotations need no new node. They contain the elaborated assignment `r[idx] <- value`, as they contain a `PropertySet` for a named property setter.
- SRTP: there is no new constraint. As for `r[idx] <- value`, the indexer lookup needs a known result type.
- Type providers: provided methods and provided indexers work as in `r[idx] <- value`.
- [FS-1342](https://github.com/fsharp/fslang-design/pull/838), if accepted: the value is an argument of the indexer setter, so it gets a contextual type. In `Dictionary<string, byte[]>(["a"] <- [1; 2])`, the value is a `byte[]`.

# Changes to the F# spec

There is no grammar change: `[idx] <- expr` is already an assignment expression.
[§14.4 Method Application Resolution](https://github.com/fsharp/fslang-spec/blob/main/spec/inference-procedures.md#method-application-resolution), step 2:

```diff
     - For each argument:
         - If `arg` is a binary expression of the form `name=expr`, it is a named actual argument.
+        - If `arg` has the form `[idx] <- expr`, it is an _indexer initializer_. If `expr` is a
+            syntactic tuple `expr1, ..., exprN`, then `expr1` is the value of the initializer, and
+            `expr2, ..., exprN` are the next arguments.
         - Otherwise, `arg` is an unnamed actual argument.
 
-    If there are no named actual arguments, and `M` has only one candidate method, which accepts
+    If there are no named actual arguments and no indexer initializers, and `M` has only one candidate method, which accepts
     only one required argument, ignore the decomposition of `arg` to tuple form. Instead, `arg` itself is
     the only named actual argument.
 
-    All named arguments must appear after all unnamed arguments.
+    All named arguments and indexer initializers must appear after all unnamed arguments.
+    Indexer initializers do not take part in steps 3 to 8.
 
     Examples:
     - `x.M(1, 2)` has two unnamed actual arguments.
     - `x.M(1, y = 2)` has one unnamed actual argument and one named actual argument.
     - `x.M(1, (y = 2))` has two unnamed actual arguments.
+    - `x.M(1, [2] <- 3)` has one unnamed actual argument and one indexer initializer.
```

Step 10:

```diff
-    - For each `NamedActualArgs` whose target is a settable property or field, assign the value into
-    the property.
+    - For each `NamedActualArgs` whose target is a settable property or field, and for each indexer
+    initializer `[idx] <- expr`, in source order: assign the value into the property or field, or
+    check and elaborate the assignment expression `v.[idx] <- expr`, where `v` is the result of the call.
```

# Drawbacks

- One argument list uses `=` for properties and `<-` for indexer items.
- `["k"] <- 1, 2` groups differently from the statement `x <- 1, 2`, which assigns a tuple.

# Alternatives

- `[idx] = value`, as in C# and as the approval comment suggests. Today `T([1] = xs)` compiles as a list comparison, so this form breaks code. A fallback to the old meaning makes the meaning depend on types.
- `.[idx] = value`, from a comment on the suggestion. It does not break code, but it brings back the dot form that [FS-1110](https://github.com/fsharp/fslang-design/blob/main/FSharp-6.0/FS-1110-index-syntax.md) replaced.
- A dictionary literal on top of FS-1342, as in the C# [dictionary expressions](https://github.com/dotnet/csharplang/blob/main/proposals/dictionary-expressions.md) proposal `[k: v]`. It gets its type from the context. It cannot set items on the result of a call with arguments. It can come later, and both features can exist together.
- The workarounds in the Motivation section.

# Prior art

C# has [index initializers](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/object-and-collection-initializers) since C# 6: `new Dictionary<string, int> { ["a"] = 1 }`.
C# runs the initializers in source order after the constructor, as this RFC does.

# Compatibility

- This is not a breaking change. Today a top-level argument `[idx] <- expr` never compiles, because a list is not an assignment target. The errors are FS0001, or the internal error FS0971 in code generation when the types agree.
- Earlier compilers reject the new form. With an earlier language version, the compiler reports that the feature is not available.
- There is no new metadata. Compiled code contains ordinary setter calls, so earlier compilers can consume it.

# Interop

Other .NET languages see ordinary setter calls. No planned C# feature interacts with this feature.

# Pragmatics

## Diagnostics

1. An initializer before an unnamed argument: FS0691, with its message extended to indexer initializers.
2. Brackets that do not contain exactly one expression: a new error that shows the form `[index] <- value`.
3. No setter for `r[idx] <- value`: the error of that assignment, at the brackets.
4. An initializer in a custom attribute: a new error.
5. An init-only indexer setter outside a constructor call: the current init-only error.

## Tooling

- The compiler records the use of the indexer setter at the brackets. Quick Info, Go To Definition, Find All References and semantic colors work as for `r[idx] <- value`.
- Signature help SHOULD treat an initializer as a named argument.
- Error recovery: an incomplete initializer, such as `["k"] <-`, SHOULD NOT stop the check of the other arguments.
- Debugging: breakpoints and steps work as for named property setters.

## Performance

Existing code does not change. An initializer compiles to the same code as the assignment `r[idx] <- value`.

## Scaling

- Initializers in one call, hand-written: up to 100.
- Initializers in one call, generated: up to 10,000.

The parser puts each initializer inside the value of the previous initializer. The compiler MUST process this nesting in linear time, without a stack overflow.

## Culture-aware formatting/parsing

Not applicable.

# Unresolved questions

1. `<-` or `=`? The approval comment prefers `=` if possible. This RFC uses `<-`, because `=` breaks code (see Alternatives).
2. The FCS syntax tree: keep the nested `SynExpr.Set` shape without an API change, or add a flat node for tools?
