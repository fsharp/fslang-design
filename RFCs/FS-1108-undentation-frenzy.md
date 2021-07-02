# F# RFC FS-1108 - Undentation frenzy

The design suggestions
- [Allow undentation for constructors](https://github.com/fsharp/fslang-suggestions/issues/724)
- [Relax indentation rules on mutually recursive functions when using with CE](https://github.com/fsharp/fslang-suggestions/issues/868)

have been marked "approved in principle", while
- [Add new undentation rule for match with](https://github.com/fsharp/fslang-suggestions/issues/833)
- [Relax indentation rules on anonymous records](https://github.com/fsharp/fslang-suggestions/issues/786)
- [Allow undentation in method and constructor calls](https://github.com/fsharp/fslang-suggestions/issues/931)
- [Alignment requirements for mutually recursive functions are non-intuitive](https://github.com/fsharp/fslang-suggestions/issues/601)
- [Improve Indentation Rules for Generic Signatures](https://github.com/fsharp/fslang-suggestions/issues/504)
- [Mutual Recursion with Async Workflow Possible Compiler Issue](https://github.com/dotnet/fsharp/issues/3326)
- [Composed function leads to unexpected indentation warning](https://github.com/dotnet/fsharp/issues/10852)
- [Warning only when function application is inside SynExpr.IfThenElse](https://github.com/dotnet/fsharp/issues/10929)
- [Relax some of the indentation rules](https://github.com/fsharp/fslang-suggestions/issues/470) (itself a superset of the following 2 suggestions)
- [Indentation of new lines should be dependent on the start of the previous line, not the end of it](https://github.com/fsharp/fslang-suggestions/issues/433)
- [Relax indentation rules on Records](https://github.com/fsharp/fslang-suggestions/issues/130)

are yet to be approved-in-principle. However, [as @catermp said](https://github.com/fsharp/fslang-suggestions/issues/786#issuecomment-533809188),
> I agree with this since we've made it a precedent to relax indentation like this in the past few releases.

This RFC covers the detailed proposal for all above suggestions.

- [x] Suggestion
- [ ] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/11772)
- [ ] Design Review Meeting(s) with @dsyme and others invitees
- [Discussion](https://github.com/fsharp/fslang-design/discussions/602)

# Summary

Allow undentation (i.e. relaxations to the indentation rules) such that all above suggestions no longer produce the warning `FS0058: Possible incorrect indentation`, and be able to compile without any warnings or errors.

# Motivation

Currently, the F# indentation rules are very inconsistent.
```fs
let a = id [
    1 // No longer produces warning FS0058 after [RFC FS-1054] less strict indentation on common DSL pattern
]
let b = id (
    1 // warning FS0058: Possible incorrect indentation
)
let c = (
    1 // But this is ok
)
```
```fs
try
    1
with _ -> 2 // Totally fine
|> ignore

match
    1
with _ -> 2 // error FS0010: Unexpected start of structured construct in expression
|> ignore
```
```fs
if true then [ 0 ] else [
    1 // This is fine
]
|> ignore
if true then <@ 0 @> else <@
    1 // warning FS0058: Possible incorrect indentation
@>
|> ignore
```
```fs
let d =
    if [
        2 // Fine
       ] = [3] then ()
let e =
    if [3] = [
        2 // warning FS0058: Possible incorrect indentation
       ] then ()
```
```fs
module rec M1 =
    let messageLoop (state:int) = async {
        return! someOther(state)
    } // Sure
    let someOther(state:int) = async {
        return! messageLoop(state)
    }
module M2 =
    let rec messageLoop (state:int) = async {
        return! someOther(state)
    }
    // error FS0010: Unexpected keyword 'and' in definition. Expected incomplete structured construct at or before this point or other token.
    and someOther(state:int) = async {
        return! messageLoop(state)
    }
```
```fs
type R = { a : int }
let f = {
    a = 1 // Ok
}
let {
    a = g
} = f // error FS0010: Incomplete structured construct at or before this point in binding. Expected '=' or other token.


type [<AutoOpen>] H = static member h a = ()
let _ = h { a =
    2 // Ok
}
let _ = h ( a =
    2 // Also ok
)
let _ = h {| a =
    2 // warning FS0058: Possible incorrect indentation
|}
```
```fs
let i =
    if true then ignore [
        1 // Ok
    ] else ignore [ 2 ]
let j =
    if true then ignore [ 1 ] else ignore [
        2 // Ok
    ]
let k =
    let tru _ = true
    if tru [
        1 // warning FS0058: Possible incorrect indentation
    ] then ()
```

We should get rid of these inconsistencies and allow all above cases to compile without any errors or warnings.

# Detailed design

1.
```fs
    match
        ...
    with
    | ...
```
will be an allowed undentation, with rules the same as `try .. with`.

2. The left bracket of the 9 pairs of expression brackets
   - `begin`/`end`
   - `(`/`)`
   - `[`/`]`
   - `{`/`}`
   - `[|`/`|]`
   - `{|`/`|}`
   - `<@`/`@>`
   - `<@@`/`@@>`
   - `Foo<`/`>`
   
   will not impose an additional indentation limit to the following line.
   As a side effect, this will also remove the warning of
   ```fs
   type R = { a : int }
   let _ = { a =
       2 // warning FS0058: Possible incorrect indentation
   }
   ```

3. An offside rule will be introduced for the right bracket of the 9 pairs of aforementioned brackets where an incomplete
   construct error will no longer be raised when the bracket is on the same column of the offside context.
   ```fs
   let {
       a = 2
   } = { a = 2 } // The } will no longer raise error FS0010: Incomplete structured construct at or before this point in binding. Expected '=' or other token.
   ```
   
As a result, the rules of [[RFC FS-1054] Less strict indentation on common DSL pattern](https://github.com/fsharp/fslang-design/blob/main/FSharp-4.5/FS-1054-undent-list-args.md), the rules of F# 2.0-4.1 allowing undentation for expressions delimited by { ... }, as well as 2 of the 3 rules added in [[FS-1070] Offside relaxations for construct and member definitions](https://github.com/fsharp/fslang-design/blob/main/FSharp-4.7/FS-1070-offside-relaxations.md) will no longer be reached when this feature is enabled.
Moreover, this RFC is also a strict superset of [[FS-1078] Offside relaxations for functions](https://github.com/fsharp/fslang-design/blob/main/RFCs/FS-1078-offside-relaxations-functions.md), which means that the implementation of this RFC will implicitly implement that RFC as well.

# Drawbacks

As with [RFC FS-1054] less strict indentation on common DSL pattern, undentations must be applied with care as they can cause code to be written that is less readable. However, since the undentation is already allowed for numerous cases listed under Motivation it seems reasonable to get uniformity here.

# Alternatives

- Don't do this. Keep dealing with annoying indentation errors.
- As with [RFC FS-1054] Less strict indentation on common DSL pattern, [FS-1070] Offside relaxations for construct and member definitions, and [FS-1078] Offside relaxations for functions, we can add special cases as they come up. However, the end result is a lot of inconsistency and unecessary special indentation rules, as evidenced in the Motivation section above as a result of FS-1054 and FS-1070 done separately without considering the general case. Moreover, even when we implement each of the suggestions separately, new inconsistent indentation warnings can still come up, such as
```fs
for x in seq {
            1 // warning FS0058: Possible incorrect indentation
         } do printfn "%d" x
```
which will be inconsistent with
```fs
if seq {
      true // warning FS0058: Possible incorrect indentation, to be removed with "Warning only when function application is inside SynExpr.IfThenElse"
   } |> Seq.head then ()
```
once only the design suggestions mentioned at the top of this document are implemented.

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change? No
* What happens when previous versions of the F# compiler encounter this design addition as source code? Error or warn as before.
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries? This is only a syntactical feature.
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct? This is not a change to FSharp.Core.

# Unresolved questions

None.
