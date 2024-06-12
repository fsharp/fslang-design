# F# RFC FS-1101 - Asynchronous sequences

The design suggestion of asynchronous sequences is "approved in principle". This RFC covers the detailed proposal for this suggestion.

- [x] Approved in principle
- [ ] Suggestion
- [ ] RFC Discussion
- [ ] Implementation

Links

* A prototype is at https://github.com/dotnet/fsharp/blob/feature/tasks/tests/fsharp/perf/tasks/TaskPerf/taskSeq.fs

* An F# implementation based on `Async<_>` is here: https://fsprojects.github.io/FSharp.Control.AsyncSeq
 
# Summary

We add a `taskSeq { .. }` builder to the F# standard library for asynchronous sequences implementing `IAsyncEnumerable`.

TBD: The name may be changed to `asyncSeq { ... }`.

TBD: There may be a corresponding module `AsyncSeq`, see https://fsprojects.github.io/FSharp.Control.AsyncSeq/.

TBD: This might go in a separate DLL, not FSharp.Core.dll

TBD: This might be done purely in a community package, possible by iterating on https://fsprojects.github.io/FSharp.Control.AsyncSeq/.

# Motivation

TBD

# Detailed design

Planned support:

* The standard computation expression features for imperative code (`Delay`, `Run`, `While`, `For`, `TryWith`, `TryFinally`, `Yield`, `YieldFrom`)

* `let!` and `return!` on task values

* `let!` and `return!` on async values

* `let!` and `return!` "task-like" values (supporting `task.GetAwaiter`, `awaiter.IsCompleted` and `awaiter.GetResult` members)

* `for` on `seq<'T>`

* `for` on `IAsyncEnumerable<'T>`

* `use` on both `IDisposable` and `IAsyncDisposable` resources

To be discussed:

* An `TaskSeq` or `AsyncSeq` module

### Binding to Task-like values

Binding to task-like values like `YieldAwaitable` is via SRTP constraints characterising the pattern:

```fsharp
  member Bind...
     when  ^TaskLike: (member GetAwaiter:  unit ->  ^Awaiter)
     and ^Awaiter :> ICriticalNotifyCompletion
     and ^Awaiter: (member get_IsCompleted:  unit -> bool)
     and ^Awaiter: (member GetResult:  unit ->  ^TResult1) 
```

e.g.

Binding to (task-like) `YieldAwaitable` values:
```fsharp
    task {
        let! x = Task.Yield()
        return 1 + x
    }
```

### IAsyncDisposable

TBD

### Tailcalls

TBD

### Background tasks and synchronization context

TBD

## Library additions 

TBD

# Performance

TBD

# Limitations

TBD

# Drawbacks

TBD

Larger FSharp.Core

# Alternatives

1. Don't do it, keep using TaskBuilder.fs

2. Build it all into the compiler. Ugh

3. Put it in a separate DLL 

# Compatibility

This is a backward compatible addition.

# Unresolved questions

* [ ] Naming - `taskSeq { .. }` or `asyncSeq { ... }`?  `taskSeq` may imply hot start for example.  

* [ ] Do we have an `AsyncSeq` or `TaskSeq` module?


