﻿using Microsoft.Testing.Platform.Builder;
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

		await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
		{
			Uid = "Test1",
			DisplayName = "Test1",
			Properties = new PropertyBag(new SkippedTestNodeStateProperty("Skipped!")),
		}));

		await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
		{
			Uid = "Test2",
			DisplayName = "Test2",
			Properties = new PropertyBag(new FailedTestNodeStateProperty(new Exception("this is a failed test"), "")),
		}));

		await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
		{
			Uid = "Test3",
			DisplayName = "Test3",
			Properties = new PropertyBag(new TimeoutTestNodeStateProperty(new Exception("this is a timeout exception"), "")),
		}));

		await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
		{
			Uid = "Test4",
			DisplayName = "Test4",
			Properties = new PropertyBag(new ErrorTestNodeStateProperty(new Exception("this is an exception"), "")),
		}));

		await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode()
		{
			Uid = "Test5",
			DisplayName = "Test5",
			Properties = new PropertyBag(new CancelledTestNodeStateProperty(new Exception("this is a cancelled exception"), "")),
		}));
		
		context.Complete();
		}
	}
}
