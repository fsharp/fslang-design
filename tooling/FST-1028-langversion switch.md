# F# RFC FS-1028 - Add Language version switch to fsc.exe and fsi.exe

The design suggestion [FILL ME IN](https://github.com/fsharp/fslang-suggestions/issues/fill-me-in) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [ ] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/fill-me-in)
* [ ] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/FILL-ME-IN)
* [ ] Implementation: [In progress](https://github.com/Microsoft/visualfsharp/pull/FILL-ME-IN)


# Summary
[summary]: Add a new switch to fsi and fsc that allows a developer to specify the expected language version.  And to list the allowed language versions.

# Motivation
[motivation]: It's hard to add and preview new language features, because of the risk of breaking existing projects.  
This switch will allow developers to fix their projects to a specific set of language features.
Whilst allowing us to ship previews for developers to try out.

# Detailed design
[design]: #detailed-design
Switch to appear in the language section.
The compiler will not be updated to enable the selection of language versions prior to F# 4.7 and will produce an error,
I.e
````
artifacts\bin\fsc\Debug\net472\fsc --langversion:4.1

error FS0246: Unrecognized value '4.1' for --langversion use --langversion:? for complete list
````

Match the C# compiler --langversion switch.

Help text
````
                - LANGUAGE -
--langversion:{?|version|latest|preview} Display the allowed values for language version, specify language version such as latest, preview or 4.7
````
?      -  The compiler shall display a list of vaid options.
latest -  The compiler shall selected the latest RTM version of the language
default-  Synonym for latest.  Perhaps eventually default may not be the latest, but it seems unlikely.
preview-  Enable all of the preview features.
n.n    -  Specify a specific language version, E.g. 4.7

It is the responsibility of the new feature developer, to ensure that if a prior language version is selected, the later features 
will not be accessible, and display a message to the user indicating that the selected language version does not support the feature.

Example code:

Get list of valid options
```fsharp
artifacts\bin\fsc\Debug\net472\fsc --langversion:?
Supported language versions:
preview
default
latest
latestmajor
4.7 (Default)
```

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
````


# Drawbacks
[drawbacks]: #drawbacks

Why should we *not* do this?

# Alternatives
[alternatives]: #alternatives

What other designs have been considered? What is the impact of not doing this?

# Compatibility
[compatibility]: #compatibility

Please address all necessary compatibility questions:
* Is this a breaking change?
* What happens when previous versions of the F# compiler encounter this design addition as source code?
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?


# Unresolved questions
[unresolved]: #unresolved-questions

What parts of the design are still TBD?

