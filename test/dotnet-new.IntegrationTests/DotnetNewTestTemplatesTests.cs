// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    public class DotnetNewTestTemplatesTests : BaseIntegrationTest
    {
        private readonly ITestOutputHelper _log;

        private static readonly ImmutableArray<string> SupportedTargetFrameworks =
        [
            ToolsetInfo.CurrentTargetFramework
        ];

        private static readonly (string ProjectTemplateName, string ItemTemplateName, string[] Languages, bool SupportsTestingPlatform)[] AvailableItemTemplates =
        [
            ("nunit", "nunit-test", Languages.All, false),
            ("mstest", "mstest-class", Languages.All, false),
        ];

        private static readonly (string ProjectTemplateName, string[] Languages, bool RunDotnetTest, bool SupportsTestingPlatform)[] AvailableProjectTemplates =
        [
            ("nunit", Languages.All, true, false),
            ("xunit", Languages.All, true, false),
            ("nunit-playwright", new[] { Languages.CSharp }, false, false),
        ];

        public DotnetNewTestTemplatesTests(ITestOutputHelper log) : base(log)
        {
            _log = log;
        }

        public static class Languages
        {
            public const string CSharp = "c#";
            public const string FSharp = "f#";
            public const string VisualBasic = "vb";
            public static readonly string[] All =
                [CSharp, FSharp, VisualBasic];
        }

        private class NullTestOutputHelper : ITestOutputHelper
        {
            public void WriteLine(string message) { }

            public void WriteLine(string format, params object[] args) { }
        }

        static DotnetNewTestTemplatesTests()
        {
            // This is the live location of the build
            string templatePackagePath = Path.Combine(
                RepoTemplatePackages,
                $"Microsoft.DotNet.Common.ProjectTemplates.{ToolsetInfo.CurrentTargetFrameworkVersion}",
                "content");

            var dummyLog = new NullTestOutputHelper();

            // Here we uninstall first, because we want to make sure we clean up before the installation
            // i.e we want to make sure our installation is done
            new DotnetNewCommand(dummyLog, "uninstall", templatePackagePath)
                   .WithCustomHive(CreateTemporaryFolder(folderName: "Home"))
                   .WithWorkingDirectory(CreateTemporaryFolder())
                   .Execute();

            new DotnetNewCommand(dummyLog, "install", templatePackagePath)
                .WithCustomHive(CreateTemporaryFolder(folderName: "Home"))
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .Pass();
        }

        [Theory]
        [MemberData(nameof(GetTemplateItemsToTest))]
        public void ItemTemplate_CanBeInstalledAndTestArePassing(string targetFramework, string projectTemplate, string itemTemplate, string language)
        {
            string testProjectName = GenerateTestProjectName();
            string outputDirectory = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();

            // Create new test project: dotnet new <projectTemplate> -n <testProjectName> -f <targetFramework> -lang <language>
            string args = $"{projectTemplate} -n {testProjectName} -f {targetFramework} -lang {language} -o {outputDirectory}";
            new DotnetNewCommand(_log, args)
                .WithCustomHive(outputDirectory).WithRawArguments()
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .Pass();

            var itemName = "test";

            // Add test item to test project: dotnet new <itemTemplate> -n <test> -lang <language> -o <outputDirectory>
            new DotnetNewCommand(_log, $"{itemTemplate} -n {itemName} -lang {language} -o {outputDirectory}")
                .WithCustomHive(outputDirectory).WithRawArguments()
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .Pass();

            if (language == Languages.FSharp)
            {
                // f# projects don't include all files by default, so the file is created
                // but the project ignores it until you manually add it into the project
                // in the right order
                AddItemToFsproj(itemName, outputDirectory, testProjectName);
            }

            var result = new DotnetTestCommand(_log, false)
                .WithWorkingDirectory(outputDirectory)
                .Execute(outputDirectory);

            result.Should().Pass();

            result.StdOut.Should().Contain("Passed!");
            // We created another test class (which will contain 1 test), and we already have 1 test when we created the test project.
            // Therefore, in total we would have 2.
            result.StdOut.Should().MatchRegex(@"Passed:\s*2");

            // After executing dotnet new and before cleaning up
            RecordPackages(outputDirectory);

            DeleteDirectoryWithRetry(outputDirectory);
            DeleteDirectoryWithRetry(workingDirectory);

        }

        [Theory]
        [InlineData("mstest", Languages.CSharp, false)]
        [InlineData("mstest", Languages.CSharp, true)]
        [InlineData("mstest", Languages.FSharp, false)]
        [InlineData("mstest", Languages.FSharp, true)]
        [InlineData("mstest", Languages.VisualBasic, false)]
        [InlineData("mstest", Languages.VisualBasic, true)]
        [InlineData("mstest-playwright", Languages.CSharp, false)]
        [InlineData("mstest-playwright", Languages.CSharp, true)]
        public void ProjectTemplate_MtpRunnerDoesNotOverwriteExistingRunner(string projectTemplate, string language, bool useMSTestSdk)
        {
            string repositoryDirectory = CreateTemporaryFolder();
            string outputDirectory = Path.Combine(repositoryDirectory, "tests", "NewTests");
            string home = CreateTemporaryFolder(folderName: "Home");
            string globalJsonPath = Path.Combine(repositoryDirectory, "global.json");
            const string originalGlobalJson = """
                {
                  // Preserve the repository's existing runner and unrelated settings.
                  "test": { "runner": "VSTest" },
                  "unrelated": { "value": "keep" }
                }
                """;
            File.WriteAllText(globalJsonPath, originalGlobalJson);

            List<string> args =
            [
                projectTemplate, "-n", "NewTests", "-lang", language, "-o", outputDirectory,
                "--test-runner", "Microsoft.Testing.Platform", "--no-restore"
            ];
            if (useMSTestSdk)
            {
                args.Add("--sdk");
            }

            var result = new DotnetNewCommand(_log, args.ToArray())
                .WithCustomHive(home)
                .WithWorkingDirectory(repositoryDirectory)
                .Execute();

            result.Should().Pass()
                .And.HaveStdErrContaining("test:runner")
                .And.HaveStdErrContaining("already has a different value")
                .And.NotHaveStdOutContaining("Successfully modified global.json.");
            File.ReadAllText(globalJsonPath).Should().Be(originalGlobalJson);
            Directory.GetFiles(outputDirectory, "*.*proj").Should().ContainSingle();
            File.Exists(Path.Combine(outputDirectory, "global.json")).Should().BeFalse();

            DeleteDirectoryWithRetry(repositoryDirectory);
            DeleteDirectoryWithRetry(home);
        }

        [Theory]
        [InlineData(null, "Microsoft.Testing.Platform")]
        [InlineData("{}", "Microsoft.Testing.Platform")]
        [InlineData("""{"test":{}}""", "Microsoft.Testing.Platform")]
        [InlineData("""{"test":{"runner":"Microsoft.Testing.Platform"}}""", "Microsoft.Testing.Platform")]
        [InlineData("""{"test":{"runner":"microsoft.testing.platform"}}""", "microsoft.testing.platform")]
        [InlineData("""{"test":{"runner":"mIcRoSoFt.TeStInG.PlAtFoRm"}}""", "mIcRoSoFt.TeStInG.PlAtFoRm")]
        public void ProjectTemplate_MtpRunnerCreatesOrUpdatesCompatibleGlobalJson(string? originalGlobalJson, string expectedRunner)
        {
            string repositoryDirectory = CreateTemporaryFolder();
            Directory.CreateDirectory(Path.Combine(repositoryDirectory, ".git"));
            string outputDirectory = Path.Combine(repositoryDirectory, "NewTests");
            string home = CreateTemporaryFolder(folderName: "Home");
            string globalJsonPath = Path.Combine(repositoryDirectory, "global.json");
            if (originalGlobalJson is not null)
            {
                File.WriteAllText(globalJsonPath, originalGlobalJson);
            }

            new DotnetNewCommand(_log, "mstest", "-n", "NewTests", "-o", outputDirectory, "--test-runner", "Microsoft.Testing.Platform", "--no-restore")
                .WithCustomHive(home)
                .WithWorkingDirectory(repositoryDirectory)
                .Execute()
                .Should().Pass()
                .And.NotHaveStdErr();

            using JsonDocument globalJson = JsonDocument.Parse(File.ReadAllText(globalJsonPath));
            globalJson.RootElement.GetProperty("test").GetProperty("runner").GetString()
                .Should().Be(expectedRunner);
            File.Exists(Path.Combine(outputDirectory, "global.json")).Should().BeFalse();

            DeleteDirectoryWithRetry(repositoryDirectory);
            DeleteDirectoryWithRetry(home);
        }

        [Theory]
        [InlineData("VSTest", false)]
        [InlineData("VSTest", true)]
        [InlineData("Microsoft.Testing.Platform", false)]
        [InlineData("Microsoft.Testing.Platform", true)]
        [InlineData("microsoft.testing.platform", false)]
        [InlineData("mIcRoSoFt.TeStInG.PlAtFoRm", true)]
        public void MtpProjectCreation_PreservesExistingTestProjects(string testRunner, bool useMSTestSdk)
        {
            string repositoryDirectory = CreateTemporaryFolder();
            string existingProjectDirectory = Path.Combine(repositoryDirectory, "ExistingTests");
            string newProjectDirectory = Path.Combine(repositoryDirectory, "NewTests");
            string home = CreateTemporaryFolder(folderName: "Home");
            string globalJsonPath = Path.Combine(repositoryDirectory, "global.json");
            string originalGlobalJson = $$"""
                {
                  "test": {
                    "runner": "{{testRunner}}"
                  }
                }
                """;
            File.WriteAllText(globalJsonPath, originalGlobalJson);
            string[] sdkArgs = useMSTestSdk ? new[] { "--sdk" } : Array.Empty<string>();
            string templateRunner = testRunner.Equals("Microsoft.Testing.Platform", StringComparison.OrdinalIgnoreCase)
                ? "Microsoft.Testing.Platform"
                : testRunner;

            List<string> existingProjectArgs =
            [
                "mstest", "-n", "ExistingTests", "-o", existingProjectDirectory,
                "-f", ToolsetInfo.CurrentTargetFramework, "--test-runner", templateRunner, "--no-restore"
            ];
            existingProjectArgs.AddRange(sdkArgs);
            new DotnetNewCommand(_log, existingProjectArgs.ToArray())
                .WithCustomHive(home)
                .WithWorkingDirectory(repositoryDirectory)
                .Execute()
                .Should().Pass();

            string[] testArgs = testRunner == "VSTest"
                ? new[] { existingProjectDirectory }
                : new[] { "--project", existingProjectDirectory };
            new DotnetTestCommand(_log, false)
                .WithWorkingDirectory(repositoryDirectory)
                .WithEnvironmentVariable("DOTNET_TEST_RUNNER", string.Empty)
                .Execute(testArgs)
                .Should().Pass();

            if (testRunner == "VSTest")
            {
                File.WriteAllText(globalJsonPath, originalGlobalJson.Replace("VSTest", "Microsoft.Testing.Platform"));
                new DotnetTestCommand(_log, false)
                    .WithWorkingDirectory(repositoryDirectory)
                    .WithEnvironmentVariable("DOTNET_TEST_RUNNER", string.Empty)
                    .Execute("--project", existingProjectDirectory, "--no-restore")
                    .Should().Fail()
                    .And.HaveStdErrContaining("All projects must use that test runner.");
                File.WriteAllText(globalJsonPath, originalGlobalJson);
            }

            List<string> newProjectArgs =
            [
                "mstest", "-n", "NewTests", "-o", newProjectDirectory,
                "-f", ToolsetInfo.CurrentTargetFramework, "--test-runner", "Microsoft.Testing.Platform", "--no-restore"
            ];
            newProjectArgs.AddRange(sdkArgs);
            var creationResult = new DotnetNewCommand(_log, newProjectArgs.ToArray())
                .WithCustomHive(home)
                .WithWorkingDirectory(repositoryDirectory)
                .Execute();
            creationResult.Should().Pass();
            using JsonDocument updatedGlobalJson = JsonDocument.Parse(File.ReadAllText(globalJsonPath));
            updatedGlobalJson.RootElement.GetProperty("test").GetProperty("runner").GetString()
                .Should().Be(testRunner);

            if (testRunner == "VSTest")
            {
                File.ReadAllText(globalJsonPath).Should().Be(originalGlobalJson);
                creationResult.Should().HaveStdErrContaining("already has a different value");
            }
            else
            {
                creationResult.Should().NotHaveStdErr();
                new DotnetTestCommand(_log, false)
                    .WithWorkingDirectory(repositoryDirectory)
                    .WithEnvironmentVariable("DOTNET_TEST_RUNNER", string.Empty)
                    .Execute("--project", newProjectDirectory)
                    .Should().Pass()
                    .And.HaveStdOutContaining("succeeded: 1");
            }

            var existingProjectResult = new DotnetTestCommand(_log, false)
                .WithWorkingDirectory(repositoryDirectory)
                .WithEnvironmentVariable("DOTNET_TEST_RUNNER", string.Empty)
                .Execute(testArgs);
            existingProjectResult.Should().Pass();
            existingProjectResult.StdOut.Should().MatchRegex(testRunner == "VSTest" ? @"Passed:\s*1" : "succeeded: 1");

            DeleteDirectoryWithRetry(repositoryDirectory);
            DeleteDirectoryWithRetry(home);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void MSTestProjectTemplate_UsesVSTestByDefault(bool useMSTestSdk)
        {
            string repositoryDirectory = CreateTemporaryFolder();
            Directory.CreateDirectory(Path.Combine(repositoryDirectory, ".git"));
            string outputDirectory = Path.Combine(repositoryDirectory, "NewTests");
            string home = CreateTemporaryFolder(folderName: "Home");
            string[] sdkArgs = useMSTestSdk ? new[] { "--sdk" } : Array.Empty<string>();

            List<string> args =
            [
                "mstest", "-n", "NewTests", "-o", outputDirectory,
                "-f", ToolsetInfo.CurrentTargetFramework, "--no-restore"
            ];
            args.AddRange(sdkArgs);
            new DotnetNewCommand(_log, args.ToArray())
                .WithCustomHive(home)
                .WithWorkingDirectory(repositoryDirectory)
                .Execute()
                .Should().Pass()
                .And.NotHaveStdErr();

            File.Exists(Path.Combine(repositoryDirectory, "global.json")).Should().BeFalse();
            File.Exists(Path.Combine(outputDirectory, "global.json")).Should().BeFalse();
            new DotnetTestCommand(_log, false)
                .WithWorkingDirectory(repositoryDirectory)
                .WithEnvironmentVariable("DOTNET_TEST_RUNNER", string.Empty)
                .Execute(outputDirectory)
                .Should().Pass()
                .And.HaveStdOutMatching(@"Passed:\s*1");

            DeleteDirectoryWithRetry(repositoryDirectory);
            DeleteDirectoryWithRetry(home);
        }

        [Theory]
        [MemberData(nameof(GetTemplateProjectsToTest))]
        public void ProjectTemplate_CanBeInstalledAndTestsArePassing(string targetFramework, string projectTemplate, string language, bool runDotnetTest)
        {
            string testProjectName = GenerateTestProjectName();
            string outputDirectory = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();

            // Create new test project: dotnet new <projectTemplate> -n <testProjectName> -f <targetFramework> -lang <language>
            string args = $"{projectTemplate} -n {testProjectName} -f {targetFramework} -lang {language} -o {outputDirectory}";
            new DotnetNewCommand(_log, args)
                .WithCustomHive(outputDirectory).WithRawArguments()
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .Pass();

            if (runDotnetTest)
            {
                var result = new DotnetTestCommand(_log, false)
                .WithWorkingDirectory(outputDirectory)
                .Execute(outputDirectory);

                result.Should().Pass();

                result.StdOut.Should().Contain("Passed!");
                result.StdOut.Should().MatchRegex(@"Passed:\s*1");
            }

            // After executing dotnet new and before cleaning up
            RecordPackages(outputDirectory);

            DeleteDirectoryWithRetry(outputDirectory);
            DeleteDirectoryWithRetry(workingDirectory);
        }

        [Theory]
        [MemberData(nameof(GetMSTestAndPlaywrightCoverageAndRunnerCombinations))]
        public void MSTestAndPlaywrightProjectTemplate_WithCoverageToolAndTestRunner_CanBeInstalledAndTestsArePassing(
            string projectTemplate,
            string targetFramework,
            string language,
            string coverageTool,
            string testRunner,
            bool runDotnetTest)
        {
            string testProjectName = GenerateTestProjectName();
            string outputDirectory = CreateTemporaryFolder(folderName: "Home");

            // Prevent the global.json post action from walking up the directory parents up to our own solution root, which would affect other tests.
            Directory.CreateDirectory(Path.Combine(outputDirectory, ".git"));

            string workingDirectory = CreateTemporaryFolder();

            // Create new test project: dotnet new <projectTemplate> -n <testProjectName> -f <targetFramework> -lang <language> --coverage-tool <coverageTool> --test-runner <testRunner>
            string args = $"{projectTemplate} -n {testProjectName} -f {targetFramework} -lang {language} -o {outputDirectory} --coverage-tool {coverageTool} --test-runner {testRunner}";
            new DotnetNewCommand(_log, args)
                .WithCustomHive(outputDirectory).WithRawArguments()
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .Pass();

            if (runDotnetTest)
            {
                var isMTP = testRunner == "Microsoft.Testing.Platform";
                if (isMTP)
                {
                    File.Exists(Path.Combine(outputDirectory, "global.json")).Should().BeTrue();
                }

                var result = new DotnetTestCommand(_log, false)
                .WithWorkingDirectory(outputDirectory)
#pragma warning disable SA1010 // Opening square brackets should be spaced correctly - false positive. Current formatting is good.
                .Execute(isMTP ? ["--project", outputDirectory] : [outputDirectory]);
#pragma warning restore SA1010 // Opening square brackets should be spaced correctly

                result.Should().Pass();

                result.StdOut.Should().Contain("Passed!");
                result.StdOut.Should().MatchRegex(isMTP ? "succeeded: 1" : @"Passed:\s*1");
            }

            // After executing dotnet new and before cleaning up
            RecordPackages(outputDirectory);

            DeleteDirectoryWithRetry(outputDirectory);
            DeleteDirectoryWithRetry(workingDirectory);
        }

        private void AddItemToFsproj(string itemName, string outputDirectory, string projectName)
        {
            var fsproj = Path.Combine(outputDirectory, $"{projectName}.fsproj");
            var lines = File.ReadAllLines(fsproj).ToList();

            lines.Insert(lines.IndexOf("  <ItemGroup>") + 1, $@"    <Compile Include=""{itemName}.fs""/>");
            File.WriteAllLines(fsproj, lines);
        }

        /// <summary>
        /// Retries Directory.Delete to handle transient file locks after dotnet test exits.
        /// </summary>
        private static void DeleteDirectoryWithRetry(string path, int maxRetries = 10)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    Directory.Delete(path, true);
                    return;
                }
                catch (UnauthorizedAccessException) when (i < maxRetries - 1)
                {
                    Thread.Sleep(100 * (i + 1));
                }
                catch (IOException) when (i < maxRetries - 1)
                {
                    Thread.Sleep(100 * (i + 1));
                }
            }
        }

        private void RecordPackages(string projectDirectory)
        {
            string packagesJsonPath = Path.Combine(
                TestContext.Current.TestPackages ?? throw new InvalidOperationException("TestPackages should never be null."),
                "cgmanifest.json");

            // Get all project files with a single directory search, then filter to specific types
            var projectFiles = Directory.GetFiles(projectDirectory, "*.*proj")
                .Where(file => file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                              file.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase) ||
                              file.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase));

            // Load existing component detection manifest or create new one
            ComponentDetectionManifest manifest;

            if (File.Exists(packagesJsonPath))
            {
                try
                {
                    string jsonContent = File.ReadAllText(packagesJsonPath);
                    manifest = JsonSerializer.Deserialize<ComponentDetectionManifest>(jsonContent) ??
                        CreateNewManifest();
                }
                catch (Exception ex)
                {
                    _log.WriteLine($"Warning: Could not parse existing component detection manifest: {ex.Message}");
                    // Don't create a new manifest when we can't parse the existing one
                    // This prevents overwriting the existing file with an empty manifest
                    return;
                }
            }
            else
            {
                manifest = CreateNewManifest();
            }

            // Keep track of whether we added anything new
            bool updatedManifest = false;

            // Extract package references from project files
            foreach (var projectFile in projectFiles)
            {
                string content = File.ReadAllText(projectFile);
                var packageRefMatches = Regex.Matches(
                    content,
                    @"<PackageReference\s+(?:Include=""([^""]+)""\s+Version=""([^""]+)""|Version=""([^""]+)""\s+Include=""([^""]+)"")",
                    RegexOptions.IgnoreCase);

                foreach (Match match in packageRefMatches)
                {
                    string packageId;
                    string version;

                    if (!string.IsNullOrEmpty(match.Groups[1].Value))
                    {
                        // Include first, then Version
                        packageId = match.Groups[1].Value;
                        version = match.Groups[2].Value;
                    }
                    else
                    {
                        // Version first, then Include
                        packageId = match.Groups[4].Value;
                        version = match.Groups[3].Value;
                    }

                    // Find existing registration for this package with the SAME VERSION
                    var existingRegistration = manifest.Registrations?.FirstOrDefault(r =>
                        r.Component != null &&
                        r.Component.Nuget != null &&
                        string.Equals(r.Component.Nuget.Name, packageId, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(r.Component.Nuget.Version, version, StringComparison.OrdinalIgnoreCase));

                    if (existingRegistration == null)
                    {
                        // Add new package if it doesn't exist with this version
                        manifest.Registrations?.Add(new Registration
                        {
                            Component = new Component
                            {
                                Type = "nuget",
                                Nuget = new NugetComponent
                                {
                                    Name = packageId,
                                    Version = version
                                }
                            }
                        });
                        updatedManifest = true;
                    }
                }
            }

            // Only write the file if we actually added something new
            if (updatedManifest)
            {
                // Ensure directory exists
                if (Path.GetDirectoryName(packagesJsonPath) is string directoryPath)
                {
                    Directory.CreateDirectory(directoryPath);
                }
                else
                {
                    _log.WriteLine($"Warning: Could not determine directory path for '{packagesJsonPath}'.");
                    return;
                }

                // Write updated manifest
                File.WriteAllText(
                    packagesJsonPath,
                    JsonSerializer.Serialize(manifest, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    }));
            }
        }

        private ComponentDetectionManifest CreateNewManifest()
        {
            return new ComponentDetectionManifest
            {
                Schema = "https://json.schemastore.org/component-detection-manifest.json",
                Version = 1,
                Registrations =
                []
            };
        }

        // Classes to model the component detection manifest
        private class ComponentDetectionManifest
        {
            [JsonPropertyName("$schema")]
            public string? Schema { get; set; }

            [JsonPropertyName("version")]
            public int Version { get; set; }

            [JsonPropertyName("registrations")]
            public List<Registration>? Registrations { get; set; }
        }

        private class Registration
        {
            [JsonPropertyName("component")]
            public Component? Component { get; set; }
        }

        private class Component
        {
            [JsonPropertyName("type")]
            public string? Type { get; set; }

            [JsonPropertyName("nuget")]
            public NugetComponent? Nuget { get; set; }
        }

        private class NugetComponent
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("version")]
            public string? Version { get; set; }
        }

        private static string GenerateTestProjectName()
        {
            // Avoiding VB errors because root namespace must not start with number or contain dashes
            return "Test_" + Guid.NewGuid().ToString("N");
        }

        public static IEnumerable<object[]> GetTemplateItemsToTest()
        {
            foreach (var targetFramework in SupportedTargetFrameworks)
            {
                foreach (var (projectTemplate, itemTemplate, languages, supportsTestingPlatform) in AvailableItemTemplates)
                {
                    foreach (var language in languages)
                    {
                        yield return new object[] { targetFramework, projectTemplate, itemTemplate, language };
                    }
                }
            }
        }

        public static IEnumerable<object[]> GetTemplateProjectsToTest()
        {
            foreach (var targetFramework in SupportedTargetFrameworks)
            {
                foreach (var (projectTemplate, languages, runDotnetTest, supportsTestingPlatform) in AvailableProjectTemplates)
                {
                    foreach (var language in languages)
                    {
                        yield return new object[] { targetFramework, projectTemplate, language, runDotnetTest };
                    }
                }
            }
        }

        public static IEnumerable<object[]> GetMSTestAndPlaywrightCoverageAndRunnerCombinations()
        {
            var coverageTools = new[] { "Microsoft.CodeCoverage", "coverlet" };
            var testRunners = new[] { "VSTest", "Microsoft.Testing.Platform" };
            foreach (var targetFramework in SupportedTargetFrameworks)
            {
                // mstest: all languages, runDotnetTest = true
                foreach (var language in Languages.All)
                {
                    foreach (var coverageTool in coverageTools)
                    {
                        foreach (var testRunner in testRunners)
                        {
                            yield return new object[] { "mstest", targetFramework, language, coverageTool, testRunner, true };
                        }
                    }
                }
                // mstest-playwright: only c#, runDotnetTest = false
                foreach (var coverageTool in coverageTools)
                {
                    foreach (var testRunner in testRunners)
                    {
                        yield return new object[] { "mstest-playwright", targetFramework, Languages.CSharp, coverageTool, testRunner, false };
                    }
                }
            }
        }
    }
}
