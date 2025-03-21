# F# RFC FS-1149 - Support CallerArgumentExpression

The design suggestion [Support [\<CallerArgumentExpression\>]](https://github.com/fsharp/fslang-suggestions/issues/966) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/966)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/17519)
- [x] [Discussion](https://github.com/fsharp/fslang-design/discussions/797)

# Summary

Support the [[\<CallerArgumentExpression\>]](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.callerargumentexpressionattribute), including:
1. When invoking existing methods in BCL or C#, pass the code text of the specified argument.
2. Allow the use of this attribute when defining F# class methods.

Also, this is a part of the interoperability with C#.

# Motivation

The motivation, pros, and cons can be seen in the [C# proposal](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/caller-argument-expression.md). To summarize, this allows developers to capture the expressions passed to a method, to enable better error messages in diagnostic/testing APIs and reduce keystrokes.

# Detailed design

### When invoking existing methods in BCL or C#

1. The compiler should be able to retrieve the original source code text within the specified text range during compilation, even if the range is affected by `#line` directives.
2. When making method call, the compiler should do the following things to the optional parameters marked with the `[<CallerArgumentExpression>]` attribute:
   1. Attempt to identify the argument which the attribute references.
   2. Determine the textual range of the argument expression in the source code.
   3. Use the retrieved text range to extract the source code snippet corresponding to the argument expression.
   4. Bind the extracted code text to the parameter and propagate it as part of the method call.
   5. If any step above fails, the optional parameter will use its declared default value.

The following examples show the expected behavior:
```fsharp
System.ArgumentException.ThrowIfNullOrEmpty null  // paramName = "null"
System.ArgumentException.ThrowIfNullOrEmpty(argument = null) // paramName = "null"
System.ArgumentException.ThrowIfNullOrEmpty(null: string) // paramName = "null: string"
System.ArgumentException.ThrowIfNullOrEmpty(null
#line 1
  : string)  // paramName = "null\n#line 1\n  : string"
```


### When defining methods using the attribute in F#

1. The attribute can be applied to optional parameters in both F# and C#-style syntax.
2. The attribute can reference the `` parameters, but **need to check** whether it can work with C#。
3. When compile with the environment without [[\<CallerArgumentExpression\>]](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.callerargumentexpressionattribute) like .NET Framework 4.7, the compiler should be able to discover the attribute that user defines.
4. **Test** whether it can work with the situations that are mentioned in [FS-1012 Support for caller info argument attributes](/FSharp-4.1/FS-1012-caller-info-attributes.md) like computation expressions.

The following examples show the expected behavior:

```fsharp
#if NETFRAMEWORK
namespace System.Runtime.CompilerServices
open System

[<AttributeUsage(AttributeTargets.Parameter, AllowMultiple=false, Inherited=false)>]
type CallerArgumentExpressionAttribute(parameterName: string) =
  inherit Attribute()

  member val ParameterName = parameterName
#endif

open System.Runtime.CompilerServices
open System.Runtime.InteropServices

namespace MyNamespace
type MyClass =
  static member MyMethod(
    argument,
    ``argument 2!``
    [<CallerArgumentExpression "argument">] ?exp: string,
    [<CallerArgumentExpression "argument 2!"; Optional; DefaultParameterValue "default">] exp2: string) =
    exp, exp2

MyClass.MyMethod(1 + 1, 2.) // "1 + 1", "2."
```

# Drawbacks

No.

# Alternatives

As mentioned at [the comment](https://github.com/fsharp/fslang-suggestions/issues/966#issuecomment-764577172), we can use the F# specific `[<ReflectedDefinition>]` to do this.

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change?
> No

* What happens when previous versions of the F# compiler encounter this design addition as source code?
> It will works as before, ignore the attribute, and pass the default value as argument.

* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
> It will works as before, as the attribute is very common in the BCL.


* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?

> This change does not affect FSharp.Core.

# Pragmatics

## Diagnostics

* Error when the attribute is applied to a non-optional parameter.
* Error when the attribute is applied to a parameter whose type is not `string`.
* Warn if the attribute is referencing a non-existing parameter name.
* Warn if the attribute is referencing the parameter that the attribute is applied to.
* Warn when the attribute is applied to a parameter with `[<CallerMemberName>]` or `[<CallerFilePath>]`.

## Performance

The feature requires the compiler to read and store all the code text in memory, which may have a negative impact on the performance of the compiler.

# Unresolved questions

What parts of the design are still TBD?

> Should this attribute can be applied to the computation expression builders?
