

## F# 4.0 Speclet: Extension Property Initializers

[F# Language User Voice Entry](http://fslang.uservoice.com/forums/245727-f-language/suggestions/6200089-allow-extension-properties-setters-in-initializers), [Pull Request](https://github.com/Microsoft/visualfsharp/pull/17), [Commit](https://github.com/Microsoft/visualfsharp/commit/d9348ec64eec7b6b9907cca8deb6d02c6cdb9af9)

Thanks to [Edward Paul (@xepaul)](https://github.com/Microsoft/visualfsharp/commits/fsharp4?author=xepaul) for suggesting and contributing the implementation for this feature.

### Aim

Allow extension property setters to be used in the initializer syntax at constructor and method calls

### Background

F# 2.0+ has always supported both extension properties and "initializer syntax" on method calls, where 
named properties are given values at object constructor and method calls.

However, setting an _extension_ property in an initializer has not been allowed.  This is a needless feature interaction limitation
and the combination should be allowed simply for the sake of language hygiene.  The combination is also useful in practice.

 
### Design

When analyzing an unassigned named item _name_ = _expr_ in a constructor or method call with return type _rty_,
first look at the intrinsic properties of _rty_, then look at the extension properties of _rty_ in scope at the
point off the call.


### Specification

In the F# specification, the section "14.4. Method Application Resolution" currently reads:

> If the return type of _M_, before the application of any type arguments _ActualTypeArgs_, contains a settable property _name_, then _M_ is the target.

(The last mention of _M_ on this line in the F# 3.1 spec is a mistake - it should say _name_).

We adjust the text to clarify that the available settable properties include
the property extension members of type, found by consulting the ExtensionsInScope table, as discussed in section 14.1.5.

### Examples

For example 

    C(ExtensionProperty = [1;2;3]) 

Or for example to provide C# “add” pattern like support 

    type StackLayout with 
        member x.Kids with set (values : UIElement seq) = values |> Seq.iter x.Children.Add 

    let layout = StackLayout (Orientation = StackOrientation.Horizontal, 
                              Kids = [Entry(Placeholder = "Username");Entry()]) 

