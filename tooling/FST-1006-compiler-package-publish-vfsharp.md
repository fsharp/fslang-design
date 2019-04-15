# [DENIED] FST-1006-compiler-package-publish-vfsharp

**This RFC has not been accepted. It remains here for historical purposes.**

This RFC proposes that we publish the [F# Compiler Tools package (FCT)](https://www.nuget.org/packages/FSharp.Compiler.Tools/) package directly from the [Visual F# repository](https://github.com/Microsoft/visualfsharp) rather than the existing [F# packaging repository](https://github.com/fsharp/fsharp).

## Background

Today, changes to the F# desktop compiler SDK are made in the Visual F# repository. When they are merged, the source code changes must also be ported over to the F# packaging repository before a new version of FCT can be published. This process is done manually and can sometimes be a bottleneck for people who need a fix.

Additionally, although the FCT bits are the same as what is developed in the Visual F# repository, they are not signed with the Microsoft signing keys, nor is the package signed with the Microsoft signing keys. This means that some organizations may not feel "safe" with this package in their systems, even if the bits are still the same as what they would get from the Visual Studio Build Tools SKU.

## Details

The details about publishing are small in number:

* Visual F# repository can publish FCT just like FSharp.Core
* FCT binaries are signed with Microsoft signing keys
* FCT package is signed with Microsoft signing keys
* Owners of the package are Microsoft and the F# Software Foundation (co-ownership model, just like FSharp.Core)

Additionally, this package is _not_ intended for use with .NET Core, nor will this proposal change that. If you need to build F# and .NET Core assets, you must use the .NET SDK just like today.
