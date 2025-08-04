// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using FakeItEasy;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Build.Containers.UnitTests;
using Microsoft.NET.Build.Containers.IntegrationTests;
using System.Threading.Tasks;
using System.Globalization;
using Task = System.Threading.Tasks.Task;
using FakeItEasy.Sdk;

namespace Microsoft.NET.Build.Containers.Tasks.IntegrationTests;

public class CreateNewImageTests(ITestOutputHelper testOutput, HelixTransientTestFolderFixture testFolder) : IClassFixture<HelixTransientTestFolderFixture>
{
    [Fact]
    public void CreateNewImage_Baseline()
    {
        DirectoryInfo newProjectDir = new(GetTestDirectoryName());
        newProjectDir.Create();

        new DotnetNewCommand(testOutput, "console", "-f", ToolsetInfo.CurrentTargetFramework)
            .WithVirtualHive()
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        new DotnetCommand(testOutput, "publish", "-c", "Release", "-r", "linux-arm64", "--no-self-contained")
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        (CreateNewImage task, var errors) = CreateTask<CreateNewImage>(task =>
        {
            task.BaseRegistry = "mcr.microsoft.com";
            task.BaseImageName = "dotnet/runtime";
            task.BaseImageTag = "7.0";

            task.OutputRegistry = "localhost:5010";
            task.LocalRegistry = DockerAvailableFactAttribute.LocalRegistry;
            task.Repository = "dotnet/create-new-image-baseline";
            task.ImageTags = new[] { "latest" };
            task.WorkingDirectory = "app/";
            task.Entrypoint = new TaskItem[] { new("dotnet"), new("build") };
        });


        Assert.True(task.Execute(), FormatBuildMessages(errors));
    }

    [Fact]
    public async Task ParseContainerProperties_EndToEnd()
    {
        DirectoryInfo newProjectDir = new(GetTestDirectoryName());
        newProjectDir.Create();

        new DotnetNewCommand(testOutput, "console", "-f", ToolsetInfo.CurrentTargetFramework)
            .WithVirtualHive()
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        new DotnetCommand(testOutput, "build", "--configuration", "release")
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        (ParseContainerProperties pcp, var errors) = CreateTask<ParseContainerProperties>(pcp =>
        {
            pcp.FullyQualifiedBaseImageName = "mcr.microsoft.com/dotnet/runtime:7.0";
            pcp.ContainerRegistry = "localhost:5010";
            pcp.ContainerRepository = "dotnet/testimage";
            pcp.ContainerImageTags = new[] { "5.0", "latest" };
        });

        Assert.True(pcp.Execute(), FormatBuildMessages(errors));
        Assert.Equal("mcr.microsoft.com", pcp.ParsedContainerRegistry);
        Assert.Equal("dotnet/runtime", pcp.ParsedContainerImage);
        Assert.Equal("7.0", pcp.ParsedContainerTag);

        Assert.Equal("dotnet/testimage", pcp.NewContainerRepository);
        pcp.NewContainerTags.Should().BeEquivalentTo(new[] { "5.0", "latest" });

        (DownloadContainerManifest downloadContainerManifest, errors) = CreateTask<DownloadContainerManifest>(downloadContainerManifest =>
        {
            downloadContainerManifest.Registry = pcp.ParsedContainerRegistry;
            downloadContainerManifest.Repository = pcp.ParsedContainerImage;
            downloadContainerManifest.Tag = pcp.ParsedContainerTag;
            downloadContainerManifest.ContentStore = testFolder.TestFolder.FullName;
        });
        downloadContainerManifest.Execute().Should().BeTrue();

        (CreateNewImage cni, errors) = await CreateTask<CreateNewImage>(async cni =>
        {
            cni.BaseRegistry = pcp.ParsedContainerRegistry;
            cni.BaseImageName = pcp.ParsedContainerImage;
            cni.BaseImageTag = pcp.ParsedContainerTag;

            cni.BaseImageManifestPath = downloadContainerManifest.Manifests.Single(m => m.GetMetadata("RuntimeIdentifier") == "linux-x64");
            cni.BaseImageConfigurationPath = downloadContainerManifest.Configs.Single(m => m.GetMetadata("RuntimeIdentifier") == "linux-x64");

            ITaskItem manualLayerTarball = await CreateLayerTarballForDirectory(newProjectDir);
            cni.GeneratedApplicationLayer = manualLayerTarball;
            cni.GeneratedManifestPath = Path.Combine(newProjectDir.FullName, "dummy-manifest.json");
            cni.GeneratedConfigurationPath = Path.Combine(newProjectDir.FullName, "dummy-configuration.json");
            cni.ContentStoreRoot = testFolder.TestFolder.FullName;

            cni.Repository = pcp.NewContainerRepository;
            cni.OutputRegistry = "localhost:5010";
            cni.WorkingDirectory = "app/";
            cni.Entrypoint = new TaskItem[] { new(newProjectDir.Name) };
            cni.ImageTags = pcp.NewContainerTags;
            cni.GenerateLabels = false;
            cni.GenerateDigestLabel = false;
        });
        Assert.True(cni.Execute(), FormatBuildMessages(errors));
    }

