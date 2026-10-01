# F# RFC FS-1333 - Record and union constructor accessibility

The design suggestion [Allow records/DUs to have different visibility constructors to their fields](https://github.com/fsharp/fslang-suggestions/issues/852) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/852)
- [x] Approved in principle
- [ ] Implementation
- [x] [Discussion](https://github.com/fsharp/fslang-design/discussions/817)

# Summary

Provide a syntax to specify the accessibility of constructors for records and unions, separately from the type's accessibility or its representation's accessibility.

# Motivation

In domain modeling, it is common to provide smart constructors, ie functions that construct a value of a given type after performing additional validation to maintain invariants on the type.
However, this is only effective at enforcing invariants if the smart constructor is the only available way to create a value of this type.

Currently, the only way to prevent constructing an otherwise accessible record or union is to specify the accessibility of the type's representation.
However, this also prevents outside access to the record fields and deconstruction of the record or union cases.
The only way to make them accessible again is to add explicit properties on the record:

```fsharp
type SafeFilePath =
    internal
        {
            DirectoryName_ : string
            FileName_ : string
        }
    static member Create (path : string) : Result<SafeFilePath, string> =
         failwith "Insert parsing/error checking logic here".

    member this.DirectoryName  : int = this.DirectoryName_
    member this.FileName : string = this.FileName_
```

or an active pattern for a union:

```fsharp
type MyDU =
    internal
    | X_ of int
    | Y_ of string

[<AutoOpen>]
module MyDU =
    let (|X|Y|) = function | X_ i -> X i | Y_ i -> Y i
```

This is unnecessarily verbose and error-prone for a common use case.

# Detailed design

A new item can now be added among the list of members of a record or a union:

```fsharp
<accessibility-modifier> new
```

This item cannot appear multiple times in the same type declaration.

## Where this accessibility applies

When present, this item determines the accessibility of the type's constructor(s).
More specifically, the following constructs are subject to this accessibility:

* Record construction syntax:

  ```fsharp
  type R =
    { x: int }
    private new

  let r = { x = 1 } // Error: the record constructor is not accessible
  ```

* Record copy-and-update syntax:

  ```fsharp
  type R =
    { x: int }
    private new
    static member Default = { x = 1 }

  let r = { R.Default with x = 2 } // Error: the record constructor is not accessible
  ```

* Record construction from C#:

  ```fsharp
  type R =
    { x: int }
    private new
  ```

  ```csharp
  var r = new R(1); // Error CS1729 : 'R' does not contain a constructor that takes 1 arguments
  ```

* Union case construction:

  ```fsharp
  type U =
    | Case1 of int
    | Case2
    private new

  let u1 = Case1 42 // Error: the union case constructor is not accessible
  let u2 = Case2    // Error: the union case constructor is not accessible
  ```

* Static members for union case construction from C#:

  ```fsharp
  type U =
    | Case1 of int
    | Case2
    private new
  ```

  ```csharp
  var u1 = U.NewCase1(42); // Error CS0117 : 'U' does not contain a definition for 'NewCase1'
  var u2 = U.Case2;        // Error CS0117 : 'U' does not contain a definition for 'Case2'
  ```

## Where this accessibility does not apply

In contrast, the following constructs are _not_ subject to this accessibility:

* Record field read and (if mutable) write access:

  ```fsharp
  type R =
    { x: int
      mutable y: int }
    private new
    static member Default = { x = 1; y = 2 }

  let r = R.Default
  r.x      // OK
  r.y <- 3 // OK
  ```

* Record deconstruction:

  ```fsharp
  type R =
    { x: int }
    private new
    static member Default = { x = 1 }

  match R.Default with
  | { x = 2 } -> "two" // OK
  | _ -> "something else"
  // val it: string = "something else"
  ```

* Anonymous record copy-and-update syntax using the given type as source:

  ```fsharp
  type R =
    { x: int }
    private new
    static member Default = { x = 1 }

  let r = {| R.Default with y = "a" |} // OK
  // val r: {| x: int; y: string |} = { x = 1; y = "a" }
  ```

* Union deconstruction:

  ```fsharp
  type U =
    | Case1 of int
    | Case2
    private new

  let f (u: U) =
    match u with
    | Case1 x -> x // OK
    | Case2 -> 0   // OK
  // val f: u -> int = <fun>
  ```

* `IsX` case check properties:

  ```fsharp
  type U =
    | Case1 of int
    | Case2
    private new

  let f (u: U) =
    if u.IsCase1 then 1 else 2 // OK
  // val f: u -> int = <fun>
  ```

* `Tag` property, nested `Tags` enum and nested classes per case from C#:

  ```fsharp
  type U =
    | Case1 of int
    | Case2
    private new
    static member Default = Case1 1
  ```

  ```csharp
  var x = U.Default.Tag == (int)U.Tags.Case1 ? (x as U.Case1).Item : 0; // OK
  ```

## Accessibility hierarchy

If the representation accessibility is specified and the constructor accessibility is not,
then the representation accessibility applies to the construction constructs listed above.
This is unchanged from the existing behaviour.

```fsharp
type R =
  private { x: int }

let r = { x = 1 } // error FS1093: The union cases or fields of the type 'R' are not accessible from this code location
```

If both the representation accessibility and the constructor accessibility are specified,
then the more restrictive of the two applies to the construction constructs listed above,
and the representation accessibility applies to other representation constructs such as field access and deconstruction.

**Alternate design**: Should the constructor accessibility be applied instead, even if it is less restrictive than the representation accessibility?
It does seem strange to be able to construct a value using field names, and then to be unable to access said fields.
Reasonable use cases would be welcome in support of this variation.

**Design question**: If we do keep the design as specified here, should there be a warning, or even an error,
when the constructor accessibility is less restrictive than the representation accessibility?
The constructor accessibility will essentially be ignored.

```fsharp
type R =
  private { x: int }
  public new       // Design question: should there be a warning or error here?

let r = { x = 1 }  // error FS1093: The union cases or fields of the type 'R' are not accessible from this code location
                   // Alternate design: OK

let f (r: R) = r.x // error FS1093: The union cases or fields of the type 'R' are not accessible from this code location
```

# Changes to the F# spec

* In section "6.3.5 Record Expressions", the sentence:

  > Each referenced field must be accessible (see [§10.5](#105-accessibility-annotations)), as must the type R.

  becomes:

  > Each referenced field must be accessible (see [§10.5](#105-accessibility-annotations)), as must the type R, and its constructor (see [§8.4.1](#841-members-in-record-types)).

* In section "6.3.6 Copy-and-update Record Expressions", after the sentence:

  > Each field label must resolve to a field Fi in a single record type R , all of whose fields are accessible.

  the following sentence is added:

  > The record type R's constructor must also be accessible.

* In section "8.4.1 Members in Record Types", a new paragraph is added:

  > Additionally, among these declarations, record types may declare the accessibility of their constructor.
  > This consists in a keyword `new` preceded by an accessibility modifier (see [§10.5](#105-accessibility-annotations)).

* In section "8.5.1 Members in Union Types", a new paragraph is added:

  > Additionally, among these declarations, union types may declare the accessibility of their case tags as constructors.
  > This consists in a keyword `new` preceded by an accessibility modifier (see [§10.5](#105-accessibility-annotations)).

* In section "10.5 Accessibility Annotations", the second table is given a new line:

  > | Component | Location | Example |
  > |---|---|---|
  > | Record or union constructor | Precedes identifier | `type R = { x: int } with private new` |

* In section "14.2.2 Item-Qualified Lookup", under the bullet point:

  > * If `item` is a union case tag, exception tag, or active pattern result element tag

  the first nested bullet point becomes:

  > * Check the tag for accessibility and attributes. For a union case tag, this includes its accessibility as constructor (see [§8.5.1](#851-members-in-union-types)).

# Drawbacks

This introduces yet another level of accessibility, making the design more complex.

It may be unintuitive for users to be able to deconstruct but not construct the same record or union type.

# Alternatives

## Alternate syntax

An alternate syntax was proposed where the constructor accessibility would be specified after the type name and before the equal sign.

```fsharp
type R private () =
  { x: int }

// or:
type R private new =
  { x: int }
```

Advantages:
* More consistent with the location of constructor accessibility for classes.
* Doesn't look like we're _adding_ a new constructor to the type.

Drawbacks:
* (Especially the `()` variant) makes it look like the type is a class, rather than a record or union, when looking just at the first line.
* Located close to the type and representation accessibility specifiers. It can make it confusing to figure out which applies to what, especially when several specifiers are present:

  ```fsharp
  type public AccessibilitySoup private new =
    internal { x: int }
  ```

# Prior art

There is a C# proposal to provide an accessibility specifier for record constructors: https://github.com/dotnet/csharplang/discussions/6310

This proposed syntax is similar to F#'s class constructor accessibility specifier, and to the alternate syntax discussed above.


# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change?

  No.

* What happens when previous versions of the F# compiler encounter this design addition as source code?

  It will be a syntax error.

* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries?

  This must be checked: what does the current F# compiler do when it encounters a compiled record or union with (from its point of view) inconsistent accessibility specifications?

* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct?

  N/A

# Interop

* What happens when this feature is consumed by another .NET language?

  The accessibility is applied as specified in the detailed design to record constructors and union case construction static members.

* Are there any planned or proposed features for another .NET language (e.g., [C#](https://github.com/dotnet/csharplang)) that we would want this feature to interoperate with?

  * This would interoperate with a potential future ability to use C# record syntax to construct or update F# records.
 
  * [The C# proposal for unions](https://github.com/dotnet/csharplang/blob/main/proposals/nominal-type-unions.md) does not currently include constructor visibility.

# Pragmatics

## Diagnostics

A new compile-time error indicates that the constructor being called is not accessible.
No existing compile-time errors are quite appropriate, with the closest ones being:
* `error FS0801: This type has no accessible object constructors` (note: if the phrase "object constructors" is reworded to just "constructors", then this could be used)
* `error FS1092: The type 'X' is not accessible from this code location`
* `error FS1093: The union cases or fields of the type 'X' are not accessible from this code location`

## Tooling

* Auto-complete should not propose the fields of an inaccessible record type in a `{ ... }` construction syntax,
  nor the cases of an inaccessible union type in expression position.

* Error recovery may need to take into consideration the fact that the `new` keyword is allowed in a new context.

## Performance

The constructor accessibility does not directly affect the performance of the constructs it applies to.

It does however provide a more efficient alternative to the active pattern method of allowing pattern matching on unions (as shown in Motivation), since calls to multi-case active patterns allocate a value of type Choice.

## Scaling

At most one new widget per type declaration is allowed. No scaling issues are expected.

## Culture-aware formatting/parsing

N/A

# Unresolved questions

* As indicated in the detailed design, there is uncertainty about what should happen when the specified constructor accessibility is less restrictive than the specified representation accessibility.
  Should it be an error? A warning? If it is valid, which accessibility should be applied to construction constructs?

* As mentioned in Compatibility, what will happen if an older compiler encounters an assembly in which the constructor accessibility of a record or union has been specified?
