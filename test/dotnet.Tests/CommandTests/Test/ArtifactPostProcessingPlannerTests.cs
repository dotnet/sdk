// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Commands.Test;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public class ArtifactPostProcessingPlannerTests
{
    [TestMethod]
    public void Plan_OneApplicationCoversAllGroups_CreatesOneJob()
    {
        ArtifactPostProcessingApplication application = CreateApplication(
            "A.dll",
            "net10.0",
            "x64",
            ["microsoft.testing.trx", "example.junit"],
            []);
        ArtifactPostProcessingArtifact[] artifacts =
        [
            CreateArtifact("A-1.trx", "microsoft.testing.trx", "A.dll", "x64"),
            CreateArtifact("B-1.trx", "microsoft.testing.trx", "B.dll", "x64"),
            CreateArtifact("A-1.xml", "example.junit", "A.dll", "x64"),
            CreateArtifact("B-1.xml", "example.junit", "B.dll", "x64"),
        ];

        ArtifactPostProcessingPlan plan = ArtifactPostProcessingPlanner.Plan([application], artifacts);

        plan.Jobs.Should().ContainSingle();
        plan.Jobs[0].Application.Should().BeSameAs(application);
        plan.Jobs[0].Groups.Select(group => group.Key)
            .Should().BeEquivalentTo("microsoft.testing.trx", "example.junit");
    }

    [TestMethod]
    public void Plan_ApplicationCoveringMostGroups_WinsMinimalSetCover()
    {
        ArtifactPostProcessingApplication trxOnly = CreateApplication(
            "A.dll",
            "net10.0",
            "x64",
            ["microsoft.testing.trx"],
            []);
        ArtifactPostProcessingApplication both = CreateApplication(
            "B.dll",
            "net9.0",
            "x64",
            ["microsoft.testing.trx", "example.junit"],
            []);
        ArtifactPostProcessingArtifact[] artifacts =
        [
            CreateArtifact("A.trx", "microsoft.testing.trx", "A.dll", "x64"),
            CreateArtifact("B.trx", "microsoft.testing.trx", "B.dll", "x64"),
            CreateArtifact("A.xml", "example.junit", "A.dll", "x64"),
            CreateArtifact("B.xml", "example.junit", "B.dll", "x64"),
        ];

        ArtifactPostProcessingPlan plan = ArtifactPostProcessingPlanner.Plan([trxOnly, both], artifacts);

        plan.Jobs.Should().ContainSingle();
        plan.Jobs[0].Application.Should().BeSameAs(both);
    }

    [TestMethod]
    public void Plan_SplitCapabilities_CreatesOneJobPerApplication()
    {
        ArtifactPostProcessingApplication trx = CreateApplication(
            "A.dll",
            "net10.0",
            "x64",
            ["microsoft.testing.trx"],
            []);
        ArtifactPostProcessingApplication junit = CreateApplication(
            "B.dll",
            "net10.0",
            "x64",
            ["example.junit"],
            []);
        ArtifactPostProcessingArtifact[] artifacts =
        [
            CreateArtifact("A.trx", "microsoft.testing.trx", "A.dll", "x64"),
            CreateArtifact("B.trx", "microsoft.testing.trx", "B.dll", "x64"),
            CreateArtifact("A.xml", "example.junit", "A.dll", "x64"),
            CreateArtifact("B.xml", "example.junit", "B.dll", "x64"),
        ];

        ArtifactPostProcessingPlan plan = ArtifactPostProcessingPlanner.Plan([trx, junit], artifacts);

        plan.Jobs.Should().HaveCount(2);
        plan.Jobs.Select(job => job.Application).Should().Contain(trx).And.Contain(junit);
    }

    [TestMethod]
    public void Plan_UntaggedArtifacts_UsesExtensionFallback()
    {
        ArtifactPostProcessingApplication application = CreateApplication(
            "A.dll",
            "net10.0",
            "x64",
            [],
            [".trx"]);
        ArtifactPostProcessingArtifact[] artifacts =
        [
            CreateArtifact("A.TRX", kind: null, "A.dll", "x64"),
            CreateArtifact("B.trx", kind: null, "B.dll", "x64"),
        ];

        ArtifactPostProcessingPlan plan = ArtifactPostProcessingPlanner.Plan([application], artifacts);

        plan.Jobs.Should().ContainSingle();
        plan.Jobs[0].Groups.Should().ContainSingle();
        plan.Jobs[0].Groups[0].Key.Should().Be(".trx");
        plan.Jobs[0].Groups[0].IsKind.Should().BeFalse();
    }

    [TestMethod]
    public void Plan_TaggedAndLegacyArtifactsTogether_MeetMergeThreshold()
    {
        ArtifactPostProcessingApplication application = CreateApplication(
            "A.dll",
            "net10.0",
            "x64",
            ["microsoft.testing.trx"],
            [".trx"]);
        ArtifactPostProcessingArtifact[] artifacts =
        [
            CreateArtifact("A.trx", "microsoft.testing.trx", "A.dll", "x64"),
            CreateArtifact("B.trx", kind: null, "B.dll", "x64"),
        ];

        ArtifactPostProcessingPlan plan = ArtifactPostProcessingPlanner.Plan([application], artifacts);

        plan.Jobs.Should().ContainSingle();
        plan.Jobs[0].Groups.Should().HaveCount(2);
        plan.Jobs[0].Groups.SelectMany(group => group.Artifacts).Should().HaveCount(2);
    }

    [TestMethod]
    public void Plan_OneArtifactOrNoCapability_CreatesNoJobs()
    {
        ArtifactPostProcessingApplication application = CreateApplication(
            "A.dll",
            "net10.0",
            "x64",
            ["microsoft.testing.trx"],
            []);

        ArtifactPostProcessingPlan oneArtifact = ArtifactPostProcessingPlanner.Plan(
            [application],
            [CreateArtifact("A.trx", "microsoft.testing.trx", "A.dll", "x64")]);
        ArtifactPostProcessingPlan unsupported = ArtifactPostProcessingPlanner.Plan(
            [application],
            [
                CreateArtifact("A.xml", "example.junit", "A.dll", "x64"),
                CreateArtifact("B.xml", "example.junit", "B.dll", "x64"),
            ]);

        oneArtifact.Jobs.Should().BeEmpty();
        unsupported.Jobs.Should().BeEmpty();
    }

    [TestMethod]
    public void Plan_SameArtifactReportedTwice_DoesNotCreateJob()
    {
        ArtifactPostProcessingApplication application = CreateApplication(
            "A.dll",
            "net10.0",
            "x64",
            ["microsoft.testing.trx"],
            []);
        ArtifactPostProcessingArtifact artifact =
            CreateArtifact("A.trx", "microsoft.testing.trx", "A.dll", "x64");

        ArtifactPostProcessingPlan plan = ArtifactPostProcessingPlanner.Plan(
            [application],
            [artifact, artifact with { ExecutionId = "another-execution" }]);

        plan.Jobs.Should().BeEmpty();
    }

    [TestMethod]
    public void Plan_CodeCoverage_RequiresArchitectureCompatibleApplication()
    {
        ArtifactPostProcessingApplication x64 = CreateApplication(
            "A.dll",
            "net10.0",
            "x64",
            ["microsoft.codecoverage"],
            []);
        ArtifactPostProcessingApplication arm64 = CreateApplication(
            "B.dll",
            "net10.0",
            "arm64",
            ["microsoft.codecoverage"],
            []);
        ArtifactPostProcessingArtifact[] artifacts =
        [
            CreateArtifact("A.coverage", "microsoft.codecoverage", "A.dll", "arm64"),
            CreateArtifact("B.coverage", "microsoft.codecoverage", "B.dll", "arm64"),
        ];

        ArtifactPostProcessingPlan plan = ArtifactPostProcessingPlanner.Plan([x64, arm64], artifacts);

        plan.Jobs.Should().ContainSingle();
        plan.Jobs[0].Application.Should().BeSameAs(arm64);
    }

    private static ArtifactPostProcessingApplication CreateApplication(
        string targetPath,
        string targetFramework,
        string architecture,
        string[] kinds,
        string[] extensions)
        => new(
            new TestModule(
                new RunProperties("dotnet", targetPath, null),
                ProjectFullPath: null,
                TargetFramework: targetFramework,
                IsTestingPlatformApplication: true,
                LaunchSettings: null,
                TargetPath: targetPath,
                DotnetRootArchVariableName: null,
                EnvironmentVariables: new Dictionary<string, string>()),
            targetFramework,
            architecture,
            new HashSet<string>(kinds, StringComparer.Ordinal),
            new HashSet<string>(extensions, StringComparer.Ordinal));

    private static ArtifactPostProcessingArtifact CreateArtifact(
        string path,
        string? kind,
        string producingTestModule,
        string architecture)
        => new(
            path,
            kind,
            producingTestModule,
            "net10.0",
            architecture,
            Guid.NewGuid().ToString("N"));
}
