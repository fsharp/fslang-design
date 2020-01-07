
The design suggestion [](https://github.com/dotnet/fsharp/issues/5496) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [ ] Approved in principle
* [ ] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/825)
* [ ] Implementation: [In progress]https://github.com/dotnet/fsharp/pull/8042)


# Summary
[summary]: Allow --langversion:preview to disable compiler warnings from APIs using the ExperimentalAttribute().

# Motivation
[motivation]: Shipping preview versions of the F# language early is very difficult, because there isn't a delivery mechanism that is seperate from Visual Studio or the DotNet Cli.
C# has the same issue and has chosen to ship preview C# lang features in the RTM builds, concealed by the --langversion switch.  F# also has a --langversion switch, however, many F# compiler features rely on new APIs added to FSharp.Core.
e.g. nameof and the 3D and 4D slicing enhancments APIs.

This feature will allow us to ship preview library features in the FSharp.Core dll, in such a way that developers will have a warning if they try to use them from a compiler without the preview switch specified, but will not have to worry about warnings when preview is specified.

# Detailed design
[design]: 
In order to not change the handling of any existing ExperimentalAttributes, we look to see if the message contains the text: "--langversion:preview"
If that text is found then no warning is produced.


Notes:
Operates in fsi, and fsc and ide

Here is an example decorated API:
````
[<Experimental("Preview library feature, requires '--langversion:preview'")>]
val inline GetArraySlice3DFixedSingle3 : source:'T[,,] ->  start1:int option -> finish1:int option -> start2:int option -> finish2:int option -> index3: int -> 'T[,]
````
Here is an example of the warning in fsi:
```
> let source =[|3;4;5;6;7;8;9|]
- let start1 = None
- let finish1 = None
- let index2=0
- let index3=0
- open OperatorIntrinsics
- GetArraySlice3DFixedDouble3(source,start1, finish1, index2,index3);;

  GetArraySlice3DFixedDouble3(source,start1, finish1, index2,index3);;
  ^^^^^^^^^^^^^^^^^^^^^^^^^^^

stdin(7,1): warning FS0057: Preview library feature, requires '--langversion:preview'. This warning can be disabled using '--nowarn:57' or '#nowarn "57"'.
````

And fsc:
````
t.fs(7,1): warning FS0057: Preview library feature, requires '--langversion:preview'. This warning can be disabled using '--nowarn:57' or '#nowarn "57"'.````
````