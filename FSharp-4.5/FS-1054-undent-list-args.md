## F# RFC FS-1054 - permit undentation on `[` ... `]` and `[|` ... `|]`

This proposes to be less stringent in enforcing indentation rules for `[` and `[|` in one particular case, aligning with the existing treatment of `{`.

* [x] Approved in principle
* [x] Implementation: [Complete](https://github.com/Microsoft/visualfsharp/pull/4929)
* [x] Discussion: https://github.com/fsharp/fslang-design/issues/300

# Summary
[summary]: #summary

This proposes to be less stringent in enforcing indentation rules for `[` and `[|` in one particular case, aligning with the existing treatment of `{`.

F# 2.0-4.1 allow an "undentation" (i.e. relaxations to the indentation rules) for expressions delimited by `{` ... `}`:

```fsharp
Class.Method(seq {
    ...
})
Class.Method(Array.ofSeq (seq {
    ...
}))
Class.Method(arg1=expr1, arg2=expr2, seq {
    ...
})
```
Here, the first token of `...` would normally be considered to break the indentation block introduced by `(` and/or `=`. However, the undentation is specifically permitted, ignoring the `SeqBlock` indentation context introduced in these cases when
assessing tokens for offside warnings.

This case was not listed in the F# Language Specification but has been present since at least F# 2.0, and is no doubt widely used.  It has now been added.

This RFC proposes to allow the same undentations for expressions delimited by `[ ... ]` and `[| ... |]`. For example:

```fsharp
Class.Method [
    ...
]
Class.Method([|
    ...
|])
Class.Method(Array.ofList [
    ...
])
Class.Method(arg1=expr1, arg2=expr2, [
    ...
])
```

# Motivation
[motivation]: #motivation

One motivation for this RFC is the embedded DSLs used to describe dynamic views in Elmish-like programming models, such as [Elmish.Xamarin.Forms](https://github.com/fsprojects/Elmish.XamarinForms/blob/master/README.md), though there are many such examples.
For example:

```fsharp
let view model dispatch =
    Xaml.StackLayout(children=[
        Xaml.Label(text=sprintf "Grid (2x2):")
        Xaml.Grid(rowdefs= [ "*"; "*" ], coldefs=[ "*"; "*" ], children = [
            for i in 1 .. 6 do for j in 1 .. 6 -> 
                Xaml.BoxView(Colors.Red)
        ])
    ])
```

Without this RFC, this DSL becomes more awkward to format:

```fsharp
let view model dispatch =
    Xaml.StackLayout(
        children=[
            Xaml.Label(text=sprintf "Grid (2x2):")
            Xaml.Grid(rowdefs= [ "*"; "*" ], coldefs=[ "*"; "*" ], 
                children = [
                    for i in 1 .. 6 do for j in 1 .. 6 -> 
                    Xaml.BoxView(Colors.Red)
                ])
        ])
```

# Detailed design
[design]: #detailed-design

The change is modifying one line of code in the F# implementation to make the treatment of `[` and `[|` align with the treatment of `{`.

# Drawbacks
[drawbacks]: #drawbacks

Undentations must be applied with care as they can cause code to be written that is less readable. However, since the undentation is already allowed for `{ ... }` it seems reasonable to get uniformity here.

# Alternatives
[alternatives]: #alternatives

There are other proposals to make the indentation rules less strict. This is one such case, but doesn't rule out further cases in the future.

# Compatibility
[compatibility]: #compatibility

This is fully backwards-compatible, simply giving fewer warnings.

# Unresolved questions
[unresolved]: #unresolved-questions

None.
