# F# RFC FS-1135 - Random functions for collections (List, Array, Seq)

The design suggestion [Add shuffle, sample etc. methods for lists, arrays etc.](https://github.com/fsharp/fslang-suggestions/issues/508) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/508)
- [x] Approved in principle
- [ ] [Implementation]() (no implementation yet)
- [ ] Design Review Meeting(s) with @dsyme and others invitees

[Discussion](https://github.com/fsharp/fslang-design/discussions/731)

# Summary

This feature extends the collection apis with functions for random sampling and shuffling built-in fsharp collections.

# Motivation

This feature is motivated by the following use cases:
 - Using F# for data science and machine learning (like building a neural network), where data shuffling plays important role
 - Building games, where random sampling is used for generating random levels, random decks, etc.
 - Building simulations, where random sampling is used for generating random input data

# Detailed design

### General

The following general rules are applied to all functions
 - New functions should be implemented in `List`, `Array`, `Seq` modules
 - Each function should have a variant that takes a [Random](https://learn.microsoft.com/en-us/dotnet/api/system.random) argument
 - Custom shared thread-safe `Random` instance should be used for function without `Random` argument (since `Random.Shared` is only available since .NET 6)

### Shuffle

The shuffle functions return a new collection of the same collection type and of the same size, with each item in a randomly mixed position. The chance to end up in any position is weighted evenly on the length of the collection.

The following functions will be added to each module.

```fsharp
// Array module
val randomShuffle: array:'T[] -> 'T[]
val randomShuffleWith: random:Random -> array:'T[] -> 'T[]
val randomShuffleInPlace: array:'T[] -> 'T[]
val randomShuffleInPlaceWith: random:Random -> array:'T[] -> 'T[]
// List module
val randomShuffle: list:'T list -> 'T list
val randomShuffleWith: random:Random -> list:'T list -> 'T list
// Seq module
val randomShuffle: source:'T seq -> 'T seq
val randomShuffleWith: random:Random -> source:'T seq -> 'T seq
```
[ArgumentNullException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentnullexception) is raised if collection is `null`, or if the `random` argument is `null`.

Example:
```fsharp
let allPlayers = [ "Alice"; "Bob"; "Charlie"; "Dave" ]
let round1Order = allPlayers |> List.randomShuffle // [ "Charlie"; "Dave"; "Alice"; "Bob" ]
```

### Choice

The choice functions return a single random element from the given collection. The random choice is weighted evenly on the size of the collection.

The following functions will be added to each module.

```fsharp
// Array module
val randomChoice: array:'T[] -> 'T
val randomChoiceWith: random:Random -> array:'T[] -> 'T
// List module
val randomChoice: list:'T list -> 'T
val randomChoiceWith: random:Random -> list:'T list -> 'T
// Seq module
val randomChoice: source:'T seq -> 'T
val randomChoiceWith: random:Random -> source:'T seq -> 'T
```
[ArgumentNullException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentnullexception) is raised if collection is `null`, or if the `random` argument is `null`.

[ArgumentException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentexception) is raised if collection is empty.

Example:
```fsharp
let allPlayers = [ "Alice"; "Bob"; "Charlie"; "Dave" ]
let round1Order = allPlayers |> List.randomChoice // "Charlie"
```

### Choices

Choices should select N elements from input collection in random order, once element is taken it can be selected again.

The following functions will be added to each module.

```fsharp
// Array module
val randomChoices: count:int -> array:'T[] -> 'T[]
val randomChoicesWith: random:Random -> count:int -> array:'T[] -> 'T[]
// List module
val randomChoices: count:int -> list:'T list -> 'T list
val randomChoicesWith: random:Random -> count:int -> list:'T list -> 'T list
// Seq module
val randomChoices: count:int -> source:'T seq -> 'T seq
val randomChoicesWith: random:Random -> count:int -> source:'T seq -> 'T seq
```
[ArgumentNullException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentnullexception) is raised if collection is `null`, or if the `random` argument is `null`.

[ArgumentException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentoutofrangeexception) is raised if N is negative.

[ArgumentException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentexception) is raised if collection is empty.

Example:
```fsharp
let allPlayers = [ "Alice"; "Bob"; "Charlie"; "Dave" ]
let round1Order = allPlayers |> List.randomChoices 3 // ["Bob", "Dave", "Bob"]
```

### Sample

Sample should select N elements from input collection in random order, once element is taken it won't be selected again. N can't be greater than collection length

The following functions will be added to each module.

```fsharp
// Array module
val randomSample: count:int -> array:'T[] -> 'T[]
val randomSampleWith: random:Random -> count:int -> array:'T[] -> 'T[]
// List module
val randomSample: count:int -> list:'T list -> 'T list
val randomSampleWith: random:Random -> count:int -> list:'T list -> 'T list
// Seq module
val randomSample: count:int -> source:'T seq -> 'T seq
val randomSampleWith: random:Random -> count:int -> source:'T seq -> 'T seq
```
[ArgumentNullException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentnullexception) is raised if collection is `null`, or if the `random` argument is `null`.

[ArgumentException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentoutofrangeexception) is raised if N is greater than collection length or is negative.

[ArgumentException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentexception) is raised if collection is empty.

Example:
```fsharp
let allPlayers = [ "Alice"; "Bob"; "Charlie"; "Dave" ]
let round1Order = allPlayers |> List.randomSample 3 // ["Charlie", "Dave", "Alice"]
```

# Drawbacks

Users may be tempted to use some of the recently added method of `System.Random` that also apply to collections instead of the ones we add in FSharp.Core. It may also be confusing to some, especially since the naming over there is slightly different. See [.NET 8 What's New](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8/runtime#methods-for-working-with-randomness).

# Alternatives

Not doing this.

# Compatibility

* Is this a breaking change? **No**
* What happens when previous versions of the F# compiler encounter this design addition as source code? **Library function, not applicable**
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries? **Library function, not applicable**
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

**These will work just like for other collections functions**

## Performance

* For existing code **Existing code won't be affected**
* For the new features **Performance should be respected when implementing this feature, since it can be used in performance-sensitive scenarios**

## Scaling

Algorithmic complexity of the new features should be O(n) for list and seq functions. O(1) for most array functions (except shuffle).

## Culture-aware formatting/parsing

N/A

# Unresolved questions

N/A
