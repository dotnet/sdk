// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using System.Xml.Linq;
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

        CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .Execute(
                "--report-trx",
                "--results-directory", resultsDirectory,
                "--configuration", TestingConstants.Debug);

        result.ExitCode.Should().Be(
            ExitCodes.AtLeastOneTestFailed,
            $"the test output was:{Environment.NewLine}{result.StdOut}{Environment.NewLine}{result.StdErr}");

        MatchCollection artifactMatches = Regex.Matches(
            result.StdOut ?? string.Empty,
            @"(?m)^\s*-\s+(?<path>.*\.trx)\s*$",
            RegexOptions.CultureInvariant);
        artifactMatches.Should().ContainSingle();

        string mergedTrxPath = artifactMatches[0].Groups["path"].Value;
        File.Exists(mergedTrxPath).Should().BeTrue();
        Directory.GetFiles(resultsDirectory, "*.trx", SearchOption.AllDirectories)
            .Should().HaveCount(3, "the two original reports remain on disk beside the merged report");

        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        XDocument mergedTrx = XDocument.Load(mergedTrxPath);
        mergedTrx.Descendants(ns + "Counters").Single().Attribute("total")!.Value.Should().Be("5");
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
