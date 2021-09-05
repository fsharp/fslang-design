# F# RFC FS-1114 - Make long-established deprecation warning messages into errors

The design suggestion [Make long-established deprecation warning messages into errors](https://github.com/fsharp/fslang-suggestions/issues/1064) has been marked "approved in principle". This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1064)
- [x] Approved in principle
- [ ] Implementation
- [ ] Design Review Meeting(s) with @dsyme and others invitees
- [Discussion](https://github.com/fsharp/fslang-design/discussions/626)

# Summary

We give errors instead of warnings for some things that have long been giving deprecation warnings. In particular constructs specifically for "ML compatibility" now give errors (unless using `/langversion:5.0` or before).

# Motivation

These constructs have long been giving deprecation warnings in F#. This simplifies the F# language and reduces the amount of time we need to explain these historical oddities.

# Detailed design

#### Multiple generic parameters using postfix type name (long-deprecated)

Since F# 1.9 we have been emitting this warning when using OCaml-style syntax for multiple generic parameters with a postfix type name.
```
> let f (x: (int, int) Dictionary) = 1;;
  let f (x: (int, int) Dictionary) = 1;;
----------^^^^^^^^^^^^^^^stdin(1,11): warning FS0062: This construct is for ML compatibility. The syntax '(typ,...,typ) ident' is not used in F# code. Consider using 'ident<typ,...,typ>' instead. You can disable this warning by using '--mlcompatibility' or '--nowarn:62'.
```
This becomes an error (unless `--langversion:5.0` or earlier is used - the number depending on when we make this change)

#### `#indent "off"` (long-deprecated)

Since F# 1.9 we've been emitting this warning
```
> #indent "off";;

  #indent "off";;
  ^^^^^^^^^^^^^

stdin(1,1): warning FS0062: This construct is for ML compatibility. Consider using a file with extension '.ml' or '.mli' instead. You can disable this warning by using '--mlcompatibility' or '--nowarn:62'.
```
 
This becomes an error (unless `--langversion:5.0` or earlier is used - the number depending on when we make this change)


#### `x.(expr)` (long deprecated)

Since F# 1.9 we've been emitting this warning
```
C:\GitHub\dsyme\fsharp>dotnet fsi

Microsoft (R) F# Interactive version 12.0.0.0 for F# 5.0
Copyright (c) Microsoft Corporation. All Rights Reserved.

For help type #help;;

> let f x = x.(1);;

  let f x = x.(1);;
  ----------^^^^^

stdin(1,11): warning FS0062: This construct is for ML compatibility. In F# code you may use 'expr.[expr]'. A type annotation may be required to indicate the first expression is an array. You can disable this warning by using '--mlcompatibility' or '--nowarn:62'.
```


This becomes an error (unless `--langversion:5.0` or earlier is used - the number depending on when we make this change)

#### `module M = struct .. end`  (long deprecated)

Since F# 1.9 we've been emitting this warning using the OCaml syntax for modules  `module M = struct .. end` and `module M : sig ... end`  and `module M : .... ` in a signature file. These are never used in F#  and indeed `module M = begin .. end` is also rarely used since indentation is enough.
```
module M = struct 
    let x = 1
end

  module M = struct
  -----------^^^^^^

stdin(1,12): warning FS0062: This construct is for ML compatibility. The syntax 'module ... = struct .. end' is not used in F# code. Consider using 'module ... = begin .. end'. You can disable this warning by using '--mlcompatibility' or '--nowarn:62'.

```

This becomes an error (unless `--langversion:5.0` or earlier is used - the number depending on when we make this change)

#### Use of inputs `*.ml` and `*.mli` (long-deprecated)

I propose using inputs with suffix `.ml` and `.mli` become an error (unless `--langversion:NNN` or earlier is used - the number depending on when we make this change)


#### Use of `(*IF-CAML*)` or `(*IF-OCAML*)`

This  is an ancient F# feature and while not explicitly deprecated now emits a warning


#### Use of `land lor lxor lsl lsr asr`

These are infix keywords in F# because they were infix keywords in OCaml.  They are not defined in FSharp.Core and while they are user-definable infix operators they are never actually used as such in any F# code I know.

Using these keywords will now emit an ML compatibility warning (not error)

# Alternatives

Continue to allow these warts

# Compatibility

* Is this a breaking change? Yes, although the constructs have long given deprecation warnings, and using `/langversion:5.0` allows compatibility to be maintained.

# Unresolved questions

Click “Files changed” → “⋯” → “View file” for the rendered RFC.

