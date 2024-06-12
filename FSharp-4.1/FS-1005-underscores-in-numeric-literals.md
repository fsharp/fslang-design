# F# RFC FS-0005 - Underscore Literals

The design suggestion [Underscores in Numeric Literals](https://fslang.uservoice.com/forums/245727-f-language/suggestions/6628026-accept-integer-literals-like-12-345-for-readabilit) has been marked "approved in principle".
This RFC covers the detailed proposal for this suggestion.

* [x] [Approved in principle](https://fslang.uservoice.com/forums/245727-f-language/suggestions/6628026-accept-integer-literals-like-12-345-for-readabilit)
* [x] Details: [discussion](https://github.com/fsharp/FSharpLangDesign/issues/52)
* [x] Implementation: [Completed](https://github.com/dotnet/fsharp/pull/1243)


# Summary
[summary]: #summary

Allow underscores between any digits in numeric literals. This feature enables you, for example, to separate groups of digits in numeric literals, which can improve the readability of your code.


For instance, if your code contains numbers with many digits, you can use an underscore character to separate digits in groups of three, similar to how you would use a punctuation mark like a comma, or a space, as a separator.

# Motivation
[motivation]: #motivation

This is a popular feature in other languages. Some other languages with a similar feature:

* [Perl](http://perldoc.perl.org/perldata.html#Scalar-value-constructors)
* [Ruby](http://www.ruby-doc.org/core-2.1.3/doc/syntax/literals_rdoc.html#label-Numbers)
* [Java 7](http://docs.oracle.com/javase/7/docs/technotes/guides/language/underscores-literals.html)
* [C++11 (use single quote)](http://www.open-std.org/jtc1/sc22/wg21/docs/papers/2013/n3781.pdf)

just to name a few...

# Detailed design
[design]: #detailed-design

You can place underscores only between digits. You cannot place underscores in the following places:

* At the beginning or end of a number
* Adjacent to a decimal point in a floating point literal
* Prior to an F or L or other suffix
* In positions where a string of digits is expected

Taken from the Java documentation, the following examples demonstrate valid examples:

```fsharp
let creditCardNumber = 1234_5678_9012_3456L
let socialSecurityNumber = 999_99_9999L
let pi = 	3.14_15F
let hexBytes = 0xFF_EC_DE_5E
let hexWords = 0xCAFE_BABE
let maxLong = 0x7fff_ffff_ffff_ffffL
let nybbles = 0b0010_0101
let bytes = 0b11010010_01101001_10010100_10010010
```

Taken from the Java documentation, the following examples demonstrate valid and invalid underscore placements (which are highlighted) in numeric literals:

```fsharp
let pi1 = 3_.1415F      // Invalid cannot put underscores adjacent to a decimal point
let pi2 = 3._1415F      // Invalid cannot put underscores adjacent to a decimal point
let socialSecurityNumber1 = 999_99_9999_L         // Invalid cannot put underscores prior to an L suffix

let x1 = _52              // This is an identifier, not a numeric literal
let x2 = 5_2              // OK (decimal literal)
let x3 = 52_              // Invalid cannot put underscores at the end of a literal
let x4 = 5_______2        // OK (decimal literal)

let x5 = 0_x52            // Invalid cannot put underscores in the 0x radix prefix
let x6 = 0x_52            // Invalid cannot put underscores at the beginning of a number
let x7 = 0x5_2            // OK (hexadecimal literal)
let x8 = 0x52_            // Invalid cannot put underscores at the end of a number

// In contrast to Java, literals with leading zeros are decimal in F#.
let x9 = 0_52             // OK (decimal literal)
let x10 = 05_2            // OK (decimal literal)
let x11 = 052_            // Invalid cannot put underscores at the end of a number

// To create an octal literal, prefix it with '0o' similar to hexadecimal literals. The same rules apply:
let x12 = 0_o52            // Invalid cannot put underscores in the 0o radix prefix
let x13 = 0o_52            // Invalid cannot put underscores at the beginning of a number
let x14 = 0o5_2            // OK (octal literal)
let x15 = 0o52_            // Invalid cannot put underscores at the end of a number
```

For QZRNG literals, the string passed to the library routine has underscores removed.

# Drawbacks
[drawbacks]: #drawbacks

The main drawback is the cost involved in making the addition.

# Alternatives
[alternatives]: #alternatives

The main alternative is simply not doing it at all.

# Unresolved questions
[unresolved]: #unresolved-questions

None remaining
