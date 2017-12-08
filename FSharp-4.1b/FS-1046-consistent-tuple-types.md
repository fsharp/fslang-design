# F# RFC FS-1046 - Better consistency in converting to F# tuple types

During the F# 4.1 release cycle, a breaking change was made to how explicit uses of ``System.Tuple<...>`` in F# source code are
interpreted.  At the time this change was made (as a bug fix) the extent of its impact was not properly appreciated, resulting
in the need for a subsequent set of code adjustments and a subequent fix.

* [The PR that caused the change](https://github.com/Microsoft/visualfsharp/pull/3283)
* [The issue documenting the regressions](https://github.com/Microsoft/visualfsharp/pull/3729)
* [The PR that reduced the severity of breaking change](https://github.com/Microsoft/visualfsharp/pull/4034)
 
### The Change 

F# maintains a distinction between "F# tuple types" (the F# view of tuple types) and ".NET tuple types" (the compiled view of tuple types).


###### F# 2.0 to early F# 4.1  (VS2017 15.0-15.3)

From F# 2.0 to the initial versions of F# 4.1, the rules applied was:

> Occurrences of ``System.Tuple<t,t2>`` and other .NET tuple types are decompiled to F# tuple types if they occur in .NET metadata. Occurrences in F# source code were not decompiled. 
>
> F# tuple types do **not** support ``ItemN`` properties, the ``Rest`` property and ``new System.Tuple<...>(...)`` construction

###### F# 4.1 after the breaking change (VS2017 15.4-15.5)

This led to some inconsistencies where tuple types that the user thinks of as "equivalent" when they flow in to F#
via C# libraries  become non-equicalent when written explicitly in  F#. As a result a [change](https://github.com/Microsoft/visualfsharp/pull/3283) was made to give the rule

>  Occurrences of ``System.Tuple<t,t2>`` and other .NET tuple types are always decompiled to F# tuple types regardless of whether they occur in .NET metadata or F# source code.
>
> F# tuple types do **not** support ``ItemN`` properties, the ``Rest`` property and ``new System.Tuple<...>(...)`` construction

###### Final F# 4.1 rules (VS2017 15.6 onwards (? - TBD) )

As a result a [change](https://github.com/Microsoft/visualfsharp/pull/4034) the final rules are

>  Occurrences of ``System.Tuple<t,t2>`` and other .NET tuple types are always decompiled to F# tuple types regardless of whether they occur in .NET metadata or F# source code.
>
> F# tuple types **do** support ``ItemN`` properties, the ``Rest`` property and ``new System.Tuple<...>(...)`` construction

### The Regressions

This change caused regressions documented in https://github.com/Microsoft/visualfsharp/pull/3729

### The subsequent fix

A [subsequent fix](https://github.com/Microsoft/visualfsharp/pull/4034) has been proposed to have F# tuple types appear to support some of the operations supported by .NET tuple types
* For Tuples allow Items 1-7 + Rest and ``new System.Tuple<...>``
* Warn on explicit use of Item* and Rest
* Hide these Tuple members from intellisense


* [x] Approved in principle
# Drawbacks
[drawbacks]: #drawbacks

TBD

# Alternatives
[alternatives]: #alternatives

TBD

# Unresolved questions
[unresolved]: #unresolved-questions

TBD
