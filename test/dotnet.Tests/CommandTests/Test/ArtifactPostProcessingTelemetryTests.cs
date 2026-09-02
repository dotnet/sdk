// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Commands.Test;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public class ArtifactPostProcessingTelemetryTests
{
    [TestMethod]
    public void CreateProperties_ReportsPlanShapeAndDuration()
    {
        ArtifactPostProcessingApplication application = CreateApplication();
        var plan = new ArtifactPostProcessingPlan(
        [
            new ArtifactPostProcessingJob(
                application,
                [
                    CreateGroup("microsoft.testing.trx", isKind: true, artifactCount: 3),
                    CreateGroup(".coverage", isKind: false, artifactCount: 2),
                ]),
        ]);

        Dictionary<string, string?> properties = ArtifactPostProcessingTelemetry.CreateProperties(
            plan,
            executedJobs: 1,
            failedJobs: 0,
            TimeSpan.FromMilliseconds(1234));

        properties["jobs_planned"].Should().Be("1");
        properties["jobs_executed"].Should().Be("1");
        properties["jobs_failed"].Should().Be("0");
        properties["artifact_count"].Should().Be("5");
        properties["kinds"].Should().Be("microsoft.testing.trx");
        properties["extensions"].Should().Be(".coverage");
        properties["duration_ms"].Should().Be("1234");
    }

    [TestMethod]
    public void CreateProperties_UnknownKindsAndExtensions_AreBucketed()
    {
        // Kinds and file extensions are chosen by whoever wrote the producing extension, so an
        // in-house post-processor can carry a product or team name. Only shipped formats are
        // reported verbatim; everything else has to collapse into a single opaque bucket.
        ArtifactPostProcessingApplication application = CreateApplication();
        var plan = new ArtifactPostProcessingPlan(
        [
            new ArtifactPostProcessingJob(
                application,
                [
                    CreateGroup("contoso.internal.telemetry", isKind: true, artifactCount: 2),
                    CreateGroup("fabrikam.audit", isKind: true, artifactCount: 2),
                    CreateGroup("microsoft.testing.trx", isKind: true, artifactCount: 2),
                    CreateGroup(".contoso", isKind: false, artifactCount: 2),
                ]),
        ]);

        Dictionary<string, string?> properties = ArtifactPostProcessingTelemetry.CreateProperties(
            plan,
            executedJobs: 1,
            failedJobs: 1,
            TimeSpan.Zero);

        properties["kinds"].Should().Be(
            "microsoft.testing.trx;other",
            "the two private kinds must collapse into one bucket rather than being uploaded");
        properties["extensions"].Should().Be("other");
    }

    [TestMethod]
    public void CreateProperties_NeverReportsArtifactPaths()
    {
        ArtifactPostProcessingApplication application = CreateApplication();
        var artifact = new ArtifactPostProcessingArtifact(
            "/repo/src/Contoso.Secret.Tests/bin/Debug/TestResults/report.trx",
            "microsoft.testing.trx",
            "Contoso.Secret.Tests.dll",
            "net10.0",
            "x64",
            "execution-1");
        var plan = new ArtifactPostProcessingPlan(
        [
            new ArtifactPostProcessingJob(
                application,
                [new ArtifactPostProcessingGroup("microsoft.testing.trx", IsKind: true, [artifact, artifact], [application])]),
        ]);

        Dictionary<string, string?> properties = ArtifactPostProcessingTelemetry.CreateProperties(
            plan,
            executedJobs: 1,
            failedJobs: 0,
            TimeSpan.Zero);

        properties.Values.Should().NotContain(value => value != null && value.Contains("Contoso"));
    }

    private static ArtifactPostProcessingGroup CreateGroup(string key, bool isKind, int artifactCount)
    {
        ArtifactPostProcessingApplication application = CreateApplication();
        ArtifactPostProcessingArtifact[] artifacts =
        [
            .. Enumerable.Range(0, artifactCount).Select(index => new ArtifactPostProcessingArtifact(
                $"artifact-{index}{(isKind ? ".trx" : key)}",
                isKind ? key : null,
                "A.dll",
                "net10.0",
                "x64",
                $"execution-{index}"))
        ];

        return new ArtifactPostProcessingGroup(key, isKind, artifacts, [application]);
    }

    private static ArtifactPostProcessingApplication CreateApplication()
        => new(
            new TestModule(
                new RunProperties("dotnet", "A.dll", null),
                ProjectFullPath: null,
                TargetFramework: "net10.0",
                IsTestingPlatformApplication: true,
                LaunchSettings: null,
                TargetPath: "A.dll",
                DotnetRootArchVariableName: null,
                EnvironmentVariables: new Dictionary<string, string>()),
            "net10.0",
            "x64",
            new HashSet<string>(StringComparer.Ordinal) { "microsoft.testing.trx" },
            new HashSet<string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal));
}
