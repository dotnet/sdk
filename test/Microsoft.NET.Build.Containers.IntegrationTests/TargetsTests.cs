// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Execution;
using Microsoft.NET.Build.Containers.IntegrationTests;
using static Microsoft.NET.Build.Containers.KnownStrings.Properties;

namespace Microsoft.NET.Build.Containers.Targets.IntegrationTests;

[TestClass]
public class TargetsTests
{
    [TestMethod]
    [DynamicData(nameof(ContainerAppCommands))]
    public void CanDeferContainerAppCommand(
        string os,
        string prop,
        bool value,
        string[] expectedAppCommandArgs)
    {
        var (project, _, d) = ProjectInitializer.InitProject(new()
        {
            [prop] = value.ToString(),
            [ContainerRuntimeIdentifier] = $"{os}-x64",

        }, projectName: $"{nameof(CanDeferContainerAppCommand)}_{prop}_{value}_{string.Join("_", expectedAppCommandArgs)}");
        using var _ = d;
        var instance = project.CreateProjectInstance(ProjectInstanceSettings.None);
        instance.Build([ComputeContainerConfig], []);
        var computedAppCommand = instance.GetItems(ContainerAppCommand).Select(i => i.EvaluatedInclude);

        // The test was not testing anything previously, as the list returned was zero length,
        // and the Zip didn't yield any results.
        // So, to make sure we actually test something, we check that we actually get the expected collection.
        computedAppCommand.Should().BeEquivalentTo(expectedAppCommandArgs);
    }

    public static IEnumerable<object[]> ContainerAppCommands()
    {
        char s = Path.DirectorySeparatorChar;
        return new List<object[]>
        {
            new object[] { "win", "SelfContained", true, new[] { $"C:{s}app{s}foo.exe" } },
            new object[] { "win", "SelfContained", false, new[] { "dotnet", $"C:{s}app{s}foo.dll" } },
            new object[] { "win", "PublishSelfContained", true, new[] { $"C:{s}app{s}foo.exe" } },
            new object[] { "win", "PublishSelfContained", false, new[] { "dotnet", $"C:{s}app{s}foo.dll" } },
            new object[] { "linux", "SelfContained", true, new[] { "/app/foo" } },
            new object[] { "linux", "SelfContained", false, new[] { "dotnet", "/app/foo.dll" } },
            new object[] { "linux", "PublishSelfContained", true, new[] { "/app/foo" } },
            new object[] { "linux", "PublishSelfContained", false, new[] { "dotnet", "/app/foo.dll" } },
        };
    }

