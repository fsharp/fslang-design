# F# RFC FS-1083 - Don't require a spaces around `>` or `<`

The design suggestion [Don't require a space before SRTP type parameter](https://github.com/fsharp/fslang-suggestions/issues/668) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

* [x] [Approved in principle](https://github.com/fsharp/fslang-suggestions/issues/668#issuecomment-443213791)
* [x] Discussion thread: absent, was resolved as part of [PR #1151](https://github.com/fsharp/fslang-suggestions/issues/1151)
* [x] Implementation: [completed](https://github.com/fsharp/fslang-suggestions/issues/1151)

## Summary

Currently, when declaring a type/signature that uses SRTP, you need to add a space between the `<` and `^`:

```fsharp
type C< ^T> = class end

let f< ^T> (x:^T) = ()
```

Failing to do so will result in an unexpected error message, "Unexpected infix operator in pattern":

!["Unexpected infix operator in pattern" error message](https://user-images.githubusercontent.com/32428601/39529555-43ea60ee-4e27-11e8-8f94-7f1a386c4f3d.png)

Not only this unexpected, but it is also inconsistent with "regular" generics, where the `'` doesn't need a space preceding it:

```fsharp
type C<'T> = class end

let f<'T> (x:'T) = ()
```

This also affects specifying the type of a generic function or member, like so:

```fsharp
val c<'T>: int // Error, thinks `:` is part of an operator

// Workaround - add a space
val c<'T> : int
```

Issue tracked here: https://github.com/dotnet/fsharp/issues/6650

## Detailed Design

This is arguably just a bug in the parser, which currently assumes that `<^` is an operator in all contexts, as if it were a user-defined operator:

```fsharp
let inline ( <^ ) x y = x + y
```

This is not the correct behavior. To address this, the parsing of type argument declarations should be adjusted to account for this case. It currently does assume that `'ident` (`| QUOTE ident` is the parser rule) is a type argument. Similar assumptions have to be ironed out.

## Drawbacks

None from a user's standpoint.

## Alternatives

The main alternative is "don't do this" and continue to require the extra space. However, this is somewhat of a parlor trick to memorize, which we want to avoid from a language design standpoiht.

## Compatibility

This is not a breaking change.

## Unresolved questions

None
