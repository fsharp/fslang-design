# F# RFC FS-1027 - FSI Reference Model and extending `#r`

* [x] Approved in principle
* [ ] [FSLang Suggestion](https://github.com/fsharp/fslang-suggestions/issues/542)
* [ ] Details: [under discussion](https://github.com/fsharp/fslang-design/issues/167)
* [ ] Implementation: [In progress](https://github.com/Microsoft/visualfsharp/pull/FILL-ME-IN)

# Summary
[summary]: #summary

The following extensions to `#r` are proposed:

* `#r "impl: <path-to-implementation-assembly>"`
* `#r "paket: <paket command>"`
* `#r "nuget: <package-name>, <package-version>"`

This extends the "language" of `#r` to support implementation and reference assemblies, NuGet packages, Paket dependencies, and potentially more.

# Motivation
[motivation]: #motivation

Motivation for this change is twofold:

1. There is a [strong desire](https://github.com/fsharp/fslang-suggestions/issues/542) to reference packages via `#r` instead of assemblies.  There has also been [experimental work](https://github.com/Microsoft/visualfsharp/pull/2483) to support Paket in this way.
2. .NET Core uses both Reference Assemblies and Implementation Assemblies, which FSI must understand.  It does not understand this split today.

# Detailed design
[design]: #detailed-design

Supporting this design requires significant changes in how FSI references assemblies.  As stated above, .NET Core (and .NET Standard) introduces a new model by which assemblies are used and laid out on disk.  FSI cannot assume everything will be in the same place as it is in a .NET Framework world.

To begin, existing behavior with `#r` will remain unchanged for FSI running in non-.NET Core.  That is, if you reference an assembly today on .NET Framework or Mono via `#r`, it will always continue to work in the same way that it always has.

On .NET Core, however, `#r "<path-to-assembly>"` will be used for referencing *Reference Assemblies*.  To handle *Implementation Assemblies*, we introduce the notion of `#r impl: <path-to-implementation-assembly>`.  This is needed to directly reference implementation asseblies pulled down in packages or in the .NET Core Shared Framework.

This split for .NET Core necessitates the introduction of `#r "nuget: name, version"` and `#r "paket: paket-command"` to simplify referencing dependencies in F# script files for .NET Core.  The intention is that FSI Scripts in the future will always specify dependencies via `#r "nuget` or `#r "paket` (or a future extended command, like `#r "project"`), allowing only FSI to be concerned with referencing the correct assemblies.  FSI in turn will defer to the specified package manager to resolve assemblies, load references in an ephemeral script, and load that script in the FSI session so that it has the references it needs.

Here is an example:

```fsharp
#r "paket: nuget Newtonsoft.Json"

open Newtonsoft.Json

type Foo = { IntVal: int; StringVal: string }

let foo = { IntVal = 12; StringVal = "bananas" }
let json = JsonConvert.SerializeObject(foo)

printfn "Object: %A\n JSON: %s" foo json
```

When FSI encounters this script, it will:

1. Create an ephemeral script.
2. Call into Paket to resolve dependencies.
3. Generate a set of *Reference Assembly* and *Implementation Assembly* references in the ephemeral script, using `#r "path-to-ref-assembly"` and `#r "impl: path-to-impl-assembly""`
4. Load the ephemeral script in the FSI session, thus giving FSI the correct references it needs to run in .NETCore
4. Execute the above script which requires `Newtonsoft.Json`.

## Errors

FSI will have to display errors from the underlying assembly resolver.  For example, if someone specifies `#r "paket: <paket-command>"` and Paket generates an error, FSI must display that.

There is also another scenario to consider, which we will explicitly not handle.  For example, given the following:

```fsharp
#r "paket: nuget Foo.Bar" // Depends on System.Whatever version 4.1.0
#r "paket: nuget Baz.Qux" // Depends on System.Whatever version 4.2.0

// some scripting code
```

This will generate an error because `System.Whatever` version `4.1.0` will already be loaded.  It's not possible to load two versions of the same assembly in the same session without doing so within an `AssemblyLoadContext`, which is not something we will do at this time.  Thus, a meaningful error must be generated stating that the `paket: nuget Baz.Qux` requires `System.Whatever` version `4.2.0`, but version `4.1.0` was already loaded.

We may consider using `AssemblyLoadContext` in some clever way in the future, but it's very difficult to get right and well outside the scope of what we need to support for a .NET Core 2.0 timeframe.

# Drawbacks
[drawbacks]: #drawbacks

A potential for increased complexity in Scripting.  This also has downstream effects in documentation and the general mindshare on how to reference assemblies for F# scripting.

Also, using `#r "impl:"` (or `#r-impl`) in general should be discouraged since it's more of a way to implement a mechanism required for .NET Core.  Having a new directive or capability that people *could* use, but *shouldn't* use, always opens things up for abuse.

# Alternatives
[alternatives]: #alternatives

Another alternative is to add more directives, such as the following:

* `#r-impl "<path-to-impl-assembly>`
* `#r-nuget "<package-name>, <package-version>`
* `#r-paket "<paket-command>: <package-or-dependency>`

A drawback here is that this is not something that C# would likely do in C# scripting.  Although the goal isn't to align with whatever C# is doing, differentiating on directives doesn't seem worth it unless a new directive is added for each *type* of operation we wish to perform.  Having a new directive for one type of operation (e.g., `#r-impl`), but no new directive for the rest would be strange and inconsistent.

# Unresolved questions
[unresolved]: #unresolved-questions

None at this time.

