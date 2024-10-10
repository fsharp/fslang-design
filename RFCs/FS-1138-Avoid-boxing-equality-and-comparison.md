# F# RFC FS-1138 - Avoid boxing on equality and comparison, default to equivalence relation mode equality

The design suggestion [Avoid boxing on equality and comparison](https://github.com/fsharp/fslang-suggestions/issues/1280) has been marked "approved in principle". (In some indeterminate future)

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1280)
- [ ] Approved in principle
- [ ] Implementation (Not started)
- [ ] Design Review Meeting(s) with @dsyme and others invitees
- Discussion (Not started)

# Summary

F#'s existing equality and comparison operators will be hidden behind a `PartialEquivalenceRelationComparison` module, with new equality and comparison operators implementing `IEquatable<'T>` and `IComparable<'T>` lookup by default, and supporting `nan = nan` instead of `nan <> nan`. This offers performance benefits and eliminates the historical design flaw that is `nan <> nan`.

# Motivation

Currently, F#'s equality and comparison operators [cause boxing on equality and comparison for value types](https://github.com/dotnet/fsharp/issues/526). However, an attempt to fix this through the use of `IEquatable<T>` and `IComparable<T>` was [rejected](https://github.com/dotnet/fsharp/pull/9404) citing the breakage for `nan` equality.

F#'s reluctance of breaking the behaviour regarding `nan` equality to optimise for performance has done more harm than good.

1. We are slower than expected in basic equality and comparison operations for value types compared to C#, especially relevant for generic contexts where static type information is unavailable for optimisation. For some people concerned about performance and looking at language benchmarks between C# and F#, this is a turnoff for introducing them to F#. Moreover, we are also slowing down most F# programs unnecessarily.
2. We preserve what is arguably a historical design flaw: `=` and `.Equals` not matching each other in behaviour. The reason for `nan <> nan` as [claimed by a member of the original IEEE754 design committee](https://stackoverflow.com/a/1573715/5429648) was an engineering compromise between the unavailability of an `isnan()` predicate and the need to test for `nan` before widespread adoption of IEEE754. This resulted in `x <> x` being a quick test for `nan`. However, F# should have good defaults and upholding that for all `x`: `x = x`, is a good default, despite how other programming languages preserve this flaw.

Some people may attempt to justify `nan <> nan` and not `nan = nan` by claiming that an undefined state is not equal to an undefined state, as in `sqrt(-3.) <> sqrt(-2.)` when applied to real numbers. But, the alternative is `sqrt(-3.) = sqrt(-2.)` which can also be said as equally undefined. Since `0./0.`, `infinity/infinity`, `0.*infinity`, `infinity-infinity`, and `sqrt(-2.)` all result in `nan` and no comparison makes total sense, we should instead
1. aim to preserve the equivalence relation `x = x` which would make building generic containers much simpler, and
2. aim to run equality and comparisons quicker for most usages.

Any instance of `=` deviating from `.Equals` that is not about the behaviour surrounding `nan` values, and any instance of `IEquatable<'T>.Equals`, `.Equals` and `IComparable<'T>.Compare = 0` resulting in different interpretations, is a poor design that we should not support anyway.

# Detailed design

The old equality and comparison operators will be hidden in a new module, called `PartialEquivalenceRelationComparison` (similar to `NonStructuralComparison`) for backwards compatibility in case anyone needs them. The new equality and comparison operators will make use of `IEquatable<'T>` and `IComparable<'T>` to speed up and preserve the equivalence relation. The implementation should:
- first optimise for existing known types in the original implementation (changing the implementation for `float` and `float32` to consider `nan = nan`, also making `[|-1y|] < [|1y|]` return `true` [due to covariance weirdness](https://github.com/dotnet/fsharp/pull/9404#issuecomment-642914149))
- then check `IStrucutralEquatable` for equality and `IStructuralComparable` for comparison (preserving the original implementation),
- then check `IEquatable<'T>` and `IComparable<'T>`
- finally use `obj.Equals` for equality or `IComparable` for comparison (preserving the original implementation).

For changing the behaviour for `nan` values, the optimiser in the compiler itself will likely also need to be changed.

Just to be safe, we will add new warnings for all local `x` bindings that use the default equality operators: `x = x` (Expression expected to be true. Was this a typo?) and `x <> x` (Expression expected to be false. If intended to be a `nan` test, please write `x = nan` instead, or open the `PartialEquivalenceRelationComparison` module.)

# Drawbacks

This is a breaking change after all. Someone somewhere may be broken by this change. But unlike low-level languages, `x <> x` is not even idiomatic in F# and it is actively hindering the development of generic containers and the road to optimisation.

# Alternatives

What other designs have been considered? What is the impact of not doing this?

- An alternative is naming the back compat module `BackCompat` but it will conflate itself with other possible back compat operators to be introduced in the future. But if this module gets large, opening this module might change too many behaviours across files. The module should be self-contained and have a descriptive name.
- Not doing this would fall into the 2 drawbacks as listed in the Motivation section.

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change? Yes.
* What happens when previous versions of the F# compiler encounter this design addition as source code? Old behaviour.
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries? New behaviour.
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct? Old behaviour for using the operators on the float(32) values directly, new behaviour otherwise.

# Pragmatics

## Diagnostics

The two warnings mentioned above.

## Tooling

Not applicable.

## Performance

Performance should only improve.

## Scaling

Not applicable.

## Culture-aware formatting/parsing

Not applicable.

# Unresolved questions

Not applicable.
