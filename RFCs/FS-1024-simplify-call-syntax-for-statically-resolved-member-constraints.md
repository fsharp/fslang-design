# F# RFC FS-1024 - Simplify call syntax for statically resolved member constraints

The design suggestion [Simplify call syntax for statically resolved member constraints](https://github.com/fsharp/fslang-suggestions/issues/440) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] [User Voice Request](https://github.com/fsharp/fslang-suggestions/issues/440)
* [ ] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/162)
* [x] Implementation: [started](https://github.com/Microsoft/visualfsharp/pull/4726)


# Summary
[summary]: #summary

Member constraint invocations are syntactically cumbersome:

```fsharp
type Example() =
  member __.F(i) = printfn "%d" i

let inline f (x : ^a) =
  (^a : (member F : int -> unit) (x, 1))
  (^a : (member F : int -> unit) (x, 2))
```

The idea is to simplify the calls to look like "dot" member invocation:

```fsharp
let inline f (x : ^a when ^a : (member F : int -> unit)) =
  x.F(1)
  x.F(2)
```
Note that in the signature, the member constraint syntax remains unchanged.

A similar suggestion can be made for static member constraint invocations. This is not in the user voice suggestion, but extrapolating - the following

```fsharp
let inline inc< ^T when ^T : (static member Inc : int -> int)> i =
    ((^T) : (static member Inc : int -> int) (i))
```
could be simplified to:

```fsharp
let inline inc< ^T when ^T : (static member Inc : int -> int)> i =
    T.Inc i
```

# Motivation
[motivation]: #motivation

This improves readability. In cases where there are multiple calls, it removes unnecessary duplication of the constraint.

It brings the syntax in line with existing syntactic support for other type constraints - for example if a type is constrained to have a default constructor you can call `new` to construct it: `let f<'T when 'T : (new : unit ->'T)> () = new 'T()`

# Detailed design
[design]: #detailed-design

Detailed design to be determined.

# Drawbacks
[drawbacks]: #drawbacks

No significant drawbacks have been identified so far.

# Alternatives
[alternatives]: #alternatives

No design alternatives have been considered so far.

A typical workaround currently is to abstract the member constraint invocation in a separate function, and then call that function instead of using the member constraint invocation syntax over and over.

# Unresolved questions
[unresolved]: #unresolved-questions

* Should we allow the similarly simplified syntax for static members? Worthwhile to note is that this already exists for operators:
```fsharp
let inline add a b = a + b
```
infers a `+` member constraint. Also it's not clear whether for a static member constraint, the syntax proposed above is desirable - should it be `^T.Inc i` instead? That is probably new syntax, while the proposed syntax `T.Inc i` may need to bring `T` in scope as a class somehow (and possibly shadow real classes called `T`). Strictly speaking this breaks backwards compatibility - code that called `Inc` on a class called `T` in scope would now call `Inc` on the type parameter `T` - though chances of this occurring in the wild seem very slim.
