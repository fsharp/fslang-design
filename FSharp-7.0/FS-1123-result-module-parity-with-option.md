# F# RFC FS-1123 - Result module parity with the Option module

The design suggestion [Result module parity with Options](https://github.com/fsharp/fslang-suggestions/issues/1123) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1123)
* [ ] Details: [under discussion](FILL-ME-IN)
* [ ] Implementation: [In progress](FILL-ME-IN)

# Summary
[summary]: #summary

Augment the Result module functions to be at parity with reference options:

* Add the `isOk` function
* Add the `isError` function
* Add the `defaultValue` function
* Add the `defaultWith` function
* Add the `count` function
* Add the `fold` function
* Add the `foldBack` function
* Add the `exists` function
* Add the `forall` function
* Add the `contains` function
* Add the `iter` function
* Add the `toArray` function
* Add the `toList` function
* Add the `toSeq` function
* Add the `toOption` function
* Add the `toValueOption` function

# Motivation
[motivation]: #motivation

Today, Result has a more limited utility due to having significantly less module-bound functions than reference type options. 

# Detailed design
[design]: #detailed-design

The `Result` type is changed as follows:

Interface file:

```fsharp
    [<CompiledName("IsOk")>]
    val inline isOk: result: Result<'T, 'Error> -> bool

    [<CompiledName("IsError")>]
    val inline isError: result: Result<'T, 'Error> -> bool

    [<CompiledName("DefaultValue")>]
    val defaultValue: value: 'T -> result: Result<'T, 'Error> -> 'T

    [<CompiledName("DefaultWith")>]
    val defaultWith: defThunk: ('Error -> 'T) -> result: Result<'T, 'Error> -> 'T

    [<CompiledName("Count")>]
    val count: result: Result<'T, 'Error> -> int

    [<CompiledName("Fold")>]
    val fold<'T, 'Error, 'State> : folder: ('State -> 'T -> 'State) -> state: 'State -> result: Result<'T, 'Error> -> 'State

    [<CompiledName("FoldBack")>]
    val foldBack<'T, 'Error, 'State> : folder: ('T -> 'State -> 'State) -> result: Result<'T, 'Error> -> state: 'State -> 'State

    [<CompiledName("Exists")>]
    val exists: predicate: ('T -> bool) -> result: Result<'T, 'Error> -> bool

    [<CompiledName("ForAll")>]
    val forall: predicate: ('T -> bool) -> result: Result<'T, 'Error> -> bool

    [<CompiledName("Contains")>]
    val inline contains: value: 'T -> result: Result<'T, 'Error> -> bool when 'T: equality

    [<CompiledName("Iterate")>]
    val iter: action: ('T -> unit) -> result: Result<'T, 'Error> -> unit

    [<CompiledName("ToArray")>]
    val toArray: result: Result<'T, 'Error> -> 'T[]

    [<CompiledName("ToList")>]
    val toList: result: Result<'T, 'Error> -> List<'T>

    [<CompiledName("ToOption")>]
    val toOption: result: Result<'T, 'Error> -> Option<'T>

    [<CompiledName("ToValueOption")>]
    val toValueOption: result: Result<'T, 'Error> -> ValueOption<'T>
```

# Drawbacks
[drawbacks]: #drawbacks

Bigger code-base to maintain.

# Alternatives
[alternatives]: #alternatives

Not add anything

# Compatibility
[compatibility]: #compatibility

* Is this a breaking change?

No.

* What happens when previous versions of the F# compiler encounter this design addition as source code?

The same as if an older F# compiler encounters the `Result` type. This is primarily just additional functions.

* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?

The same behavior as before, since this is binary-compatible.

* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?

Since this is binary-compatible, no change other than seeing new functions occurs.

# Unresolved questions
[unresolved]: #unresolved-questions

N/A
