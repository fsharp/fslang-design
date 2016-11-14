# F# Language Design RFCs 

RFCs and docs related to the F# language design process. The Process:

1. Use [F# Language Suggestions](https://github.com/fsharp/fslang-suggestions) to submit ideas, vote on them and discuss them.

2. Ideas which get "approved in principle" get an [RFC entry](https://github.com/fsharp/fslang-design/tree/master/RFCs) based on the [template](https://github.com/fsharp/fslang-design/blob/master/RFC_template.md), and a corresponding [RFC discussion thread](https://github.com/fsharp/fslang-design/issues)

   There is currently a backlog of approved ideas. If an idea has been approved and you'd
   like to accelerate the creation of an RFC,  send a PR creating the RFC document for any approved-in-principle issue.
   First in first served.  To "grab the token" send a PR doing nothing but creating or naming the RFC file, and
   then fill in the further details with additional commits to the PR.

3. Implementations and testing are usually submitted to the [visualfsharp](https://github.com/Microsoft/visualfsharp) repository and then integrated to [fsharp](https://github.com/fsharp/fsharp) and  [FSharp.Compiler.Service](https://github.com/fsharp/FSharp.Compiler.Service)

All in-progress RFCs, listed in the [RFC folder](https://github.com/fsharp/fslang-design/blob/master/RFCs), are part of a future version of F#.

When RFCs are implemented and a version of F# is revved, the RFCs which correspond to the F# version they were implemented in are archived under the appropriate folder.

For RFCs that were implemented in F# 4.0, see the [F# 4.0 RFCs](https://github.com/fsharp/fslang-design/blob/master/FSharp-4.0)

For RFCs that were implemented in F# 4.1, see the [F# 4.1 RFCs](https://github.com/fsharp/fslang-design/blob/master/FSharp-4.1


## Open RFCs

* [F# RFC FS-1001 - String interpolation](https://github.com/fsharp/fslang-design/blob/master/RFCs/FS-1001-StringInterpolation.md)
* [F# RFC FS-1003 - Add nameof Operator](https://github.com/fsharp/fslang-design/blob/master/RFCs/FS-1003-nameof-operator.md)
* [F# RFC FS-1007 - Additional Option module functions](https://github.com/fsharp/fslang-design/blob/master/RFCs/FS-1007-additional-Option-module-functions.md)
* [F# RFC FS-1011 - Warn when recursive function is not tail-recursive](https://github.com/fsharp/fslang-design/blob/master/RFCs/FS-1011-warn-on-recursive-without-tail-call.md)
* [F# RFC FS-1018 - Adjust extension method scope](https://github.com/fsharp/fslang-design/blob/master/RFCs/FS-1018-adjust-extensions-method-scope.md)
* [F# RFC FS-1022 - Override  ToString  for discriminated unions and records](https://github.com/fsharp/fslang-design/blob/master/RFCs/FS-1022-override-ToString-for-discriminated-unions-and-records.md)
* [F# RFC FS-1023 - Allow type providers to generate types from types](https://github.com/fsharp/fslang-design/blob/master/RFCs/FS-1023-type-providers-generate-types-from-types.md)
* [F# RFC FS-1024 - Simplify call syntax for statically resolved member constraints](https://github.com/fsharp/fslang-design/blob/master/RFCs/FS-1024-simplify-call-syntax-for-statically-resolved-member-constraints.md)
* [F# RFC FS-1025 - Improve record type inference](https://github.com/fsharp/fslang-design/blob/master/RFCs/FS-1025-improve-record-type-inference.md)
* [F# RFC FS-1026 - Allow params dictionaries as method arguments](https://github.com/fsharp/fslang-design/blob/master/RFCs/FS-1026-allow-params-dictionaries-as-method-arguments.md)


