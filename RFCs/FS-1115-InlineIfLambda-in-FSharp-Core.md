# F# RFC FS-1115 - Tracking the use of InlineIfLambda in FSharp.Core

F# 6 added the InlineIfLambda feature.

This is a long-running pre-approved RFC tracking the use of this feature in FSharp.Core.

### Approved uses in FSharp.Core 6.0.0

* Array.map
* Array.iter
* Array.minBy
* Array.compareWith
* Resumable code and tasks

> NOTE: this list appears adhoc and we should be systematic, hence this RFC

### Approved uses in current preview

* lock

# Drawbacks
[drawbacks]: #drawbacks

* There are no known drawbacks of using this with the above functions.

# Alternatives
[alternatives]: #alternatives

* Don't use it

# Unresolved questions
[unresolved]: #unresolved-questions

* Other uses
