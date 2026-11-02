using System.Diagnostics;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;

namespace TestProjectWithNetFM
{
	internal class Program
	{
		public static async Task<int> Main(string[] args)
		{
			// To attach to the children
			ITestApplicationBuilder testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);
			testApplicationBuilder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, __) => new DummyTestAdapter());

			ITestApplication testApplication = await testApplicationBuilder.BuildAsync();
			return await testApplication.RunAsync();
		}
	}

	public class DummyTestAdapter : ITestFramework, IDataProducer
	{
		public string Uid => nameof(DummyTestAdapter);

		public string Version => "2.0.0";

		public string DisplayName => nameof(DummyTestAdapter);

		public string Description => nameof(DummyTestAdapter);

		public Task<bool> IsEnabledAsync() => Task.FromResult(true);

		public Type[] DataTypesProduced => new[]
		{
			typeof(TestNodeUpdateMessage)
		};

		public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
			=> Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

		public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
			=> Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

		public async Task ExecuteRequestAsync(ExecuteRequestContext context)
		{
			var currentProcess = Process.GetCurrentProcess();
			var processPath = currentProcess.MainModule?.FileName;
			if (processPath is null)
			{
				await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
				{
					Uid = "Test0",
					DisplayName = "Test0",
					Properties = new PropertyBag(new FailedTestNodeStateProperty(new Exception("Process path is null"), "")),
				}));

				context.Complete();
				return;
			}

			await Task.Delay(5000);
			
			var processes = Process.GetProcessesByName(currentProcess.ProcessName);
			var pathPrefix = Path.GetDirectoryName(Path.GetDirectoryName(processPath));
			if (processes.Where(p => p.Id != Process.GetCurrentProcess().Id && p.MainModule is not null && p.MainModule.FileName.StartsWith(pathPrefix!)).Any())
			{
                await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
                {
                    Uid = "Test0",
                    DisplayName = "Test0",
                    Properties = new PropertyBag(new FailedTestNodeStateProperty(new Exception("This is run in parallel!"), "")),
                }));
			}
			else
			{
                await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
                {
                    Uid = "Test0",
                    DisplayName = "Test0",
                    Properties = new PropertyBag(new PassedTestNodeStateProperty("OK")),
                }));
            }

			await Task.Delay(5000);

			context.Complete();
		}
	}
}
