# F# RFC FS-1009 - Allow mutually referential types and modules over larger scopes within files

The design suggestion [Allow mutually referential types and modules over larger scopes within single files](https://fslang.uservoice.com/forums/245727-f-language/suggestions/11723964-allow-types-and-modules-to-be-mutually-referential) has been marked "approved in principle". This RFC covers more detailed information about this suggestion. Please [discuss this RFC using the corresponding issue](https://github.com/fsharp/FSharpLangDesign/issues/76).

* [x] Status: Approved in principle
* [x] Details: [Under discussion](https://github.com/fsharp/FSharpLangDesign/issues/76)
* [x] Implementation: [Completed](https://github.com/dotnet/fsharp/pull/1132)


# Summary
[summary]: #summary

Allow a collection of both types and modules within a single scope in a single file to be mutually referential.
The exact mechanism used to declare the scope of mutual reference is TBD, but the current proposal is to 
use a ``rec`` declaration on a namespace and/or module as follows:

```fsharp
namespace rec MyFramework

type X = ... refer to Y and Z...

type Y = .... refer to X and Z...

module Z = ... refer to X and Y ...

```

The scope is always given by using ``rec`` on a module or namespace declaration group.  The scope of mutual reference
can be as large as a single file, or as small as a single module.  

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

The ability to choose the scope of mutual recursion and control it is significant in the F# language design
and one source of its low dependency metrics.
The current "small recursive scopes" work well for the majority of purposes and set the defaults "the right way", resulting
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


In the current investigations, types, modules, ``exception``, ``let`` and ``open`` bindings can be mutually referential.

Multiple namespace implementation fragments can't yet be included in mutually referential groups, e.g. in the following N2 can
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

 ``open`` declarations are allowed in mutually referential groups, and can refer to modules being defined. Consider
for example:

```fsharp
module rec SomeComponents = 

    open M

    type C() = 
        member c.P = f()  // refers to M.f() 

    module M = 
        let f() = ...

```

There are use-cases for this, e.g. when the module ``M`` defines optional or private extension members related to ``C``
which are used in the implementation of ``C``.
Allowing ``open`` on modules within the scope obviously needs care in the implementation and
should be used carefully by the programmer as well: it can clearly result in code that is hard
to understand. (At the technical level, care is needed because progressively more contents of a module or namespace
become available at each stage of type/module realization - prior to this suggestion type realization did not need to
process ``open`` declarations.)

The current prototype restricts ``open`` declarations to only be at the top of mutually-referential 
module and namespace declaration groups.

One example where a specific interaction may exist is the way
that ``open`` makes both F# and C# extension methods available.  So cases such as this need careful testing:

```fsharp

module rec Example = 
  open M

  type C() = ...

  module M = 
      type C with 
          member c.P = 1
          ...
```

#### Meaning of ``rec`` w.r.t. Name Resoltion

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

Note that the interpretation of ``rec`` differs from that in OCaml.  In OCaml, ``module rec M`` allows the
module contents to be accessed via the name ``M``, e.g. ``module rec M = ... let g() = 2 let f() = 1 + M.g()``.
But unless M is opened, mutual references _must_ go via the name ``M``. This proposal for F# is that there is
effectively an implicit ``open`` immediately after the ``rec`` and on all nested modules.  This fits
with F#'s de-emphasis of modules-as-algebraic-values, and fits with F#'s existing rule that implicitly opens
a namespace N inside the definition of N.

#### Interaction with ``open``

Consider the following code:

```fsharp

module Test = 
      open System.Collections.Generic

      type Queue<'a> = 
        abstract IsEmpty : bool

      let test (q: Queue<_>) = q.IsEmpty 
```

Which ``Queue`` does the type ``Queue<_>`` refer to?  The type being defined, obviously (and not
the one from ``System.Collections.Generic``).  During the development of the prototype of this feature,
a mistake was made where type definitions being defined in a module/namespace
were added to the environment _before_ processing ``open`` declarations.  They should be added _after_.

#### Interaction with ``[<AutoOpen>]``

Given the interaction with ``open``, it's also important to clarify and test the behaviour w.r.t. ``[<AutoOpen>]``.  

#### Interaction with module abbreviations

In F#, module abbreviations ``module M = A.B.C`` are simple aliases for modules, in scope in the current environment.
They do not actually declare a new module and, unlike type abbreviations, are not exported as part of the signature of
a module.

For mutually recursive groups, the proposal is that module abbreviations always come immediately after the ``open`` declarations
(which, as stated above, come first within each sub-module).  This means that ``open`` declarations are processed first,
followed by module abbreviations.

#### Interaction with attribute declarations

F# files can include assembly attribute declarations:

```fsharp
namespace N

[< assembly: SomeAttribute("...") >]
do ()
```

The proposal is that these be allowed in a set of mutually recursive declarations




#### Interaction with ``#`` declarations

``#`` declarations (e.g. ``#nowarn`` declarations) can't currently be included in mutually referential groups, see below.

#### Interaction with signatures

Currently, signatures can only be mutually recursive in a similar way to implementations, notably ``type X ... and Y ...``.

The proposal is that  ``rec`` is allowed on ``namespace`` and ``module`` in signatures just as in implementations.

```fsharp

namespace rec MyNamespace

open System

type C = 
    new : unit -> C
    member D : D
    member P : int

type D = 
    new : unit -> D
    member C : C
    member P : int
```



#### Interaction with ``.fsx`` scripting files

For ``.fsx`` files, the default is for declarations not to be part of a mutually referential group. Individual
modules can be declared mutually referential, e.g.

```fsharp

printfn "a script"

module rec Silly = 
    let f x = g (x + 1)
    let g x = if x = 10 then "TEN" else f (x + 1)

printfn "more of a script"
```

There is no way to declare that the whole script is mutually referential short of using one big module to contain all the
contents of a script.

#### Interaction with ``mutable`` 

F# currently disallows ``let rec`` values being marked ``mutable``.  It is proposed that we 
maintain this restriction for module-defined mutable values in mutually referential groups. e.g.

```fsharp
module rec Tests

let mutable x = 0
```
will give an error ``error FS0874: reecursive 'let' bindings may be marked mutable.  

Technically speaking, we could no doubt allow this.  However, it should be treated as an issue orthogonal to this feature.

Note that static mutable data in classes is still permitted, e.g.
```fsharp
module rec Tests

type C() =
    static let mutable failures = []
    static member Failures = failures
```

#### Interaction with existing known issues

Processing hierarchies of recursive type definitions of generic types where the
type parameters of these types are constrained by instances of the generic types is
quite difficult.  The use of ``rec`` can exacerbate this because more types are made recursive
more easily.

Specifically, during the course of development of a prototype of this RFC
we noticed that there seems to be an existing known issue with processing these type definitions:

```fsharp
type Exp<'c when 'c :> Exp<'c>> = 
    interface 
    end

and EvalExp<'c when 'c :> EvalExp<'c>> =
    interface
      inherit Exp<'c>
    end

type PrintLit<'c when 'c :> Exp<'c>>(value) =
    member x.BasePrint() = printf "out %d" value
    interface Exp<'c> 

and EvalLit<'d when 'd :> EvalExp<'d>>(value:int) =
    inherit PrintLit<'d>(value) 
    interface EvalExp<'d> 
```

If the last ``and`` is changed to a ``type`` then the definitions are checked ok.

This issue is orthogonal to the ``rec`` feature.


# Drawbacks
[drawbacks]: #drawbacks

### Concerns that it encourages the over-use of mutually referential code 

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

### Alternative: ``... and ... `` for more declarations 

One sytntac alternative that was considered was  allowing ``and`` to join more declaration forms, e.g.

```fsharp

module M = ...

and module N = ....

and exception E of stirng

and let f x = x + 1

and type C() = ...
```

We decided against this for two reasons

* it is actually quite an intrusive change to the syntax of the language and it's implementation, particularly with regard to whitespace sensitivity and the offside rule

* it feels syntactically awkward in a similar way to current ``type X .. and ... `` feels awkward, especially when combined with attribute declarations


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

### Alternative: Optionally allow complete mutual recursion across all files in an assembly

One option for F# is to optionally allow complete mutual reference throughout an assembly.
For example, one could imagine a variation on this proposal that used a command-line switch to turn
on mutual reference across all files in an assembly:

    fsc.exe /mutrec:on ....

Alternatively (or together with this), you could imagine requiring an explicit declaration on each and every file in
an assembly that indicated the contents of each file were recursive in an "open" way:

```fsharp
namespace open rec MyNamespace

type X = ...

type Y = ...
```


There are advantages to this approach, namely simplicity and familiarity for the majority of programmers today.
Most programmers now grow up with the expectation that all declarations within
a C# assembly or Java package can be mutually referential.  Anecdotal
evidence supports the claim that some C# and Java developers reject F# because it requires
declaration ordering, though many who perservere come to love that feature.

However, there are reasons why full-assembly-recursion, by itself, would a problematic addition for F#, even if optional:
* If implemented in isolation, it would be seem to change a key characteristic of F# coding (the avoidance of spaghetti frameworks). 
* It would be used too often when not needed. As mentioned above, the ability to minimize and control recursive scopes and to encourage a sound declaration order is regarded a a quality of the language, and by itself the "max" option would seem to offer no useful intermediate points. Specifically, people may be forced to turn on "the big recursion switch" when faced with some of the quite reasonable coding situations outlined at the start of this RFC.
* It is technically difficult to implement. It certainly minimally requires solving all the problems  solved by this RFC.  Further, the question of how the feature interacts with signature files would need to be addressed (and that is a non-trivial thing to solve).
* For a type-inferred, Hindley-Milner-style language, it is technically essentially impossible to provide incremental 
  checking for single files within an assembly where all files are mutually referential. This means that visualIDE tooling performance
  would be worse when editing large assemblies when this switch is enabled.

These reasons together lead to the conclusion that a "full recursion across assemblies" option is in a different category to this RFC
and should be considered and discussed separately.

# Unresolved questions
[unresolved]: #unresolved-questions

As further issues are identified they will be noted in this RFC.

