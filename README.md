# F# Language Design RFCs 

RFCs (requests for comments) and docs related to the F# language design process. 

* [Open F# Language RFCs (including candidates for F# vNext)](https://github.com/fsharp/fslang-design/blob/master/RFCs)

* [RFCs implemented in F# 4.5](https://github.com/fsharp/fslang-design/blob/master/FSharp-4.5)

* [RFCs implemented in FSharp.Core 4.4.5.0](https://github.com/fsharp/fslang-design/tree/master/FSharp.Core-4.4.5.0)

* [RFCs implemented in F# 4.1 update](https://github.com/fsharp/fslang-design/blob/master/FSharp-4.1b)

* [RFCs implemented in FSharp.Core 4.4.3.0](https://github.com/fsharp/fslang-design/tree/master/FSharp.Core-4.4.3.0)

* [RFCs implemented in F# 4.1](https://github.com/fsharp/fslang-design/blob/master/FSharp-4.1)

* [RFCs implemented in F# 4.0](https://github.com/fsharp/fslang-design/blob/master/FSharp-4.0)

* [F# Tooling RFCs](https://github.com/fsharp/fslang-design/blob/master/tooling)



### The Process:

1. Use [F# Language Suggestions](https://github.com/fsharp/fslang-suggestions) to submit ideas, vote on them and discuss them.

2. Ideas which get "approved in principle" get an [RFC entry](https://github.com/fsharp/fslang-design/tree/master/RFCs) based on the [template](https://github.com/fsharp/fslang-design/blob/master/RFC_template.md), and a corresponding [RFC discussion thread](https://github.com/fsharp/fslang-design/issues)

   There is currently a backlog of approved ideas. If an idea has been approved and you'd
   like to accelerate the creation of an RFC,  send a PR creating the RFC document for any approved-in-principle issue.
   First in first served.  To "grab the token" send a PR doing nothing but creating or naming the RFC file, and
   then fill in the further details with additional commits to the PR.

3. Implementations and testing are usually submitted to the [visualfsharp](https://github.com/Microsoft/visualfsharp) repository and then integrated to [fsharp](https://github.com/fsharp/fsharp) and  [FSharp.Compiler.Service](https://github.com/fsharp/FSharp.Compiler.Service)

All in-progress RFCs, listed in the [RFC folder](https://github.com/fsharp/fslang-design/blob/master/RFCs), are part of a future version of F#.

When RFCs are implemented and a version of F# is revved, the RFCs which correspond to the F# version they were implemented in are archived under the appropriate folder.

### Language Update Release Trains


1. Delivery of language features is via RFCs plus implementations submitted to https://github.com/Microsoft/visualfsharp.

2. We use incremental delivery of language features.  

3. What this means procedurally is that when we have 1-3 100% “ready, tested and completed” FSharp.Core and F# Language features that aren't "too minor" then we will bump the language version in the actively developed release train branch (e.g. `dev15.8`) and start to merge the features to both master and that release branch.

3. Practically speaking, this means the features are "on the train" to ship in preview releases of various tooling about ~1-2 months after integration, and non-preview releases about 2-3 months later (depending on everything).

4. The feature set will get integrated and shipped in Visual Studio, FCS packages, Mono and .NET SDK compilers in about the same timeframe.  (Other editors can pick up an updated FCS pretty quickly)

5. We may delay some features until a "major version release" (which may or may not corresponds to a new version of Visual Studio)

### Roadmap and Areas of Priority Work

Any or all of the [approved-in-principle](https://github.com/fsharp/fslang-suggestions/labels/approved-in-principle) items are eligible to catch a release train.  That is as good as it gets for a "roadmap" for the language design.

The BDFL (@dsyme) has put together a list of "proposed priority" approved language design items which he plans to focus on or which other people have taken past RFC stage.  This is _not_ a roadmap, because other people will choose to prioritize other approved items (e.g. match! - which is not a priority item for me), and is also subject to change and edit.. It is an informal list.  You can find that list here: https://github.com/fsharp/fslang-suggestions/labels/proposed-priority 


