# FST-1007 Move VisualFSharp repo to the dotnet organization

This RFC proposes that we move the [microsoft/visualfsharp repository](https://github.com/microsoft/visualfsharp) to the .NET organization and rename it to `dotnet/fsharp`.

## Rationale

There are a few reasons why we're interested in moving the VisualFSharp repo:

* The name is/feels old. The compiler and tools we ship in Visual Studio is still called "Visual F#", but there's nothing visual about it (no designer support), no active website refers to the product this way, the Visual Studio release notes do not refer to the product this way, and practically speaking, most F# users do not do this either. Note that C# does not name their repository "VisualCSharp", and doing so would probably be greatly scorned by the greater C# and .NET community.
* All Microsoft employees on the F# team are a part of the .NET organization at Microsoft. Every team under the .NET organization has their products under the [dotnet](github.com/dotnet) orgnaization on GitHub, which is legally an independent entity from Microsoft. We feel that this aligns quite well with the independent nature of F# while also maintaining the reality that it is primarily Microsoft employees who develop and maintain the F# compiler, FSharp.Core, and F# tools for Visual Studio.
* Microsoft regularly does cross-repo issue migration when a bug is filed in repository X but is actually a concern for repository Y. This is more difficult with the F# repository residing under the Microsoft organization rather than the dotnet organization.
* F# can also be considered a .NET Foundation project, which could offer increased visibility to the greater .NET organization.

## Practical issues to address

* Transfer of IP and other legal matters - All covered.
* Issues/PRs/discussions/etc. migration - GitHub handles this automatically. No history should be lost in either the commits nor comments.
* Links across the web that link to microsoft/visualfsharp - GitHub handles this automatically.
