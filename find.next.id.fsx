open System.IO
open System

let nextId  =
  DirectoryInfo(__SOURCE_DIRECTORY__).GetFiles("*.md", SearchOption.AllDirectories)
  |> Seq.map (fun f -> f.Name.ToLowerInvariant().Split([|"fst-";"fs-"; "-"|], StringSplitOptions.RemoveEmptyEntries))
  |> Seq.choose (function
    | [||]
    | [|_|] -> None
    | items -> 
      items 
      |> Array.map (fun v -> Int32.TryParse(v)) 
      |> Array.filter fst 
      |> Array.map snd 
      |> Array.tryHead
  )
  |> Seq.max 
  |> ((+) 1)

printfn $"next RFC ID: %04i{nextId}"