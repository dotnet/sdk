// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Utilities;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.Build.Containers.IntegrationTests;
using Microsoft.NET.Build.Containers.UnitTests;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework;

namespace Microsoft.NET.Build.Containers.Tasks.IntegrationTests;

[Collection("Docker tests")]
public class CreateNewImageTests
{
    private ITestOutputHelper _testOutput;

    public CreateNewImageTests(ITestOutputHelper testOutput)
    {
        _testOutput = testOutput;
    }

    [DockerAvailableFact]
    public void CreateNewImage_Baseline()
    {
        DirectoryInfo newProjectDir = new DirectoryInfo(Path.Combine(TestSettings.TestArtifactsDirectory, nameof(CreateNewImage_Baseline)));

        if (newProjectDir.Exists)
        {
            newProjectDir.Delete(recursive: true);
        }

        newProjectDir.Create();

        new DotnetNewCommand(_testOutput, "console", "-f", ToolsetInfo.CurrentTargetFramework)
            .WithVirtualHive()
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        new DotnetCommand(_testOutput, "publish", "-c", "Release", "-r", "linux-arm64", "--no-self-contained")
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        CreateNewImage task = new CreateNewImage();
        task.BaseRegistry = "mcr.microsoft.com";
        task.BaseImageName = "dotnet/runtime";
        task.BaseImageTag = "7.0";

        task.OutputRegistry = "localhost:5010";
        task.PublishDirectory = Path.Combine(newProjectDir.FullName, "bin", "Release", ToolsetInfo.CurrentTargetFramework, "linux-arm64", "publish");
        task.ImageName = "dotnet/testimage";
        task.ImageTags = new[] { "latest" };
        task.WorkingDirectory = "app/";
        task.ContainerRuntimeIdentifier = "linux-arm64";
        task.Entrypoint = new TaskItem[] { new("dotnet"), new("build") };
        task.RuntimeIdentifierGraphPath = ToolsetUtils.GetRuntimeGraphFilePath();

        Assert.True(task.Execute());
        newProjectDir.Delete(true);
    }

    [DockerAvailableFact]
    public void ParseContainerProperties_EndToEnd()
    {
        DirectoryInfo newProjectDir = new DirectoryInfo(Path.Combine(TestSettings.TestArtifactsDirectory, nameof(ParseContainerProperties_EndToEnd)));

        if (newProjectDir.Exists)
        {
            newProjectDir.Delete(recursive: true);
        }

        newProjectDir.Create();

        new DotnetNewCommand(_testOutput, "console", "-f", ToolsetInfo.CurrentTargetFramework)
            .WithVirtualHive()
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        new DotnetCommand(_testOutput, "build", "--configuration", "release")
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        ParseContainerProperties pcp = new ParseContainerProperties();
        pcp.FullyQualifiedBaseImageName = "mcr.microsoft.com/dotnet/runtime:7.0";
        pcp.ContainerRegistry = "localhost:5010";
        pcp.ContainerImageName = "dotnet/testimage";
        pcp.ContainerImageTags = new[] { "5.0", "latest" };

        Assert.True(pcp.Execute());
        Assert.Equal("mcr.microsoft.com", pcp.ParsedContainerRegistry);
        Assert.Equal("dotnet/runtime", pcp.ParsedContainerImage);
        Assert.Equal("7.0", pcp.ParsedContainerTag);

        Assert.Equal("dotnet/testimage", pcp.NewContainerImageName);
        pcp.NewContainerTags.Should().BeEquivalentTo(new[] { "5.0", "latest" });

        CreateNewImage cni = new CreateNewImage();
        cni.BaseRegistry = pcp.ParsedContainerRegistry;
        cni.BaseImageName = pcp.ParsedContainerImage;
        cni.BaseImageTag = pcp.ParsedContainerTag;
        cni.ImageName = pcp.NewContainerImageName;
        cni.OutputRegistry = "localhost:5010";
        cni.PublishDirectory = Path.Combine(newProjectDir.FullName, "bin", "release", ToolsetInfo.CurrentTargetFramework);
        cni.WorkingDirectory = "app/";
        cni.Entrypoint = new TaskItem[] { new("ParseContainerProperties_EndToEnd") };
        cni.ImageTags = pcp.NewContainerTags;
        cni.ContainerRuntimeIdentifier = "linux-x64";
        cni.RuntimeIdentifierGraphPath = ToolsetUtils.GetRuntimeGraphFilePath();

        Assert.True(cni.Execute());
        newProjectDir.Delete(true);
    }

