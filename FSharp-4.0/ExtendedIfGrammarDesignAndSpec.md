## F# 4.0 Speclet: Extended ``#if`` Grammar (Part 1)

[F# Language User Voice](https://fslang.uservoice.com/forums/245727-f-language/suggestions/6079342-allow-extended-if-grammar), [Pull Request](https://github.com/dotnet/fsharp/pull/55/), [Commit](https://github.com/dotnet/fsharp/commit/438eae21c91d8648f88ea8b343640b1b60460fad)

### Aim

Allow for ``#if```expressions like:

    #if SILVERLIGHT || COMPILED && (NETCOREFX || !DEBUG)
    #endif

Non-Aim: ``#elif``, user voice noted ``#elif`` should be supported. ``#elif`` is ignored in order to reduce the scope.

### Background

F# 3.0 is limited to ``#if`` expressions like:

    #if SILVERLIGHT
    #endif

Advanced ``#if`` expressions are useful in a world with heterogenus platforms. In addition, as C# compiler supports advanced ``#if`` expressions developers are expecting F# compiler feature parity.

### Design

As ``#if`` expressions are not trivial (nested parentheses, operator precedence) a specialized "preprocessor" lexer/parser pair is introduced (pplex.fsy/pppars.fsy).

The existing simple "preprocessor" parser in lex.fsl is removed and instead lex.fsl invokes the "preprocessor" lexer/parser for each line that start with ``#if/#elif``. The resulting expression tree is evaluated and the existing mechanism for activating/deactivating code is used. Any errors discovered during "preprocessor" parsing are reported like normal.

For "squiggles" and "code coloring" we rely on the current editor framework. 

### Specification

Section 3.3 (Conditional Compilation) of the F# specification is adjusted to specify the grammar of the extended #if grammar

Change indent-text to #if-expression defined as:

    ident           := identifier
    paren           := '(' #if-expression ')'
    neg             := '!' #if-expression
    and             := term
                       term && and
    or              := and
                       and || or
    #if-expression  := or
