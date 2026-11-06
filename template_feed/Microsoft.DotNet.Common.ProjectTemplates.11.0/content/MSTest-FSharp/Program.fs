namespace Company.TestProject1

open Microsoft.Testing.Platform.Builder

module Program =

    [<EntryPoint>]
    let main args =
        task {
            let! builder = TestApplication.CreateBuilderAsync(args)
            SelfRegisteredExtensions.AddSelfRegisteredExtensions(builder, args)
            use! app = builder.BuildAsync()
            return! app.RunAsync()
        }
        |> Async.AwaitTask
        |> Async.RunSynchronously
