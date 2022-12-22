// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.Extensions.Logging;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
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
        [InlineData("class", "9.0", "netstandard2.0")]
        [InlineData("interface")]
        [InlineData("interface", "9.0", "netstandard2.0")]
        [InlineData("record")]
        [InlineData("record", "9.0")]
        [InlineData("record", "8.0", "netstandard2.0")]
        [InlineData("struct")]
        [InlineData("struct", "9.0", "netstandard2.0")]
        [InlineData("enum")]
        [InlineData("enum", "9.0", "netstandard2.0")]
        public async void DotnetCSharpClassTemplatesTest(
            string templateShortName,
            string langVersion = "preview",
            string targetFramework = "net7.0")
        {
            string expectedProjectName = $"{templateShortName}.langVersion={langVersion}.targetFramework={targetFramework}";
            string workingDir = CreateTemporaryFolder($"{nameof(DotnetCSharpClassTemplatesTest)}.{expectedProjectName}");
            string projectName = CreateTestProject(workingDir, langVersion, targetFramework);

            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: templateShortName)
            {
                SnapshotsDirectory = "Approvals",
                VerifyCommandOutput = true,
                TemplateSpecificArgs = new[] { "--name", "Class1" },
                VerificationExcludePatterns = new[]
                {
                    "*/stderr.txt",
                    "*\\stderr.txt",
                    $"*{projectName}.*",
                    "*project.*.*"
                },
                SettingsDirectory = _fixture.HomeDirectory,
                DotnetExecutablePath = TestContext.Current.ToolsetUnderTest.DotNetHostPath,
                DoNotAppendTemplateArgsToScenarioName = true,
                DoNotPrependTemplateNameToScenarioName = true,
                ScenarioName = expectedProjectName,
                OutputDirectory = workingDir,
                EnsureEmptyOutputDirectory = false
            }
            .WithCustomScrubbers(
                ScrubbersDefinition.Empty
                    .AddScrubber(sb => sb.Replace($"_{projectName}", $"{expectedProjectName}")));

            VerificationEngine engine = new VerificationEngine(_logger);
            await engine.Execute(options)
                .ConfigureAwait(false);
        }

        private string CreateTestProject(
            string workingDir,
            string langVersion,
            string targetFramework)
        {
            new DotnetNewCommand(Log, "classlib", "-o", workingDir, "--name", "ClassLib", "--framework", targetFramework, "--langVersion", langVersion)
                .WithVirtualHive()
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            foreach (string classFile in Directory.GetFiles(workingDir, "*.cs"))
            {
                File.Delete(classFile);
            }

            return Path.GetFileNameWithoutExtension(Directory.GetFiles(workingDir, "*.csproj")?.FirstOrDefault() ?? string.Empty);
        }
    }
}
