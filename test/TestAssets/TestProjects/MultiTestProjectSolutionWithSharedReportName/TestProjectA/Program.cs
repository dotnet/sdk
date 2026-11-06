using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Services;

var testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);

testApplicationBuilder.RegisterTestFramework(
	_ => new TestFrameworkCapabilities(),
	(_, serviceProvider) => new DummyTestAdapter(serviceProvider));

using var testApplication = await testApplicationBuilder.BuildAsync();
return await testApplication.RunAsync();

public class DummyTestAdapter(IServiceProvider serviceProvider) : ITestFramework, IDataProducer
{
	// Every project in this solution writes this same relative file name into its test results
	// directory, the way a coverage or TRX report with a relative path does. With a shared results
	// directory the projects overwrite each other; with a per-module layout both reports survive.
	private const string ReportFileName = "report.txt";

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
		string resultDirectory = serviceProvider.GetConfiguration().GetTestResultDirectory();
		Directory.CreateDirectory(resultDirectory);
		File.WriteAllText(Path.Combine(resultDirectory, ReportFileName), "TestProjectA");

		await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
		{
			Uid = "Test1",
			DisplayName = "Test1",
			Properties = new PropertyBag(new PassedTestNodeStateProperty("OK")),
		}));

		context.Complete();
	}
}
