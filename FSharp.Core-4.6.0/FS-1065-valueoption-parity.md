# F# RFC FS-1065 - ValueOption type and function parity

The design suggestion [ValueOption parity with options](https://github.com/fsharp/fslang-suggestions/issues/703) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/703)
* [ ] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/FILL-ME-IN)
* [x] Implementation: [In progress](https://github.com/Microsoft/visualfsharp/pull/5772)

# Summary
[summary]: #summary

Augment the ValueOption type and module functions to be at parity with reference options:

* Add `[<DebuggerDisplay>]` attribute to the type.
* Add `IsNone`, `IsSome`, `None`, `Some`, `op_Implicit`, and `ToString` members to the type.
* Add `ValueOption` versions of the `Option` module functions.

# Motivation
[motivation]: #motivation

Today, ValueOption has a more limited utility due to having significantly less module-bound functions than reference type options. Additionally, it lacks the `DebuggerDisplay` attribute that options have. This makes it seem a bit "second-class" compared with reference type options.

# Detailed design
[design]: #detailed-design

The `ValueOption` type is changed as follows:

Interface file:

```fsharp
/// <summary>The type of optional values, represented as structs.</summary>
///
/// <remarks>Use the constructors <c>ValueSome</c> and <c>ValueNone</c> to create values of this type.
/// Use the values in the <c>ValueOption</c> module to manipulate values of this type,
/// or pattern match against the values directly.</remarks>
[<StructuralEquality; StructuralComparison>]
[<CompiledName("FSharpValueOption`1")>]
[<Struct>]
type ValueOption<'T> =
    /// <summary>The representation of "No value"</summary>
    | ValueNone: 'T voption

    /// <summary>The representation of "Value of type 'T"</summary>
    /// <param name="Value">The input value.</param>
    /// <returns>An option representing the value.</returns>
    | ValueSome: 'T -> 'T voption

    /// <summary>Get the value of a 'ValueSome' option. An InvalidOperationException is raised if the option is 'ValueNone'.</summary>
    member Value : 'T

    /// <summary>Create a value option value that is a 'ValueNone' value.</summary>
    static member None : 'T voption

    /// <summary>Create a value option value that is a 'Some' value.</summary>
    /// <param name="value">The input value</param>
    /// <returns>A value option representing the value.</returns>
    static member Some : value:'T -> 'T voption
    
    /// <summary>Return 'true' if the value option is a 'ValueSome' value.</summary>
    member IsSome : bool

    /// <summary>Return 'true' if the value option is a 'ValueNone' value.</summary>
    member IsNone : bool
```

Implementation file:

```fsharp
[<StructuralEquality; StructuralComparison>]
[<Struct>]
[<CompiledName("FSharpValueOption`1")>]
[<DebuggerDisplay("ValueSome({Value})")>]
type ValueOption<'T> =
    | ValueNone : 'T voption
    | ValueSome : 'T -> 'T voption

    member x.Value = match x with ValueSome x -> x | ValueNone -> raise (new System.InvalidOperationException("ValueOption.Value"))

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.IsNone = match x with ValueNone -> true | _ -> false

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    member x.IsSome = match x with ValueSome _ -> true | _ -> false

    [<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
    static member None : 'T voption = ValueNone

    static member Some (value) : 'T voption = ValueSome(value)

    static member op_Implicit (value) : 'T option = Some(value)

    override x.ToString() = 
        // x is non-null, hence ValueSome
        "ValueSome("^anyToStringShowingNull x.Value^")"

and 'T voption = ValueOption<'T>
```

Additionally, a new module for value option functions is added:

```fsharp
/// <summary>Basic operations on value options.</summary>
module ValueOption =
    /// <summary>Returns true if the value option is not ValueNone.</summary>
    /// <param name="voption">The input value option.</param>
    /// <returns>True if the value option is not ValueNone.</returns>
    [<CompiledName("IsSome")>]
    val inline isSome: voption:'T voption -> bool

    /// <summary>Returns true if the value option is ValueNone.</summary>
    /// <param name="voption">The input value option.</param>
    /// <returns>True if the voption is ValueNone.</returns>
    [<CompiledName("IsNone")>]
    val inline isNone: voption:'T voption -> bool

    /// <summary>Gets the value of the value option if the option is <c>ValueSome</c>, otherwise returns the specified default value.</summary>
    /// <param name="value">The specified default value.</param>
    /// <param name="voption">The input voption.</param>
    /// <returns>The voption if the voption is ValueSome, else the default value.</returns>
    /// <remarks>Identical to the built-in <see cref="defaultArg"/> operator, except with the arguments swapped.</remarks>
    [<CompiledName("DefaultValue")>]
    val defaultValue: value:'T -> voption:'T voption -> 'T

    /// <summary>Gets the value of the voption if the voption is <c>ValueSome</c>, otherwise evaluates <paramref name="defThunk"/> and returns the result.</summary>
    /// <param name="defThunk">A thunk that provides a default value when evaluated.</param>
    /// <param name="voption">The input voption.</param>
    /// <returns>The voption if the voption is ValueSome, else the result of evaluating <paramref name="defThunk"/>.</returns>
    /// <remarks><paramref name="defThunk"/> is not evaluated unless <paramref name="voption"/> is <c>ValueNone</c>.</remarks>
    [<CompiledName("DefaultWith")>]
    val defaultWith: defThunk:(unit -> 'T) -> voption:'T voption -> 'T

    /// <summary>Returns <paramref name="option"/> if it is <c>Some</c>, otherwise returns <paramref name="ifNone"/>.</summary>
    /// <param name="ifNone">The value to use if <paramref name="option"/> is <c>None</c>.</param>
    /// <param name="option">The input option.</param>
    /// <returns>The option if the option is Some, else the alternate option.</returns>
    [<CompiledName("OrElse")>]
    val orElse: ifNone:'T voption -> voption:'T voption -> 'T voption

    /// <summary>Returns <paramref name="voption"/> if it is <c>Some</c>, otherwise evaluates <paramref name="ifNoneThunk"/> and returns the result.</summary>
    /// <param name="ifNoneThunk">A thunk that provides an alternate value option when evaluated.</param>
    /// <param name="voption">The input value option.</param>
    /// <returns>The voption if the voption is ValueSome, else the result of evaluating <paramref name="ifNoneThunk"/>.</returns>
    /// <remarks><paramref name="ifNoneThunk"/> is not evaluated unless <paramref name="voption"/> is <c>ValueNone</c>.</remarks>
    [<CompiledName("OrElseWith")>]
    val orElseWith: ifNoneThunk:(unit -> 'T voption) -> voption:'T voption -> 'T voption

    /// <summary>Gets the value associated with the option.</summary>
    /// <param name="voption">The input value option.</param>
    /// <returns>The value within the option.</returns>
    /// <exception href="System.ArgumentException">Thrown when the option is ValueNone.</exception>
    [<CompiledName("GetValue")>]
    val get: voption:'T voption -> 'T

    /// <summary><c>count inp</c> evaluates to <c>match inp with ValueNone -> 0 | ValueSome _ -> 1</c>.</summary>
    /// <param name="voption">The input value option.</param>
    /// <returns>A zero if the option is ValueNone, a one otherwise.</returns>
    [<CompiledName("Count")>]
    val count: voption:'T voption -> int

    /// <summary><c>fold f s inp</c> evaluates to <c>match inp with ValueNone -> s | ValueSome x -> f s x</c>.</summary>
    /// <param name="folder">A function to update the state data when given a value from a value option.</param>
    /// <param name="state">The initial state.</param>
    /// <param name="voption">The input value option.</param>
    /// <returns>The original state if the option is ValueNone, otherwise it returns the updated state with the folder
    /// and the voption value.</returns>
    [<CompiledName("Fold")>]
    val fold<'T,'State> : folder:('State -> 'T -> 'State) -> state:'State -> voption:'T voption -> 'State

    /// <summary><c>fold f inp s</c> evaluates to <c>match inp with ValueNone -> s | ValueSome x -> f x s</c>.</summary>
    /// <param name="folder">A function to update the state data when given a value from a value option.</param>
    /// <param name="voption">The input value option.</param>
    /// <param name="state">The initial state.</param>
    /// <returns>The original state if the option is ValueNone, otherwise it returns the updated state with the folder
    /// and the voption value.</returns>
    [<CompiledName("FoldBack")>]
    val foldBack<'T,'State> : folder:('T -> 'State -> 'State) -> voption:'T voption -> state:'State -> 'State

    /// <summary><c>exists p inp</c> evaluates to <c>match inp with ValueNone -> false | ValueSome x -> p x</c>.</summary>
    /// <param name="predicate">A function that evaluates to a boolean when given a value from the option type.</param>
    /// <param name="voption">The input value option.</param>
    /// <returns>False if the option is ValueNone, otherwise it returns the result of applying the predicate
    /// to the option value.</returns>
    [<CompiledName("Exists")>]
    val exists: predicate:('T -> bool) -> voption:'T voption -> bool

    /// <summary><c>forall p inp</c> evaluates to <c>match inp with ValueNone -> true | ValueSome x -> p x</c>.</summary>
    /// <param name="predicate">A function that evaluates to a boolean when given a value from the value option type.</param>
    /// <param name="voption">The input value option.</param>
    /// <returns>True if the option is None, otherwise it returns the result of applying the predicate
    /// to the option value.</returns>
    [<CompiledName("ForAll")>]
    val forall: predicate:('T -> bool) -> voption:'T voption -> bool

    /// <summary>Evaluates to true if <paramref name="voption"/> is <c>ValueSome</c> and its value is equal to <paramref name="value"/>.</summary>
    /// <param name="value">The value to test for equality.</param>
    /// <param name="voption">The input value option.</param>
    /// <returns>True if the option is <c>ValueSome</c> and contains a value equal to <paramref name="value"/>, otherwise false.</returns>
    [<CompiledName("Contains")>]
    val inline contains: value:'T -> voption:'T voption -> bool when 'T : equality

    /// <summary><c>iter f inp</c> executes <c>match inp with ValueNone -> () | ValueSome x -> f x</c>.</summary>
    /// <param name="action">A function to apply to the voption value.</param>
    /// <param name="voption">The input value option.</param>
    /// <returns>Unit if the option is ValueNone, otherwise it returns the result of applying the predicate
    /// to the voption value.</returns>
    [<CompiledName("Iterate")>]
    val iter: action:('T -> unit) -> voption:'T voption -> unit

    /// <summary><c>map f inp</c> evaluates to <c>match inp with ValueNone -> ValueNone | ValueSome x -> ValueSome (f x)</c>.</summary>
    /// <param name="mapping">A function to apply to the voption value.</param>
    /// <param name="voption">The input value option.</param>
    /// <returns>A value option of the input value after applying the mapping function, or ValueNone if the input is ValueNone.</returns>
    [<CompiledName("Map")>]
    val map: mapping:('T -> 'U) -> voption:'T voption -> 'U voption

    /// <summary><c>map f voption1 voption2</c> evaluates to <c>match voption1, voption2 with ValueSome x, ValueSome y -> ValueSome (f x y) | _ -> ValueNone</c>.</summary>
    /// <param name="mapping">A function to apply to the voption values.</param>
    /// <param name="voption1">The first value option.</param>
    /// <param name="voption2">The second value option.</param>
    /// <returns>A value option of the input values after applying the mapping function, or ValueNone if either input is ValueNone.</returns>
    [<CompiledName("Map2")>]
    val map2: mapping:('T1 -> 'T2 -> 'U) -> voption1: 'T1 voption -> voption2: 'T2 voption -> 'U voption

    /// <summary><c>map f voption1 voption2 voption3</c> evaluates to <c>match voption1, voption2, voption3 with ValueSome x, ValueSome y, ValueSome z -> ValueSome (f x y z) | _ -> ValueNone</c>.</summary>
    /// <param name="mapping">A function to apply to the value option values.</param>
    /// <param name="voption1">The first value option.</param>
    /// <param name="voption2">The second value option.</param>
    /// <param name="voption3">The third value option.</param>
    /// <returns>A value option of the input values after applying the mapping function, or ValueNone if any input is ValueNone.</returns>
    [<CompiledName("Map3")>]
    val map3: mapping:('T1 -> 'T2 -> 'T3 -> 'U) -> 'T1 voption -> 'T2 voption -> 'T3 voption -> 'U voption

    /// <summary><c>bind f inp</c> evaluates to <c>match inp with ValueNone -> ValueNone | ValueSome x -> f x</c></summary>
    /// <param name="binder">A function that takes the value of type T from a value option and transforms it into
    /// a value option containing a value of type U.</param>
    /// <param name="voption">The input value option.</param>
    /// <returns>An option of the output type of the binder.</returns>
    [<CompiledName("Bind")>]
    val bind: binder:('T -> 'U voption) -> voption:'T voption -> 'U voption

    /// <summary><c>flatten inp</c> evaluates to <c>match inp with ValueNone -> ValueNone | ValueSome x -> x</c></summary>
    /// <param name="voption">The input value option.</param>
    /// <returns>A value option of the output type of the binder.</returns>
    /// <remarks><c>flatten</c> is equivalent to <c>bind id</c>.</remarks>
    [<CompiledName("Flatten")>]
    val flatten: voption:'T voption voption -> 'T voption

    /// <summary><c>filter f inp</c> evaluates to <c>match inp with ValueNone -> ValueNone | ValueSome x -> if f x then ValueSome x else ValueNone</c>.</summary>
    /// <param name="predicate">A function that evaluates whether the value contained in the value option should remain, or be filtered out.</param>
    /// <param name="voption">The input value option.</param>
    /// <returns>The input if the predicate evaluates to true; otherwise, ValueNone.</returns>
    [<CompiledName("Filter")>]
    val filter: predicate:('T -> bool) -> voption:'T voption -> 'T voption

    /// <summary>Convert the value option to an array of length 0 or 1.</summary>
    /// <param name="voption">The input value option.</param>
    /// <returns>The result array.</returns>
    [<CompiledName("ToArray")>]
    val toArray: voption:'T voption -> 'T[]

    /// <summary>Convert the value option to a list of length 0 or 1.</summary>
    /// <param name="voption">The input value option.</param>
    /// <returns>The result list.</returns>
    [<CompiledName("ToList")>]
    val toList: voption:'T voption -> 'T list

    /// <summary>Convert the value option to a Nullable value.</summary>
    /// <param name="voption">The input value option.</param>
    /// <returns>The result value.</returns>
    [<CompiledName("ToNullable")>]
    val toNullable: voption:'T voption -> Nullable<'T>

    /// <summary>Convert a Nullable value to a value option.</summary>
    /// <param name="value">The input nullable value.</param>
    /// <returns>The result value option.</returns>
    [<CompiledName("OfNullable")>]
    val ofNullable: value:Nullable<'T> -> 'T voption 

    /// <summary>Convert a potentially null value to a value option.</summary>
    /// <param name="value">The input value.</param>
    /// <returns>The result value option.</returns>
    [<CompiledName("OfObj")>]
    val ofObj: value: 'T -> 'T voption  when 'T : null

    /// <summary>Convert an option to a potentially null value.</summary>
    /// <param name="value">The input value.</param>
    /// <returns>The result value, which is null if the input was ValueNone.</returns>
    [<CompiledName("ToObj")>]
    val toObj: value: 'T voption -> 'T when 'T : null
```

# Drawbacks
[drawbacks]: #drawbacks

Additional functions further the decisions that people need to make w.r.t working with reference or value types. Though not necessarily bad, this doesn't exactly simplify things.

# Alternatives
[alternatives]: #alternatives

An alternative design could have been to never do the `ValueOption` type, and instead have some notion of optional "shapes" that could be used either as a reference type or as a value type. But considering we do not have higher-kinded types and typeclasses in the language at this point in time, there is little reason to not flesh this feature out.

# Compatibility
[compatibility]: #compatibility

* Is this a breaking change?

No.

* What happens when previous versions of the F# compiler encounter this design addition as source code?

The same as if an older F# compiler encounters the `ValueOption` type. This is primarily just additional functions.

* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?

The same behavior as before, since this is binary-compatible.

* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?

Since this is binary-compatible, no change other than seeing new functions occurs.

# Unresolved questions
[unresolved]: #unresolved-questions

N/A
