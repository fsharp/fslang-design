# F# RFC FS-1115 - Tracking the use of InlineIfLambda in FSharp.Core

F# 6 added the InlineIfLambda feature.

This is a long-running pre-approved RFC tracking the use of this feature in FSharp.Core.

> NOTE: It appears to make sense to just go through FSharp.Core systematically and use it on every inlined function that takes a lambda which is only applied once

> NOTE: It is likely `inline` could be used more widely across FSharp.Core as well

### Approved uses in FSharp.Core 6.0.0

* Array.map
* Array.iter
* Array.minBy
* Array.compareWith
* Resumable code and tasks

> NOTE: this list appears adhoc and we should be systematic, hence this RFC


### Approved uses in current preview

* lock
* Array.init
* Array.sumBy
* Array.averageBy
* List.iter
* List.iteri
* List.sumBy
* List.averageBy
* List.compareWith
* Seq.sumBy
* Seq.averageBy

# Drawbacks
[drawbacks]: #drawbacks

* There are no known drawbacks of using this with the above functions.

# Alternatives
[alternatives]: #alternatives

* Don't use it

# Unresolved questions
[unresolved]: #unresolved-questions

* Other uses

* See https://github.com/dotnet/fsharp/issues/6424
