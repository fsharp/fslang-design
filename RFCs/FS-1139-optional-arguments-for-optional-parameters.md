# F# RFC FS-1139 - Optional arguments for optional parameters

[The design suggestion](https://github.com/fsharp/fslang-suggestions/issues/1167) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/1167)
- [x] Approved in principle
- [ ] [Implementation](not started)
- [ ] Design Review Meeting(s) with @dsyme and others invitees

# Summary

We introduce easy to use default values to optional arguments in class methods.

# Motivation

Current way of doing it requires 2 attributes (Optional/OptionalArgument and DefaultValue) which comes as a surprise for both new and seasoned F# developers. To make it more easy to read and use F#, this syntax should be simplified the way it works across the industry.

This will also simplify interop code a lot.

Current way
```fsharp
type Foo =
    static member Bar([<System.Runtime.InteropServices.Optional>]
                      [<System.Runtime.InteropServices.DefaultParameterValue(42)>] arg: int) = arg

    // this way is compatible with C# 
    // as F# compiler emits [opt] parameter attribute with .params[1] = 43
    static member Baz([<FSharp.Core.OptionalArgument>]
                      [<System.Runtime.InteropServices.DefaultParameterValue(43)>] arg: int) = arg

Foo.Bar() // 42
Foo.Baz() // 43
```

First of all, this looks unnecessarly verbose, and second there are two attributes both capabale of making it work.
Also attribute `DefaultParameterValue` accepts object type as generic attributes are not implemented in F# yet.

# Detailed design

Proposal makes syntax much more succint and close to what developer could expect.
The default value must be one of the following
- a literal
- enum or struct (but not a GENERIC struct)

This should work only for non optional arguments as optional arguments in F# are compiled as ref type `FSharp.Core.Option<T>`

Example code:

```fsharp
type Enum = 
  | A = 0
  | B = 1

type CustomStruct = struct end

type Foo =
    static member Bar         (arg:  int = 42)     = arg
    static member BarInferred (arg       = 43)     = arg // should be inferred as int

    static member Enum        (arg: Enum = Enum.A) = arg
    static member EnumInferred(arg       = Enum.B) = arg // should be inferred as Enum

    static member Struct      (arg: CustomStruct = CustomStruct()) = arg

    // this is an existing functionality
    static member Opt(?arg: int) = arg
 
Foo.Bar()          // 42
Foo.BarInferred()  // 43
Foo.Enum()         // Enum.A
Foo.EnumInferred() // Enum.B
Foo.Struct()       // CustomStruct
Foo.Opt()          // None
```

Produced binaries with new syntax should be compiled the same way as the current solution with attributes for binary compatability.
Therefore it should be compiled with both attributes
- `System.Runtime.InteropServices.OptionalArgumentAttribute`
- `System.Runtime.InteropServices.DefaultParameterValueAttribute`

```fsharp
open System.Runtime.InteropServices

type Enum = 
  | A = 0
  | B = 1

type CustomStruct = struct end

type Foo =
    // OK
    static member Bar   ([<OptionalArgument; DefaultParameterValue(42)>] arg: int) = arg
    static member Enum  ([<OptionalArgument; DefaultParameterValue(Enum.A)>] arg: Enum) = arg
    static member Struct([<OptionalArgument; DefaultParameterValue(CustomStruct())>] arg: CustomStruct) = arg
```

We should not allow default values preceding any other arguments without default values

```fsharp
type Foo =
  static member Bar(arg1 = 42, arg2: int) = arg1 + arg2 // should give a proper error about position of optional arguments
```

We should not allow non-constant values to be passed as default values as it could break on interop.
There is an existing error FS0267:
`This is not a valid constant expression or custom attribute value`
which is kind of correct, but could give mixed feeling if we'll reuse it for default values of optional arguments (as user doesn't know it will be compiled to attributes). Therefore we'll introduce a new error
```fsharp
type Foo =
    // All above should produce compilation error
    // error FSXXXX: This is not a valid constant expression or struct value to pass as default argument value. Make sure your argument is non-optional

    // ref type
    static member Array(arg: int list = [1;2;3]) = arg
    // generic struct
    static member Array(arg: int voption = ValueSome 1) = arg
    // optional argument
    static member Array(?arg: int = 1) = arg
```

# Drawbacks

Another way of doing the same.

Also, some interop issues and backward compatability issues prevents us from properly utilizing it
 - Attribute values are expected to be constants or structs by both C# and F# specs

# Alternatives

Write overload for each needed combination
```fsharp
type Foo =
    static member Bar(?arg: int) = arg
    static member Bar() = Foo.Bar(42)

Foo.Bar() // Some 42
```

But with this alternative, it's not possible to assign default values to arbitrary arguments
```fsharp
type Foo =
    static member Bar(?arg1: int, ?arg2: int) = arg1, arg2
    // this method suppose to assign default value to arg1 ONLY, therefore arg2 is still there
    static member Bar(?arg2: int) = Foo.Bar(arg1 = 42, ?arg2 = arg2) // we pass 42 as arg1 default value

    // this method suppose to assign default value to arg2 ONLY, therefore arg1 is still there
    // but this produces compilation error
    // error FS0438: Duplicate method. The method 'Bar' has the same name and signature as another method in type 'Foo'.
    static member Bar(?arg1: int) = Foo.Bar(?arg1 = arg1, arg2 = 43) // we pass 43 as arg2 default value

Foo.Bar() // Some 42
```

Also the number of such overloads explodes non-linearly with amount of optional arguments increases.

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change?

  No

* What happens when previous versions of the F# compiler encounter this design addition as source code?

  Compilation failure

* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?

  Code compiles fine

* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?

  N/A

# Pragmatics

## Diagnostics

N/A

## Tooling

N/A

## Performance

Should not be noticable

## Scaling

N/A

## Culture-aware formatting/parsing

N/A

# Unresolved questions

