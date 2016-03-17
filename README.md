F# Language Design RFCs 
=================

RFCs and docs related to the F# langauge design process.

Open RFCs

* [F# RFC FS-1001 String Interpolation](https://github.com/fsharp/FSharpLangDesign/blob/master/RFCs/FS-1001-StringInterpolation.md)
* [F# RFC FS-1003 - nameof Operator](https://github.com/fsharp/FSharpLangDesign/blob/master/RFCs/FS-1003-nameof-operator.md)
* [F# RFC FS-1004 - Result type](https://github.com/fsharp/FSharpLangDesign/blob/master/RFCs/FS-1004-result-type.md)
* [F# RFC FS-0005 - Underscore Literals](https://github.com/fsharp/FSharpLangDesign/blob/master/RFCs/FS-1005-underscores-in-numeric-literals.md)
* [F# RFC FS-0006 - Struct Tuples and Interop with C# 7.0 Tuples](https://github.com/fsharp/FSharpLangDesign/blob/master/RFCs/FS-1006-struct-tuples.md)
* [F# RFC FS-1007 - Additional Option module functions](https://github.com/fsharp/FSharpLangDesign/blob/master/RFCs/FS-1007-additional-Option-module-functions.md)
* [F# RFC FS-0008 - Struct Records](https://github.com/fsharp/FSharpLangDesign/blob/master/RFCs/FS-1008-struct-records.md)
* [F# RFC FS-1009 - Allow mutually referential types and modules over larger scopes](https://github.com/fsharp/FSharpLangDesign/blob/master/RFCs/FS-1009-mutually-referential-types-and-modules-single-scope.md)
* [F# RFC FS-1010 - Add Map.count](https://github.com/fsharp/FSharpLangDesign/blob/master/RFCs/FS-1010-add-map-count.md)

Completed RFCs

* [RFC FS-1002 Cartesian product function for collections](https://github.com/fsharp/FSharpLangDesign/blob/master/RFCs/FS-1002-cartesian-product-for-collections.md)

Process:

1. Use [F# Language User Voice](http://fslang.uservoice.com) to submit ideas, vote on them and discuss them.

2. Ideas which get "approved in principle" get an [RFC entry](https://github.com/fsharp/FSharpLangDesign/tree/master/RFCs) based on the [template](https://github.com/fsharp/FSharpLangDesign/blob/master/RFC_template.md), and a corresponding [RFC discussion thread](https://github.com/fsharp/FSharpLangDesign/issues)

   There is currently a backlog of approved ideas. If an idea has been approved and you'd
   like to accelerate the creation of an RFC,  send a PR creating the RFC document for any approved-in-principle issue.
   First in first served.  To "grab the token" send a PR doing nothing but creating or naming the RFC file, and
   then fill in the further details with additional commits to the PR.

3. Implementations and testing are usually submitted to the [visualfsharp](https://github.com/Microsoft/visualfsharp/pulls) repository

