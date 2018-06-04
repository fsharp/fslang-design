# F# RFC FS-1037 -  Allow Union Constructor to deal with Flexible Types

An F# union type constructor doesn't act like a function with respect to subtyping, see https://github.com/fsharp/fsharp/issues/660

After brief discussion in issue https://github.com/fsharp/fsharp/issues/660 Don Syme says:

> @liboz I don't believe there's a specific reason to disallow this. In theory allowing it may be a breaking change, as it allows more general types to be inferred in some situations. Normally this is not a problem but perhaps the value restriction can some into play.


* [x] Approved in principle
* [x] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/219)
* [x] Implementation: [In progress](https://github.com/Microsoft/visualfsharp/pull/2382)


# Summary
[summary]: #summary

Currently the Union Constructor has inconsistencies dealing with flexible types. 

For example:

```type Foo = Items of seq<int>```

This works:

```Items [1;2;3]```

However, this does not work:

```[1;2;3] |> Items```

with the error that:

```
Type mismatch. Expecting a
    int list -> 'a    
but given a
    seq<int> -> Foo
 ```
    
# Motivation
[motivation]: #motivation

The above error message is very confusing since an ```int list``` is a subset of ```seq<int>```. Furthermore, this issue is specific to the Union Constructor and a regular function deals with flexible types in the above manner with no issues (i.e. an function that is an alias of the Union Constructor has no problem with the above scenario).

# Detailed design
[design]: #detailed-design

Enable the Union Constructor to accept flexible types whereas before it did not.

# Drawbacks
[drawbacks]: #drawbacks

This could be a breaking change by allowing more general types to be inferred and there could be a performance hit. Furthermore, the following test is in the code base, which appears to explicitly test against the specific scenario, perhaps indicating a potential problem with allowing Union Constructors to deal with flexible types.

```
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

TBD
