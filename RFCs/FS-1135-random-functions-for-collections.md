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
 - Shared Random instance will be used for all basic functions. In .NET 6 and higher [Random.Shared](https://learn.microsoft.com/en-us/dotnet/api/system.random.shared) will be used, otherwise Fsharp.Core defined shared instance.

### Shuffle

Shuffle function should returned a new shuffled collection of the same collection type.

Two functions should be added to each module.

```fsharp
val shuffle: CollectionType<'T> -> CollectionType<'T>
val shuffleRand: Random -> CollectionType<'T> -> CollectionType<'T>
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
val choice: CollectionType<'T> -> 'T
val choiceRand: Random -> CollectionType<'T> -> 'T
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
val choices: int -> CollectionType<'T> -> CollectionType<'T>
val choicesRand: Random -> int -> CollectionType<'T> -> CollectionType<'T>
```
[ArgumentNullException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentnullexception) should be raised if collection is null

[ArgumentOutOfRangeException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentoutofrangeexception) should be raised if N is negative

Example:
```fsharp
let allPlayers = [ "Alice"; "Bob"; "Charlie"; "Dave" ]
let round1Order = allPlayers |> List.choices 3 // ["Bob", "Dave", "Bob"]
```

### Sample

Sample should select N elements from input collection in random order, once element is taken it won't be selected again. N can't be greater than collection length

Two functions should be added to each module.

```fsharp
val sample: int -> CollectionType<'T> -> CollectionType<'T>
val sampleRand: Random -> int -> CollectionType<'T> -> CollectionType<'T>
```
[ArgumentNullException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentnullexception) should be raised if collection is null

[ArgumentOutOfRangeException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentoutofrangeexception) should be raised if N is greater than collection length or negative

Example:
```fsharp
let allPlayers = [ "Alice"; "Bob"; "Charlie"; "Dave" ]
let round1Order = allPlayers |> List.sample 3 // ["Charlie", "Dave", "Alice"]
```

# Drawbacks

If implemented before this appears in Linq, the naming can be different

# Alternatives

Use online snippets, or provide a nuget package.

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change? **No, unless user defined those extensions himself**
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