    /// <summary>
    /// Creates a console app that outputs the environment variable added to the image.
    /// </summary>
    [DockerAvailableFact()]
    public void Tasks_EndToEnd_With_EnvironmentVariable_Validation()
    {
        DirectoryInfo newProjectDir = new(GetTestDirectoryName());
        newProjectDir.Create();

        new DotnetNewCommand(testOutput, "console", "-f", ToolsetInfo.CurrentTargetFramework)
            .WithVirtualHive()
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        EndToEndTests.ChangeTargetFrameworkAfterAppCreation(newProjectDir.FullName);

        File.WriteAllText(Path.Combine(newProjectDir.FullName, "Program.cs"), $"Console.Write(Environment.GetEnvironmentVariable(\"GoodEnvVar\"));");

        new DotnetCommand(testOutput, "build", "--configuration", "release", "/p:runtimeidentifier=linux-x64")
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        (ParseContainerProperties pcp, List<string?> errors) = CreateTask<ParseContainerProperties>(pcp =>
        {
            pcp.FullyQualifiedBaseImageName = $"mcr.microsoft.com/{DockerRegistryManager.RuntimeBaseImage}:{DockerRegistryManager.Net9ImageTag}";
            pcp.ContainerRegistry = "";
            pcp.ContainerRepository = "dotnet/envvarvalidation";
            pcp.ContainerImageTag = "latest";

            Dictionary<string, string> dict = new()
            {
                { "Value", "Foo" }
            };
            pcp.ContainerEnvironmentVariables = [new TaskItem("B@dEnv.Var", dict), new TaskItem("GoodEnvVar", dict)];
        });

        Assert.True(pcp.Execute(), FormatBuildMessages(errors));
        Assert.Equal("mcr.microsoft.com", pcp.ParsedContainerRegistry);
        Assert.Equal("dotnet/runtime", pcp.ParsedContainerImage);
        Assert.Equal(DockerRegistryManager.Net9ImageTag, pcp.ParsedContainerTag);
        Assert.Single(pcp.NewContainerEnvironmentVariables);
        Assert.Equal("Foo", pcp.NewContainerEnvironmentVariables[0].GetMetadata("Value"));

        Assert.Equal("dotnet/envvarvalidation", pcp.NewContainerRepository);
        Assert.Equal("latest", pcp.NewContainerTags[0]);

        (CreateNewImage cni, errors) = CreateTask<CreateNewImage>(cni =>
        {
            cni.BaseRegistry = pcp.ParsedContainerRegistry;
            cni.BaseImageName = pcp.ParsedContainerImage;
            cni.BaseImageTag = pcp.ParsedContainerTag;
            cni.Repository = pcp.NewContainerRepository;
            cni.OutputRegistry = pcp.ParsedContainerRegistry;
            cni.ImageTags = pcp.NewContainerTags;
            cni.WorkingDirectory = "app/";
            cni.Entrypoint = new TaskItem[] { new(newProjectDir.Name) };
            cni.ContainerEnvironmentVariables = pcp.NewContainerEnvironmentVariables;
            cni.BaseRegistry = pcp.ParsedContainerRegistry;
            cni.BaseImageName = pcp.ParsedContainerImage;
            cni.BaseImageTag = pcp.ParsedContainerTag;
            cni.Repository = pcp.NewContainerRepository;
            cni.OutputRegistry = pcp.NewContainerRegistry;
            cni.WorkingDirectory = "/app";
            cni.Entrypoint = new TaskItem[] { new($"/app/{newProjectDir.Name}") };
            cni.ImageTags = pcp.NewContainerTags;
            cni.ContainerEnvironmentVariables = pcp.NewContainerEnvironmentVariables;
            cni.LocalRegistry = DockerAvailableFactAttribute.LocalRegistry;
        });

        Assert.True(cni.Execute(), FormatBuildMessages(errors));

        var config = GetImageConfigFromTask(cni);
        // because we're building off of .net 8 images for this test, we can validate the user id and aspnet https urls
        Assert.Equal("1654", config.Config?.User);

        var ports = config.Config?.ExposedPorts!;
        Assert.Single(ports);
        Assert.Equal(new(8080, PortType.tcp), ports.First());

        ContainerCli.RunCommand(testOutput, "--rm", $"{pcp.NewContainerRepository}:latest")
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Foo");
    }

