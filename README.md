# F# Language Design RFCs 

RFCs (requests for comments) and docs related to the F# language design process. 

All open RFCs that have not been released live under `/RFCs`.

All RFCs that have been implemented in a preview of F# live under `/preview`.

All release language and FSharp.Core RFCs live under a folder with their respective release.

All tooling RFCs (typically for cross-cutting, cross-editor tooling) live under `/tooling`.

### The Process:

1. Use [F# Language Suggestions](https://github.com/fsharp/fslang-suggestions) to submit ideas, vote on them and discuss them.

2. Ideas which get "approved in principle" get an [RFC entry](https://github.com/fsharp/fslang-design/tree/master/RFCs) based on the [template](https://github.com/fsharp/fslang-design/blob/master/RFC_template.md), and a corresponding [RFC discussion thread](https://github.com/fsharp/fslang-design/issues)

   There is currently a backlog of approved ideas. If an idea has been approved and you'd
   like to accelerate the creation of an RFC,  send a PR creating the RFC document for any approved-in-principle issue.
   First in first served.  To "grab the token" send a PR doing nothing but creating or naming the RFC file, and
   then fill in the further details with additional commits to the PR.

3. Implementations and testing are submitted to the [dotnet/fsharp repository](https://github.com/Microsoft/visualfsharp).

When RFCs are implemented and a version of F# is revved, the RFCs which correspond to the F# version they were implemented in are archived under the appropriate folder.

### Language Update Release Trains

1. Delivery of language features is via RFCs plus implementations submitted to https://github.com/dotnet/fsharp.

2. New features that meet our quality bar are then merged and placed beyond a preview flag in the F# compiler and FSharp.Core.

3. When we have a handful of 100% “ready, tested and completed” FSharp.Core and F# Language features in preview that aren't "too minor", then we will bump the language version and begin tactical work to release them in a few  months.

4. New releases of F# typically align with a .NET and/or Visual Studio version.

### Roadmap and Areas of Priority Work

Any or all of the [approved-in-principle](https://github.com/fsharp/fslang-suggestions/labels/approved-in-principle) items are eligible to catch a release train. That is as good as it gets for a "roadmap" for the language design.

The BDFL (@dsyme) has put together a list of "proposed priority" approved language design items which he plans to focus on or which other people have taken past RFC stage.  This is _not_ a roadmap, because other people will choose to prioritize other approved items (e.g. match! - which is not a priority item for me), and is also subject to change and edit.. It is an informal list.  You can find that list here: https://github.com/fsharp/fslang-suggestions/labels/proposed-priority

## Code of Conduct

This repository is governed by the [Contributor Covenant Code of Conduct](https://www.contributor-covenant.org/).

We pledge to be overt in our openness, welcoming all people to contribute, and pledging in return to value them as whole human beings and to foster an atmosphere of kindness, cooperation, and understanding.
