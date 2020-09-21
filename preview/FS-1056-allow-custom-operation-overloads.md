# F# RFC FS-1056 - Allow overloads of custom keywords in computation expressions


* [Approved in principle](https://github.com/fsharp/fslang-suggestions/issues/69#issuecomment-388558877)
* Implementation: [In Progress](https://github.com/Microsoft/visualfsharp/pull/4949)
* Discussion: https://github.com/fsharp/fslang-design/issues/301

# Summary
[summary]: #summary

When using a custom operation inside a computation expression, that operation can't be used if its method has an overload. That check is made only when the method is used inside a computation expression and can be [surprising when it happens](https://github.com/SaturnFramework/Saturn/issues/47).

# Motivation
[motivation]: #motivation

Computation expressions are useful to create domain specific languages. [Saturn](https://github.com/SaturnFramework/Saturn) is heavily based on them and is gaining a lot of traction as a web development framework which implements the server-side MVC pattern. As users get familiar when using CE-based APIs, more library developers can leverage that familiarity when designing a new library.

Enabling overloads can be used to hide the inner working of the API. In Saturn's case, the method `set_body` could be used to set the response body from a `byte array` or a `string`.

```fsharp    
    [<CustomOperation("set_body")>]
    member __.SetBody(state, value) : HttpHandler  = state >=> (setBody value)

    [<CustomOperation("set_body")>]
    member __.SetBody(state, value) : HttpHandler  = state >=> (setBodyFromString value)
```
Making the user use one keyword for `byte[]` and another for `string` would cause a worse experience.

Another unsupported feature that could be useful are optional arguments and arguments with the `[<ParamArray>]` attribute. That could enable computation expressions that are really expressive. With the following computation builder class:


```fsharp
type InputKind =
    | Text of placeholder:string option
    | Password of placeholder: string option

type InputOptions =
  { Label: string option
    Kind : InputKind
    Validators : (string -> bool) array }

type InputBuilder() =

    member t.Yield(_) = 
      { Label = None
        Kind = Text None
        Validators = [||] }
        
    [<CustomOperation("text")>]
    member this.Text(io,?placeholder) =
        { io with Kind = Text placeholder }
        
    [<CustomOperation("password")>]
    member this.Password(io,?placeholder) =
        { io with Kind = Password placeholder }
        
    [<CustomOperation("label")>]
    member this.Label(io,label) = 
        { io with Label = Some label }
        
    [<CustomOperation("with_validators")>]
    member this.Validators(io, [<ParamArray>] validators) =
        { io with Validators = validators }
    
let input = InputBuilder()

```

you could create inputs with the following code:

```fsharp
let name =
  input {
    label "Name"
    text
    with_validators
        (String.IsNullOrWhiteSpace >> not)
  }
        
let email =
  input {
    label "Email"
    text "Your email"
    with_validators
        (String.IsNullOrWhiteSpace >> not)
        (fun s -> s.Contains "@")
  }
        
let password =
  input {
    label "Password"
    password "Must contains at least 6 characters, one number and one uppercase"
    with_validators
        (String.exists Char.IsUpper)
        (String.exists Char.IsDigit)
        (fun s -> s.Length >= 6)
  }
```


# Detailed design
[design]: #detailed-design

At the time, preventing the overloading of the methods was a design choice to improve the code clarity.

After removing the protections that prevents the compilation of computation expression a custom operation is called, the state and arguments are tupled and passed to the method. When using the following builder:

```fsharp
type Builder() =
    member __.Yield(_) = ()
    
    [<CustomOperation("method")>]
    member __.Method(state, ...arguments) = ...

let builder = Builder()
```

with the code: 
```fsharp
builder {method a b c d e f}
```

That is essentially the same as calling:
```fsharp
builder.Method((builder.Yield()), a, b, c, d, e, f)
```

The same rules as calling this code will apply for the computation expressions.

The restriction of having a 1:1 match on the method and keyword names for the overloads is kept as it keeps the intent clear. Howevere the overloads don't need to be marked with `[<CustomOperation>]` again.


# Drawbacks
[drawbacks]: #drawbacks

Error messages gets more generic as it will give the same error as it would when calling a non-existing method if the custom operation don't match any overload.

# Alternatives
[alternatives]: #alternatives

The main alternative is not doing this at all.


# Compatibility
[compatibility]: #compatibility

This is not a breaking change.

# Unresolved questions
[unresolved]: #unresolved-questions

None
