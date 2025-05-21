// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.TestTemplates.Acceptance.Tests;

public sealed partial class AcceptanceTests : IClassFixture<AcceptanceTests.TemplateFixture>, IDisposable
{
    private static readonly ImmutableArray<string> SupportedTargetFrameworks =
    [
        "net10.0",
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

    // Fixture for setup/teardown
    public class TemplateFixture : IDisposable
    {
        public TemplateFixture()
        {
            foreach (var targetFramework in SupportedTargetFrameworks)
            {
                DotnetUtils.InvokeDotnetNewUninstall(GetTestTemplatePath(targetFramework), false);
                DotnetUtils.InvokeDotnetNewInstall(GetTestTemplatePath(targetFramework));
            }

            // Setup the artifacts/temp directory to not use arcade
            File.WriteAllText(Path.Combine(Constants.ArtifactsTempDirectory, "Directory.Build.props"), "<Project />");
            File.WriteAllText(Path.Combine(Constants.ArtifactsTempDirectory, "Directory.Build.targets"), "<Project />");
        }

        public void Dispose()
        {
            foreach (var targetFramework in SupportedTargetFrameworks)
            {
                DotnetUtils.InvokeDotnetNewUninstall(GetTestTemplatePath(targetFramework));
            }
        }
    }

    public AcceptanceTests(TemplateFixture fixture) { }

    public void Dispose()
    {
        // Per-test cleanup if needed
    }

    [Theory]
    [MemberData(nameof(GetTemplateItemsToTest))]
    public void ItemTemplate_CanBeInstalledAndTestArePassing(string targetFramework, string projectTemplate, string itemTemplate,
        string language, bool isTestingPlatform)
    {
        string testProjectName = GenerateTestProjectName();
        string outputDirectory = Path.Combine(Constants.ArtifactsTempDirectory, testProjectName);

        // Create new test project: dotnet new <projectTemplate> -n <testProjectName> -f <targetFramework> -lang <language>
        DotnetUtils.InvokeDotnetNew(projectTemplate, testProjectName, targetFramework, language, outputDirectory);

        var itemName = "test";

        // Add test item to test project: dotnet new <itemTemplate> -n <test> -lang <language> -o <testProjectName>
        DotnetUtils.InvokeDotnetNew(itemTemplate, itemName, language: language, outputDirectory: outputDirectory);

        if (language == Languages.FSharp)
        {
            // f# projects don't include all files by default, so the file is created
            // but the project ignores it until you manually add it into the project
            // in the right order
            AddItemToFsproj(itemName, outputDirectory, testProjectName);
        }

        // Run tests: dotnet test <path>
        var result = DotnetUtils.InvokeDotnetTest(outputDirectory);

        // Verify the tests run as expected.
        result.ValidateSummaryStatus(isTestingPlatform, 2);

        Directory.Delete(outputDirectory, true);
    }

    [Theory]
    [MemberData(nameof(GetTemplateProjectsToTest))]
    public void ProjectTemplate_CanBeInstalledAndTestsArePassing(string targetFramework, string projectTemplate, string language,
        bool runDotnetTest, bool isTestingPlatform)
    {
        var testProjectName = GenerateTestProjectName();
        string outputDirectory = Path.Combine(Constants.ArtifactsTempDirectory, testProjectName);

        // Create new test project: dotnet new <projectTemplate> -n <testProjectName> -f <targetFramework> -lang <language> -o <outputDirectory>
        var dotnetNewResult = DotnetUtils.InvokeDotnetNew(projectTemplate, testProjectName, targetFramework, language, outputDirectory);
        Assert.Equal(0, dotnetNewResult.ExitCode);

        if (runDotnetTest)
        {
            // Run tests: dotnet test <path>
            var result = DotnetUtils.InvokeDotnetTest(outputDirectory);

            // Verify the tests run as expected.
            result.ValidateSummaryStatus(isTestingPlatform, 1);
        }

        Directory.Delete(outputDirectory, true);
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
        var testProjectName = GenerateTestProjectName();
        string outputDirectory = Path.Combine(Constants.ArtifactsTempDirectory, testProjectName);

        // Create new project with extra args for coverage tool and test runner
        var dotnetNewResult = DotnetUtils.InvokeDotnetNew(
            projectTemplate,
            testProjectName,
            targetFramework,
            language,
            outputDirectory,
            true,
            "--coverage-tool", coverageTool,
            "--test-runner", testRunner
        );
        Assert.Equal(0, dotnetNewResult.ExitCode);

        if (runDotnetTest)
        {
            // Run tests: dotnet test <path>
            var result = DotnetUtils.InvokeDotnetTest(outputDirectory);

            // Validate the tests run as expected (isTestingPlatform: testRunner == "Microsoft.Testing.Platform")
            result.ValidateSummaryStatus(testRunner.Equals("Microsoft.Testing.Platform", StringComparison.OrdinalIgnoreCase), 1);
        }

        Directory.Delete(outputDirectory, true);
    }

    public static IEnumerable<object[]> GetTemplateItemsToTest()
    {
        foreach (var targetFramework in SupportedTargetFrameworks)
        {
            foreach (var (projectTemplate, itemTemplate, languages, supportsTestingPlatform) in AvailableItemTemplates)
            {
                foreach (var language in languages)
                {
                    yield return new object[] { targetFramework, projectTemplate, itemTemplate, language, supportsTestingPlatform };
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
                    yield return new object[] { targetFramework, projectTemplate, language, runDotnetTest, supportsTestingPlatform };
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

    private static string GenerateTestProjectName()
    {
        // Avoiding VB errors because root namespace must not start with number or contain dashes
        return "Test_" + Guid.NewGuid().ToString("N");
    }

    private void AddItemToFsproj(string itemName, string outputDirectory, string projectName)
    {
        var fsproj = Path.Combine(outputDirectory, $"{projectName}.fsproj");
        var lines = File.ReadAllLines(fsproj).ToList();

        lines.Insert(lines.IndexOf("  <ItemGroup>") + 1, $@"    <Compile Include=""{itemName}.fs""/>");
        File.WriteAllLines(fsproj, lines);
    }

    private static string GetTestTemplatePath(string targetFramework)
    {
        // Strip the "net" prefix from the target framework
        string version = targetFramework.StartsWith("net", StringComparison.OrdinalIgnoreCase)
            ? targetFramework.Substring(3)
            : targetFramework;

        var repoRoot = Path.Combine(Path.GetDirectoryName(typeof(AcceptanceTests).Assembly.Location) ?? string.Empty, @"..\..\..\..\..");
        Console.WriteLine($"repoRoot: {repoRoot}");

        // Combine the repo root with the rest of the path
        return Path.Combine(repoRoot, "template_feed", "Microsoft.DotNet.Common.ProjectTemplates." + version, "content");
    }
}
