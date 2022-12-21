// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.Extensions.Logging;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.TemplateEngine.Authoring.TemplateVerifier;
using Microsoft.TemplateEngine.TestHelper;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    public class DotnetClassTemplateTests : BaseIntegrationTest, IClassFixture<SharedHomeDirectory>
    {
        private readonly SharedHomeDirectory _fixture;
        private readonly ITestOutputHelper _log;
        private readonly ILogger _logger;

        public DotnetClassTemplateTests(SharedHomeDirectory fixture, ITestOutputHelper log) : base(log)
        {
            _fixture = fixture;
            _log = log;
            _logger = new XunitLoggerProvider(log)
                .CreateLogger("TestRun");
        }

        [Theory]
        [InlineData("class")]
        [InlineData("class", "9.0", "net5.0")]
        [InlineData("interface")]
        [InlineData("interface", "9.0", "net5.0")]
        [InlineData("record")]
        [InlineData("record", "9.0", "net5.0")]
        [InlineData("record", "8.0", "netcoreapp3.1")]
        [InlineData("struct")]
        [InlineData("struct", "9.0", "net5.0")]
        [InlineData("enum")]
        [InlineData("enum", "9.0", "net5.0")]
        public async void DotnetCSharpClassTemplatesTest(
            string templateShortName,
            string langVersion = "11.0",
            string targetFramework = "net7.0")
        {
            string scenarioName = $"{nameof(DotnetCSharpClassTemplatesTest)}.{templateShortName}";
            string workingDir = CreateTemporaryFolder(scenarioName);
            CreateTestProject(workingDir, langVersion, targetFramework);

            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: templateShortName)
            {
                SnapshotsDirectory = "Approvals",
                VerifyCommandOutput = true,
                VerificationExcludePatterns = new[] { "*/stderr.txt", "*\\stderr.txt" },
                SettingsDirectory = _fixture.HomeDirectory,
                DotnetExecutablePath = TestContext.Current.ToolsetUnderTest.DotNetHostPath,
                DoNotPrependCallerMethodNameToScenarioName = true,
                DoNotAppendTemplateArgsToScenarioName = true,
                ScenarioName = scenarioName,
                OutputDirectory = workingDir
            };

            VerificationEngine engine = new VerificationEngine(_logger);
            await engine.Execute(options)
                .ConfigureAwait(false);
        }

        private void CreateTestProject(
            string workingDir,
            string langVersion,
            string targetFramework)
        {
            new DotnetNewCommand(Log, "classlib", "-o", workingDir, "--framework", targetFramework, "--langVersion", langVersion, "--no-restore")
                .WithVirtualHive()
                .WithWorkingDirectory(workingDir)
            .Execute();

            foreach (string classFile in Directory.GetFiles(workingDir, "*.cs"))
            {
                File.Delete(classFile);
            }
        }
    }
}
