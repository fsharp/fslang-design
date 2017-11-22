
# RFC FS-1040: Bug Fix for compiling matches against enums, now throws FS3191 

In F# 4.1 a bug fix was made that means code may no longer compile. Because this was a breaking change to some code this "RFC" documents this bug-fix post-hoc.

Reason: pattern matching against enums cannot take a variable anymore.

### Repro steps

This may illustrate the issue:

```f#
module TestFS3191 =
    type MyEnum = 
        | Value1 = 1
        | Value2 = 2
        | Value3 = 3

    let testEnum =
        let foo = MyEnum.Value1

        match foo with
        | MyEnum.Value1 foo -> foo   // error in VS2017, no error in VS2015
        | MyEnum.Value2 foo -> foo
        | MyEnum.Value3 foo -> foo
```

### F# 4.0 behavior

Compiles


### F# 4.1 behavior


> error FS3191: This literal pattern does not take arguments

### Known workarounds

Update the code. Presumable, the coder wanted `as foo`, or didn't intend to use `foo` in that location in the first place and it got there. I noticed that it gets ignored by the compiler. If you try this:

```f#
        match foo with
        | MyEnum.Value1 a-> a   // error on second "a", it says it isn't defined
```

### Mea culpa

@dsyme says:

> This was a bug fix. The code should never have been accepted. We could have made it a warning but chose to just make the fix instead. Given the time that has passed I'd imagine we'll leave it like it is now.


