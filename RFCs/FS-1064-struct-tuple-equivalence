# F# RFC FS-1064 - Allow access of ValueTuple Properties from Struct Tuples

The issue [ValueTuple Item1,Item2,.. fields and ITuple interface are not visible or accessible](https://github.com/Microsoft/visualfsharp/issues/5654) is a hole in the struct tuples features.
This RFC covers the detailed proposal for this suggestion.

* [x] [Bug report](https://github.com/Microsoft/visualfsharp/issues/5654)
* [ ] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/FILL-ME-IN)
* [ ] Implementation: N/A


# Summary
[summary]: #summary

Allow the usage of `Item1`/`Item2`/etc. from struct tuples and ValueTuple in F# source code. Mirror the way this is done with reference tuples in F#.

# Motivation
[motivation]: #motivation

Struct tuples are backed by the `ValueTuple` type, which does define `Item1`/`Item2`/etc as properties on it. However, it is a compile error to access these in F# code from an F# struct tuple.

You can access these fields from reference tuples today, which makes things very akward:

```fsharp
let x = (1, 2)
x.Item1 // Warning, but can be used

let f1 (x: System.Tuple<int, int>) = x.Item1 // Warning, but can be used

let y = struct(1, 2)
y.Item1 // error FS0039: The field, constructor or member 'Item1' is not defined.

let f2 (x: System.ValueTuple<int, int>) = x.Item1 // error FS0039: The field, constructor or member 'Item1' is not defined. 
```

# Detailed design
[design]: #detailed-design

The access patterns for struct tuples should mirror reference tuples:

* Allow access of `Item1`/`Item2`/`Rest` from struct tuples in F# source
* Allow access of these members from `System.ValueTuple` when defines in F# source code
* Warn on access of these members as we do for reference tuples

This code should compile with warnings:

```fsharp
let x = struct(1, 2)
x.Item1 // error FS0039: The field, constructor or member 'Item1' is not defined.

let f (x: System.ValueTuple<int, int>) = x.Item1 // error FS0039: The field, constructor or member 'Item1' is not defined. 
```
At each site, the warning should be:

`warning FS3220: This method or property is not normally used from F# code, use an explicit tuple pattern for deconstruction instead.`

# Drawbacks
[drawbacks]: #drawbacks

No real drawbacks, as this is arguably a bug fix.

# Alternatives
[alternatives]: #alternatives

No other alternatives aside from keeping the existing behavior.

# Compatibility
[compatibility]: #compatibility

* Is this a breaking change?

No.

* What happens when previous versions of the F# compiler encounter this design addition as source code?

No changes.

* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?

No changes.

* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?

N/A.

# Unresolved questions

N/A
