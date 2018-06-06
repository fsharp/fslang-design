# F# RFC FS-1045 - Add new `FuncConvert.FromFunc` and `FuncConvert.FromAction` APIs  for use from C# (was "add `Func` overloads to ``FuncConvert.ToFSharpFunc``)

Add new `FuncConvert.FromFunc` and `FuncConvert.FromAction` APIs.

* [x] Approved in principle
* [x] Details
* [x] Approved 
* [x] Implementation: [complete](https://github.com/Microsoft/visualfsharp/pull/4815)

## Summary

The previous recommended way to create an ``FSharpFunc`` value from C# is to use ``FuncConvert.ToFSharpFunc``.  However no ``Func``-based
overloads were available for this, only ones based on ``Converter`` and ``Action``.

The existing ``FuncConvert`` API is as follows:
```fsharp
type FuncConvert = 
    static member ToFSharpFunc: System.Action<'T> -> ('T -> unit)
    static member ToFSharpFunc: System.Converter<'T,'U> -> ('T -> 'U)
```

As noted in [this issue](https://github.com/Microsoft/visualfsharp/issues/1847) it is not possible to use ``FuncConvert.ToFSharpFunc`` in .NET Standard 1.6 and .NET Core 2.0 programming because the `Converter` type is missing in .NET Standard 1.6. (It is present in .NET Standard 2.0 but FSharp.Core is not yet made available for that except through compat with .NET Standard 1.6) 

Further, the existing `FuncConvert` API doesn't accept `Func<A,B>` nor `Action<A,B>` values which are now normal in C# programming. We could add overloads to this API taking ``System.Func`` values. However, this doesn't really work because the API becomes too heavily overloaded, breaking existing code. 

Instead, in this RFC we add new APIs:

```fsharp
/// <summary>Convert the given Action delegate object to an F# function value</summary>
/// <param name="func">The input Action delegate.</param>
/// <returns>The F# function.</returns>
static member  inline FromAction       : action:Action          -> (unit -> unit)

/// <summary>Convert the given Action delegate object to an F# function value</summary>
/// <param name="func">The input Action delegate.</param>
/// <returns>The F# function.</returns>
static member  inline FromAction       : action:Action<'T>          -> ('T -> unit)

/// <summary>Convert the given Action delegate object to an F# function value</summary>
/// <param name="func">The input Action delegate.</param>
/// <returns>The F#funcfunction.</returns>
static member  inline FromAction       : action:Action<'T1,'T2>          -> ('T1 -> 'T2 -> unit)

/// <summary>Convert the given Action delegate object to an F# function value</summary>
/// <param name="func">The input Action delegate.</param>
/// <returns>The F# function.</returns>
static member  inline FromAction       : action:Action<'T1,'T2,'T3>          -> ('T1 -> 'T2 -> 'T3 -> unit)

/// <summary>Convert the given Action delegate object to an F# function value</summary>
/// <param name="func">The input Action delegate.</param>
/// <returns>The F# function.</returns>
static member  inline FromAction       : action:Action<'T1,'T2,'T3,'T4>          -> ('T1 -> 'T2 -> 'T3 -> 'T4 -> unit)

/// <summary>Convert the given Action delegate object to an F# function value</summary>
/// <param name="func">The input Action delegate.</param>
/// <returns>The F# function.</returns>
static member  inline FromAction       : action:Action<'T1,'T2,'T3,'T4,'T5>          -> ('T1 -> 'T2 -> 'T3 -> 'T4 -> 'T5 -> unit)

/// <summary>Convert the given Func delegate object to an F# function value</summary>
/// <param name="func">The input Func delegate.</param>
/// <returns>The F# function.</returns>
static member  inline FromFunc       : func:Func<'T>          -> (unit -> 'T)

/// <summary>Convert the given Func delegate object to an F# function value</summary>
/// <param name="func">The input Func delegate.</param>
/// <returns>The F# function.</returns>
static member  inline FromFunc       : func:Func<'T,'U>          -> ('T -> 'U)

/// <summary>Convert the given Func delegate object to an F# function value</summary>
/// <param name="func">The input Func delegate.</param>
/// <returns>The F#funcfunction.</returns>
static member  inline FromFunc       : func:Func<'T1,'T2,'U>          -> ('T1 -> 'T2 -> 'U)

/// <summary>Convert the given Func delegate object to an F# function value</summary>
/// <param name="func">The input Func delegate.</param>
/// <returns>The F# function.</returns>
static member  inline FromFunc       : func:Func<'T1,'T2,'T3,'U>          -> ('T1 -> 'T2 -> 'T3 -> 'U)

/// <summary>Convert the given Func delegate object to an F# function value</summary>
/// <param name="func">The input Func delegate.</param>
/// <returns>The F# function.</returns>
static member  inline FromFunc       : func:Func<'T1,'T2,'T3,'T4,'U>          -> ('T1 -> 'T2 -> 'T3 -> 'T4 -> 'U)

/// <summary>Convert the given Func delegate object to an F# function value</summary>
/// <param name="func">The input Func delegate.</param>
/// <returns>The F# function.</returns>
static member  inline FromFunc       : func:Func<'T1,'T2,'T3,'T4,'T5,'U>          -> ('T1 -> 'T2 -> 'T3 -> 'T4 -> 'T5 -> 'U)

```

# Motivation
[motivation]: #motivation

As noted in [this issue](https://github.com/Microsoft/visualfsharp/issues/1847) the ``Converter`` overload is not available
in the .NET Standard 1.6 version of FSharp.Core, because the ``System.Converter`` type is not available at all in .NET Standard 1.6.

The use of ``Converter`` and ``Action`` overloads dates from .NET 2.0, when this API first appeared.  The complete set of ``Func``
types only became available in .NET 4.x.  

# Detailed design
[design]: #detailed-design

See above. Note that:
* The API produces _curried_ F# functions.  The previous API `FuncConvert.ToFSharpFunc` (which still exists) produced F# functions taking _tupled_ arguments.
* The API is up to argument length 5 because that is the common limit used in existing FSharp.Core APIs, e.g. for `FuncConvert.FuncFromTupled`

# Example C# code

Here is are some example uses of FSharp.Core library functions from C#:

```csharp
ListModule.Map<int,string>(FuncConvert.FromFunc<int,string>(i => i.ToString() + i.ToString()), myList);
ListModule.MapIndexed<int,string>(FuncConvert.FromFunc<int,int,string>((i,j) => i.ToString() + j), myList);
ListModule.Iterate<string>(FuncConvert.FromAction<string>(s => { Console.WriteLine("s = {0}", s);}), myList2);
```

The code is still unpleasant, but at least:
1. the code is using "modern" C# delegate types `Action` and `Func`
1. the same C# code is usable on all of .NET Core, .NET Standard and .NET Framework

# Compatibility
[compatibility]: #compatibility

This is a compatible change.

# Unresolved questions
[unresolved]: #unresolved-questions

None.
