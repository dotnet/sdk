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

        private static readonly string PackagesJsonPath = Path.Combine(CodeBaseRoot, "test", "TestPackages", "cgmanifest.json");

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

            Directory.Delete(outputDirectory, true);
            Directory.Delete(workingDirectory, true);

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

            Directory.Delete(outputDirectory, true);
            Directory.Delete(workingDirectory, true);
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
                    File.WriteAllText(Path.Combine(outputDirectory, "dotnet.config"), """
                        [dotnet.test.runner]
                        name = "Microsoft.Testing.Platform"
                        """);
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

            Directory.Delete(outputDirectory, true);
            Directory.Delete(workingDirectory, true);
        }

        private void AddItemToFsproj(string itemName, string outputDirectory, string projectName)
        {
            var fsproj = Path.Combine(outputDirectory, $"{projectName}.fsproj");
            var lines = File.ReadAllLines(fsproj).ToList();

            lines.Insert(lines.IndexOf("  <ItemGroup>") + 1, $@"    <Compile Include=""{itemName}.fs""/>");
            File.WriteAllLines(fsproj, lines);
        }

        private void RecordPackages(string projectDirectory)
        {
            // Get all project files with a single directory search, then filter to specific types
            var projectFiles = Directory.GetFiles(projectDirectory, "*.*proj")
                .Where(file => file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                              file.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase) ||
                              file.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase));

            // Load existing component detection manifest or create new one
            ComponentDetectionManifest manifest;

            if (File.Exists(PackagesJsonPath))
            {
                try
                {
                    string jsonContent = File.ReadAllText(PackagesJsonPath);
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
                if (Path.GetDirectoryName(PackagesJsonPath) is string directoryPath)
                {
                    Directory.CreateDirectory(directoryPath);
                }
                else
                {
                    _log.WriteLine($"Warning: Could not determine directory path for '{PackagesJsonPath}'.");
                    return;
                }

                // Write updated manifest
                File.WriteAllText(
                    PackagesJsonPath,
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
