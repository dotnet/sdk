// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit.Sdk;

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
        public McpServerTemplateFixture(IMessageSink messageSink) : base(messageSink)
        {
            BaseWorkingDirectory = Utilities.CreateTemporaryFolder(nameof(McpServerTemplateTests));
        }

        internal string BaseWorkingDirectory { get; private set; }
    }
}
