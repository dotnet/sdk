Module Program

    Function Main(args As String()) As Integer
        Return MainAsync(args).GetAwaiter().GetResult()
    End Function

    Public Async Function MainAsync(args As String()) As Global.System.Threading.Tasks.Task(Of Integer)
        Dim builder = Await Global.Microsoft.Testing.Platform.Builder.TestApplication.CreateBuilderAsync(args)
        SelfRegisteredExtensions.AddSelfRegisteredExtensions(builder, args)
        Using app = Await builder.BuildAsync()
            Return Await app.RunAsync()
        End Using
    End Function

End Module
