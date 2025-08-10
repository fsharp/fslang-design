# F# RFC FS-1152 - Fold loops

The design suggestion ["for-with" syntactic sugar for folds](https://github.com/fsharp/fslang-suggestions/issues/1362) has not yet been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1362)
- [ ] Approved in principle
- [ ] [Implementation](https://github.com/dotnet/fsharp/pull/FILL-ME-IN)
- [x] [Discussion](https://github.com/fsharp/fslang-design/discussions/808)

# Summary

Fold is a fundamental operation in pure functional programming. It deserves a better syntax that what fold functions currently provide. To solve this, a new syntax for `for`-loops that allow accumulation is introduced that is intuitive, teachable to new users, easily documentable and without too much unexpected behavior (all 4 phrases are just [Principle of Least Surprise](https://en.wikipedia.org/wiki/Principle_of_least_astonishment)). It also derives and provides for useful semantics (a reasonably learned F# programmer will reliably reach for in practice, enabling the developers to make their lives easier). It is also rigorous (other features rely on this being sensible and future features may build on it).
```fs
for <pat> in <expr> with <pat> = <expr> do
    <expr>
```
Extending the `for <pat> in <expr> do` syntax, a new optional clause with an accumulator initializer, denoted by `with`, is introduced. The presence of the `with` clause enables the value of the loop body to update the accumulator (placed after `with`), which is the loop return value.

# Motivation

Let's start with how this is used in practice - the summarization of a sequence into a value, aka the fold operation, compared across different language features.

```fs
// Current - fold
let effects, model =
    Seq.fold (fun (effects, model) item ->
        let effect, model = Model.action item model // Pure function
        let model = Model.action2 model // Pure function
        effect :: effects, model)
        ([], model) // ugh - hard to get order and parentheses right
        items
// Current - ||> fold
let effects, model =
    (([], model), items) // ugh - nested tuples and hard to get order and parentheses right
    ||> Seq.fold (fun (effects, model) item ->
        let effect, model = Model.action item model // Pure function
        let model = Model.action2 model // Pure function
        effect :: effects, model)
// Current - recursive function
let effects, model =
    let rec [<TailCall>] processItems (effects, model) items = // boilerplate, hard to write correctly
        match items with // boilerplate and specific to lists - can't do this for other collections
        | [] -> effects, model // boilerplate
        | item::items -> // boilerplate
            let effect, model = Model.action item model // Pure function
            let model = Model.action2 model // Pure function
            processItems (effect :: effects, model) items
    processItems ([], model) items
// Current - mutable accumulator
let effects, model =
    let mutable accum = [], model // hmm, a mutable variable is required even in an architecture of pure functions and it needs the most boilerplate
    for item in items do
        let effects, model = accum // boilerplate
        let effect, model = Model.action item model // Pure function
        let model = Model.action2 model // Pure function
        accum <- effect :: effects, model
    accum // boilerplate
// Proposed fold loop 
let effects, model =
    for item in items with effects, model = [], model do // look at how simple the same code can become!
        let effect, model = Model.action item model // Pure function
        let model = Model.action2 model // Pure function
        effect :: effects, model
```
Notice that for `fold`s, it is easy to accidentally do `(model, effect :: effects)` or `(model, [])` - especially for people new to functional programming, tupling like this is hard to get right.
This even happens for experienced F# programmers. If the user doesn't get them right, the problem is figuring out what they got wrong from the type errors.
People also often get the parameter order mixed up, such as doing `items ([], model)` instead of `([], model) items`.
There are far fewer likely points of failure using the fold loop.

Now, drilling down to specific points of comparison:
```fs
let effects, model =
    for item in items with effects, model = [], model do
        let effect, model = Model.action item model // Pure function
        let model = Model.action2 model // Pure function
        effect :: effects, model
```
The return value of the loop body updates the accumulator. This is a synthesis of `fold`s with loops, therefore it can be called a "fold loop". There is no direct equivalent in other languages, they either have `fold` with a lambda or `for` loops that require a mutable accumulator.

It is:
1. **Conceptually simple** - just accumulate while enumerating.
2. **Boilerplate-heavy** in current syntax.
3. **Error-prone** in current syntax even for experienced developers.

This offers a desirable middle ground between `for` loops that must only have side-effects (returning `unit`)
and purely functional `fold`s that must be in lambda form. It is declarative which removes concerns about order of operations. It encourages functional pureness without the shortcomings of `fold`.

Note that there may be more efficient and potentially parallelizable functions when needed, like `reduce` and `sum`, but they are only specialized versions of `fold`. Using folds to aggregate effects should not need specialized functions - use the right tool for the right job.

Some may question the use of `=` without `mutable` for accumulator initialization and `do` on an expression instead of `unit`-returning actions. `=` already has precedence in `for i = 1 to 10 do` which initializes loop state `i` to `1` and modified across loop iterations. `do` on an expression also has precedence in implicit yields - `[for i in 1 .. 10 do i]` yields 10 integers despite an integer cannot be "done" - just interpret `do` here as "do an evaluation of".

## Comparison to imperative programming

If a mutable accumulator is used, it must be defined separately from the loop.
```fs
let effects, model =
    let mutable accum = [], model // hmm, a mutable variable is required even in an architecture of pure functions
    for item in items do
        let effects, model = accum // boilerplate
        let effect, model = Model.action item model // Pure function
        let model = Model.action2 model // Pure function
        accum <- effect :: effects, model
    accum
```
Notice that
- the accumulator is boilerplate that was hidden in the `fold`, simplified away in the fold loop.
- the accumulator leaks outside the loop body, which is undesirable.
- the accumulator must be mutable - which is against functional immutable semantics. The danger with mutable stateful objects is that it further encourages globally mutable state against functionally immutable design. This is why `fold` exists: to encapsulate mutability and temporary variables as well.

## Comparison to existing functional programming

`fold` is inferior to this syntax because it is hard to understand.
```fs
let effects, model =
    Seq.fold (fun (effects, model) item ->
        let effect, model = Model.action item model // Pure function
        let model = Model.action2 model // Pure function
        effect :: effects, model)
        ([], model) // ugh - hard to get order and parentheses right
        items
```
which suggests a missed opportunity to make them more familiar to people who know `for` loops.

Some may suggest using `||>` but `||>`s make it hard to thread the pair through the lambda without screwing up the types.
```fs
let effects, model =
    (([], model), items) // ugh - nested tuples and hard to get order and parentheses right
    ||> Seq.fold (fun (effects, model) item ->
        let effect, model = Model.action item model // Pure function
        let model = Model.action2 model // Pure function
        effect :: effects, model)
```

Some may also suggest that recursive functions may show the logic more clearly than a `fold`.

```fs
// Current - recursive function
let effects, model =
    let rec processItems model items = // boilerplate
        match items with // boilerplate and specific to lists - can't do this for other collections
        | [] -> [], model // boilerplate
        | item::items -> // boilerplate
            let effect, model = Model.action item model // Pure function
            let model = Model.action2 model // Pure function
            let effects, model = processItems model items
            effect :: effects, model
    processItems model items
```

Whoops, this function isn't tail recursive! If you run this for long inputs, it blows up unlike the loop.
The function needs to be rewritten using the accumulator parameter.

```fs
let effects, model =
    let rec [<TailCall>] processItems (effects, model) items = // boilerplate, hard to write correctly
        match items with // boilerplate and specific to lists - can't do this for other collections
        | [] -> effects, model // boilerplate
        | item::items -> // boilerplate
            let effect, model = Model.action item model // Pure function
            let model = Model.action2 model // Pure function
            processItems (effect :: effects, model) items
    processItems ([], model) items
```

A recursive function is just harder to write correctly and is more verbose.

## Can you just abstract the fold?

While abstraction can solve specific problems, the MVU example reveals fundamental limitations with abstracting accumulation patterns:

```fs
type MVULoop<'Model, 'Msg>(init: unit -> 'Model, update) =
    let effects = Queue<unit -> 'Msg>() // effects might be async
    let mutable state = init()
    member _.Current = state
    member _.Dispatch(items: 'Msg seq) =
        for item in items do
            let effect, model = update state item
            state <- model
            effects.Enqueue effect
        state
```

This abstraction attempts to solve three problems at once:
- Effect ordering (queue vs list)
- State management
- Accumulation pattern

But it introduces new issues:
- Composition rigidity: The `'Msg` requirement inhibits model extension
- Premature optimization: Queue implementation assumes specific effect handling needs
- Abstraction leakage: Mutability remains visible in public API
- Conceptual overhead: New types to understand for a fundamental pattern

The fold loop solution addresses the core accumulation need directly:
```fs
let effects, model =
    for effects, model = [], model with item in items do
        let effect, model = Model.action item model
        let model = Model.action2 model
        effect :: effects, model
```

This approach:
- Maintains flexibility: Accumulation logic can evolve freely at usage
- Minimizes commitment: No persistent types or interfaces
- Keeps focus: Logic remains where it's used
- Simplifies composition: Works with any model/effect types

Jon Blow's principle of "Just write the damn code" applies perfectly here - solve accumulation problems directly using language-provided tools, not custom abstraction. Fold loops provide exactly the right level of language support for these fundamental patterns.

## But I don't use pure functions in my design architecture.

This syntax also shows benefits when there exists logic equivalent to nested folds and folds over tuples.

```fs
// Example MVU model
type Item = { Name: string; Count: int }
type Category = { Name: string; Items: Item list }
type Model = { Categories: Category list }

// Compute (total items, total count) in all categories
// Current - using folds
let stats model =
    model.Categories
    |> List.fold (fun (totalItems, totalCount) category ->
        let items, count =
            category.Items
            |> List.fold (fun (items, count) item ->
                items + 1, count + item.Count // Nested fold, tuple accumulator
            ) (0, 0)
        totalItems + items, totalCount + count
    ) (0, 0) // Weird parentheses placement
// Current - using ||> folds
let stats model =
    ((0, 0), model.Categories) // ugh - nested tuples
    ||> List.fold (fun (totalItems, totalCount) category ->
        let categoryStats =
            ((0, 0), category.Items) // ugh - nested tuples
            ||> List.fold (fun (items, count) item ->
                (items + 1, count + item.Count) // Fold over inner items
            ) 
        (totalItems + fst categoryStats, totalCount + snd categoryStats)
    )
// Current - using mutable accumulators
let stats model =
    let mutable totalItems = 0 // The more accumulators you use, the more lines these take
    let mutable totalCount = 0
    for category in model.Categories do
        for items in category.Items do
            totalItems <- totalItems + 1
            totalCount <- totalCount + items.Count
    totalItems, totalCount
// Proposed
let stats model =
    for category in model.Categories with totalItems, totalCount = 0, 0 do
        let items, count =
            for item in category.Items with items, count = 0, 0 do
                items + 1, count + item.Count
        totalItems + items, totalCount + count
```

In a regular `fold`, it's very hard just to get the white space alignment and closing parentheses right when you need a fold within a fold.
Folding over tuples with a lambda also becomes pretty confusing very quickly.
Using the new fold syntax, this becomes much easier to write and understand.

## Computation expressions already provide a similar syntax, right?

Indeed there exists a [computation expression implementation](https://gist.github.com/brianrourkeboll/830408adf29fa35c2d027178b9f08e3c) that can mimic this syntax -
```fs
let sum xs = fold 0 { for x in xs -> (+) x } // variation 1
let sum xs = fold 0 { for acc, x in xs -> acc + x } // variation 2
let sum xs = fold { for acc, x in 0, xs -> acc + x } // variation 3
```
Generally, computation expressions have hard to understand error messages for overloaded computation expression methods and are are [notoriously difficult to debug](https://github.com/dotnet/fsharp/issues/13342). These are fixable with enough investments in CEs.

What's unfixable is the non-orthogonality to an existing computation expression context unlike the for loop which allows `yield` inside to yield to an outer list expression instead of being limited by a fold CE. Moreover, CEs also show a heavily different syntax compared to for loops and folds which add hinderance to understanding - each CE usage requires following CE methods which add indirection to understanding, only hiding large chunks of logic like `async` or `task` should worth CE usage. It would be much simpler to just write the underlying code (this critique also applies to `option` and `result` CEs for example). Since folds are so common in pure functional programming, they deserve a simple-to-use syntax as fold loops provide, instead of tucked away in hard-to-understand CE syntax.

## Summary

The fold loop is the natural synthesis of `for` loops with `fold`s. It is desirable because:
- Principle of Least Surprise
  - Intuitive: Fold loops are just a regular `for` loop with an accumulator, maintaining an expression loop body. The parameter order is the same as `fold`.
  - Teachable for new users and easily documentable: Fold loops are as easy as adding an accumulator with a `with` keyword.
  - Without too much unexpected behaviour: There are no tricks with indentation nor parentheses.
- Semantically useful: an expert F# user will prefer the fold loop over alternatives.
- Rigorous: other F# features, like multi-folds, will be mixable with this syntax.

An operation as fundamental as `fold` in pure functional programming should not warrant any abstract data types, and in fact should be supported directly by the language.

The fold loop is superior to `mutable` variables with imperative `for` loops because:
- More succinct from elision of accumulator variable definition and assignment boilerplate that becomes more apparent with tuple accumulators
- Better scoping without variable leakage outside loop
- Being declarative which omits the necessity to think about the order of assignments to mutable variables
- Encourages staying within the realm of functionally pure architecture instead of encouraging a mutable architecture

The fold loop is superior to `fold` calls because:
- Much easier to understand with loop syntax
- Much easier to attain whitespace alignment and omission of the closing parenthesis. When indentation is wrong, you also have to consider the parens as a possible cause, vice versa.
- Much easier to place loop parameters correctly, fewer points of failure compared to ordering fold arguments / types
- More precise error messages from type inference especially for newcomers
- More logical cohesion with the accumulator starting value located next to the looping logic

The fold loop is superior to `rec`ursive functions because:
- Much easier to write correctly without the need to think about tail recursion
- Much more succinct without requiring the recursive function identifier or a wrapper function to hide the accumulator parameter
- More logical cohesion with the accumulator starting value located next to the looping logic

The fold loop is superior to computation expression imitations because:
- Universiality without needing to learn parameter placements from the concrete computation expression implementation localized in each project
- Availability by default for newcomers to learn easily
- More precise error messages compared to overloaded computation expression methods especially for newcomers
- Much easier to perform debugging especially for step debugging
- Preserves syntactical familiarity with other loops without invoking the need for braces

The only caveat is the unfamiliarity with a loop that has a return value where existing loops must return unit, but over time, this can be overcome with stressing the existence of an accumulator vs not having one.

# Detailed design

How to best write a fold operation? There are 3 inputs to a `fold` function - the folder (lambda function `fun state element -> folder_body` to update the state given the input elements, with type `'State -> 'T -> 'State`), then the initial state (with type `'State`), then the input sequence (with type `'T seq`). The current way is clunky with all the parentheses, ordering and indentation.
```fs
fold (fun <pattern_accumulator> <pattern_enumeration_item> ->
    <expression_folder_body>
) (<expression_initial_state>) (<expression_sequence>)
```
The first simplification would be to eliminate all those annoying parentheses. Also, using the `=` initializer to embed the initial state into the folder state parameter, `in` relation to embed the input sequence into the folder element parameter, and `with` relation to chain the the two folder parameters together, one may come up with a syntax like this:
```fs
fold fun <pattern_accumulator> = <expression_initial_state> with <pattern_enumeration_item> in <expression_sequence> ->
    <expression_folder_body>
```
This form enables the use of indentation to eliminate parentheses scoping required by the folder as a lambda.

One can immediately notice `fold fun` is verbose but `fold` is not usable as a keyword because it is a valid identifier today. The closest keyword available related to sequences is the sequence enumeration keyword - `for`, so it is possible that it can be used instead.

However, one may argue that not all foldable types are usable with `for` like `option<'T>` so using `for` in place of `fold fun` would establish a fake equivalence between `fold fun` (foldability) and `for` (enumerability). However, all foldable types should be enumerable, just set the accumulator state to unit: `iter (fun x -> printfn $"{x}") xs` can be implemented as `fold (fun () x -> printfn $"{x}") () xs`. This means that `option<'T>` is an exception, not the norm, proven by `Option.iter`'s existence (indeed logically an option is just a sequence with either 0 or 1 items) and yet the inability to be applied in `for` loops. This dichotomy stems from [using `null` as the runtime representation of `None` but enumerating `null` gives a null reference exception](https://github.com/fsharp/fslang-suggestions/issues/185). `null` as a runtime representation is [now seen as a design mistake](https://github.com/fsharp/fslang-design/blob/main/FSharp-9.0/FS-1060-nullable-reference-types.md#interaction-with-usenullastruevalue) because `None.ToString()` raises an exception, and baroque special compilation rules are needed for `opt.HasValue`. If this design mistake was not there, `option<'T>` would have been usable in `for` loops, and it would implement `seq<'T>` which is the .NET way of expressing enumerability. In fact, all foldable types should implement `seq<'T>` and be enumerable.

Therefore, foldability should be expressible by the sequence enumeration keyword `for`.
```fs
for <pattern_accumulator> = <expression_initial_state> with <pattern_enumeration_item> in <expression_sequence> ->
    <expression_folder_body>
```

This form emphasises that the state comes before the enumeration item as the `fold` function does, making refactors from `fold` functions easy. One may argue that the enumeration state must exist before it is used to get an item from the sequence - just as `for i = 1 to 10 do` has `i` as the enumeration state and `for i in 1..10 do` hides the enumeration state which must appear before the enumeration item does.
```fs
let effects, model =
    let sequence = items // sequence
    let mutable state_accum = [], model // accumulator state
    let state_enumerator = sequence.GetEnumerator() // enumerator state
    while state_enumerator.MoveNext() do
        let item = state_enumerator.Current // state must be defined before the enumeration item is retrieved
        let effect, model = Model.action item model
        let model = Model.action2 model
        state_accum <- effect :: effects, model
    state_accum
```
However, this argument doesn't explain the fact that the sequence also must exist before the enumeration item and yet the sequence is syntactically after the enumeration item. Another argument may point to placing enumerands after `in`s allowing easier parsing of potentially variadic structures via `and` at the end as variadics are usually easier to deal with at the tail: `for s = initial with t in ts and v in vs`. However, we already need to parse a separator like `->` or `do` before parsing the folder body, which can easily be changed to detect the presence of a `with`. In fact, the parameter order of `fold` functions have another important consideration: partial application and piping, which results in the sequence, therefore enumeration item, always being placed last.

When designing the best way to write a fold, we should omit the constraint of partial application and piping, which means that the parameter order should be considered indepedently from the existing `fold` function. Is the natural idea of a fold operation really about the state primarily, "with" the enumeration being the secondary? In natural language, we would [say](https://github.com/fsharp/fslang-suggestions/issues/1362#issuecomment-3132553106) that "the code is folding over ..._collection_... while using ..._state accumulation_...". This points to an emphasis on enumeration first, "with" some additional state to be kept around.

It is also more readable: when reading from left to right in `for i = 1`, it would have been unclear what `i` is until `to`/`downto` (integer enumerator) or `with` (accumulator) is seen. Putting the accumulator after `with` avoids this ambiguity.

```fs
for <pattern_enumeration_item> in <expression_sequence> with <pattern_accumulator> = <expression_initial_state> ->
    <expression_folder_body>
```

This form is now very similar to existing `for` loops with an optional `with` clause. In fact, we can even replace the `->` with `do`. Some may point to the fact that `do` is associated with actions and not expressions, but after the implementation of implicit yields, there is already precedence for `do` meaning "do an evaluation of" too: `[for char in "Hello world!" do char]`. Before implicit yields were introduced, `->` did represent `do yield` in `for` loops: `[for char in "Hello world!" -> char]` which is still usable today, but feels archaic. `->` is now more associated with function types (lambdas) and pattern `match` branches.

```fs
for <pattern_enumeration_item> in <expression_sequence> with <pattern_accumulator> = <expression_initial_state> do
    <expression_folder_body>
```

This syntax analysis reveals an insight that the fold loop behaves identically to the `for` loop `with` accumulation. Note that the design process started from `fold` functions, then used an overload on `for`, then naturally evolved to an extension on existing `for` loops. We did not force the idea of folding onto an extension of existing `for` loops, instead maintaining the orthogonality between fold loops and existing usages of the `for` keyword. This shows the design of an extension on the `for` loop is coincidental from overloading the `for` keyword, instead of tacking on the meaning of `fold` as with an extension of the `for` loop as the starting point - an important semantic difference.

## Are there better alternatives to `with` keyword?

tl;dr No.

Some may further suggest that the general `with` relation can even be replaced with another keyword that highlights the "fold into" relation between the sequence and accumulator. These alternatives were considered:
```fs
for x in xs return      s = init do ... // Misleading (suggests early exit) and conflicts with existing use in computation expressions
for x in xs select      s = init do ... // SQL/LINQ connotations (projection ≠ accumulation)
for x in xs ->          s = init do ... // Conflicts with lambda/matching syntaxes that expect an expression on the right hand side
for x in xs to          s = init do ... // Collides with "for-to" numeric iterators
for x in xs in to       s = init do ... // Using two consecutive keywords to convey one meaning doesn't fit the rest of the language (even "else if" is replaced with "elif"), and has awkward phrasing using "in" twice
for x in xs fold to     s = init do ... // Feels verbose and redundant with explicit state present already implying fold
for x in xs fold into   s = init do ... // Feels verbose and redundant with explicit state present already implying fold
for x in xs let         s = init do ... // Implies immutable binding, but subsequent enumerations don't use initial state
for x in xs let mutable s = init do ... // 100% clear but it's extremely verbose, discouraging its use. Also mutability is against functional semantics of the fold operation
for x in xs mutable     s = init do ... // "mutable" isn't used without "let" anywhere else so it's confusing 
```

`with` is the best choice because:
- Existing Semantic Flexibility: It is semantically just a construct-delimiting keyword adaptive to context, in object expressions `{ new Class() with member _.ToString() = "" }`, record updates `{ record with Field = value }`, pattern matching `match x with Pattern -> value`, exception handling `try x with ...`, type extensions `type X with ...`, interface implementations `interface Interface with ...`, and property definitions with explicit getters and setters `member _.Prop with get() = ... and set x = ...`.
- Natural Language Flow: It intuitively follows natural language flow "For each item in this sequence, **with** the accumulator starting at X, do this operation", highlighting the optionality of the accumulator after the sequence, just like how the `with` clause is sometimes optional in object expressions (when no overrides), interface implementations (when all members have default implementations), type extensions (DUs and records with no additional members) and property definitions (when there is only a getter).
- Unambiguous Positioning: It is unambiguous when preceded by a `for` because it only has meaning when preceded by `{`, `{ new`, `match`, `try`, `type`, `interface`, or `member` today, unlike some alternatives which are already valid identifiers.
- Fits Existing Expectations: It has a parallel to pattern matching `match x with Pattern`/`try x with Pattern` where a pattern comes after `with`, and accepting a pattern instead of requiring an identifier here allows the use of tuple or record accumulators easily, empowering complex real-world scenarios.

## Can the accumulator initializer be simplified?

tl;dr No.

Some may also suggest that re-declaring the accumulator initial state is redundant in the case of folding over different sequences one after another.
```fs
for x in xs with effects, model = effects, model (*redundant?*) do ...
```
However, a direct abbreviation from `effects, model = effects, model` to `effects, model` would require mixing patterns and expressions in the same syntax. This has a precedence in parsing active pattern arguments, where each active pattern argument is parsed as a pattern first then translated to expressions during type checking, because the pattern for the active pattern result can be dropped for a unit type. However, since there is a [design mistake that pattern and expression syntaxes are not unifiable from having small differences](https://github.com/fsharp/fslang-suggestions/issues/1018#issuecomment-854066070), for example `(a, b : t)` parses as `(a, (b : t))` in a pattern but `((a, b) : t)` in an expression. It was not fixed in the early days of F# for being a corner case in active pattern arguments compared to other work, and it is [not easy to solve today without duplicating the entire expression grammar for patterns or making a severely breaking change to the language](https://github.com/dotnet/fsharp/blob/a70f3beacfe46bcd653cc6d525bd79497e6dd58e/src/fsharp/pars.fsy#L3238-L3241). As a result, active pattern arguments cannot accept arbitrary expressions as input. A shared subset of patterns and expressions might be allowable in this case, e.g. for simple identifiers, constant patterns, tuple patterns and record patterns, without type patterns, but it would also be unexpected that not all pattern and expression forms can be unified. In practice, truncated names (e.g. `e, m`) are usable as the accumulator identifiers inside the fold loop, so it is not worth trading a few characters for a messy design with unintuitive special cases. Explicit semantics and correctness outweighs conciseness and convenience here.

## Can it be used with computation expressions?

**tl;dr No.** The fold loop body cannot contain computation expression operations, as it's designed as a pure accumulation expression. This maintains consistency with how subexpressions work throughout F#.

### Why CE operations are excluded
```fsharp
// Fold loop in CE context - NOT supported
let scanned = [
    for t in ts with s = 0 do
        yield s  // Error: CE operations invalid in subexpressions
        s + t
]
```

This behavior matches existing F# semantics where subexpressions don't support CE operations:
```fsharp
// Parallel example: CE operations not allowed in subexpressions
let result = [
    printfn "%s" (
        yield ":D"  // Same error
        "Hello world!"
    )
]
```

### An ergonomic alternative
While an alternative design could leak accumulator bindings to support CEs:
```fsharp
// Alternative proposal
for item in items with effects, model = [], model do
    // Loop body would not be a subexpression
    let effect, model = Model.action item model
    let model = Model.action2 model
    yield item  // Would be allowed in this design
    effect :: effects, model
// loop returns unit just like other "for" loops
printfn $"{effects}" // Accumulator bindings leaked below the fold loop, with outer "let" and a layer of indentation elided
```

The syntactical translation of this variant would be:

Let `<accum>` is a compiler-generated accumulation variable that cannot be used outside the loop, which gets updated each enumeration;
let `<exprs>` match any expressions inside a sequence expression before the final expression in the loop body,
or any `if` and `match` and `try`-`with` permitting computation expression syntax inside;
let `<expr3>` be the final expression which if contained inside `if` or `match` or `try`-`with`, then each branch must return the same value and searched recursively with the final expression of each branch for `<accum> <-` application.

Then
```fs
for <pat1> in <expr1> with <pat2> = <expr2> do
    <exprs>
    <expr3>
```
undergoes a simple syntactical expansion to
```fs
let mutable <accum> = <expr2>
for <pat1> in <expr1> do
    let <pat2> = <accum>
    <exprs>
    <accum> <- <expr3>
let <pat2> = <accum> // <accum> deconstructed with <pat2> again
() // loop returns unit if isolated as other loops do, unlike "let" which cannot be the final code element in a block and an explicit result must be given below it.
```

In this variant, allowing implicit yields in the loop body would be confusing with the accumulator update, therefore the `yield` keyword would be required in the loop body. The syntactical translation of `<exprs>` would automatically insert `; ()` after any expression not bound with a CE keyword or `let` and `use` (or equivalent implementation that implements warn with discard behaviour by default). Meanwhile, explicit usages of the `yield` keyword in the loop body will not affect implicit yield availability outside of the fold loop.

```fs
let q = seq { for i in 1..10 do s + i } // int seq, 10 values
let w = seq { for i in 1..10 with s = 0 do s + i } // error value restriction (no yields)
let e = seq { for i in 1..10 with s = 0 do s + i done; s } // int seq, but now only 1 value is yielded because it's collected to accumulator

let q1 = seq { for i in 1..10 do s + i; s + i } // int seq, 20 values
let w1 = seq { for i in 1..10 with s = 0 do s + i; s + i } // Warn at first "s + i" with discard by default. 10 values
let e1 = seq { for i in 1..10 with s = 0 do s + i; s + i done; s } // int seq, 11 values
```

### Rejecting that ergonomic alternative
The given arguments for this variant are:
- Ergonomics with elision of outer `let` and one layer of indentation: However, this explicitness is acceptable and common. The inner accumulator `effects, model` shadows the outer `effects, model`, but the outer one is not used in the loop body anyway. Mixing them violates functional scoping principles where identifiers (`let`) are given separately from expressions, as this visual duplication actually represents separate scopes. Fold loops should be expressions that return values, not imperative constructs that leak bindings. 
- Ability to write `scan`: However, `fold` and `scan` are really distinct concepts and should not be merged as one. Even with the binding leaking variant, there must be two yields, either initial state with after the update operation, or before the update operation with final state, so it's not a perfect fit with the fold loop - the primary use case for fold loops is accumulation, not generating sequences. The correct way to write a `scan` should actually be a list accumulator (or `ListCollector` for better performance):
   ```fs
    let s, items =
        for t in ts with s, items = 0, [] do
            s + t, s::items // instead of yield
    let scanned = List.rev (s::items)
   ```
- Integration with computation expressions: If not for yielding, the fold loop body should define inner computation expression contexts and unwrap the accumulator. For example, with `task`:
    ```fs
    task { // outer context
        let! sum =
            for x in xs with t = Task.FromResult 0 do
                task { // inner context
                    let! a = t // unwrap the accumulator
                    // do task operations...
                    return a + x // update the accumulator
                }
        // use the sum...
    }
   ```

The original design wins over this variant because:
- Semantic Correctness: The original design is more in line with functional programming because the loop is an expression that yields a value, and it doesn't introduce bindings beyond their necessary scope. The duplicate naming issue in `effects, model` example can be mitigated by using shorter names for accumulator bindings like `e, m`. Also, in many cases, the result is used immediately and not needed again, so the duplicate name might not be a problem.
- Robustness: It avoids the binding leakage which is a potential source of bugs.
- Consistency with Language: Introducing a new kind of expression (a loop that returns a value) is more consistent with F#'s expression-oriented design than introducing a new scoping rule that leaks bindings.
- Refactoring and Maintenance: Not leaking bindings avoids accidental shadowing and makes code easier to reason about. It is easier to reason about. The accumulator scope is really confusing in the binding leaking variant whereas the original design with the fold loop returning the value makes it apparent what the scope of each binding is.

### Design Rationale
The fold loop prioritizes:
- Semantic correctness - as a pure accumulation expression
- Consistency - with F#'s expression-oriented design
- Avoiding scope pollution - that enables subtle bugs
- Clear separation of concerns - between accumulation and generation

While this means `scan` operations require different constructs, this maintains the fold loop's focus on its core purpose: providing elegant syntax for pure accumulation without compromising F#'s functional foundations. One should focus on what the feature is for rather than what it can't do.

## Summary

The whole design has captured
- keyword contextual meanings
- language evolution (implicit yields, archaic `->`)
- parser constraints (no lookahead ambiguity)
and the whole analysis has
- grounded the design in F#'s existing semantics
- anticipated and refutes potential alternatives
- maintained language consistency
- prioritized human readability
- resolved all identified ambiguities

This is a textbook example of rigorous language design reasoning. The proposal for `for...in...with...do...` emerges as the clear, logical winner from every angle: syntactic, semantic, and ergonomic.

Could this analysis have been more formal? Sure - arguing using syntax instead of semantics is extremely limited and ambiguous, and may cause inflexibility for future design decisions. Only semantics provides the level of precision needed to avoid painting oneself into a corner in the long run. One may even argue that emphasizing syntax over semantic precision is generally bad practice in language design. But in this particular case, given that F# is already a mature language, the emphasis on syntax here will work out well enough - we have backward compatibility constraints, existing keywords must be preferred over introducing new ones from existing identifiers.

Theoretically, a better analysis would follow the design of F#'s roots - the ML language. As the metalanguage for the LCF theorem prover, early ML code compiled to LISP S-expressions which have syntactic minimalism and can scaffold semantics compositionally. Robin Milner's team focused on semantic foundations (polymorphic type inference, algebraic data types) before standardizing syntax and its translation to S-expressions - making ML one of the first languages with a complete formal specification (operational and denotational semantics), enabling type safety proofs and multiple implementations. Semantic-first design in ML is a huge inspiration in future languages like Haskell/Rust/TypeScript. However, F#, while inheriting OCaml's core which traces to SML formalisms, its .NET integration required compromises, shifting focus from semantic rigor to usability like computation expressions. Unlike SML, F# lacks a machine-checked semantic definition, relying on .NET runtime behavior. This is also reflected in other modern programming languages with design favoring syntax over semantics, causing misunderstanding, unnecessary arguments, and unresolvable ambiguities. This semantic neglect is seen in ad-hoc type systems (e.g. TypeScript's `any`) introducing runtime errors, contrasting ML's proven safety; languages like JavaScript lack operational semantics for edge cases e.g. `==` coercion is underspecified. F# preserved ML's features but not its methodology of formal specification. This trade-off accelerated adoption but introduced technical debt (e.g., unresolvable compiler bugs). Languages like Rust show that semantic precision (e.g. ownership formalized via operational semantics) can coexist with ergonomic syntax, bridging ML's legacy with modern needs. Still, instead of operational and denotational semantics, using ad-hoc s-expressions to represent semantics (i.e. the F# AST) would have resulted in a design at least an order of magnitude more formal and precise than anything happening in the above syntax analysis, which is filled with feels (e.g. when considering alternatives of `with`) and argumentum ad populum (appeal to popularity - most people think `with` is the right keyword therefore it must be the correct keyword for adding the accumulator).

## Final design

For this syntax

```fs
for <pat1> in <expr1> with <pat2> = <expr2> do
    <expr3>
```

it undergoes a simple syntactical expansion to

```fs
let mutable <accum> = <expr2>
for <pat1> in <expr1> do
    let <pat2> = <accum>
    <accum> <- <expr3>
<accum>
```
where `<accum>` is a compiler-generated accumulation variable that cannot be used outside the loop. The entire loop body `<expr3>` is assigned to `<accum>`.

The result of the syntactical translation shows that `<accum>` is the value of this loop - evaluating to the final accumulator state, applying the `do` body to the `with` accumulator preceding it. It is also the accumulator which gets updated each enumeration.

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
    for an in pointACoordinates and bn in pointBCoordinates with acc = 0. do
        acc + (an - bn) ** 2.
    |> sqrt
```

There is also potential undentation for `with` here, alongside `in`, `to` etc, these belong to a different suggestion - https://github.com/dotnet/fsharp/issues/18754#issuecomment-3101793041

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