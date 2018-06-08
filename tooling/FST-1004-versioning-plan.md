# F# Tooling RFC FST-1004 - Versioning

[Discussion here](https://github.com/fsharp/fslang-design/issues/250).

We have strong motivation to decouple versioning of the F# compiler and tools from the F# language and core library. The reasoning is as follows:

* The F# major language version number will change more and more slowly over time.  Proposed and approved new features still fit snugly in the minor version number.
* The F# minor language version is expected to change, but not as often as tooling and SDK versions.
* Decoupling the FSharp.Compiler.Service version from the language version has proven to be a good thing, as it has allowed the adoption of SemVer and rapid iteration.  A similar model can be applied for the F# compiler and tooling.
* Coupling the compiler version with the language version is not always done in other languages (e.g., C#), and such decoupling hasn't been a problem.

## Table of versions

| Visual Studio version | 15.5 (current) | 15.6 | 15.7 | 15.8 | vNext |
|------------|----------:|-----:|------:|------:|------:|
| **F# language version** | 4.1 | 4.1 | 4.1 | 4.5 | 4.n |
| **FSharp.Core version** | 4.4.1.0 | 4.4.3.w | 4.4.3.w | 4.5.w.0 | 4.n.w.0 |
| **FSharp.Core NuGet package version** | 4.2.x | 4.3.x | 4.3.x | 4.5.x | 4.n.x |
| **F# compiler banner** | F# Compiler 4.1 | F# Compiler 10.0.a for F# 4.1 | F# Compiler 10.1.a for F# 4.1 |F# Compiler 10.2.b for F# 4.5 | F# Compiler XX.a.b for F# 4.n |
| **VS product Name** | Visual F# 4.1 | Visual F# 10.0.a for F# 4.1 | Visual F# 10.1.a for F# 4.1 | Visual F# 10.2.b for F# 4.5 | Visual F# XX.a.b for F# 4.n |
| **VS product details** | Microsoft Visual F# 4.1 | Microsoft Visual F# 10.0.a for F# 4.1 | Microsoft Visual F# 10.1.a for F# 4.1 | Microsoft Visual F# 10.2.b for F# 4.5 | Microsoft Visual F# XX.a.b for F# 4.n |
| **F# SDK version** | 4.1 | 10.0.a | 10.1.a | 10.2.a | ZZ.a |
| **F# Compiler .dll version** | 4.4.1.0 | 10.0.a.b | 10.1.a.b | 10.2.a.b | WW.a.b.c |
| **FSharp.Compiler.Tools version** | 4.1.x | 10.0.a for F# 4.1 | 10.1.a for F# 4.1 | 10.2.b for F# 4.5 | XX.a.b for F# 4.n |
| **VS Assembly versions** | 15.4.x.y | 15.6.e.f | 15.7.e.f | 15.8.e.f | vNext.e.f |

Where:

* `n` - occasional sequence of increments of F# language version
* `w` - minor increment of FSharp.Core assembly version. Normally 0
* `x` - minor increment of FSharp.Core nuget package version (may or may not change FSharp.Core assembly version)
* `a.b.c` - sequence for compiler tools, independent of other versioning
* `e.f` - sequence of Visual Studio binary version numbers, independent of other versioning

You'll note a few things:

* There will be no F# 4.2, F# 4.3, or F# 4.4 language version - it will skip to F# 4.5.  This is because it will allow versions of the F# language, FSharp.Core, and FSharp.Core NuGet package to line up in a VS 15.7 timeframe.
* The Compiler and SDK version will start at 10.x.y, and so on. This is to ensure that the F# language version will never catch up, and lets us start on the path of SemVer.
* Visual Studio assembly versions will match the VS release. It should be plainly obvious if an assembly is a part of Visual Studio this way.
* FSharp.Compiler.Service is **not** a part of this plan.  Given that it has already been on this separate versioning path, and may need to version independently of the assets Microsoft produces for a number of reasons, we intend on keeping it as-is.

We believe that this will make versioning a bit more sane moving forward, as it gives us the ability to rev tooling at a faster pace than the language, while also laying out a plan to align F# language versions, FSharp.Core versions, and FSharp.Core NuGet package versions.

## Options for FSharp.Core

How to handle FSharp.Core and its NuGet package moving forward is still an open question. Although the plan is for the versions of the assembly and package to match, the relationship between FSharp.Core and the F# language could change as well.  There are really two options on the table.

### Option 1: Version in lockstep with the F# Language

This is straightforward, and aligns with how we've versioned things in the past.

**Benefits:**

* FSharp.Core and its NuGet package doesn't change very often
* It's always obvious which version of the assembly and package is for which language version
* This is roughly how things have worked before

**Drawbacks:**

* The only changes that can go in between language releases are patches
* We will have to either (a) sit on improvements until the language revs, or (b) release improvements via a patch version

### Option 2: Version independently from the F# language

This is less straightforward, and is a bit of a departure from past behavior.

**Benefits:**

* Freedom to release a new version with improvements whenever we want
* Independence from the language version means people may be more likely to take upgrades
* We won't be sitting on new features

**Drawbacks:**

* It may not be obvious which version of FSharp.Core has new language features that are incompatible with previous versions
* It may not be possible to convey meaning via the version number
* There would need to be a policy in place for the F# Compiler, indicating the highest version of FSharp.Core it will be guaranteed to load

To make this option a reality, we must solve the problem with referencing higher versions of FSharp.Core at runtime. The following policy points are proposed:

* Each release of the F# SDK defines a highest version of FSharp.Core that it is guaranteed to load successfully.
* The highest version of FSharp.Core is the version that the F# SDK releases with. It may or may not be a new version.
* If someone references a higher version of FSharp.Core than what is defined in that SDK release, a warning will be emitted.
* Patch versions are ignored; that is, in FSharp.Core W.X.Y.Z, the Y and Z values are not counted in this policy.

Imagine the following scenario:

The F# SDK version 11.0.0 is released, and it defines FSharp.Core 4.6.x.y as the highest version of FSharp.Core guaranteed to load successfully.

* If a user attempts to load FSharp.Core 4.6.0.0, the check is satisfied.
* If a user attempts to load FSharp.Core 4.6.3.2, the check is satisfied.
* If a user attempts to load FSharp.Core 4.7.0.0, the check is **not** satisifed, and a warning is emitted:

"The version of FSharp.Core you attempted to load (4.7.0.0) is higher than the highest version of FSharp.Core that is guaranteed to load (4.6.x.y) on F# SDK version 11.0.0. Your code may fail at runtime. See here for a matrix of F# SDK and FSharp.Core versions: [LINK]"

Failing such a policy, if someone were to decide that they want to use a higher version of FSharp.Core than what was released with the version of the F# SDK they have on their machine, there is no telling if it would load successfully, and if it failed, it would be difficult to diagnose why.

In other words, we're open to giving people the rope to hang themselves with, should they decide that they want to do this.
