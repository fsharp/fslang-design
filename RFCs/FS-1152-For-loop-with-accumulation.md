# F# RFC FS-1152 - Fold loops

The design suggestion ["for-with" syntactic sugar for folds](https://github.com/fsharp/fslang-suggestions/issues/1362) has not yet been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1362)
- [ ] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [x] [Discussion](https://github.com/fsharp/fslang-design/discussions/808)

# Summary

A new syntax for `for`-loops that allow accumulation is introduced that is intuitive, initially teachable, and without too much unexpected behavior (all 3 phrases are just [Principle of Least Surprise](https://en.wikipedia.org/wiki/Principle_of_least_astonishment)). It is easy to document and teach to new users.
```fs
for <pat> = <expr> with <pat> in <expr> do
    <expr>
```
The value of the loop body is used to update the accumulator which is returned in the end.

# Motivation

Currently, to do this, we need an additional mutable variable. For example,
```fs
let mutable sentence = "" // mutable accumulator
for word in ["Hello"; " "; "World"; "!"] do
    sentence <- sentence + word
printfn "%s" sentence
```

However, the mutable variable leaks outside the loop body, which is undesirable.
Moreover, the accumulator must be mutable - which is against functional immutable semantics.

It is proposed that the accumulator be embeddable into the loop itself:
```fs
for sentence = "" with word in ["Hello"; " "; "World"; "!"] do
    sentence + word
|> printfn "%s"
```
The return value of the loop body updates the accumulator.
This offers a desirable middle ground between `for` loops that must only have side-effects (returning `unit`)
and purely functional `fold`s that must be in lambda form.

Meanwhile, folds are hard to understand.
```fs
["Hello"; " "; "World"; "!"]
|> Seq.fold (fun sentence -> sentence + word (*loop body but shown as a lambda!*)) "" // initial state is placed last??
|> printfn "%s"
```
which suggests a missed opportunity to make them more familiar to people who know `for` loops.

Some may also suggest that recursive functions may show the logic more clearly than a `fold`.

```fs
let rec processWords words =
    match words with
    | [] -> "" // Empty list means empty string,
    | word::words -> word + processWords words // and we do append for each element!
["Hello"; " "; "World"; "!"]
|> processWords
|> printfn "%s"
```

Whoops, this function isn't tail recursive! If you run this for long inputs, it blows up unlike the loop.
The function needs to be rewritten using the accumulator parameter.

```fs
let rec [<TailCall>] processWords accum words = // [<TailCall>] with no warning to verify we have a tail call
    match words with
    | [] -> accum // Empty list means accumulator,
    | word::words -> processWords (accum + word) words // and we do append on the accumulator!
["Hello"; " "; "World"; "!"]
|> processWords "" // Accumulator is applied away from the function or yet another wrapper function needs to be defined verbosely...
|> printfn "%s"
```

A recursive function is just harder to write correctly and is more verbose.

## Doesn't FSharp.Core already provide better functions for this example?

The benefits of this syntax start to compound when there are nested folds and folds over tuples.

```fs
// Example MVU model
type Item = { Name: string; Count: int }
type Category = { Name: string; Items: Item list }
type Model = { Categories: Category list }

// Compute (total items, total count) in all categories
// Current
let stats model =
    model.Categories
    |> List.fold (fun (totalItems, totalCount) category ->
        let items, count =
            category.Items
            |> List.fold (fun (items, count) item ->
                items + 1, count + item.Count // Fold over inner items
            ) (0, 0)
        totalItems + items, totalCount + count
    ) (0, 0) // Initial state
// Proposed
let stats' model =
    for totalItems, totalCount = 0, 0 with category in model.Categories do
        let items, count =
            for items, count = 0, 0 with item in category.Items do
                items + 1, count + item.Count
        totalItems + items, totalCount + count
```

In a regular `fold`, it's very hard just to get the white space alignment and closing parentheses right when you need a fold within a fold.
Folding over tuples with a lambda also becomes pretty confusing very quickly.
Using the new fold syntax, this becomes much easier to write and understand.

## How about `||>`?

```fs
// Current
let stats'' model = // ||>
    ((0, 0), model.Categories) // ugh - nested tuples
    ||> List.fold (fun (totalItems, totalCount) category ->
        let categoryStats =
            ((0, 0), category.Items) // ugh - nested tuples
            ||> List.fold (fun (items, count) item ->
                (items + 1, count + item.Count) // Fold over inner items
            ) 
        (totalItems + fst categoryStats, totalCount + snd categoryStats)
    )
```

`||>`s make it hard to thread the pair through the lambda without screwing up the types.

Let's look at another example:

```fs
// Current
let effects, model =
    Seq.fold (fun (effects, model) item ->
        let effect, model = Model.action item model
        let model = Model.action2 model
        effect :: effects, model)
        ([], model)
        items
let effects', model' = // ||>
    (([], model), items) // ugh - nested tuples
    ||> Seq.fold (fun (effects, model) item ->
        let effect, model = Model.action item model
        let model = Model.action2 model
        effect :: effects, model)
```

It is easy to accidentally do `(model, effect :: effects)` or `(model, [])` - especially for people new to functional programming, tupling like this is hard to get right.
This even happens for experienced F# programmers. 

If the user doesn't get them right, the problem is figuring out what they got wrong from the type errors.

People also often get the parameter order mixed up, such as doing `items ([], model)` instead of `([], model) items`.
There are far fewer likely points of failure using the `for` loop with accumulation.

```fs
// Proposed
let effects, model =
    for effects, model = [], model with item in items do
        let effect, model = Model.action item model
        let model = Model.action2 model
        effect :: effects, model
```

## Computation expressions already provide a similar syntax, right?

Indeed there exists a [computation expression implementation](https://gist.github.com/brianrourkeboll/830408adf29fa35c2d027178b9f08e3c) that can mimic this syntax -
```fs
let sum xs = fold 0 { for x in xs -> (+) x } // variation 1
let sum xs = fold 0 { for acc, x in xs -> acc + x } // variation 2
let sum xs = fold { for acc, x in 0, xs -> acc + x } // variation 3
```
But this is not orthogonal to an existing computation expression context unlike the for loop which allows `let!` inside that refer to the outer context. Moreover, error messages for overloaded computation expression methods are hard to understand, and computation expressions are [notoriously difficult to debug](https://github.com/dotnet/fsharp/issues/13342). Computation expressions also show a heavily different syntax compared to for loops and folds which add hinderance to understanding.

## Summary

The fold loop is superior to `mutable` variables with imperative `for` loops because:
- More succinct from elision of accumulator variable
- Better scoping without variable leakage outside loop
- Preservation of functional immutable semantics lost from a mutable variable

The fold loop is superior to `fold` calls because:
- Potential orthogonality to outer computation expression contexts
- Much easier to understand with loop syntax
- Much easier to attain whitespace alignment and omission of the closing parenthesis. When indentation is wrong, you also have to consider the parens as a possible cause, vice versa.
- Much easier to place loop parameters correctly, fewer points of failure compared to ordering fold arguments / types
- More precise error messages from type inference especially for newcomers
- More logical cohesion with the accumulator starting value located next to the looping logic

The fold loop is superior to `rec`ursive functions because:
- Potential orthogonality to outer computation expression contexts
- Much easier to write correctly without the need to think about tail recursion
- Much more succinct without requiring the recursive function identifier or a wrapper function to hide the accumulator parameter
- More logical cohesion with the accumulator starting value located next to the looping logic

The fold loop is superior to computation expression imitations because:
- Potential orthogonality to outer computation expression contexts
- Universiality without needing to learn parameter placements from the concrete computation expression implementation localized in each project
- Availability by default for newcomers to learn easily
- More precise error messages compared to overloaded computation expression methods especially for newcomers
- Much easier to perform debugging especially for step debugging
- Preserves syntactical familiarity with other loops without invoking the need for braces

The only caveat is the unfamiliarity with a loop that has a return value where existing loops must return unit, but over time, this can be overcome with stressing the existence of an accumulator vs not having one.

# Detailed design

`for` is used as the loop keyword because the fold loop behaves identical to the `for` loop with accumulation. Ideally, `fold` would be usable as the loop keyword, but it obviously clashes with existing uses of it as an identifier.

## Alternative 1 - Loop body is a value without CE context

For this syntax

```fs
for <pat1> = <expr1> with <pat2> in <expr2> do
    <expr3>
```

it undergoes a simple syntactical expansion to

```fs
let mutable <accum> = <expr1>
for <pat2> in <expr2> do
    let <pat1> = <accum>
    <accum> <- <expr3>
<accum>
```
where `<accum>` is a compiler-generated accumulation variable that cannot be used outside the loop. The entire loop body `<expr3>` is assigned to `<accum>`.

The result of the syntactical translation shows that `<accum>` is the value of this loop. It is also the accumulator which gets updated each iteration.

Note that since the loop body is assigned to the accumulator, the loop body is not a computation expression context.
```fs
let result = [
    for sentence = "" with word in ["Hello"; " "; "World"; "!"] do
        sentence // this is NOT an implicit yield. This warns with a discard behavior.
        // yield sentence // Error: CE syntax cannot be used here.
        sentence + word
    |> printfn "%s" // |> can be used on the loop result
]
```

The reason is as follows:

```fs
let result = [
    printfn "%s" (
        ":D" // this is also NOT an implicit yield as it is a subexpression.
        for sentence = "" with word in ["Hello"; " "; "World"; "!"] do
            sentence // Warns with discard
            // yield sentence // Error: CE syntax cannot be used here.
            sentence + word
    )
]
```

## Alternative 2 - Loop body is a CE context with final expression updating the accumulator

The above alternative loses expressiveness compared to for loops with mutable accumulators. For example, `scan`s cannot be written with the fold loop.

```fs
let scanned =
  [ for s = 0 with t in ts do
      yield s + t
      s + t
  ]
```

However, interactions with CEs start to get complicated.

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
or any `if` and `match` and `try`-`with` permitting computation expression syntax inside.

If the final expression `<expr3>` is contained inside `if` or `match` or `try`-`with`, then each branch must return the same value and `<expr3>` is searched recursively with the final expression of each branch for `<accum> <-` application.

The result of the syntactical translation shows that `<accum>` is the value of this loop. It is also the accumulator which gets updated each iteration.

### Alternative 2.1 - Implicit yields in loop body

Note that `<exprs>` are subject to usual treatment by computation expressions including application of implicit yields,
which is different from the final expression `<expr3>`.

Therefore, this may happen:
```fs
let result = [
    for sentence = "" with word in ["Hello"; " "; "World"; "!"] do
        sentence
        sentence + word
    |> printfn "%s"
]
// expands to
let result = [
    let mutable <accum> = ""
    for word in ["Hello"; " "; "World"; "!"] do
        sentence // Implicit yield here
        <accum> <- sentence + word // But not for the final expression of the loop body
    <accum> // The fact that the accumulator value is usable is unrelated to the above loop!
    |> printfn "%s"
]
```

### Alternative 2.2 - Warn with discard in loop body

It might be the case that two seemingly similar lines behaving distinctly is too dangerous.

```fs
// Alternative 2.1
let q = seq { for i in 1..10 do s + i } // int seq, 10 values
let w = seq { for s = 0 with i in 1..10 do s + i } // int seq, but now only 1 value is yielded because it's collected to accumulator

let q1 = seq { for i in 1..10 do s + i; s + i } // int seq, 20 values
let w1 = seq { for s = 0 with i in 1..10 do s + i; s + i } // int seq, 11 values now?
```

To resolve this, the syntactical translation of `<exprs>` would automatically insert `; ()` after any expression not bound with a CE keyword or `let` and `use` (or equivalent implementation that implements warn with discard behaviour by default). Meanwhile, explicit usages of the `yield` keyword will not affect implicit yield availability outside of the fold loop.

```fs
// Alternative 2.2
let q = seq { for i in 1..10 do s + i } // int seq, 10 values
let w = seq { for s = 0 with i in 1..10 do s + i } // int seq, but now only 1 value is yielded because it's collected to accumulator

let q1 = seq { for i in 1..10 do s + i; s + i } // int seq, 20 values
let w1 = seq { for s = 0 with i in 1..10 do s + i; s + i } // int seq, 1 value, warn at first "s + i" with discard by default
let w2 = seq { for s = 0 with i in 1..10 do yield s + i; s + i } // int seq, 11 values
```

### Alternative 2.3 - Warn with implicit yield in loop body

Above but instead of discard by default, it's yield by default with warning about ambiguity. It might make more sense to align with the default behaviour in CEs rather than outside? The auto-insertion would be `yield` before instead of `; ()` after.

### Alternative 2.4 - Ambiguity error for unbound expressions

Or it might be best to just error about the ambiguity instead of defaulting to any behaviour after all.

## Alternative A - Loop itself returns the value

All above assumes that the loop itself returns the value. This alternative is more preferable if we assume that the fold accumulated value doesn't need further processing (returning for function) or can easily be piped.

## Alternative B - Loop bindings exposed to code below

It might also be the case that the same deconstruction is needed for the accumulator value after processing after all. Moreover, the fact that a pipe argument can be a CE context (shown in Alternative 2.1) is also inconsistent with other kinds of expressions that lose compuation expression context when piped!

```fs
// Alternative A
let effects, model = // This seems repetitive with the loop accumulator...
    for effects, model = [], model with item in items do
        let effect, model = Model.action item model
        let model = Model.action2 model
        effect :: effects, model
printfn $"{effects}"
printfn $"{model}"
```

It might be more ergonomic to just leak the pattern bindings in the accumulator below the loop for further processing of accumulated results.

```fs
// Alternative B
for effects, model = [], model with item in items do
    let effect, model = Model.action item model
    let model = Model.action2 model
    effect :: effects, model
printfn $"{effects}" // refers to "effects" of the above fold loop
printfn $"{model}"
```

The loop itself would return `unit` just like a regular `for` loop does - eliminating the unfamiliarity with a loop that has a return value. However, the tradeoff is that exposing loop bindings like this is even more unfamiliar than the loop modification itself.

The syntactical translation would be modified as follows. For

```fs
for <pat1> = <expr1> with <pat2> in <expr2> do
    <exprs>
    <expr3>
```

it undergoes a simple syntactical expansion to

### Alternative Ba - Loop returns unit

```fs
let mutable <accum> = <expr1>
for <pat2> in <expr2> do
    let <pat1> = <accum>
    <exprs>
    <accum> <- <expr3>
let <pat1> = <accum> // <accum> deconstructed with <pat1> again
() // loop returns unit if isolated
```

This alternative maintains the familiarity with loops returning unit.

### Alternative Bb - Loop requires a following expression and cannot be isolated
```fs
let mutable <accum> = <expr1>
for <pat2> in <expr2> do
    let <pat1> = <accum>
    <exprs>
    <accum> <- <expr3>
let <pat1> = <accum> // <accum> deconstructed with <pat1> again
// loop requires an expression following it!
```

For this alternative, an error message will be raised like `let`s do.
```
error FS0588: The block following this 'for' with accumulation is unfinished. Every code block is an expression and must have a result. 'for' with accumulation cannot be the final code element in a block. Consider giving this block an explicit result.
```
This alternative maintains the robustness that `let`s requiring following expressions do.

## Summary

There exists the combination of these alternatives to choose from:
(A or Ba or Bb) and (1 or 2.1 or 2.2 or 2.3 or 2.4)

# Drawbacks

It's another syntax to learn.

# Alternatives

Doing nothing - we miss an opportunity to bridge regular for loops with lambda folds.

There is a potential extension to folding across multiple sequences with the `and` keyword in the future -
```fs
// n-dimensional Euclidian distance between A and B
let pointACoordinates = [ 1.5; 2.; -3.4; -1.2 ]
let pointBCoordinates = [ -3.1; 3.1; 1.; -0.2 ]

let euclidianDistance =
    for acc = 0. with an in pointACoordinates and bn in pointBCoordinates do
        acc + (an - bn) ** 2.
    |> sqrt
```

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
