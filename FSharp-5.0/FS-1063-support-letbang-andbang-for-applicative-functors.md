# F# RFC FS-1063 - More efficient and expressive computation expressions via `and!` and `BindReturn`

The design suggestion [Support let! .. and... for applicative functors](https://github.com/fsharp/fslang-suggestions/issues/579) has been marked "approved in principle". The 2nd design suggestion [extend ComputationExpression builders with Map](https://github.com/fsharp/fslang-suggestions/issues/36) has also been approved. This RFC covers the detailed proposal for the combination of these two suggestions.

* [x] [Approved in principle](https://github.com/fsharp/fslang-suggestions/issues/579#event-1345361104) & [prioritised](https://github.com/fsharp/fslang-suggestions/issues/579#event-1501977428)
* [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/579)
* [x] [Discussion](https://github.com/fsharp/fslang-design/issues/335)
* [x] Implementation: [Completed](https://github.com/dotnet/fsharp/pull/7756)
* [x] [Design note](https://github.com/dsyme/fsharp-presentations/blob/master/design-notes/rethinking-applicatives.md)

# Summary
[summary]: #summary

This RFC adds support a new `let! ... and! ... ` syntax in computation expressions.  This allows
computations to avoid using of a sequence of `let! ... let! ...` which forces re-execution of 'expensive' binds
when these are independent. There are many examples where this is valuable, many of them known as "applicatives" in the functional
programming community - or, equivelantly, in the distinction between the "static" and "dynamic" portions of computation graphs.

The RFC also adds an optional translation of those binds which immediately always execute `return`. These use an optional builder method `BindReturn`.

# Detailed Design
[design]: #detailed-design

First, a `let! ... and! ...` expressions is de-sugared as follows. Given this:

    builder { let! pat1 = e1 and! ... and! patN = eN in ... }

then

1. If a `BindN` (e.g. `Bind3`) method is present on the builder, then the de-sugaring is:

       builder.BindN(e1, ..., eN, (fun (pat1, ..., patN) -> ... )

2. Otherwise, a `MergeSources` method must be present on the builder.  Optionally, `MergeSources3`, `MergeSources4` .. may also present on the builder up to some `MergeSourcesM` (M for Max). If `N <= M` the expression is de-sugared to:

       builder.Bind(builder.MergeSourcesN(e1, ..., eN), (fun (pat1, ..., patN) -> ... )

   If `N > M` an appropriate set of `MergeSources` calls is made, e.g. for N = 7 and M = 5

       builder.Bind(builder.MergeSources5(e1, e2, e3, e4, builder.MergeSources3(e5, e6, e7)), (fun (pat1, ..., pat4, (pat5, pat6, pat7)) -> ... )


Next, consider any `let! pat = expr in innerComp`. If the `innerComp` is an immediately-returning computation, i.e.

1. an immediate `return`

2. a conditional `if e1 then e2 else e3` where `e2` and `e3` are both immediate `return`

3. a sequential expression `e1; e2` where `e2` is an immediate `return` 

4. a `match` where each target is an immediate return

then the expression becomes `builder.BindReturn(source, (fun pat -> innerExpr))` where `innerExpr` is the inner computation with `return` removed.

Likewise, apply a corresponding rule to produce calls to `Bind2Return`, `Bind3Return` and so on from `and!` expressions whose inner computation is an imediately-returning computation.


## Notes

* A summary of the relevant new builder methods:

  * `MergeSources` - gives `and!` support for arbitrary number of `and!`  via tupling nodes
  * `MergeSources3`, `MergeSources4`... - optional, reduce number of tupling nodes

  * `Bind2`, `Bind3` etc. - optional, binds `let! .. and! ...` efficiently without tupling nodes

  * `BindReturn` - adds support and/or efficiency for `let! ... return` 
  * `Bind2Return`, `Bind3Return`, etc. - optional, binds `let! .. and! ... return` efficiently without tupling nodes

* Allowing a de-sugaring `and!` to direct calls to overloaded `Bind2`, `Bind3` methods etc. allows for more efficient implementations
  of dependency graphs avoiding needless merging and unmerging of inputs for the most common cases.

* Allowing a de-sugaring of `and!` to `MergeSources` etc. in other cases allows for an arbitrary number of `let! ... and! ...` while maintaining 
  type safety.  The upper limit of 5 simultaneous merges is arbitrary but aligned with the design choices in other parts of the F# design.

* Allowing a de-sugaring of bind-return patterns to `BindReturn` etc. allows for complete elimination of binds when forming static 
  computation graphs, with no execution of binds nor reallocation of nodes.

* The consuming pattern of Bind2 and others are tuples, but the de-sugaring does not commit to be either a struct tuple or reference tuple.

Sample signatures:
```fsharp
type ApplicativeBuilder() =
    inherit TraceCore()

    // Fundamental methods

    // The standard Return
    member builder.Return(x: 'T) : M<'T> = ...

    // The standard Bind
    member builder.Bind(x1: M<'T1>, f: 'T1 -> M<'T2>) : M<'T2> = ...

    // If you have Bind, then BindReturn can be added for performance
    //
    // If you don't have Bind, then adding BindReturn allows a single let! bind followed by a return,
    // i.e. an applicative.
    member builder.BindReturn(x: M<'T1>, f: 'T1 -> 'T2) : M<'T2> = ...

    // If you have Bind or BindReturn, MergeSources adds support for `and!`.  Struct tuples can be used
    member builder.MergeSources(x1: M<'T1>, x2: M<'T2>) : M<'T1 * 'T2> = ...

    // Performance optimizations
    
    // If you have MergeSources, then a MergeSources3 can be added for performance 
    member builder.MergeSources3(x1: M<'T1>, x2: M<'T2>, x3: M<'T3>) : M<'T1 * 'T2 * 'T3> = ...

    // If you have MergeSources, then a MergeSources4 can be added for performance 
    member builder.MergeSources4(x1: M<'T1>, x2: M<'T2>, x3: M<'T3>, x4: M<'T4>) : M<'T1 * 'T2 * 'T3 * 'T4> = ...

    // If you have MergeSources, then a Bind2 can be added for performance
    member builder.Bind2(x1: M<'T1>, x2: M<'T2>, f: 'T1 * 'T2 -> M<'T3>) : M<'T3> = ...

    // If you have BindReturn and MergeSources, then Bind2Return can be added for performance
    member builder.Bind2Return(x1: M<'T1>, x2: M<'T2>, f: 'T1 * 'T2 -> 'T3) : M<'T3> = ...

    // If you have BindReturn and MergeSources, then Bind3Return can be added for performance
    member builder.Bind3Return(x1: M<'T1>, x2: M<'T2>, x3: M<'T3>, f: 'T1 * 'T2 * 'T3 -> 'T4) : M<'T4> = ...
```

## Interaction with Custom Operators

In F# computation expressions a custom operator with `MaintainsVariableSpaceUsingBind=true`

    b { ... part1; op; part2 }

is processed as if it were the following

    b { let! varspace = op (b { ... part1; return varspace })
        part2 }

The details of this are in the F# language specification.

The  `... part1; return varspace` is subject to the extra processing specified in this RFC. For example, it is
eligible to become a `BindReturn` if `part1` is a `let! .. and! ...`.

Likewise, the `let! varspace = ... in part2` is also subject to the extra processing specified in this RFC.  For example, it is eligible to
become a `BindReturn` if `part2` is a simple `return`.

An example is given in [the accompanying design note](https://github.com/dsyme/fsharp-presentations/blob/master/design-notes/rethinking-applicatives.md)


# Discussion and Examples

## Dependency/Calculation graphs

Consider a typical dependency graph implementation (this is a sketch, see [the example in the PR](https://github.com/dotnet/fsharp/pull/7756/files#diff-e77ad28a19cb44b6c0d896f17fac41f6))

```fsharp
type Node<'T> =
    /// Evaluate the node if it is out of date and cache the value
    member Value: 'T

    /// The nodes that depend on this node
    member Dependents: Node list
    
type Input<'T> =
    member Node: Node<'T>
    member SetValue: 'T -> unit

type NodeBuilder

let node = NodeBuilder()
```
Now  consider these:
```fsharp
let inp1 = Input(3)
let inp2 = Input(7)
let inp3 = Input(0)

let test1 = 
    node { 
        let! v1 = inp1.Node
        and! v2 = inp2.Node
        and! v3 = inp3.Node
        return v1 + v2 + v3
    }
   // Equivalent to
   //   node.Bind3Return(inp1.Node, inp2.Node, inp3.Node, (fun (v1, v2, v3) ->
   //      v1 + v2 + v3))

let test2 = 
    node { 
        let! v1 = inp1.Node
        let! v2 = inp2.Node
        let! v3 = inp3.Node
        return v1 + v2 + v3
    }
   // Equivalent to 
   //   node.Bind(inp1.Node, (fun v1 ->
   //     node.Bind(inp2.Node, (fun v2 ->
   //       node.Bind(inp3.Node, (fun (v1, v2, v3) -> v1 + v2 + v3))   
```
and a load such as
```
for i in 1 .. 1000 do
  inp1.Value <- 4 
  let v2 = test2.Value // recompute

  inp2.Value <- 10
  let v3 = test2.Value // recompute
  ()
```
Then a typical performance difference is:
```
total recalcs using and! = 2000
total nodes using and! = 1

total recalcs using let! = 5000
total nodes using let! = 7000
```
Note that the approach incorporating `and!`is much, much more efficient. Furthermore, the actual calculation graph is
**static** rather than dynamic - no new nodes are allocated during recalc execution.


## Observables 

As an example, with this new syntax, [Pauan points out](https://github.com/fsharp/fslang-suggestions/issues/579#issuecomment-310799948) that we can write a convenient and readable computation expression for `Observable`s that acts similarly to [`Observable.zip`](http://fsprojects.github.io/FSharp.Control.Reactive/tutorial.html#Observable-Module), but [avoids unnecessary resubscriptions and other overheads associated with `Bind`](https://github.com/fsharp/fslang-suggestions/issues/579#issuecomment-310854419) and syntactically scales nicely with the number of arguments whilst admitting arguments of different types.

Some examples, assuming an appropriate definition of `observable`:

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

### Other applicatives 

[Applicative functors](https://en.wikipedia.org/wiki/Applicative_functor) (or just "applicatives", for short) have been growing in popularity as a way to build applications and model certain domains over the last decade or so, since McBride and Paterson published [Applicative Programming with Effects](http://www.staff.city.ac.uk/~ross/papers/Applicative.html). Applicatives are now reaching a level of popularity within the community that supporting them with a convenient and readable syntax, as we do for monads, makes sense.

With _applicative_ computation expressions, we can write more computations with this convenient syntax than before (there are more contexts which meet the requirements for applicative computation expressions than the existing monadic ones), and we can write more efficient computations (the requirements of applicatives rule out needing to support some potentially expensive operations).

In some cases, applicative computation expressions have no useful `bind` at all except where the result is an immediate
`return`.  In other cases, the use of a true `bind` may represent a "dynamic" parts of a computation graph, and the
use of `let! .. and! ...` may represent the "static" part of a computation graph.

Applicatives can be implemented by adjusting the types for `Bind` and `Return`. This restricts the CE to one large multi-bind, followed by a simple return.

```fsharp
type Applicative() =

    member x.MergeSources(a : option<'a>, b : option<'b>) =
        (a,b) ||> Option.map2 (fun a b -> (a,b))

    // NOTE: the Bind is really a `Map` as the continuation returns 'b instead of <'b>
    member x.Bind(m : option<'a>, mapping : 'a -> 'b) : option<'b> =
        m |> Option.map mapping

    // NOTE: the Return doesn't return M<'T>
    member x.Return v = v

let app = Applicative()
```

Now, with this typing, the following is allowed:

```fsharp
let test (a : option<int>) (b : option<int>) =
    app {
        let! a = a
        and! b = b
        // Similar to this:
        // let! (a, b) = app.CombineSources(a, b)
        let x = b * b + a
        return a * b + x
    }
```

But this is not:

```fsharp
let test (a : option<int>) (b : option<int>) =
    app {
        let! a = a
        let! b = b
        let x = b * b + a
        return a * b + x
    }
```


## Examples of useful applications

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
    let! (ActivePattern(x)) = foo ✔️
    and! (y,_)            = bar ✔️
    and! (SingleCaseDu z) = baz ✔️
    return x + y + z
 }
```

TBD: there are other syntactc forms that are valid, these need to be listed

# Drawbacks
[drawbacks]: #drawbacks

* Additional design complexity

# Alternative Designs
[alternative-designs]: #alternative-designs

The original design was based on a highly constrained form of applicative and an `Apply` de-sugaring.  

The original design supported `use!` or `anduse!` via a `ApplyUsing` method.
This was removed partly because of complexity, and partly because `builder.MergeSources(...)` gives no particular place
to put the resource reclamation. Also, the exact guarantees about when the resource reclamation protection is
guaranteed are not entirely easy to ascertain and [can result in resource leaks](https://github.com/fsharp/fslang-design/commit/1392717f4a1d9cc74db5c6e9036fe625f8aee532#diff-ace7d4450b2d38b03ffd531603d424a8L779).
Removing this forces the user to either have a `Using` method with `use!`, or to write more explicit code making one
or more explicit calls to functional combinators, e.g.

```fsharp
ce {
    use! r1 = computeResource1() // Requires 'Using'
    let! v2 = computeValue2()
    return resource1.Value + value2
 }
```
rather than
```fsharp
ce {
    use! r1 = computeResource1()
    and! v2 = computeValue2()
    return resource1.Value + value2
 }
```

We chose not to support `do!` or `anddo!` in place of a `let! _ = ...` or `and! _ = ...` (respectively), since `do!` implies side-effects and hence sequencing in a way that applicatives explicitly aim to avoid (see the parallelism example earlier). These keywords and their corresponding translations could be introduced in a later addition to the language, if the community's position changed on the matter.

[Tomas Petricek's Joinads](http://tomasp.net/blog/fsharp-variations-joinads.aspx/) offered a superset of the features proposed in this RFC, but was [rejected](https://github.com/fsharp/fslang-suggestions/issues/172) due to its complexity. The above proposal is of much smaller scope, so should be a much less risky change.

Various attempts have been made to attempt to get the benefits of applicatives within the existing syntax, but most end up involving confusing boilerplate, and make it easy to provide arguments in the wrong order because they cannot be named (in contrast to `let! ... and! ... return ...` which forces each argument to be named right next to its value in order to be used inside the `return`). It tends to be the case that even the authors of these experiments consider them abuses of the existing language features and recommend against them.


# Compatibility
[compatibility]: #compatibility

## Is this a breaking change?

No

# Unresolved Questions
[unresolved]: #unresolved-questions

None


