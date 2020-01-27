# F# RFC FS-1043 - Extension members should be available to solve operator trait constraints

These design suggestions:
* https://github.com/fsharp/fslang-suggestions/issues/230
* https://github.com/fsharp/fslang-suggestions/issues/29

have been marked "approved in principle". This RFC covers the detailed proposal for these

* [x] Approved in principle
* [ ] Implementation: [In progress](https://github.com/dotnet/fsharp/pull/6805)


# Summary
[summary]: #summary

Extension methods are previously ignored by SRTP constraint resolution.  This RFC means they are taken into account.

For example, consider
```fsharp

type System.String with
    static member ( * ) (foo, n: int) = String.replicate n foo

let r4 = "r" * 4
let spaces n = " " * n
```
Prior to this RFC the result is:
```
foo.fs(2,21): warning FS1215: Extension members cannot provide operator overloads.  Consider defining the operator as part of the type definition instead.
foo.fs(4,16): error FS0001: The type 'int' does not match the type 'string'
```

# Motivation
[motivation]: #motivation

It is reasonable to use extension methods to retrofit operators and other semantics on to existing types. This "completes the picture" as extension methods in a natural way.


# Detailed design
[design]: #detailed-design

The proposed change is as follows, in the internal logic of the constraint solving process:

1. During constraint solving, the record of each SRTP constraint incorporates the relevant extension methods in-scope at the point the SRTP constraint is asserted. That is, at the point a generic construct is used and "freshened".  The accessibility domain (i.e. the information indicating accessible methods) is also noted as part of the constraint.  Both of these pieces of information are propagated as part of the constraint. We call these the *trait possible extension solutions* and the *trait accessor domain*

2. When checking whether one unsolved SRTP constraint A *implies* another B (note: this a process used to avoid asserting duplicate constraints when propagating a constraint from one type parameter to another - see `implies` in `ConstraintSolver.fs`), both the possible extension solutions and the accessor domain of A are ignored, and those of the existing asserted constraint are preferred.

3. When checking whether one unsolved SRTP constraint is *consistent* with another (note: this is a process used to check for inconsistency errors amongst a set of constraints - see `consistent` in `ConstraintSolver.fs`), the possible extension solutions and accessor domain are ignored.

4. When attempting to solve the constraint via overload resolution, the possible extension solutions which are accessible from the trait accessor domain are taken into account.  

5. Built-in constraint solutions for things like `op_Addition` constraints are applied if and when the relevant types match precisely, and are applied even if some extension methods of that name are available.


# Drawbacks
[drawbacks]: #drawbacks

* This slightly strengthens the "type-class"-like capabilities of SRTP resolution. This means that people may increasingly use SRTP code as a way to write generic, reusable code rather than passing parameters explicitly.  While this is reasonable for generic arithmetic code, it has many downsides when applied to other things.

# Alternatives
[alternatives]: #alternatives

1. Don't do it


# Compatibility
[compatibility]: #compatibility

Status: We are trying to determine when/if this RFC is a breaking change.

We assume it must be a breaking change, because additional methods are taken into account in the overload resolution used in SRTP constraint resolution. That must surely cause it to fail where it would have succeeded before. However,

1. All the new methods are extension methods, which are lower priority in overload resolution

Even if it's theoretically a breaking change, we may still decide it's worthwhile because the risk of change is low.  This seems plausible because

1. Taking the extra existing extension methods into account is natural and a lot like an addition to the .NET libraries causing overload resolution to fail. We don't really consider that a breaking change (partly because this is measured differently for C# and F#, as they have different sensitivities to adding overloads).

2. For the built-in operators like `(+)`, there will be relatively few such candidate extension methods in F# code because we give warnings when users try to add extension methods for these

3. Nearly all SRTP constraints (at least the ones for built-in operators) are on static members, and C# code can't introduce extension members that are static - just instance ones. So C# extension members will only cause compat concern for F# code using SRTP constraints on instance members, AND where the C# extension methods make a difference to overload resolution.

Still, we're pretty sure this must be a breaking change. We would appreciate help construct test cases where it is/isn't.


# Unresolved questions
[unresolved]: #unresolved-questions

* [ ] Points 2 & 3 (`consistent` and `implies`) are subtle and I will attempt to expand the test cases where constraints flow together from different accessibility
domains to try to identify a case where this matters. However it's actually very hard and artificial to construct tests where this matters, because SRTP constraints are typically freshened
and solved within quite small scopes where the available methods and accessibility domain is always consistent.

