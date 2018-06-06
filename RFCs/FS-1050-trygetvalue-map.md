# F# RFC FS-1050 - Add TryGetValue on Map

This proposes to add a `TryGetValue` method on the `FSharp.Collections.Map` type and `tryGetValue` to the corresponding `Map` module.

* [x] Approved in principle
* [x] Implementation: [Ready](https://github.com/Microsoft/visualfsharp/pull/4827)


# Summary
[summary]: #summary

The `Map` type currently support `TryFind` returning an `option`.  This adds the performance-friendly `TryGetValue` to avoid any need for an allocation on return.


# Motivation
[motivation]: #motivation

To improve performance of lookup using standard .NET techniques

# Detailed design
[design]: #detailed-design

See the implementation, which is straightforward

# Drawbacks
[drawbacks]: #drawbacks

There may be confusion  for newcomers about which lookup technique to use.

# Alternatives
[alternatives]: #alternatives

* Add a new method returning a struct option.  But it is better to support the standard .NET method name and pattern.

# Compatibility
[compatibility]: #compatibility

Not abreaking change

# Unresolved questions
[unresolved]: #unresolved-questions

None

