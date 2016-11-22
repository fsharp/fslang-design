# F# RFC FS-1007 - Additional Option module functions

The design suggestion [Additional Option module functions](https://fslang.uservoice.com/forums/245727-f-language/suggestions/6672880-add-a-option-getordefault-method-as-a-curryable-al) has been marked "approved in principle".  
This RFC covers the detailed proposal for this suggestion.

[Discussion thread](https://github.com/fsharp/FSharpLangDesign/issues/60)

* [x] Approved in principle
* [ ] Details: [under discussion](https://github.com/fsharp/FSharpLangDesign/issues/60)
* [ ] Implementation: [submitted](https://github.com/Microsoft/visualfsharp/pull/1781)

### Introduction

Additional functions to `Option` module

### Proposed functions

#### Functions that provide default values in case of `None`

Currently three slates of names have been proposed:

```fsharp
val defaultIfNone :              'a         -> 'a option -> 'a
val defaultIfNoneFrom : (unit -> 'a)        -> 'a option -> 'a
val orElse :                     'a option  -> 'a option -> 'a option
val orElseFrom :        (unit -> 'a option) -> 'a option -> 'a option
```

```fsharp
val getOrDefault :             'a         -> 'a option -> 'a
val getOrDefaultFun : (unit -> 'a)        -> 'a option -> 'a
val orElse :                   'a option  -> 'a option -> 'a option
val orElseFun :       (unit -> 'a option) -> 'a option -> 'a option
```

```fsharp
val withDefault :          'a         -> 'a option -> 'a
val defaultFrom : (unit -> 'a)        -> 'a option -> 'a
val orElse :               'a option  -> 'a option -> 'a option
val orElseWith:   (unit -> 'a option) -> 'a option -> 'a option
```

#### `Option.map` overloads

```fsharp
val map2: ('a -> 'b -> 'c) -> 'a option -> 'b option -> 'c option
val map3: ('a -> 'b -> 'c -> 'd) -> 'a option -> 'b option -> 'c option -> 'd option
```

#### `Option.apply`

```fsharp
val apply: 'a option -> ('a -> 'b) option -> 'b option
```

#### `Option.contains`

```fsharp
val contains: 'a -> 'a option -> bool
```

#### `Option.flatten`

```fsharp
val flatten: 'a option option -> 'a option
```

### Under discussion:

- `inline` modifier: Should functions in the `Option` module be marked as inline? If so, which ones would benefit the most?
- Naming of new functions
- study adopting [`ExtCore.Option` module](https://github.com/jack-pappas/ExtCore/blob/5221f4e67a93cffdb85203f3ae403a6052bcfbc0/ExtCore/Pervasive.fs#L810)

### Performance considerations

The standard range of performance considerations for F# library functions apply.

### Testing considerations

The standard range of testing  considerations for F# library functions apply.
