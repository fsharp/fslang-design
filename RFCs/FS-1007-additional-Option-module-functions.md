# F# RFC FS-1007 - Additional Option module functions

The design suggestion [Additional Option module functions](https://fslang.uservoice.com/forums/245727-f-language/suggestions/6672880-add-a-option-getordefault-method-as-a-curryable-al) has been marked "approved in principle".  
This RFC covers the detailed proposal for this suggestion.

[Discussion thread](https://github.com/fsharp/FSharpLangDesign/issues/60)

* [x] Approved in principle
* [ ] Details: [under discussion](https://github.com/fsharp/FSharpLangDesign/issues/60)
* [ ] Implementation: not yet submitted

### Introduction

Additional functions to `Option` module

Proposed functions:

#### Default if `None`

Gets the value associated with the option or the supplied default value

```fsharp
val getOrDefault : 'a -> 'a option -> 'a
```

#### Option.map overload

```fsharp
val map2: ('a -> 'b -> 'c) -> 'a option -> 'b option -> 'c option
```

```fsharp
val map3: ('a -> 'b -> 'c -> 'd) -> 'a option -> 'b option -> 'c option -> 'd option
```

#### Conversion to and from null

```fsharp
val ofNull : 'a -> 'a option
```

```fsharp
val toNull : 'a option -> 'a
```

#### Conversion to and from `System.Nullable`

```fsharp
val ofNullable : System.Nullable<'T> -> 'T option
```

```fsharp
val toNullable : 'T option -> System.Nullable<'T>
```

### Under discussion:

- `inline` modifier
- exact additions

### Naming 

The name of the functions has not been finalized.  Some of the suggestions are:

- `getOrDefault`
   * `getOrElse`

### Performance considerations

The standard range of performance considerations for F# library functions apply.

### Testing considerations

The standard range of testing  considerations for F# library functions apply.
