// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

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
                var result = new DotnetTestCommand(_log, false)
                .WithWorkingDirectory(outputDirectory)
                .Execute(outputDirectory);

                result.Should().Pass();

                result.StdOut.Should().Contain("Passed!");
                result.StdOut.Should().MatchRegex(@"Passed:\s*1");
            }

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
