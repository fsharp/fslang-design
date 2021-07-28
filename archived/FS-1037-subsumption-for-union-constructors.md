# F# RFC FS-1037 - Subsumption for Union Constructors

An F# union type constructor doesn't act like a function with respect to subtyping, see https://github.com/fsharp/fsharp/issues/660

After brief discussion in issue https://github.com/fsharp/fsharp/issues/660 Don Syme says:

> @liboz I don't believe there's a specific reason to disallow this. In theory allowing it may be a breaking change, as it allows more general types to be inferred in some situations. Normally this is not a problem but perhaps the value restriction can some into play.


* [x] Approved in principle
* [x] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/219)
* [x] Implementation: [Complete](https://github.com/Microsoft/visualfsharp/pull/5030)

# Summary
[summary]: #summary

Historically, the Union Constructor has inconsistencies dealing with flexible types. 

For example:

```fsharp
type Foo = Items of seq<int>
```

This works:

```fsharp
Items [1;2;3]
```

However, this does not work:

```fsharp
[1;2;3] |> Items
```

with the error:

```
Type mismatch. Expecting a
    int list -> 'a    
but given a
    seq<int> -> Foo
 ```
    
# Motivation
[motivation]: #motivation

The above error message is very confusing since an `int list` is a subset of `seq<int>`. Furthermore, this issue is specific to the Union Constructor. A regular function deals with flexible types in the above manner with no issues. For example, a function that is an alias of the Union Constructor has no problem with the above scenario, which makes the limitationr ather awkward.

# Detailed design
[design]: #detailed-design

Enable the Union Constructor to accept flexible types whereas before it did not.

# Drawbacks
[drawbacks]: #drawbacks

This could be a breaking change by allowing more general types to be inferred and there could be a performance hit. Furthermore, the following test is in the code base, which appears to explicitly test against the specific scenario, perhaps indicating a potential problem with allowing Union Constructors to deal with flexible types.

```fsharp
type A() = 
    member x.P = 1

type B() = 
    inherit A()
    member x.P = 1

type Data = Data of A * A


let data (x,y) = Data (x,y)
let pAA = (new A(),new A()) 
let pBB = (new B(),new B())
let pAB = (new A(),new B())
let pBA = (new B(),new A())
pBB |> Data   // not permitted (questionable)
pAB |> Data   // not permitted (questionable)
pBA |> Data   // not permitted (questionable)
pBB |> data   // permitted
pAB |> data   // permitted
pBA |> data   // permitted
```

# Alternatives
[alternatives]: #alternatives

Use a function as an alias of the union constructor.

# Unresolved questions
[unresolved]: #unresolved-questions

None.
