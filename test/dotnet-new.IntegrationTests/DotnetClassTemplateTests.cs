// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Authoring.TemplateVerifier;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    [TestClass]
    public class DotnetClassTemplateTests : BaseIntegrationTest
    {
        private ITestOutputHelper _log => Log;
        private ILogger? _loggerInstance;
        private ILogger _logger => _loggerInstance ??= new TestLoggerFactory(Log).CreateLogger(nameof(DotnetClassTemplateTests));
        private static SharedHomeDirectory s_fixture = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext ctx)
        {
            s_fixture = new SharedHomeDirectory(new TestContextOutputHelper(ctx));
        }

        [ClassCleanup]
        public static void ClassCleanup() => s_fixture?.Dispose();

        private SharedHomeDirectory _fixture => s_fixture;

        [TestMethod]
        [DataRow("class")]
        [DataRow("class", "preview", "net7.0")]
        [DataRow("class", "10.0", "net6.0")]
        [DataRow("class", "9.0", "netstandard2.0")]
        [DataRow("interface")]
        [DataRow("interface", "10.0", "net6.0")]
        [DataRow("interface", "9", "netstandard2.0")]
        [DataRow("record")]
        [DataRow("record", "10", "net6.0")]
        [DataRow("record", "9.0")]
        [DataRow("record", "8.0", "netstandard2.0")]
        [DataRow("struct")]
        [DataRow("struct", "10")]
        [DataRow("struct", "10", "net6.0")]
        [DataRow("struct", "9.0", "netstandard2.0")]
        [DataRow("enum")]
        [DataRow("enum", "10", "net6.0")]
        [DataRow("enum", "", "net7.0")]
        [DataRow("enum", "9.0", "netstandard2.0")]
        [DataRow("enum", "", "netstandard2.0")]
        public async Task DotnetCSharpClassTemplatesTest(
            string templateShortName,
            string langVersion = "",
            string targetFramework = "")
        {
            // prevents logging a welcome message from sdk installation
            Dictionary<string, string?> environmentUnderTest = new() { ["DOTNET_NOLOGO"] = false.ToString() };
            SdkTestContext.Current.AddTestEnvironmentVariables(environmentUnderTest);

            string folderName = GetFolderName(templateShortName, langVersion, targetFramework);
            string workingDir = CreateTemporaryFolder($"{nameof(DotnetCSharpClassTemplatesTest)}.{folderName}");
            string projectName = CreateTestProject(workingDir, langVersion, targetFramework);

            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: templateShortName)
            {
                SnapshotsDirectory = ApprovalsDirectory,
                VerifyCommandOutput = true,
                TemplateSpecificArgs = new[] { "--name", "TestItem1" },
                VerificationExcludePatterns = new[]
                {
                    "*/stderr.txt",
                    "*\\stderr.txt",
                    // restored files in obj folder
                    $"*{projectName}.csproj.*",
                    "*project.*.*"
                },
                SettingsDirectory = _fixture.HomeDirectory,
                DotnetExecutablePath = SdkTestContext.Current.ToolsetUnderTest?.DotNetHostPath,
                DoNotAppendTemplateArgsToScenarioName = true,
                DoNotPrependTemplateNameToScenarioName = true,
                ScenarioName = folderName,
                OutputDirectory = workingDir,
                EnsureEmptyOutputDirectory = false
            }
            .WithCustomEnvironment(environmentUnderTest!)
            .WithCustomScrubbers(
               ScrubbersDefinition.Empty
               .AddScrubber(sb => sb.ScrubMSBuildDebugLogMessage(), "txt")
               .AddScrubber((path, content) =>
               {
                   if (path.Replace(Path.DirectorySeparatorChar, '/') == "std-streams/stdout.txt")
                   {
                       content
                       .UnixifyNewlines()
                       .ScrubAndReplace(
                           "Warning: Failed to evaluate bind symbol \'evaluatedLangVersion\', it will be skipped.",
                           string.Empty);

                       content.ScrubAndReplace("\n", string.Empty);
                   }
               }));

            VerificationEngine engine = new(_logger);
            await engine.Execute(options, TestContext.CancellationToken);

            ValidateInstantiatedProject(workingDir);
        }

        [TestMethod]
        [DataRow("class")]
        [DataRow("class", "latest", "net7.0")]
        [DataRow("class", "16", "net6.0")]
        [DataRow("class", "15.3", "netstandard2.0")]
        [DataRow("enum")]
        [DataRow("enum", "16", "net6.0")]
        [DataRow("enum", "latest", "net7.0")]
        [DataRow("enum", "15.3", "netstandard2.0")]
        [DataRow("structure")]
        [DataRow("structure", "latest")]
        [DataRow("struct", "16", "net6.0")]
        [DataRow("structure", "15.3", "netstandard2.0", "CustomFileName")]
        [DataRow("interface")]
        [DataRow("interface", "16", "net7.0")]
        [DataRow("interface", "latest", "net6.0")]
        [DataRow("interface", "15.3", "netstandard2.0")]
        [DataRow("module")]
        [DataRow("module", "16", "net7.0")]
        [DataRow("module", "latest", "net6.0")]
        [DataRow("module", "15.3", "netstandard2.0")]
        [DataRow("module", "15.5", "netstandard2.0", "CustomFileName")]
        public async Task DotnetVisualBasicClassTemplatesTest(
            string templateShortName,
            string langVersion = "",
            string targetFramework = "",
            string fileName = "")
        {
            // prevents logging a welcome message from sdk installation
            Dictionary<string, string?> environmentUnderTest = new() { ["DOTNET_NOLOGO"] = false.ToString() };
            SdkTestContext.Current.AddTestEnvironmentVariables(environmentUnderTest);

            string folderName = GetFolderName(templateShortName, langVersion, targetFramework);
            string workingDir = CreateTemporaryFolder($"{nameof(DotnetVisualBasicClassTemplatesTest)}.{folderName}");
            string projectName = CreateTestProject(workingDir, langVersion, targetFramework, "VB");

            TemplateVerifierOptions options = new TemplateVerifierOptions(templateName: templateShortName)
            {
                SnapshotsDirectory = ApprovalsDirectory,
                VerifyCommandOutput = true,
                TemplateSpecificArgs = new[] { "--name", string.IsNullOrWhiteSpace(fileName) ? "TestItem1" : fileName, "--language", "VB" },
                VerificationExcludePatterns = new[]
                {
                    "*/stderr.txt",
                    "*\\stderr.txt",
                    // restored files in obj folder
                    $"*{projectName}.vbproj.*",
                    "*project.*.*"
                },
                SettingsDirectory = _fixture.HomeDirectory,
                DotnetExecutablePath = SdkTestContext.Current.ToolsetUnderTest?.DotNetHostPath,
                DoNotAppendTemplateArgsToScenarioName = true,
                DoNotPrependTemplateNameToScenarioName = true,
                ScenarioName = folderName,
                OutputDirectory = workingDir,
                EnsureEmptyOutputDirectory = false
            }
            .WithCustomEnvironment(environmentUnderTest!)
            .WithCustomScrubbers(
               ScrubbersDefinition.Empty
               .AddScrubber(sb => sb.ScrubMSBuildDebugLogMessage(), "txt")
               .AddScrubber((path, content) =>
               {
                   if (path.Replace(Path.DirectorySeparatorChar, '/') == "std-streams/stdout.txt")
                   {
                       content
                       .UnixifyNewlines()
                       .ScrubAndReplace(
                           "Warning: Failed to evaluate bind symbol \'evaluatedLangVersion\', it will be skipped.",
                           string.Empty);

                       content.ScrubAndReplace("\n", string.Empty);
                   }
               }));

            VerificationEngine engine = new(_logger);
            await engine.Execute(options, TestContext.CancellationToken);

            ValidateInstantiatedProject(workingDir);
        }

        private string CreateTestProject(
            string workingDir,
            string langVersion,
            string targetFramework,
            string language = "")
        {
            IDictionary<string, string> languageToProjectExtMap = new Dictionary<string, string>
            {
                { "VB", ".vbproj" },
                { "", ".csproj" }
            };

            IDictionary<string, string> languageToClassExtMap = new Dictionary<string, string>
            {
                { "VB", ".vb" },
                { "", ".cs" }
            };

            IList<string> projectArgs = new List<string>() { "classlib", "-o", workingDir, "--name", "ClassLib" };
            if (!string.IsNullOrEmpty(langVersion))
            {
                projectArgs.AddRange(new[] { "--langVersion", langVersion });
            }
            if (!string.IsNullOrEmpty(targetFramework))
            {
                projectArgs.AddRange(new[] { "--framework", targetFramework });
            }
            if (!string.IsNullOrEmpty(language))
            {
                projectArgs.AddRange(new[] { "--language", language });
            }

            new DotnetNewCommand(Log, projectArgs.ToArray())
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            foreach (string classFile in Directory.GetFiles(workingDir, $"*{languageToClassExtMap[language]}"))
            {
                File.Delete(classFile);
            }

            return Path.GetFileNameWithoutExtension(Directory
                .GetFiles(workingDir, $"*{languageToProjectExtMap[language]}")?.FirstOrDefault() ?? string.Empty);
        }

        private void ValidateInstantiatedProject(string workingDir)
        {
            new DotnetBuildCommand(_log)
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            Directory.Delete(workingDir, true);
        }

        private string GetFolderName(string templateShortName, string langVersion, string targetFramework)
        {
            StringBuilder sb = new();
            sb.Append($"{templateShortName}");

            if (!string.IsNullOrEmpty(langVersion))
            {
                sb.Append($".langVersion={langVersion}");
            }

            if (!string.IsNullOrEmpty(targetFramework))
            {
                sb.Append($".targetFramework={targetFramework}");
            }

            return sb.ToString();
        }
    }
}
