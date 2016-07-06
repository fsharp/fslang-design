
# F# RFC FS-1001 - String Interpolation

There is an approved-in-principle [proposal](http://fslang.uservoice.com/forums/245727-f-language/suggestions/6002107-steal-nice-println-syntax-from-swift) to extend the existing printf functionality in the F# language design with [string interpolation][2]. To discuss this design please us [design discussion thread][7].

  * [ ] Discussion: [under discussion](https://github.com/fsharp/FSharpLangDesign/issues/6)
  * [ ] Implementation: [Proof of concept submitted](https://github.com/Microsoft/visualfsharp/pull/921)

### Summary

Proposed syntax:

```fsharp
"%(**embedded expression**)"
```

### Links

* [C# string interpolation docs](https://msdn.microsoft.com/en-us/library/dn961160.aspx)
* [Swift string interpolation](https://developer.apple.com/library/ios/documentation/Swift/Conceptual/Swift_Programming_Language/StringsAndCharacters.html)

### Motivation

TBD

### Detailed Design


The linked prototype accepts arbitrary F# expression as **embedded expression**. 
In prototype source string literal is split into chunks containing text and embedded expressions. Then chunks are joined using [String.Concat][4].

Initial string literal:

```fsharp
"%d%(foo)%d%(bar.bar)"
```

After splitting:

```fsharp
Text("%d"); Expression(foo); Text(%d); Expression(bar.bar)
```

Final result

```fsharp
String.Concat([| "%d"; box foo; "%d"; box (bar.baz) |])
```

### Drawbacks

TBD

### Alternatives

TBD

### Open questions:

* Is general idea of implementing this feature entirely on semantic level is acceptable?
* Should embedded expressions be restricted to just identifiers\dotted names or we should allow full set of F# expressions?
* Under the hood [String.Concat][4] uses [ToString][5] to obtain string representation of the object (which is equivalent to **"%O"** format specifier in printf). 
This option is definitely not the best one for F# types like records\discriminated unions that are printed far more nicely with **"%A"**. However for primitive types always using **"%A"** seems to be an overkill.
Should we always prefer one way of printing things (and if yes - which one) or printing strategy should vary from type to type.
* Should we provide ways to specify width\precision\alignment similar to what [printf][6] is doing today? If yes - what modification should be made to the proposed syntax?
 

### Detailed Changes to Language Specification

TBD

[2]:http://en.wikipedia.org/wiki/String_interpolation
[4]:http://msdn.microsoft.com/en-us/library/system.string.concat(v=vs.110).aspx
[5]:http://msdn.microsoft.com/en-us/library/system.object.tostring(v=vs.110).aspx
[6]:http://msdn.microsoft.com/en-us/library/ee370560.aspx
[7]:https://github.com/fsharp/FSharpLangDesign/issues/6
