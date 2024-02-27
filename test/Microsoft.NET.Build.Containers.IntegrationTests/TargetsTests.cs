// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Execution;
using Microsoft.NET.Build.Containers.IntegrationTests;
using static Microsoft.NET.Build.Containers.KnownStrings.Properties;

namespace Microsoft.NET.Build.Containers.Targets.IntegrationTests;

public class TargetsTests
{
    [InlineData("SelfContained", true, "/app/foo.exe")]
    [InlineData("SelfContained", false, "dotnet", "/app/foo.dll")]
    [InlineData("PublishSelfContained", true, "/app/foo.exe")]
    [InlineData("PublishSelfContained", false, "dotnet", "/app/foo.dll")]
    [Theory]
    public void CanDeferEntrypoint(string selfContainedPropertyName, bool selfContainedPropertyValue, params string[] entrypointArgs)
    {
        var (project, _, d) = ProjectInitializer.InitProject(new()
        {
            [selfContainedPropertyName] = selfContainedPropertyValue.ToString()
        }, projectName: $"{nameof(CanDeferEntrypoint)}_{selfContainedPropertyName}_{selfContainedPropertyValue}_{string.Join("_", entrypointArgs)}");
        using var _ = d;
        Assert.True(project.Build(ComputeContainerConfig));
        var computedEntrypointArgs = project.GetItems(ContainerEntrypoint).Select(i => i.EvaluatedInclude).ToArray();
        foreach (var (First, Second) in entrypointArgs.Zip(computedEntrypointArgs))
        {
            Assert.Equal(First, Second);
        }
    }

