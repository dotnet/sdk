// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using FakeItEasy;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Build.Containers.IntegrationTests;
using Microsoft.NET.Build.Containers.UnitTests;
using NuGet.Protocol;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.NET.Build.Containers.Tasks.IntegrationTests;

[Collection("Docker tests")]
public class CreateImageIndexTests
{
    private ITestOutputHelper _testOutput;

    public CreateImageIndexTests(ITestOutputHelper testOutput)
    {
        _testOutput = testOutput;
    }

    [DockerAvailableFact]
    public async Task CreateImageIndex_Baseline()
    {
        DirectoryInfo newProjectDir = CreateNewProject();
        (IBuildEngine buildEngine, List<string?> errors) = SetupBuildEngine();
        string outputRegistry = DockerRegistryManager.LocalRegistry;
        string repository = "dotnet/create-image-index-baseline";
        string[] tags = new[] { "tag1", "tag2" };

        // Create images for 2 rids
        TaskItem image1 = PublishAndCreateNewImage("linux-x64", outputRegistry, repository, tags, newProjectDir, buildEngine, errors);
        TaskItem image2 = PublishAndCreateNewImage("linux-arm64", outputRegistry, repository, tags, newProjectDir, buildEngine, errors);

        // Create image index
        CreateImageIndex cii = new();
        cii.BuildEngine = buildEngine;
        cii.OutputRegistry = outputRegistry;
        cii.Repository = repository;
        cii.ImageTags = tags;
        cii.GeneratedContainers = [image1, image2];
        Assert.True(cii.Execute(), FormatBuildMessages(errors));

        // Assert that the image index is created correctly
        cii.GeneratedImageIndex.Should().NotBeNullOrEmpty();
        var imageIndex = cii.GeneratedImageIndex.FromJson<ManifestListV2>();
        imageIndex.manifests.Should().HaveCount(2);

        imageIndex.manifests[0].digest.Should().Be(image1.GetMetadata("ManifestDigest"));
        imageIndex.manifests[0].platform.os.Should().Be("linux");
        imageIndex.manifests[0].platform.architecture.Should().Be("amd64");

        imageIndex.manifests[1].digest.Should().Be(image2.GetMetadata("ManifestDigest"));
        imageIndex.manifests[1].platform.os.Should().Be("linux");
        imageIndex.manifests[1].platform.architecture.Should().Be("arm64");

        // Assert that the image index is pushed to the registry
        var loggerFactory = new TestLoggerFactory(_testOutput);
        var logger = loggerFactory.CreateLogger(nameof(CreateImageIndex_Baseline));
        Registry registry = new(outputRegistry, logger, RegistryMode.Pull);

        await AssertThatImageIsReferencedInImageIndex("linux-x64", repository, tags, registry);
        await AssertThatImageIsReferencedInImageIndex("linux-arm64", repository, tags, registry);

        newProjectDir.Delete(true);
    }

    private DirectoryInfo CreateNewProject()
    {
        DirectoryInfo newProjectDir = new(GetTestDirectoryName());
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
        return newProjectDir;
    }

    private TaskItem PublishAndCreateNewImage(
        string rid,
        string outputRegistry,
        string repository,
        string[] tags,
        DirectoryInfo newProjectDir,
        IBuildEngine buildEngine,
        List<string?> errors)
    {
        new DotnetCommand(_testOutput, "publish", "-c", "Release", "-r", rid, "--no-self-contained")
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        CreateNewImage cni = new();

        cni.BuildEngine = buildEngine;

        cni.BaseRegistry = "mcr.microsoft.com";
        cni.BaseImageName = "dotnet/runtime";
        cni.BaseImageTag = "7.0";

        cni.OutputRegistry = outputRegistry;
        cni.LocalRegistry = DockerAvailableFactAttribute.LocalRegistry;
        cni.PublishDirectory = Path.Combine(newProjectDir.FullName, "bin", "Release", ToolsetInfo.CurrentTargetFramework, rid, "publish");
        cni.Repository = repository;
        cni.ImageTags = tags.Select(t => $"{t}-{rid}").ToArray();
        cni.WorkingDirectory = "app/";
        cni.ContainerRuntimeIdentifier = rid;
        cni.Entrypoint = new TaskItem[] { new("dotnet"), new("build") };
        cni.RuntimeIdentifierGraphPath = ToolsetUtils.GetRuntimeGraphFilePath();

        Assert.True(cni.Execute(), FormatBuildMessages(errors));

        TaskItem generatedContainer = new("GeneratedContainer" + rid);
        generatedContainer.SetMetadata("Manifest", cni.GeneratedContainerManifest);
        generatedContainer.SetMetadata("Configuration", cni.GeneratedContainerConfiguration);
        generatedContainer.SetMetadata("ManifestDigest", cni.GeneratedContainerDigest);
        generatedContainer.SetMetadata("ManifestMediaType", cni.GeneratedContainerMediaType);

        return generatedContainer;
    }

    private async Task AssertThatImageIsReferencedInImageIndex(string rid, string repository, string[] tags, Registry registry)
    {
        foreach (var tag in tags)
        {
            var individualImage = await registry.GetImageManifestAsync(
            repository,
            $"{tag}-{rid}",
            rid,
            ToolsetUtils.RidGraphManifestPicker,
            cancellationToken: default).ConfigureAwait(false);
            individualImage.Should().NotBeNull();

            var imageFromImageIndex = await registry.GetImageManifestAsync(
                repository,
                tag,
                rid,
                ToolsetUtils.RidGraphManifestPicker,
                cancellationToken: default).ConfigureAwait(false);
            imageFromImageIndex.Should().NotBeNull();

            imageFromImageIndex.ManifestConfigDigest.Should().Be(individualImage.ManifestConfigDigest);
        }
    }

    private static (IBuildEngine buildEngine, List<string?> errors) SetupBuildEngine()
    {
        List<string?> errors = new();
        IBuildEngine buildEngine = A.Fake<IBuildEngine>();
        A.CallTo(() => buildEngine.LogWarningEvent(A<BuildWarningEventArgs>.Ignored)).Invokes((BuildWarningEventArgs e) => errors.Add(e.Message));
        A.CallTo(() => buildEngine.LogErrorEvent(A<BuildErrorEventArgs>.Ignored)).Invokes((BuildErrorEventArgs e) => errors.Add(e.Message));
        A.CallTo(() => buildEngine.LogMessageEvent(A<BuildMessageEventArgs>.Ignored)).Invokes((BuildMessageEventArgs e) => errors.Add(e.Message));

        return (buildEngine, errors);
    }

    private static string GetTestDirectoryName([CallerMemberName] string testName = "DefaultTest") => Path.Combine(TestSettings.TestArtifactsDirectory, testName + "_" + DateTime.Now.ToString("yyyyMMddHHmmss"));

    private static string FormatBuildMessages(List<string?> messages) => string.Join("\r\n", messages);
}

