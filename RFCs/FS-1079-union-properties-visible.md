# F# RFC FS-1079 - Make .Is* discriminated union properties visible from F# #

The design suggestion [Make .Tag and .Is* discriminated union properties visible from F#](https://github.com/fsharp/fslang-suggestions/issues/222) has been marked "approved in principle".

This RFC covers the detailed proposal.

# Summary

F# discriminated unions have a number of generated public members that were created for C# interoperability and are hidden from F# code. These include an instance property `IsFoo : bool` per case `Foo`.

This proposal is to make these members visible and usable from F# code too.

# Motivation

It is sometimes useful to simply determine whether a value of a discriminated union is an instance of a given case, without needing to do anything more with its arguments. For example:

```fsharp
type Contact =
    | Email of address: string
    | Phone of countryCode: int * number: string

type Person = { name: string; contact: Contact }

let canSendEmailTo person =
    match person.contact with
    | Email _ -> true
    | _ -> false
```

Considering that F# already generates a member `IsEmail : bool` which does exactly the same as the above, it would make sense to make it available to F# developers so that the example becomes:

```fsharp
let canSendEmailTo person =
    person.contact.IsEmail
```
As you can see, the use of `IsEmail` is far more succinct for this use case, obviating the need for a pattern match just to check a single thing.

# Detailed design

The compiler generates the following public members for every discriminated union type:

1. A property per case named `IsFoo : bool` for a case named `Foo`. This property is true if `this` is an instance of `Foo` and false otherwise.

2. A property `Tag : int` whose value identifies the case: it is `0` if `this` is an instance of the first case (in type declaration order), `1` if `this` is an instance of the second case, and so on.

3. A nested static type `Tags`.

4. Inside `Tags`, a constant integer static field per case, with the same name as the case. Its value is the same as the `Tag` property of an instance of the corresponding case.

5. Either a static property `Foo` or a static method `NewFoo` per case named `Foo`, depending on whether `Foo` has arguments or not, which constructs an instance of this case.

In this design, members #1 (`Is*`) are made available to F# code as well. #2 through #5 are kept hidden from F#:

* The arguments for or against exposing `Tag` are discussed in [Alternatives](#alternatives).

* The `Foo` / `NewFoo` members are equivalent to the corresponding case constructor and would be redundant and confusing.

Here is an example union type declaration with all of the newly F#-visible members listed explicitly (in pseudo-code, since nested `type` declarations are invalid F#):

```fsharp
type Example =
    | First
    | Second of int * string
    
    // The following members were already generated, and are now visible to F#:
    
    member IsFirst : bool
    
    member IsSecond : bool

    // The following members were already generated, and remain hidden from F#:
    
    member Tag : int
    
    type Tags =
    
        val First : int
        
        val Second : int
    
    static member First : Example
    
    static member NewSecond : int * string -> Example
```

# Drawbacks

These attributes might incite beginners to check a value's case using a series of `if x.IsFoo then ... elif x.IsBar then ...` in situations where pattern matching would be more appropriate.

# Alternatives

An alternate design would be to also expose the `Tag` property and `Tags` nested type (#2 through #4 as listed in [Detailed design](#detailed-design)). They can be useful in some limited situations, but are left out of the main proposal for several reasons:

* They are more rarely useful than `Is*`.

* They would be even more prone to confusing beginners, by inciting them to match against `x.Tag` instead of `x`.

* Exposing `Tag` and `Tags` changes whether reordering the cases of a union type declaration is a breaking change:

    |                                     | Before this change   | After this change   |
    | :---------------------------------- | :------------------: | :-----------------: |
    | Binary compatible for F# consumer   | No                   | No                  |
    | Source compatible for F# consumer   | **Yes**              | **No**              |
    | Binary compatible for C# consumer   | No                   | No                  |
    | Source compatible for C# consumer   | No                   | No                  |

# Compatiblity

* Is this a breaking change?

No. It was already illegal to explicitly declare members whose name clashes with these generated members.

It does however change what constitutes a breaking change in F# code (see Drawbacks above).

* What happens when previous versions of the F# compiler encounter this design addition as source code?

It fails to compile any invocations of the generated members.

* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?

They see a normal discriminated union, and do not allow invoking its generated members.

* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?

It is not directly an extension to FSharp.Core, but it does apply to union types declared in FSharp.Core: `option`, `Result`, `Choice`. Previous versions of the F# compiler do not allow invoking the generated members on these types.

# Unresolved questions

N/A
