# F# RFC FS-NNNN - Create

The design suggestion [FILL ME IN](https://fslang.uservoice.com/forums/245727-f-language/suggestions/fill-me-in) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [ ] Approved in principle
* [x] [User Voice Request](https://fslang.uservoice.com/not-applicable)
* [ ] Details: [under discussion](https://github.com/fsharp/FSharpLangDesign/issues/FILL-ME-IN)
* [ ] Implementation: [In progress](https://github.com/Microsoft/visualfsharp/pull/FILL-ME-IN)


# Summary
[summary]: #summary

A F# Quirks Repo should be made to capture unexpected or buggy behaviour we may come across while using FSharp Interactive and/or the FSharp compiler.  

Typically, these quirks are of a minor nature, and not worth bothering the community with through User Voice. However, this database could also be used to have type checked examples which the User Voice forum could cross reference too. Early supporting exploration code might back up user voice questions too.

Further, unit tests could be devised to grade different releases of the F# Compiler / F# Interactive. An ECMA style JavaScript compatibility table comes to mind... but that's probably another request. It would be good to verify if we're improving over time. 

# Motivation
[motivation]: #motivation

New F# users eventually get past teething issues and find out how to disambiguate, and so on. However, once a habit is formed, we lose the opportunity to sand off rough edges in the code authoring process. New users should be encouraged to speak up about things that don't make sense. It's possible they just don't make sense.

Additionally, advanced users from other functional languages may expect that certain constructs should work out of the box. Perhaps they should. Perhaps they shouldn't give a slightly different philosophy. Either way, let's try to capture these too.

The last category of things this database might cover is inconsistencies in the language which may not be apparent on the surface.

Why is this useful? Well, those of us working with the AST may, if we are aware of quirks, notice the cause, and from time to time, come up with small fixes and nicer warning. If we're not aware of these things (because we have formed habits of our own), then we'll probably gloss over the information in front of our eyes. That's the experiment. That's the process we're trying to improve by building this.

Here is a taster for things we might cover:

Example:
Question: what should x equal? Surely this should not happen...

let Some(x,y) = Some(1,2) 
x = Some(1,2) // surely this should not happen
              // ie. x = 1 and y = 2

resolution:

Use parens to disambiguate:
 
let (Some(x,y)) = Some(1,2) 


# Detailed design
[design]: #detailed-design

We need to design folders and conventions for the different. 

# Drawbacks
[drawbacks]: #drawbacks

It takes more work to check code in to support a User Voice request, but this could be optional... or could be done by those of us who are researching requests and issues.

# Alternatives
[alternatives]: #alternatives

What other designs have been considered? 

None.

What is the impact of not doing this? 

We'll just continue on as per normal, and pass on this chance to improve the process.

# Unresolved questions
[unresolved]: #unresolved-questions

What parts of the design are still TBD? TBD
