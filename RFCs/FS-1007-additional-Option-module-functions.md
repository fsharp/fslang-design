# F# RFC FS-1007 - Additional Option module functions

The design suggestion [Additional Option module functions](https://fslang.uservoice.com/forums/245727-f-language/suggestions/6672880-add-a-option-getordefault-method-as-a-curryable-al) has been marked "approved in principle".  
This RFC covers the detailed proposal for this suggestion.

[Discussion thread](https://github.com/fsharp/FSharpLangDesign/issues/60)

* [x] Approved in principle
* [x] Details: [under discussion](https://github.com/fsharp/FSharpLangDesign/issues/60)
* [x] Implementation: [Completed](https://github.com/Microsoft/visualfsharp/pull/1781)

### Introduction

Additional functions to `Option` module

### Approved functions

#### Functions that provide default values in case of `None`

The approved names and forms are:
```fsharp
val defaultValue :         'a         -> 'a option -> 'a
val defaultWith : (unit -> 'a)        -> 'a option -> 'a
val orElse :               'a option  -> 'a option -> 'a option
val orElseWith :  (unit -> 'a option) -> 'a option -> 'a option
```

#### `Option.map` overloads

```fsharp
val map2: ('a -> 'b -> 'c) -> 'a option -> 'b option -> 'c option
val map3: ('a -> 'b -> 'c -> 'd) -> 'a option -> 'b option -> 'c option -> 'd option
```

#### `Option.contains`

```fsharp
val contains: 'a -> 'a option -> bool
```

#### `Option.flatten`

```fsharp
val flatten: 'a option option -> 'a option
```

### Denied functions

#### `Option.apply`

```fsharp
val apply: 'a option -> ('a -> 'b) option -> 'b option
```

> We don't have any other functions in the F# core library working over
> containers of function values. Uses of this function are very rare and
> using it doesn't particularly make code more readable or even much more
> succinct. Any programmer who can use it correctly can write the one line
> helper.

### Functions not acted on

These functions may be brought up at a later time. Action was not taken on these function definitions:

#### Handling the result of `TryParse`-style functions

```fsharp
val ofTry : bool * 'a -> 'a option
```

Proposed names:
* `ofTry`
* `ofByRef`

Use case:

```fsharp
let parseInt x =
  match System.Int32.TryParse x with
  | false, _ -> None
  | true, v -> Some v
```
becomes
```fsharp
let parseInt x =
  System.Int32.TryParse x
  |> Option.ofByRef
```

#### Add `toSeq`

Provides a more complete relation to the `Array`–`List`–`Seq` set,
as `toArray` and `toList` already exist.

```fsharp
val toSeq : 'a option -> 'a seq
```

### Under discussion:

- `inline` modifier: Should functions in the `Option` module be marked as inline? If so, which ones would benefit the most?
- study adopting [`ExtCore.Option` module](https://github.com/jack-pappas/ExtCore/blob/5221f4e67a93cffdb85203f3ae403a6052bcfbc0/ExtCore/Pervasive.fs#L810)

### Performance considerations

The standard range of performance considerations for F# library functions apply.

### Testing considerations

The standard range of testing  considerations for F# library functions apply.
