using System.Reflection;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;

// Drop a uniquely-named marker file per process launch so tests can deterministically count how many
// times this test application actually ran (used to validate traversal de-duplication).
var markerDir = Environment.GetEnvironmentVariable("TRAVERSAL_MARKER_DIR");
if (!string.IsNullOrEmpty(markerDir))
{
	Directory.CreateDirectory(markerDir);
	var assemblyName = Assembly.GetEntryAssembly()?.GetName().Name ?? "unknown";
	File.WriteAllText(Path.Combine(markerDir, $"{assemblyName}-{Guid.NewGuid():N}.marker"), assemblyName);
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

	public Type[] DataTypesProduced => [];

	public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
		=> Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

	public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
		=> Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

	public async Task ExecuteRequestAsync(ExecuteRequestContext context)
	{
		context.Complete();
		await Task.CompletedTask;
	}
}
