# F# Tooling RFC FST-1003 - Loading Type Provider Design-Time Components into F# Tooling

### Summary 

This RFC proposes an adjustment to how the compile-time (aka "design time") component of an F# type provider is located and
loaded into F# tooling.  This change would have an impact on the future package layout of components that include F# type providers.

Despite being a tooling issue rather than a language issue, this is being treated as an F# RFC to facilitate discussion.

* [Discussion](https://github.com/fsharp/fslang-design/issues/229)
* [Implementation](https://github.com/Microsoft/visualfsharp/pull/3864)

### Background and Terminology

Type providers "augment" a regular DLL reference.  To use a type provider the
programmer specifies a reference to a DLL (e.g. ``FSharp.Data.dll``), which contains an attribute that indicates there is an associated
Type Provider Design Time Component (TPDTC, e.g. ``FSharp.Data.DesignTime.dll``) to also use. The TPDTC
gets loaded into F#-aware host tools (i.e. compilers, editor addins and other tooling built using ``FSharp.Compiler.Service.dll``)
at design-time. That is, any referenced DLL processed by the F# compiler logi  may contain a [``TypeProviderAssembly(...)``](https://msdn.microsoft.com/en-us/visualfsharpdocs/conceptual/compilerservices.typeproviderassemblyattribute-class-%5Bfsharp%5D) attribute indicating the presense of an associated TPDTC.

The host tooling then does F# analysis or compilation via requests to ``FSharp.Compiler.Service.dll``, which in turn
interrogates the TPDTCs to resolve type names, methods and so on.  The TPDTCs hand back metadata (via an artificial implementation of System.Type objects) and target code (via F# quotations and/or generated assembly fragments).

Tooling contexts include:

* ``FsAutoComplete.exe`` .NET Framework or .NET Core
* ``fsc.exe`` running on .NET Core 2.0 as either 64-bit or 32-bit
* fsi.exe running on .NET Framework as 32-bit
* fsiAnyCpu.exe running on .NET Framework as either 64-bit or 32-bit
* fsc.exe running on .NET Framework as either 64-bit or 32-bit
* devenv.exe running on .NET Framework 32-bit

and indeed any context that uses ``FSharp.Compiler.Service.dll`` as either netstandard 2.0 or .NET Framework component.

Recent PRs in the Type Provider SDK repo (e.g. https://github.com/fsprojects/FSharp.TypeProviders.SDK/pull/139) are a big step towards completing our type provider story for .NET Core. With this work, properly written TPDTCs using the latest version of the TPSDK now _always_ produce target code for the **target** reference assemblies being used by the compilation. This applies to both generative and erasing type providers.  

One final piece of the type provider puzzle is to **how F# tooling locates an appropriate TPDTC, especially depending on whether the compiler  is running in  .NET Core or .NET Framework**.  

Historically, in 2012, we made the **incorrect** assumption that TPDTCs could just be .NET 4.x components
sitting alongside a runtime library. This is wrong from multiple angles. Specifically we must be able to
choose a TPDTC suitable for a particular tooling context.


### Proposal

When a referenced assembly contains [TypeProviderAssembly](https://msdn.microsoft.com/visualfsharpdocs/conceptual/compilerservices.typeproviderassemblyattribute-class-%5bfsharp%5d) attribute, indicating it wants design-time type provider component ``MyDesignTime.dll``, then

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
    ...\typeproviders\fsharpNN\net46\ARCH\MyDesignTime.dll
    ...\typeproviders\fsharpNN\net46\MyDesignTime.dll
    ...
    ...\typeproviders\fsharpNN\netstandard2.0\ARCH\MyDesignTime.dll
    ...\typeproviders\fsharpNN\netstandard2.0\MyDesignTime.dll
    MyDesignTime.dll 
```

relative to the location of the runtime DLL, which is presumed to be in a nuget package.  

* When we use ``...`` we mean a recursive upwards directory search looking for a directory names ``typeproviders``, stopping when we find a directory name ``packages`` 

* WHen we use ``fsharpNN`` we mean a successive search backwards for ``fsharp42``, ``fsharp41`` etc.  Putting a TPDTC in ``fsharp41`` means the TPDTC is suitable to load into F# 4.1 tooling and later, and has the right to minimally assume FSharp.Core 4.4.1.0

his means that a package can contain a type provider design-time for both .NET Core and .NET Framework.  This will allow it to load into both compiler, F# Interactive and tools running .NET Framework OR .NET Core.

### Examples

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

    typeproviders\fsharp40\net461\FSharp.Data.DesignTime.dll  
    typeproviders\fsharp40\netcoreapp2.0\FSharp.Data.DesignTime.dll  
```

Here we are assuming FSharp.Data wants to use different design time DLLs for the two host tooling contexts we care about.  Example 3 deals with the case where FSHarp.Data wants to use a single DLL.

##### Example 3 - FSharp.Data (with .NET Standard design-time DLL)

A layout like this may also be feasible if [shipping facades to create a .NET Standard 2.0 TPDTC](https://github.com/fsprojects/FSharp.TypeProviders.SDK/#making-a-net-standard-20-tpdtc)

```
    lib\net45\FSharp.Data.dll 
    lib\netstandard2.0\FSharp.Data.dll 

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
    typeproviders\fsharp40\net461\MyTypeProvider.DesignTime.dll
```
Again **if your TPDTC component is a solitary NET Framework component then it will be unusable when a host tool executes using .NET Core**.


Note the above is effectively a proposed package architecture for any cross-generating compiler plugins.  If we ever support other kinds of compiler plugins then we could follow a similar pattern.


### Drawbacks 

This proposal has generated extensive discussion, see https://github.com/Microsoft/visualfsharp/issues/3736

A primary concern is that the F# compiler must include some minimal logic about framework names and package layout. This is anathema in modern .NET design, where tools such as Paket and Nuget are used to manage references and dependencies.

The discussion thread linked above records the discussion and response. Basically relying on tools such as Paket and Nuget to handle the configuration of design-time tools is extremely problematic.  For example, at first sight it looks possible to utilize the capability of Paket emit load scripts to help with such configuration, however these wouldn't apply to coommand-line compilation or project builds.  Further, Nuget is very far from being able to do this kind of configuration and it is unrealistic to expect them to add this capability in any timeframe that helps. Further, the full set of design-time tooling contexts is not actually known to NuGet.exe or Paket.exe -  -  a new F# design-time  tool (such as a documentation generator or VSCode editor) hosted in .NET Core may arrive after the fact

In contrast, in the proposal in this RFC, a relatively modest adjustment is made to the current scheme to interpret ``TypeProviderAssembly`` attributes to a relative reference in a stable and predictable way.  This resolution would apply to any and
all tooling built using updated versions of ``FSharp.Compiler.Service.dll``, and would roll out consistenly across all F# implementations.

## Alternatives
[alternatives]: #alternatives


1. Separate TPDTC and TPRTC references and add new features to Paket and NuGet to configure design-time tooling with the right set of TPDTC references.

   Response: See 'Drawbacks' above and linked discussion thread

2. Use an existing architecture such as  [Roslyn Analyzer Package Layout](https://docs.microsoft.com/en-us/nuget/schema/analyzers-conventions).  

   Response: However this layout doesn't cope with .NET Core v. NET Framework, and doesn't cope with evolution of the type provider API/protocol between the F# compiler and the TPDTC components.

3. Radically change TPDTCs so they aren't compiled components at all, but are specified using some other means like F# source files (e.g. source tasks in MSBuild)

   Response: Too intrusive, too radical

## Links


* [Roslyn Analyzer Package Layout](https://docs.microsoft.com/en-us/nuget/schema/analyzers-conventions)

