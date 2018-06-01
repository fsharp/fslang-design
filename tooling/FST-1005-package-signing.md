# FS-1005 Package Authoring for Microsoft co-owned packages

As per the requirements by Microsoft's Developer Division, all NuGet packages which are developed and distributed by Microsoft (including if they are co-owned) must be adhere to the following:

* Co-owned on [nuget.org](https://www.nuget.org/) by the [Microsoft](https://www.nuget.org/profiles/microsoft) and [fsharporg](https://www.nuget.org/profiles/fsharporg) organizations.
* Binary is signed with Microsoft signing keys.
* Package itself is signed with Microsoft signing keys.
* The following package authoring:
    * **Author** includes "Microsoft".
    * **Owner** includes "Microsoft".
    * **Copyright** is "© Microsoft Corporation. All rights reserved".
    * **License URL** points to the Visual F# MIT license.
    * **RequireLicenseAcceptance** is set to true.
    * **Project URL** points to the Visual F# repo on GitHub.

The following packages will follow this:

* FSharp.Core

## What does this mean?

This is effectively Microsoft getting their ducks in row. That is, the days of publishing with person accounts (e.g., "dsyme", "kevinransom") are no longer allowed. If a package is authored or co-authored by a Microsoft team, it must be presented as "Microsoft" and not an individual account for a given employee.

## What does this not mean?

Any of the following do not apply:

* Removing ownership of packages from F# Sofware Foundation
* Removing ability of F# Software Foundation to manage the above projects in conjunction with Microsoft
* Removing of any existing permissions or capabilities for the F# Software Foundation

In short, the existing relationship between Microsoft and FSSF as far as package ownership goes remains the same: Microsoft and the FSSF are co-owners of the above packages, and both maintain the rights to manage assets.
