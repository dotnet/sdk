// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

<<<<<<< HEAD
namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    public class McpServerTemplateTests : BaseIntegrationTest, IClassFixture<McpServerTemplateFixture>
    {
        private readonly McpServerTemplateFixture _fixture;
        private readonly ITestOutputHelper _log;

        public McpServerTemplateTests(McpServerTemplateFixture fixture, ITestOutputHelper log) : base(log)
        {
            _fixture = fixture;
            _log = log;
        }

        [Theory]
        [InlineData("mcpserver_local", "mcpserver", "--transport", "local")]
        [InlineData("mcpserver_remote", "mcpserver", "--transport", "remote")]
        [InlineData("mcpserver_local_no_selfcontained", "mcpserver", "--transport", "local", "--self-contained", "false")]
        [InlineData("mcpserver_remote_no_selfcontained", "mcpserver", "--transport", "remote", "--self-contained", "false")]
        [InlineData("mcpserver_local_aot", "mcpserver", "--transport", "local", "--aot", "true")]
        [InlineData("mcpserver_remote_aot", "mcpserver", "--transport", "remote", "--aot", "true")]
=======

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    [TestClass]
    public class McpServerTemplateTests : BaseIntegrationTest
    {
        private ITestOutputHelper _log => Log;
        private static McpServerTemplateFixture s_fixture = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext ctx)
        {
            s_fixture = new McpServerTemplateFixture(new TestContextOutputHelper(ctx));
        }

        [ClassCleanup]
        public static void ClassCleanup() => s_fixture?.Dispose();

        private McpServerTemplateFixture _fixture => s_fixture;

        [TestMethod]
        [DataRow("mcpserver_local", "mcpserver", "--transport", "local")]
        [DataRow("mcpserver_remote", "mcpserver", "--transport", "remote")]
        [DataRow("mcpserver_local_no_selfcontained", "mcpserver", "--transport", "local", "--self-contained", "false")]
        [DataRow("mcpserver_remote_no_selfcontained", "mcpserver", "--transport", "remote", "--self-contained", "false")]
        [DataRow("mcpserver_local_aot", "mcpserver", "--transport", "local", "--aot", "true")]
        [DataRow("mcpserver_remote_aot", "mcpserver", "--transport", "remote", "--aot", "true")]
>>>>>>> origin/main
        public void AllMcpServerProjectsRestoreAndBuild(string testName, params string[] args)
        {
            string workingDir = Path.Combine(_fixture.BaseWorkingDirectory, testName);
            Directory.CreateDirectory(workingDir);

            new DotnetNewCommand(_log, args)
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();

            new DotnetRestoreCommand(_log)
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();

            new DotnetBuildCommand(_log)
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();

            Directory.Delete(workingDir, true);
        }
    }

    public sealed class McpServerTemplateFixture : SharedHomeDirectory
    {
<<<<<<< HEAD
        public McpServerTemplateFixture(IMessageSink messageSink) : base(messageSink)
=======
        public McpServerTemplateFixture(ITestOutputHelper log) : base(log)
>>>>>>> origin/main
        {
            BaseWorkingDirectory = Utilities.CreateTemporaryFolder(nameof(McpServerTemplateTests));
        }

        internal string BaseWorkingDirectory { get; private set; }
    }
}