    /// <summary>
    /// Creates a console app that outputs the environment variable added to the image.
    /// </summary>
    [DockerAvailableFact]
    public void Tasks_EndToEnd_With_EnvironmentVariable_Validation()
    {
        DirectoryInfo newProjectDir = new DirectoryInfo(Path.Combine(TestSettings.TestArtifactsDirectory, nameof(Tasks_EndToEnd_With_EnvironmentVariable_Validation)));

        if (newProjectDir.Exists)
        {
            newProjectDir.Delete(recursive: true);
        }

        newProjectDir.Create();

        new DotnetNewCommand(_testOutput, "console", "-f", ToolsetInfo.CurrentTargetFramework)
            .WithVirtualHive()
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        File.WriteAllText(Path.Combine(newProjectDir.FullName, "Program.cs"), $"Console.Write(Environment.GetEnvironmentVariable(\"GoodEnvVar\"));");

        new DotnetCommand(_testOutput, "build", "--configuration", "release", "/p:runtimeidentifier=linux-x64", $"/p:RuntimeFrameworkVersion=8.0.0-preview.3.23174.8")
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        ParseContainerProperties pcp = new ParseContainerProperties();
        pcp.FullyQualifiedBaseImageName = $"mcr.microsoft.com/{DockerRegistryManager.RuntimeBaseImage}:{DockerRegistryManager.Net8PreviewImageTag}";
        pcp.ContainerRegistry = "";
        pcp.ContainerImageName = "dotnet/envvarvalidation";
        pcp.ContainerImageTag = "latest";

        Dictionary<string, string> dict = new Dictionary<string, string>();
        dict.Add("Value", "Foo");

        pcp.ContainerEnvironmentVariables = new[] { new TaskItem("B@dEnv.Var", dict), new TaskItem("GoodEnvVar", dict) };

        Assert.True(pcp.Execute());
        Assert.Equal("mcr.microsoft.com", pcp.ParsedContainerRegistry);
        Assert.Equal("dotnet/runtime", pcp.ParsedContainerImage);
        Assert.Equal(DockerRegistryManager.Net8PreviewImageTag, pcp.ParsedContainerTag);
        Assert.Single(pcp.NewContainerEnvironmentVariables);
        Assert.Equal("Foo", pcp.NewContainerEnvironmentVariables[0].GetMetadata("Value"));

        Assert.Equal("dotnet/envvarvalidation", pcp.NewContainerImageName);
        Assert.Equal("latest", pcp.NewContainerTags[0]);

        CreateNewImage cni = new CreateNewImage();
        cni.BaseRegistry = pcp.ParsedContainerRegistry;
        cni.BaseImageName = pcp.ParsedContainerImage;
        cni.BaseImageTag = pcp.ParsedContainerTag;
        cni.ImageName = pcp.NewContainerImageName;
        cni.OutputRegistry = pcp.NewContainerRegistry;
        cni.PublishDirectory = Path.Combine(newProjectDir.FullName, "bin", "release", ToolsetInfo.CurrentTargetFramework, "linux-x64");
        cni.WorkingDirectory = "/app";
        cni.Entrypoint = new TaskItem[] { new("/app/Tasks_EndToEnd_With_EnvironmentVariable_Validation") };
        cni.ImageTags = pcp.NewContainerTags;
        cni.ContainerEnvironmentVariables = pcp.NewContainerEnvironmentVariables;
        cni.ContainerRuntimeIdentifier = "linux-x64";
        cni.RuntimeIdentifierGraphPath = ToolsetUtils.GetRuntimeGraphFilePath();
        cni.LocalRegistry = global::Microsoft.NET.Build.Containers.KnownLocalRegistryTypes.Docker;

        Assert.True(cni.Execute());

        ContainerCli.RunCommand(_testOutput, "--rm", $"{pcp.NewContainerImageName}:latest")
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Foo");
    }

