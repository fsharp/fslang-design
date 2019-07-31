# F# RFC FS-1075 - Interop with C# nullable-typed optional parameters

The design suggestion [Improve interop to `Nullable` optional parameters](https://github.com/fsharp/fslang-suggestions/issues/774) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion and some related design relaxations.

* [x] Approved in principle
* [ ] [Discussion](TBD)
* [x] [Implementation](https://github.com/dotnet/fsharp/pull/7296)

# Summary
[summary]: #summary

Consider a C# definition 
```fsharp
        public int SomeMethod(int? channels = 0, int? ratio = 1)
```
We adjust F# to allow providing non-Nullable values at the callsite:
```fsharp
    C.SomeMethod(channels = 3)
    C.SomeMethod(channels = 3, ratio = 3)
    C.SomeMethod(ratio = 3)
```
in addition to the currently allowed provision of Nullable-typed values:
```fsharp
    C.SomeMethod(channels = Nullable 3)
    C.SomeMethod(channels = Nullable 3, ratio = Nullable 3)
    C.SomeMethod(ratio = Nullable 3)
```

# Detailed Description of Problem

The change involves

1. Removing the propagation of a "strong" known type into the checking of the argument expression (if the argument is a C#-style nullable optional argument).

2. Using a type-directed rule to allow either `Nullable<ty>` or `ty` arguments. 

This would mean that

```fsharp
let f x = C.SomeMethod(ratio = x)
```

would continue to typecheck as today (here `x` will have type `Nullable<int>`) until you add a type annotation to `x`, e.g.

```fsharp
let f (x: int) = C.SomeMethod(ratio = x)
```

### Code samples

TBD

# Drawbacks
[drawbacks]: #drawbacks

TBD

# Compatibility
[compatibility]: #compatibility

# Alternatives
[alternatives]: #alternatives

TBD

# Unresolved questions
[unresolved]: #unresolved-questions

* [ ] Should we apply this rule to **all** Nullable-typed arguments on methods, not just optional ones.








