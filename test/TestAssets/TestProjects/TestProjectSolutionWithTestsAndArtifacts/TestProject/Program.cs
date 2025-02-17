//See https://aka.ms/new-console-template for more information

//Opt -out telemetry

using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;

Environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1");

var testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);

testApplicationBuilder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, __) => new DummyTestAdapter());

// Enable Trx
testApplicationBuilder.AddTrxReportProvider();
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
		typeof(TestNodeUpdateMessage),
		typeof(SessionFileArtifact),
		typeof(TestNodeFileArtifact),
		typeof(FileArtifact), };

	public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
		=> Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

	public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
		=> Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

	public async Task ExecuteRequestAsync(ExecuteRequestContext context)
	{
		await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
		{
			Uid = "Test1",
			DisplayName = "Test1",
			Properties = new PropertyBag(new PassedTestNodeStateProperty("OK")),
		}));

		await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
		{
			Uid = "Test1",
			DisplayName = "Test1",
			Properties = new PropertyBag(new SkippedTestNodeStateProperty("OK skipped!")),
		}));

		await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
		{
			Uid = "Test2",
			DisplayName = "Test2",
			Properties = new PropertyBag(new FailedTestNodeStateProperty(new Exception("this is a failed test"), "not OK")),
		}));

		await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
		{
			Uid = "Test3",
			DisplayName = "Test3",
			Properties = new PropertyBag(new TimeoutTestNodeStateProperty(new Exception("this is a timeout exception"), "not OK")),
		}));

		await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
		{
			Uid = "Tes43",
			DisplayName = "Test4",
			Properties = new PropertyBag(new ErrorTestNodeStateProperty(new Exception("this is an exception"), "not OK")),
		}));

		await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
		{
			Uid = "Test5",
			DisplayName = "Test5",
			Properties = new PropertyBag(new CancelledTestNodeStateProperty(new Exception("this is a cancelled exception"), "not OK")),
		}));

		await context.MessageBus.PublishAsync(this, new FileArtifact(new FileInfo("file.txt"), "file", "file description"));

		await context.MessageBus.PublishAsync(this, new SessionFileArtifact(context.Request.Session.SessionUid, new FileInfo("sessionFile.txt"), "sessionFile", "description"));

		await context.MessageBus.PublishAsync(this, new TestNodeFileArtifact(context.Request.Session.SessionUid, new TestNode()
		{
			Uid = "Test6 id",
			DisplayName = "Test6",
			Properties = new PropertyBag(new PassedTestNodeStateProperty("OK")),
		}, new FileInfo("testNodeFile.txt"), "testNodeFile", "description"));

		context.Complete();
	}
}