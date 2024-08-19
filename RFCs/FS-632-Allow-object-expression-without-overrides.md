# F# RFC FS-632 - Allow object expression without overrides.

The design suggestion [Allow object expression without overrides](https://github.com/fsharp/fslang-suggestions/issues/632) has been marked "approved in principle."

This RFC covers the detailed proposal for this suggestion.

- [x] [Allow object expression without overrides](https://github.com/fsharp/fslang-suggestions/issues/632)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/17387)
- [x] [Discussion](https://github.com/fsharp/fslang-design/discussions/781)

# Summary

We allow object expressions without overrides for abstract and non-abstract classes.

# Motivation
- Consistent with the behavior of object expressions with interfaces where the user is allowed to create an object expression without overriding any members.

```fsharp
type IFoo = interface end

{ new IFoo }
```

- Better interoperability with C# libraries that have classes with protected constructors and may provide functionality used by derived classes.

# Detailed design

<!-- This is the bulk of the RFC. Explain the design in enough detail for somebody familiar
with the language to understand, and for somebody familiar with the compiler to implement.
This should get into specifics and corner-cases, and include examples of how the feature is used.
 -->

- We will update the type checker `TcObjectExpr` to allow object expressions without overrides:
  - Check there isn't any manually overridden members like `ToString`, `Equals`, `GetHashCode`, etc.
  - Check there isn't extra implementations like interfaces or required abstract members implemented.
  - Make sure continue to raise errors if an abstract class defines any abstract members that need to be implemented
  - Make sure we continue to raise an error we attempt to instantiate an abstract class:

## Before

To create an object expression without overrides, the user has to override a member, even if it is not necessary.

```fsharp
type IFirst = interface end

[<AbstractClass>]
type ClassEnd() = class end

// FS0738 Invalid object expression. Objects without overrides or interfaces should use the expression form 'new Type(args)' without braces.
let objExpr = { new ClassEnd() } 

// Workaround: override a member
let objExpr = { new ClassEnd() with member this.ToString() = "ClassEnd" }

// Workaround: implement a marker interface
let objExpr = { new ClassEnd() interface IFirst }

type Class() = class end

//FS0738 Invalid object expression. Objects without overrides or interfaces should use the expression form 'new Type(args)' without braces.
let objExpr = { new Class() }

// Workaround: override a member like ToString, Equals, GetHashCode, etc.
let objExpr = { new Class() with member this.ToString() = "ClassEnd" }

// Workaround: implement a marker interface
let objExpr = { new Class() interface IFirst }
```

## After

We won't need to use any workaround to use classes(abstract or non-abstract) in object expressions.

```fsharp
[<AbstractClass>]
type AbstractClass() = class end

// Ok
let objExpr = { new AbstractClass() }

type Class() = class end

// Ok
let objExpr = { new Class() }
```

Please address all necessary compatibility questions:

* Is this a breaking change?
  * No.
  
* What happens when previous versions of the F# compiler encounter this design addition as source code?
  * Older compiler versions will still emit an error when they encounter this design addition as source code.

* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
  * Older compiler versions will be able to consume the compiled result of this feature without issue.

* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?
  * N/A.

# Pragmatics

## Diagnostics

<!-- Please list the reasonable expectations for diagnostics for misuse of this feature. -->
  N/A.

We continue to emit an error message when not all abstract members are implemented:

```fsharp
[<AbstractClass>]
type AbstractClass() = 
    abstract member M: unit -> unit
    
let objExpr = { new AbstractClass() }

stdin(6,15): error FS0365: No implementation was given for 'abstract AbstractClass.M: unit -> unit'
```

## Tooling

Please list the reasonable expectations for tooling for this feature, including any of these:

* Debugging
  * Breakpoints/stepping
    * N/A.
  * Expression evaluator
    * N/A.
  * Data displays for locals and hover tips
    * N/A.
* Auto-complete
  * N/A.
* Tooltips
  * N/A.
* Navigation and go-to-definition
  * N/A.
* Error recovery (wrong, incomplete code)
  * N/A.
* Colorization
  * N/A.
* Brace/parenthesis matching
  * N/A.

## Performance

<!-- Please list any notable concerns for impact on the performance of compilation and/or generated code -->

  * No performance or scaling impact is expected.

## Scaling

<!-- Please list the dimensions that describe the inputs for this new feature, e.g. "number of widgets" etc.  For each, estimate a reasonable upper bound for the expected size in human-written code and machine-generated code that the compiler will accept. -->

  * N/A.

## Culture-aware formatting/parsing

Does the proposed RFC interact with culture-aware formatting and parsing of numbers, dates and currencies? For example, if the RFC includes plaintext outputs, are these outputs specified to be culture-invariant or current-culture.

  * No.

# Unresolved questions

  * None.
