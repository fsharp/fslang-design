# F# RFC FS-1072 - Support "without"ers for Anonymous Records

The design suggestion [Support "without" for Anonymous Records](https://github.com/fsharp/fslang-suggestions/issues/762) has been marked "approved in principle".

This RFC covers the detailed proposal.

# Summary

F# anonymous records currently allow creating new anonymous records via the `with` keyword. Unlike records however, it is possible to construct a new anonymous record with additional labels.

This proposal is the reverse: by using a "with"-like keyword, construct a new anonymous record with _less_ labels:

```fsharp
let data = {| X = 1; Y = "abc"; Z = 6 |}
// val data: {| X: int; Y: string; Z: int |}

let subset = {| data with- Z |}
// val subset: {| X: int; Y: string |}
```

# Motivation

A "without" expression simplifies generating a subset of an anonymous record or record type, especially if that subset is nearly identical.

Here is an example of constructing such a subset today:

```fsharp
type Example = { FirstName: string; LastName: string; Age: int }

let example = { FirstName = "Phillip"; LastName = "Carter"; Age = 28 }

let getSubset (example: Example) =
    {| FirstName = example.FirstName; LastName = example.LastName |}
```

Imagine if `Example` had 20 fields, and you only wanted to use 19 of them from an instance of that `Example` type. This would be quite a lot of typing!

This form of programming is often used in data manipulation scripts with Python via the Pandas library. It is important in this kind of domain to be able to succinctly "drop a column" of information. A "without" expression could offer similar benefits.

# Detailed design

## Syntax

The anonymous record copy-and-update expression is extended to support a "without"-style form:

```fsharp
let data = {| X = 1; Y = "abc"; Z = 6 |}
// val data: {| X: int; Y: string; Z: int |}

let subset = {| data with- Z |}
// val subset: {| X: int; Y: string |}
```

There are two distinct pieces to it:

### The `with-` keyword

To accomplish this, a new keyword is added: `with-`. The meaning of `a with- [Labels]` is as follows:

_Construct a new anonymous record that is equivalent to `a`, but without `[Labels]`._

Using the previous example, `data with- Z` means, "Construct a new anonymous record equivalent to `data`, but without the `Z` label".

### Specifying labels to remove

The second part of this expression form is a semicolon-delimited list of labels to exclude.

```fsharp
let data = {| X = 1; Y = "abc"; Z = 6; W = 0 |}
// val data: {| W: int; X: int; Y: string; Z: int |}

let subset = {| data with- Z; W |}
// val subset: {| X: int; Y: string |}
```

Excluding 1 label does not require specifying a semicolon. Excluding 2 more more labels does.

## Interaction with `with`

It is possible to mix `with` and `with-` in a single expression:

```fsharp
let data = {| X = 1; Y = "abc"; Z = 6; W = 0 |}
// val data: {| W: int; X: int; Y: string; Z: int |}

let subset = {| data with- Z; W
                     with A = 3 |}
// val subset: {| A: int; X: int; Y: string |}
```

That is to say, it is possible to exclude at least 1 label and construct at least 1 new label for the result anonymous record.

Order matters in one case: using a `with-` for a label added by a `with`. This is an error, since you cannot subtract something that does not exist. If someone wishes to drop a label that they just added, they should specify `with` first.

Otherwise, order does not matter. A `with-` can be specified before or after a `with`.

It is possible with `with` and `with-` a label with the same name. The result will be that it does not exist in the result anonymous record. This is akin to allowing you to write `let x = 1 + 1 - 1`.

## Interaction with tools

The `with-` keyword appears in completion list, just like `with`.

When typing a label to exclude following a `with`, possible label names to exclude appear in the completion list.

Labels can have symbol functions invoked on them (e.g., rename) just as with normal records in a `with` expression.

# Drawbacks

Giving this sort of functionality to anonymous records sets them further apart from named records. Because named records require that the result of a copy-and-update expression is the same as the record being copied, this cannot be done for named records unless a "record algebra" is defined and includes the ability to do some form of record subtyping.

This runs the risk of developers never "nominalizing" their anonymous records in the future, and nominalization is a stated goal for anonymous records.

# Alternatives

What other designs have been considered? What is the impact of not doing this?

## Syntax

The `without` keyword was also considered. However, this was determined to be quite tricky to implement, as it would be a contextual keyword. Expressions like this would have to work:

```fsharp
let without = {| with="or"; without="you" |}
// val without: {| with: string; without: string |}

let with = {| without without without |}
// val with: {| with: string |}
```

## Disallow mixing with `with`

It was considered to only allow _either_ `with`-style or `with-`-style copy-and-update expressions. However, the desire to express a subset of a record and add or update a label to that subset in a single expression is likely to be too high to have this restriction.

# Compatibility

* Is this a breaking change?

No.

* What happens when previous versions of the F# compiler encounter this design addition as source code?

They fail to compile, as with any new language feature.

* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?

They will just see a type like they do with anonymous records.

* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?

N/A

# Unresolved questions

N/A
