# F# RFC FS-1027 - Complete Optional and DefaultParameterValue Implementation

The issue report [`[<DefaultParameterValue>]` is ignored in F#-authored code](https://github.com/Microsoft/visualfsharp/issues/96)
has been addressed in [this Pull Request](https://github.com/Microsoft/visualfsharp/pull/1812).

Since it is a rather large change to F#'s behavior, this RFC covers the detailed design and implementation choices that were made.

* [x] Approved in principle
* [x] [Issue report](https://github.com/Microsoft/visualfsharp/issues/96)
* [x] Implementation: [Complete](https://github.com/Microsoft/visualfsharp/pull/1812)

# Summary
[summary]: #summary

The attributes `[<Optional;DefaultParameterValue<(...)>]` are typically used
in F# for C# and VB interop, so that C#/VB callers see an argument as optional.
The intention is that this is equivalent to defining the argument as optional in C# as in `MyMethod(int i = 3)`.

The F# compiler did not compile `[<DefaultParameterValue(...)>]` correctly, leading
to C#/VB to not use the specified default value in F#.

In addition, while the F# compiler can consume correctly compiled optional,
default arguments (as e.g. produced by csc today), it could not consume F# optional, default arguments
specified in F# in the same assembly.

This RFC details how both issues were addressed.

# Motivation
[motivation]: #motivation

Given the abundance of C#/VB code that is floating around, this way of specifying optional,
default parameters is likely to be the default interop mechanism, despite that there is a
separate F# mechanism using the `option<'T>` type.

# Detailed design
[design]: #detailed-design

Some technical context first: C#-style optional, default parameters are technically
specified on the caller side. That is, when the compiler resolves a method call with
an omitted optional argument, it reads the default value for that argument from the method
definition (this is encoded in the IL in a special-purpose way), and then passes that value explicitly.
In other words it's syntactic sugar for reading the value in `DefaultParameterValue`
and passing that as the argument.

(Contrast this approach with F# style optional arguments - which are callee side.
If an argument is omitted at the call site, the compiler simply passes `None` for that
argument, and the callee can decide which value to actually use as a default.)

The issues addressed by this RFC are two-fold.
1. The F# compiler was not producing the correct IL for default parameter values.
This meant that e.g. the C# compiler was not able to pass the default values specified
in F# code for any omitted arguments.
2. The F# compiler was able to consume optional, default arguments (provided they are
correctly compiled, i.e. not by fsc) from other assemblies, but not when caller and callee
were defined in the same assembly.

## Produce correct IL for default parameter values

Optional arguments and default values for arguments have special constructions
on the IL level - `[opt]` for optionality and a sort of argument "initializer" to
specify default values. The upshot is that compilers need to compile `OptionalAttribute`
and `DefaultParameterValue` not as normal argument attributes, but to these special IL
constructs.

The F# compiler was already doing that correctly for `Optional` and with
this RFC also generates the right IL for `DefaultParameterValue`. In addition, the
`DefaultParameterValueAttribute` itself is no longer present in fsc-generated IL, as it is
(and was) superfluous. This is similar to other attributes that are compiled to
specific IL constructs, e.g. `OutAttribute` and `OptionalAttribute`.

## Callers can consume optional, default arguments on callees in same assembly

Since optional, default parameters are "caller-side" as explained above, the code path
for reading the default value from IL vs reading it from a method in the same compilation
unit is different (the first does IL parsing, the second F# parsing, compilation etc).

As a result, the IL reading code could correctly consume optional, default parameters but
the F# compiling code could not. The F# compiler has been enhanced so that the two
are treated identically. In other words, you can now define a method argument in F# attributed
with `[<Optional;DefaultParameterValue(...)>] arg` and you'll be able to omit it
at call sites, whether calling an F# method in the same assembly or calling a .NET
method in another assembly.

## Allowable default values

This section details the values that are allowed as argument to the `DefaultParameterValue`
attribute.

For primitive types, i.e. (s)byte, (u)in16, (u)int32, (u)int64, float32, float and string, all constant
values are allowed.

For reference types, the only allowed default value is `null`.

For value types, the only allowed default value is the default value of the struct.

## Gotchas, warnings and errors

The value given as argument to `DefaultParameterValue` must match the type of the
parameter, i.e. the following is not allowed:

```fsharp
type Class() =
  static member Wrong([<Optional;DefaultParameterValue("string")>] i:int) = ()
```
We decided to let the compiler generate a warning in this case, and ignore both
attributes altogether, i.e. the method above is compiled effectively as if it was
written:

```fsharp
type Class() =
  static member Wrong(i:int) = ()
```
Note that the default value `null` needs to be type-annotated, as otherwise the
compiler infers the wrong type, i.e. `[<Optional;DefaultParameterValue(null:obj)>] o:obj`.

Lastly, although it is not expected usage, all of the following is possible and does not
generate warnings or errors:
- specifying `Optional` without `DefaultParameterValue` - callers can then omit the argument
and will choose a default value by convention - for primitive types and structs this is
again the default constructor.
- specifying `DefaultParameterValue` without `Optional`.
- specifying `Optional;DefaultParameterValue` on any parameter, i.e. it does not need
to be in the last position.

# Drawbacks
[drawbacks]: #drawbacks

No particular drawbacks have been identified.

# Alternatives
[alternatives]: #alternatives

No alternatives have been considered.

# Unresolved questions
[unresolved]: #unresolved-questions

No unresolved questions at this time.
