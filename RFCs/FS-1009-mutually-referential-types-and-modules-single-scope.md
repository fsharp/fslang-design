# F# RFC FS-1009 - Allow mutually referential types and modules in a closed scope 

The design suggestion [Allow mutually referential types and modules in a closed scope in a single file](https://fslang.uservoice.com/forums/245727-f-language/suggestions/11723964-allow-types-and-modules-to-be-mutually-referential) has been marked "under review". This RFC covers more detailed information about this suggestion.

* [x] Under review
* [ ] Details: Under review
* [ ] Implementation: Prototyping


# Summary
[summary]: #summary

Allow a collection of both types and modules within a single scope in a single file to be mutually referential.
The exact mechanism used to declare the scope of mutual reference is TBD, but one possibility is
to use a delimited section as follows:

```fsharp
namespace Foo

#beginrec

type X = ... refer to Y and Z...

type Y = .... refer to X and Z...

module Z = ... refer to X and Y ...

#endrec
```

or just

```fsharp
namespace Foo

#rec

type X = ... refer to Y and Z...

type Y = .... refer to X and Z...

module Z = ... refer to X and Y ...
```



# Motivation
[motivation]: #motivation

In F# mutual reference is relatively de-emphasized and scoped, but not banned. In F# 1.0-4.0 the situation is as follows:

* A group of types can be mutually referential using "type X = ... and Y = ... "
* A group of functions and values can be mutually referential using "let rec f x = ... and g x = ..."

This works well for the majority of purposes and sets the defaults in the right way, resulting
in [code that has low circularity and generally avoiding "spaghetti code"](http://fsharpforfunandprofit.com/posts/cycles-and-modularity-in-the-wild/).
However, it places an artificial restriction on the kinds of mutual recursion permitted:

* types and modules can't currently be mutually referential, even when the modules only contain function definitions that support the functionality
  available on the type's members.  This leads to the artificial use of extra type to hold static functions and
  values, and can discourage the use of module-bound code. This can be worse than awkward on the occasions
  that mutual reference is used.

* ``exception`` declarations can't currently be mutually referential with anything.

Note that this proposal doesn't lift the requirement to have a file order and doesn't extend mutually-referential scopes
across more than one closed scope within one file.

There is some syntactic awkwardness when mutually refential types have attributes, e.g.

```fsharp
[<Struct>]
type X = ...

and [<Struct>] Y = ...
```

is awkward compared to 

```fsharp
[<Struct>]
type X = ...

[<Struct>] 
type Y = ...
```




# Detailed design
[design]: #detailed-design

### Basic design suggestion

See above.  Note that 

```fsharp
let rec f x = ...
and g x = ...
```

would be equivalent to

```fsharp

#beginrec

let f x = ...
let g x = ...

#endrec
```


### Allowed declarations

In the current investigations, types, modules, ``exception`` and ``let`` bindings can be mutually referential.

``open`` declarations can't currently be included in mutually referential groups, see below.

Other ``#`` declarations (e.g. ``#nowarn`` declarations) can't currently be included in mutually referential groups, see below.

Namespace implementation fragments can't be included in mutually referential groups, e.g. this is not allowed:

```fsharp
#rec

namespace N1


namespace N2
```

### Inference

Inference would be as "one big mutually referential group", just as "type Y = ... and Z = ..." today.

### Initialization 

Execution of initialization code would be as an initialization graph, just as "static let" today.

### Feature Interactions

Based on a detailed prototype, the following feature interactions have been identified

#### Interaction with type realization 

"Realizing" or "Loading" types is a non-trivial part of the F# checking process. The type realization logic (in TypeChecker.fs)
is multi-pass, first establishing the "kinds" of the types being defined, then their representation (union, record etc.), then
values for their public-facing members, then doing type checking of implementations and members.  This process needs to be very
carefully adjusted to account for module definitions, which may in turn hold nested type definitions: essentially
"type realization" becomes "type and module realization".  Conceptually the
process remains much as before, but the code now works with a more sophisticated tree of declarations.

#### Interaction with ``open`` and type/module realization 

There is an open question as to whether ``open`` declarations should be available in mutually referential groups.

#beginrec

open M

type C() = ...

module M = ...

#endrec

While logically speaking possible to implement,
allowing ``open`` needs care: progressively more contents of a module or namespace
become available at each stage of realization.  Prior to this suggestion type realization did not need to
process ``open`` declarations.

One example where a specific interaction may exist is the way
that ``open`` makes both F# and C# extension methods available.  So cases such as

```fsharp

#beginrec

open M

type C() = ...

module M = 
    type C with 
        member c.P = 1
        ...
#endrec
```

#### Interaction with `#` directives

TBD


# Drawbacks
[drawbacks]: #drawbacks

There is understandably some resistance to making writing mutually referential code easier in F#: see
for example the original comment [here](https://fslang.uservoice.com/forums/245727-f-language/suggestions/11723964-allow-types-and-modules-to-be-mutually-referential).
Existing F# users find it clarifying and simplifying to have both a file order and to have minimized mutually-referential scopes.
However, creating closed sets of mutually recursive types (including static methods and data)
is part of the F# language design: the intent has never been to adopt an extreme position where all mutual reference is banned.
The current design is awkwardly limiting in the way that only types can be mutually referential.

Were a proposal along these lines to be implemented, F# will continue to de-emphasize mutual references by default. The question, as always, is finding the right balance.

# Alternatives
[alternatives]: #alternatives

Many options have been considered, though discussion is welcome:
1. Not do anything.
2. Allow full mutual recursion across all files in an assembly.
3. Variations on the syntax used to declare mutual referentiality. e.g. Use ``module rec M = ...`` and ``namespace rec N = ...``.  (these options would mediate all recursion via a module or namsespace name)
4. Require a forward signature on a module

# Unresolved questions
[unresolved]: #unresolved-questions

There are many parts of this design still under investigation.  As issues are identified they will be noted in this RFC.

