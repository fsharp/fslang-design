
## F# 4.0 Speclet: Make “Microsoft” prefix optional when using core FSharp.Core namespaces, types and modules

[User Voice](https://fslang.uservoice.com/forums/245727-f-language/suggestions/6107641-make-microsoft-prefix-optional-when-using-core-f), 
[Pull Request](https://visualfsharp.codeplex.com/SourceControl/network/forks/dsyme/cleanup/contribution/7456), 
[Commit](https://github.com/Microsoft/visualfsharp/commit/51d7b62c820e8212d470bb35bd74b6d4bbb37fa6)

### Background

The modules and types from the FSharp.Core dll use the names "Microsoft.FSharp.Collections" etc.

Since the F# language is independent, cross-platform and open-source, it is appropriate that source 
code be able to optionally use just "FSharp.Collections". This would also mean the names match the name 
of the DLL (FSharp.Core). 

This is an appropriate change for a cross-platform, open source language with multiple tooling vendors. It is, 
for example, relevant to educators writing textbooks on F# intended for a neutral audience. Just as C# coding 
uses the neutral "System" for its core types, so F# coding uses "FSharp".


### Examples

With this change

    open Microsoft.FSharp.Quotations

can be changed to 

    open FSharp.Quotations


### Design

The feature works as follows:

1. implicitly add an auto-open of the "Microsoft" whenever FSharp.Core is referenced. Concretely 
    this means that the "FSharp" entity ref (from Microsoft.FSharp in FSharp.Core) will be added 
    to the environment tables, and thus the contents of further namespaces will be accessible.  

2. The contents may also be accessed via the "global" keyword, e.g. ``global.FSharp.Quotations.Expr.Let``.  

If user assemblies define conflicting types in namespaces such as FSharp.Quotations, those assemblies will 
be searched in preference.


### Binary Compatibility 

The change doesn't affect binary compatibility and doesn't change FSharp.Core.dll. "Microsoft" namespaces are 
still used in FSharp.Core and can be seen in stack traces etc.  The change is deliberately only intended 
to allow a surface neutrality. For example, the compiler still expects the critical 
core types (like lists etc.) to be in "Microsoft.FSharp*" namespaces in the binary format of FSharp.Core.dll.

This would not be a breaking change, existing code would continue to compile. The compiled form used 
in FSharp.Core.dll wouldn't change, for reasons of binary compatibility.

