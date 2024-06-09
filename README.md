# F# Language Design RFCs 

RFCs ([Request for Comments](https://en.wikipedia.org/wiki/Request_for_Comments)) and docs related to the F# language design process. 

All open RFCs that have not been released live under `/RFCs`.

All RFCs that have been implemented in a preview of F# live under `/preview`.

All release language and FSharp.Core RFCs live under a folder with their respective release.

All tooling RFCs (typically for cross-cutting, cross-editor tooling) live under `/tooling`.

### The Process:

1. Use [F# Language Suggestions](https://github.com/fsharp/fslang-suggestions) to submit ideas, vote on them and discuss them.

2. Ideas which get "approved in principle" get an [RFC entry](https://github.com/fsharp/fslang-design/tree/master/RFCs) based on the [template](https://github.com/fsharp/fslang-design/blob/master/RFC_template.md), and a corresponding [RFC discussion thread](https://github.com/fsharp/fslang-design/issues)

   There is currently a backlog of approved ideas. If an idea has been approved and you'd
   like to accelerate the creation of an RFC, send a PR creating the RFC document for any approved-in-principle issue.   
   First in first served.  To "grab the token" send a PR doing nothing but creating or naming the RFC file, and
   then fill in the further details with additional commits to the PR.

   * to pick the RFC numbered identifier, you can run `dotnet fsi find.next.id.fsx`
   * to name the file, tooling RFCs are prefixed with `FST-`, language RFC are prefixed with `FS-`, use `-` and lower casing (relaxed for code identifiers), giving descriptive name
   * the file location is always under RFCs and get moved by owners of the repository when it ships in a preview or a release.



3. Implementations and testing are submitted to the [dotnet/fsharp repository](https://github.com/dotnet/fsharp).

When RFCs are implemented and a version of F# is revved, the RFCs which correspond to the F# version they were implemented in are archived under the appropriate folder.

### Language Update Release Trains

1. Delivery of language features is via RFCs plus implementations submitted to https://github.com/dotnet/fsharp.

2. New features that meet our quality bar are then merged and placed beyond a preview flag in the F# compiler and FSharp.Core.

3. When we have a handful of 100% “ready, tested and completed” FSharp.Core and F# Language features in preview that aren't "too minor", then we will bump the language version and begin tactical work to release them in a few  months.

4. New releases of F# typically align with a .NET and/or Visual Studio version.

### Who is in Charge?

Historically the designer of F# has been Don Syme (@dsyme). Practically speaking, today most of the design process operates through the efforts of contributors, overseen by those with commit rights on this repository, who are currently @vzarytovskii, @dsyme, @cartermp, @baronfel, and @abelbraaksma. Much of the work happens by contributions via RFCs and most features now proceed from initial approval all the way to implementation through community and enterprise contributions. The planning and progress process for features is intended to be transparent and participative.

Throughout feature development the needs of all stakeholders can be taken into account, e.g. the needs of those delivering tooling or long-term support for F#. Together with the F# community, the overseers of the design process will continue to refine this process based on community and delivery needs.

### Roadmap and Areas of Priority Work

Any or all of the [approved-in-principle](https://github.com/fsharp/fslang-suggestions/labels/approved-in-principle) items are eligible to catch a release train. That is as good as it gets for a "roadmap" for the language design.

There is a list of "proposed priority" approved language design items.  This is _not_ a roadmap, because other people will choose to prioritize other approved items, and is also subject to change and edit. You can find that list here: https://github.com/fsharp/fslang-suggestions/labels/proposed-priority.

## Style Guide

The F# style guide is hosted as part of the [.NET docs for F#](https://docs.microsoft.com/dotnet/fsharp/style-guide/) and by default [Fantomas](https://github.com/fsprojects/fantomas) aims to implement this style guide.

The design process for raising issues arising about this style guide is managed via issues in this repository.  Issues are noted by `[style-guide]` in the title.

The style guide itself is adjusted via PRs to [the style guide doc](https://github.com/dotnet/docs/blob/main/docs/fsharp/style-guide/formatting.md) however discussion should happen here.

Adjustments to the style guide should generally only be made with consideration about their implementability in Fantomas, and if an adjustment is approved you should be prepared to contribute a matching pull request to Fantomas.

The decision maker for the style guide is @dsyme, with input/veto from @nojaf (current maintainer of Fantomas) and input from all interested parties.

## Code of Conduct

This repository is governed by the [Contributor Covenant Code of Conduct](https://www.contributor-covenant.org/).

We pledge to be overt in our openness, welcoming all people to contribute, and pledging in return to value them as whole human beings and to foster an atmosphere of kindness, cooperation, and understanding.
