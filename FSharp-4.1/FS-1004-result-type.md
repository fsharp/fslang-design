
# F# RFC FS-1004 - Result type

The design suggestion [Result type](https://fslang.uservoice.com/forums/245727-f-language/suggestions/9484395-discriminated-union-type-in-order-to-be-able-to-wr) has been implemented in F# 4.1.
This RFC covers the detailed proposal for this suggestion.

[Discussion thread](https://github.com/fsharp/FSharpLangDesign/issues/49)

* [x] Approved in principle
* [x] Details: [under discussion](https://github.com/fsharp/FSharpLangDesign/issues/49)
* [x] Implementation: [Completed](https://github.com/Microsoft/visualfsharp/pull/964)

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


| Type name | Success case  | Failure case |
| --------- | ------------- | ------------ |
| Result    | Ok            | Error        |


### Testing considerations

The standard range of testing considerations for F# library types apply.

### Proposal for additional functions

Uncontentious:

```fsharp
bind : ('T -> Result<'U, 'TError>) -> Result<'T, 'TError> -> Result<'U, 'TError>
map : ('T -> 'U) -> Result<'T, 'TError> -> Result<'U, 'TError>
mapError : ('TError -> 'U) -> Result<'T, 'TError> -> Result<'T, 'U>
```

A result builder should be a separate RFC which covers adding builders for both `Option` and `Result`.

Suggested:

```fsharp
attempt : (unit -> 'a) -> Result<'a,exn>
```



