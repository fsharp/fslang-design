
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

The type should be added in `FSharp.Core` library

### Naming 

The name of the type and union cases has not been finalized. Some of the suggestions are

| Type name | Success case  | Failure case |
| --------- | ------------- | ------------ |
| Result    | Success       | Failure      |
| Result    | Success       | Error        |
| Result    | OK            | Error        |
| Result    | OK            | Err          |
| Result    | OK            | Bad          |

### Testing considerations

The standard range of testing considerations for F# library types apply.

### Open Questions

- namespace
- declare as struct
- compiled name
- require additional functions?
