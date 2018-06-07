# F# RFC FS-1050 - Add TryGetValue on Map

This proposes to add a `TryGetValue` method on the `FSharp.Collections.Map` type.

* [x] Approved in principle
* [x] Implementation: [Ready](https://github.com/Microsoft/visualfsharp/pull/4827)

# Summary
[summary]: #summary

The `Map` type currently supports the `TryFind` member returning an `Option`. This adds the performance-friendly `TryGetValue` member to avoid any need for an allocation on return.

# Motivation
[motivation]: #motivation

To improve performance of lookup using standard .NET techniques.

# Detailed design
[design]: #detailed-design

```fsharp
/// <summary>Lookup an element in the map, assigning to <c>value</c> if the element is in the domain 
/// of the map and returning <c>false</c> if not.</summary>
/// <param name="key">The input key.</param>
/// <param name="value">A reference to the output value.</param>
/// <returns><c>true</c> if the value is present, <c>false</c> if not.</returns>
member TryGetValue: key:'Key * [<System.Runtime.InteropServices.Out>] value:byref<'Value> -> bool
```

Usage:

```fsharp
let mp = Map.ofList [("doot", 1); ("beef", 2); ("hoopty", 3)]

match mp.TryGetValue "doot" with
| (true, value) -> printfn "Value: %A" value
| (false, _) -> printfn "No value found!"
```

# Drawbacks
[drawbacks]: #drawbacks

There may be confusion for newcomers about which lookup technique to use.

# Alternatives
[alternatives]: #alternatives

Add a new method returning a struct option. But it is better to support the standard .NET method name and pattern.

# Compatibility
[compatibility]: #compatibility

Not a breaking change.

# Unresolved questions
[unresolved]: #unresolved-questions

None.
