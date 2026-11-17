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
    public void Plan_RequiredSingleton_DoesNotProcessOptionalSingleton()
    {
        ArtifactPostProcessingApplication application = CreateApplication(
            "A.dll", "net10.0", "x64", ["example.summary", "microsoft.testing.trx"], [],
            requiredKinds: ["example.summary"]);
        ArtifactPostProcessingArtifact[] artifacts =
        [
            CreateArtifact("A.summary", "example.summary", "A.dll", "x64"),
            CreateArtifact("A.trx", "microsoft.testing.trx", "A.dll", "x64"),
        ];

        ArtifactPostProcessingPlan plan = ArtifactPostProcessingPlanner.Plan([application], artifacts);

        ArtifactPostProcessingGroup group = plan.Jobs.Should().ContainSingle().Subject.Groups.Should().ContainSingle().Subject;
        group.Key.Should().Be("example.summary");
        group.Artifacts.Should().ContainSingle().Which.Path.Should().Be("A.summary");
    }

    [TestMethod]
    public void Plan_RequiredGroup_ElectsApplicationAdvertisingRequiredSupport()
    {
        ArtifactPostProcessingApplication optional = CreateApplication(
            "A.dll", "net11.0", "x64", ["example.summary"], []);
        ArtifactPostProcessingApplication required = CreateApplication(
            "B.dll", "net10.0", "x64", ["example.summary"], [], requiredKinds: ["example.summary"]);

        ArtifactPostProcessingPlan plan = ArtifactPostProcessingPlanner.Plan(
            [optional, required],
            [CreateArtifact("A.summary", "example.summary", "A.dll", "x64")]);

        plan.Jobs.Should().ContainSingle().Which.Application.Should().BeSameAs(required);
    }

    [TestMethod]
    public void Plan_RequiredKindWithoutSupportedKind_DoesNotCreateJob()
    {
        ArtifactPostProcessingApplication application = CreateApplication(
            "A.dll", "net10.0", "x64", [], [], requiredKinds: ["example.summary"]);

        ArtifactPostProcessingPlan plan = ArtifactPostProcessingPlanner.Plan(
            [application],
            [CreateArtifact("A.summary", "example.summary", "A.dll", "x64")]);

        plan.Jobs.Should().BeEmpty();
    }

    [TestMethod]
    public void Plan_RequiredKindWithoutInputs_DoesNotInventJob()
    {
        ArtifactPostProcessingApplication application = CreateApplication(
            "A.dll", "net10.0", "x64", ["example.summary"], [], requiredKinds: ["example.summary"]);

        ArtifactPostProcessingPlanner.Plan([application], []).Jobs.Should().BeEmpty();
    }

    [TestMethod]
    public void Plan_DuplicateRequiredInput_IsProcessedOnce()
    {
        ArtifactPostProcessingApplication application = CreateApplication(
            "A.dll", "net10.0", "x64", ["example.summary"], [], requiredKinds: ["example.summary"]);
        ArtifactPostProcessingArtifact artifact = CreateArtifact("A.summary", "example.summary", "A.dll", "x64");

        ArtifactPostProcessingPlan plan = ArtifactPostProcessingPlanner.Plan([application], [artifact, artifact]);

        plan.Jobs.Should().ContainSingle().Which.Groups.Should().ContainSingle().Which.Artifacts.Should().ContainSingle();
    }

    [TestMethod]
    [DataRow((int)TestRunCancellationReason.MaximumFailedTests, false)]
    [DataRow((int)TestRunCancellationReason.MaximumFailedTests, true)]
    [DataRow((int)TestRunCancellationReason.Timeout, false)]
    [DataRow((int)TestRunCancellationReason.Timeout, true)]
    public void Plan_TruncatedSingleton_RequiresExplicitTruncationCapability(int reason, bool supportsTruncation)
    {
        ArtifactPostProcessingApplication application = CreateApplication(
            "A.dll", "net10.0", "x64", ["example.summary"], [],
            truncatedRunKinds: supportsTruncation ? ["example.summary"] : [],
            requiredKinds: ["example.summary"]);

        ArtifactPostProcessingPlan plan = ArtifactPostProcessingPlanner.Plan(
            [application],
            [CreateArtifact("A.summary", "example.summary", "A.dll", "x64")],
            (TestRunCancellationReason)reason);

        plan.Jobs.Should().HaveCount(supportsTruncation ? 1 : 0);
    }

    [TestMethod]
    public void Plan_TruncatedOptionalSingleton_UsesAdvertisedTruncationSupport()
    {
        ArtifactPostProcessingApplication application = CreateApplication(
            "A.dll", "net10.0", "x64", ["example.summary"], [], truncatedRunKinds: ["example.summary"]);

        ArtifactPostProcessingPlan plan = ArtifactPostProcessingPlanner.Plan(
            [application],
            [CreateArtifact("A.summary", "example.summary", "A.dll", "x64")],
            TestRunCancellationReason.Timeout);

        plan.Jobs.Should().ContainSingle();
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

    [TestMethod]
    public void Plan_DifferentKindsSharingAnExtension_AreNotMerged()
    {
        // JUnit and NUnit3 both write '.xml'. Extension-only matching would collapse them into one
        // group and hand a JUnit merger a set of NUnit3 reports; the kind is what keeps them apart.
        ArtifactPostProcessingApplication application = CreateApplication(
            "A.dll",
            "net10.0",
            "x64",
            ["example.junit", "example.nunit3"],
            [".xml"]);
        ArtifactPostProcessingArtifact[] artifacts =
        [
            CreateArtifact("A-junit.xml", "example.junit", "A.dll", "x64"),
            CreateArtifact("B-junit.xml", "example.junit", "B.dll", "x64"),
            CreateArtifact("A-nunit.xml", "example.nunit3", "A.dll", "x64"),
            CreateArtifact("B-nunit.xml", "example.nunit3", "B.dll", "x64"),
        ];

        ArtifactPostProcessingPlan plan = ArtifactPostProcessingPlanner.Plan([application], artifacts);

        plan.Jobs.Should().ContainSingle();
        ArtifactPostProcessingGroup[] groups = [.. plan.Jobs[0].Groups];
        groups.Select(group => group.Key).Should().BeEquivalentTo("example.junit", "example.nunit3");
        groups.Should().OnlyContain(
            group => group.Artifacts.Count == 2,
            "each kind keeps its own inputs even though both write the same file extension");
    }

    [TestMethod]
    public void Plan_TaggedArtifact_IsNotAlsoRoutedThroughTheExtensionFallback()
    {
        // A tagged artifact must never appear in both its kind group and the fallback group for its
        // extension: the merge tool would then receive it twice and double-count its tests.
        ArtifactPostProcessingApplication application = CreateApplication(
            "A.dll",
            "net10.0",
            "x64",
            ["microsoft.testing.trx"],
            [".trx"]);
        ArtifactPostProcessingArtifact[] artifacts =
        [
            CreateArtifact("A.trx", "microsoft.testing.trx", "A.dll", "x64"),
            CreateArtifact("B.trx", "microsoft.testing.trx", "B.dll", "x64"),
            CreateArtifact("C.trx", kind: null, "C.dll", "x64"),
        ];

        ArtifactPostProcessingPlan plan = ArtifactPostProcessingPlanner.Plan([application], artifacts);

        string[] plannedPaths =
            [.. plan.Jobs.SelectMany(job => job.Groups).SelectMany(group => group.Artifacts).Select(artifact => artifact.Path)];
        plannedPaths.Should().OnlyHaveUniqueItems();
        plannedPaths.Should().BeEquivalentTo("A.trx", "B.trx", "C.trx");
    }

    [TestMethod]
    [DataRow((int)TestRunCancellationReason.MaximumFailedTests)]
    [DataRow((int)TestRunCancellationReason.Timeout)]
    public void Plan_PolicyTruncatedRun_OnlyIncludesOptedInKinds(int cancellationReason)
    {
        ArtifactPostProcessingApplication application = CreateApplication(
            "A.dll",
            "net10.0",
            "x64",
            ["microsoft.testing.trx", "example.summary"],
            [],
            truncatedRunKinds: ["example.summary"]);
        ArtifactPostProcessingArtifact[] artifacts =
        [
            CreateArtifact("A.trx", "microsoft.testing.trx", "A.dll", "x64"),
            CreateArtifact("B.trx", "microsoft.testing.trx", "B.dll", "x64"),
            CreateArtifact("A.summary", "example.summary", "A.dll", "x64"),
            CreateArtifact("B.summary", "example.summary", "B.dll", "x64"),
        ];

        ArtifactPostProcessingPlan plan = ArtifactPostProcessingPlanner.Plan(
            [application],
            artifacts,
            (TestRunCancellationReason)cancellationReason);

        plan.Jobs.Should().ContainSingle();
        plan.Jobs[0].Groups.Should().ContainSingle();
        plan.Jobs[0].Groups[0].Key.Should().Be("example.summary");
    }

    [TestMethod]
    [DataRow((int)TestRunCancellationReason.MaximumFailedTests)]
    [DataRow((int)TestRunCancellationReason.Timeout)]
    public void Plan_PolicyTruncatedRun_OnlyIncludesOptedInExtensions(int cancellationReason)
    {
        ArtifactPostProcessingApplication application = CreateApplication(
            "A.dll",
            "net10.0",
            "x64",
            [],
            [".trx", ".summary"],
            truncatedRunExtensions: [".summary"]);
        ArtifactPostProcessingArtifact[] artifacts =
        [
            CreateArtifact("A.trx", kind: null, "A.dll", "x64"),
            CreateArtifact("B.trx", kind: null, "B.dll", "x64"),
            CreateArtifact("A.summary", kind: null, "A.dll", "x64"),
            CreateArtifact("B.summary", kind: null, "B.dll", "x64"),
        ];

        ArtifactPostProcessingPlan plan = ArtifactPostProcessingPlanner.Plan(
            [application],
            artifacts,
            (TestRunCancellationReason)cancellationReason);

        plan.Jobs.Should().ContainSingle();
        plan.Jobs[0].Groups.Should().ContainSingle();
        plan.Jobs[0].Groups[0].Key.Should().Be(".summary");
    }

    private static ArtifactPostProcessingApplication CreateApplication(
        string targetPath,
        string targetFramework,
        string architecture,
        string[] kinds,
        string[] extensions,
        string[]? truncatedRunKinds = null,
        string[]? truncatedRunExtensions = null,
        string[]? requiredKinds = null)
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
            new HashSet<string>(extensions, StringComparer.Ordinal),
            new HashSet<string>(truncatedRunKinds ?? [], StringComparer.Ordinal),
            new HashSet<string>(truncatedRunExtensions ?? [], StringComparer.Ordinal))
        {
            RequiredKinds = new HashSet<string>(requiredKinds ?? [], StringComparer.Ordinal),
        };

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
