# F# RFC FS-1045 - Add ``Func`` overloads of ``FuncConvert.ToFSharpFunc`` for use from C#

The suggestion to add overloads to ``FuncConvert.ToFSharpFunc`` taking ``Func`` values has been approved in principle.
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [ ] Details
* [ ] Implementation: [complete](https://github.com/Microsoft/visualfsharp/pull/3691)

**NOTE: this is a potential breaking change to C# code using ``FuncConvert.ToFSharpFunc`` and thus may need reconsideration.**

# Summary
[summary]: #summary

The recommended way to create an ``FSharpFunc`` value from C# is to use ``FuncConvert.ToFSharpFunc``.  However no ``Func``-based
overloads were available for this, only ones based on ``Converter`` and ``Action``.

# Motivation
[motivation]: #motivation

The existing ``FuncConvert`` API is as follows:
```fsharp
    type FuncConvert = 
        static member  inline ToFSharpFunc       : action:Action<'T> -> ('T -> unit)
        static member  inline ToFSharpFunc       : converter:Converter<'T,'U> -> ('T -> 'U)
```

However as noted in [this issue](https://github.com/Microsoft/visualfsharp/issues/1847) the ``Converter`` overload is not available
in the .NET Standard 1.6 DLL for FSharp.Core. 

The use of ``Converter`` and ``Action`` overloads dates from .NET 2.0, when this API first appeared.  The complete set of ``Func``
types only became available in .NET 4.x.  One possible resolution of this issue is to move away from using ``Converter`` and use ``Func`` instead.

# Discussion of Problems

However, as noted below, this causes a breaking change for C# client code if we increase the number of overloads available, requiring
many more C# type annotations and less use of ``a => b`` lambda syntax in C#.

An alternative may be to make the ``Converter`` API available in the .NTE Standard 2.0 DLL for FSharp.Core assuming the ``Converter``
type is available in .NET Standard 2.0.



# Detailed design
[design]: #detailed-design

Adds
```
    type FuncConvert = 
        static member  inline ToFSharpFunc       : func:Func<'U> -> (unit -> 'U)
        static member  inline ToFSharpFunc       : func:Func<'T,'U> -> ('T -> 'U)
        static member  inline ToFSharpFunc       : func:Func<'T1,'T2,'U> -> ('T1 * 'T2 -> 'U)
        static member  inline ToFSharpFunc       : func:Func<'T1,'T2,'T3,'U> -> ('T1 * 'T2 * 'T3 -> 'U)
        static member  inline ToFSharpFunc       : func:Func<'T1,'T2,'T3,'T4,'U>  -> ('T1 * 'T2 * 'T3 * 'T4 -> 'U)
        static member  inline ToFSharpFunc       : func:Func<'T1,'T2,'T3,'T4,'T5,'U> -> ('T1 * 'T2 * 'T3 * 'T4 * 'T5 -> 'U)
        static member  inline ToFSharpFunc       : func:Func<'T1,'T2,'T3,'T4,'T5,'T6,'U> -> ('T1 * 'T2 * 'T3 * 'T4 * 'T5 * 'T6 -> 'U)
        static member  inline ToFSharpFunc       : func:Func<'T1,'T2,'T3,'T4,'T5,'T6,'T7,'U> -> ('T1 * 'T2 * 'T3 * 'T4 * 'T5 * 'T6 * 'T7 -> 'U)

```
to the existing overloads:

```fsharp
    type FuncConvert = 
        static member  inline ToFSharpFunc       : action:Action<'T> -> ('T -> unit)
        static member  inline ToFSharpFunc       : converter:Converter<'T,'U> -> ('T -> 'U)
```


# Compatibility
[compatibility]: #compatibility

This change breaks existing C# code because additional overloads are available. In particular code such as 

    FuncConvert.ToFSharpFunc<int,string>(i => i.ToString() + i.ToString())

gives

    test.cs(89,60): error CS0121: The call is ambiguous between the following methods or properties: 'FuncConvert.ToFSharpFunc<T, TResult>(Func<T, TResult>)' and 'FuncConvert.ToFSharpFunc<T, TResult>(Converter<T, TResult>)'


# Unresolved questions
[unresolved]: #unresolved-questions

* What to do about the breaking change
