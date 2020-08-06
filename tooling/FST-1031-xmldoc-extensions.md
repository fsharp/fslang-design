# F# Tooling RFC FST-1031 - Recommended XML doc extensions for F# documentation tooling

NOTE: This is a draft

It is well known that the information present in .NET XML documentation files is not sufficient for quality documentation generation.
The purpose of this tooling RFC is to describe minor but imporant new documentation tags supported by the `FSharp.Formatting` API documentation generation tooling.

## Background

The F# language and implementation adopts [the XML Doc standards of C#](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/xmldoc/),
with some additions, and the F# compiler processes these comments to produce `Component.xml` documentation files acceptable to .NET tooling such as 
Visual Studio.

The tags that form the core of the XML doc specification are:
```
<c>	<para>	<see>*	<value>
<code>	<param>*	<seealso>*	
<example>	<paramref>	<summary>	
<exception>*	<permission>*	<typeparam>*	
<include>*	<remarks>	<typeparamref>	
<list>	<inheritdoc>	<returns>	
```

However, while this information is largely sufficient for autocomplete IDE tooling,
it is well known that the information present in .NET XML documentation files is not sufficient for quality documentation generation. For
this reason, the XML doc standards of C# explicitly encourage documentation tooling to accept additional XML documentation tags for use
by tooling.  

The purpose of this tooling RFC is to describe the proposed new tags supported by the `FSharp.Formatting` API documentation generation library,
including the `GenerateModel` stage that acts prior to HTML generation.

This is a tooling RFC because:

1. The use of the tags is visible in F# source code.

2. The centrality of the `FSharp.Formatting.ApiDocs` component means that what it accepts can end up being a language standard.

In practice, at the time of writing, the `FSharp.Formatting.ApiDocs.dll` component (or its direct predecessor `FSharp.MetadataFormat.dll`) is used
somewhere in the processnig of all F# XML doc files for documentation generation, except in the Visual Studio IDE, and is really the only
component capable of processing F# XML doc files at all.

## Problems Addressed

Five primary problems with .NET XML tags are addressed:

1. There is no way to document a namespace, and this is needed in practice

2. There is no way to categorise types and modules in a namespace (nor members in a type or module), and this is needed in practice

3. There is no way to order the categories once specified

4. There is no way to suppress entities and members from listings

5. XML doc sections such as `<summary>` can't officially contain certain commonly used HTML tags such as `<a>`

Because of the nature of these extensions, they are simply ignored by generic .NET documentation tooling including the Visual Studio IDE.

## `<namespacedoc>`

The `<namespacedoc>` tag gives documentation for an enclosing namespace and can occur on any entity (type or module) in a namespace.  Inside it can be the same tags as for a module, e.g.
`<summary>`, `<remarks>`, `<example>` and so on.

For the XML doc format, the tag extends the tags available on .NET type definitions.  The tag is added to those because there
are no available index entries for namespaces produced by .NET or F# tooling.

Only one `<namespacedoc>` tag should be used per namespace in an assembly.  This condition may or may not be checked by documentation tooling.

For example:

```fsharp
namespace MyLibrary.Core

    open System

    /// <namespacedoc>
    ///   <summary>Contains core functionality for the library.</summary>
    ///   <remarks>You need to use this.</remarks>
    /// </namespacedoc>
    ///
    /// <summary>The main type.</summary>
    type MyType() = 
       member x.P = 1
```

## `<category>`

The `<category>` tag gives a category for an entity or member for the purposes of documentation generation.  It can have one
attribute `Index` used to give a suggested ordering for the categories in documentation generation.

For example:

```fsharp
namespace MyLibrary.Core

    open System

    /// <summary>The main type.</summary>
    ///
    /// <category Index="1">Basic Types</category>
    type MyType() = 
       member x.P = 1

    /// <summary>A type you learn later.</summary>
    ///
    /// <category Index="3">Advanced Types</category>
    type MyAdvancedType() = 
       member x.P = 1
```

## `<exclude>`

The `<exclude>` tag indicates a recommendation to documentation tooling that a type, module or member be excluded from lists of entities
or members, even if other documentation is generated for it.

> NOTE: this tag is also used by the Sandcastle tool.

For example:


```fsharp
namespace MyLibrary.Core

    open System

    /// <summary>This type is for implementation purposes.</summary>
    ///
    /// <exclude />
    type MyNeedlessType() = 
       member x.P = 1
```

## `<a>`, `<b>`, `<i>`

XML doc sections such as `<summary>` can't officially contain HTML tags such as `<a>`, nor is the use of these excluded.

In practice, XML documentation processing tools accept the use of these tags.  This is just noting that these three
tags in particular are considered standard extensions.

## A note on Cross-references 

Note that F# cross-references using the `<see cref="...">` tag for types, modules, extension members and so on all
use the compiled name of the relevant entity or member using `T:`, `M:`, `P:` XML doc signatures .  For example 

```fsharp
    /// <exception cref="T:System.ArgumentNullException">Thrown when ...</exception>

    /// ... <see cref="T:System.Collections.Generic.IComparer`1"/> ...

    /// ... <see cref="M:Microsoft.FSharp.Core.Operators.Compare"/> ....
        
    /// ... <see cref="M:Microsoft.FSharp.Collections.SeqModule.Sort"/>
```
                
## Alternatives

* An alternative is to locate all the above information in separate metadata.  However all the above information is best
  located in the source code, for the same reason as other XML doc information.

* Another alternative for namespace comments is to allow them in F# source code and have the F# compiler
  emit extra entries in the XML doc file with a signature for the namespace.  This would be best done if
  namespace comments are ever supported by C#.

* The `FSharp.MetadataFormat` component previously accepted the adhoc `/// [omit]` text in XML comments. However this
  had the unfortunate side effect of being shown in XML docs in the IDE.  The use of a new `<exclude />` tag is
  a better solution.

## Related Information

There is [a separate proposal for interpreting `///` comments as markdown syntax](https://github.com/fsharp/fslang-suggestions/issues/237),
and the FSharp.Formatting tools [accept a version of markdown comments](https://fsprojects.github.io/FSharp.Formatting/apidocs.html#Markdown-Comments),
though these comments aren't known to other .NET or F# tooling (e.g. F# IDE tooling).