    [DockerAvailableFact]
    public async System.Threading.Tasks.Task CreateNewImage_RootlessBaseImage()
    {
        const string RootlessBase ="dotnet/rootlessbase";
        const string AppImage = "dotnet/testimagerootless";
        const string RootlessUser = "101";

        // Build a rootless base runtime image.
        Registry registry = new Registry(ContainerHelpers.TryExpandRegistryToUri(DockerRegistryManager.LocalRegistry));

        ImageBuilder imageBuilder = await registry.GetImageManifestAsync(
            DockerRegistryManager.RuntimeBaseImage,
            DockerRegistryManager.Net8PreviewImageTag,
            "linux-x64",
            ToolsetUtils.GetRuntimeGraphFilePath(),
            cancellationToken: default).ConfigureAwait(false);

        Assert.NotNull(imageBuilder);

        imageBuilder.SetUser(RootlessUser);

        BuiltImage builtImage = imageBuilder.Build();

        var sourceReference = new ImageReference(registry, DockerRegistryManager.RuntimeBaseImage, DockerRegistryManager.Net8PreviewImageTag);
        var destinationReference = new ImageReference(registry, RootlessBase, "latest");

        await registry.PushAsync(builtImage, sourceReference, destinationReference, Console.WriteLine, cancellationToken: default).ConfigureAwait(false);

        // Build an application image on top of the rootless base runtime image.
        DirectoryInfo newProjectDir = new DirectoryInfo(Path.Combine(TestSettings.TestArtifactsDirectory, nameof(CreateNewImage_RootlessBaseImage)));

        if (newProjectDir.Exists)
        {
            newProjectDir.Delete(recursive: true);
        }

        newProjectDir.Create();

        new DotnetNewCommand(_testOutput, "console", "-f", ToolsetInfo.CurrentTargetFramework)
            .WithVirtualHive()
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        new DotnetCommand(_testOutput, "publish", "-c", "Release", "-r", "linux-x64", "--no-self-contained")
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        CreateNewImage task = new CreateNewImage();
        task.BaseRegistry = "localhost:5010";
        task.BaseImageName = RootlessBase;
        task.BaseImageTag = "latest";

        task.OutputRegistry = "localhost:5010";
        task.PublishDirectory = Path.Combine(newProjectDir.FullName, "bin", "Release", ToolsetInfo.CurrentTargetFramework, "linux-x64", "publish");
        task.ImageName = AppImage;
        task.ImageTags = new[] { "latest" };
        task.WorkingDirectory = "app/";
        task.ContainerRuntimeIdentifier = "linux-x64";
        task.Entrypoint = new TaskItem[] { new("dotnet"), new("build") };
        task.RuntimeIdentifierGraphPath = ToolsetUtils.GetRuntimeGraphFilePath();

        Assert.True(task.Execute());
        newProjectDir.Delete(true);

        // Verify the application image uses the non-root user from the base image.
        imageBuilder = await registry.GetImageManifestAsync(
            AppImage,
            "latest",
            "linux-x64",
            ToolsetUtils.GetRuntimeGraphFilePath(),
            cancellationToken: default).ConfigureAwait(false);

        Assert.Equal(RootlessUser, imageBuilder.BaseImageConfig.User);
    }
}
