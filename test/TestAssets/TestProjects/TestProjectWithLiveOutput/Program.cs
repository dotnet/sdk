using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;

// Written before the test application is built, i.e. before the pipe handshake with the SDK
// completes. It exercises the "buffer until the protocol version is negotiated, then flush" path.
Console.WriteLine("LIVE_OUTPUT_BEFORE_HANDSHAKE");

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
		// Written while the test session is running, so it must be streamed to the SDK terminal
		// as it is produced instead of being buffered and only shown when something fails.
		Console.WriteLine("LIVE_OUTPUT_STANDARD_OUTPUT");
		Console.Error.WriteLine("LIVE_OUTPUT_STANDARD_ERROR");

		await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
		{
			Uid = "Test0",
			DisplayName = "Test0",
			Properties = new PropertyBag(new PassedTestNodeStateProperty("OK")),
		}));

		context.Complete();
	}
}
