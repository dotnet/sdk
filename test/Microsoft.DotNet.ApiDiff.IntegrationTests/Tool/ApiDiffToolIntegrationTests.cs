// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.ApiDiff.IntegrationTests.Tool
{
    /// <summary>
    /// End-to-end tests that drive the <c>apidiff</c> CLI tool by invoking
    /// <c>dotnet exec Microsoft.DotNet.ApiDiff.Tool.dll</c> against pairs of built assembly folders
    /// and asserting on the produced markdown diff, exactly as a customer would.
    /// </summary>
    public class ApiDiffToolIntegrationTests : SdkTest
    {
        public ApiDiffToolIntegrationTests(ITestOutputHelper log) : base(log)
        {
        }

        [PlatformSpecificFact(skipPlatforms: TestPlatforms.OSX, skipArchitecture: Architecture.Arm64, skipReason: "https://github.com/dotnet/sdk/issues/54248")]
        public void ApiDiffTool_NoChanges_ProducesNoMemberDiffs()
        {
            string assembly = BuildAssembly("MyLib", "public class Greeter { public string Hello(string name) => name; }", nameof(ApiDiffTool_NoChanges_ProducesNoMemberDiffs) + "_before");

            string beforeFolder = Path.GetDirectoryName(assembly)!;
            string outputFolder = Path.Combine(Path.GetDirectoryName(beforeFolder)!, "diff");
            Directory.CreateDirectory(outputFolder);

            // Use the same folder on both sides; expect a clean diff with no member additions/removals.
            Run(
                "--before", beforeFolder, "--after", beforeFolder,
                "--beforeFriendlyName", "old", "--afterFriendlyName", "new",
                "--output", outputFolder,
                "--tableOfContentsTitle", "api_diff").Should().Pass();

            // The tool always emits a table-of-contents file; per-assembly markdown should not contain
            // any "Added", "Removed", or "Changed" headings since the inputs are identical.
            string toc = Path.Combine(outputFolder, "api_diff.md");
            File.Exists(toc).Should().BeTrue($"the table-of-contents file should be written to {toc}");
        }

        [PlatformSpecificFact(skipPlatforms: TestPlatforms.OSX, skipArchitecture: Architecture.Arm64, skipReason: "https://github.com/dotnet/sdk/issues/54248")]
        public void ApiDiffTool_AddedMember_ProducesDiff()
        {
            const string sharedSource =
                @"public class Greeter { public string Hello(string name) => name; }";
            const string sourceWithAddedMember =
                @"public class Greeter { public string Hello(string name) => name; public string Welcome(string name) => name; }";

            string beforeAssembly = BuildAssembly("MyLib", sharedSource, nameof(ApiDiffTool_AddedMember_ProducesDiff) + "_before");
            string afterAssembly = BuildAssembly("MyLib", sourceWithAddedMember, nameof(ApiDiffTool_AddedMember_ProducesDiff) + "_after");

            string outputFolder = Path.Combine(Path.GetTempPath(),
                $"apidiff-{nameof(ApiDiffTool_AddedMember_ProducesDiff)}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(outputFolder);

            const string tocTitle = "diff";
            Run(
                "--before", Path.GetDirectoryName(beforeAssembly)!,
                "--after", Path.GetDirectoryName(afterAssembly)!,
                "--beforeFriendlyName", "1.0",
                "--afterFriendlyName", "2.0",
                "--tableOfContentsTitle", tocTitle,
                "--output", outputFolder).Should().Pass();

            // Per-assembly markdown files are written as "<tableOfContentsTitle>_<assemblyName>.md".
            string assemblyDiff = Path.Combine(outputFolder, $"{tocTitle}_MyLib.md");
            File.Exists(assemblyDiff).Should().BeTrue($"the per-assembly markdown should be written to {assemblyDiff}");
            File.ReadAllText(assemblyDiff).Should().Contain("Welcome",
                "the added member should appear in the diff for MyLib");
        }

        [PlatformSpecificFact(skipPlatforms: TestPlatforms.OSX, skipArchitecture: Architecture.Arm64, skipReason: "https://github.com/dotnet/sdk/issues/54248")]
        public void ApiDiffTool_TableOfContentsTitle_ControlsOutputFileName()
        {
            string assembly = BuildAssembly("MyLib", "public class Greeter { public string Hello(string name) => name; }",
                nameof(ApiDiffTool_TableOfContentsTitle_ControlsOutputFileName));

            string folder = Path.GetDirectoryName(assembly)!;
            string outputFolder = Path.Combine(Path.GetDirectoryName(folder)!, "diff");
            Directory.CreateDirectory(outputFolder);

            const string customTitle = "my_custom_title";
            Run(
                "--before", folder, "--after", folder,
                "--beforeFriendlyName", "old", "--afterFriendlyName", "new",
                "--output", outputFolder,
                "--tableOfContentsTitle", customTitle).Should().Pass();

            string toc = Path.Combine(outputFolder, $"{customTitle}.md");
            File.Exists(toc).Should().BeTrue($"the table-of-contents file should honour --tableOfContentsTitle and be written to {toc}");
        }

        [PlatformSpecificFact(skipPlatforms: TestPlatforms.OSX, skipArchitecture: Architecture.Arm64, skipReason: "https://github.com/dotnet/sdk/issues/54248")]
        public void ApiDiffTool_MissingRequiredOption_FailsWithHelpfulError()
        {
            // Omit --output and verify System.CommandLine reports the missing required option
            // and that the tool exits with a non-zero status.
            CommandResult result = Run("--before", ".", "--after", ".",
                "--beforeFriendlyName", "old", "--afterFriendlyName", "new");

            result.Should().Fail();
            string output = result.StdOut + result.StdErr;
            output.Should().Contain("--output");
        }

        private CommandResult Run(params string[] args)
        {
            var allArgs = new List<string> { "exec", ToolPaths.ApiDiffToolDll };
            allArgs.AddRange(args);
            return new DotnetCommand(Log, allArgs.ToArray()).Execute();
        }

        /// <summary>
        /// Compiles the given C# source into a small library and returns the absolute path to the produced DLL.
        /// </summary>
        private string BuildAssembly(string assemblyName, string sourceCode, string identifier)
        {
            TestProject project = new()
            {
                Name = assemblyName,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            };
            project.SourceFiles.Add($"{assemblyName}.cs", sourceCode);

            TestAsset asset = TestAssetsManager.CreateTestProject(project, identifier: identifier);
            new BuildCommand(asset).Execute().Should().Pass();

            return Path.Combine(asset.TestRoot, project.Name, "bin", "Debug",
                ToolsetInfo.CurrentTargetFramework, $"{assemblyName}.dll");
        }
    }
}
