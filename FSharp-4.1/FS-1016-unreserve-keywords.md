# F# RFC FS-1016 - Revise the Reserved Keyword List

The design suggestion [Revise the reserved keyword list](https://fslang.uservoice.com/forums/245727-f-language/suggestions/7006663-revise-the-reserved-keyword-list-e-g-remove-met) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] Details: [discussion](https://github.com/fsharp/FSharpLangDesign/issues/102)
* [x] Implementation: [Completed](https://github.com/dotnet/fsharp/pull/1279)


# Summary
[summary]: #summary

We have had requests to revise the reserved keyword list, e.g.

> The keyword "method" is currently reserved for future use. In many web programming scenarios "method" refers to the HTTP method (GET/POST) and the fact that you currently have to escape the keyword and write ``method`` is annoying. Calling function parameter "meth" instead works (though some people may find this an unfortunate naming ;-)), but it is against F# coding guidelines. And furthermore, this only helps when you're defining the API, but sometimes you need to consume C# API that already has "method" as optional argument. 
> I think that the F# community is quite happy with using the name "member" for declaring all members (including methods, properties and events) and I don't really think there is a need for keeping "method" reserved.


# Motivation
[motivation]: #motivation

The motivation is to make sure that users hit the "reserved keyword" warning less often.

# Detailed design
[design]: #detailed-design

Unreserve the following keywords:

    method  - the F# community are happy with 'member' to introduce methods
    constructor - the F# community are happy with 'new' to introduce constructors
    atomic - this was related to the fad for transactional memory circa 2006. In F# this would now be a library-defined computation expression
    eager - this is no longer needed, it was initially designed to be "let eager" to match a potential "let lazy"
    object  - there is no need to reserve this
    recursive  - F# is happy using "rec"
    functor  - If F# added parameterized modules, we would use "module M(args) = ..."
    measure  - There is no specific reason to reserve this these days, the [<Measure>] attribute suffices
    volatile - There is no specific reason to reserve this these days, the [<Volatile>] attribute suffices



# Drawbacks
[drawbacks]: #drawbacks

The main drawback is simply the cost of making the change and the churn in the language spec

# Alternatives
[alternatives]: #alternatives

We could just not do it.  But it seems to make sense to revise this now several years have passed since the reservations were made

# Unresolved questions
[unresolved]: #unresolved-questions

None
