# F# RFC FS-1009 - Allow mutually referential types and modules in a closed scope 

The design suggestion [Allow mutually referential types and modules in a closed scope in a single file](https://fslang.uservoice.com/forums/245727-f-language/suggestions/11723964-allow-types-and-modules-to-be-mutually-referential) has been marked "under review". This RFC covers more detailed information about this suggestion. Please [discuss this RFC using the corresponding issue](https://github.com/fsharp/FSharpLangDesign/issues/76).

* [x] Under review
* [ ] Details: [Under discussion](https://github.com/fsharp/FSharpLangDesign/issues/76)
* [ ] Implementation: [Prototyping](https://github.com/Microsoft/visualfsharp/compare/master...dsyme:mutrec)


# Summary
[summary]: #summary

Allow a collection of both types and modules within a single scope in a single file to be mutually referential.
The exact mechanism used to declare the scope of mutual reference is TBD, but one possibility is
to use a ``rec`` declaration on a namespace and/or module as follows:

```fsharp
namespace rec MyFramework

type X = ... refer to Y and Z...

type Y = .... refer to X and Z...

module Z = ... refer to X and Y ...

```


Note that this proposal doesn't do a number of things that you might think:
* it doesn't change the requirement to have a file order in F# compilation
* it doesn't allow mutually-referential scopes across multiple files
* it doesn't make declarations independent of ordering: both type inference and initialization are still order-specific 

The proposed change is non-breaking, it is an optional extension to the language. It is not expected that this would be the norm for F# development.


# Motivation
[motivation]: #motivation

In the F# language design, mutual reference between declarations is relatively
de-emphasized, but not banned. In F# 1.0-4.0 the situation is as follows:

* A group of functions and values can be mutually referential using "let rec f x = ... and g x = ..."
* A group of types can be mutually referential using "type X = ... and Y = ... "

This works well for the majority of purposes and sets the defaults "the right way", resulting
in code that has low circularity and
[generally avoiding "spaghetti code"](http://fsharpforfunandprofit.com/posts/cycles-and-modularity-in-the-wild/).
However, it places some artificial restriction on the kinds of mutual recursion permitted which can be frustrating
and lead to difficult corners in F# programming:

* Types and modules can't currently be mutually referential at all, even when the modules only contain 
  function definitions that support the functionality
  available on the type's members.  This leads to the artificial use of extra types to hold static methods and
  static values, and this can discourage the use of module-bound code, or may discourage the appropriate use of object-oriented
  features to give an API for functional language code.

```fsharp
type Plane() = 
    member x.StartTheEngines() = ... PlaneHelpers.start x

module private PlaneHelpers = 
    let start(x:Plane)=  ... // note, the module is mutually refential with the type
```

* This problem is particularly apparent when a module is used to contain helpers for implementations of interfaces and
  virtual members on class types, e.g. ``IComparable``, ``ToString``, ``GetHashCode`` and do on.

* F# ``exception`` declarations can't currently be mutually referential with anything.  This is awkward when
  exceptions are raised in members of a type ``C`` and must carry data of type ``C``.

```fsharp
exception Error of Plane

type Plane() = 
    member x.StartTheEngines() = ... raise (NoFuelInPlance x)
```

* There is also some syntactic awkwardness when mutually refential types have attributes, e.g.

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
module M = 
  let rec f x = ...
  and g x = ...
```

would be equivalent to

```fsharp
module rec M = 
  let f x = ...
  let g x = ...

```

The ``rec`` is allowed on ``namespace rec ... `` and ``module rec ...`` declarations.

### Allowed declarations in mutually referential declaration groups


In the current investigations, types, modules, ``exception`` and ``let`` bindings can be mutually referential.

``open`` declarations can't currently be included in mutually referential groups, see below.


Namespace implementation fragments can't yet be included in mutually referential groups, e.g. in the following N2 can
see N1 but not vice-versa.

```fsharp

namespace rec N1


namespace rec N2
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

There is an open question as to whether ``open`` declarations should be available in mutually referential groups,
and if they are whether they can refer to modules being defined. Consider
for example:

```fsharp
#beginrec

open M

type C() = ...

module M = ...

#endrec
```

While logically speaking possible to implement, allowing ``open`` on modules being defined 
needs care and can result in code that is really
non-trivial to understand. At the technical level, progressively more contents of a module or namespace
become available at each stage of realization (prior to this suggestion type realization did not need to
process ``open`` declarations).

The current prototype restricts ``open`` declarations to only be at the top of mutually-referential 
module and namespace declaration groups, and ``open`` declarations may not refer to modules being
defined.  However this will likely be changed to allow references to modules being defined.

One example where a specific interaction may exist is the way
that ``open`` makes both F# and C# extension methods available.  So cases such as

```fsharp

module rec Example = 
  open M

  type C() = ...

  module M = 
      type C with 
          member c.P = 1
          ...
```

#### Meaning of ``rec``

The meaning of ``rec`` is that _all_ type, function, member and value declarations in all nested modules
may be mutually referential.  For example:

```fsharp

namespace rec NS = 
   //   These names resolve to types in this scope:
   //       NS.C, NS.D, NS.M.E, NS.M.F
   //       C, D, M.E, M.F
   //   These names resolve to functions in this scope:
   //       NS.M.x1, NS.M.x2, NS.M.N.y1, NS.M.N.y2, 
   //       M.x1, M.x2, M.N.y1, M.N.y2 

   type C() = ...
   
   type D() = ...

   module M = 
      //   These names resolve to types in this scope:
      //       NS.C, NS.D, NS.M.E, NS.M.F
      //       C, D, M.E, M.F
      //       E, F
      //   These names resolve to functions in this scope:
      //       NS.M.x1, NS.M.x2, NS.M.N.y1, NS.M.N.y2, 
      //       M.x1, M.x2, M.N.y1, M.N.y2 
      //       x1, x2, N.y1, N.y2 
   
      type E() = ...
    
      type F() = ...

      let x1 () = ...
      let x2 () = ...

      // A nested module
      module N = 
         //   These names resolve to types in this scope:
         //       NS.C, NS.D, NS.M.E, NS.M.F
         //       C, D, M.E, M.F
         //       E, F
         //   These names resolve to functions in this scope:
         //       NS.M.x1, NS.M.x2, NS.M.N.y1, NS.M.N.y2, 
         //       M.x1, M.x2, M.N.y1, M.N.y2 
         //       x1, x2, N.y1, N.y2 
         //       y1, y2 

         let y1 () = ...
         let y2 () = ...
```

Note that the interpretaion of ``rec`` differs from that in OCaml.  In OCaml, ``module rec M`` allows the
module contents to be accessed via the name ``M``, e.g. ``module rec M = ... let g() = 2 let f() = 1 + M.g()``.
But unless M is opened, mutual references _must_ go via the name ``M``. This proposal for F# is that there is
effectively an implicit ``open`` immediately after the ``rec`` and on all nested modules.  This fits
with F#'s de-emphasis of modules-as-algebraic-values, and fits with F#'s existing rule that implicitly opens
a namespace N inside the definition of N.

#### Interaction with ``#`` declarations

``#`` declarations (e.g. ``#nowarn`` declarations) can't currently be included in mutually referential groups, see below.

#### Interaction with signatures

Currently, signatures can only be mutually recursive in a similar way to implementations, notably ``type X ... and Y ...``.

The natural thing would be to allow ``#rec`` in signatures just as in implementations.


# Drawbacks
[drawbacks]: #drawbacks

### It encourages the use of mutually referential code more than today

There is understandably some resistance to making writing mutually referential code easier in F#: see
for example the original comment [here](https://fslang.uservoice.com/forums/245727-f-language/suggestions/11723964-allow-types-and-modules-to-be-mutually-referential).
Existing F# users find it clarifying and simplifying to have both a file order and to have minimized mutually-referential scopes.
However, creating closed sets of mutually recursive types (including static methods and data)
is part of the F# language design: the intent has never been to adopt an extreme position where all mutual reference is banned.
The current design is awkwardly limiting in the way that only types can be mutually referential.

Were a proposal along these lines to be implemented, F# will continue to de-emphasize mutual references by default. The question, as always, is finding the right balance.

One risk of the proposal is that the feature will get over-used by beginners as the "easy" answer to avoid thinking
about order of code.  It may also be used by those looking to do top-down development (e.g. putting ``main`` first).  

A final risk is that this feature will lead to cascading requests to allow open recursion across multiple files in F#
code.  


# Alternatives
[alternatives]: #alternatives

Many options have been considered, though discussion is welcome:

### Alternative: Not do anything.

This is of course the default assumption :)

###  Alternative: Allow full mutual recursion across all files in an assembly.

This is the assumption that C# and Java users have when coming to a new language.  It brings enormous challenges
for type inference and for implementing efficient "safe" static initialization.

### Alternative: ``#rec`` declarations and scoped mutually refential regions

There is no intrinsic reason why mutual reference needs to be dealt
with at the ``module`` or ``namespace`` scope - it is often useful to make 
selected delimited groups of declarations mutually referential.

As part of the design investigation we looked at allowing delimited mutually referential regions using a ``#rec`` 
declarations:

```fsharp
#rec

let f x = ...
let g x = ...
```

or

```fsharp
#beginrec

let f x = ...
let g x = ...
#endrec
```

While perfectly feasible technically - and actually quite simple to implement - this is a little ugly syntactically
and doesn't seem aesthetically pleasing enough to include as the default mechanism. 

### Alternative: Require a forward signature on a module

The current proposal makes recursion possible combined with type inference. This utilizes F#'s existing
type checking.

Proposals for mutually recursive modules often require a module signature be given to mediate type inference
for mutually recursive modules, e.g. ``module rec M : MSig = ...``

The problem with this approach is that it doesn't solve the actual problem to hand (the lack of mutual referentiality
between types and modules in practical F# coding), while imposing large costs in requiring the addition of
named signatures and requiring the user to write signatures.  Relatively few F# users use explicit module signatures.

Another variation on this approach is to only allow mutual reference when a signature file has been given for a module.
This has similar drawbacks.

# Unresolved questions
[unresolved]: #unresolved-questions

There are many parts of this design still under investigation.  As issues are identified they will be noted in this RFC.

