# F# RFC FS-1032 - Support for F# in the dotnet sdk

A proposal is being put forward - initially by Microsoft - to include F# functionality directly into [the dotnet SDK](https://github.com/dotnet/sdk), also known as "Microsoft.NET.Sdk",
which is the primary next-generation cross-platform SDK for .NET Framework and .NET Core programming. This work can be seen as the next logical step
in the evolution of [the FSharp.NET.SDK](https://github.com/dotnet/netcorecli-fsc/).

Despite being a tooling issue rather than a language issue, this is being treated as an F# RFC to facilitate discussion.

* [ ] Most discussion is happening on [this PR](https://github.com/dotnet/sdk/pull/1172). There is also [the RFC discussion issue](https://github.com/fsharp/fslang-design/issues/188)

The implementation of this RFC is fragmented into [dotnet/sdk](https://github.com/dotnet/sdk) and [Microsoft/visualfsharp](https://github.com/Microsoft/visualfsharp).
Much of the functionality involves transitioning, merging or rejigging the logic of [FSharp.NET.Sdk](https://github.com/dotnet/netcorecli-fsc/).
Some relevant PRs are:

* [Update dotnet sdk to support dotnet cli F# and VS F#](https://github.com/dotnet/sdk/pull/1172)

* [Update fsharp deployments to include simple Microsoft.NET.Sdk.FSharp.props](https://github.com/Microsoft/visualfsharp/pull/2993)

## Summary

The proposal is that F# functionality should be added directly to Microsoft.NET.Sdk in a
technical manner "just like C# and Visual Basic".

If done right, this should give several benefits including a better overall experience for F# in more tooling, 
core integration of F# into the universe of tools that stem from the Microsoft.NET.SDK (particularly in cases
where the .NET CLI tools are **not** being assumed or if the evolution path of the .NET CLI tools changes).
It comes with considerable drawbacks as well, such as speed of iteration and potential for duplication
of effort, see below.

## Background

The next generation of .NET programming tooling is being delivered via the [Microsoft.NET.Sdk](https://github.com/dotnet/sdk), aka "dotnet sdk".  This SDK also
helps support both .NET Core and .NET Framework programming. (In some cases, support for .NET Framework programming relies on referencing through
to an existing install of the .NET Framework SDK)

.NET programming is also supported by the [.NET CLI Tools](https://github.com/dotnet/cli). Up to this point, F# integration for this next generation of .NET tooling has been via [FSharp.NET.Sdk], whcih can be
seen as an extension SDK to the .NET CLI Tools. At the time of writing the FSharp.NET.Sdk is "bundled" with the .NET 
CLI tools through this reference [here](https://github.com/dotnet/cli/blob/85ca206d84633d658d7363894c4ea9d59e515c1a/build/BundledSdks.props#L8).

## Requirements

* Supports all existing scenarios of FSharp.NET.Sdk

* The tooling can be pointed to an updated F# compiler, e.g. a compiler deliverd via the
  FSharp.Compiler.Tools package, see [this comment](https://github.com/dotnet/sdk/pull/1172#issuecomment-299280631) for example.


## Advantages

If done right, integrating F# support directly into the Microsoft.NET.Sdk should give several benefits
* a core integration of F# into the universe of tools that stem from the Microsoft.NET.SDK
* aligns F# with future engineering of the Microsoft.NET.SDK so that any work that is done for other languages will also benefit F# 
* will prevent "drift" where F# is "just different" and "on the side" and treated as a special case by people doing core .NET engineering.
* help solve some technical issues for Visual Studio tooling: it ensures that the default compiler used by projects is exactly the compiler installed by Visual Studio, and ensure the compiler is NGEN'd to be fast.

Some specific technical advantages:

* This arrangement will allow editor tooling, desktop MSBuild and dotnet Cli to build FSharp apps

* This will ensure that editor tooling does not need to o a project restore to get the SDK and compiler on project load

* Installs of the MIcrosoft.NET.Sdk get to NGEN or crossgen the F# compiler on install. This is a considerable thing,
  and absolutely crucial for fast  compilation of large projects on Windows.

* Only one copy of the F# compiler gets installed when using default settings. That's good for download times
  A new F# project + build will not require any package references to be downloaded. Right now a copy of the compiler gets downloaded/installed

* The default project files can be a little simpler (no reference to the compiler package nor FSharp.NET.Sdk)

* No need to publish FSharp.NET.Sdk separately.

# Drawbacks
[drawbacks]: #drawbacks

* The proposal may not be approved and may be disruptive

* Speed of iteration on the tooling may be reduced

  --> The iteration speed of the FSHarp.NET.Sdk has been incredible. However there is an inherent tradeoff
  here: deeper integration makes more rapid iteration more difficult. The fully open nature of the process
  and the fairly rapid iteration on preview releases of the Microsoft.NET.Sdk and the F# tools should
  alleviate many of these concerns.

* The implementation may ignore the many technical issues addressed by the [FSharp.NET.Sdk] work.

  --> This can be addressed in part by making sure all work is referenced and compared with the relevant FSharp.NET.Sdk functionality.

* The tooling may be biased to only address issues relevant to the proposers (Microsoft and VIsual Studio).

  --> This can be addressed by a proper RFC and by making sure the implementation is fully transparent.

# Unresolved questions
[unresolved]: #unresolved-questions

* If F# support goes in the Microsoft.NET.Sdk, then it would seem logical to remove the bundling of
  the FSharp.NET.Sdk in the .NET CLI tools.  It doesn't make sense to have two F# stories in the .NET CLI tools.

* Mono may not be correctly addressed. Mono 5.0, when run with msbuild, it will run mono fsc.exe (net40)
  from FSharp.Compiler.Tools package, see https://bugzilla.xamarin.com/show_bug.cgi?id=55626

* There are open questions about how Roslyn Common Project System tooling integrates
  F# support, see [this PR](https://github.com/dotnet/project-system/pull/1670), [this comment thread](https://github.com/dotnet/project-system/pull/1670) and 
  potentially other ongoing work.

* There are numerouow [FSharp.NET SDK bugs](https://github.com/dotnet/netcorecli-fsc/issues)
  like [this one](https://github.com/dotnet/netcorecli-fsc/issues/93). The list is an interesting guide.
  For example, it looks like we need to add F# support to [this code](https://github.com/Microsoft/msbuild/blob/master/src/Tasks/WriteCodeFragment.cs#L294).

# Alternatives
[alternatives]: #alternatives

* One alternative would be to continue with [FSharp.NET.Sdk] and not integrate F# support directly into Microsoft.NET.Sdk.




