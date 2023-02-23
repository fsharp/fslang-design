# F# RFC FS-1135 - Random functions for collections (List, Array, Seq)

The design suggestion [Add shuffle, sample etc. methods for lists, arrays etc](https://github.com/fsharp/fslang-suggestions/issues/508) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/508)
- [x] Approved in principle
- [ ] [Implementation]() (no implementation yet)
- [ ] Design Review Meeting(s) with @dsyme and others invitees
- [Discussion](https://github.com/fsharp/fslang-design/discussions/731)

# Summary

This feature extends collections apis with functions for random sampling and shuffling built-in fsharp collections.

# Motivation

This feature is motivated by the following use cases:
 - Using F# for data science and machine learning (like building a neural network), where data shuffling plays important role
 - Building games, where random sampling is used for generating random levels, random decks, etc.
 - Building simulations, where random sampling is used for generating random input data

# Detailed design

### General

The following general rules are applied to all functions
 - New function should be implemented in List, Array, Seq modules
 - All functions should not mutate the input collection
 - All functions should have a variant with a [Random](https://learn.microsoft.com/en-us/dotnet/api/system.random) parameter
 - Shared thread-safe Random instance should be used for all basic functions.

### Shuffle

Shuffle function should returned a new shuffled collection of the same collection type.

Two functions should be added to each module.

```fsharp
// Array module
val shuffle: 'T[] -> 'T[]
val shuffleRand: Random -> 'T[] -> 'T[]
// List module
val shuffle: 'T list -> 'T list
val shuffleRand: Random -> 'T list -> 'T list
// Seq module
val shuffle: 'T seq -> 'T seq
val shuffleRand: Random -> 'T seq -> 'T seq
```
[ArgumentNullException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentnullexception) should be raised if collection is null

Example:
```fsharp
let allPlayers = [ "Alice"; "Bob"; "Charlie"; "Dave" ]
let round1Order = allPlayers |> List.shuffle // [ "Charlie"; "Dave"; "Alice"; "Bob" ]
```

### Choice

Choice function should returned a random element from the collection.

Two functions should be added to each module.

```fsharp
// Array module
val choice: 'T[] -> 'T
val choiceRand: Random -> 'T[] -> 'T
// List module
val choice: 'T list -> 'T
val choiceRand: Random -> 'T list -> 'T
// Seq module
val choice: 'T seq -> 'T
val choiceRand: Random -> 'T seq -> 'T
```
[ArgumentNullException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentnullexception) should be raised if collection is null

[InvalidOperationException](https://learn.microsoft.com/en-us/dotnet/api/system.invalidoperationexception) should be raised if collection is empty

Example:
```fsharp
let allPlayers = [ "Alice"; "Bob"; "Charlie"; "Dave" ]
let round1Order = allPlayers |> List.choice // "Charlie"
```

### Choices

Choices should select N elements from input collection in random order, once element is taken it can be selected again.

Two functions should be added to each module.

```fsharp
// Array module
val choices: int -> 'T[] -> 'T[]
val choicesRand: Random -> int -> 'T[] -> 'T[]
// List module
val choices: int -> 'T list -> 'T list
val choicesRand: Random -> int -> 'T list -> 'T list
// Seq module
val choices: int -> 'T seq -> 'T seq
val choicesRand: Random -> int -> 'T seq -> 'T seq
```
[ArgumentNullException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentnullexception) should be raised if collection is null

[ArgumentOutOfRangeException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentoutofrangeexception) should be raised if N is negative

[InvalidOperationException](https://learn.microsoft.com/en-us/dotnet/api/system.invalidoperationexception) should be raised if collection is empty

Example:
```fsharp
let allPlayers = [ "Alice"; "Bob"; "Charlie"; "Dave" ]
let round1Order = allPlayers |> List.choices 3 // ["Bob", "Dave", "Bob"]
```

### Sample

Sample should select N elements from input collection in random order, once element is taken it won't be selected again. N can't be greater than collection length

Two functions should be added to each module.

```fsharp
// Array module
val sample: int -> 'T[] -> 'T[]
val sampleRand: Random -> int -> 'T[] -> 'T[]
// List module
val sample: int -> 'T list -> 'T list
val sampleRand: Random -> int -> 'T list -> 'T list
// Seq module
val sample: int -> 'T seq -> 'T seq
val sampleRand: Random -> int -> 'T seq -> 'T seq
```
[ArgumentNullException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentnullexception) should be raised if collection is null

[ArgumentOutOfRangeException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentoutofrangeexception) should be raised if N is greater than collection length or negative

[InvalidOperationException](https://learn.microsoft.com/en-us/dotnet/api/system.invalidoperationexception) should be raised if collection is empty

Example:
```fsharp
let allPlayers = [ "Alice"; "Bob"; "Charlie"; "Dave" ]
let round1Order = allPlayers |> List.sample 3 // ["Charlie", "Dave", "Alice"]
```

# Drawbacks

System.Random interface has added some new methods in [.NET 8](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8#methods-for-working-with-randomness), where naming is a bit different. More new methods can eventually be added in future .NET versions.

# Alternatives

Use online snippets, or provide a nuget package.

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change? **No, unless user defined those extensions themselves**
* What happens when previous versions of the F# compiler encounter this design addition as source code? **Should be fine**
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries? **Should be fine**
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct? **Will work as usual**

# Pragmatics

## Diagnostics

Please list the reasonable expectations for diagnostics for misuse of this feature. **I don't see a way to misuse it**

## Tooling

Please list the reasonable expectations for tooling for this feature, including any of these:

* Debugging
    * Breakpoints/stepping
    * Expression evaluator
    * Data displays for locals and hover tips
* Auto-complete
* Tooltips
* Navigation and Go To Definition
* Colorization
* Brace/parenthesis matching

**Should work just like for other collections functions**

## Performance

Please list any notable concerns for impact on the performance of compilation and/or generated code

* For existing code **Existing code should not be affected**
* For the new features **Performance should be respected when implementing this feature, since it can be used in performance-sensitive scenaris**

## Scaling

Algorithmic complexity of the new features should be O(n) or less. 

## Culture-aware formatting/parsing

N/A

# Unresolved questions

It's unclear, if more sophisticated overloads should be added:
 - Weights parameter for choices function
 - Counts parameter for sample function

~~In .NET 6 and higher [Random.Shared](https://learn.microsoft.com/en-us/dotnet/api/system.random.shared) is available, but as soon as F# Core only targets standard, it can't use it. Is targeting higher .NET possible (not just for this feature, maybe some others need it)?
Same question about new [.NET 8 apis](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8#methods-for-working-with-randomness), they could be reused in theory~~