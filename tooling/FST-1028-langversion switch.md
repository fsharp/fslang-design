
The design suggestion [Introduce language version compiler flag and MSBuild property](https://github.com/dotnet/fsharp/issues/5496) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [ ] [Suggestion](https://github.com/dotnet/fsharp/issues/5496)
* [ ] Implementation: [In progress]https://github.com/fsharp/fslang-design/pull/360)


# Summary
[summary]: Add a new switch to `fsi` and `fsc` that allows a developer to specify the expected language version.  And to list the allowed language versions.


# Motivation
[motivation]: It's difficult to add and preview new language features, because of the risk of breaking existing projects.  
This switch allows developers to fix their projects to a specific set of language features, or opt into preview language features.

The switch is not about allowing breaking changes in the language. It's purpose is to allow users of the tooling to access a specific version of the language.
We expect source code that compiled and ran using the F# X version of the language to continue to successfully compile and run with F# X+1 or F# X+2 or F# X+99 of the language.
Some times we will fix bugs in the implementation of the tooling that will cause the above to not be achieved. Changes due to bug fixes should not be impacted by this switch. On the other hand, often those bugs where they had a usable outcome, and lasted for more than 1 version of the language, become an intrinsic part of the language and never get fixed.

The switch has no impact on FSharp.Core referencing.  However, some language features are partially implemented in FSharp.Core.dll.  The FSharp Compiler, requires that when it executes it is executing with the FSharp.Core that it was compiled with.  The source being compiled can reference whatever version of FSharp Core that is selected by the developer.

# Detailed design
[design]: #detailed-design

Notes:

* Switch to appear in the language section of the output set of compiler options.

* The compiler will not be updated to enable the selection of language versions prior to F# 4.7 and will produce an error, i.e.

```
artifacts\bin\fsc\Debug\net472\fsc --langversion:4.1
error FS0246: Unrecognized value '4.1' for --langversion use --langversion:? for complete list
```
Matches the C# compiler --langversion switch.

Help text
```
                - LANGUAGE -
--langversion:{?|version|latest|preview} Display the allowed values for language version, specify language version such as latest, preview or 4.7
?      -  The compiler shall display a list of vaid options.
latest -  The compiler shall selected the latest RTM version of the language
default-  Synonym for latest.  Perhaps eventually default may not be the latest, but it seems unlikely.
preview-  Enable all of the preview features.
n.n    -  Specify a specific language version, E.g. 4.7
```

It is the responsibility of the new feature developer, to ensure that if a prior language version is selected, the later features 
will not be accessible, and display a message to the user indicating that the selected language version does not support the feature.

Get list of valid options:
````
artifacts\bin\fsc\Debug\net472\fsc --langversion:?
Supported language versions:
preview
default
latest
latestmajor
4.7 (Default)
````

Specify that the compiler should use preview features
````
artifacts\bin\fsc\Debug\net472\fsc --langversion:preview
Microsoft (R) F# Compiler version 10.5.0.0 for F# 4.7
Copyright (c) Microsoft Corporation. All Rights Reserved.
````

Specify that the compiler should use language version 4.7
````
artifacts\bin\fsc\Debug\net472\fsc --langversion:4.7
Microsoft (R) F# Compiler version 10.5.0.0 for F# 4.7
Copyright (c) Microsoft Corporation. All Rights Reserved.
`
