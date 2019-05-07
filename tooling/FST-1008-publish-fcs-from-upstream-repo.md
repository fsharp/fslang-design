# FST 1008 Publish FCS from the dotnet/fsharp repository

This RFC proposes that we publish the [FSharp.Compiler.Service](https://www.nuget.org/packages/FSharp.Compiler.Service/) (FCS) package from the main F# development repository (`dotnet/fsharp`) and archive the [current downstream repository](https://github.com/fsharp/fsharp.compiler.service).

## Publishing the package from dotnet/fsharp

### Signing

For starters, as per Microsoft policy, any official package considered to be a part of the F# product must be signed:

* The binaries must be signed by Microsoft signing keys
* The package itself must be signed by Microsoft signing keys

This would be the same as FSharp.Core today.

### Prerelease usage

The F# build would produce a signed FCS package. This would be accessible in a build artifacts directory for anyone looking to build this package against a particular branch of the F# codebase.

Additionally, Microsoft will publish a nightly feed on MyGet where a new package would be published every time there is a signed F# build.

These two avenues for consuming a prerelease version of the package should be satisfactory for all tooling authors and those looking to use the package for other purposes.

## Package ownership

We propose that package ownership follows the same model as [FSharp.Core](https://www.nuget.org/packages/FSharp.Core/) - namely, it has two official owners: Microsoft and fsharporg. Each of these owners are considered groups where individuals can be added and removed. In this case, we would expect current non-Microsoft maintainers to be members of the fsharporg group so that they can manage the package itself should a Microsoft employee not be available to do so.

The one quirk here, when compared with the current state of FCS, is that only Microsoft employees would be able to ensure new _binaries_ are able to be produced when needed. In other words, members of the fsharporg group can publish/delist/etc. the package itself, including uploading a new package from the nightly feed, but they cannot actually update the binaries independently of the output of the signed build. We don't see this as an issue in practice because the nightly feed will always be the most up to date build of the bits needed in the package itself, but it is worth calling out.

## Documentation and webpage updates

Today, the FCS [website](http://fsharp.github.io/FSharp.Compiler.Service/) has useful documentation for people who are looking to get started with using the service. This webpage must be capable of being produced and updated from the `dotnet/fsharp` repository. There are a few proposed variations on the URL:

* The same as before: `http://fsharp.github.io/FSharp.Compiler.Service/`
* Using `dotnet`: `http://dotnet.github.io/FSharp.Compiler.Service/`
* Using `dotnet/fsharp`: `http://dotnet.github.io/fsharp/FSharp.Compiler.Service/`

The first is preferred so that links need not be updated across the web, so that is the priority to get. But if it cannot be done, they are listed in order of preference.

## Archiving the FCS repository

Once the `dotnet/fsharp` repository is set up to publish nightlies of the FCS package and the NuGet package itself has been set up as documented above, the process for archiving the FCS repository can begin. It's not as simple as merely archiving the repository, however:

### Migrating issues

At the time of writing, there are 73 open issues. Each will need to be determined to be valid or invalid. If they are valid, they must be moved to `dotnet/fsharp` and tagged appropriately. There should be 0 remaining issues open in this repository.

### Migrating pull requests

At the time of writing, there are 3 open pull requests. If they are valid, they should be moved to `dotnet/fsharp` and closed in the FCS repository. There should be 0 remaining pull requests open.

### README and repo description updates

The README must be modified to state clearly that this repository is archived, all open issues have been moved to `dotnet/fsharp`, and any new issues or PRs should be filed there.

Additionally, we should retain the historical context of this repository to ensure that we don't lose any of the rich history it has developed over the years.

Finally, once all of the above is completed, we can archive the repository.

## Compatibility and versioning

Despite the package being at version `28.0.0` at the time of writing, it's best to think of it as version `0.28`. It is not binary compatible and is subject to many breaking changes. The FCS API itself is likely to undergo more changes in the future, and consumers of that API will have to adjust to those changes. This is the norm today, and we expect it to continue for quite some time. Given this, the compatibility and versioning rules are:

* The binary in the package is **not** binary compatible and will change
* The package will follow semantic versioning, where any major version change indicates a possible breaking change

In the future, the FCS APIs may stabilize and binary compatibility may be enforced. But that is at an unknown time long in the future.

## Fable and other dialects of F# that use FCS

[Fable](http://fable.io/) is the most prominent dialect of F#. The Fable tool chain uses a [fork of FCS](https://github.com/fable-compiler/Fable/tree/master/src/fcs-fable) that adds a few things to the codebase to make Fable possible.

Moving to `dotnet/fsharp` makes this a bit more challenging for Fable maintainers to keep up to date:

* FCS being a part of the larger repository means there is more churn to keep up with
* The build process for `fcs-fable` is a bit more complicated

This can be eased by setting up a bot that automatically merges into a fork of `dotnet/fsharp` that Fable would use, but increased churn is clearly a downside even if a bot can automate the routine stuff.

However, when positive changes for Fable are made to the F# compiler itself, those changes can be absorbed immediately, rather than waiting for a manual merge to a downstream positive. So that is a positive.

## Build process

Today, FCS does not require the installation of too many dependencies nor does it require Windows. This is especially important for Fable.

Given this, building FCS from `dotnet/fsharp` should also require no additional dependencies. It should be as [simple as it is today](https://github.com/Microsoft/visualfsharp/tree/master/fcs#building-testing-packaging-releases) in the current FCS source.

To reiterate - to build FCS in `dotnet/fsharp`:

* You should only require an OS that .NET Core can run on; i.e., no Windows-only dependencies
* You should not need to install any machine dependencies to build FCS
* You should be able to run all FCS tests without machine dependencies or a particular OS
* You should be able to edit the FCS source in your editor of choice by opening `FSharp.Compiler.Service.sln`
* You should be able to produce an unsigned `.nupkg` of FCS for personal use