# F# RFC FS-1148 - Allow decimal constants

The design suggestion [Decimal, nativeint and unativeint literals](https://github.com/fsharp/fslang-suggestions/issues/847) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/847)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/17769)
- [ ] Discussion

# Summary

This feature extends the `[<Literal>]` attribute to support `decimal`.

# Motivation

- Better consistency between numeric types
- Increased interoperability with C#

# Detailed design

As of F# 8 it is not possible to define a decimal constant and the following code
```fsharp
[<Literal>]
let d = 2.5m
```
does not compile.
```
error FS0267: This is not a valid constant expression or custom attribute value
```
However in C# decimal constants are supported and the following code
```csharp
public class C {
  public const decimal c = 3.5M;
}
```
compiles to IL similar to (some sections removed for clarity)
```il
.class public auto ansi beforefieldinit C
    extends [System.Runtime]System.Object
{
    .field public static initonly valuetype [System.Runtime]System.Decimal c
    .custom instance void [System.Runtime]System.Runtime.CompilerServices.DecimalConstantAttribute::.ctor(uint8, uint8, uint32, uint32, uint32) = (
        01 00 01 00 00 00 00 00 00 00 00 00 23 00 00 00
        00 00
    )
    .method private hidebysig specialname rtspecialname static 
        void .cctor () cil managed 
    {
        .maxstack 8

        IL_0000: ldc.i4.s 35
        IL_0002: ldc.i4.0
        IL_0003: ldc.i4.0
        IL_0004: ldc.i4.0
        IL_0005: ldc.i4.1
        IL_0006: newobj instance void [System.Runtime]System.Decimal::.ctor(int32, int32, int32, bool, uint8)
        IL_000b: stsfld valuetype [System.Runtime]System.Decimal C::c
        IL_0010: ret
    }
}

```

# Compatibility

This feature would not be backwards compatible and would not compile on versions preceding F# `N`. Attempts to compile code containing decimal constants market with `[<Literal>]` attribute on F# `< N` would result in the FS0267 compile error.

# Pragmatics

## Performance

This feature should have no or marginal impact on the performance compilation and/or generated code.