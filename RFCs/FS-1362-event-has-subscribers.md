# F# RFC FS-1362 - `HasSubscribers` on F# event objects

The design suggestion [Expose GetInvocationList from F# event](https://github.com/fsharp/fslang-suggestions/issues/938) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/938)
- [x] [Approved in principle](https://github.com/fsharp/fslang-suggestions/issues/938#issuecomment-931782202)
- [ ] Implementation (not started)
- [x] [Discussion](https://github.com/fsharp/fslang-design/pull/867)

# Summary

FSharp.Core adds a `HasSubscribers` property to `Event<'T>`, `Event<'Delegate, 'Args>` and `DelegateEvent<'Delegate>`.
The code that owns an event object can then skip costly work when no handler listens.

The suggestion asked for `GetInvocationList`. The discussion chose `HasSubscribers` instead, because a list shows internal state ([comment](https://github.com/fsharp/fslang-suggestions/issues/938#issuecomment-932441633)).

# Motivation

In C#, the class that declares a field-like event can compare the event with `null`. It then computes costly event arguments only when a handler exists.
An F# class that uses `Event` cannot do this, because the handler list is a private field. The suggestion reads that field through reflection:

```fsharp
let multicast =
    typeof<Event<EventHandler, EventArgs>>.GetField("multicast", BindingFlags.NonPublic ||| BindingFlags.Instance)
```

This code depends on a private field name of FSharp.Core.

# Detailed design

FSharp.Core adds one member to `DelegateEvent<'Delegate>`, `Event<'Delegate, 'Args>` and `Event<'T>`:

```fsharp
/// <summary>Returns <c>true</c> when the event has at least one handler.</summary>
member HasSubscribers: bool
```

1. `HasSubscribers` MUST return `true` when the event has at least one handler. Otherwise it MUST return `false`. A handler is a delegate that `AddHandler` adds or an observer that `Subscribe` adds. The handler counts until `RemoveHandler` or `Dispose` removes it.
2. The property MUST NOT allocate, take a lock or change state.
3. The result is a snapshot. Another thread can add or remove a handler right after the read. If the property returns `false`, a handler that another thread adds at that time misses the event. If it returns `true`, `Trigger` can find no handler. The C# test `Changed != null` has the same limit.
4. `IEvent`, `IDelegateEvent` and the `Event` module do not change. Only the code that holds the event object can read the property. A subscriber that holds only the value of `Publish` cannot.

The property is an ordinary instance property of a class. Quotations and SRTP member constraints handle it like any other property.

```fsharp
type Document() =
    let changed = Event<Summary>()

    [<CLIEvent>]
    member _.Changed = changed.Publish

    member _.Apply(edit: Edit) =
        // ... apply the edit ...
        if changed.HasSubscribers then
            changed.Trigger(Summary.compute edit) // costly: runs only when a handler listens
```

# Changes to the F# spec

None. The change is in FSharp.Core only.

# Drawbacks

FSharp.Core gets three more public members.

# Alternatives

- `GetInvocationList()`, as the suggestion first asked. It shows internal state, and it allocates an array on each call.
- A method `HasSubscribers()`. A property is the .NET convention for a cheap read without side effects.
- A new member on `IEvent` or `IDelegateEvent`. It breaks all existing implementations of these interfaces. It also shows the handler state to every subscriber.
- A copy of `Event` with the extra member in user code. Each project must keep its own copy.
- No change. Users continue to read the private field through reflection.

# Prior art

- C#: inside the class that declares it, a field-like event is a delegate field, and `Changed != null` tests for handlers.
- Rx.NET: [`Subject<T>.HasObservers`](https://reactiveui.net/api/System.Reactive/System/Reactive/Subjects/Subject{T}/HasObservers/) reports whether any observer subscribes to the subject.
- Java: [`SubmissionPublisher.hasSubscribers()`](https://docs.oracle.com/en/java/javase/21/docs/api/java.base/java/util/concurrent/SubmissionPublisher.html) has the same purpose.

# Compatibility

* The change is binary compatible. It adds members to FSharp.Core classes and needs no language version.
* An earlier compiler compiles source that uses the property when the project references the new FSharp.Core.
* A binary that uses the property fails with `MissingMethodException` on an earlier FSharp.Core, as for every FSharp.Core addition.
* A project can already define an extension property `HasSubscribers` on these types. Compiled code keeps calling its extension. After recompilation, the call binds to the new property, because intrinsic members win over extension members.
* A project can already define an extension method `HasSubscribers()` on these types. From language version 9.0, the method keeps winning ([FS-1137](https://github.com/fsharp/fslang-design/blob/main/preview/FS-1137-prefer-extensions-method-over-property-when-arguments-provided.md)). With language version 8.0, the call `e.HasSubscribers()` fails with error FS0003.

# Interop

C# sees a `bool` property `HasSubscribers` on the compiled event classes `FSharpEvent` and `FSharpDelegateEvent`. No planned C# feature interacts with it.

# Pragmatics

## Diagnostics

None.

## Tooling

Completion and Quick Info show the property with its XML documentation. The debugger can evaluate it safely.

## Performance

The property reads one field. Existing code does not change.

## Scaling

Not applicable.

## Culture-aware formatting/parsing

Not applicable.

# Unresolved questions

None.
