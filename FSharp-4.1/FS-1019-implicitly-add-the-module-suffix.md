# F# RFC FS-1019 - Implicitly add the Module suffix if a type is being defined with the same name as a module

The design suggestion [Implicitly add the Module suffix if a type is being defined with the same name as a module](https://fslang.uservoice.com/forums/245727-f-language/suggestions/14533251-shorten-compilationrepresentation-compilationrep) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [x] Details: [under discussion](https://github.com/fsharp/FSharpLangDesign/issues/108)
* [x] Implementation: [Completed](https://github.com/Microsoft/visualfsharp/pull/1319)

# Summary
[summary]: #summary

The ``[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]`` attribute is commonly used on top of modules. This RFC proposes that the ``Module`` suffix be added implicitly if a type and a module have the same name within the same namespace declaration group.

For example, for the code below the compiled name of ``module A`` will be ``AModule``, just as if the attribute had been used.

```fsharp
type A() = 
    member x.P = 1

module A =
    let create() = 1
```

# Motivation
[motivation]: #motivation

The attribute is verbose and tedious. In the cases that this RFC seeks to address, _not_ adding the attribute would result in a compilation error anyway, so allowing it to be omitted makes the compiler more user friendly.

# Detailed design
[design]: #detailed-design

If a module is defined in a declaration group (i.e. namespace declaration group, or the group of declarations making up a module) containing a non-augmentation type definition of the same name, then the compiled name of the module is implicitly suffixed by ``Module`` as if the ``[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix )>]`` had been used. This behavior _cannot_ be overriden with the `[<CompiledName()>]` attribute.

This applies if the module is an abbreviation.

This does not apply if the type with the same name as the module has generic parameters, because in that case there is no ambiguity between the module type and the other type. Also, this would break code where a module with the same name has no ``ModuleSuffix`` argument currently:

```fsharp

type Go<'a> = Go of 'a

module Go
```

The `Go` module in this example should not be compiled as `GoModule` after this change, as it wasn't previously.

The use of the explicit attribute is not deprecated, since it can still be useful in a signature file, in the case that the type definition is hidden by the signature but the module definition is not.

# Drawbacks
[drawbacks]: #drawbacks

The main drawback of doing this is that the compiled form of ``module X`` becomes a little more subtle, since you have to check if a type is being defined with the same name.  However this is a very minor problem.

# Alternatives
[alternatives]: #alternatives

The main alternative is to leave things as they are.

# Unresolved questions
[unresolved]: #unresolved-questions

None
