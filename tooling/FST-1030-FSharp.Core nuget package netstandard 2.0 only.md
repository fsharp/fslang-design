# F# Tooling RFC FST-1030 - Make FSharp.Core nuget package netstandard2.0 only
NOTE: This is a draft and remains to be reviewed by key contributors. Please contribute to the discussion and submit adjustments to the draft.

The purpose of this RFC document is to develop a shared plan of record for the delivery of FSharp.Core in NuGet package form as a netstandard2.0-only library.

Discussion thread: https://github.com/fsharp/fslang-design/issues/482
Suggestion thread: https://github.com/fsharp/fslang-suggestions/issues/893

## Background
Historically FSharp.Core.dll was compiled and delivered by Microsoft as a .NET 4.x binary installed under "Reference Assemblies". The DLL was signed and strong-named by Microsoft. Way, way back in time it was called fslib.dll.

Subsequently, FSharp.Core.dll has been recompiled for "trimmed down" platforms: portable profiles, Xamarin profiles, .NET Core, .NET Standard and .NET Framework. There is even a version of FSharp.Core for the "Xamarin iOS TV" profile. When provided by Microsoft, these were signed, strong-named and installed under "Reference Assemblies" on Windows. For other F# compilation environments like Xamarin, Mono, CloudSharper, FSharp.Formatting, Azure Notebooks, Azure Functions there was a bit of a mess (made considerably worse by the separation of sigdata/optdata files, now addressed in the F# compiler)

Many years ago, the F# Core Engineering Group created a NuGet package called FSharp.Core, partly to avoid a proliferation of "homebrew" packages appearing at that time.  3 years ago, this package was treated as the primary distribution mechanism for FSharp.Core in all scenarios. It is built, managed, and distributed from the F# development repository: https://github.com/dotnet/fsharp

From late 2017 to early 2020, the FSharp.Core package on NuGet has used multitargeting, providing two binaries: one that targets `net45` and another that targets `netstandard2.0`.

## Implementation
The implementation is easy.  We will remove the build for the desktop clr and all multi-fsharp.core testing support. This will make the build of FSharp.Core emit a single assembly that targets `netstandard2.0`.

Both binaries today have identical public API surface areas, so there is no concern about missing APIs by moving to `netstandard2.0` only. All API ability and behavior is tied to the existing behavior of .NET Standard 2.0 and .NET Framework - that is, everything "just works" if you are targeting .NET Framework 4.7.2 or higher. If you are targeting a lower .NET Framework version, there is a small chance that something could throw `PlatformNotSupportedException`. However, this is unlikely given the small amount of .NET APIs that are actually in use by FSharp.Core.

## Assumptions
For the purposes of this RFC we will assume that:

# Scenarios
Please contribute scenarios to the list below.

1. General purpose library developer targeting usage on both NetCoreApp *, and net472+ frameworks uses F# to build an library.
When the FSharp.Core targets netstandard 2.0  only then a nuget package with a single netstandard2.0 will work well, the developer will be able to build libraries that target all coreclr frameworks from netcoreapp2.0 up, and all desktop frameworks from net472 to net48.

2. Application developer targeting windows development build a command line application  uses F# to build an application.
The developer can target Windows using net472 and net48, or the coreclr with all netcoreapps from 2.0 up

3. Application developer targeting linux / mac development build a command line application  uses F# to build an application.
The developer can target Windows linux / mac or windows using the coreclr with all netcoreapps from 2.0 up

4. Application developer targeting windows / linux / mac using net5 uses F# to build an app.
The developer can target Windows linux / mac or windows using the coreclr with all net5 up

5. General purpose library developer targeting usage on net45+ frameworks uses F# to build an library.
netstandard2.0 is not well supported on versions of dotnet earlier than 4.7.2, in this circumstance the developer should reference the FSharp.Core nuget package version 4.7.3 or earlier to get the net45 version of FSharp.Core.  New language features that rely on an updated FSharp.Core will not work.

5. Application developer targeting usage on net45+ frameworks uses F# to build an application.
netstandard2.0 is not well supported on versions of dotnet earlier than 4.7.2, in this circumstance the developer should reference the FSharp.Core nuget package version 4.7.3 or earlier to get the net45 version of FSharp.Core.  New language features that rely on an updated FSharp.Core will not work.


# Open Questions (General)

# Alternatives
1.  Continue targeting multiple frameworks
Instead of building against a single netstandard2.0 framework we additionally target netstandard 2.1, net5 ... etc

Response:
Future frameworks are targeting release specific framework rids, I.e. netcoreapp3.0, netcoreapp3.1, net5 ... beyond.
If we chose to target specific frameworks with FSharp.Core library developers would be responsible for picking, developing and deploying rid specific FSharp.Cores.  Right now FSharp.Core doesn't need any rid specific code and so we will ship a single FSharp.Core with the widest reach possible.

# Acknowledgements
