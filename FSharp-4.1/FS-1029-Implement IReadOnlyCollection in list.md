# F# RFC FS-1029 - (Implement IReadOnlyCollection<'T> in list<'T>)

The design suggestion [Implement IReadOnlyCollection<'T> in list<'T>](https://github.com/fsharp/fslang-suggestions/issues/181) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/181)
* [x] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/158)
* [x] Implementation: [Completed](https://github.com/Microsoft/visualfsharp/pull/2093)


# Summary
[summary]: #summary

.Net 4.5 has a new type `IReadOnlyCollection<'T>` and `list<'T>` (a.k.a. `FSharpList<'T>`) fits this interface precisely.

# Motivation

[motivation]: #motivation

It is often useful to get the Count/Length of a list relatively quickly without resorting to reflection or `Seq.length`.

# Detailed design
[design]: #detailed-design

Implement `IReadOnlyCollection<'T>` by making `Count` return the `Length` property that is already on `list<'T>`. I.e.

```
interface IReadOnlyCollection<'T> with
    member l.Count = l.Length
```

# Drawbacks
[drawbacks]: #drawbacks

The Count property would not be `O(1)`, which could be surprising. However, Length is a property of F# lists that is already not O(1).
This caused Json.NET to fail to be able to deserialize F# lists as documented [here](https://github.com/Microsoft/visualfsharp/issues/2257). It was resolved by Json.NET [here](https://github.com/JamesNK/Newtonsoft.Json/pull/1181).

# Alternatives
[alternatives]: #alternatives

Implement a length tracker on all lists or just don't do it.

# Unresolved questions
[unresolved]: #unresolved-questions

What parts of the design are still TBD?

