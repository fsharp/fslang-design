# F# RFC FS-1012 - Support for caller info argument attributes (CallerLineNumber, CallerFileName, CallerMemberName)

The design suggestion [F# compiler should support CallerLineNumber, CallerFilePath etc](https://fslang.uservoice.com/forums/245727-f-language/suggestions/8899330-f-compiler-should-support-callerlinenumber-calle) has been marked "planned".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] [Discussion](https://github.com/fsharp/FSharpLangDesign/issues/84)
* [x] Implementation: [Completed](https://github.com/dotnet/fsharp/issues/1114)


# Summary
[summary]: #summary

This RFC describes F# support for the emerging .NET standard for compile-time treatment of method arguments tagged with one of
the following attributes from the `System.Runtime.CompilerServices` namespace:

  - `CallerLineNumberAttribute`
    - An integer optional argument with this attribute will be given a runtime value matching the line number of the source of the callsite
  - `CallerFilePathAttribute`
    - A string optional argument with this attribute will be given a runtime value matching the absolute file path of source of the callsite
  - `CallerMemberNameAttribute`
    - A string optional argument with this attribute will be given a runtime value matching the unqualified name of the enclosing member of the callsite

[Brief description and motivation on MSDN.](https://msdn.microsoft.com/en-us/library/hh534540.aspx)

# Motivation
[motivation]: #motivation

These attributes are useful for diagnostic and logging purposes, among others. They provide a way to obtain stack-trace or symbol-like source
information in a lightweight way at runtime, perhaps for inclusion in a log line. They also help developers with patterns like `INotifyPropertyChanged`,
providing a way to track member or property names as strings without hard-coded literals.

# Detailed design
[design]: #detailed-design

## Feature interaction: First-class uses of methods

What happens with first-class uses of attributed methods?

Answer: First-class uses of methods work as expected for C# and F# methods and there are tests in the PR.

## Feature Interaction - Method overloading

Does the use of an attribute change the way type inference and method selection works for a method?

Answer: No changes were made in this area so existing rules apply. The output of the code below is: ``Line f``

```fsharp
type M() =
    member self.f([<CallerLineNumber>]?line : int) =
        printfn "Line %d" line.Value

    member self.f() =
        printfn "Line f"

let m = M()

let foo () =
    m.f()
    
foo ()

```

## Feature Interaction - Computation Expressions

Can computation expression methods accepting caller info attributes?

Yes: Computation Expressions accept caller info attributes no special implementation and there are tests in the PR.

Check within an async expression ``async { ... }`` (whose desugaring has some implied lambda expressions)

Yes: Async computation expression values works as expected no special implementation and there are tests in the PR.

## Feature Interaction - Quotations

Check this feature works as expected with quotation literals

Yes: Quotations works as expected no special implementation and there are tests in the PR.

## Feature Interaction - Anonymous function

Check an anonymous lambda expression, e.g. ``(fun () -> ...)``.  

Yes: anonymous lambda expression works as expected no special implementation and there are tests in the PR.

## Feature Interaction - Object expression

Check an object expression member implementation implementing an interface
And check an object expression member implementation implementing an abstract member in a base class

Yes: Object expression works as expected no special implementation and there are tests in the PR.

## Feature Interaction - Delegates

Check a delegate implementation e.g. ``new System.Func<int,int>(fun a -> ...)``

Yes: Delegates works as expected no special implementation and there are tests in the PR.

## Implementation Details
Caller member name is the "top-level" bindings like methods, module-level and class-level functions, module-level and class-level values and they will be captured.
Sub-level bindings like "local variable" let bindings or nested functions are not captured as member name and the name the capture is from the parent scope.
For example the output of the code below is: f
```fsharp
module Test
let f () = 
    let x = 
        let g () = MyTy.GetCallerMemberName() 
        g()
    x
```

Top level bindings like class ctor or static ctor output is like in c# https://github.com/dotnet/roslyn/blob/56f605c41915317ccdb925f66974ee52282609e7/src/Compilers/CSharp/Test/Emit/Attributes/AttributeTests_CallerInfoAttributes.cs#L1274

# Drawbacks
[drawbacks]: #drawbacks

The major drawback is that added complexity this brings to the rules of the language.

# Alternatives
[alternatives]: #alternatives

Some alternatives are:

- Implement an F#-specific version of this feature.  This is rejected because it is better to conform to .NET standards rather than be F#-specific for this feature.

- Do not implement this feature.  This is rejected because it is better to conform to .NET standards for this topic.


# Unresolved questions
[unresolved]: #unresolved-questions

