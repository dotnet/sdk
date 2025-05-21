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

        //var assemblyDir = Path.GetDirectoryName(typeof(AcceptanceTests).Assembly.Location) ?? string.Empty;
        var assemblyDir = Path.GetFullPath(Path.GetDirectoryName(typeof(AcceptanceTests).Assembly.Location) ?? string.Empty);

        Console.WriteLine($"Listing contents of assembly.location: {assemblyDir}");

        if (Directory.Exists(assemblyDir))
        {
            var directories = Directory.GetDirectories(assemblyDir);
            var files = Directory.GetFiles(assemblyDir);

            Console.WriteLine("Directories:");
            foreach (var dir in directories)
            {
                Console.WriteLine("directory1: " + dir);
            }

            Console.WriteLine("Files:");
            foreach (var file in files)
            {
                Console.WriteLine("file1: " + file);
            }
        }
        else
        {
            Console.WriteLine("Directory does not exist: " + assemblyDir);
        }

        //var repoRoot = GetAndVerifyRepoRoot();
        var assemblyDir1 = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(AcceptanceTests).Assembly.Location) ?? string.Empty, "..", "..", ".."));

        Console.WriteLine($"Listing contents of assembly.location with changes: {assemblyDir1}");

        if (Directory.Exists(assemblyDir1))
        {
            var directories = Directory.GetDirectories(assemblyDir1);
            var files = Directory.GetFiles(assemblyDir1);

            Console.WriteLine("Directories:");
            foreach (var dir in directories)
            {
                Console.WriteLine("directory2: " + dir);
            }

            Console.WriteLine("Files:");
            foreach (var file in files)
            {
                Console.WriteLine("file2: " + file);
            }
        }
        else
        {
            Console.WriteLine("Directory does not exist: " + assemblyDir1);
        }

        if (Directory.Exists(Path.Join(assemblyDir1, "template_feed")))
        {
            Console.WriteLine(" assemblyDir1 " + Path.Join(assemblyDir1, "template_feed"));
        }

        var assemblyDir2 = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(AcceptanceTests).Assembly.Location) ?? string.Empty, "..", "..", ".."));

        if (Directory.Exists(Path.Join(assemblyDir2, "template_feed")))
        {
            Console.WriteLine(" assemblyDir2 " + Path.Join(assemblyDir2, "template_feed"));
        }

        var assemblyDir3 = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(AcceptanceTests).Assembly.Location) ?? string.Empty, ".."));

        if (Directory.Exists(Path.Join(assemblyDir3, "template_feed")))
        {
            Console.WriteLine(" assemblyDir3 " + Path.Join(assemblyDir3, "template_feed"));
        }

        var assemblyDir4 = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(AcceptanceTests).Assembly.Location) ?? string.Empty, "..", "..", "..", ".."));

        if (Directory.Exists(Path.Join(assemblyDir4, "template_feed")))
        {
            Console.WriteLine(" assemblyDir4 " + Path.Join(assemblyDir4, "template_feed"));
        }

        var assemblyDir12 = Path.Combine(Path.GetDirectoryName(typeof(AcceptanceTests).Assembly.Location) ?? string.Empty, "..", "..");


        if (Directory.Exists(Path.Join(assemblyDir12, "template_feed")))
        {
            Console.WriteLine(" assemblyDir12 " + Path.Join(assemblyDir12, "template_feed"));
        }

        var assemblyDir22 = Path.Combine(Path.GetDirectoryName(typeof(AcceptanceTests).Assembly.Location) ?? string.Empty, "..", "..", "..");

        if (Directory.Exists(Path.Join(assemblyDir22, "template_feed")))
        {
            Console.WriteLine(" assemblyDir22 " + Path.Join(assemblyDir22, "template_feed"));
        }

        var assemblyDir32 = Path.Combine(Path.GetDirectoryName(typeof(AcceptanceTests).Assembly.Location) ?? string.Empty, "..");

        if (Directory.Exists(Path.Join(assemblyDir32, "template_feed")))
        {
            Console.WriteLine(" assemblyDir32 " + Path.Join(assemblyDir32, "template_feed"));
        }

        var assemblyDir42 = Path.Combine(Path.GetDirectoryName(typeof(AcceptanceTests).Assembly.Location) ?? string.Empty, "..", "..", "..", "..");

        if (Directory.Exists(Path.Join(assemblyDir42, "template_feed")))
        {
            Console.WriteLine(" assemblyDir42 " + Path.Join(assemblyDir42, "template_feed"));
        }

        var assemblyDir5 = Path.Combine(Path.GetDirectoryName(typeof(AcceptanceTests).Assembly.Location) ?? string.Empty, "..", "..", "..", "..", "..");

        if (Directory.Exists(Path.Join(assemblyDir5, "template_feed")))
        {
            Console.WriteLine(" assemblyDir5 " + Path.Join(assemblyDir5, "template_feed"));
        }

        var assemblyDir6 = Path.Combine(Path.GetDirectoryName(typeof(AcceptanceTests).Assembly.Location) ?? string.Empty, "..", "..", "..", "..", "..", "..");

        if (Directory.Exists(Path.Join(assemblyDir6, "template_feed")))
        {
            Console.WriteLine(" assemblyDir6 " + Path.Join(assemblyDir6, "template_feed"));
        }

        ////Console.WriteLine($"repoRoot: {repoRoot}");

        //var path = Path.Combine(assemblyDir, "template_feed");
        //// Combine the repo root with the rest of the path
        //Console.WriteLine($"templateFeedPath: {assemblyDir}");

        //var baseDir = Path.GetDirectoryName(typeof(AcceptanceTests).Assembly.Location) ?? string.Empty;
        //var assemblyDir = Path.GetFullPath(Path.GetDirectoryName(typeof(AcceptanceTests).Assembly.Location) ?? string.Empty);
        //var baseDir = Path.GetDirectoryName(typeof(AcceptanceTests).Assembly.Location) ?? string.Empty;
        return Path.Combine(
            assemblyDir5,
            "template_feed",
            "Microsoft.DotNet.Common.ProjectTemplates." + version,
            "content"
        );
    }

    private static string GetAndVerifyRepoRoot()
    {
        string repoRoot = Path.GetFullPath(Path.Combine(TestContext.Current.TestAssetsDirectory, "..", ".."));
        if (!Directory.Exists(repoRoot))
        {
            Assert.Fail($"The repo root cannot be evaluated.");
        }
        if (!File.Exists(Path.Combine(repoRoot, "sdk.sln")))
        {
            Assert.Fail($"The repo root doesn't contain 'sdk.sln'.");
        }
        return repoRoot;
    }
}
