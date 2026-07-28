module FSharpWebAppWithLib.Program

open System
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Hosting

let responseText () = FSharpWebAppWithLib.Lib.message ()

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    let app = builder.Build()

    app.MapGet("/", Func<string>(fun () -> responseText ())) |> ignore

    app.Run()
    0
