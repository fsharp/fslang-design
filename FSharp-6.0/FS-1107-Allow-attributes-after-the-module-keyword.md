# F# RFC FS-1107 - Allow attributes after the module keyword

The design suggestion [Allow attributes after the module keyword](https://github.com/fsharp/fslang-suggestions/issues/757) has been marked "approved in principle".

This RFC covers the detailed proposal for this suggestion.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/757)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/11722)
- [x] ~~Design Review Meeting(s) with @dsyme and others invitees~~ Not needed
- [Discussion](https://github.com/fsharp/fslang-design/discussions/600)

# Summary

Attributes can now be placed after the `module` keyword.

# Motivation

Consider:
```fs
[<AutoOpen>]
module M =
    [<AbstractClass>]
    type C() = class end
    [<Literal>]
    let X = 3
```
The `Literal` attribute can be embedded into the `let`:
```fs
[<AutoOpen>]
module M =
    [<AbstractClass>]
    type C() = class end
    let [<Literal>] X = 3 // ✓
```
Same for the `AbstractClass`:
```fs
[<AutoOpen>]
module M =
    type [<AbstractClass>] C() = class end // ✓
    let [<Literal>] X = 3
```
But not the `AutoOpen`.
```fs
module [<AutoOpen>] M = // FS0010: Unexpected start of structured construct in definition. Expected identifier, 'global' or other token.
    type [<AbstractClass>] C() = class end // FS0010: Unexpected keyword 'type' in implementation file
    let [<Literal>] X = 3
```

Therefore, it is inconsistent that attributes can be placed after the `type` and `let` keywords, but not the `module` keyword.

The existing way of approaching this problem in F# is leaving the `AutoOpen` above the `module`, or leave it before the `module` keyword but have indentation warnings on the lines after:

```fsharp
[<AutoOpen>] 
module M =      
     [<AbstractClass>] type C() = class end
     [<AbstractClass>] type D() = class end  // no indentation warning
     [<AbstractClass>] type E() = class end  // no indentation warning
     [<Literal>] let X = 3 
     [<Literal>] let Y = 3    // indentation warning
     [<Literal>] let Z = 3    // indentation warning
```

![image](https://user-images.githubusercontent.com/16015770/71782978-bd1a0400-2fe0-11ea-8031-da0c78895702.png)
 
In the case of `module`, the indentation rules consider the position _after_ the attribute(s), which in turn would mean everything needs to be indented to the right. Not an ideal workaround ;).

# Detailed design

For both `type`s and `let`s, attributes can be placed right before the accessibility modifier.
```fs
module private rec M =
    type [<Experimental "">] private Hello() = do ()
    let rec [<Experimental "">] private f() = ()
```

This will be followed.
```
moduleIntro: 
  | moduleKeyword opt_access opt_rec path 
```
will be changed to
```
moduleIntro: 
  | moduleKeyword opt_attributes opt_access opt_rec path
```
.

The end effect is that `[<AttributeA>] module [<AttributeB>] M = do ()` will be equivalent to `[<AttributeA>] [<AttributeB>] module M = do ()` or `module [<AttributeA>] [<AttributeB>] M = do ()`.

# Drawbacks

[@charlesroddie said:](https://github.com/fsharp/fslang-suggestions/issues/757#issuecomment-505128388)
> The [style guide](https://docs.microsoft.com/en-us/dotnet/fsharp/style-guide/formatting) suggests "place the attribute on its own line" (for `[<Literal>]`s but this naturally extends to other attributes). Consistency is good here.

However, [@cartermp said:](https://github.com/fsharp/fslang-suggestions/issues/757#issuecomment-505861069)
> @charlesroddie although the style guide does mention that, at the end of the day it's only style. I think having consistency in the language is key here; we want people to adopt that guide not because they have to, but because they agree with its contents.

# Alternatives

Not doing this will continue having design inconsistencies as well as unnecessary splitting of one-line module definitions just to insert an attribute, if avoiding indentation warnings.

# Compatibility

Please address all necessary compatibility questions:

* Is this a breaking change? No.
* What happens when previous versions of the F# compiler encounter this design addition as source code? Error as usual.
* What happens when previous versions of the F# compiler encounter this design addition in compiled binaries? Run as usual.
* If this is a change or extension to FSharp.Core, what happens when previous versions of the F# compiler encounter this construct? This is not a change to FSharp.Core.

# Unresolved questions

None.
