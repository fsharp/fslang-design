# F# RFC FS-1088 - if! (if-bang) computation expression keyword

The design suggestion [Add if! (if-bang) keyword to computation expressions](https://github.com/fsharp/fslang-suggestions/issues/863) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] Approved in principle
- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/863)
- [ ] [RFC Discussion](https://github.com/fsharp/fslang-design/issues/FILL-ME-IN)
- [ ] Implementation (not started)

# Summary

F# should support an ``if!`` (pronounced "if-bang") keyword that functions as an if statement operating on the result of binding to a computation expression. F# should also support an analagous ``elif!`` ("elif-bang") keyword.

# Motivation

Just as ``match!`` makes pattern match on bound CE values a little sweeter, ``if!`` makes checking the results of computation expression wrapping booleans a little more succinct, and also removes the burden of having to name a variable that is going to be used exactly once on the next line. A prime scenario is dialog boxes. For example, assuming a ``runConfirmationDialogAsync`` function is defined, the ``if!`` keyword allows the programmer to shorten the following:

```
async {
    let! shouldQuit = runConfirmationDialogAsync ("Clippy has detected that you are trying to exit without saving. Quit anyway?", Buttons.Quit, Buttons.Cancel)
    if shouldQuit then
        System.Environment.Exit 1
    else printfn "User decided not to exit"
}
```

to: 

```
async {
    if! runConfirmationDialogAsync ("Clippy has detected that you are trying to exit without saving. Quit anyway?", Buttons.Quit, Buttons.Cancel) then
        System.Environment.Exit 1
    else printfn "User decided not to exit"
}
```

Finally, if you're going to have ``if!``, ``elif!`` logically follows.

# Detailed design

The parser should be extended to accept ``if!`` anywhere both ``if`` (like ``let!``) and a computation expression construct can be used. The parser should also be extended to accept ``elif!`` wherever both ``else`` and a computation expression construct (like ``let!``) can be used.

The compiler should treat ``if!`` identically to a ``let!`` followed by an ``if``, and it should treat ``elif!`` identically to an ``else`` with a ``let!`` that only executes immediately before it.

In other words, the following code:

```
async {
    if! cexpr1 then expr1
    elif! cexpr2 then expr2
    else expr3
}
```

should be treated as if it had been written:

```
async {
    let! cexpr1Bound = cexpr
    if cexpr1Bound then expr1
    else
        let! cexpr2Bound = cexpr2
        if cexpr2 then expr2
        else expr3
}
```

It should be noted that it would not be sufficient to simply generate all the associated ``let!``s (or, as it appears in the IL, the call to ``.Bind``) for each ``elif!`` at the beginning of the whole if expression, as this would cause undesired evaluation (likely, side effects as well) even when those branches' checks aren't reached. In other words, the prior example's ``cexpr2`` must only be evaluated when it is reached.

Any combination of successive ``elif`` and ``elif!`` branches should be allowed. Additionally, an ``if!`` need not be present in order to use ``elif!``. Here are some more valid use cases:

```
async {
    if! cexpr then expr
    elif expr then expr
    elif! expr then expr
    else expr
}
```

```
async {
    if expr then expr
    elif! cexpr then expr
    else expr
}
```

# Drawbacks

``if!`` and ``elif!`` increase core language complexity.

# Alternatives

The alternative is to do nothing, and for programmers to continue manually using ``let!`` in conjunction with ``if!``. However it should be noted that ``match!`` provides a small amount of alleviation, although it is debatable whether or not this is really better:

```
match! cexpr with
| true -> expr
| false -> expr
```

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change?
    * No.
* What happens when previous versions of the F# compiler encounter this design addition as source code?
    * A pre FS-1088 compiler will generate a parse error.
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
    * A pre FS-1088 will not have any issues, because this addition only affects generated code, not any signatures.
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?
    * N/A

# Unresolved questions

None.
