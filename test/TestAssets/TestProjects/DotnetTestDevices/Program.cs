// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;

Console.WriteLine(
    $"Runtime environment variables: FOO={Environment.GetEnvironmentVariable("FOO")}, INJECTED={Environment.GetEnvironmentVariable("INJECTED")}");

int transportOptionIndex = Array.IndexOf(args, "--dotnet-test-transport");
bool httpTransportSelected = transportOptionIndex >= 0 &&
    transportOptionIndex + 1 < args.Length &&
    string.Equals(args[transportOptionIndex + 1], "http", StringComparison.OrdinalIgnoreCase);
if (!httpTransportSelected)
{
    string? responseFileArgument = args.FirstOrDefault(static arg => arg.StartsWith('@'));
    if (responseFileArgument is not null && File.Exists(responseFileArgument[1..]))
    {
        httpTransportSelected = File.ReadLines(responseFileArgument[1..])
            .Any(static line => line.Equals("--dotnet-test-transport http", StringComparison.OrdinalIgnoreCase));
    }
}

if (httpTransportSelected)
{
    Console.WriteLine("HTTP transport selected.");
}

var testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);

testApplicationBuilder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, __) => new DummyTestAdapter());

using var testApplication = await testApplicationBuilder.BuildAsync();
return await testApplication.RunAsync();

public class DummyTestAdapter : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyTestAdapter);

    public string Version => "2.0.0";

    public string DisplayName => nameof(DummyTestAdapter);

    public string Description => nameof(DummyTestAdapter);

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Type[] DataTypesProduced => new[] {
        typeof(TestNodeUpdateMessage)
    };

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
        {
            Uid = "Test0",
            DisplayName = "Test0",
            Properties = new PropertyBag(new PassedTestNodeStateProperty("OK")),
        }));

        context.Complete();
    }
}
