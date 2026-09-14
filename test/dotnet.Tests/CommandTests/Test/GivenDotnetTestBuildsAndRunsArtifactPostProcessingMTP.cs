// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Utils;
using ExitCodes = Microsoft.NET.TestFramework.ExitCode;

namespace Microsoft.DotNet.Cli.Test.Tests;

[TestClass]
public class GivenDotnetTestBuildsAndRunsArtifactPostProcessingMTP : SdkTest
{
    [TestMethod]
    public void MultiProjectRun_MergesTrxArtifacts()
    {
        TestAsset testInstance = TestAssetsManager
            .CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString())
            .WithSource();
        EnableTrxReport(testInstance.Path);
        string resultsDirectory = Path.Combine(testInstance.Path, "TestResults");

        CommandResult firstResult = Run(testInstance.Path, resultsDirectory);
        string firstMergedTrxPath = GetMergedTrxPath(firstResult);

        File.Exists(firstMergedTrxPath).Should().BeTrue();
        Path.GetFileName(firstMergedTrxPath).Should().MatchRegex("^merged-[0-9a-f]{32}\\.trx$");
        Directory.GetFiles(resultsDirectory, "*.trx", SearchOption.AllDirectories)
            .Should().HaveCount(3, "the two original reports remain on disk beside the merged report");

        CommandResult secondResult = Run(testInstance.Path, resultsDirectory);
        string secondMergedTrxPath = GetMergedTrxPath(secondResult);

