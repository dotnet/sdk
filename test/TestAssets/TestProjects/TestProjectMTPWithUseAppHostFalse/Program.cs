using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;

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

	public Type[] DataTypesProduced { get; } = [typeof(TestNodeUpdateMessage)];

	public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
		=> Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

	public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
		=> Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

	public async Task ExecuteRequestAsync(ExecuteRequestContext context)
	{
		// Simple dummy test that always passes
		await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
			context.Request.Session.SessionUid,
			new TestNode()
			{
				Uid = "dummy_test_1",
				DisplayName = "Dummy Test 1",
				Properties = new PropertyBag(PassedTestNodeStateProperty.CachedInstance)
			}));

		context.Complete();
	}
}
