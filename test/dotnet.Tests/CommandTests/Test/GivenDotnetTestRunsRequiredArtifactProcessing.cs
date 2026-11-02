// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;
using Microsoft.DotNet.Cli.Utils;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public class GivenDotnetTestRunsRequiredArtifactProcessing : SdkTest
{
    [TestMethod]
    [DataRow(false, false, 0, 2, 1, 0, 1)]
    [DataRow(true, false, 2, 5, 2, 1, 2)]
    [DataRow(false, true, 0, 2, 1, 0, 1)]
    [DataRow(true, true, 2, 5, 2, 1, 2)]
    public void RequiredSummary_UsesRealWorkerForSingleAndMultipleModules(
        bool multipleModules, bool useAppHost, int exitCode, int total, int passed, int failed, int skipped)
    {
        TestAsset asset = CreateAsset();
        CommandResult result = Run(asset, multipleModules, useAppHost: useAppHost);

        AssertSummary(asset, result, exitCode, total, passed, failed, skipped, multipleModules ? 2 : 1);
        result.StdOut.Should().Contain("name=Overall test results - ")
            .And.NotContain("name=Test results - ");
        Directory.GetFiles(Path.Combine(asset.Path, "TestResults"), "*.md", SearchOption.AllDirectories)
            .Should().ContainSingle();
        if (useAppHost)
        {
            using JsonDocument manifest = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(asset.Path, "TestResults", "worker-manifest.json")));
            foreach (JsonElement input in manifest.RootElement.GetProperty("inputs").EnumerateArray())
            {
                input.GetProperty("producingTestModule").GetString().Should().EndWith(".dll",
                    "the managed producer behind an apphost is not the selected executable");
                input.GetProperty("targetFramework").ValueKind.Should().Be(JsonValueKind.Null,
                    "test-module selection does not establish a compilation target framework");
            }
        }
    }

    [TestMethod]
    [DataRow("minimum", 9, 2, 1, 1)]
    [DataRow("ignored-minimum", 0, 2, 1, 1)]
    [DataRow("zero", 8, 0, 0, 0)]
    [DataRow("ignored-zero", 0, 0, 0, 0)]
    [DataRow("zero-minimum", 9, 0, 0, 0)]
    public void RequiredSummary_ReceivesFinalMinimumZeroAndIgnoredExitPolicies(
        string policy, int exitCode, int total, int passed, int skipped)
    {
        TestAsset asset = CreateAsset();
        bool zeroTests = policy.Contains("zero", StringComparison.Ordinal);
        string[] sdkArguments = policy.Contains("minimum", StringComparison.Ordinal)
            ? ["--minimum-expected-tests", "3"]
            : [];
        string[] testArguments = policy.StartsWith("ignored-", StringComparison.Ordinal)
            ? ["--ignore-exit-code", zeroTests ? "8" : "9"]
            : [];

        CommandResult result = Run(asset, multipleModules: false, zeroTests ? "zero" : "", sdkArguments, testArguments);

        AssertSummary(asset, result, exitCode, total, passed, 0, skipped, 1);
    }

    [TestMethod]
    public void RequiredSummary_IgnoredFailureKeepsLiveFailuresButUsesSuccessfulVerdict()
    {
        TestAsset asset = CreateAsset();

        CommandResult result = Run(asset, multipleModules: true, testArguments: ["--ignore-exit-code", "2"]);

        AssertSummary(asset, result, 0, 5, 2, 1, 2, 2);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RequiredSummary_ExplicitOptOutPreservesPerModuleFallback(bool multipleModules)
    {
        TestAsset asset = CreateAsset();

        CommandResult result = Run(asset, multipleModules, sdkArguments: ["--no-artifact-post-processing"]);

        result.ExitCode.Should().Be(multipleModules ? 2 : 0);
        result.StdOut.Should().Contain("name=Test results - ")
            .And.NotContain("name=Overall test results - ");
        File.Exists(Path.Combine(asset.Path, "TestResults", "worker-manifest.json")).Should().BeFalse();
        Directory.GetFiles(Path.Combine(asset.Path, "TestResults"), "*.md", SearchOption.AllDirectories)
            .Should().HaveCount(multipleModules ? 2 : 1);
    }

    [TestMethod]
    [DataRow("fail", false)]
    [DataRow("fail", true)]
    [DataRow("missing", false)]
    [DataRow("partial", false)]
    public void RequiredSummary_WorkerFailureDegradesReportingWithoutChangingTestVerdict(string mode, bool multipleModules)
    {
        TestAsset asset = CreateAsset();

        CommandResult result = Run(asset, multipleModules, mode);

        result.ExitCode.Should().Be(multipleModules ? 2 : 0);
        result.StdOut.Should().Contain("Reporting: degraded")
            .And.NotContain("name=Overall test results - ");
        File.Exists(Path.Combine(asset.Path, "TestResults", "worker-manifest.json")).Should().BeTrue();
        Directory.GetFiles(Path.Combine(asset.Path, "TestResults"), "*.md", SearchOption.AllDirectories)
            .Should().BeEmpty();
    }

    [TestMethod]
    public async Task RequiredWorker_FirstCancellationBoundsUncooperativeProcessLifetime()
    {
        TestAsset asset = TestAssetsManager.CopyTestAsset("ConsoleAppDoesNothing", Guid.NewGuid().ToString()).WithSource();
        string readyPath = Path.Combine(asset.Path, "worker-ready.txt");
        File.WriteAllText(Path.Combine(asset.Path, "Program.cs"), """
            System.IO.File.WriteAllText(System.Environment.GetEnvironmentVariable("REQUIRED_WORKER_READY"), System.Environment.ProcessId.ToString());
            System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
            """);
        new BuildCommand(asset).Execute().Should().Pass();

        string targetPath = Path.Combine(asset.Path, "bin", "Debug", ToolsetInfo.CurrentTargetFramework, "ConsoleAppDoesNothing.dll");
        var module = new TestModule(
            new RunProperties(SdkTestContext.Current.ToolsetUnderTest.DotNetHostPath, $"exec \"{targetPath}\"", asset.Path),
            ProjectFullPath: null, ToolsetInfo.CurrentTargetFramework, IsTestingPlatformApplication: true,
            LaunchSettings: null, targetPath, DotnetRootArchVariableName: null,
            EnvironmentVariables: new Dictionary<string, string> { ["REQUIRED_WORKER_READY"] = readyPath });
        var buildOptions = new BuildOptions(
            new PathOptions(null, null, null, null, ResultsDirectoryLayout.Flat, null, null),
            false, false, null, false, false, [], [], null, false, new Dictionary<string, string>());
        using var reporter = new TerminalTestReporter(new CapturingConsole(), new TerminalTestReporterOptions { AnsiMode = AnsiMode.NoAnsi });
        using var application = new TestApplication(
            module, buildOptions, new TestOptions(false, false, TestListFormat.Text, IsArtifactPostProcessing: true),
            TestResultsDirectoryResolver.CreateShared(buildOptions.PathOptions, asset.Path), reporter, _ => { },
            artifactPostProcessingInvocation: new ArtifactPostProcessingInvocation("unused-manifest.json"));
        using var cancellation = new CtrlCCancellationManager(() => { }, exitAction: _ => { }, subscribeToConsole: false);
        Task<int> run = application.RunAsync(cancellation);
        try
        {
            var readyClock = Stopwatch.StartNew();
            while (!File.Exists(readyPath) && readyClock.Elapsed < TimeSpan.FromSeconds(30))
            {
                await Task.Delay(25, TestContext.CancellationToken);
            }

            File.Exists(readyPath).Should().BeTrue("the real worker must have started before cancellation");
            cancellation.SimulateCtrlC();
            await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.CancellationToken);
        }
        finally
        {
            if (!run.IsCompleted)
            {
                cancellation.SimulateCtrlC();
                cancellation.SimulateCtrlC();
                await run.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
            }
        }
    }

    private static void AssertSummary(
        TestAsset asset, CommandResult result, int exitCode, int total, int passed, int failed, int skipped, int modules)
    {
        result.ExitCode.Should().Be(exitCode, $"stdout: {result.StdOut}{Environment.NewLine}stderr: {result.StdErr}");
        string manifestPath = Path.Combine(asset.Path, "TestResults", "worker-manifest.json");
        File.Exists(manifestPath).Should().BeTrue("the real required worker must run even for a single fragment");
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement summary = manifest.RootElement.GetProperty("runSummary");
        summary.GetProperty("exitCode").GetInt32().Should().Be(exitCode);
        summary.GetProperty("totalTests").GetInt64().Should().Be(total);
        summary.GetProperty("passedTests").GetInt64().Should().Be(passed);
        summary.GetProperty("failedTests").GetInt64().Should().Be(failed);
        summary.GetProperty("skippedTests").GetInt64().Should().Be(skipped);
        summary.GetProperty("testModuleCount").GetInt32().Should().Be(modules);
        summary.GetProperty("durationTicks").GetInt64().Should().BeGreaterThan(0);
        manifest.RootElement.GetProperty("inputs").GetArrayLength().Should().Be(modules);
        result.StdOut.Should().NotContain("Reporting: degraded");
    }

    private CommandResult Run(
        TestAsset asset, bool multipleModules, string mode = "", string[]? sdkArguments = null,
        string[]? testArguments = null, bool useAppHost = false)
    {
        string[] selection = multipleModules
            ? []
            : ["--project", Path.Combine("OtherTestProject", "OtherTestProject.csproj")];
        if (useAppHost)
        {
            new BuildCommand(asset, "OtherTestProject").Execute().Should().Pass();
            if (multipleModules)
            {
                new BuildCommand(asset, "TestProject").Execute().Should().Pass();
            }

            string executableExtension = OperatingSystem.IsWindows() ? ".exe" : string.Empty;
            selection =
            [
                "--test-modules", Path.Combine(
                    multipleModules ? "**" : "OtherTestProject", "bin", "Debug", ToolsetInfo.CurrentTargetFramework,
                    "*TestProject" + executableExtension),
            ];
        }

        var command = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(asset.Path)
            .WithEnvironmentVariable("TF_BUILD", "true")
            .WithEnvironmentVariable("REQUIRED_PROCESSING_TEST_MODE", mode);
        command.EnvironmentToRemove.AddRange(
            ["TESTINGPLATFORM_DOTNETTEST_EXECUTIONID", "TESTINGPLATFORM_EXITCODE_IGNORE", "SYSTEM_ACCESSTOKEN", "SYSTEM_COLLECTIONURI"]);
        return command.Execute(
        [
            .. selection, .. useAppHost ? Array.Empty<string>() : ["--configuration", "Debug"],
            "--no-ansi", "--no-progress",
            "--results-directory", Path.Combine(asset.Path, "TestResults"),
            .. sdkArguments ?? [],
            "--", "--report-azdo", "--report-azdo-summary", .. testArguments ?? [],
        ]);
    }

    private TestAsset CreateAsset()
    {
        TestAsset asset = TestAssetsManager.CopyTestAsset("MultiTestProjectSolutionWithTests", Guid.NewGuid().ToString()).WithSource();
        foreach (string projectPath in Directory.GetFiles(asset.Path, "*TestProject.csproj", SearchOption.AllDirectories))
        {
            XDocument project = XDocument.Load(projectPath);
            project.Root!.Add(new XElement("ItemGroup", new XElement("PackageReference",
                new XAttribute("Include", "Microsoft.Testing.Extensions.AzureDevOpsReport"),
                new XAttribute("Version", "$(MicrosoftTestingPlatformVersion)"))));
            project.Save(projectPath);
        }

        foreach (string programPath in Directory.GetFiles(asset.Path, "Program.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(programPath);
            int noisyOutputStart = source.IndexOf("for (int i = 0; i < 3; i++)", StringComparison.Ordinal);
            int builderStart = source.IndexOf("var testApplicationBuilder =", StringComparison.Ordinal);
            source = source.Remove(noisyOutputStart, builderStart - noisyOutputStart)
                .Replace("using Microsoft.Testing.Platform.Builder;", """
                    using Microsoft.Testing.Extensions;
                    using System.Text.Json;
                    using Microsoft.Testing.Platform.Builder;
                    """, StringComparison.Ordinal)
                .Replace("var testApplicationBuilder =", """
                    if (args.Contains("internal-merge-artifacts"))
                    {
                        string manifestPath = args[Array.IndexOf(args, "--manifest") + 1];
                        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
                        string output = manifest.RootElement.GetProperty("outputDirectory").GetString()!;
                        File.Copy(manifestPath, Path.Combine(output, "worker-manifest.json"));
                        string? mode = Environment.GetEnvironmentVariable("REQUIRED_PROCESSING_TEST_MODE");
                        if (mode == "fail")
                        {
                            return 5;
                        }
                        if (mode is "missing" or "partial")
                        {
                            string input = manifest.RootElement.GetProperty("inputs")[0].GetProperty("path").GetString()!;
                            if (mode == "missing")
                            {
                                File.Move(input, input + ".original");
                            }
                            else
                            {
                                File.WriteAllText(input, "{\"schemaVersion\":");
                            }
                        }
                    }

                    var testApplicationBuilder =
                    """, StringComparison.Ordinal)
                .Replace("testApplicationBuilder.RegisterTestFramework", """
                    testApplicationBuilder.AddAzureDevOpsProvider();
                    testApplicationBuilder.RegisterTestFramework
                    """, StringComparison.Ordinal);
            int firstResult = source.IndexOf("await context.MessageBus.PublishAsync", StringComparison.Ordinal);
            source = source.Insert(firstResult, """
                if (Environment.GetEnvironmentVariable("REQUIRED_PROCESSING_TEST_MODE") == "zero")
                {
                    context.Complete();
                    return;
                }

                """);
            File.WriteAllText(programPath, source);
        }

        return asset;
    }
}