    [Fact]
    public async Task CreateNewImage_RootlessBaseImage()
    {
        const string RootlessBase = "dotnet/rootlessbase";
        const string AppImage = "dotnet/testimagerootless";
        const string RootlessUser = "1654";
        var loggerFactory = new TestLoggerFactory(testOutput);
        var logger = loggerFactory.CreateLogger(nameof(CreateNewImage_RootlessBaseImage));

        // Build a rootless base runtime image.
        Registry registry = new(DockerRegistryManager.LocalRegistry, logger, RegistryMode.Push);

        ImageBuilder imageBuilder = await registry.GetImageManifestAsync(
            DockerRegistryManager.RuntimeBaseImage,
            DockerRegistryManager.Net8ImageTag,
            "linux-x64",
            ToolsetUtils.RidGraphManifestPicker,
            cancellationToken: default);

        Assert.NotNull(imageBuilder);
        BuiltImage builtImage = imageBuilder.Build();

        var sourceReference = new SourceImageReference(registry, DockerRegistryManager.RuntimeBaseImage, DockerRegistryManager.Net8ImageTag, null);
        var destinationReference = new DestinationImageReference(registry, RootlessBase, new[] { "latest" });

        await registry.PushAsync(builtImage, sourceReference, destinationReference, cancellationToken: default);

        // Build an application image on top of the rootless base runtime image.
        DirectoryInfo newProjectDir = new(Path.Combine(TestSettings.TestArtifactsDirectory, nameof(CreateNewImage_RootlessBaseImage)));
        newProjectDir.Create();

        new DotnetNewCommand(testOutput, "console", "-f", ToolsetInfo.CurrentTargetFramework)
            .WithVirtualHive()
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        new DotnetCommand(testOutput, "publish", "-c", "Release", "-r", "linux-x64", "--no-self-contained")
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        (CreateNewImage task, var errors) = CreateTask<CreateNewImage>(task =>
        {
            task.BaseRegistry = "localhost:5010";
            task.BaseImageName = RootlessBase;
            task.BaseImageTag = "latest";

            task.OutputRegistry = "localhost:5010";
            task.Repository = AppImage;
            task.ImageTags = new[] { "latest" };
            task.WorkingDirectory = "app/";
            task.Entrypoint = new TaskItem[] { new("dotnet"), new("build") };
        });

        Assert.True(task.Execute());

        // Verify the application image uses the non-root user from the base image.
        imageBuilder = await registry.GetImageManifestAsync(
            AppImage,
            "latest",
            "linux-x64",
            ToolsetUtils.RidGraphManifestPicker,
            cancellationToken: default);

        Assert.Equal(RootlessUser, imageBuilder.BaseImageConfig.User);
    }


