# F# RFC FS-1032 - Support for F# in the dotnet sdk

A proposal is being put forward - initiated by Microsoft - to include F# functionality directly into [the dotnet SDK](https://github.com/dotnet/sdk), also known as "Microsoft.NET.Sdk",
which is the primary next-generation cross-platform SDK for .NET Framework and .NET Core programming. This work can be seen as the next logical step
in the evolution of [the FSharp.NET.SDK](https://github.com/dotnet/netcorecli-fsc/) - merging much of the funtionality into an even more core piece of .NET tooling.

Despite being a tooling issue rather than a language issue, this is being treated as an F# RFC to facilitate discussion.

* [ ] Most discussion is happening on [this PR](https://github.com/dotnet/sdk/pull/1172). There is also [the RFC discussion issue](https://github.com/fsharp/fslang-design/issues/188)

The implementation of this RFC is fragmented into [dotnet/sdk](https://github.com/dotnet/sdk) and [Microsoft/visualfsharp](https://github.com/Microsoft/visualfsharp).
Much of the functionality involves transitioning, merging or rejigging the logic of [FSharp.NET.Sdk](https://github.com/dotnet/netcorecli-fsc/).
Some relevant PRs are:

* [Update dotnet sdk to support F# and VS F#](https://github.com/dotnet/sdk/pull/1172)

* [Update fsharp deployments to include simple Microsoft.NET.Sdk.FSharp.props](https://github.com/Microsoft/visualfsharp/pull/2993)

## Summary

The proposal is that F# functionality should be added directly to Microsoft.NET.Sdk in a
technical manner "just like C# and Visual Basic".

If done right, this should give several benefits including a better overall experience for F# in more tooling, 
core integration of F# into the universe of tools that stem from the Microsoft.NET.SDK in a way that is the same
as C#. It comes with considerable drawbacks as well, such as speed of iteration and potential for duplication
of effort, see below.

## Background

The next generation of .NET programming tooling is being delivered via the [dotnet sdk](https://github.com/dotnet/sdk), aka "Microsoft.NET.Sdk".  This SDK supports both .NET Core and .NET Framework programming. (In some cases, the dotnet sdk support for .NET Framework programming implicitly references through to an existing install of the .NET Framework SDK or Mono tooling)

.NET programming is also supported by the [.NET CLI Tools](https://github.com/dotnet/cli) which
build on top of the dotnet sdk and include their own extension mechanism, which the F# tooling takes
advantage of. 

Up to this point, F# integration for this next generation of .NET tooling has been via [FSharp.NET.Sdk], which is an 
extension SDK to the Microsoft.NET.Sdk, bundled with the .NET CLI Tools through this reference [here](https://github.com/dotnet/cli/blob/85ca206d84633d658d7363894c4ea9d59e515c1a/build/BundledSdks.props#L8).


## Requirements

* Supports all existing scenarios of FSharp.NET.Sdk

* Minimal disruption and alteration to the Microsoft.NET.Sdk code.  (This implies some rejigging of the pieces of the FSharp.NET.Sdk implementation)

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

* No need to publish FSharp.NET.Sdk separately (except perhaps for templates? see below)

## Drawbacks
[drawbacks]: #drawbacks

* The proposal may not be approved and may be disruptive

* The F# community lose a point where they can inject new commands into the dotnet CLI tooling experience, e.g. "dotnet script".

* Speed of iteration on the tooling may be reduced

  --> The iteration speed of the FSharp.NET.Sdk has been incredible. However there is an inherent tradeoff
  here: deeper integration makes more rapid iteration more difficult. The fully open nature of the process
  and the fairly rapid iteration on preview releases of the Microsoft.NET.Sdk and the F# tools should
  alleviate many of these concerns.

* The implementation may ignore the many technical issues addressed by the [FSharp.NET.Sdk] work.

  --> This can be addressed in part by making sure all work is referenced and compared with the relevant FSharp.NET.Sdk functionality.

* The tooling may be biased to only address issues relevant to the proposers (Microsoft and VIsual Studio).

  --> This can be addressed by a proper RFC and by making sure the implementation is fully transparent.

## Unresolved questions
[unresolved]: #unresolved-questions

### Templating

Templating for the next generation of .NET tooling is handled at [dotnet/templating](https://github.com/dotnet/templating). In that repo, the FSharp.NET.Sdk currently has the "good" name for F#, as in 

    dotnet new --lang F# 

Historically templates has been a major issue for the FSharp.NET.Sdk.  In the words of @enricosada:

> - templates BITE ME HARD, A LOT, MULTIPLE TIMES, WHEN I WAS ALREADY ON THE FLOOR. for lots of reasons (and not just myself).

We have options here:

- We can propose switch the F# sdk templates to F#.sdk, and the integrated F# to use F#.  And have two sets of F# templates … 

- We can have no templates in Microsoft.NET.Sdk and repurpose FSharp.NET.Sdk to be a collection of templates.

There is ongoing dicussion on this point. It may be treated as an orthogonal issue. Relevant comment from @dsyme:

> Historically we’ve tried to encourage developers to contribute templates to core tooling many times – but the turnaround times have been too long to make the mechanism useful.  If a developer contributes a template today, it needs to be usable by the developer (and her friends, and her friends friends) tomorrow – via the standard “dotnet new -lang F#” – without any reference to preview packages or alpha feeds - otherwise the templates just get completely stale, and no one contributes them.

This indicates that the templating delivery mechanism may need much more rapid turnaround time than available through the mechanisms proposed in this RFC.

### Mono

There is a concern that Mono may not be correctly addressed. 

It is very important to get a grip on the Mono angle to this work.  It is proposed that the FSharp.NET.Sdk test matrix or an equivalent integrated into visualfsharp repo as part of this work.  

For the FSharp.NET.Sdk,  using msbuild on Mono with new-style project files will run ``mono fsc.exe`` from the referenced ``FSharp.Compiler.Tools`` package, see [this](https://bugzilla.xamarin.com/show_bug.cgi?id=55626)

Relevant comment from @dsyme

> The value that the FSharp.NET.Sdk has brought in this space is vast.   Just last week Enrico really achieved an amazing “convergence” result when the same tooling could build the same project files on .NET Core, Mono, .NET Framework and everything works (apart from the few orthogonal known issues, which are on all platforms). I can’t tell you what a breakthrough this is for F#.  For the first time, cross-platform F# project build/test tooling feels like it is on a convergence path – the new .NET tooling is really going to simplify everyone’s lives. Every F# repo was suddenly going to become simpler.
> 
> I was so excited by this result I told my wife.  That’s a sure indicator that we have to make sure we keep that value. 

### Bundling of FSharp.NET.Sdk with CLI tools

If F# support goes in the Microsoft.NET.Sdk, then it would seem logical to remove the bundling of
the FSharp.NET.Sdk in the .NET CLI tools or to reduce the FSharp.NET.Sdk package to only deal with some orthogonal issue
such as templating.  It doesn't make sense to have two F# stories in the .NET CLI tools.

@KevinRansom adds:

> It has to continue to work, we will not remove it from the dotnet cli.  We assume that developers have existing projects that make use of it, we need to ensure that those projects build with the new tooling.  Currently they don’t because dotnet cli doesnot ship a with the ability to run 1.0 apps … super weirdly … I will put in a diversion to make it use the deployed compiler so that they work.


### Others

* There are open questions about how Roslyn Common Project System tooling integrates
  F# support, see [this PR](https://github.com/dotnet/project-system/pull/1670), [this comment thread](https://github.com/dotnet/project-system/pull/1670) and 
  potentially other ongoing work.

* There are numerouow [FSharp.NET SDK bugs](https://github.com/dotnet/netcorecli-fsc/issues)
  like [this one](https://github.com/dotnet/netcorecli-fsc/issues/93). The list is an interesting guide.
  For example, it looks like we need to add F# support to [this code](https://github.com/Microsoft/msbuild/blob/master/src/Tasks/WriteCodeFragment.cs#L294).

## Alternatives
[alternatives]: #alternatives

* One alternative would be to continue with [FSharp.NET.Sdk] and not integrate F# support directly into Microsoft.NET.Sdk.


## Acknowledgements

It is normal to keep RFCs depersonalized.  However, in this case it is worth breaking that rule, and noting as a community that the development of the FSharp.NET.Sdk has been a long, arduous labour-of-love by the various contributors, but particularly by @enricosada.  What has become the FSharp.NET.Sdk tooling has taken 4 (!) iterations to get to the current state, operating in an incredibly confusing changing landscape of dotnetcli, dnx, .NET Core, Mono, msbuild and many other changing/moving parts.  These iterations have allowed nearly all the bugs in the interaction between the F# compiler and targets and the .NET SDK to be ironed out - without this integrating F# more deeply into the dotnet SDK would not be possible.  The FSharp.NET.Sdk has also been crucial in getting F# on .NET Core to a large initial audience.   This RFC acknowledges these efforts as a huge success, no matter how this proposed RFC is taken forward.


