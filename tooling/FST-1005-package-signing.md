# FS-1005 Package Signing

This is inspired by [this PR](https://github.com/Microsoft/visualfsharp/pull/4734).

As per the requirements by Microsoft's Developer Division, all NuGet packages which are developed and distributed by Microsoft (including if they are co-owned) must be signed with Microsoft's signing keys. This includes the following packages:

* FSharp.Core

(The list is short right now, but should we start publishing other `FSharp.*` packages, this list would update)

## What does this mean?

The binaries in the above-mentioned packages are already signed by Microsoft. This means that the packages themselves are signed as well.

## What does this not mean?

Any of the following do not apply:

* Removing ownership of packages from F# Sofware Foundation
* Removing ability of F# Software Foundation to manage the above projects in conjunction with Microsoft
* Removing of any existing permissions or capabilities for the F# Software Foundation

In short, the existing relationship between Microsoft and FSSF as far as package ownership goes remains the same: Microsoft and the FSSF are co-owners of the above packages.