    [TestMethod]
    public void CanDeferToContainerImageNameWhenPresent()
    {
        var customImageName = "my-container-app";
        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            [ContainerImageName] = customImageName
        });
        using var _ = d;
        var instance = project.CreateProjectInstance(ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerConfig }, new[] { logger });
        logger.Warnings.Should().HaveCount(1, "a warning for the use of the old ContainerImageName property should have been created");
        logger.Warnings[0].Code.Should().Be(KnownStrings.ErrorCodes.CONTAINER003);
        Assert.AreEqual(customImageName, instance.GetPropertyValue(ContainerRepository));
    }

    [DataRow("WebApplication44", "webapplication44", true)]
    [DataRow("friendly-suspicious-alligator", "friendly-suspicious-alligator", true)]
    [DataRow("*friendly-suspicious-alligator", "", false)]
    [DataRow("web/app2+7", "web/app2-7", true)]
    [DataRow("Microsoft.Apps.Demo.ContosoWeb", "microsoft-apps-demo-contosoweb", true)]
    [TestMethod]
    public void CanNormalizeInputContainerNames(string projectName, string expectedContainerImageName, bool shouldPass)
    {
        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            [AssemblyName] = projectName
        }, projectName: $"{nameof(CanNormalizeInputContainerNames)}_{projectName}_{expectedContainerImageName}_{shouldPass}");
        using var _ = d;
        var instance = project.CreateProjectInstance(ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerConfig }, new[] { logger }, null, out var outputs).Should().Be(shouldPass, String.Join(Environment.NewLine, logger.AllMessages));
        Assert.AreEqual(expectedContainerImageName, instance.GetPropertyValue(ContainerRepository));
    }

    [DataRow("7.0.100", true)]
    [DataRow("8.0.100", true)]
    [DataRow("7.0.100-preview.7", true)]
    [DataRow("7.0.100-rc.1", true)]
    [DataRow("6.0.100", false)]
    [DataRow("7.0.100-preview.1", false)]
    [TestMethod]
    public void CanWarnOnInvalidSDKVersions(string sdkVersion, bool isAllowed)
    {
        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            ["NETCoreSdkVersion"] = sdkVersion,
            ["PublishProfile"] = "DefaultContainer"
        }, projectName: $"{nameof(CanWarnOnInvalidSDKVersions)}_{sdkVersion}_{isAllowed}");
        using var _ = d;
        var instance = project.CreateProjectInstance(ProjectInstanceSettings.None);
        instance.Build(new[] { "_ContainerVerifySDKVersion" }, new[] { logger }, null, out var outputs).Should().Be(isAllowed);
        var derivedIsAllowed = Boolean.Parse(project.GetProperty("_IsSDKContainerAllowedVersion").EvaluatedValue);
        if (isAllowed)
        {
            logger.Errors.Should().HaveCount(0, "an error should not have been created");
            derivedIsAllowed.Should().Be(true, "SDK version {0} should have been allowed", sdkVersion);
        }
        else
        {
            logger.Errors.Should().HaveCount(1, "an error should have been created").And.Satisfy(error => error.Code == KnownStrings.ErrorCodes.CONTAINER002);
            derivedIsAllowed.Should().Be(false, "SDK version {0} should not have been allowed", sdkVersion);
        }
    }

    [DataRow(true)]
    [DataRow(false)]
    [TestMethod]
    public void GetsConventionalLabelsByDefault(bool shouldEvaluateLabels)
    {
        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            [ContainerGenerateLabels] = shouldEvaluateLabels.ToString()
        }, projectName: $"{nameof(GetsConventionalLabelsByDefault)}_{shouldEvaluateLabels}");
        using var _ = d;
        var instance = project.CreateProjectInstance(ProjectInstanceSettings.None);
        var success = instance.Build(new[] { ComputeContainerConfig }, new[] { logger }, null, out var outputs);
        success.Should().BeTrue("Build should have succeeded");
        if (shouldEvaluateLabels)
        {
            instance.GetItems(ContainerLabel).Should().NotBeEmpty("Should have evaluated some labels by default");
        }
        else
        {
            instance.GetItems(ContainerLabel).Should().BeEmpty("Should not have evaluated any labels by default");
        }
    }

    private static bool LabelMatch(string label, string value, ProjectItemInstance item) => item.EvaluatedInclude == label && item.GetMetadata("Value") is { } v && v.EvaluatedValue == value;

    [DataRow(true)]
    [DataRow(false)]
    [TestMethod]
    public void ShouldNotIncludeSourceControlLabelsUnlessUserOptsIn(bool includeSourceControl)
    {
        var commitHash = "abcdef";
        var repoUrl = "https://git.cosmere.com/shard/whimsy.git";

        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            ["PublishRepositoryUrl"] = includeSourceControl.ToString(),
            ["PrivateRepositoryUrl"] = repoUrl,
            ["SourceRevisionId"] = commitHash
        }, projectName: $"{nameof(ShouldNotIncludeSourceControlLabelsUnlessUserOptsIn)}_{includeSourceControl}");
        using var _ = d;
        var instance = project.CreateProjectInstance(ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerConfig }, new[] { logger }, null, out var outputs).Should().BeTrue("Build should have succeeded but failed due to {0}", String.Join("\n", logger.AllMessages));
        var labels = instance.GetItems(ContainerLabel);
        if (includeSourceControl)
        {
            labels.Should().NotBeEmpty("Should have evaluated some labels by default")
                .And.ContainSingle(label => LabelMatch("org.opencontainers.image.source", repoUrl, label))
                .And.ContainSingle(label => LabelMatch("org.opencontainers.image.revision", commitHash, label)); ;
        }
        else
        {
            labels.Should().NotBeEmpty("Should have evaluated some labels by default")
                .And.NotContain(label => LabelMatch("org.opencontainers.image.source", repoUrl, label))
                .And.NotContain(label => LabelMatch("org.opencontainers.image.revision", commitHash, label)); ;
        };
    }

    [DataRow("https://git.cosmere.com/shard/whimsy.git", "https://git.cosmere.com/shard/whimsy")]
    [DataRow("https://repos.git.cosmere.com/shard/whimsy.git", "https://repos.git.cosmere.com/shard/whimsy")]
    [TestMethod]
    public void ShouldTrimTrailingGitSuffixFromRepoUrls(string repoUrl, string expectedLabel)
    {
        var commitHash = "abcdef";

        static string NormalizeString(string s) => s.Replace(':', '_').Replace('/', '_');

        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            ["PublishRepositoryUrl"] = true.ToString(),
            ["PrivateRepositoryUrl"] = repoUrl,
            ["SourceRevisionId"] = commitHash,
            ["RepositoryType"] = "git"
        }, projectName: $"{nameof(ShouldNotIncludeSourceControlLabelsUnlessUserOptsIn)}_{NormalizeString(repoUrl)}_{NormalizeString(expectedLabel)}");
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerConfig }, new[] { logger }, null, out var outputs).Should().BeTrue("Build should have succeeded but failed due to {0}", String.Join("\n", logger.AllMessages));
        var labels = instance.GetItems(ContainerLabel);

        labels.Should().NotBeEmpty("Should have evaluated some labels by default")
            .And.ContainSingle(label => LabelMatch("org.opencontainers.image.source", expectedLabel, label), String.Join(",", logger.AllMessages));
    }

    [DataRow(true)]
    [DataRow(false)]
    [TestMethod]
    public void ShouldIncludeBaseImageLabelsUnlessUserOptsOut(bool includeBaseImageLabels)
    {
        var expectedBaseImage = "mcr.microsoft.com/dotnet/runtime:7.0";
        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            ["ContainerGenerateLabelsImageBaseName"] = includeBaseImageLabels.ToString(),
            ["ContainerBaseImage"] = expectedBaseImage,
            ["ContainerGenerateLabels"] = true.ToString()
        }, projectName: $"{nameof(ShouldIncludeBaseImageLabelsUnlessUserOptsOut)}_{includeBaseImageLabels}");
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerConfig }, new[] { logger }, null, out var outputs).Should().BeTrue("Build should have succeeded but failed due to {0}", String.Join("\n", logger.AllMessages));
        var labels = instance.GetItems(ContainerLabel);
        if (includeBaseImageLabels)
        {
            labels.Should().NotBeEmpty("Should have evaluated some labels by default")
                .And.ContainSingle(label => LabelMatch("org.opencontainers.image.base.name", expectedBaseImage, label));
        }
        else
        {
            labels.Should().NotBeEmpty("Should have evaluated some labels by default")
                .And.NotContain(label => LabelMatch("org.opencontainers.image.base.name", expectedBaseImage, label));
        };
    }

    [DataRow(true)]
    [DataRow(false)]
    [TestMethod]
    public void ShouldIncludeSDKAndRuntimeVersionLabelsUnlessUserOptsOut(bool includeToolsetVersionLabels)
    {
        var runtimeMajorMinor = "7.0";
        var randomSdkVersion = "8.0.100";
        var expectedBaseImage = $"mcr.microsoft.com/dotnet/runtime:{runtimeMajorMinor}";
        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            ["ContainerGenerateLabelsDotnetToolset"] = includeToolsetVersionLabels.ToString(),
            ["ContainerBaseImage"] = expectedBaseImage,
            ["ContainerGenerateLabels"] = true.ToString(), // always include other labels, but not necessarily the toolset labels
            ["NETCoreSdkVersion"] = randomSdkVersion // not functionally relevant for the test, just need a known version
        }, projectName: $"{nameof(ShouldIncludeSDKAndRuntimeVersionLabelsUnlessUserOptsOut)}_{includeToolsetVersionLabels}");
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerConfig }, new[] { logger }, null, out var outputs).Should().BeTrue("Build should have succeeded but failed due to {0}", String.Join("\n", logger.AllMessages));
        var labels = instance.GetItems(ContainerLabel);
        if (includeToolsetVersionLabels)
        {
            labels.Should().NotBeEmpty("Should have evaluated some labels by default")
                .And.ContainSingle(label => LabelMatch("net.dot.runtime.majorminor", runtimeMajorMinor, label))
                .And.ContainSingle(label => LabelMatch("net.dot.sdk.version", randomSdkVersion, label));
        }
        else
        {
            labels.Should().NotBeEmpty("Should have evaluated some labels by default")
                .And.NotContain(label => LabelMatch("net.dot.runtime.majorminor", runtimeMajorMinor, label))
                .And.NotContain(label => LabelMatch("net.dot.sdk.version", randomSdkVersion, label));
        };
    }

    [DataRow("7.0.100", "v7.0", "7.0")]
    [DataRow("7.0.100-preview.7", "v7.0", "7.0")]
    [DataRow("7.0.100-rc.1", "v7.0", "7.0")]
    [DataRow("8.0.100", "v8.0", "8.0")]
    [DataRow("8.0.100", "v7.0", "7.0")]
    [DataRow("8.0.100-preview.7", "v8.0", "8.0.0-preview.7")]
    [DataRow("8.0.100-rc.1", "v8.0", "8.0.0-rc.1")]
    [DataRow("8.0.100-rc.1", "v7.0", "7.0")]
    [DataRow("8.0.200", "v8.0", "8.0")]
    [DataRow("8.0.200", "v7.0", "7.0")]
    [DataRow("8.0.200-preview3", "v7.0", "7.0")]
    [DataRow("8.0.200-preview3", "v8.0", "8.0")]
    [DataRow("6.0.100", "v6.0", "6.0")]
    [DataRow("6.0.100-preview.1", "v6.0", "6.0")]
    [DataRow("8.0.100-dev", "v8.0", "8.0-preview")]
    [DataRow("8.0.100-ci", "v8.0", "8.0-preview")]
    [DataRow("8.0.100-rtm.23502.3", "v8.0", "8.0")]
    [DataRow("8.0.100-servicing.23502.3", "v8.0", "8.0")]
    [DataRow("8.0.100-alpha.12345", "v8.0", "8.0-preview")]
    [DataRow("9.0.100-alpha.12345", "v9.0", "9.0-preview")]
    [TestMethod]
    public void CanComputeTagsForSupportedSDKVersions(string sdkVersion, string tfm, string expectedTag)
    {
        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            ["TargetFrameworkIdentifier"] = ".NETCoreApp",
            ["NETCoreSdkVersion"] = sdkVersion,
            ["TargetFrameworkVersion"] = tfm,
            ["PublishProfile"] = "DefaultContainer"
        }, projectName: $"{nameof(CanComputeTagsForSupportedSDKVersions)}_{sdkVersion}_{tfm}_{expectedTag}");
        using var _ = d;
        var instance = project.CreateProjectInstance(ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerBaseImage }, new[] { logger }, null, out var outputs).Should().BeTrue(String.Join(Environment.NewLine, logger.Errors));
        var computedTag = instance.GetProperty(ContainerBaseImage).EvaluatedValue;
        computedTag.Should().EndWith(expectedTag);
    }

    [DataRow("v8.0", "linux-x64", null)]
    [DataRow("v8.0", "linux-musl-x64", null)]
    [DataRow("v8.0", "win-x64", "ContainerUser")]
    [DataRow("v7.0", "linux-x64", null)]
    [DataRow("v7.0", "win-x64", null)]
    [DataRow("v9.0", "linux-x64", null)]
    [DataRow("v9.0", "win-x64", "ContainerUser")]
    [TestMethod]
    public void CanComputeContainerUser(string tfm, string rid, string? expectedUser)
    {
        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            ["TargetFrameworkIdentifier"] = ".NETCoreApp",
            ["TargetFrameworkVersion"] = tfm,
            ["TargetFramework"] = "net" + tfm.TrimStart('v'),
            ["ContainerRuntimeIdentifier"] = rid,
        }, projectName: $"{nameof(CanComputeContainerUser)}_{tfm}_{rid}_{expectedUser}");
        using var _ = d;
        var instance = project.CreateProjectInstance(ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerConfig }, new[] { logger }, null, out var outputs).Should().BeTrue(String.Join(Environment.NewLine, logger.Errors));
        var computedTag = instance.GetProperty("ContainerUser")?.EvaluatedValue;
        computedTag.Should().Be(expectedUser);
    }

    [DataRow("linux-x64", "linux-x64")]
    [DataRow("linux-arm64", "linux-arm64")]
    [DataRow("windows-x64", "linux-x64")]
    [DataRow("windows-arm64", "linux-arm64")]
    [TestMethod]
    public void WindowsUsersGetLinuxContainers(string sdkPortableRid, string expectedRid)
    {
        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            ["TargetFrameworkVersion"] = "v6.0",
            ["NETCoreSdkPortableRuntimeIdentifier"] = sdkPortableRid
        }, projectName: $"{nameof(WindowsUsersGetLinuxContainers)}_{sdkPortableRid}_{expectedRid}");
        using var _ = d;
        var instance = project.CreateProjectInstance(ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerConfig }, null, null, out var outputs).Should().BeTrue(String.Join(Environment.NewLine, logger.Errors));
        var computedRid = instance.GetProperty(ContainerRuntimeIdentifier)?.EvaluatedValue;
        computedRid.Should().Be(expectedRid);
    }

    [DataRow("8.0.100", "v7.0", "", "7.0")]
    [DataRow("8.0.100-preview.2", "v8.0", "", "8.0.0-preview.2")]
    [DataRow("8.0.100-preview.2", "v8.0", "jammy", "8.0.0-preview.2-jammy")]
    [DataRow("8.0.100-preview.2", "v8.0", "jammy-chiseled", "8.0.0-preview.2-jammy-chiseled")]
    [DataRow("8.0.100-rc.2", "v8.0", "jammy-chiseled", "8.0.0-rc.2-jammy-chiseled")]
    [DataRow("8.0.100", "v8.0", "jammy-chiseled", "8.0-jammy-chiseled-extra")]
    [DataRow("8.0.200", "v8.0", "jammy-chiseled", "8.0-jammy-chiseled-extra")]
    [DataRow("8.0.300", "v8.0", "noble-chiseled", "8.0-noble-chiseled-extra")]
    [DataRow("8.0.300", "v8.0", "jammy-chiseled", "8.0-jammy-chiseled-extra")]
    [TestMethod]
    public void CanTakeContainerBaseFamilyIntoAccount(string sdkVersion, string tfmMajMin, string containerFamily, string expectedTag)
    {
        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            ["NetCoreSdkVersion"] = sdkVersion,
            ["TargetFrameworkVersion"] = tfmMajMin,
            [ContainerFamily] = containerFamily,
        }, projectName: $"{nameof(CanTakeContainerBaseFamilyIntoAccount)}_{sdkVersion}_{tfmMajMin}_{containerFamily}_{expectedTag}");
        using var _ = d;
        var instance = project.CreateProjectInstance(ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerBaseImage }, null, null, out var outputs).Should().BeTrue(String.Join(Environment.NewLine, logger.Errors));
        var computedBaseImageTag = instance.GetProperty(ContainerBaseImage)?.EvaluatedValue;
        computedBaseImageTag.Should().EndWith(expectedTag);
    }

    [DataRow("v6.0", "linux-musl-x64", "mcr.microsoft.com/dotnet/runtime:6.0-alpine")]
    [DataRow("v6.0", "linux-x64", "mcr.microsoft.com/dotnet/runtime:6.0")]
    [DataRow("v7.0", "linux-musl-x64", "mcr.microsoft.com/dotnet/runtime:7.0-alpine")]
    [DataRow("v7.0", "linux-x64", "mcr.microsoft.com/dotnet/runtime:7.0")]
    [DataRow("v8.0", "linux-musl-x64", "mcr.microsoft.com/dotnet/runtime:8.0-alpine")]
    [DataRow("v8.0", "linux-x64", "mcr.microsoft.com/dotnet/runtime:8.0")]
    [TestMethod]
    public void MuslRidsGetAlpineContainers(string tfm, string rid, string expectedImage)
    {
        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            ["NetCoreSdkVersion"] = "8.0.100",
            ["TargetFrameworkVersion"] = tfm,
            [KnownStrings.Properties.ContainerRuntimeIdentifier] = rid,
        }, projectName: $"{nameof(MuslRidsGetAlpineContainers)}_{tfm}_{rid}_{expectedImage}");
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerBaseImage }, null, null, out var outputs).Should().BeTrue(String.Join(Environment.NewLine, logger.Errors));
        var computedBaseImageTag = instance.GetProperty(ContainerBaseImage)?.EvaluatedValue;
        computedBaseImageTag.Should().BeEquivalentTo(expectedImage);
    }

    [DataRow("linux-musl-x64;linux-musl-arm64", "mcr.microsoft.com/dotnet/runtime:8.0-alpine")]
    [DataRow("linux-x64;linux-arm64", "mcr.microsoft.com/dotnet/runtime:8.0")]
    [TestMethod]
    public void AllMuslRidsGetAlpineContainers(string rids, string expectedImage)
    {
        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            ["NetCoreSdkVersion"] = "8.0.100",
            ["TargetFrameworkVersion"] = "v8.0",
            [KnownStrings.Properties.ContainerRuntimeIdentifier] = rids,
        }, projectName: $"{nameof(AllMuslRidsGetAlpineContainers)}");
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerBaseImage }, null, null, out var outputs).Should().BeTrue(String.Join(Environment.NewLine, logger.Errors));
        var computedBaseImageTag = instance.GetProperty(ContainerBaseImage)?.EvaluatedValue;
        computedBaseImageTag.Should().BeEquivalentTo(expectedImage);
    }

    [TestMethod]
    public void NotAllMuslRidsLogsError()
    {
        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            ["NetCoreSdkVersion"] = "8.0.100",
            ["TargetFrameworkVersion"] = "v8.0",
            [KnownStrings.Properties.ContainerRuntimeIdentifier] = "linux-musl-x64;linux-arm64",
        }, projectName: $"{nameof(NotAllMuslRidsLogsError)}");
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerBaseImage }, [logger], null, out var outputs).Should().BeFalse(String.Join(Environment.NewLine, logger.Errors));
        logger.Errors.Should().ContainSingle(error => error.Message == Resources.Strings.InvalidTargetRuntimeIdentifiers);
    }

    [DataRow("linux-musl-x64", "mcr.microsoft.com/dotnet/runtime-deps:8.0-alpine")]
    [DataRow("linux-x64", "mcr.microsoft.com/dotnet/runtime-deps:8.0-jammy-chiseled")]
    [TestMethod]
    public void AOTAppsGetExpectedImages(string rid, string expectedImage)
    {
        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            ["NetCoreSdkVersion"] = "8.0.100",
            ["TargetFrameworkVersion"] = "v8.0",
            [KnownStrings.Properties.ContainerRuntimeIdentifier] = rid,
            [KnownStrings.Properties.PublishSelfContained] = true.ToString(),
            [KnownStrings.Properties.PublishAot] = true.ToString(),
            [KnownStrings.Properties.InvariantGlobalization] = true.ToString(),
        }, projectName: $"{nameof(AOTAppsGetExpectedImages)}_{rid}_{expectedImage}");
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerBaseImage }, null, null, out var outputs).Should().BeTrue(String.Join(Environment.NewLine, logger.Errors));
        var computedBaseImageTag = instance.GetProperty(ContainerBaseImage)?.EvaluatedValue;
        computedBaseImageTag.Should().BeEquivalentTo(expectedImage);
    }

    [DataRow("linux-musl-x64", "mcr.microsoft.com/dotnet/runtime-deps:8.0-alpine-extra")]
    [DataRow("linux-x64", "mcr.microsoft.com/dotnet/runtime-deps:8.0-noble-chiseled-extra")]
    [TestMethod]
    public void AOTAppsWithCulturesGetExtraImages(string rid, string expectedImage)
    {
        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            ["NetCoreSdkVersion"] = "8.0.300",
            ["TargetFrameworkVersion"] = "v8.0",
            [KnownStrings.Properties.ContainerRuntimeIdentifier] = rid,
            [KnownStrings.Properties.PublishSelfContained] = true.ToString(),
            [KnownStrings.Properties.PublishAot] = true.ToString(),
            [KnownStrings.Properties.InvariantGlobalization] = false.ToString()
        }, projectName: $"{nameof(AOTAppsWithCulturesGetExtraImages)}_{rid}_{expectedImage}");
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerBaseImage }, null, null, out var outputs).Should().BeTrue(String.Join(Environment.NewLine, logger.Errors));
        var computedBaseImageTag = instance.GetProperty(ContainerBaseImage)?.EvaluatedValue;
        computedBaseImageTag.Should().BeEquivalentTo(expectedImage);
    }

    [DataRow("linux-musl-x64", "mcr.microsoft.com/dotnet/runtime-deps:8.0-alpine-extra")]
    [DataRow("linux-x64", "mcr.microsoft.com/dotnet/runtime-deps:8.0-noble-chiseled-extra")]
    [TestMethod]
    public void TrimmedAppsWithCulturesGetExtraImages(string rid, string expectedImage)
    {
        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            ["NetCoreSdkVersion"] = "8.0.300",
            ["TargetFrameworkVersion"] = "v8.0",
            [KnownStrings.Properties.ContainerRuntimeIdentifier] = rid,
            [KnownStrings.Properties.PublishSelfContained] = true.ToString(),
            [KnownStrings.Properties.PublishTrimmed] = true.ToString(),
            [KnownStrings.Properties.InvariantGlobalization] = false.ToString()
        }, projectName: $"{nameof(TrimmedAppsWithCulturesGetExtraImages)}_{rid}_{expectedImage}");
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerBaseImage }, null, null, out var outputs).Should().BeTrue(String.Join(Environment.NewLine, logger.Errors));
        var computedBaseImageTag = instance.GetProperty(ContainerBaseImage)?.EvaluatedValue;
        computedBaseImageTag.Should().BeEquivalentTo(expectedImage);
    }

    [DataRow("linux-musl-x64", "mcr.microsoft.com/dotnet/runtime-deps:8.0-alpine")]
    [DataRow("linux-x64", "mcr.microsoft.com/dotnet/runtime-deps:8.0-noble-chiseled")]
    [TestMethod]
    public void TrimmedAppsWithoutCulturesGetbaseImages(string rid, string expectedImage)
    {
        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            ["NetCoreSdkVersion"] = "8.0.300",
            ["TargetFrameworkVersion"] = "v8.0",
            [KnownStrings.Properties.ContainerRuntimeIdentifier] = rid,
            [KnownStrings.Properties.PublishSelfContained] = true.ToString(),
            [KnownStrings.Properties.PublishTrimmed] = true.ToString(),
            [KnownStrings.Properties.InvariantGlobalization] = true.ToString()
        }, projectName: $"{nameof(TrimmedAppsWithCulturesGetExtraImages)}_{rid}_{expectedImage}");
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerBaseImage }, null, null, out var outputs).Should().BeTrue(String.Join(Environment.NewLine, logger.Errors));
        var computedBaseImageTag = instance.GetProperty(ContainerBaseImage)?.EvaluatedValue;
        computedBaseImageTag.Should().BeEquivalentTo(expectedImage);
    }

    [DataRow(true, false, "linux-musl-x64", true, "mcr.microsoft.com/dotnet/runtime-deps:8.0-alpine")]
    [DataRow(true, false, "linux-musl-x64", false, "mcr.microsoft.com/dotnet/runtime-deps:8.0-alpine-extra")]
    [DataRow(false, true, "linux-musl-x64", true, "mcr.microsoft.com/dotnet/runtime-deps:8.0-alpine")]
    [DataRow(false, true, "linux-musl-x64", false, "mcr.microsoft.com/dotnet/runtime-deps:8.0-alpine-extra")]

    [DataRow(true, false, "linux-x64", true, "mcr.microsoft.com/dotnet/runtime-deps:8.0-noble-chiseled")]
    [DataRow(true, false, "linux-x64", false, "mcr.microsoft.com/dotnet/runtime-deps:8.0-noble-chiseled-extra")]
    [DataRow(false, true, "linux-x64", true, "mcr.microsoft.com/dotnet/runtime-deps:8.0-noble-chiseled")]
    [DataRow(false, true, "linux-x64", false, "mcr.microsoft.com/dotnet/runtime-deps:8.0-noble-chiseled-extra")]
    [TestMethod]
    public void TheBigMatrixOfTrimmingInference(bool trimmed, bool aot, string rid, bool invariant, string expectedImage)
    {
        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            ["NetCoreSdkVersion"] = "8.0.300",
            ["TargetFrameworkVersion"] = "v8.0",
            [KnownStrings.Properties.ContainerRuntimeIdentifier] = rid,
            [KnownStrings.Properties.PublishSelfContained] = true.ToString(),
            [KnownStrings.Properties.PublishTrimmed] = trimmed.ToString(),
            [KnownStrings.Properties.PublishAot] = aot.ToString(),
            [KnownStrings.Properties.InvariantGlobalization] = invariant.ToString()
        }, projectName: $"{nameof(TrimmedAppsWithCulturesGetExtraImages)}_{rid}_{expectedImage}");
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerBaseImage }, null, null, out var outputs).Should().BeTrue(String.Join(Environment.NewLine, logger.Errors));
        var computedBaseImageTag = instance.GetProperty(ContainerBaseImage)?.EvaluatedValue;
        computedBaseImageTag.Should().BeEquivalentTo(expectedImage);
    }

    [DataRow("linux-musl-x64", "mcr.microsoft.com/dotnet/runtime-deps:7.0-alpine")]
    [DataRow("linux-x64", "mcr.microsoft.com/dotnet/runtime-deps:7.0")]
    [TestMethod]
    public void AOTAppsLessThan8DoNotGetAOTImages(string rid, string expectedImage)
    {
        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            ["NetCoreSdkVersion"] = "8.0.100",
            ["TargetFrameworkVersion"] = "v7.0",
            [KnownStrings.Properties.ContainerRuntimeIdentifier] = rid,
            [KnownStrings.Properties.PublishSelfContained] = true.ToString(),
            [KnownStrings.Properties.PublishAot] = true.ToString(),
            [KnownStrings.Properties.InvariantGlobalization] = true.ToString(),
        }, projectName: $"{nameof(AOTAppsLessThan8DoNotGetAOTImages)}_{rid}_{expectedImage}");
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerBaseImage }, null, null, out var outputs).Should().BeTrue(String.Join(Environment.NewLine, logger.Errors));
        var computedBaseImageTag = instance.GetProperty(ContainerBaseImage)?.EvaluatedValue;
        computedBaseImageTag.Should().BeEquivalentTo(expectedImage);
    }

    [TestMethod]
    public void FDDConsoleAppWithCulturesAndOptingIntoChiseledGetsExtras()
    {
        var expectedImage = "mcr.microsoft.com/dotnet/runtime:8.0-jammy-chiseled-extra";
        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            ["NetCoreSdkVersion"] = "8.0.100",
            ["TargetFrameworkVersion"] = "v8.0",
            [KnownStrings.Properties.ContainerRuntimeIdentifier] = "linux-x64",
            [KnownStrings.Properties.ContainerFamily] = "jammy-chiseled",
            [KnownStrings.Properties.InvariantGlobalization] = false.ToString(),
        }, projectName: $"{nameof(FDDConsoleAppWithCulturesAndOptingIntoChiseledGetsExtras)}");
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerBaseImage }, null, null, out var outputs).Should().BeTrue(String.Join(Environment.NewLine, logger.Errors));
        var computedBaseImageTag = instance.GetProperty(ContainerBaseImage)?.EvaluatedValue;
        computedBaseImageTag.Should().BeEquivalentTo(expectedImage);
    }

    [TestMethod]
    public void FDDAspNetAppWithCulturesAndOptingIntoChiseledGetsExtras()
    {
        var expectedImage = "mcr.microsoft.com/dotnet/aspnet:8.0-jammy-chiseled-extra";
        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            ["NetCoreSdkVersion"] = "8.0.100",
            ["TargetFrameworkVersion"] = "v8.0",
            [KnownStrings.Properties.ContainerRuntimeIdentifier] = "linux-x64",
            [KnownStrings.Properties.ContainerFamily] = "jammy-chiseled",
            [KnownStrings.Properties.InvariantGlobalization] = false.ToString(),
        }, bonusItems: new()
        {
            [KnownStrings.Items.FrameworkReference] = KnownFrameworkReferences.WebApp
        }, projectName: $"{nameof(FDDAspNetAppWithCulturesAndOptingIntoChiseledGetsExtras)}");
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerBaseImage }, null, null, out var outputs).Should().BeTrue(String.Join(Environment.NewLine, logger.Errors));
        var computedBaseImageTag = instance.GetProperty(ContainerBaseImage)?.EvaluatedValue;
        computedBaseImageTag.Should().BeEquivalentTo(expectedImage);
    }

    [DataRow("linux-musl-x64", "mcr.microsoft.com/dotnet/runtime-deps:7.0-alpine")]
    [DataRow("linux-x64", "mcr.microsoft.com/dotnet/runtime-deps:7.0")]
    [TestMethod]
    public void AOTAppsLessThan8WithCulturesDoNotGetExtraImages(string rid, string expectedImage)
    {
        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            ["NetCoreSdkVersion"] = "8.0.100",
            ["TargetFrameworkVersion"] = "v7.0",
            [KnownStrings.Properties.ContainerRuntimeIdentifier] = rid,
            [KnownStrings.Properties.PublishSelfContained] = true.ToString(),
            [KnownStrings.Properties.PublishAot] = true.ToString(),
            [KnownStrings.Properties.InvariantGlobalization] = false.ToString()
        }, projectName: $"{nameof(AOTAppsWithCulturesGetExtraImages)}_{rid}_{expectedImage}");
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerBaseImage }, null, null, out var outputs).Should().BeTrue(String.Join(Environment.NewLine, logger.Errors));
        var computedBaseImageTag = instance.GetProperty(ContainerBaseImage)?.EvaluatedValue;
        computedBaseImageTag.Should().BeEquivalentTo(expectedImage);
    }

    [DataRow("8.0.100", "v8.0", "jammy-chiseled", "mcr.microsoft.com/dotnet/runtime:8.0-jammy-chiseled-extra")]
    [DataRow("9.0.100", "v9.0", "noble-chiseled", "mcr.microsoft.com/dotnet/runtime:9.0-noble-chiseled-extra")]
    [DataRow("10.0.100", "v10.0", "noble-chiseled", "mcr.microsoft.com/dotnet/runtime:10.0-noble-chiseled-extra")]
    [TestMethod]
    public void FDDConsoleAppWithCulturesAndOptingIntoChiseledGetsExtrasForNet9AndLater(string sdkVersion, string tfm, string containerFamily, string expectedImage)
    {
        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            ["NetCoreSdkVersion"] = sdkVersion,
            ["TargetFrameworkVersion"] = tfm,
            [KnownStrings.Properties.ContainerRuntimeIdentifier] = "linux-x64",
            [KnownStrings.Properties.ContainerFamily] = containerFamily,
            [KnownStrings.Properties.InvariantGlobalization] = false.ToString(),
        }, projectName: $"{nameof(FDDConsoleAppWithCulturesAndOptingIntoChiseledGetsExtrasForNet9AndLater)}_{sdkVersion}_{tfm}_{containerFamily}");
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerBaseImage }, null, null, out var outputs).Should().BeTrue(String.Join(Environment.NewLine, logger.Errors));
        var computedBaseImageTag = instance.GetProperty(ContainerBaseImage)?.EvaluatedValue;
        computedBaseImageTag.Should().BeEquivalentTo(expectedImage);
    }

    [DataRow("8.0.100", "v8.0", "jammy-chiseled", "mcr.microsoft.com/dotnet/aspnet:8.0-jammy-chiseled-extra")]
    [DataRow("9.0.100", "v9.0", "noble-chiseled", "mcr.microsoft.com/dotnet/aspnet:9.0-noble-chiseled-extra")]
    [DataRow("10.0.100", "v10.0", "noble-chiseled", "mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra")]
    [TestMethod]
    public void FDDAspNetAppWithCulturesAndOptingIntoChiseledGetsExtrasForNet9AndLater(string sdkVersion, string tfm, string containerFamily, string expectedImage)
    {
        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            ["NetCoreSdkVersion"] = sdkVersion,
            ["TargetFrameworkVersion"] = tfm,
            [KnownStrings.Properties.ContainerRuntimeIdentifier] = "linux-x64",
            [KnownStrings.Properties.ContainerFamily] = containerFamily,
            [KnownStrings.Properties.InvariantGlobalization] = false.ToString(),
        }, bonusItems: new()
        {
            [KnownStrings.Items.FrameworkReference] = KnownFrameworkReferences.WebApp
        }, projectName: $"{nameof(FDDAspNetAppWithCulturesAndOptingIntoChiseledGetsExtrasForNet9AndLater)}_{sdkVersion}_{tfm}_{containerFamily}");
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerBaseImage }, null, null, out var outputs).Should().BeTrue(String.Join(Environment.NewLine, logger.Errors));
        var computedBaseImageTag = instance.GetProperty(ContainerBaseImage)?.EvaluatedValue;
        computedBaseImageTag.Should().BeEquivalentTo(expectedImage);
    }

    [TestMethod]
    public void AspNetFDDAppsGetAspNetBaseImage()
    {
        var expectedImage = "mcr.microsoft.com/dotnet/aspnet:8.0";
        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            ["NetCoreSdkVersion"] = "8.0.200",
            ["TargetFrameworkVersion"] = "v8.0",
            [KnownStrings.Properties.ContainerRuntimeIdentifier] = "linux-x64",
        }, bonusItems: new()
        {
            [KnownStrings.Items.FrameworkReference] = KnownFrameworkReferences.WebApp
        }, projectName: $"{nameof(AspNetFDDAppsGetAspNetBaseImage)}");
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerBaseImage }, [logger], null, out var outputs).Should().BeTrue(String.Join(Environment.NewLine, logger.Errors));
        var computedBaseImageTag = instance.GetProperty(ContainerBaseImage)?.EvaluatedValue;
        computedBaseImageTag.Should().BeEquivalentTo(expectedImage);
    }

    private static class KnownFrameworkReferences
    {
        public static Microsoft.Build.Framework.ITaskItem[] ConsoleApp { get; } = [new Microsoft.Build.Utilities.TaskItem("Microsoft.NETCore.App")];
        public static Microsoft.Build.Framework.ITaskItem[] WebApp { get; } = [.. ConsoleApp, new Microsoft.Build.Utilities.TaskItem("Microsoft.AspNetCore.App")];
    }
}