    [Fact]
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
        Assert.Equal(customImageName, instance.GetPropertyValue(ContainerRepository));
    }

    [InlineData("WebApplication44", "webapplication44", true)]
    [InlineData("friendly-suspicious-alligator", "friendly-suspicious-alligator", true)]
    [InlineData("*friendly-suspicious-alligator", "", false)]
    [InlineData("web/app2+7", "web/app2-7", true)]
    [InlineData("Microsoft.Apps.Demo.ContosoWeb", "microsoft-apps-demo-contosoweb", true)]
    [Theory]
    public void CanNormalizeInputContainerNames(string projectName, string expectedContainerImageName, bool shouldPass)
    {
        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            [AssemblyName] = projectName
        }, projectName: $"{nameof(CanNormalizeInputContainerNames)}_{projectName}_{expectedContainerImageName}_{shouldPass}");
        using var _ = d;
        var instance = project.CreateProjectInstance(ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerConfig }, new[] { logger }, null, out var outputs).Should().Be(shouldPass, String.Join(Environment.NewLine, logger.AllMessages));
        Assert.Equal(expectedContainerImageName, instance.GetPropertyValue(ContainerRepository));
    }

    [InlineData("7.0.100", true)]
    [InlineData("8.0.100", true)]
    [InlineData("7.0.100-preview.7", true)]
    [InlineData("7.0.100-rc.1", true)]
    [InlineData("6.0.100", false)]
    [InlineData("7.0.100-preview.1", false)]
    [Theory]
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

    [InlineData(true)]
    [InlineData(false)]
    [Theory]
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

    [InlineData(true)]
    [InlineData(false)]
    [Theory]
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

    [InlineData("7.0.100", "v7.0", "7.0")]
    [InlineData("7.0.100-preview.7", "v7.0", "7.0")]
    [InlineData("7.0.100-rc.1", "v7.0", "7.0")]
    [InlineData("8.0.100", "v8.0", "8.0")]
    [InlineData("8.0.100", "v7.0", "7.0")]
    [InlineData("8.0.100-preview.7", "v8.0", "8.0.0-preview.7")]
    [InlineData("8.0.100-rc.1", "v8.0", "8.0.0-rc.1")]
    [InlineData("8.0.100-rc.1", "v7.0", "7.0")]
    [InlineData("8.0.200", "v8.0", "8.0")]
    [InlineData("8.0.200", "v7.0", "7.0")]
    [InlineData("8.0.200-preview3", "v7.0", "7.0")]
    [InlineData("8.0.200-preview3", "v8.0", "8.0")]
    [InlineData("6.0.100", "v6.0", "6.0")]
    [InlineData("6.0.100-preview.1", "v6.0", "6.0")]
    [InlineData("8.0.100-dev", "v8.0", "8.0-preview")]
    [InlineData("8.0.100-ci", "v8.0", "8.0-preview")]
    [InlineData("8.0.100-rtm.23502.3", "v8.0", "8.0")]
    [InlineData("8.0.100-servicing.23502.3", "v8.0", "8.0")]
    [InlineData("8.0.100-alpha.12345", "v8.0", "8.0-preview")]
    [InlineData("9.0.100-alpha.12345", "v9.0", "9.0-preview")]
    [Theory]
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

    [InlineData("v8.0", "linux-x64", null)]
    [InlineData("v8.0", "linux-musl-x64", null)]
    [InlineData("v8.0", "win-x64", "ContainerUser")]
    [InlineData("v7.0", "linux-x64", null)]
    [InlineData("v7.0", "win-x64", null)]
    [InlineData("v9.0", "linux-x64", null)]
    [InlineData("v9.0", "win-x64", "ContainerUser")]
    [Theory]
    public void CanComputeContainerUser(string tfm, string rid, string? expectedUser)
    {
        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            ["TargetFrameworkIdentifier"] = ".NETCoreApp",
            ["TargetFrameworkVersion"] = tfm,
            ["TargetFramework"] = "net" + tfm.TrimStart('v'),
            ["ContainerRuntimeIdentifier"] = rid
        }, projectName: $"{nameof(CanComputeContainerUser)}_{tfm}_{rid}_{expectedUser}");
        using var _ = d;
        var instance = project.CreateProjectInstance(ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerConfig }, new[] { logger }, null, out var outputs).Should().BeTrue(String.Join(Environment.NewLine, logger.Errors));
        var computedTag = instance.GetProperty("ContainerUser")?.EvaluatedValue;
        computedTag.Should().Be(expectedUser);
    }

    [InlineData("linux-x64", "linux-x64")]
    [InlineData("linux-arm64", "linux-arm64")]
    [InlineData("windows-x64", "linux-x64")]
    [InlineData("windows-arm64", "linux-arm64")]
    [Theory]
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

    [InlineData("8.0.100", "v7.0", "", "7.0")]
    [InlineData("8.0.100-preview.2", "v8.0", "", "8.0.0-preview.2")]
    [InlineData("8.0.100-preview.2", "v8.0", "jammy", "8.0.0-preview.2-jammy")]
    [InlineData("8.0.100-preview.2", "v8.0", "jammy-chiseled", "8.0.0-preview.2-jammy-chiseled")]
    [InlineData("8.0.100-rc.2", "v8.0", "jammy-chiseled", "8.0.0-rc.2-jammy-chiseled")]
    [InlineData("8.0.100", "v8.0", "jammy-chiseled", "8.0-jammy-chiseled")]
    [Theory]
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

    [InlineData("v6.0", "linux-musl-x64", "mcr.microsoft.com/dotnet/runtime:6.0-alpine")]
    [InlineData("v6.0", "linux-x64", "mcr.microsoft.com/dotnet/runtime:6.0")]
    [InlineData("v7.0", "linux-musl-x64", "mcr.microsoft.com/dotnet/runtime:7.0-alpine")]
    [InlineData("v7.0", "linux-x64", "mcr.microsoft.com/dotnet/runtime:7.0")]
    [InlineData("v8.0", "linux-musl-x64", "mcr.microsoft.com/dotnet/runtime:8.0-alpine")]
    [InlineData("v8.0", "linux-x64", "mcr.microsoft.com/dotnet/runtime:8.0")]
    [Theory]
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

    [InlineData("linux-musl-x64", "mcr.microsoft.com/dotnet/nightly/runtime-deps:8.0-alpine-aot")]
    [InlineData("linux-x64", "mcr.microsoft.com/dotnet/nightly/runtime-deps:8.0-jammy-chiseled-aot")]
    [Theory]
    public void AOTAppsGetAOTImages(string rid, string expectedImage)
    {
        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            ["NetCoreSdkVersion"] = "8.0.100",
            ["TargetFrameworkVersion"] = "v8.0",
            [KnownStrings.Properties.ContainerRuntimeIdentifier] = rid,
            [KnownStrings.Properties.PublishSelfContained] = true.ToString(),
            [KnownStrings.Properties.PublishAot] = true.ToString(),
            [KnownStrings.Properties.InvariantGlobalization] = true.ToString(),
        }, projectName: $"{nameof(AOTAppsGetAOTImages)}_{rid}_{expectedImage}");
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        instance.Build(new[] { ComputeContainerBaseImage }, null, null, out var outputs).Should().BeTrue(String.Join(Environment.NewLine, logger.Errors));
        var computedBaseImageTag = instance.GetProperty(ContainerBaseImage)?.EvaluatedValue;
        computedBaseImageTag.Should().BeEquivalentTo(expectedImage);
    }

    [InlineData("linux-musl-x64", "mcr.microsoft.com/dotnet/nightly/runtime-deps:8.0-alpine-extra")]
    [InlineData("linux-x64", "mcr.microsoft.com/dotnet/nightly/runtime-deps:8.0-jammy-chiseled-extra")]
    [Theory]
    public void AOTAppsWithCulturesGetExtraImages(string rid, string expectedImage)
    {
        var (project, logger, d) = ProjectInitializer.InitProject(new()
        {
            ["NetCoreSdkVersion"] = "8.0.100",
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

    [InlineData("linux-musl-x64", "mcr.microsoft.com/dotnet/runtime-deps:7.0-alpine")]
    [InlineData("linux-x64", "mcr.microsoft.com/dotnet/runtime-deps:7.0")]
    [Theory]
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

    [InlineData("linux-musl-x64", "mcr.microsoft.com/dotnet/runtime-deps:7.0-alpine")]
    [InlineData("linux-x64", "mcr.microsoft.com/dotnet/runtime-deps:7.0")]
    [Theory]
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

    [Fact]
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
        instance.Build(new[] { ComputeContainerBaseImage }, null, null, out var outputs).Should().BeTrue(String.Join(Environment.NewLine, logger.Errors));
        var computedBaseImageTag = instance.GetProperty(ContainerBaseImage)?.EvaluatedValue;
        computedBaseImageTag.Should().BeEquivalentTo(expectedImage);
    }

    private static class KnownFrameworkReferences
    {
        public static Microsoft.Build.Framework.ITaskItem[] ConsoleApp { get; } = [new Microsoft.Build.Utilities.TaskItem("Microsoft.NETCore.App")];
        public static Microsoft.Build.Framework.ITaskItem[] WebApp { get; } = [.. ConsoleApp, new Microsoft.Build.Utilities.TaskItem("Microsoft.AspNetCore.App")];
    }
}
