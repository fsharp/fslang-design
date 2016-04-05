
# F# RFC FS-1004 - Result type

The design suggestion [Result type](https://fslang.uservoice.com/forums/245727-f-language/suggestions/9484395-discriminated-union-type-in-order-to-be-able-to-wr) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

[Discussion thread](https://github.com/fsharp/FSharpLangDesign/issues/49)

* [x] Approved in principle
* [ ] Details: [under discussion](https://github.com/fsharp/FSharpLangDesign/issues/49)
* [ ] Implementation: [In progress](https://github.com/Microsoft/visualfsharp/pull/964)

### Introduction

In order to be able to write code that you can easily consume that does not throw an exception 
it's useful to have a fsharp type who contains the result on success or the error on failure

```fsharp
type Result<'TSuccess,'TError> = 
     | Success of 'TSuccess 
     | Error of 'TError
```

The type should be added in `FSharp.Core` library in the ``FSharp.Core`` namespace.

The compiled name of the type should be ``FSharpResult`2`` for consistency with other FSharp.Core types.

### Unresolved Questions

- declare as struct (waiting for support for union types to be structs)


### Resolved Questions

* No additional functions or methods will be supported in FSharp.Core.   These may be added in a community library or user code.
