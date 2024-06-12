
## F# 4.0 Speclet: Allow inheritance from types that have multiple interface instantiations

[User Voice](http://fslang.uservoice.com/forums/245727-f-language/suggestions/5663504-allow-to-implement-the-same-interface-at-different), 
[Pull Request](https://github.com/dotnet/fsharp/pull/18), 
[Commit](https://github.com/dotnet/fsharp/commit/2302b9edfd6585357333ee2a426f2e78a797e9c0)

### Background

F# 2.0-3.1 had an overly draconian restrictions where you could not inherit from any .NET type that 
contains multiple instantiations of the same interface type (for the full interface type set in the whole hierarchy).  
This made it impossible to use certain C# types at all.  We've gradually seen more of these C# types in practice

### Design

This change allows inheritance from such types in type declarations and object expressions.


### Limitations to the Design

The change still keeps the restriction that an F# type can itself only implement one instantiation of a generic interface type.  For example

    type C() = 
        interface I<int>
        interface I<string>

is not allowed. This is partly because of the way interface implementation methods are named in compiled IL (only the prefix "I" is added), and partly because the equivalent object expression form can include type unknowns, e.g.

    { interface I<_> with ...
      interface I<_> with ...

and we don't want to support this kind of inference, and equally don't want a non-orthogonality between object expressions and class definitions.  As a workaround you can always add an extra inherit each type you wish to introduce new interface instantiations, e.g.

    type C() = 
        interface I<int> with ...
    
    type D() = 
        inherit C()
        interface I<string>  with ...


This also applies to an F# type attempting to implement an interface which includes multiple 
instantiations of the same interface, e.g.

    type C() = 
        interface I2  // we expect an error here if I2 : I<int> and I<string>

where ``I2`` is a C# type including interfaces ``I<int>`` and ``I<string>``.

### Compatibility 

This is not a breaking change.