        secondMergedTrxPath.Should().NotBe(
            firstMergedTrxPath,
            "each invocation has distinct execution IDs and must produce a non-colliding merged report");
        File.Exists(secondMergedTrxPath).Should().BeTrue();
        Directory.GetFiles(resultsDirectory, "*.trx", SearchOption.AllDirectories)
            .Should().HaveCount(4, "the original reports are overwritten while each merged run is preserved");

        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        XDocument mergedTrx = XDocument.Load(secondMergedTrxPath);
        mergedTrx.Descendants(ns + "Counters").Single().Attribute("total")!.Value.Should().Be("5");
    }

    [TestMethod]
    public void MultiProjectRun_MergesCodeCoverageArtifacts()
    {
        TestAsset testInstance = TestAssetsManager
            .CopyTestAsset("TestProjectSolutionWithCodeCoverage", Guid.NewGuid().ToString())
            .WithSource();
        string solutionPath = CreateMultiProjectCoverageSolution(testInstance);

        string unmergedResultsDirectory = Path.Combine(testInstance.Path, "UnmergedTestResults");
        CommandResult unmergedResult = RunCoverage(
            testInstance.Path,
            solutionPath,
            unmergedResultsDirectory,
            "--no-artifact-post-processing");

        AssertTestFailures(unmergedResult);
        string[] unmergedCoveragePaths =
            Directory.GetFiles(unmergedResultsDirectory, "*.coverage", SearchOption.AllDirectories);
        unmergedCoveragePaths.Should().HaveCount(2, "each test application keeps its own coverage artifact");
        GetReportedCoveragePaths(unmergedResult).Should().BeEquivalentTo(unmergedCoveragePaths);

        string mergedResultsDirectory = Path.Combine(testInstance.Path, "MergedTestResults");
        CommandResult mergedResult = RunCoverage(testInstance.Path, solutionPath, mergedResultsDirectory);

        AssertTestFailures(mergedResult);
        string mergedCoveragePath = GetReportedCoveragePaths(mergedResult).Should().ContainSingle().Subject;
        File.Exists(mergedCoveragePath).Should().BeTrue();
        string[] mergedCoveragePaths =
            Directory.GetFiles(mergedResultsDirectory, "*.coverage", SearchOption.AllDirectories);
        mergedCoveragePaths.Should().HaveCount(3, "the two original reports remain on disk beside the merged report");
        mergedCoveragePaths.Should().Contain(mergedCoveragePath);
    }

    [TestMethod]
    public void MultiProjectRun_WithNoArtifactPostProcessing_KeepsOneReportPerTestApplication()
    {
        TestAsset testInstance = TestAssetsManager
            .CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString())
            .WithSource();
        EnableTrxReport(testInstance.Path);
        string resultsDirectory = Path.Combine(testInstance.Path, "TestResults");

        CommandResult result = Run(testInstance.Path, resultsDirectory, "--no-artifact-post-processing");

        result.ExitCode.Should().Be(
            ExitCodes.AtLeastOneTestFailed,
            $"the test output was:{Environment.NewLine}{result.StdOut}{Environment.NewLine}{result.StdErr}");

        string[] trxReports = Directory.GetFiles(resultsDirectory, "*.trx", SearchOption.AllDirectories);
        trxReports.Should().HaveCount(2, "no test application is relaunched to merge the reports");
        trxReports.Should().NotContain(path => Path.GetFileName(path).StartsWith("merged-", StringComparison.Ordinal));
    }

    [TestMethod]
    public void SingleTestApplication_ProducesOneReport_WithNoMergedReport()
    {
        TestAsset testInstance = TestAssetsManager
            .CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString())
            .WithSource();
        EnableTrxReport(testInstance.Path);
        string resultsDirectory = Path.Combine(testInstance.Path, "TestResults");

        // Scoping the multi-project solution to a single project yields exactly one test application,
        // so the planner never sees the >= 2 inputs it requires to plan a merge.
        CommandResult result = Run(
            testInstance.Path,
            resultsDirectory,
            "--project",
            $"TestProject{Path.DirectorySeparatorChar}TestProject.csproj");

        result.ExitCode.Should().Be(
            ExitCodes.AtLeastOneTestFailed,
            $"the test output was:{Environment.NewLine}{result.StdOut}{Environment.NewLine}{result.StdErr}");

        string[] trxReports = Directory.GetFiles(resultsDirectory, "*.trx", SearchOption.AllDirectories);
        trxReports.Should().ContainSingle("a single test application produces exactly one TRX report");
        trxReports.Should().NotContain(
            path => Path.GetFileName(path).StartsWith("merged-", StringComparison.Ordinal),
            "a single input never satisfies the planner's >= 2 inputs rule, so no test application is relaunched to merge");
    }

    [TestMethod]
    public void RunCutShortByMaximumFailedTests_DoesNotRunIneligibleTrxProcessor()
    {
        TestAsset testInstance = TestAssetsManager
            .CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString())
            .WithSource();
        EnableTrxReport(testInstance.Path);
        string resultsDirectory = Path.Combine(testInstance.Path, "TestResults");

        CommandResult result = Run(testInstance.Path, resultsDirectory, "--maximum-failed-tests", "1");

        result.ExitCode.Should().Be(
            ExitCodes.TestExecutionStoppedForMaxFailedTests,
            $"the test output was:{Environment.NewLine}{result.StdOut}{Environment.NewLine}{result.StdErr}");

        // The run is parallel, so how many modules survived to write a TRX before the policy tripped is
        // timing dependent; only the absence of a merged report is deterministic.
        string[] trxReports = Directory.Exists(resultsDirectory)
            ? Directory.GetFiles(resultsDirectory, "*.trx", SearchOption.AllDirectories)
            : [];
        trxReports.Should().NotContain(
            path => Path.GetFileName(path).StartsWith("merged-", StringComparison.Ordinal),
            "the TRX processor does not opt into policy-truncated runs");

        // The progress line is printed as soon as post-processing has anything planned, so its absence
        // shows no eligible group was planned. (For the same timing reason as above this cannot prove
        // that two reports existed, so it is a guard against ineligible TRX processing rather than a
        // proof that a merge was averted.)
        result.StdOut.Should().NotContain(
            CliCommandStrings.ArtifactPostProcessingStarted,
            "a processor that did not opt in must not run for a truncated test run");
    }

    [TestMethod]
    public void TestModulesRun_MergesTrxArtifacts()
    {
        TestAsset testInstance = TestAssetsManager
            .CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString())
            .WithSource();
        EnableTrxReport(testInstance.Path);

        new BuildCommand(testInstance, "TestProject").Execute().Should().Pass();
        new BuildCommand(testInstance, "OtherTestProject").Execute().Should().Pass();

        string resultsDirectory = Path.Combine(testInstance.Path, "TestResults");
        string modulesFilter = $"**/bin/**/Debug/{ToolsetInfo.CurrentTargetFramework}/*TestProject.dll"
            .Replace('/', Path.DirectorySeparatorChar);

        CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .Execute("--test-modules", modulesFilter, "--report-trx", "--results-directory", resultsDirectory);

        string mergedTrxPath = GetMergedTrxPath(result);
        File.Exists(mergedTrxPath).Should().BeTrue();
        Path.GetFileName(mergedTrxPath).Should().MatchRegex("^merged-[0-9a-f]{32}\\.trx$");
        Directory.GetFiles(resultsDirectory, "merged-*.trx", SearchOption.AllDirectories)
            .Should().ContainSingle("post-processing is orchestrated independently of --test-modules discovery");

        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        int TotalCount(string trxPath) => int.Parse(
            XDocument.Load(trxPath).Descendants(ns + "Counters").Single().Attribute("total")!.Value);

        int individualTotal = Directory.GetFiles(resultsDirectory, "*.trx", SearchOption.AllDirectories)
            .Where(path => !Path.GetFileName(path).StartsWith("merged-", StringComparison.Ordinal))
            .Sum(TotalCount);
        TotalCount(mergedTrxPath).Should().Be(
            individualTotal,
            "the merged report accounts for every test from each per-module report");
    }

    private CommandResult Run(string workingDirectory, string resultsDirectory, params string[] additionalArguments)
    {
        string[] arguments =
        [
            "--report-trx",
            "--results-directory", resultsDirectory,
            "--configuration", TestingConstants.Debug,
            .. additionalArguments
        ];

        Microsoft.NET.TestFramework.Commands.TestCommand command = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(workingDirectory);

        // This test suite itself runs under Microsoft.Testing.Platform, so its process already
        // carries an execution id. A test application only generates its own when the variable is
        // unset, so without this the applications launched by the inner 'dotnet test' would adopt
        // this harness's id and every invocation would look like the same execution. Removing it
        // from the child environment restores what a normal 'dotnet test' sees: one fresh execution
        // id per invocation.
        command.EnvironmentToRemove.Add("TESTINGPLATFORM_DOTNETTEST_EXECUTIONID");

        return command.Execute(arguments);
    }

    private CommandResult RunCoverage(
        string workingDirectory,
        string solutionPath,
        string resultsDirectory,
        params string[] additionalArguments)
    {
        string[] arguments =
        [
            "--solution", solutionPath,
            "--coverage",
            "--results-directory", resultsDirectory,
            "--configuration", TestingConstants.Debug,
            .. additionalArguments
        ];

        Microsoft.NET.TestFramework.Commands.TestCommand command = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(workingDirectory);
        command.EnvironmentToRemove.Add("TESTINGPLATFORM_DOTNETTEST_EXECUTIONID");

        return command.Execute(arguments);
    }

    private static string CreateMultiProjectCoverageSolution(TestAsset testInstance)
    {
        string firstProjectDirectory = Path.Combine(testInstance.Path, "TestProject");
        string secondProjectDirectory = Path.Combine(testInstance.Path, "OtherTestProject");
        Directory.CreateDirectory(secondProjectDirectory);

        foreach (string fileName in new[] { "Program.cs", "Test1.cs" })
        {
            string source = File.ReadAllText(Path.Combine(firstProjectDirectory, fileName))
                .Replace("namespace TestProject", "namespace OtherTestProject", StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(secondProjectDirectory, fileName), source);
        }

        string firstProjectPath = Path.Combine(firstProjectDirectory, "TestProject.csproj");
        string secondProjectPath = Path.Combine(secondProjectDirectory, "OtherTestProject.csproj");
        File.Copy(firstProjectPath, secondProjectPath);

        string versionsRoot = SdkTestContext.Current.ToolsetUnderTest.RepoRoot
            ?? SdkTestContext.Current.TestExecutionDirectory;
        string versionsPropsPath = Path.Combine(versionsRoot, "eng", "Version.Details.props");
        string mstestVersion = testInstance.ReadMSTestPackageVersionFromProps(versionsPropsPath);
        testInstance.UpdateProjectFileWithMSTestPackageVersion(firstProjectPath, mstestVersion);
        testInstance.UpdateProjectFileWithMSTestPackageVersion(secondProjectPath, mstestVersion);

        string solutionPath = Path.Combine(testInstance.Path, "CoverageTests.slnx");
        File.WriteAllText(
            solutionPath,
            """
            <Solution>
              <Project Path="OtherTestProject/OtherTestProject.csproj" />
              <Project Path="TestProject/TestProject.csproj" />
            </Solution>
            """);
        return solutionPath;
    }

    private static void AssertTestFailures(CommandResult result)
        => result.ExitCode.Should().Be(
            ExitCodes.AtLeastOneTestFailed,
            $"the test output was:{Environment.NewLine}{result.StdOut}{Environment.NewLine}{result.StdErr}");

    private static string[] GetReportedCoveragePaths(CommandResult result)
        =>
        [
            .. Regex.Matches(
                    result.StdOut ?? string.Empty,
                    @"(?m)^\s*-\s+(?<path>.*\.coverage)\s*$",
                    RegexOptions.CultureInvariant)
                .Select(match => match.Groups["path"].Value)
        ];

    private static string GetMergedTrxPath(CommandResult result)
    {
        result.ExitCode.Should().Be(
            ExitCodes.AtLeastOneTestFailed,
            $"the test output was:{Environment.NewLine}{result.StdOut}{Environment.NewLine}{result.StdErr}");

        MatchCollection artifactMatches = Regex.Matches(
            result.StdOut ?? string.Empty,
            @"(?m)^\s*-\s+(?<path>.*\.trx)\s*$",
            RegexOptions.CultureInvariant);
        artifactMatches.Should().ContainSingle();

        string mergedTrxPath = artifactMatches[0].Groups["path"].Value;
        return mergedTrxPath;
    }

    private static void EnableTrxReport(string testAssetPath)
    {
        foreach (string projectPath in Directory.GetFiles(testAssetPath, "*TestProject.csproj", SearchOption.AllDirectories))
        {
            XDocument project = XDocument.Load(projectPath);
            XElement packageReferenceGroup = project.Root!
                .Elements("ItemGroup")
                .Single(group => group.Elements("PackageReference").Any());
            packageReferenceGroup.Elements("PackageReference")
                .Single(reference => (string?)reference.Attribute("Include") == "Microsoft.Testing.Platform")
                .SetAttributeValue("Version", "$(MicrosoftTestingPlatformVersion)");
            packageReferenceGroup.Add(new XElement(
                "PackageReference",
                new XAttribute("Include", "Microsoft.Testing.Extensions.TrxReport"),
                new XAttribute("Version", "$(MicrosoftTestingPlatformVersion)")));
            project.Save(projectPath);
        }

        foreach (string programPath in Directory.GetFiles(testAssetPath, "Program.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(programPath)
                .Replace(
                    """
                    for (int i = 0; i < 3; i++)
                    {
                    	Console.WriteLine(new string('a', 10000));
                    	Console.Error.WriteLine(new string('e', 10000));
                    }

                    """,
                    string.Empty,
                    StringComparison.Ordinal)
                .Replace(
                    "using Microsoft.Testing.Platform.Builder;",
                    """
                    using Microsoft.Testing.Extensions;
                    using Microsoft.Testing.Extensions.TrxReport.Abstractions;
                    using Microsoft.Testing.Platform.Builder;
                    """,
                    StringComparison.Ordinal)
                .Replace(
                    "new TestFrameworkCapabilities()",
                    "new TestFrameworkCapabilities(new TrxReportCapability())",
                    StringComparison.Ordinal)
                .Replace(
                    "testApplicationBuilder.RegisterTestFramework",
                    """
                    testApplicationBuilder.AddTrxReportProvider();

                    testApplicationBuilder.RegisterTestFramework
                    """,
                    StringComparison.Ordinal)
                .Replace(
                    """
                    	public async Task ExecuteRequestAsync(ExecuteRequestContext context)
                    	{
                    """,
                    """
                    	public async Task ExecuteRequestAsync(ExecuteRequestContext context)
                    	{
                    		var testMethodIdentifier = new TestMethodIdentifierProperty(
                    			string.Empty, string.Empty, nameof(DummyTestAdapter), "Test", 0, [], string.Empty);
                    """,
                    StringComparison.Ordinal)
                .Replace(
                    """new PassedTestNodeStateProperty("OK"))""",
                    """new PassedTestNodeStateProperty("OK"), testMethodIdentifier)""",
                    StringComparison.Ordinal)
                .Replace(
                    """new SkippedTestNodeStateProperty("OK skipped!"))""",
                    """new SkippedTestNodeStateProperty("OK skipped!"), testMethodIdentifier)""",
                    StringComparison.Ordinal)
                .Replace(
                    """new SkippedTestNodeStateProperty("skipped"))""",
                    """new SkippedTestNodeStateProperty("skipped"), testMethodIdentifier)""",
                    StringComparison.Ordinal)
                .Replace(
                    """new FailedTestNodeStateProperty(new Exception("this is a failed test"), "not OK"))""",
                    """new FailedTestNodeStateProperty(new Exception("this is a failed test"), "not OK"), testMethodIdentifier)""",
                    StringComparison.Ordinal)
                + """

                public sealed class TrxReportCapability : ITrxReportCapability
                {
                    bool ITrxReportCapability.IsSupported => true;
                    void ITrxReportCapability.Enable()
                    {
                    }
                }
                """;
            File.WriteAllText(programPath, source);
        }
    }
}
