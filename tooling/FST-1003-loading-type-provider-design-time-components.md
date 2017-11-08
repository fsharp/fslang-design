# F# Tooling RFC FST-1003 - Loading Type Provider Design-Time Components into F# Tooling

### Summary 

We are proposing an adjustment to how the compile-time (aka "design time") component of an F# type provider is located and
loaded into F# tooling.  This change would have an impact on the future package layout of components that include F# type providers.

Despite being a tooling issue rather than a language issue, this is being treated as an F# RFC to facilitate discussion.

* [ ] Most discussion is happening on [this PR](https://github.com/dotnet/sdk/pull/1172). There is also [the RFC discussion issue](https://github.com/fsharp/fslang-design/issues/188)

### Background

Recent PRs in the Type Provider SDK repo (e.g. https://github.com/fsprojects/FSharp.TypeProviders.SDK/pull/139) are a big step towards completing our type provider story for .NET Core With this work, properly written type providers using the latest version of the TPSDK can now _always_ produce target code for either .NET Core or .NET Framework – i.e. they properly adjust for the target reference assemblies being used by the compilation. This applies to both generative and erasing type providers.  

One final piece of the type provider puzzle is to **have the F# compiler load an appropriate design-time DLL depending on whether the compiler  is running in  .NET Core or .NET Framework**.  

Historically, in 2012, we made the **incorrect** assumption that .NET 4.x components would always be loadable into any future .NET
tooling, and thus TPDTC could be .NET 4.x components sitting alongside a runtime library.
As a result, currently, the compiler looks for design time DLL alongside the referenced runtime
DLLs. (For simple type providers, these DLLs are the same)

We must be able to load type provider design-time DLLs into many different tooling contexts, notable

* ``FsAutoComplete.exe`` .NET Framework running 64-bit
* ``fsc.exe`` running on .NET Core 2.0 as either 64-bit or 32-bit
* fsi.exe running on .NET Framework as 32-bit
* fsiAnyCpu.exe running on .NET Framework as either 64-bit or 32-bit
* fsc.exe running on .NET Framework as either 64-bit or 32-bit
* devenv.exe running on .NET Framework 32-bit

and any context that uses FSharp.Compiler.Service.dll as either netstandard 2.0 or a .NET Framework component.


### Proposal

When a referenced assembly as the usual [TypeProviderAssembly](https://msdn.microsoft.com/visualfsharpdocs/conceptual/compilerservices.typeproviderassemblyattribute-class-%5bfsharp%5d) attribute, indicating it wants design-time type provider component “MyDesignTime.dll”, then

1. When executing using .NET Core the compiler looks in this order

 ```
    ...\typeproviders\fsharpNN\netcoreapp2.0\ARCH\MyDesignTime.dll
    ...\typeproviders\fsharpNN\netcoreapp2.0\MyDesignTime.dll 
    ...\typeproviders\fsharpNN\netstandard2.0\ARCH\MyDesignTime.dll
    ...\typeproviders\fsharpNN\netstandard2.0\MyDesignTime.dll 
    MyDesignTime.dll 
```

2.	When executing using .NET Framework the compiler looks in this order

```
    ...\typeproviders\fsharpNN\net461\ARCH\MyDesignTime.dll
    ...\typeproviders\fsharpNN\net461\MyDesignTime.dll
    ...\typeproviders\fsharpNN\netstandard2.0\ARCH\MyDesignTime.dll
    ...\typeproviders\fsharpNN\netstandard2.0\MyDesignTime.dll
    MyDesignTime.dll 
```

relative to the location of the runtime DLL, which is presumed to be in a nuget package.  

* When we use ``...`` we mean a recursive upwards directory search looking for a directory names ``typeproviders``, stopping when we find a directory name ``packages`` 

* WHen we use ``fsharpNN`` we mean a successive search backwards for ``fsharp42``, ``fsharp41`` etc.  Putting a TPDTC in ``fsharp41`` means the TPDTC is suitable to load into F# 4.1 tooling and later, and has the right to minimally assume FSharp.Core 4.4.1.0

Some examples:

This means that a package can contain a type provider design-time for both .NET Core and .NET Framework.  This will allow it to load into both compiler, F# Interactive and tools running .NET Framework OR .NET Core.

##### Example 1 - the simplest form

The very simplest type providers can use a single .NET Standard 2.0 DLL for both the runtime library and the design-time component. Layout:

```
      lib\netstandard2.0\MyRuntimeAndDesignTime.dll 
```

As of today you will also need to [ship facades for netstandard.dll, System.Runtime.dll and System.Reflection.dll alongside this component](https://github.com/fsprojects/FSharp.TypeProviders.SDK/#making-a-net-standard-20-tpdtc) and as such you should either
* adopt one of the layouts below to ensure facade extra files don't end up in the ``lib`` directory, OR
* put the facades in a well-known relative location (**not** under the lib directory in the package) and added AssemblyResolve events to go and find them

##### Example 2 - FSharp.Data

FSharp.Data would lay out as follows:

```
    lib\net45\FSharp.Data.dll 
    lib\netstandard2.0\FSharp.Data.dll 
    lib\portable-whatever\FSharp.Data.dll 

    typeproviders\fsharp40\net461\FSharp.Data.DesignTime.dll  
    typeproviders\fsharp40\netcoreapp2.0\FSharp.Data.DesignTime.dll  
```

Here we are assuming FSharp.Data wants to use different design time DLLs for the two host tooling contexts we care about.  Example 3 deals with the case where FSHarp.Data wants to use a single DLL.

##### Example 3 - FSharp.Data (with .NET Standard design-time DLL)

A layout like this may also be feasible if [shipping facades to create a .NET Standard 2.0 TPDTC](https://github.com/fsprojects/FSharp.TypeProviders.SDK/#making-a-net-standard-20-tpdtc)

```
    lib\net45\FSharp.Data.dll 
    lib\netstandard2.0\FSharp.Data.dll 
    lib\portable-whatever\FSharp.Data.dll 

    typeproviders\fsharp41\netstandard2.0\FSharp.Data.DesignTime.dll  
```

See note on facades above.

##### Example 4 - type providers with 32/64-bit dependencies

A Python type provider may have different dependencies for 32 and 64-bit for both runtime and design-time (the directory names may not be exactly right)

```
    lib\netstandard2.0\x86\FPython.Runtime.dll 
    lib\netstandard2.0\x64\FPython.Runtime.dll 

    typeproviders\fsharp41\netstandard2.0\x86\FPython.Runtime.dll 
    typeproviders\fsharp41\netstandard2.0\x86\cpython32.dll # some 32-bit DLL needed to run at design-time

    typeproviders\fsharp41\netstandard2.0\x64\FPython.Runtime.dll 
    typeproviders\fsharp41\netstandard2.0\x64\cpython64.dll # some 64-bit DLL needed to run at design-time
```

plus facades as mentioned above

##### Non Example 5 

Going forward, we should __not__ be happy with type provider packages that look like this - **these will be unusable when the compiler executes using .NET Core**

```
    lib\net45\MyTypeProvider.dll 
```

or even

```
    lib\net45\MyTypeProvider.dll 
    typeproviders\fsharp40\net45\MyTypeProvider.DesignTime.dll
```
Again **if your TPDTC component is a solitary NET Framework component then it will be unusable when a host tool executes using .NET Core**.


Note the above is effectively a proposed package architecture for any cross-generating compiler plugins.  If we ever support other kinds of compiler plugins then we could follow a similar pattern.


### Drawbacks 

TBD


## Alternatives
[alternatives]: #alternatives

TBD


## Links


* [Roslyn Analyzer Package Layout](https://docs.microsoft.com/en-us/nuget/schema/analyzers-conventions)

