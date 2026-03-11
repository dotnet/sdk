namespace TestProject
{
	internal class Program
	{
		public static async global::System.Threading.Tasks.Task<int> Main(string[] args)
		{
			Microsoft.Testing.Platform.Builder.ITestApplicationBuilder builder = await global::Microsoft.Testing.Platform.Builder.TestApplication.CreateBuilderAsync(args);
			builder.AddSelfRegisteredExtensions(args);
			using (global::Microsoft.Testing.Platform.Builder.ITestApplication app = await builder.BuildAsync())
			{
				return await app.RunAsync();
			}
		}
	}
}
