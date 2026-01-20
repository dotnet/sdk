using System.Diagnostics;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestFramework;

if (args.Length == 1 && args[0] == "hang")
{
    var @event = new ManualResetEvent(false);
    @event.WaitOne();
    return 0;
}

var builder = await TestApplication.CreateBuilderAsync(args);
builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, _) => new MyTestFramework());
using var testApp = await builder.BuildAsync();
return await testApp.RunAsync();

internal class MyTestFramework : ITestFramework
{
    public string Uid => nameof(MyTestFramework);
    public string Version => "1.0.0";
    public string DisplayName => nameof(MyTestFramework);
    public string Description => DisplayName;
    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
    {
        return Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });
    }
    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });
    public Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        var fileName = Process.GetCurrentProcess().MainModule.FileName;
        var p = Process.Start(new ProcessStartInfo(fileName, "hang")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        });
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        p.ErrorDataReceived += (sender, e) => { };
        p.OutputDataReceived += (sender, e) => { };
        context.Complete();
        return Task.CompletedTask;
    }
    public Task<bool> IsEnabledAsync()
        => Task.FromResult(true);
}