    [DockerAvailableFact()]
    public void CanOverrideContainerImageFormat()
    {
        DirectoryInfo newProjectDir = new(GetTestDirectoryName());
        newProjectDir.Create();

        new DotnetNewCommand(testOutput, "console", "-f", ToolsetInfo.CurrentTargetFramework)
            .WithVirtualHive()
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        new DotnetCommand(testOutput, "build", "--configuration", "release")
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        (ParseContainerProperties pcp, var errors) = CreateTask<ParseContainerProperties>(pcp =>
        {
            pcp.FullyQualifiedBaseImageName = "mcr.microsoft.com/dotnet/runtime:9.0";
            pcp.ContainerRegistry = "localhost:5010";
            pcp.ContainerRepository = "dotnet/testimage";
            pcp.ContainerImageTags = new[] { "5.0", "latest" };
        });
        Assert.True(pcp.Execute(), FormatBuildMessages(errors));
        Assert.Equal("mcr.microsoft.com", pcp.ParsedContainerRegistry);
        Assert.Equal("dotnet/runtime", pcp.ParsedContainerImage);
        Assert.Equal("9.0", pcp.ParsedContainerTag);

        Assert.Equal("dotnet/testimage", pcp.NewContainerRepository);
        pcp.NewContainerTags.Should().BeEquivalentTo(new[] { "5.0", "latest" });

        (CreateNewImage cni, errors) = CreateTask<CreateNewImage>(cni =>
        {
            cni.BaseRegistry = pcp.ParsedContainerRegistry;
            cni.BaseImageName = pcp.ParsedContainerImage;
            cni.BaseImageTag = pcp.ParsedContainerTag;
            cni.Repository = pcp.NewContainerRepository;
            cni.OutputRegistry = "localhost:5010";
            cni.WorkingDirectory = "app/";
            cni.Entrypoint = new TaskItem[] { new(newProjectDir.Name) };
            cni.ImageTags = pcp.NewContainerTags;

            cni.ImageFormat = KnownImageFormats.OCI.ToString();
        });

        Assert.True(cni.Execute(), FormatBuildMessages(errors));
        cni.GeneratedContainerMediaType.Should().Be(SchemaTypes.OciManifestV1);
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

    private string GetTestDirectoryName([CallerMemberName] string testName = "DefaultTest") => Path.Combine(testFolder.TestFolder.FullName, testName + "_" + DateTime.Now.ToString("yyyyMMddHHmmss"));

    private static string FormatBuildMessages(List<string?> messages) => string.Join("\r\n", messages);

    private static Image GetImageConfigFromTask(CreateNewImage task) => Json.Deserialize<Image>(task.GeneratedContainerConfiguration)!;

    private async Task<ITaskItem> CreateLayerTarballForDirectory(DirectoryInfo directoryToCompress)
    {
        var fakeTarballPath = Path.Combine(testFolder.TestFolder.FullName, $"fake{DateTime.Now:yyyyMMddHHmmss}.tar");
        var compressedPath = fakeTarballPath + ".gz";
        TarFile.CreateFromDirectory(directoryToCompress.FullName, fakeTarballPath, includeBaseDirectory: false);
        using (var readStream = File.OpenRead(fakeTarballPath))
        using (var destStream = File.OpenWrite(compressedPath))
        using (var zipStream = new GZipStream(destStream, CompressionMode.Compress, leaveOpen: true))
        {
            readStream.CopyTo(zipStream);
        }
        var layerItem = new TaskItem(compressedPath);
        using (var layerStream = File.OpenRead(compressedPath))
        {
            ApplyDescriptorMetadata(layerItem, await Layer.DescriptorFromStream(layerStream, SchemaTypes.DockerLayerGzip, DigestAlgorithm.sha256));
        }
        return layerItem;
    }

    private static void ApplyDescriptorMetadata(ITaskItem item, Descriptor descriptor)
    {
        item.SetMetadata("MediaType", descriptor.MediaType);
        item.SetMetadata("Digest", descriptor.Digest.ToString());
        item.SetMetadata("Size", descriptor.Size.ToString(CultureInfo.InvariantCulture));
    }

    private (T task, List<string?> errors) CreateTask<T>(Action<T> configure) where T : Microsoft.Build.Utilities.Task, new()
    {
        T task = new();
        (IBuildEngine buildEngine, List<string?> errors) = SetupBuildEngine();
        task.BuildEngine = buildEngine;
        configure(task);
        return (task, errors);
    }

    private async Task<(T task, List<string?> errors)> CreateTask<T>(Func<T, Task> configure) where T : Microsoft.Build.Utilities.Task, new()
    {
        T task = new();
        (IBuildEngine buildEngine, List<string?> errors) = SetupBuildEngine();
        task.BuildEngine = buildEngine;
        await configure(task);
        return (task, errors);
    }
}
