# F# RFC FS-1038 - Evaluate generalizable values once

The design suggestion [Evaluate generalizable values once](https://github.com/fsharp/fslang-suggestions/issues/602) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] Approved in principle
* [ ] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/602)
* [ ] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/222)
* [ ] Implementation: [In progress](https://github.com/Microsoft/visualfsharp/pull/FILL-ME-IN)


# Summary
[summary]: #summary

As of VS2017.3 / F# 4.1, ``let``-bound values decorated with ``[<GeneralizableValue>]`` aren't actually values; they're compiled as
methods, and the bound expression is re-evaluated each time the "value" is used. This RFC proposes changing the compiled
representation of these values so the bound expression is evaluated only once as with other ``let``-bound values.

# Motivation
[motivation]: #motivation

Module-scoped, ``let``-bound values decorated with ``[<GeneralizableValue>]`` (sometimes called _type functions_) don't currently
behave like other ``let``-bound values in that the bound expressions are evaluated each time the value is used, instead of just
once. This can have performance implications if the expression is "expensive" and users intend for the generalizable value to cache
some result based on the type argument. After the changes proposed by this RFC, the expressions bound to these values will be
evaluated only once and the results cached, just as with any other ``let``-bound values.

Example:

```fsharp
module MyModule =
    // This currently allocates a new string each time 'lowerCaseTypeName' is used.
    [<GeneralizableValue>]
    let lowerCaseTypeName<'T> = typeof<'T>.Name.ToLowerInvariant()
```

# Detailed design
[design]: #detailed-design

To evaluate the expressions bound to the values just once, this RFC proposes leveraging a specific feature of the .NET CLR:
static constructors of generic classes are run each time the class is instantiated with a different type argument, and each
instantation of the class has it's own set of ``static`` fields.

The F# 4.1 compiler simply generates a static method (rather than a static field or property) for module-scoped, ``let``-bound
values marked with ``[<GeneralizableValue>]``. To illustrate, the F# compiler is currently producing code that'd decompile into
something like the following C# code:

```csharp
static class MyModule
{
    public static string lowerCaseTypeName<T>()
    {
        return typeof(T).Name;
    }
}
```

The proposal here is to modify the compiler so for each generalizable value it generates a nested, ``static``, generic,
``beforefieldinit`` class. Within the generated class, the bound expression should be compiled into the class' static constructor
(``.cctor``), and the result assigned to a ``readonly`` field. This field then holds the ``let``-bound value. The compiled F#
code would then decompile into something like this C# code:

```csharp
static class MyModule
{
    [CompilerGenerated]
    private static class lowerCaseTypeName__BackingField<T>
    {
        private static readonly string __value = typeof(T).Name.ToLowerInvariant();

        public static string Value { get { return __value; } }
    }

    public static string lowerCaseTypeName<T>()
    {
        return lowerCaseTypeName__BackingField<T>.Value;
    }
}
```

*TODO:* Expand on the changes needed to the compiler to implement this new functionality.

*TODO:* Expand on what the compiler will need to do, if anything, for backwards-compatibility with older versions of F#. E.g. when
referencing a library built with an older version of F#, or what the generated code needs to look like in order to work correctly
if code compiled with an older version of the compiler (e.g., F# 4.1) references a library compiled with the same version of the
compiler, but that library is later redirected (using assembly binding redirects) to a newer version of the library compiled by
the F# compiler after the changes in this RFC are made.

# Drawbacks
[drawbacks]: #drawbacks

There could be code depending on the fact that generalizable values are evaluated each time they're used. As Don mentioned in the
comments on the initial suggestion, uses of ``[<GeneralizableValue>]`` are already fairly rare and any code relying on the
expression being evaluated multiple times is already failing to adhere to the F# language specification.

This change will also cause the values to become GC roots (like other ``let``-bound values), so could change memory usage and GC
characteristics of programs using these values. Imagine the following scenario: a program creates some large, expensive-to-compute
object within a generalizable value. Currently, the object won't be rooted so the GC can clean it up and finalize it once the object
is no longer needed; after this change, the object will be a GC root so won't be cleaned up or finalized (ever) unless the user
e.g. manually disposes the object.

# Alternatives
[alternatives]: #alternatives

To date, I haven't come up with any other sensible designs for this. In theory it could be done with a dictionary instead of
generating the backing classes, but that approach seems worse in every possible aspect.

# Unresolved questions
[unresolved]: #unresolved-questions

* Exactly how should the cached value be exposed to other code?

  Expanding on the example from above to add a function consuming the generalizable value:

  ```fsharp
    module MyModule =
        // This currently allocates a new string each time 'lowerCaseTypeName' is used.
        [<GeneralizableValue>]
        let lowerCaseTypeName<'T> = typeof<'T>.Name.ToLowerInvariant()

        // Checks whether a type's lowercase, invariant-culture type name is longer than a specified length.
        let typeNameLongerThan<'T> len =
            lowerCaseTypeName<'T>.Length > len
    ```

  * If we need to minimize changes to the rest of the compiler and ensure cross-F#-version compatibility, we'll still need to
    generate a method for the generalizable value; that method could either access the result field in the backing class directly,
    or it could call a property getter on the backing class to retrieve the value, e.g.

    ```csharp
    static class MyModule
    {
        [CompilerGenerated]
        private static class lowerCaseTypeName__BackingField<T>
        {
            private static readonly string __value = typeof(T).Name.ToLowerInvariant();

            public static string Value { get { return __value; } }
        }

        public static string lowerCaseTypeName<T>()
        {
            return lowerCaseTypeName__BackingField<T>.Value;
        }

        public static bool typeNameLongerThan<'T>(int len)
        {
            return lowerCaseTypeName<'T>().Length > len;
        }
    }
    ```

  * The minimalist approach that'd eliminate unnecessary methods and properties is to just make the backing class and field
    ``public``, and have the compiler compile any uses of the value into a static field access (an ``ldsfld`` CIL instruction) to
    the backing field within the generated backing class.

    ```csharp
    static class MyModule
    {
        public static class lowerCaseTypeName<T>
        {
            public static readonly string Value = typeof(T).Name.ToLowerInvariant();
        }

        public static bool typeNameLongerThan<'T>(int len)
        {
            return lowerCaseTypeName<'T>.Value.Length > len;
        }
    }
    ```

  * One hybrid approach might be to combine the above approaches. For backwards-compatibility with earlier versions of F#, the
    compiler could generate code like the 'minimalist' approach above, but also generate a method in the module class (as in the
    current compiler) which simply fetches the value from the backing field. Any code compiled with a newer version of the compiler
    would know to access the field directly, and code compiled with an older compiler that (at runtime) pulled in one of it's
    references compiled with a newer F# compiler (e.g., it's picking up a new version through an assembly binding redirect)
    would still have the generated method available so will continue to work.


* Should the compiler mark the generated classes with ``[<CompilerGenerated>]``? If it does, will it still be possible to debug the
  evaluation of the bound expression in e.g. Visual Studio?
