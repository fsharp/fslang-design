# F# RFC FS-1111 - Reference cell operation advisory messages

The design suggestion [Give advisory messages when using (!) and := and ask users to use .Value instead](https://github.com/fsharp/fslang-suggestions/issues/569) has been approved in principle.
This RFC covers the detailed proposal.

- [x] [Suggestion](https://github.com/fsharp/fslang-suggestions/issues/569)
- [x] Approved in principle
- [x] [Implementation](https://github.com/dotnet/fsharp/pull/11900)
- [ ] Design Review Meeting(s) with @dsyme and others invitees
- [ ] [Discussion](https://github.com/fsharp/fslang-design/discussions/614)

# Summary

Give advisory messages on the use of `!`, `:=`, `incr` and `decr` from the F# standard library, linking people to a guidance page
and suggesting to change to use `cell.Value` operations.

# Background

Since F# 0.1, F# has had `:=`, `!`, `incr`, `decr` operations for mutable heap-allocated reference cells.

As of F# 4.0, ``let mutable x = ...`` supports automatic promotion to a reference cell if the ``x`` is captured in a closure. As a result,
the explicit use of ``ref``  cells is now far less common for F# programming, so much so that, in retrospect, we would never have given them such high
presence in the FSharp.Core library design. Additionally, reference cells are objects (actually records), and using them should use object notation.

Because of this, we can now gently give advisory messages when using these.

    let (!) (r: 'T ref)  = r.Value
    let (:=) (r: 'T ref) (v: 'T)  = r.Value <- v
    let incr (r: int ref)  = r.Value <- r.Value + 1
    let decr (r: int ref)  = r.Value <- r.Value - 1

and instead ask people to use ``r.Value`` instead

This has two advantages

* for programmers coming from most other languages, ``!`` already has a very specific meaning - usually boolean negation.  For these
  people - and arguably others - ``r.Value`` is clearer
  
* it reduces the number of operators necessary to learn in F# coding

* in the very, very long-term we could consider giving ``!x`` the usual meaning as an overloaded operator suitable for using with boolean values.

# Detailed Design

When `:=`, `!`, `incr`, `decr` are encountered an information deprecation warning is emitted.

# Compatibility

This is a backwards compatible change. Informational warnings are not errors even if `--warnaserror` is on, unless the explicit error number
is selected.

The informational message is sufficient to gradually advise all users that these constructs are considered deprecated.

# Actions

Users encountering these informational warnings can

1. Convert their code to use a `let mutable`, appropriately scoped. For example

   ```fsharp
   let f () =
       let cell = ref 3
       for i in 0..10 do
           incr cell
           printfn "cell = %A" !cell
           cell := !cell + 5
           printfn "cell = %A" !cell
           decr cell
   ```
   
   can become

   ```fsharp
   let f () =
       let mutable cell = 3
       for i in 0..10 do
           cell <- cell + 1
           printfn "cell = %A" cell
           cell <- cell + 5
           printfn "cell = %A" cell
           cell <- cell - 1
   ```
   
2. Convert their code to use suggested forms

   ```fsharp
      !cell          --->     cell.Value
      cell := expr   --->     cell.Value <- expr
      incr cell      --->     cell.Value <- cell.Value + 1
      decr cell      --->     cell.Value <- cell.Value - 1
   ```

3. OR add the following code to their project

   ```fsharp
   let (!) (r: 'T ref)  = r.Value
   let (:=) (r: 'T ref) (v: 'T)  = r.Value <- v
   let incr (r: int ref)  = r.Value <- r.Value + 1
   let decr (r: int ref)  = r.Value <- r.Value - 1
   ```

4. OR suppress the informational warning through `--nowarn:3370`


# Unresolved questions

None


