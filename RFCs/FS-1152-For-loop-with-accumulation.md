# F# RFC FS-1152 - For loop with accumulation

The design suggestion ["for-with" syntactic sugar for folds](https://github.com/fsharp/fslang-suggestions/issues/1362) has not yet been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1362)
- [ ] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/FILL-ME-IN)

# Summary

A new syntax for `for`-loops that allow accumulation is introduced.
```
for <pat> = <expr> with <pat> in <expr> do
    <expr>
```
The value of the loop body is used to update the accumulator which is returned in the end.

# Motivation

Currently, to do this, we need an additional mutable variable. For example,
```fs
let mutable sentence = "" // mutable accumulator
for word in ["Hello", " ", "World", "!"] do
    sentence <- sentence + word
printfn "%s" sentence
```

However, the mutable variable leaks outside the loop body, which is undesirable.
Moreover, the accumulator must be mutable - which is against functional immutable semantics.

It is proposed that the accumulator be embeddable into the loop itself:
```fs
for sentence = "" with word in ["Hello", " ", "World", "!"] do
    sentence + word
|> printfn "%s"
```
The return value of the loop body updates the accumulator.
This offers a desirable middle ground between `for` loops that must only have side-effects (returning `unit`)
and purely functional `fold`s that must be in lambda form.

Meanwhile, folds are hard to understand and the body cannot be use computation expression functionality.
```fs
["Hello", " ", "World", "!"]
|> Seq.fold (fun sentence -> sentence + word (*loop body, and yet we lose the ability to do let!*)) "" // initial state is placed last??
|> printfn "%s"
```
which suggests a missed opportunity to make them more familiar to people who know `for` loops.

# Detailed design

For this syntax

```fs
for <pat1> = <expr1> with <pat2> in <expr2> do
    <exprs>
    <expr3>
```

it undergoes a simple syntactical expansion to

```fs
let mutable <accum> = <expr1>
for <pat2> in <expr2> do
    let <pat1> = <accum>
    <exprs>
    <accum> <- <expr3>
<accum>
```
where `<accum>` is a compiler-generated accumulation variable that cannot be used outside the loop,
and `<exprs>` match any expressions inside a sequence expression before the final expression in the loop body,
or any `if` and `match` and `try` permitting computation expression syntax inside.

If the final expression `<expr3>` is contained inside `if` or `match` or `try`, then each branch must return the same value and `<expr3>` is searched recursively with the final expression of each branch for `<accum> <-` application.

The result of the syntactical translation shows that `<accum>` is the value of this loop. It is also the accumulator which gets updated each iteration.

Note that `<exprs>` are subject to usual treatment by computation expressions including application of implicit yields,
which is different from the final expression `<expr3>`.

Therefore, this may happen:
```fs
let result = [
    for sentence = "" with word in ["Hello", " ", "World", "!"] do
        sentence
        sentence + word
    |> printfn "%s"
]
// expands to
let result = [
    let mutable <accum> = ""
    for word in ["Hello", " ", "World", "!"] do
        sentence // Implicit yield here
        <accum> <- sentence + word // But not for the final expression of the loop body
    <accum>
    |> printfn "%s"
]
```

### Unresolved question

There are a few choices that can be made for `<exprs>` -
- Allow implicit yields before the final value
- Warn with implicit yield behaviour
- Warn with discard behaviour
- Error because it's ambiguous

Allowing implicit yields before the final value might not be the best choice here.

Warning with discard is also a valid choice when inside this kind of for loop. Explicit `yield`s are always available if needed.

# Drawbacks

It's another syntax to learn.

# Alternatives

Doing nothing - we miss an opportunity to bridge regular for loops with lambda folds.

There is a potential extension to folding across multiple sequences with the `and` keyword in the future -
```fs
for s, p = 0, 0 with t in ts and u in us do
    s + t, p + u
```

Also, there exists a [computation expression implementation](https://gist.github.com/brianrourkeboll/830408adf29fa35c2d027178b9f08e3c) that can mimic this syntax -
```fs
let sum xs = fold 0 { for x in xs -> (+) x } // variation 1
let sum xs = fold 0 { for acc, x in xs -> acc + x } // variation 2
let sum xs = fold { for acc, x in 0, xs -> acc + x } // variation 3
```
But this is not orthogonal to an existing computation expression context unlike the for loop which allows `let!` inside that refer to the outer context. Moreover, error messages for overloaded computation expression methods are hard to understand, and computation expressions are [notoriously difficult to debug](https://github.com/dotnet/fsharp/issues/13342). Computation expressions also show a heavily different syntax compared to for loops and folds which add hinderance to understanding.

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change? No
* What happens when previous versions of the F# compiler encounter this design addition as source code? Reject syntax.
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries? Treat them the same as for loops with mutable accumulator.
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct? Not applicable.

# Pragmatics

## Diagnostics

Just what you expect from a regular for loop.

## Tooling

Please list the reasonable expectations for tooling for this feature, including any of these:

* Debugging
  * Breakpoints/stepping
  * Expression evaluator
  * Data displays for locals and hover tips
* Auto-complete
* Tooltips
* Navigation and Go To Definition
* Error recovery (wrong, incomplete code)
* Colorization
* Brace/parenthesis matching

Just what you expect from a regular for loop.
