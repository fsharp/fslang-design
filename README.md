# F# Language Design RFCs 

RFCs and docs related to the F# language design process. 

* [Open F# Language RFCs (including candidates for F# 4.2)](https://github.com/fsharp/fslang-design/blob/master/RFCs)

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

