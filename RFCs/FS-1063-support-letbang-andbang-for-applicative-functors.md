# F# RFC FS-1063 - Support let! .. and... for applicative functors

The design suggestion [Support let! .. and... for applicative functors](https://github.com/fsharp/fslang-suggestions/issues/579) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] [Approved in principle](https://github.com/fsharp/fslang-suggestions/issues/579#event-1345361104) & [prioritised](https://github.com/fsharp/fslang-suggestions/issues/579#event-1501977428)
* [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/579)
* [x] [Discussion](https://github.com/fsharp/fslang-design/issues/335)
* [ ] Implementation: [In progress](https://github.com/Microsoft/visualfsharp/pull/5696)

# Summary
[summary]: #summary

Extend computation expressions to support applicative functors via a new `let! ... and! ... return ...` syntax.

With this new syntax, [Pauan points out](https://github.com/fsharp/fslang-suggestions/issues/579#issuecomment-310799948) that we can write a convenient and readable computation expression for `Observable`s that acts similarly to [`Observable.zip`](http://fsprojects.github.io/FSharp.Control.Reactive/tutorial.html#Observable-Module), but [avoids unnecessary resubscriptions and other overheads associated with `Bind`](https://github.com/fsharp/fslang-suggestions/issues/579#issuecomment-310854419) and syntactically scales nicely with the number of arguments whilst admitting arguments of different types.

Applicative computation expression form:

```fsharp
// Outputs a + b, which is recomputed every time foo or bar outputs
// a new value, avoiding any unnecessary resubscriptions
observable {
    let! a = foo
    and! b = bar
    return a + b
}
```

In comparison to using `Observable.zip`:

```fsharp
Observable.zip foo bar (fun a b -> a + b) // Less readable, awkward to have more than two observables
```

Or using a zip-like custom operation in a query expression:

```fsharp
rxquery {
    for a in foo do
    zip b in bar
    select (a + b) // Harder to map into a general monadic form - syntax implies a collection-like construct
}
```

Whilst applicative computation expressions can be very simple, they can also still do many of the extra things you might expect:

```fsharp
observable {
    use! a = foo             // Makes sure `a` is disposed
    and! (b,_) = bar         // Allows pattern matching
    and! c =
        observable {         // Supports nesting
            let! e = quux
            and! f = corge
            return e * f
        }
    return a.Count() + b / c // Can return with arbitrarily complex expressions
}
```

# Motivation
[motivation]: #motivation

Applicative functors (or just "applicatives", for short) have been growing in popularity as a way to build applications and model certain domains over the last decade or so, since McBride and Paterson published [Applicative Programming with Effects](http://www.staff.city.ac.uk/~ross/papers/Applicative.html). Applicatives are now reaching a level of popularity within the community that supporting them with a convenient and readable syntax, as we do for monads, makes sense.

With _applicative_ computation expressions, we can write more computations with this convenient syntax than before (there are more contexts which meet the requirements for applicative computation expressions than the existing monadic ones), and we can write more efficient computations (the requirements of applicatives rule out needing to support some potentially expensive operations).

## Why applicatives?

[Applicative functors](https://en.wikipedia.org/wiki/Applicative_functor) sit, on the spectrum of flexibility vs. predictability, somewhere between [functors](https://en.wikipedia.org/wiki/Functor#Computer_implementations) (i.e. types which support `Map`), and [monads](https://en.wikipedia.org/wiki/Monad_(functional_programming)) (i.e. types which support `Bind`, which currently underpin computation expressions).

If we consider `Bind : M<'T> * ('T -> M<'U>) -> M<'U>`, we can see that the second element of the input is a function that requires a value to create the resulting "wrapped value". This means the argument to `Bind` has the power to completely change the context of the result based on the value seen (e.g. to create and destroy `Observable` subscriptions), but it also means that the expression builder can predict much less about what the given function will decide to do, and hence has fewer outcomes that it can rule out and potentially optimise away.

In contrast, `Apply : M<'T -> 'U> * M<'T> -> M<'U>` only needs a wrapped function, which is something we have whilst building our computation and not something that can be controlled by the values that come later. This removes some flexibility to drastically alter the shape of the context in response to values seen later, but means that the computation expression builder now knows much more about what can or cannot happen after construction, and hence can make intelligent decisions off the back of that (e.g. to avoid unsubscribing only to immediately resubscribe, or to perhaps run two operations in parallel because it knows there can be no dependencies between them).

So, importantly, applicatives allow us the power to use functions which are "wrapped up" inside a functor, but [preserve our ability to analyse the structure of the computation](https://paolocapriotti.com/assets/applicative.pdf). This is a critical distinction which can have a huge impact on performance, and indeed on what is possible to construct at all, so has very tangible implications.

## Examples of useful applicatives

The examples below all make use of types which are applicatives, but explicitly _not_ monads, to allow a powerful model for building a particular kind of computation, whilst preserving enough constraints to offer useful guarantees. Each example includes a sample code snippet using the new syntax.

[Marlow et al.](https://dl.acm.org/citation.cfm?id=2628144) discuss the fact that the independence of arguments to an applicative (as opposed to the implied sequencing of monads) allow us to conveniently introduce parallelism.

```fsharp
// Reads the values of x, y and z concurrently, then applies f to them
parallel {
    let! x = slowRequestX()
    and! y = slowRequestY()
    and! z = slowRequestZ()
    return f x y z
}
```

[Tomas Petricek's formlets blog post](http://tomasp.net/blog/formlets-in-linq.aspx/) introduces the idea that we can use applicatives to build web forms. The guarantee of a static structure of the formlet applicative is used to render forms, but its powerful behaviours still allow useful processing of requests.

```fsharp
// One computation expression gives both the behaviour of the form and its structure
formlet {
    let! name = Formlet.textBox
    and! gender = Formlet.dropDown ["Male"; "Female"]
    return name + " is " + gender
}
```

[Pauan's comment about Observables](https://github.com/fsharp/fslang-suggestions/issues/579#issuecomment-310799948) (mentioned [earlier](#motivation)) points out that applicatives allow us to avoid frequent resubscriptions to `Observable` values because we know precisely how they'll be hooked up ahead of time, and that it won't change within the lifetime of the applicative.

```fsharp
// Outputs a + b, which is recomputed every time foo or bar outputs a new value,
// avoiding any unnecessary resubscriptions
observable {
    let! a = foo
    and! b = bar
    return a + b
}
```

[McBride & Paterson's paper](http://www.staff.city.ac.uk/~ross/papers/Applicative.html) introduces a type very much like F#'s `Result<'T,'TError>` which can be used to stitch together functions and values which might fail, but conveniently accumulating all of the errors which can then be helpfully presented at once, as opposed to immediately presenting the first error. This allows you to take [Scott Wlaschin's Railway Oriented Programming](https://fsharpforfunandprofit.com/rop/) to the next level by not just bailing out when things go wrong, but also providing helpful and detailed error messages.

```fsharp
// If both reading from the database or the file go wrong, the computation
// can collect up the errors into a list to helpfully present to the user,
// rather than just immediately showing the first error and obscuring the
// second error
result {
    let! users = readUsersFromDb()
    and! birthdays = readUserBirthdaysFromFile(filename)
    return updateBirthdays users birthdays
}
```

[Capriotti & Kaposi's paper](https://paolocapriotti.com/assets/applicative.pdf) introduces an example of creating an command line argument parser, where a single applicative can both statically generate help text for the parser, and dynamically parse options given to an application. [eulerfx](https://github.com/fsharp/fslang-suggestions/issues/579#issuecomment-309764738) has imagined an F# interpretation of that:

```fsharp
// One computation expression gives both the behaviour of the parser
// (in terms of how to parse each element of it, what their defaults should
// be, etc.) and the information needed to generate its help text
opt {
    let! username = Opt("username", (Some ""), Some)
    and! fullname = Opt("fullname", None, Some)
    and! id = Opt("id", None, readInt)
    return User(username, fullname, id)
}
```

With all of these examples, we can nest the applicative computation expressions inside other computation expressions to build larger descriptions that cleanly separate the pure computation from its context.

# Detailed Design
[design]: #detailed-design

This RFC introduces the syntax and desugaring for applicative computation expressions, much like that which exists for monads and related constructs.

## Syntax

To be accepted as an applicative computation expression (CE), the CE must be of the form `let! ... and! ... return ...`:

* There must be at least one `and!`s after the `let!`, but there is no hard upper-limit on the number of `and!`s
* No `let!`s may appear after an `and!` in the same applicative CE
* No normal `let`s in between the `let!` and `and!`s
* No usage of `yield` in place of `return`
* `use!` and `anduse!` may replace `let!` and `and!`, respectively, to indicate a resource that needs to be managed by the CE builder
* `do!`, `match!`, `return!` and other remaining CE keywords are also not valid in an applicative CE

### Valid syntax

Only one `and!`:

```fsharp
ce {
    let! x = foo
    and! y = bar ✔️
    return x + y
 }
```

Many `and!`s:

```fsharp
ce {
    let! w = foo
    and! x = bar ✔️
    and! y = baz ✔️
    and! z = qux ✔️
    return w + x + y + z
 }
```

`let`-binding inside the `return`:

```fsharp
ce {
    let! x = foo
    and! y = bar
    and! z = baz️
    return (let w = x + y in w + z) ✔️
 }
```

Function call inside the `return`:

```fsharp
ce {
    let! x = foo
    and! y = bar
    and! z = baz
    return sprintf "x = %d, y = %d, z = %d" x y z ✔️
 }
```

The `let!` is replaced by `use!`:

```fsharp
ce {
    use! x = foo ✔️
    and! y = bar
    return x + y
 }
```

Constant and wildcard patterns:

```fsharp
ce {
    let! x  = foo
    and! () = performOperation() ✔️
    and! _  = getSomethingToIgnore() ✔️
    and! y  = bar
    return x + y
 }
```

Variable patterns:

```fsharp
ce {
    let! ActivePattern(x) = foo ✔️
    and! (y,_)            = bar ✔️
    and! (SingleCaseDu z) = baz ✔️
    return x + y + z
 }
```

An arbitrary `and!` is replaced by `anduse!`:

```fsharp
ce {
    let!    w = foo
    and!    x = bar
    anduse! y = baz ✔️
    and!    z = qux
    return w + x + y + z
 }
```

The `let! ... and! ...` form is replaced entirely by its resource-tracking equivalent:

```fsharp
ce {
    use!    x = foo ✔️
    anduse! y = bar ✔️
    anduse! z = qux ✔️
    return x + y + z
 }
```

### Invalid syntax

A `let!` after an `and!`:

```fsharp
ce {
    let! x = foo
    and! y = bar
    let! z = baz ❌
    return x + y + z
 }
// Example.fsx(4,5): error FS3243: Expecting 'and!', 'anduse!' or 'return' but saw something else. Applicative computation expressions must be of the form 'let! <pat1> = <expr2> and! <pat2> = <expr2> and! ... and! <patN> = <exprN> return <exprBody>'.
```

A `let` interrupting the `let! ... and! ...` block:

```fsharp
ce {
    let! x = foo
    let  z = x / 3 ❌
    and! y = bar
    return x + y + z
 }
// Example.fsx(4,5): error FS0010: Unexpected keyword 'and!' in expression
```

A `let` after the `and!`s but before `return`:

```fsharp
ce {
    let! x = foo
    and! y = bar
    let z = y * 2 ❌
    return x + y + z
 }
// Example.fsx(4,5): error FS3243: Expecting 'and!', 'anduse!' or 'return' but saw something else. Applicative computation expressions must be of the form 'let! <pat1> = <expr2> and! <pat2> = <expr2> and! ... and! <patN> = <exprN> return <exprBody>'.
```

A `yield` instead of a `return`:

```fsharp
ce {
    let! x = foo
    and! y = bar
    yield x + y ❌
 }
// Example.fsx(4,5): error FS3245: 'yield' is not valid in this position in an applicative computation expression. Did you mean 'return' instead?
```

Multiple `return`s:

```fsharp
ce {
    let! x = foo
    and! y = bar
    return x + y
    return (x + y) * 2 ❌
 }
// Example.fsx(5,5): error FS3247: Saw unexpected expression sequenced after 'return'. Applicative computation expressions must be terminated with a single 'return'.
```

Additional expressions sequenced after the `return`:

```fsharp
ce {
    let! x = foo
    and! y = bar
    return x + y
    let w = 42 ❌
    let z = w - 6 ❌
 }
// Example.fsx(5,5): error FS3247: Saw unexpected expression sequenced after 'return'. Applicative computation expressions must be terminated with a single 'return'.
```

No `return` at all:

```fsharp
ce {
    let! x = foo
    and! y = bar
 } ❌
// Example.fsx(2,5): error FS3246: No body given after the applicative bindings. Expected a 'return' to terminate this applicative computation expression.
```

Other, unsupported, CE keywords anywhere in the expression:

```fsharp
ce {
    let! x = foo
    and! y = bar
    return! f x y ❌
 }
// Example.fsx(4,5): error FS3245: 'return!' is not valid in this position in an applicative computation expression. Did you mean 'return' instead?
```

```fsharp
ce {
    let! x = foo
    and! y = bar
    do! webRequest z ❌
    return x + y
 }
// Example.fsx(4,5): error FS3243: Expecting 'and!', 'anduse!' or 'return' but saw something else. Applicative computation expressions must be of the form 'let! <pat1> = <expr2> and! <pat2> = <expr2> and! ... and! <patN> = <exprN> return <exprBody>'.
```

```fsharp
ce {
    let! x = foo
    and! y = bar
    for elem in z do printfn "Elem: %+A" elem ❌
    return x + y
 }
// Example.fsx(4,5): error FS3243: Expecting 'and!', 'anduse!' or 'return' but saw something else. Applicative computation expressions must be of the form 'let! <pat1> = <expr2> and! <pat2> = <expr2> and! ... and! <patN> = <exprN> return <exprBody>'.
```

Pattern matching in conjunction with `use!` or `anduse!`:
```fsharp
ce {
    use! (_,x) = foo
    anduse! y  = bar
    return x + y
 }
// Example.fsx(2,10): error FS3244: Pattern matching is not allowed on the left-hand side of the equals. 'use! ... anduse! ...' bindings must be of the form 'use! <var> = <expr>' or 'anduse! <var> = <expr>'.
```

### Rationale for strong syntax constraints

This syntax may sound very constrained, but it is for good reason. The structure imposed by this rule forces the CE to be in a canonical form ([McBride & Paterson](http://www.staff.city.ac.uk/~ross/papers/Applicative.html)):

> Any expression built from the Applicative combinators can be transformed to a canonical form in which a single pure function is "applied" to the effectful parts in depth-first order:  
`pure f <*> arg1 <*> ... <*> argN`  
This canonical form captures the essence of Applicative programming: computations have a fixed structure, given by the pure function, and a sequence of subcomputations, given by the effectful arguments.

In our case, the expression to the right of `return` (i.e. `pure`) becomes the body of a lambda, whose parameters are introduced by the `let! ... and! ...` preceding it.

Similarly, the canonical form of `let! ... and! ... return ...` in F# makes should make it clear that what we are really doing it calling the function given to `return` with the arguments introduced by `let! ... and! ...`, but in a special context determined by the CE builder.

Despite requiring the canonical form, there are still many ways to build more complex and useful expressions from this syntax. The rest of this section will detail these.

## Desugaring

Computation expressions are provided meaning via [translation to method calls on a builder class](https://docs.microsoft.com/en-us/dotnet/fsharp/language-reference/computation-expressions#creating-a-new-type-of-computation-expression).

The new builder methods are as follows:

|Method       |Typical Signature|Description|
|-------------|-----------------|-----------|
|`Apply`      |`M<'T -> 'U> * M<'T> -> M<'U>`|Called for `let!`, `use!`, `and!` and `anduse!` to allow function application in the context of the CE|
|`ApplyUsing` |`'T * ('T -> 'U) -> 'U when 'U :> IDisposable`|Called in addition to `Apply` for `use!` and `anduse!` to manage resources|

An example desugaring of a basic applicative computation expression:

```fsharp
ce {
    let! x = foo
    and! y = bar
    and! z = baz
    return x + y + z
 }
```

becomes

```fsharp
ce.Apply(
    ce.Apply(
        ce.Apply(
            ce.Return(
                (fun x ->
                    (fun y ->
                        (fun z ->
                            x + y + z
                        )
                    )
                )),
            foo),
        bar),
    baz)
```

### Pattern matching

```fsharp
let (|Quad|) (i : int) =
    i * i * i * i

type SingleCaseDu<'a> = SingleCaseDu of 'a

ce {
    let! Quad(x)          = foo
    and! (y,_)            = bar
    and! (SingleCaseDu z) = baz
    return x + y + z
 }
```

becomes

```fsharp
ce.Apply(
    ce.Apply(
        ce.Apply(
            ce.Return(
                (fun Quad(x) ->
                    (fun (y,_) ->
                        (fun (SingleCaseDu z) ->
                            x + y + z
                        )
                    )
                )),
            foo),
        bar), 
    baz)
```

Pattern matching on the left-hand side of of a `let! ... and! ...` binding is valid, and desugars into the same pattern, but now as part of the lambda corresponding to that binding.

### Using the monadic and applicative styles simultaneously

Recall that `let! ... and! ... return ...` syntax precludes an additional `let!` anywhere in the CE. In the case where your applicative happens also to be a monad, and you want to leverage the benefits of an applicative in some places (e.g. for performance reasons) but also use a `let!` (e.g. for convenience, or to do something a pure applicative doesn't support), you must do so inside a different CE context, e.g.:

```fsharp
ce {
    let! quux =
        ce {
            let! x                = foo
            and! (y,_)            = bar
            and! (SingleCaseDu z) = baz
            return x + y + z
        }
    if quux > 6
    then
        return quux
    else
        return 5
}
```

becomes

```fsharp
ce.Bind(
    ce.Apply(
        ce.Apply(
            ce.Apply(
                ce.Return(
                    (fun x ->
                        (fun (y,_) ->
                            (fun (SingleCaseDu z) ->
                                x + y + z
                            )
                        )
                    )),
                foo),
            bar),
        baz),
    (fun quux ->
        if quux > 6
        then
            return quux
        else
            return 5)
)
```

### Using yield

The `yield` keyword can be used to tie together a series of applicative computation expressions, but the rules about the canonical form of applicatives still apply, so we just need to use the same trick as with additional `let!`s and leave the scope of the canonical applicative syntax (and therefore leave the additional constraints it places upon us):

```fsharp
ce {
    yield
        ce {
            let! x = foo
            and! y = bar
            and! z = baz
            return x + y + z
        }
    yield
        ce {
            let! x = foo
            and! y = bar
            return x + y
        }
    yield
        ce {
            let! y = bar
            and! z = baz
            return y + z
        }
}
```

In order to aid readability, it may be more appropriate to pull out each `yield` argument and name it outside the parent CE, and that is supported too:

```fsharp
let xyz =
    ce {
        let! x = foo
        and! y = bar
        and! z = baz
        return x + y + z
    }

let xy =
    ce {
        let! x = foo
        and! y = bar
        return x + y
    }

let yz =
    ce {
        let! y = bar
        and! z = baz
        return y + z
    }

ce {
    yield xyz
    yield xy
    yield yz
}
```

The new syntax can also be mixed with other styles (e.g. a custom `<|>` alternation operator or custom CE keywords) to find the right solution for the problem at hand. In either case, the same complications and trade-offs detailed below apply for `let! ... and! ... return ...` blocks combined with `yield`, as with these alternatives.

#### Why yield works this way

The existing `let!` CE syntax allows us to desugar sequenced statements into a compound expression via a call to `Combine`. One common example of this is `seq { }` computation expressions:

```fsharp
// Generates the sequence { 1; 2; 3 }
seq {
    yield 1
    yield 2
    yield 3
}
```

The `yield` keyword is used to signify that each element is yielded as the resulting sequence is iterated, but strictly speaking there are two steps taking place here:

1. `yield` takes the value on the left and wraps it up in the appropriate context by calling the `Yield` method on the builder.
2. The sequencing of the expressions by placing them each on a new line (or by separating them with a `;`) results in the expressions being tied together via nested calls to `Combine` on the builder.

> As an aside, `return` desugars to a call to `Return` on the builder, just as is the case for `yield` desugaring to `Yield`. In fact, the two generally have the same type signature and do the same thing, the difference being that `yield` is used to emphasise this idea of logically appending to a sequence.

One might assume that the syntax could be extended to something such as:

```fsharp
ce {
    let! x = foo
    and! y = bar
    and! z = baz
    yield x + y + z
    yield x + y
    yield y + z
 }
```

Unfortunately, the naive desugaring of this can make it very easy to build a resulting chain of method calls which unintentionally ends up being very large:

```fsharp
ce.Combine(
    ce.Combine(
        ce.Apply(
            ce.Apply(
                ce.Apply(
                    ce.Return(
                        (fun x ->
                            (fun y ->
                                (fun z ->
                                    x + y + z
                                )
                            )
                        )),
                    foo),
                bar),
            baz),
        ce.Apply(
            ce.Apply(
                ce.Apply(
                    ce.Return(
                        (fun x ->
                            (fun y ->
                                (fun z ->
                                    x + y
                                )
                            )
                        )),
                    foo),
                bar),
            baz)
    ),
    ce.Apply(
        ce.Apply(
            ce.Apply(
                ce.Return(
                    (fun x ->
                        (fun y ->
                            (fun z ->
                                y + z
                            )
                        )
                    )),
                foo),
            bar), 
        baz)
)
```

**N.B.** the size of the desugared expression grows with the product of the number of bindings introduced by the `let! ... and! ...` syntax and the number calls to `Combine` implied by the alternative cases.

An attempt at a very smart desugaring which tries to cut down the resulting expression might, on the face of it, seem like a reasonable option. However, beyond the cost of analysing which values which are introduced by `let! ... and! ...` actually go on to be used, we must also consider the right-hand sides of the `let! ... and! ...` bindings and the pattern matching: Do we evaluated these once up front? Or recompute them in each alternative case at the leaf of the tree of calls to `Combine`? What if the expressions on the right-hand sides have side-effects, or the left-hand side utilises active patterns with side-effects? At that point we either make complex, unintuitive rules, or force the CE writer to be explicit.

Continuing in the spirit of CEs generally being straightforward desugarings, we therefore choose to make make the CE writer clearly state their desire, and hence wholly separate the notion of sequencing from the applicative syntax:

```fsharp
ce {
    yield
        ce {
            let! x = foo
            and! y = bar
            and! z = baz
            return x + y + z
        }
    yield
        ce {
            let! x = foo
            and! y = bar
            return x + y
        }
    yield
        ce {
            let! y = bar
            and! z = baz
            return y + z
        }
}
```

then desugars to

```fsharp
ce.Combine(
    ce.Combine(
        ce.Apply(
            ce.Apply(
                ce.Apply(
                    ce.Return(
                        (fun x ->
                            (fun y ->
                                (fun z ->
                                    x + y + z
                                )
                            )
                        )),
                    foo),
                bar),
            baz),
        ce.Apply(
            ce.Apply(
                ce.Return(
                    (fun x ->
                        (fun y ->
                            x + y
                        )
                    )),
                foo),
            bar)
    ),
    ce.Apply(
        ce.Apply(
            ce.Return(
                (fun y ->
                    (fun z ->
                        y + z
                    )
                )),
            bar),
        baz)
)
```

**N.B.** this syntax forces the writer to be explicit about how many times `Apply` should be called, and with which arguments, for each call to `Combine`, since they create a new applicative computation for each combined case. Notice also how the right-hand sides are copied for each case in order to keep the occurrence of potential side-effects from evaluating them predictable, and also occur before the pattern matching _each time_ a new alternative case is explored.

### Managing resources

Just as monads support `Using` via `use!`, applicatives support `ApplyUsing` via `use! ... anduse! ...` to help manage resources.

In the applicative CE syntax, a binding can be either `and!` or `anduse!` (unless it is the first, in which case it must be either `let!` or `use!`), i.e. you can mix-and-match to describe which bindings should be covered by a call to `ApplyUsing`:

```fsharp
ce {
     use! x    = foo // x is wrapped in a call to ce.ApplyUsing
     and! y    = bar // y is _not_ wrapped in a call to ce.ApplyUsing
     anduse! z = baz // z is wrapped in a call to ce.ApplyUsing
     return x + y + z
 }
```

becomes

```fsharp
ce.Apply(
    ce.Apply(
        ce.Apply(
            ce.Return(
                (fun x ->
                    (fun y ->
                        (fun z ->
                            // Only once all arguments have been applied in, we make sure
                            // disposal happens via ce.ApplyUsing. Exceptions in ce.Apply,
                            // for example, could mean resources are leaked, (similarly
                            // to the existing weakness for ce.Bind)
                            ce.ApplyUsing(x, fun x ->
                                                            // <- N.B. No ce.ApplyUsing call here because we used `and!`
                                    ce.ApplyUsing(z, fun z -> // instead of `anduse!` for `y` in the CE. Similarly, we
                                        x + y + z           // could have chosen to use `let!` instead of `use!` for the
                                    )                       // first binding to avoid a call to ce.ApplyUsing
                                )
                            )
                        )
                    )
                )),
            foo),
        bar),
    baz)
```

The new `ApplyUsing` builder method is very much like the existing `Using`, but it does not require wrapping the given value in a context:

```fsharp
Using : 'T * ('T -> M<'U>) -> M<'U> when 'U :> IDisposable
```

in comparison to

```fsharp
ApplyUsing : 'T * ('T -> 'U) -> 'U when 'U :> IDisposable
```

Just as with the existing monadic `use!` syntax, the left-hand side of an applicative binding is constrained to being a variable. It cannot be a pattern that deconstructs a value because that makes things much more complicated: What if multiple names are bound, should they all be disposed? What if it is not the case that they are all disposable? Is the intention to dispose the bound variable, or the structure being pattern matched upon?

### Ambiguities surrounding a `let! .. return ...`

Some CEs could be desugared in multiple ways, depending on which methods are implemented on a builder (and assuming the implementations follow the standard laws relating these functions).

For example:

```fsharp
ce {
    let! x = foo
    return x + 1
 }
```

Can be desugared via `Bind` and `Return`:

```fsharp
ce.Bind(
    (fun x -> ce.Return(x + 1)),
    foo)
```

Or via `Apply` and `Return`:

```fsharp
ce.Apply(
    ce.Return(fun x -> x + 1),
    foo)
```

This is because the operation is really equivalent to a `Map`, something which can be implemented in terms of `Return` and either `Bind` or `Apply`. It is in this sense that these functions are more general than a plain functor's `Map`.

In order to avoid breaking backwards compatibility, the default resolution is to desugar via `Bind`, _failing if it is not defined on the builder_ (even though, conceptually, it could be implemented via `Apply`). This is consistent with in previous F# versions. [Later work on supporting `Map`](https://github.com/fsharp/fslang-design/blob/master/RFCs/FS-1048-ce-builder-map.md) can then make the choice about how to resolve this in a way which works with that in mind too.

## Run and Delay

When a computation expression builder implements a `Run` or `Delay` method (or both), the desugared computation expression is wrapped in further calls corresponding to what is defined on the builder. Since applicative computations are required to follow the canonical form (exactly one `return`, etc.), an applicative computation expression will have precisely one `Run` method call (if it is defined) and one `Delay` method call (if it is defined).

For example, if `Run` and `Delay` are both defined on the `ce` builder:

```fsharp
ce {
    let! x = foo
    and! y = bar
    and! z = baz
    return x + y + z
 }
```

becomes

```fsharp
builder.Run(
    builder.Delay(fun () ->
        ce.Apply(
            ce.Apply(
                ce.Apply(
                    ce.Return(
                        (fun x ->
                            (fun y ->
                                (fun z ->
                                    x + y + z
                                )
                            )
                        )),
                    foo),
                bar),
            baz)
    )
)
```

These methods offer a mechanism to hook into before (`Delay`) and after (`Run`) the evaluation of the computation expression.

### The proposed desugaring is purely syntactical

The proposed change acts purely as a syntactic rewriting of a computation expression to calls to methods on a builder. As such, whilst the change is largely motivated by the theory of applicatives, the desugaring can be used to call methods of different types to those suggested above. This attribute is in line with the existing semantics of computation expression translation (note how [the MSDN docs](https://docs.microsoft.com/en-us/dotnet/fsharp/language-reference/computation-expressions#creating-a-new-type-of-computation-expression) talk in terms of "_typical_ signatures").

# Drawbacks
[drawbacks]: #drawbacks

The new applicative computation expressions are quite constrained, and as has been discussed, that is precisely what allows them to be so useful. However, these constraints are potentially somewhat unintuitive to the beginner. Computation expressions already involve one of the steeper learning curves of the F# language features, so the added complexity from this feature needs to be carefully weighed against the potential guarantees, expressiveness and performance gains that they can offer.

# Alternative Designs
[alternative-designs]: #alternative-designs

We chose not to support `do!` or `anddo!` in place of a `let! _ = ...` or `and! _ = ...` (respectively), since `do!` implies side-effects and hence sequencing in a way that applicatives explicitly aim to avoid (see the parallelism example earlier). These keywords and their corresponding translations could be introduced in a later addition to the language, if the community's position changed on the matter.

[Tomas Petricek's Joinads](http://tomasp.net/blog/fsharp-variations-joinads.aspx/) offered a superset of the features proposed in this RFC, but was [rejected](https://github.com/fsharp/fslang-suggestions/issues/172) due to its complexity. The above proposal is of much smaller scope, so should be a much less risky change.

Various attempts have been made to attempt to get the benefits of applicatives within the existing syntax, but most end up involving confusing boilerplate, and make it easy to provide arguments in the wrong order because they cannot be named (in contrast to `let! ... and! ... return ...` which forces each argument to be named right next to its value in order to be used inside the `return`). It tends to be the case that even the authors of these experiments consider them abuses of the existing language features and recommend against them.

<details>
  <summary>Expand: An example of trying to simulate applicatives using the existing CE syntax</summary>
  <p>

```fsharp
type 'a Foo = private Foo of 'a

[<RequireQualifiedAccess>]
module Foo =

    let ofValue (a : 'a) : 'a Foo = Foo a

    let apply ((Foo f) : ('a -> 'b) Foo) ((Foo a) : 'a Foo) : 'b Foo =
        Foo (f a)


type FooBuilder () =

    member __.Yield (_ : unit) =
        id

    [<CustomOperation("apply")>]
    member __.Apply (f : 'a Foo -> ('b -> 'c) Foo, foo : 'b Foo) : 'a Foo -> 'c Foo =
        f >> (fun ff -> Foo.apply ff foo)

    [<CustomOperation("into")>]
    member __.Into (f : 'a Foo -> 'b Foo, a : 'a) : 'b Foo =
        a |> Foo.ofValue |> f


let foo = FooBuilder ()

let test =
    foo {
        apply (Foo.ofValue 5)
        apply (Foo.ofValue true)
        apply (Foo.ofValue 12.34)
        apply (Foo.ofValue "Hello")
        into (fun i b f s -> if i > 3 && b then s else "Nope")
    }
```

Thanks to [nickcowle](https://github.com/nickcowle) for providing this example.

</p></details>

# Compatibility
[compatibility]: #compatibility

## Is this a breaking change?

This change should be backwards compatible.

Existing computation expression builders with an `Apply` method should not change in behaviour, since usages of the builder would still need to add the new `let! ... and! ...` syntax to activate it. In particular, in the case of `let! ... return ...`, we will continue to only pick bind, as mentioned earlier.

## What happens when previous versions of the F# compiler encounter this design addition as source code?

Previous compiler versions reject the new `and!` and `anduse!` keywords:

```
error FS1141: Identifiers followed by '!' are reserved for future use
```

## What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?

Since the syntax is desugared into a standard method call on the builder object, after compilation, usages of this feature will be usable with previous compiler versions.

# Unresolved Questions
[unresolved]: #unresolved-questions

None.
