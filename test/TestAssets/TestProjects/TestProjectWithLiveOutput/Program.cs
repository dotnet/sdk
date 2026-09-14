using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

var testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);

testApplicationBuilder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, serviceProvider) => new DummyTestAdapter(serviceProvider.GetOutputDevice()));

using var testApplication = await testApplicationBuilder.BuildAsync();
return await testApplication.RunAsync();

public class DummyTestAdapter(IOutputDevice outputDevice) : ITestFramework, IDataProducer, IOutputDeviceDataProducer
{
	// Set by the test to a path that does not exist yet. The test creates the file as soon as it
	// observes LIVE_OUTPUT_STANDARD_OUTPUT on the standard output of 'dotnet test' while the
	// command is still running, so this app can only see the file appear if its own console output
	// really was forwarded live rather than buffered until it exits.
	private const string SentinelPathEnvironmentVariable = "LIVE_OUTPUT_SENTINEL_PATH";

	private static readonly TimeSpan SentinelTimeout = TimeSpan.FromSeconds(60);

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
		Console.WriteLine("LIVE_OUTPUT_STANDARD_OUTPUT");
		Console.Error.WriteLine("LIVE_OUTPUT_STANDARD_ERROR");

		// Text written through the platform's output device does not reach the console directly:
		// it is forwarded to 'dotnet test' over the pipe protocol and rendered by its reporter.
		// Only durable session messages, warnings and errors cross the wire - plain informational
		// text is deliberately discarded by the host under the pipe protocol.
		await outputDevice.DisplayAsync(this, new SessionMessageOutputDeviceData("LIVE_OUTPUT_SESSION_MESSAGE"), context.CancellationToken);
		await outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData("LIVE_OUTPUT_WARNING_MESSAGE"), context.CancellationToken);
		await outputDevice.DisplayAsync(this, new ErrorMessageOutputDeviceData("LIVE_OUTPUT_ERROR_MESSAGE"), context.CancellationToken);

		string? failureReason = await WaitForOutputToBeObservedAsync();

		await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
		{
			Uid = "Test0",
			DisplayName = "Test0",
			Properties = new PropertyBag(failureReason is null
				? new PassedTestNodeStateProperty("OK")
				: new FailedTestNodeStateProperty(failureReason)),
		}));

		context.Complete();
	}

	private static async Task<string?> WaitForOutputToBeObservedAsync()
	{
		string? sentinelPath = Environment.GetEnvironmentVariable(SentinelPathEnvironmentVariable);
		if (string.IsNullOrEmpty(sentinelPath))
		{
			return $"{SentinelPathEnvironmentVariable} is not set.";
		}

		DateTime deadline = DateTime.UtcNow + SentinelTimeout;
		while (DateTime.UtcNow < deadline)
		{
			if (File.Exists(sentinelPath))
			{
				return null;
			}

			await Task.Delay(50);
		}

		return $"The standard output of this test app was not observed within {SentinelTimeout.TotalSeconds} seconds while it was still running, so it was not forwarded live.";
	}
}
