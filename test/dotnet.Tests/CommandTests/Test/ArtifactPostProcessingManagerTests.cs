// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;
using TestExitCode = Microsoft.DotNet.Cli.Commands.Test.ExitCode;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public class ArtifactPostProcessingManagerTests
{
    [TestMethod]
    public void ApplyOutputs_MatchingKind_ReplacesOriginalArtifacts()
    {
        var console = new CapturingConsole();
        using var reporter = CreateReporter(console);
        ArtifactPostProcessingArtifact first = CreateArtifact("first.trx", "microsoft.testing.trx");
        ArtifactPostProcessingArtifact second = CreateArtifact("second.trx", "microsoft.testing.trx");
        ArtifactPostProcessingApplication application = CreateApplication();
        var group = new ArtifactPostProcessingGroup(
            "microsoft.testing.trx",
            IsKind: true,
            [first, second],
            [application]);
        var job = new ArtifactPostProcessingJob(application, [group]);
        reporter.ArtifactAdded(false, "A.dll", "net10.0", "x64", "execution-1", null, first.Path);
        reporter.ArtifactAdded(false, "B.dll", "net10.0", "x64", "execution-2", null, second.Path);
        ArtifactPostProcessingArtifact merged = CreateArtifact("merged.trx", "microsoft.testing.trx");

        ArtifactPostProcessingManager.ApplyOutputs(reporter, job, [merged]);
        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, TestExitCode.Success);

        string output = console.GetOutput();
        output.Should().Contain("merged.trx");
        output.Should().NotContain("first.trx");
        output.Should().NotContain("second.trx");
    }

    [TestMethod]
    public void ApplyOutputs_UnmatchedOutput_StillReportsOutputAndPreservesOriginals()
    {
        var console = new CapturingConsole();
        using var reporter = CreateReporter(console);
        ArtifactPostProcessingArtifact first = CreateArtifact("first.coverage", "microsoft.codecoverage");
        ArtifactPostProcessingArtifact second = CreateArtifact("second.coverage", "microsoft.codecoverage");
        ArtifactPostProcessingApplication application = CreateApplication();
        var group = new ArtifactPostProcessingGroup(
            "microsoft.codecoverage",
            IsKind: true,
            [first, second],
            [application]);
        var job = new ArtifactPostProcessingJob(application, [group]);
        reporter.ArtifactAdded(false, "A.dll", "net10.0", "x64", "execution-1", null, first.Path);
        reporter.ArtifactAdded(false, "B.dll", "net10.0", "x64", "execution-2", null, second.Path);
        ArtifactPostProcessingArtifact converted = CreateArtifact("coverage.cobertura.xml", "cobertura");

        ArtifactPostProcessingManager.ApplyOutputs(reporter, job, [converted]);
        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, TestExitCode.Success);

        string output = console.GetOutput();
        output.Should().Contain("coverage.cobertura.xml");
        output.Should().Contain("first.coverage");
        output.Should().Contain("second.coverage");
    }

    [TestMethod]
    public void ApplyOutputs_KindOutput_AlsoConsumesLegacyInputsWithSameExtension()
    {
        var console = new CapturingConsole();
        using var reporter = CreateReporter(console);
        ArtifactPostProcessingArtifact taggedFirst = CreateArtifact("tagged-first.xml", "example.junit");
        ArtifactPostProcessingArtifact taggedSecond = CreateArtifact("tagged-second.xml", "example.junit");
        ArtifactPostProcessingArtifact legacyFirst = CreateArtifact("legacy-first.xml", kind: null);
        ArtifactPostProcessingArtifact legacySecond = CreateArtifact("legacy-second.xml", kind: null);
        ArtifactPostProcessingApplication application = CreateApplication();
        var taggedGroup = new ArtifactPostProcessingGroup(
            "example.junit",
            IsKind: true,
            [taggedFirst, taggedSecond],
            [application]);
        var fallbackGroup = new ArtifactPostProcessingGroup(
            ".xml",
            IsKind: false,
            [legacyFirst, legacySecond],
            [application]);
        var job = new ArtifactPostProcessingJob(application, [taggedGroup, fallbackGroup]);
        foreach (ArtifactPostProcessingArtifact artifact in taggedGroup.Artifacts.Concat(fallbackGroup.Artifacts))
        {
            reporter.ArtifactAdded(false, "A.dll", "net10.0", "x64", artifact.ExecutionId, null, artifact.Path);
        }

        ArtifactPostProcessingManager.ApplyOutputs(
            reporter,
            job,
            [CreateArtifact("merged.xml", "example.junit")]);
        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, TestExitCode.Success);

        string output = console.GetOutput();
        output.Should().Contain("merged.xml");
        output.Should().NotContain("tagged-first.xml");
        output.Should().NotContain("tagged-second.xml");
        output.Should().NotContain("legacy-first.xml");
        output.Should().NotContain("legacy-second.xml");
    }

    [TestMethod]
    public void GetArtifactPostProcessingLaunchArguments_DotnetCommand_UsesOnlyExecAndTargetPath()
    {
        ArtifactPostProcessingApplication application = CreateApplication();

        string arguments = TestApplication.GetArtifactPostProcessingLaunchArguments(application.Module);

        arguments.Should().Be("exec A.dll");
    }

    [TestMethod]
    public void GetArtifactPostProcessingLaunchArguments_AppHost_UsesNoTestArguments()
    {
        ArtifactPostProcessingApplication application = CreateApplication();
        TestModule appHostModule = application.Module with
        {
            RunProperties = new RunProperties("A.exe", "--filter injected", null),
        };

        string arguments = TestApplication.GetArtifactPostProcessingLaunchArguments(appHostModule);

        arguments.Should().BeEmpty();
    }

    private static TerminalTestReporter CreateReporter(CapturingConsole console)
    {
        var reporter = new TerminalTestReporter(console, new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.SimpleAnsi,
            ShowProgress = false,
        });
        reporter.TestExecutionStarted(
            DateTimeOffset.UtcNow,
            workerCount: 1,
            isDiscovery: false,
            isHelp: false,
            isRetry: false);
        return reporter;
    }

    private static ArtifactPostProcessingApplication CreateApplication()
    {
        var module = new TestModule(
            new RunProperties("dotnet", "A.dll", null),
            ProjectFullPath: null,
            TargetFramework: "net10.0",
            IsTestingPlatformApplication: true,
            LaunchSettings: null,
            TargetPath: "A.dll",
            DotnetRootArchVariableName: null,
            EnvironmentVariables: new Dictionary<string, string>());
        return new ArtifactPostProcessingApplication(
            module,
            "net10.0",
            "x64",
            new HashSet<string>(StringComparer.Ordinal) { "microsoft.testing.trx", "microsoft.codecoverage" },
            new HashSet<string>(StringComparer.Ordinal));
    }

    private static ArtifactPostProcessingArtifact CreateArtifact(string path, string? kind)
        => new(path, kind, "A.dll", "net10.0", "x64", Guid.NewGuid().ToString("N"));
}
