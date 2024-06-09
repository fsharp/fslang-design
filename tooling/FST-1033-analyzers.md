# FST-1033 Towards F# Analyzer support in FSharp.Compiler.Service

This RFC discusses a path forward to add F# analyzers to heart of F# tooling, at the level of the FSharp.Compiler.Service.  

[F# Analyzers](https://github.com/ionide/FSharp.Analyzers.SDK#fsharpanalyzerssdk) exist today for use with toolchains based
on FSharp.AutoComplete.  However they must be recompiled on each update to FSharp.AutoComplete.  The problems discussed here
are mostly to do with the binary compatibility of information being drawn from FCS by analyzers, and what we might do about that.

Links:

* [F# Analyzers](https://github.com/ionide/FSharp.Analyzers.SDK#fsharpanalyzerssdk)

* [Roslyn analyzers](https://github.com/dotnet/roslyn-analyzers)

* Resharper analyzers for Rider
  * [F# example part1](https://github.com/JetBrains/fsharp-support/blob/net211/ReSharper.FSharp/src/FSharp.Psi.Features/src/Daemon/Analyzers/RedundantNew.fs)
  * [F# example part2](https://github.com/JetBrains/fsharp-support/blob/net211/ReSharper.FSharp/src/FSharp.Psi.Features/src/QuickFixes/RemoveRedundantNewFix.fs)

* [FSharpLint](https://github.com/fsprojects/FSharpLint)

* [Discussion](https://github.com/fsharp/fslang-design/issues/508)

* [Related Discussion in F# Analyzer SDK](https://github.com/ionide/FSharp.Analyzers.SDK/issues/28)

### Background

Analyzers are design-time components that run during editing, compilation and (perhaps) prior to script execution.
Analyzers (e.g. Roslyn analyzers) traditionally deliver the following to the developer:

* additional diagnostics

* additional potential code fixes 

In addition, for the purposes of this RFC, we are also interested in analyzers that deliver the following:

* additional quick info (hover tips), see also [this issue](https://github.com/ionide/FSharp.Analyzers.SDK/issues/20)

This is because one motivating use of analyzers is to provide additional information from inference procedures (such as [shape checking in the DiffSharp tooling](https://github.com/DiffSharp/DiffSharp/pull/207)).

We consider the following of long term interest but they are not part of this RFC:

* additional go-to-definition info

* additional F1 help info

* additional auto-complete info

* aligning the mechanism with code optimizers that return new expression trees

* aligning the mechanism with code generators or type providers that return new declarations

### Existing F# Analyzer API (v0.3)

The [v0.3 API for an F# analyzer](https://github.com/ionide/FSharp.Analyzers.SDK/blob/master/src/FSharp.Analyzers.SDK/FSharp.Analyzers.SDK.fs)
is simple enough:

```fsharp
type AnalyzerAttribute
    member _.Name = name

type Context =
    { FileName: string
      Content: string[]
      ParseTree: ParsedInput
      TypedTree: FSharpImplementationFileContents
      Symbols: FSharpEntity list
      GetAllEntities: bool -> AssemblySymbol list}

type Fix =
    { FromRange : Range.range
      FromText : string
      ToText : string }

type Severity =
    | Info
    | Warning
    | Error

type Message =
    { Type: string
      Message: string
      Code: string
      Severity: Severity
      Range: Range.range
      Fixes: Fix list }

type Analyzer = Context -> Message list
```

So an analyzer is just a producer of diagnostics with associated fixes.  This could easily be extended to allow additional production of quick info.

### The problem of binary compatibility

The big problem here is hidden in these four lines:
```
      ParseTree: ParsedInput
      TypedTree: FSharpImplementationFileContents
      Symbols: FSharpEntity list
      GetAllEntities: bool -> AssemblySymbol list
```

All these types are very complex and come from the non-binary-compatible FSharp.Compiler.Service component.

The problem here is that the F# Compiler Service is not yet a binary compatible API - almost every revision of the F# compiler breaks the API.

For addition into the core of the F# toolchain (FCS) analyzers will have to be binary compatible, that is if you write an analyzer it
must be loadable into all future iterations of F# tooling.  This is not optional for delivery of analyzer support in the Visual F# Tools.
Making progress on this is really what this RFC is about.

(Note this would not a problem if it can be assumed that all analyzers are recompiled and delivered afresh for each new iteration of delivered F# tooling.
However that's not a realistic assumption)

### Possible Path forward Part 1

Full binary compatibility for FCS doesn't seem to be feasible in the short term, the exposed API is very large and some elements in particular are changing.

Instead, I propose a path where we aim to carve out parts of the FCS API (e.g. SyntaxTree/FSharpSymbol/FSharpExpr) and put them in a separate assembly
as interfaces.  Initially these types would *only* be simple records and interfaces.

In this situation we'd have

* `FSharp.Compiler.Analyzers.dll` is a binary compatible component in the dotnet/fsharp repo, say v1.0.0.0, containing only interfaces and (never-changing) records.
    
* `MyAnalyzer.dll` consumes and implements some of these interfaces
    
* All future `FSharp.Compiler.Service.dll` respectively implement and consume the same interfaces 

New iterations of F# tooling would continue to consume all previous iterations of analyzers. We could therefore adopt the usual sort of naming:

    FSharp.Compiler.Analyzers.v1.0
    FSharp.Compiler.Analyzers.v2.0

Note that type providers have a similar story, though their API is defined in FSharp.Core because it is relatively simple
(the complexity of the API being hidden in the System.Type/MethodInfo objects returned).  For something as potentially rich as analyzers -
accessing the entire SyntaxTree/FSharpExpr/FSharpSymbol API, we should not do this.

### Possible Path forward - Part 2

Given that it may take a while to stabilise enough of FCS and enrich it to contain full syntax trees and symbol/expression information, it might be
simpler to start with `FSharp.Compiler.Analyzers.v1.0` only containing an API which has **no context except the cracked project arguments and handles to the
relevant logical source file contents**.

This means analyzers that want to access the syntax tree would have to parse using their own private copy of FCS.
Each analyzer would have to run its own compilation/analysis internally, rather than having access to the FCS compiled trees.
This is expensive for each analyser but is at least a start.

The `FSharp.Compiler.Analyzers.v1.0` API could then be something simple like this:

```fsharp
/// Marks an analyzer for scanning
type AnalyzerAttribute

// implemented by FCS
type ISourceText =
    abstract ... (same as ISourceText in F# compiler)

// implemented by FCS
type IAnalyzerContext =
    abstract ProjectFile: string
    abstract ProjectOptions: string[]
    abstract GetSource: fileName: string -> ISourceText

type postion = string * int * int
type range = string * int * int * int * int

// produced by analyzer
type Fix =
    { FromRange : range
      FromText : string
      ToText : string }

// produced by analyzer
type Severity =
    | Info
    | Warning
    | Error

// produced by analyzer
type Message =
    { Type: string
      Message: string
      Code: string
      Severity: Severity
      Range: range
      Fixes: Fix list }

type IQuickInfo = ...

// implemented by analyzer
type IAnalyzer =
    abstract GetDiagnostics: IAnalyzerContext * fileName: string * source: ISourceText -> Async<Message list>
    abstract GetQuickInfo: IAnalyzerContext * fileName: string * source: ISourceText * position -> Async<QuickInfo list>

```

Here

* The Analyzer context is per-project, but individual requests are per-file, as per the current design of the F# tools

* The logical source file contents are defined via ISourceFile.

* Async is used both to support cancellation and because it's reasonable to expect analyzers to operate async

I believe the above API would be sufficient for both existing F# Analyzers (though they would each have to host an instance of FSHarp.Compiler.Service,
which is expensive but at least gets things started), and for the needs of the shape checking in DiffSharp (which recompiles and executes 
using reflection out of process - more like a testing tool - and not even within the FSharp.AutoComplete process).

Given this starting point we could then iterate towards expanding the functionality available in the context.

### Alternatives

* We could consider distributing F# Analyzers in source form :-)

* We could iterate the existing F# Analyzer support but without *any* dependency in the F# Analyzer API on FCS, and instead incorporate many shims
  and design with regard to the API reaching binary compatibility.  This would be a breaking change for F# analyzers.

### Discussion Summary

This RFC is intended to start a discussion and iterate towards steps forward.  We'll try to summarise the discussion here:

@dsyme says: I'm not sure how else to make progress in a reasonable timeframe, short of expecting analyzers to be recompiled.

@dsyme says: The Visual F# Tools use FSharp.Compiler.Private, so it's not possible to hand off values to F# Analyzers today.  Even if they
shifted to FSharp.Compiler.Service we would face the problem of binary compatibility.

### TODO

The [existing issues with F# analyzers](https://github.com/ionide/FSharp.Analyzers.SDK/issues) should all be considered, including

* [Passing options](https://github.com/ionide/FSharp.Analyzers.SDK/issues/8)

* [Cancellation](https://github.com/ionide/FSharp.Analyzers.SDK/issues/8)

* [Tooltips](https://github.com/ionide/FSharp.Analyzers.SDK/issues/20), see above

* [Making tooltips on-demand](https://github.com/ionide/FSharp.Analyzers.SDK/issues/27)
