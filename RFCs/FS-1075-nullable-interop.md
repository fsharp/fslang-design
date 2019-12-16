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
Currently Nullable-typed values may be given at the callsite:
```fsharp
    C.SomeMethod(channels = Nullable 3)
    C.SomeMethod(channels = Nullable 3, ratio = Nullable 3)
    C.SomeMethod(ratio = Nullable 3)
```
We adjust F# to allow providing non-Nullable values at the callsite:
```fsharp
    C.SomeMethod(channels = 3)
    C.SomeMethod(channels = 3, ratio = 3)
    C.SomeMethod(ratio = 3)
```

# Detailed Description of Problem

The change involves

1. Removing the propagation of a "strong" known type into the checking of the argument expression (if the argument is a C#-style nullable optional argument).

2. Using a type-directed rule to allow either `Nullable<ty>` or `ty` arguments. 

The use of a type-directed rule means that

```fsharp
let f x = C.SomeMethod(ratio = x)
```

would continue to typecheck as today (here `x` will have type `Nullable<int>`) until you add a type annotation to `x`, e.g.

```fsharp
let f (x: int) = C.SomeMethod(ratio = x)
```

When a C# argument `x` has a default value and is nullable, the use of `?x` must take type option.

### Code samples and test matrix

Consider the following C# code, covering traditional optional arguments, nullable optional arguments (with non-null and null defaults):
```csharp
public class C
{
    public static int TraditionalOptionals(int x = 3, string y = "abc", double d = 5.0)
    {
        return x + y.Length + (int) d;
    }
    public static int NullableOptionals(int? x = 3, string y = "abc", double? d = 5.0)
    {
        return (x.HasValue ? x.Value : -100) + y.Length + (int) (d.HasValue ? d.Value : 0.0);
    }
    public static int NullableOptionals2(int? x = null, string y = null, double? d = null)
    {
        int length;
        if (y == null)
            length = -1;
        else
            length = y.Length;
        return (x.HasValue ? x.Value : -1) + length + (int) (d.HasValue ? d.Value : -1.0);
    }
}
```
The use of traditional optionals is as follows:
```fsharp
C.TraditionalOptionals(x = 6) // produces 14
C.TraditionalOptionals(y = "aaaaaa") // produces 14
C.TraditionalOptionals(d = 8.0) // produces 14
```
Note using the `?x` name for the parameter of a traditional optional takes an `Some`/`None` option value:
```fsharp
C.TraditionalOptionals(?x = Some 6) // produces 14
C.TraditionalOptionals(?y = Some "aaaaaa") // produces 14
C.TraditionalOptionals(?d = Some 8.0) // produces 14
C.TraditionalOptionals(?x = None) // produces 11
C.TraditionalOptionals(?y = None) // produces 11
C.TraditionalOptionals(?d = None) // produces 11
```
The use of nullable optionals with defaults is as follows:
```fsharp
C.NullableOptionals()  // produces 11
C.NullableOptionals(x = 6)  // produces 14
C.NullableOptionals(y = "aaaaaa")  // produces 14
```
Again using the `?x` name for the parameter of an optional takes an `Some`/`None` option value - this doesn't change just because the C# method uses a nullable value.
```fsharp
C.NullableOptionals(?x = Some 6)  // produces 14
C.NullableOptionals(?d = Some 8.0)  // produces 14
C.NullableOptionals(?x = None)  // produces -92
C.NullableOptionals(?d = None)  // produces 6
```
For legacy reasons an explicit `Nullable` can be given:
```fsharp
C.NullableOptionals(x = Nullable 6)   // produces 14 
C.NullableOptionals(d = Nullable 8.0)  // produces 14
```
The use of nullable optionals with `null` defaults follows the same pattern
```fsharp
C.NullableOptionals2()  // produces -3
C.NullableOptionals2(x = 6)  // produces 4 // can provide nullable for legacy
C.NullableOptionals2(y = "aaaaaa")   // produces 4
C.NullableOptionals2(d = 8.0)  // produces 6 

C.NullableOptionals2(?x = Some 6) // produces 4
C.NullableOptionals2(?d = Some 8.0) // produces 6
C.NullableOptionals2(?x = None) // produces -3
C.NullableOptionals2(?d = None) // produces -3
```
Again for legacy reasons an explicit `Nullable` can be given:
```fsharp
C.NullableOptionals2(x = Nullable 6)   // produces 4 // can provide nullable for legacy
C.NullableOptionals2(d = Nullable 8.0)   // produces 6 
```

# Drawbacks
[drawbacks]: #drawbacks

Potential confusion about what is and isn't allowed (though this will be less than the current confusion)

# Compatibility
[compatibility]: #compatibility

This is backwards compatible.

# Alternatives
[alternatives]: #alternatives

Don't fix this problem

# Unresolved questions
[unresolved]: #unresolved-questions

* [ ] Should we apply this rule to **all** Nullable-typed arguments on methods, not just optional ones, giving a value-to-Nullable rule at method calls?


