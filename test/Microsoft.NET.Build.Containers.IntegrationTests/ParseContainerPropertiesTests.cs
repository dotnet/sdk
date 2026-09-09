// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Containers.IntegrationTests;
using static Microsoft.NET.Build.Containers.KnownStrings;
using static Microsoft.NET.Build.Containers.KnownStrings.Properties;

namespace Microsoft.NET.Build.Containers.Tasks.IntegrationTests;

[TestClass]
public class ParseContainerPropertiesTests
{
    [TestMethod]
    public void Baseline()
    {
        var (project, logs, d) = ProjectInitializer.InitProject(new()
        {
            [ContainerBaseImage] = "mcr.microsoft.com/dotnet/runtime:7.0",
            [ContainerRegistry] = "localhost:5010",
            [ContainerRepository] = "dotnet/testimage",
            [ContainerImageTags] = "7.0;latest"
        });
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        Assert.IsTrue(instance.Build(new[] { ComputeContainerConfig }, new[] { logs }, null, out var outputs));

        Assert.AreEqual("mcr.microsoft.com", instance.GetPropertyValue(ContainerBaseRegistry));
        Assert.AreEqual("dotnet/runtime", instance.GetPropertyValue(ContainerBaseName));
        Assert.AreEqual("7.0", instance.GetPropertyValue(ContainerBaseTag));

        Assert.AreEqual("dotnet/testimage", instance.GetPropertyValue(ContainerRepository));
        instance.GetItems(ContainerImageTags).Select(i => i.EvaluatedInclude).ToArray().Should().BeEquivalentTo(new[] { "7.0", "latest" });
        instance.GetItems("ProjectCapability").Select(i => i.EvaluatedInclude).ToArray().Should().BeEquivalentTo(new[] { "NetSdkOCIImageBuild" });
    }

    [TestMethod]
    public void SpacesGetReplacedWithDashes()
    {
        var (project, logs, d) = ProjectInitializer.InitProject(new()
        {
            [ContainerBaseImage] = "mcr.microsoft.com/dotnet runtime:7.0",
            [ContainerRegistry] = "localhost:5010"
        });
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        Assert.IsTrue(instance.Build(new[] { ComputeContainerConfig }, new[] { logs }, null, out var outputs));

        Assert.AreEqual("mcr.microsoft.com", instance.GetPropertyValue(ContainerBaseRegistry));
        Assert.AreEqual("dotnet-runtime", instance.GetPropertyValue(ContainerBaseName));
        Assert.AreEqual("7.0", instance.GetPropertyValue(ContainerBaseTag));
    }

    [TestMethod]
    public void RegexCatchesInvalidContainerNames()
    {
        var (project, logs, d) = ProjectInitializer.InitProject(new()
        {
            [ContainerBaseImage] = "mcr.microsoft.com/dotnet/runtime:7.0",
            [ContainerRegistry] = "localhost:5010",
            [ContainerRepository] = "dotnet testimage",
            [ContainerImageTag] = "5.0"
        });
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        Assert.IsTrue(instance.Build(new[] { ComputeContainerConfig }, new[] { logs }, null, out var outputs));
        Assert.Contains(m => m.Message?.Contains("'dotnet testimage' was not a valid container image name, it was normalized to 'dotnet-testimage'") == true, logs.Messages);
    }

    [TestMethod]
    public void RegexCatchesInvalidContainerTags()
    {
        var (project, logs, d) = ProjectInitializer.InitProject(new()
        {
            [ContainerBaseImage] = "mcr.microsoft.com/dotnet/runtime:7.0",
            [ContainerRegistry] = "localhost:5010",
            [ContainerRepository] = "dotnet/testimage",
            [ContainerImageTag] = "5 0"
        });
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        Assert.IsFalse(instance.Build(new[] { ComputeContainerConfig }, new[] { logs }, null, out var outputs));

        Assert.IsNotEmpty(logs.Errors);
        Assert.AreEqual(logs.Errors[0].Code, ErrorCodes.CONTAINER2007);
    }

    [TestMethod]
    public void CanOnlySupplyOneOfTagAndTags()
    {
        var (project, logs, d) = ProjectInitializer.InitProject(new()
        {
            [ContainerBaseImage] = "mcr.microsoft.com/dotnet/runtime:7.0",
            [ContainerRegistry] = "localhost:5010",
            [ContainerRepository] = "dotnet/testimage",
            [ContainerImageTag] = "5.0",
            [ContainerImageTags] = "latest;oldest"
        });
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        Assert.IsFalse(instance.Build(new[] { ComputeContainerConfig }, new[] { logs }, null, out var outputs));

        Assert.IsNotEmpty(logs.Errors);
        Assert.AreEqual(logs.Errors[0].Code, ErrorCodes.CONTAINER2008);
    }

    [TestMethod]
    public void InvalidTagsThrowError()
    {
        var (project, logs, d) = ProjectInitializer.InitProject(new()
        {
            [ContainerBaseImage] = "mcr.microsoft.com/dotnet/aspnet:8.0",
            [ContainerRepository] = "dotnet/testimage",
            [ContainerImageTags] = "'latest;oldest'"
        });
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        Assert.IsFalse(instance.Build(new[] { ComputeContainerConfig }, new[] { logs }, null, out var outputs));

        Assert.IsNotEmpty(logs.Errors);
        Assert.AreEqual(logs.Errors[0].Code, ErrorCodes.CONTAINER2010);
    }

    [TestMethod]
    public void FailsOnCompletelyInvalidRepositoryNames()
    {
        var (project, logs, d) = ProjectInitializer.InitProject(new()
        {
            [ContainerBaseImage] = "mcr.microsoft.com/dotnet/runtime:7.0",
            [ContainerRegistry] = "localhost:5010",
            [ContainerImageName] = "㓳㓴㓵㓶㓷㓹㓺㓻",
            [ContainerImageTag] = "5.0"
        });
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        Assert.IsFalse(instance.Build(new[] { ComputeContainerConfig }, new[] { logs }, null, out var outputs));

        Assert.IsNotEmpty(logs.Errors);
        Assert.AreEqual(logs.Errors[0].Code, ErrorCodes.CONTAINER2005);
    }

    [TestMethod]
    public void FailsWhenFirstCharIsAUnicodeLetterButNonLatin()
    {
        var (project, logs, d) = ProjectInitializer.InitProject(new()
        {
            [ContainerBaseImage] = "mcr.microsoft.com/dotnet/runtime:7.0",
            [ContainerRegistry] = "localhost:5010",
            [ContainerImageName] = "㓳but-otherwise-valid",
            [ContainerImageTag] = "5.0"
        });
        using var _ = d;
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        Assert.IsFalse(instance.Build(new[] { ComputeContainerConfig }, new[] { logs }, null, out var outputs));

        Assert.IsNotEmpty(logs.Errors);
        Assert.AreEqual(logs.Errors[0].Code, ErrorCodes.CONTAINER2005);
    }
}
