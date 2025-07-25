﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Formats.Tar;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FakeItEasy;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Build.Containers.LocalDaemons;
using Microsoft.NET.Build.Containers.Resources;
using Microsoft.NET.Build.Containers.UnitTests;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

[Collection("Docker tests")]
public class EndToEndTests : IDisposable
{
    private ITestOutputHelper _testOutput;
    private readonly TestLoggerFactory _loggerFactory;

    public EndToEndTests(ITestOutputHelper testOutput)
    {
        _testOutput = testOutput;
        _loggerFactory = new TestLoggerFactory(testOutput);
    }

    public static string NewImageName([CallerMemberName] string callerMemberName = "")
    {
        var (normalizedName, warning, error) = ContainerHelpers.NormalizeRepository(callerMemberName);
        if (error is (var format, var args))
        {
            throw new ArgumentException(string.Format(Strings.ResourceManager.GetString(format)!, args));
        }

        return normalizedName!; // non-null if error is null
    }

    public void Dispose()
    {
        _loggerFactory.Dispose();
    }

    internal static readonly string _oldFramework = "net9.0";
    // CLI will not let us to target net9.0 anymore but we still need it because images for net10.0 aren't ready yet.
    // so we let it create net10.0 app, then change the target. Since we're building just small sample applications, it works.
    internal static void ChangeTargetFrameworkAfterAppCreation(string path)
    {
        DirectoryInfo d = new DirectoryInfo(path);
        FileInfo[] Files = d.GetFiles("*.csproj"); //Getting .csproj files
        string csprojFilename = Files[0].Name; // There is only one
        string text = File.ReadAllText(Path.Combine(path, csprojFilename));
        text = text.Replace("net10.0", _oldFramework);
        File.WriteAllText(Path.Combine(path, csprojFilename), text);
    }

    [DockerAvailableFact(Skip = "https://github.com/dotnet/sdk/issues/49502")]
    public async Task ApiEndToEndWithRegistryPushAndPull()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(ApiEndToEndWithRegistryPushAndPull));
        string publishDirectory = BuildLocalApp();

        // Build the image

        Registry registry = new(DockerRegistryManager.LocalRegistry, logger, RegistryMode.Push);

        ImageBuilder imageBuilder = await registry.GetImageManifestAsync(
            DockerRegistryManager.RuntimeBaseImage,
            DockerRegistryManager.Net9ImageTag,
            "linux-x64",
            ToolsetUtils.RidGraphManifestPicker,
            cancellationToken: default).ConfigureAwait(false);

        Assert.NotNull(imageBuilder);

        Layer l = Layer.FromDirectory(publishDirectory, "/app", false, imageBuilder.ManifestMediaType);

        imageBuilder.AddLayer(l);

        imageBuilder.SetEntrypointAndCmd(new[] { "/app/MinimalTestApp" }, Array.Empty<string>());

        BuiltImage builtImage = imageBuilder.Build();

        // Push the image back to the local registry
        var sourceReference = new SourceImageReference(registry, DockerRegistryManager.RuntimeBaseImage, DockerRegistryManager.Net9ImageTag, null);
        var destinationReference = new DestinationImageReference(registry, NewImageName(), new[] { "latest", "1.0" });

        await registry.PushAsync(builtImage, sourceReference, destinationReference, cancellationToken: default).ConfigureAwait(false);

        foreach (string tag in destinationReference.Tags)
        {
            // pull it back locally
            ContainerCli.PullCommand(_testOutput, $"{DockerRegistryManager.LocalRegistry}/{NewImageName()}:{tag}")
                .Execute()
                .Should().Pass();

            // Run the image
            ContainerCli.RunCommand(_testOutput, "--rm", "--tty", $"{DockerRegistryManager.LocalRegistry}/{NewImageName()}:{tag}")
                .Execute()
                .Should().Pass();
        }
    }

    [DockerAvailableFact(Skip = "https://github.com/dotnet/sdk/issues/49502")]
    public async Task ApiEndToEndWithLocalLoad()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(ApiEndToEndWithLocalLoad));
        string publishDirectory = BuildLocalApp(tfm: ToolsetInfo.CurrentTargetFramework);

        // Build the image

        Registry registry = new(DockerRegistryManager.LocalRegistry, logger, RegistryMode.Push);

        ImageBuilder imageBuilder = await registry.GetImageManifestAsync(
            DockerRegistryManager.RuntimeBaseImage,
            DockerRegistryManager.Net9ImageTag,
            "linux-x64",
            ToolsetUtils.RidGraphManifestPicker,
            cancellationToken: default).ConfigureAwait(false);
        Assert.NotNull(imageBuilder);

        Layer l = Layer.FromDirectory(publishDirectory, "/app", false, imageBuilder.ManifestMediaType);

        imageBuilder.AddLayer(l);

        imageBuilder.SetEntrypointAndCmd(new[] { "/app/MinimalTestApp" }, Array.Empty<string>());

        BuiltImage builtImage = imageBuilder.Build();

        // Load the image into the local registry
        var sourceReference = new SourceImageReference(registry, DockerRegistryManager.RuntimeBaseImage, DockerRegistryManager.Net9ImageTag, null);
        var destinationReference = new DestinationImageReference(registry, NewImageName(), new[] { "latest", "1.0" });

        await new DockerCli(_loggerFactory).LoadAsync(builtImage, sourceReference, destinationReference, default).ConfigureAwait(false);

        // Run the image
        foreach (string tag in destinationReference.Tags)
        {
            ContainerCli.RunCommand(_testOutput, "--rm", "--tty", $"{NewImageName()}:{tag}")
                .Execute()
                .Should().Pass();
        }
    }

    [DockerAvailableFact(Skip = "https://github.com/dotnet/sdk/issues/49502")]
    public async Task ApiEndToEndWithArchiveWritingAndLoad()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(ApiEndToEndWithArchiveWritingAndLoad));
        string publishDirectory = BuildLocalApp(tfm: ToolsetInfo.CurrentTargetFramework);

        // Build the image

        Registry registry = new(DockerRegistryManager.LocalRegistry, logger, RegistryMode.Push);

        ImageBuilder imageBuilder = await registry.GetImageManifestAsync(
            DockerRegistryManager.RuntimeBaseImage,
            DockerRegistryManager.Net9ImageTag,
            "linux-x64",
            ToolsetUtils.RidGraphManifestPicker,
            cancellationToken: default).ConfigureAwait(false);
        Assert.NotNull(imageBuilder);

        Layer l = Layer.FromDirectory(publishDirectory, "/app", false, imageBuilder.ManifestMediaType);

        imageBuilder.AddLayer(l);

        imageBuilder.SetEntrypointAndCmd(new[] { "/app/MinimalTestApp" }, Array.Empty<string>());

        BuiltImage builtImage = imageBuilder.Build();

        // Write the image to disk
        var archiveFile = Path.Combine(TestSettings.TestArtifactsDirectory,
            nameof(ApiEndToEndWithArchiveWritingAndLoad), "app.tar.gz");
        var sourceReference = new SourceImageReference(registry, DockerRegistryManager.RuntimeBaseImage, DockerRegistryManager.Net9ImageTag, null);
        var destinationReference = new DestinationImageReference(new ArchiveFileRegistry(archiveFile), NewImageName(), new[] { "latest", "1.0" });

        await destinationReference.LocalRegistry!.LoadAsync(builtImage, sourceReference, destinationReference, default).ConfigureAwait(false);

        Assert.True(File.Exists(archiveFile), $"File.Exists({archiveFile})");

        // Load the archive
        ContainerCli.LoadCommand(_testOutput, "--input", archiveFile)
            .Execute()
            .Should().Pass();

        // Run the image
        foreach (string tag in destinationReference.Tags)
        {
            ContainerCli.RunCommand(_testOutput, "--rm", "--tty", $"{NewImageName()}:{tag}")
                .Execute()
                .Should().Pass();
        }
    }

    [DockerAvailableFact(Skip = "https://github.com/dotnet/sdk/issues/49502")]
    public async Task TarballsHaveCorrectStructure()
    {
        var archiveFile = Path.Combine(TestSettings.TestArtifactsDirectory,
            nameof(TarballsHaveCorrectStructure), "app.tar.gz");

        // 1. Create docker image and write it to a tarball
        (BuiltImage dockerImage, SourceImageReference sourceReference, DestinationImageReference destinationReference) =
            await BuildDockerImageWithArciveDestinationAsync(archiveFile, ["latest"], nameof(TarballsHaveCorrectStructure));

        await destinationReference.LocalRegistry!.LoadAsync(dockerImage, sourceReference, destinationReference, default).ConfigureAwait(false);

        Assert.True(File.Exists(archiveFile), $"File.Exists({archiveFile})");

        CheckDockerTarballStructure(archiveFile);

        // 2. Convert the docker image to an OCI image and write it to a tarball
        BuiltImage ociImage = ConvertToOciImage(dockerImage);

        await destinationReference.LocalRegistry!.LoadAsync(ociImage, sourceReference, destinationReference, default).ConfigureAwait(false);

        Assert.True(File.Exists(archiveFile), $"File.Exists({archiveFile})");

        CheckOciTarballStructure(archiveFile);
    }

    private async Task<(BuiltImage image, SourceImageReference sourceReference, DestinationImageReference destinationReference)> BuildDockerImageWithArciveDestinationAsync(string archiveFile, string[] tags, string testName)
    {
        ILogger logger = _loggerFactory.CreateLogger(testName);
        Registry registry = new(DockerRegistryManager.LocalRegistry, logger, RegistryMode.Push);

        ImageBuilder imageBuilder = await registry.GetImageManifestAsync(
            DockerRegistryManager.RuntimeBaseImage,
            DockerRegistryManager.Net8ImageTag,
            "linux-x64",
            ToolsetUtils.RidGraphManifestPicker,
            cancellationToken: default).ConfigureAwait(false);
        Assert.NotNull(imageBuilder);

        BuiltImage builtImage = imageBuilder.Build();

        // Write the image to disk
        var sourceReference = new SourceImageReference(registry, DockerRegistryManager.RuntimeBaseImage, DockerRegistryManager.Net7ImageTag, null);
        var destinationReference = new DestinationImageReference(new ArchiveFileRegistry(archiveFile), NewImageName(), tags);

        return (builtImage, sourceReference, destinationReference);
    }

    private BuiltImage ConvertToOciImage(BuiltImage builtImage)
    {
        // Convert the image to an OCI image
        var ociImage = new BuiltImage
        {
            Config = builtImage.Config,
            ImageDigest = builtImage.ImageDigest,
            ImageSha = builtImage.ImageSha,
            Manifest = builtImage.Manifest,
            ManifestDigest = builtImage.ManifestDigest,
            ManifestMediaType = SchemaTypes.OciManifestV1,
            Layers = builtImage.Layers
        };

        return ociImage;
    }

    private void CheckDockerTarballStructure(string tarball)
    {
        var layersCount = 0;
        int configJson = 0;
        int manifestJsonCount = 0;

        using (FileStream fs = new FileStream(tarball, FileMode.Open, FileAccess.Read))
        using (var tarReader = new TarReader(fs))
        {
            var entry = tarReader.GetNextEntry();

            while (entry is not null)
            {
                if (entry.Name == "manifest.json")
                {
                    manifestJsonCount++;
                }
                else if (entry.Name.EndsWith(".json"))
                {
                    configJson++;
                }
                else if (entry.Name.EndsWith("/layer.tar"))
                {
                    layersCount++;
                }
                else
                {
                    Assert.Fail($"Unexpected entry in tarball: {entry.Name}");
                }

                entry = tarReader.GetNextEntry();
            }
        }

        Assert.Equal(1, manifestJsonCount);
        Assert.Equal(1, configJson);
        Assert.True(layersCount > 0);
    }

    private void CheckOciTarballStructure(string tarball)
    {
        int blobsCount = 0;
        int ociLayoutCount = 0;
        int indexJsonCount = 0;

        using (FileStream fs = new FileStream(tarball, FileMode.Open, FileAccess.Read))
        using (var tarReader = new TarReader(fs))
        {
            var entry = tarReader.GetNextEntry();

            while (entry is not null)
            {
                if (entry.Name == "oci-layout")
                {
                    ociLayoutCount++;
                }
                else if (entry.Name == "index.json")
                {
                    indexJsonCount++;
                }
                else if (entry.Name.StartsWith("blobs/sha256/"))
                {
                    blobsCount++;
                }
                else
                {
                    Assert.Fail($"Unexpected entry in tarball: {entry.Name}");
                }

                entry = tarReader.GetNextEntry();
            }
        }

        Assert.Equal(1, ociLayoutCount);
        Assert.Equal(1, indexJsonCount);
        Assert.True(blobsCount > 0);
    }

    private string BuildLocalApp([CallerMemberName] string testName = "TestName", string tfm = ToolsetInfo.CurrentTargetFramework, string rid = "linux-x64")
    {
        string workingDirectory = Path.Combine(TestSettings.TestArtifactsDirectory, testName);

        DirectoryInfo d = new(Path.Combine(workingDirectory, "MinimalTestApp"));
        if (d.Exists)
        {
            d.Delete(recursive: true);
        }
        Directory.CreateDirectory(workingDirectory);

        new DotnetNewCommand(_testOutput, "console", "-f", tfm, "-o", "MinimalTestApp")
            .WithVirtualHive()
            .WithWorkingDirectory(workingDirectory)
            .Execute()
            .Should().Pass();

        ChangeTargetFrameworkAfterAppCreation(Path.Combine(TestSettings.TestArtifactsDirectory, testName, "MinimalTestApp"));


        var publishCommand =
            new DotnetCommand(_testOutput, "publish", "-bl", "MinimalTestApp", "-r", rid, "-f", _oldFramework, "-c", "Debug")
                .WithWorkingDirectory(workingDirectory);

        publishCommand.Execute()
            .Should().Pass();

        string publishDirectory = Path.Join(workingDirectory, "MinimalTestApp", "bin", "Debug", _oldFramework, rid, "publish");
        return publishDirectory;
    }

    [DockerAvailableFact(Skip = "https://github.com/dotnet/sdk/issues/49502")]
    public async Task EndToEnd_MultiProjectSolution()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(EndToEnd_MultiProjectSolution));
        DirectoryInfo newSolutionDir = new(Path.Combine(TestSettings.TestArtifactsDirectory, nameof(EndToEnd_MultiProjectSolution)));

        if (newSolutionDir.Exists)
        {
            newSolutionDir.Delete(recursive: true);
        }

        newSolutionDir.Create();

        // Create solution with projects
        new DotnetNewCommand(_testOutput, "sln", "-n", nameof(EndToEnd_MultiProjectSolution))
            .WithVirtualHive()
            .WithWorkingDirectory(newSolutionDir.FullName)
            .Execute()
            .Should().Pass();

        new DotnetNewCommand(_testOutput, "console", "-n", "ConsoleApp")
            .WithVirtualHive()
            .WithWorkingDirectory(newSolutionDir.FullName)
            .Execute()
            .Should().Pass();

        new DotnetCommand(_testOutput, "sln", "add", Path.Combine("ConsoleApp", "ConsoleApp.csproj"))
            .WithWorkingDirectory(newSolutionDir.FullName)
            .Execute()
            .Should().Pass();

        new DotnetNewCommand(_testOutput, "web", "-n", "WebApp")
            .WithVirtualHive()
            .WithWorkingDirectory(newSolutionDir.FullName)
            .Execute()
            .Should().Pass();

        new DotnetCommand(_testOutput, "sln", "add", Path.Combine("WebApp", "WebApp.csproj"))
            .WithWorkingDirectory(newSolutionDir.FullName)
            .Execute()
            .Should().Pass();

        // set TFM for the console app
        using (FileStream stream = File.Open(Path.Join(newSolutionDir.FullName, "ConsoleApp", "ConsoleApp.csproj"), FileMode.Open, FileAccess.ReadWrite))
        {
            XDocument document = await XDocument.LoadAsync(stream, LoadOptions.None, CancellationToken.None);
            document
                .Descendants()
                .First(e => e.Name.LocalName == "TargetFramework")
                .Value = _oldFramework;

            stream.SetLength(0);
            await document.SaveAsync(stream, SaveOptions.None, CancellationToken.None);
        }

        // Set TFM for WebApp
        using (FileStream stream = File.Open(Path.Join(newSolutionDir.FullName, "WebApp", "WebApp.csproj"), FileMode.Open, FileAccess.ReadWrite))
        {
            XDocument document = await XDocument.LoadAsync(stream, LoadOptions.None, CancellationToken.None);
            document
                .Descendants()
                .First(e => e.Name.LocalName == "TargetFramework")
                .Value = _oldFramework;

            stream.SetLength(0);
            await document.SaveAsync(stream, SaveOptions.None, CancellationToken.None);
        }

        // Publish
        CommandResult commandResult = new DotnetCommand(_testOutput, "publish", "/t:PublishContainer")
            .WithWorkingDirectory(newSolutionDir.FullName)
            .Execute();

        commandResult.Should().Pass();
        commandResult.Should().HaveStdOutContaining("Pushed image 'webapp:latest'");
        commandResult.Should().HaveStdOutContaining("Pushed image 'consoleapp:latest'");
    }

    /// <summary>
    /// Tests that a multi-project solution with a library that targets multiple frameworks can be published.
    /// This is interesting because before https://github.com/dotnet/sdk/pull/47693 the container targets
    /// wouldn't be loaded for multi-TFM project evaluations, so any calls to the PublishContainer target
    /// for libraries (which may be multi-targeted even when referenced from a single-target published app project) would fail.
    /// It's safe to load the target for libraries in a multi-targeted context because libraries don't have EnableSdkContainerSupport
    /// enabled by default, so the target will be skipped.
    /// </summary>
    [DockerAvailableFact(Skip = "https://github.com/dotnet/sdk/issues/49502")]
    public async Task EndToEnd_MultiProjectSolution_with_multitargeted_library()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(EndToEnd_MultiProjectSolution_with_multitargeted_library));
        DirectoryInfo newSolutionDir = new(Path.Combine(TestSettings.TestArtifactsDirectory, nameof(EndToEnd_MultiProjectSolution_with_multitargeted_library)));

        if (newSolutionDir.Exists)
        {
            newSolutionDir.Delete(recursive: true);
        }

        newSolutionDir.Create();

        // Create solution with projects
        new DotnetNewCommand(_testOutput, "sln", "-n", nameof(EndToEnd_MultiProjectSolution_with_multitargeted_library))
            .WithVirtualHive()
            .WithWorkingDirectory(newSolutionDir.FullName)
            .Execute()
            .Should().Pass();

        new DotnetNewCommand(_testOutput, "web", "-n", "WebApp")
            .WithVirtualHive()
            .WithWorkingDirectory(newSolutionDir.FullName)
            .Execute()
            .Should().Pass();

        new DotnetCommand(_testOutput, "sln", "add", Path.Combine("WebApp", "WebApp.csproj"))
            .WithWorkingDirectory(newSolutionDir.FullName)
            .Execute()
            .Should().Pass();

        new DotnetNewCommand(_testOutput, "classlib", "-n", "Library")
            .WithVirtualHive()
            .WithWorkingDirectory(newSolutionDir.FullName)
            .Execute()
            .Should().Pass();

        new DotnetCommand(_testOutput, "sln", "add", Path.Combine("Library", "Library.csproj"))
            .WithWorkingDirectory(newSolutionDir.FullName)
            .Execute()
            .Should().Pass();

        // Set TFMs for Library - use current toolset + NS2.0 for compatibility
        // also set IsPublishable to false
        using (FileStream stream = File.Open(Path.Join(newSolutionDir.FullName, "Library", "Library.csproj"), FileMode.Open, FileAccess.ReadWrite))
        {
            XDocument document = await XDocument.LoadAsync(stream, LoadOptions.None, CancellationToken.None);
            var tfmNode =
                document
                .Descendants()
                .First(e => e.Name.LocalName == "TargetFramework");
            var propertyGroupNode = tfmNode.Parent!;
            tfmNode.Remove();
            propertyGroupNode.Add(new XElement("TargetFrameworks", $"{ToolsetInfo.CurrentTargetFramework};netstandard2.0"));
            propertyGroupNode.Add(new XElement("IsPublishable", "false"));
            stream.SetLength(0);
            await document.SaveAsync(stream, SaveOptions.None, CancellationToken.None);
        }

        // Publish
        CommandResult commandResult = new DotnetCommand(_testOutput, "publish", "/t:PublishContainer")
            .WithWorkingDirectory(newSolutionDir.FullName)
            .Execute();

        commandResult.Should().Pass();
        commandResult.Should().HaveStdOutContaining("Pushed image 'webapp:latest'");
    }

    [DockerAvailableTheory(Skip = "https://github.com/dotnet/sdk/issues/49502")]
    [InlineData("webapi", false)]
    [InlineData("webapi", true)]
    [InlineData("worker", false)]
    [InlineData("worker", true)]
    [InlineData("console", true)]
    [InlineData("console", false)]
    public async Task EndToEnd_NoAPI_ProjectType(string projectType, bool addPackageReference)
    {
        DirectoryInfo newProjectDir = new(Path.Combine(TestSettings.TestArtifactsDirectory, $"CreateNewImageTest_{projectType}_{addPackageReference}"));
        DirectoryInfo privateNuGetAssets = new(Path.Combine(TestSettings.TestArtifactsDirectory, "ContainerNuGet"));

        if (newProjectDir.Exists)
        {
            newProjectDir.Delete(recursive: true);
        }

        if (privateNuGetAssets.Exists)
        {
            privateNuGetAssets.Delete(recursive: true);
        }

        newProjectDir.Create();
        privateNuGetAssets.Create();
        new DotnetNewCommand(_testOutput, projectType, "-f", ToolsetInfo.CurrentTargetFramework)
            .WithVirtualHive()
            .WithWorkingDirectory(newProjectDir.FullName)
            // do not pollute the primary/global NuGet package store with the private package(s)
            .WithEnvironmentVariable("NUGET_PACKAGES", privateNuGetAssets.FullName)
            .Execute()
            .Should().Pass();

        if (addPackageReference)
        {
            File.Copy(Path.Combine(TestContext.Current.TestExecutionDirectory, "NuGet.config"), Path.Combine(newProjectDir.FullName, "NuGet.config"));

            (string? packagePath, string? packageVersion) = ToolsetUtils.GetContainersPackagePath();

            new DotnetCommand(_testOutput, "nuget", "add", "source", Path.GetDirectoryName(packagePath) ?? string.Empty, "--name", "local-temp")
                .WithEnvironmentVariable("NUGET_PACKAGES", privateNuGetAssets.FullName)
                .WithWorkingDirectory(newProjectDir.FullName)
                .Execute()
                .Should().Pass();

            // Add package to the project
            new DotnetCommand(_testOutput, "add", "package", "Microsoft.NET.Build.Containers", "-f", ToolsetInfo.CurrentTargetFramework, "-v", packageVersion ?? string.Empty)
                .WithEnvironmentVariable("NUGET_PACKAGES", privateNuGetAssets.FullName)
                .WithWorkingDirectory(newProjectDir.FullName)
                .Execute()
                .Should().Pass();
        }
        else
        {
            string projectPath = Path.Combine(newProjectDir.FullName, newProjectDir.Name + ".csproj");

            var project = XDocument.Load(projectPath);
            var ns = project.Root?.Name.Namespace ?? throw new InvalidOperationException("Project file is empty");

            project.Save(projectPath);
        }

        string imageName = NewImageName();
        string imageTag = $"1.0-{projectType}-{addPackageReference}";

        // Build & publish the project
        CommandResult commandResult = new DotnetCommand(
            _testOutput,
            "publish",
            "/t:PublishContainer",
            "/p:RuntimeIdentifier=linux-x64",
            $"/p:ContainerBaseImage={DockerRegistryManager.FullyQualifiedBaseImageAspNet}",
            $"/p:ContainerRegistry={DockerRegistryManager.LocalRegistry}",
            $"/p:ContainerRepository={imageName}",
            $"/p:ContainerImageTag={imageTag}",
            "/p:UseRazorSourceGenerator=false")
            .WithEnvironmentVariable("NUGET_PACKAGES", privateNuGetAssets.FullName)
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute();

        commandResult.Should().Pass();

        if (addPackageReference)
        {
            commandResult.Should().HaveStdOutContaining("warning CONTAINER005: The Microsoft.NET.Build.Containers NuGet package is explicitly referenced but the current SDK can natively publish the project as a container. Consider removing the package reference to Microsoft.NET.Build.Containers because it is no longer needed.");
        }
        else
        {
            commandResult.Should().NotHaveStdOutContaining("warning");
        }

        ContainerCli.PullCommand(_testOutput, $"{DockerRegistryManager.LocalRegistry}/{imageName}:{imageTag}")
            .Execute()
            .Should().Pass();

        var containerName = $"test-container-1-{projectType}-{addPackageReference}";
        CommandResult processResult = ContainerCli.RunCommand(
            _testOutput,
            [
                "--rm",
                "--name",
                containerName,
                "-P",
                ..projectType != "console" ? ["--detach"] : new string[]{},
                $"{DockerRegistryManager.LocalRegistry}/{imageName}:{imageTag}"
            ])
        .Execute();
        processResult.Should().Pass();
        Assert.NotNull(processResult.StdOut);
        string appContainerId = processResult.StdOut.Trim();
        bool everSucceeded = false;
        if (projectType == "webapi")
        {
            var portCommand =
            ContainerCli.PortCommand(_testOutput, containerName, 8080)
                .Execute();
            portCommand.Should().Pass();
            var port = portCommand.StdOut?.Trim().Split("\n")[0]; // only take the first port, which should be 0.0.0.0:PORT. the second line will be an ip6 port, if any.
            _testOutput.WriteLine($"Discovered port was '{port}'");
            var tempUri = new Uri($"http://{port}", UriKind.Absolute);
            var appUri = new UriBuilder(tempUri)
            {
                Host = "localhost"
            }.Uri;
            HttpClient client = new();
            client.BaseAddress = appUri;
            // Give the server a moment to catch up, but no more than necessary.
            for (int retry = 0; retry < 10; retry++)
            {
                try
                {
                    var response = await client.GetAsync($"weatherforecast").ConfigureAwait(false);

                    if (response.IsSuccessStatusCode)
                    {
                        everSucceeded = true;
                        break;
                    }
                }
                catch { }

                await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
            }
            ContainerCli.LogsCommand(_testOutput, appContainerId)
            .Execute()
            .Should().Pass();
            Assert.True(everSucceeded, $"{appUri}weatherforecast never responded.");

            ContainerCli.StopCommand(_testOutput, appContainerId)
            .Execute()
            .Should().Pass();
        }
        else if (projectType == "worker")
        {
            // the worker template needs a second to start up and emit the logs we are looking for
            await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            ContainerCli.LogsCommand(_testOutput, appContainerId)
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining("Worker running at");

            ContainerCli.StopCommand(_testOutput, appContainerId)
            .Execute()
            .Should().Pass();

        }
        else if (projectType == "console")
        {
            processResult.Should().Pass().And.HaveStdOutContaining("Hello, World!");
        }

        newProjectDir.Delete(true);
        privateNuGetAssets.Delete(true);
    }

    [DockerAvailableTheory(Skip = "https://github.com/dotnet/sdk/issues/49502")]
    [InlineData(DockerRegistryManager.FullyQualifiedBaseImageAspNet)]
    [InlineData(DockerRegistryManager.FullyQualifiedBaseImageAspNetDigest)]
    public void EndToEnd_NoAPI_Console(string baseImage)
    {
        DirectoryInfo newProjectDir = new(Path.Combine(TestSettings.TestArtifactsDirectory, "CreateNewImageTest"));
        DirectoryInfo privateNuGetAssets = new(Path.Combine(TestSettings.TestArtifactsDirectory, "ContainerNuGet"));

        if (newProjectDir.Exists)
        {
            newProjectDir.Delete(recursive: true);
        }

        if (privateNuGetAssets.Exists)
        {
            privateNuGetAssets.Delete(recursive: true);
        }

        newProjectDir.Create();
        privateNuGetAssets.Create();

        new DotnetNewCommand(_testOutput, "console", "-f", ToolsetInfo.CurrentTargetFramework)
            .WithVirtualHive()
            .WithWorkingDirectory(newProjectDir.FullName)
            // do not pollute the primary/global NuGet package store with the private package(s)
            .WithEnvironmentVariable("NUGET_PACKAGES", privateNuGetAssets.FullName)
            .Execute()
            .Should().Pass();
        ChangeTargetFrameworkAfterAppCreation(newProjectDir.FullName);

        File.Copy(Path.Combine(TestContext.Current.TestExecutionDirectory, "NuGet.config"), Path.Combine(newProjectDir.FullName, "NuGet.config"));

        (string? packagePath, string? packageVersion) = ToolsetUtils.GetContainersPackagePath();

        new DotnetCommand(_testOutput, "nuget", "add", "source", Path.GetDirectoryName(packagePath) ?? string.Empty, "--name", "local-temp")
            .WithEnvironmentVariable("NUGET_PACKAGES", privateNuGetAssets.FullName)
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        // Add package to the project
        new DotnetCommand(_testOutput, "add", "package", "Microsoft.NET.Build.Containers", "-f", _oldFramework, "-v", packageVersion ?? string.Empty)
            .WithEnvironmentVariable("NUGET_PACKAGES", privateNuGetAssets.FullName)
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        string imageName = NewImageName();
        string imageTag = "1.0";

        // Build & publish the project
        new DotnetCommand(
            _testOutput,
            "publish",
            "/t:PublishContainer",
            "/p:runtimeidentifier=linux-x64",
            $"/p:ContainerBaseImage={baseImage}",
            $"/p:ContainerRegistry={DockerRegistryManager.LocalRegistry}",
            $"/p:ContainerRepository={imageName}",
            $"/p:ContainerImageTag={imageTag}")
            .WithEnvironmentVariable("NUGET_PACKAGES", privateNuGetAssets.FullName)
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        ContainerCli.PullCommand(_testOutput, $"{DockerRegistryManager.LocalRegistry}/{imageName}:{imageTag}")
            .Execute()
            .Should().Pass();

        var containerName = "test-container-2";
        CommandResult processResult = ContainerCli.RunCommand(
            _testOutput,
            "--rm",
            "--name",
            containerName,
            $"{DockerRegistryManager.LocalRegistry}/{imageName}:{imageTag}")
        .Execute();
        processResult.Should().Pass().And.HaveStdOut("Hello, World!");

        newProjectDir.Delete(true);
        privateNuGetAssets.Delete(true);
    }

    [DockerAvailableFact(Skip = "https://github.com/dotnet/sdk/issues/49502")]
    public void EndToEnd_SingleArch_NoRid()
    {
        // Create a new console project
        DirectoryInfo newProjectDir = CreateNewProject("console", _oldFramework);

        string imageName = NewImageName();
        string imageTag = "1.0";

        // Run PublishContainer for multi-arch
        CommandResult commandResult = new DotnetCommand(
            _testOutput,
            "publish",
            "/t:PublishContainer",
            $"/p:ContainerBaseImage={DockerRegistryManager.FullyQualifiedBaseImageAspNet}",
            $"/p:ContainerRepository={imageName}",
            $"/p:ContainerImageTag={imageTag}")
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute();
        commandResult.Should().Pass();

        // Check that the containers can be run
        CommandResult processResultX64 = ContainerCli.RunCommand(
            _testOutput,
            "--rm",
            "--name",
            $"test-container-singlearch-norid",
            $"{imageName}:{imageTag}")
        .Execute();
        processResultX64.Should().Pass().And.HaveStdOut("Hello, World!");
    }

    /**
    [InlineData("endtoendmultiarch-localregisty")]
    [InlineData("myteam/endtoendmultiarch-localregisty")]
    [DockerIsAvailableAndSupportsArchTheory(Skip = "https://github.com/dotnet/sdk/issues/49502", "linux/arm64", checkContainerdStoreAvailability: true)]
    public void EndToEndMultiArch_LocalRegistry(string imageName)
    {
        string tag = "1.0";
        string image = $"{imageName}:{tag}";

        // Create a new console project
        DirectoryInfo newProjectDir = CreateNewProject("console", _oldFramework);

        // Run PublishContainer for multi-arch
        CommandResult commandResult = new DotnetCommand(
            _testOutput,
            "build",
            "/t:PublishContainer",
            "/p:RuntimeIdentifiers=\"linux-x64;linux-arm64\"",
            $"/p:ContainerBaseImage={DockerRegistryManager.FullyQualifiedBaseImageAspNet}",
            $"/p:ContainerRepository={imageName}",
            $"/p:ContainerImageTag={tag}")
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute();

        // Check that the app was published for each RID, one image was created locally
        commandResult.Should().Pass()
            .And.HaveStdOutContaining(GetPublishArtifactsPath(newProjectDir.FullName, _oldFramework, "linux-x64"))
            .And.HaveStdOutContaining(GetPublishArtifactsPath(newProjectDir.FullName, _oldFramework, "linux-arm64"))
            .And.HaveStdOutContaining($"Building image '{imageName}' for runtime identifier 'linux-x64'")
            .And.HaveStdOutContaining($"Building image '{imageName}' for runtime identifier 'linux-arm64'")
            .And.HaveStdOutContaining($"Pushed image '{image}' to local registry");

        // Check that the containers can be run
        CommandResult processResultX64 = ContainerCli.RunCommand(
            _testOutput,
            "--rm",
            "--platform",
            "linux/amd64",
            "--name",
            $"test-container-{imageName.Replace('/', '-')}-x64",
            image)
        .Execute();
        processResultX64.Should().Pass().And.HaveStdOut("Hello, World!");

        CommandResult processResultArm64 = ContainerCli.RunCommand(
            _testOutput,
            "--rm",
            "--platform",
            "linux/arm64",
            "--name",
            $"test-container-{imageName.Replace('/', '-')}-arm64",
            image)
        .Execute();
        processResultArm64.Should().Pass().And.HaveStdOut("Hello, World!");

        // Cleanup
        newProjectDir.Delete(true);
    }
    */

    [DockerAvailableFact(Skip = "https://github.com/dotnet/sdk/issues/49502")]
    public void MultiArchStillAllowsSingleRID()
    {
        string imageName = NewImageName();
        string imageTag = "1.0";
        string qualifiedImageName = $"{imageName}:{imageTag}";

        // Create a new console project
        DirectoryInfo newProjectDir = CreateNewProject("console", _oldFramework);

        // Run PublishContainer for multi-arch-capable, but single-arch actual
        CommandResult commandResult = new DotnetCommand(
            _testOutput,
            "publish",
            "/t:PublishContainer",
            // make it so the app is _able_ to target both linux TFMs
            "/p:RuntimeIdentifiers=\"linux-x64;linux-arm64\"",
            // and that it opts into to multi-targeting containers for both of those linux TFMs
            "/p:ContainerRuntimeIdentifiers=\"linux-x64;linux-arm64\"",
            // but then only actually publishes for one of them
            "/p:ContainerRuntimeIdentifier=linux-x64",
            $"/p:ContainerBaseImage={DockerRegistryManager.FullyQualifiedBaseImageAspNet}",
            $"/p:ContainerRepository={imageName}",
            $"/p:ContainerImageTag={imageTag}",
            "/bl")
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute();

        // Check that the app was published for each RID,
        // images were created locally for each RID
        // and image index was NOT created
        commandResult.Should().Pass()
            // no rid-specific path because we didn't pass RuntimeIdentifier
            .And.NotHaveStdOutContaining(GetPublishArtifactsPath(newProjectDir.FullName, _oldFramework, "linux-x64"))
            .And.HaveStdOutContaining($"Pushed image '{qualifiedImageName}' to local registry")
            .And.NotHaveStdOutContaining("Pushed image index");

        // Check that the containers can be run
        CommandResult processResultX64 = ContainerCli.RunCommand(
            _testOutput,
            "--rm",
            "--name",
            $"test-container-{imageName}",
            qualifiedImageName)
        .Execute();
        processResultX64.Should().Pass().And.HaveStdOut("Hello, World!");

        // Cleanup
        newProjectDir.Delete(true);
    }

    [DockerAvailableFact(Skip = "https://github.com/dotnet/sdk/issues/49502")]
    public void MultiArchStillAllowsSingleRIDUsingJustRIDProperties()
    {
        string imageName = NewImageName();
        string imageTag = "1.0";
        string qualifiedImageName = $"{imageName}:{imageTag}";

        // Create a new console project
        DirectoryInfo newProjectDir = CreateNewProject("console", _oldFramework);

        // Run PublishContainer for multi-arch-capable, but single-arch actual
        CommandResult commandResult = new DotnetCommand(
            _testOutput,
            "publish",
            "/t:PublishContainer",
            // make it so the app is _able_ to target both linux TFMs
            "/p:RuntimeIdentifiers=\"linux-x64;linux-arm64\"",
            // but then only actually publishes for one of them
            "-r", "linux-x64",
            $"/p:ContainerBaseImage={DockerRegistryManager.FullyQualifiedBaseImageAspNet}",
            $"/p:ContainerRepository={imageName}",
            $"/p:ContainerImageTag={imageTag}",
            "/bl")
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute();

        // Check that the app was published for each RID,
        // images were created locally for each RID
        // and image index was NOT created
        commandResult.Should().Pass()
            .And.HaveStdOutContaining(GetPublishArtifactsPath(newProjectDir.FullName, _oldFramework, "linux-x64", configuration: "Release"))
            .And.NotHaveStdOutContaining(GetPublishArtifactsPath(newProjectDir.FullName, _oldFramework, "linux-arm64", configuration: "Release"))
            .And.HaveStdOutContaining($"Pushed image '{qualifiedImageName}' to local registry")
            .And.NotHaveStdOutContaining("Pushed image index");

        // Check that the containers can be run
        CommandResult processResultX64 = ContainerCli.RunCommand(
            _testOutput,
            "--rm",
            "--name",
            $"test-container-{imageName}-x64",
            qualifiedImageName)
        .Execute();
        processResultX64.Should().Pass().And.HaveStdOut("Hello, World!");

        // Cleanup
        newProjectDir.Delete(true);
    }

    private DirectoryInfo CreateNewProject(string template, string tfm = ToolsetInfo.CurrentTargetFramework, [CallerMemberName] string callerMemberName = "")
    {
        DirectoryInfo newProjectDir = new DirectoryInfo(Path.Combine(TestSettings.TestArtifactsDirectory, callerMemberName));

        if (newProjectDir.Exists)
        {
            newProjectDir.Delete(recursive: true);
        }

        newProjectDir.Create();

        new DotnetNewCommand(_testOutput, template, "-f", tfm)
            .WithVirtualHive()
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        return newProjectDir;
    }

    private string GetPublishArtifactsPath(string projectDir, string tfm, string rid, string configuration = "Debug")
        => Path.Combine(projectDir, "bin", configuration, tfm, rid, "publish");

    /**
    [InlineData("endtoendmultiarch-archivepublishing")]
    [InlineData("myteam/endtoendmultiarch-archivepublishing")]
    [DockerIsAvailableAndSupportsArchTheory(Skip = "https://github.com/dotnet/sdk/issues/49502", "linux/arm64", checkContainerdStoreAvailability: true)]
    public void EndToEndMultiArch_ArchivePublishing(string imageName)
    {
        string tag = "1.0";
        string image = $"{imageName}:{tag}";
        string archiveOutput = TestSettings.TestArtifactsDirectory;
        string imageTarball = Path.Combine(archiveOutput, $"{imageName}.tar.gz");

        // Create a new console project
        DirectoryInfo newProjectDir = CreateNewProject("console", _oldFramework);

        // Run PublishContainer for multi-arch with ContainerArchiveOutputPath
        CommandResult commandResult = new DotnetCommand(
            _testOutput,
            "build",
            "/t:PublishContainer",
            "/p:RuntimeIdentifiers=\"linux-x64;linux-arm64\"",
            $"/p:ContainerArchiveOutputPath={archiveOutput}",
            $"/p:ContainerBaseImage={DockerRegistryManager.FullyQualifiedBaseImageAspNet}",
            $"/p:ContainerRepository={imageName}",
            $"/p:ContainerImageTag={tag}")
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute();

        // Check that the app was published for each RID, one image was created in local archive
        commandResult.Should().Pass()
            .And.HaveStdOutContaining(GetPublishArtifactsPath(newProjectDir.FullName, _oldFramework, "linux-x64"))
            .And.HaveStdOutContaining(GetPublishArtifactsPath(newProjectDir.FullName, _oldFramework, "linux-arm64"))
            .And.HaveStdOutContaining($"Building image '{imageName}' for runtime identifier 'linux-x64'")
            .And.HaveStdOutContaining($"Building image '{imageName}' for runtime identifier 'linux-arm64'")
            .And.HaveStdOutContaining($"Pushed image '{image}' to local archive at '{imageTarball}'");

        // Check that tarball were created
        File.Exists(imageTarball).Should().BeTrue();

        // Load the multi-arch image from the tarball
        ContainerCli.LoadCommand(_testOutput, "--input", imageTarball)
           .Execute()
           .Should().Pass();

        // Check that the containers can be run
        CommandResult processResultX64 = ContainerCli.RunCommand(
            _testOutput,
            "--rm",
            "--platform",
            "linux/amd64",
            "--name",
            $"test-container-{imageName.Replace('/', '-')}-x64",
            image)
        .Execute();
        processResultX64.Should().Pass().And.HaveStdOut("Hello, World!");

        CommandResult processResultArm64 = ContainerCli.RunCommand(
            _testOutput,
            "--rm",
            "--platform",
            "linux/arm64",
            "--name",
            $"test-container-{imageName.Replace('/', '-')}-arm64",
            image)
        .Execute();
        processResultArm64.Should().Pass().And.HaveStdOut("Hello, World!");

        // Cleanup
        newProjectDir.Delete(true);
    }
    */

    [DockerIsAvailableAndSupportsArchFact("linux/arm64", checkContainerdStoreAvailability: true)]
    public void EndToEndMultiArch_RemoteRegistry()
    {
        string imageName = NewImageName();
        string imageTag = "1.0";
        string registry = DockerRegistryManager.LocalRegistry;
        string imageX64 = $"{imageName}:{imageTag}-linux-x64";
        string imageArm64 = $"{imageName}:{imageTag}-linux-arm64";
        string imageIndex = $"{imageName}:{imageTag}";
        string imageFromRegistry = $"{registry}/{imageIndex}";

        // Create a new console project
        DirectoryInfo newProjectDir = CreateNewProject("console", _oldFramework);

        // Run PublishContainer for multi-arch with ContainerRegistry
        CommandResult commandResult = new DotnetCommand(
            _testOutput,
            "build",
            "/t:PublishContainer",
            "/p:RuntimeIdentifiers=\"linux-x64;linux-arm64\"",
            $"/p:ContainerBaseImage={DockerRegistryManager.FullyQualifiedBaseImageAspNet}",
            $"/p:ContainerRegistry={registry}",
            $"/p:ContainerRepository={imageName}",
            $"/p:ContainerImageTag={imageTag}")
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute();

        // Check that the app was published for each RID,
        // images for each RID were pushed to remote registry
        // and image index was pushed to remote registry
        commandResult.Should().Pass()
            .And.HaveStdOutContaining(GetPublishArtifactsPath(newProjectDir.FullName, _oldFramework, "linux-x64"))
            .And.HaveStdOutContaining(GetPublishArtifactsPath(newProjectDir.FullName, _oldFramework, "linux-arm64"))
            .And.HaveStdOutContaining($"Pushed image '{imageX64}' to registry '{registry}'.")
            .And.HaveStdOutContaining($"Pushed image '{imageArm64}' to registry '{registry}'.")
            .And.HaveStdOutContaining($"Pushed image index '{imageIndex}' to registry '{registry}'.");

        // Check that the containers can be run
        // First pull the image from the registry for each platform
        ContainerCli.PullCommand(
            _testOutput,
            "--platform",
            "linux/amd64",
            imageFromRegistry)
            .Execute()
            .Should().Pass();
        ContainerCli.PullCommand(
            _testOutput,
            "--platform",
            "linux/arm64",
            imageFromRegistry)
            .Execute()
            .Should().Pass();

        // Run the containers
        ContainerCli.RunCommand(
            _testOutput,
            "--rm",
            "--platform",
            "linux/amd64",
            "--name",
            $"test-container-{imageName}-x64",
            imageFromRegistry)
        .Execute().Should().Pass().And.HaveStdOut("Hello, World!");
        ContainerCli.RunCommand(
            _testOutput,
            "--rm",
            "--platform",
            "linux/arm64",
            "--name",
            $"test-container-{imageName}-arm64",
            imageFromRegistry)
        .Execute().Should().Pass().And.HaveStdOut("Hello, World!");

        // Cleanup
        newProjectDir.Delete(true);
    }

    [DockerAvailableFact(Skip = "https://github.com/dotnet/sdk/issues/45181")]
    public void EndToEndMultiArch_ContainerRuntimeIdentifiersOverridesRuntimeIdentifiers()
    {
        // Create a new console project
        DirectoryInfo newProjectDir = CreateNewProject("console", _oldFramework);
        string imageName = NewImageName();
        string imageTag = "1.0";

        // Run PublishContainer for multi-arch with ContainerRuntimeIdentifiers
        // RuntimeIdentifiers should contain all the RIDs from ContainerRuntimeIdentifiers to be able to publish
        CommandResult commandResult = new DotnetCommand(
            _testOutput,
            "build",
            "/t:PublishContainer",
            "/p:RuntimeIdentifiers=\"linux-x64;linux-arm64\"",
            "/p:ContainerRuntimeIdentifiers=linux-arm64",
            $"/p:ContainerBaseImage={DockerRegistryManager.FullyQualifiedBaseImageAspNet}",
            $"/p:ContainerRepository={imageName}",
            $"/p:ContainerImageTag={imageTag}")
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute();

        // Check that the app was published only for RID from ContainerRuntimeIdentifiers
        // images were built only for RID for from ContainerRuntimeIdentifiers
        commandResult.Should().Pass()
            .And.NotHaveStdOutContaining(GetPublishArtifactsPath(newProjectDir.FullName, _oldFramework, "linux-x64"))
            .And.HaveStdOutContaining(GetPublishArtifactsPath(newProjectDir.FullName, _oldFramework, "linux-arm64"))
            .And.NotHaveStdOutContaining($"Building image '{imageName}' for runtime identifier 'linux-x64'")
            .And.HaveStdOutContaining($"Building image '{imageName}' for runtime identifier 'linux-arm64'");

        // Cleanup
        newProjectDir.Delete(true);
    }

    [DockerIsAvailableAndSupportsArchFact("linux/arm64", checkContainerdStoreAvailability: true)]
    public void EndToEndMultiArch_EnvVariables()
    {
        string imageName = NewImageName();
        string tag = "1.0";
        string image = $"{imageName}:{tag}";

        // Create new console app, set ContainerEnvironmentVariables, and set to output env variable
        DirectoryInfo newProjectDir = CreateNewProject("console", _oldFramework);
        var csprojPath = Path.Combine(newProjectDir.FullName, $"{nameof(EndToEndMultiArch_EnvVariables)}.csproj");
        var csprojContent = File.ReadAllText(csprojPath);
        csprojContent = csprojContent.Replace("</Project>",
            """
                <ItemGroup>
                    <ContainerEnvironmentVariable Include="GoodEnvVar" Value="Foo" />
                    <ContainerEnvironmentVariable Include="AnotherEnvVar" Value="Bar" />
                </ItemGroup>
            </Project>
            """);
        File.WriteAllText(csprojPath, csprojContent);
        File.WriteAllText(Path.Combine(newProjectDir.FullName, "Program.cs"),
            """
            Console.Write(Environment.GetEnvironmentVariable("GoodEnvVar"));
            Console.Write(Environment.GetEnvironmentVariable("AnotherEnvVar"));
            """);

        // Run PublishContainer for multi-arch
        new DotnetCommand(
            _testOutput,
            "build",
            "/t:PublishContainer",
            "/p:RuntimeIdentifiers=\"linux-x64;linux-arm64\"",
            $"/p:ContainerBaseImage={DockerRegistryManager.FullyQualifiedBaseImageAspNet}",
            $"/p:ContainerRepository={imageName}",
            $"/p:ContainerImageTag={tag}")
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        // Check that the env var is printed for linux/amd64 platform
        string containerNameX64 = $"test-container-{imageName}-x64";
        CommandResult processResultX64 = ContainerCli.RunCommand(
            _testOutput,
            "--rm",
            "--platform",
            "linux/amd64",
            "--name",
            containerNameX64,
            image)
        .Execute();
        processResultX64.Should().Pass().And.HaveStdOut("FooBar");

        // Check that the env var is printed for linux/arm64 platform
        string containerNameArm64 = $"test-container-{imageName}-arm64";
        CommandResult processResultArm64 = ContainerCli.RunCommand(
            _testOutput,
            "--rm",
            "--platform",
            "linux/arm64",
            "--name",
            containerNameArm64,
            image)
        .Execute();
        processResultArm64.Should().Pass().And.HaveStdOut("FooBar");

        // Cleanup
        newProjectDir.Delete(true);
    }

    [DockerIsAvailableAndSupportsArchFact("linux/arm64", checkContainerdStoreAvailability: true)]
    public void EndToEndMultiArch_Ports()
    {
        string imageName = NewImageName();
        string tag = "1.0";
        string image = $"{imageName}:{tag}";

        // Create new web app, set ContainerPort
        DirectoryInfo newProjectDir = CreateNewProject("webapp", _oldFramework);
        var csprojPath = Path.Combine(newProjectDir.FullName, $"{nameof(EndToEndMultiArch_Ports)}.csproj");
        var csprojContent = File.ReadAllText(csprojPath);
        csprojContent = csprojContent.Replace("</Project>",
            """
                <ItemGroup>
                    <ContainerPort Include="8082" Type="tcp" />
                    <ContainerPort Include="8083" Type="tcp" />
                </ItemGroup>
            </Project>
            """);
        File.WriteAllText(csprojPath, csprojContent);

        // Run PublishContainer for multi-arch
        new DotnetCommand(
            _testOutput,
            "build",
            "/t:PublishContainer",
            "/p:RuntimeIdentifiers=\"linux-x64;linux-arm64\"",
            $"/p:ContainerBaseImage={DockerRegistryManager.FullyQualifiedBaseImageAspNet}",
            $"/p:ContainerRepository={imageName}",
            $"/p:ContainerImageTag={tag}")
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        // Check that the ports are correct for linux/amd64 platform
        var containerNameX64 = $"test-container-{imageName}-x64";
        CommandResult processResultX64 = ContainerCli.RunCommand(
            _testOutput,
            "--rm",
            "--platform",
            "linux/amd64",
            "--name",
            containerNameX64,
            "-P",
            "--detach",
            image)
        .Execute();
        processResultX64.Should().Pass();

        // 8080 is the default port
        CheckPorts(containerNameX64, [8080, 8082, 8083], [8081]);

        // Check that the ports are correct for linux/arm64 platform
        var containerNameArm64 = $"test-container-{imageName}-arm64";
        CommandResult processResultArm64 = ContainerCli.RunCommand(
            _testOutput,
            "--rm",
            "--platform",
            "linux/arm64",
            "--name",
            containerNameArm64,
            "-P",
            "--detach",
            image)
        .Execute();
        processResultArm64.Should().Pass();

        // 8080 is the default port
        CheckPorts(containerNameArm64, [8080, 8082, 8083], [8081]);

        // Cleanup
        // we ran containers with detached option, so we need to stop them
        ContainerCli.StopCommand(_testOutput, containerNameX64)
           .Execute()
           .Should().Pass();
        ContainerCli.StopCommand(_testOutput, containerNameArm64)
           .Execute()
           .Should().Pass();
        newProjectDir.Delete(true);
    }

    private void CheckPorts(string containerName, int[] correctPorts, int[] incorrectPorts)
    {
        foreach (var port in correctPorts)
        {
            // Check the provided port is available
            ContainerCli.PortCommand(_testOutput, containerName, port)
                .Execute().Should().Pass();
        }
        foreach (var port in incorrectPorts)
        {
            // Check that not provided port is not available
            ContainerCli.PortCommand(_testOutput, containerName, port)
                .Execute().Should().Fail();
        }
    }

    [DockerAvailableFact(checkContainerdStoreAvailability: true)]
    public void EndToEndMultiArch_Labels()
    {
        string imageName = NewImageName();
        string tag = "1.0";
        string image = $"{imageName}:{tag}";

        // Create new console app
        DirectoryInfo newProjectDir = CreateNewProject("webapp");

        // Run PublishContainer for multi-arch with ContainerGenerateLabels
        new DotnetCommand(
            _testOutput,
            "publish",
            "/t:PublishContainer",
            "/p:RuntimeIdentifiers=\"linux-x64;linux-arm64\"",
            $"/p:ContainerBaseImage={DockerRegistryManager.FullyQualifiedBaseImageAspNet}",
            $"/p:ContainerRepository={imageName}",
            $"/p:ContainerImageTag={tag}")
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        // Check that labels are set
        CommandResult inspectResult = ContainerCli.InspectCommand(
            _testOutput,
            "--format={{json .Config.Labels}}",
            image)
        .Execute();
        inspectResult.Should().Pass();
        var labels = JsonSerializer.Deserialize<Dictionary<string, string>>(inspectResult.StdOut ?? string.Empty);
        labels.Should().NotBeNull().And.HaveCountGreaterThan(0);
        labels!.Values.Should().AllSatisfy(value => value.Should().NotBeNullOrEmpty());

        // Cleanup
        newProjectDir.Delete(true);
    }

    [DockerSupportsArchInlineData("linux/arm/v7", "linux-arm", "/app")]
    [DockerSupportsArchInlineData("linux/arm64/v8", "linux-arm64", "/app")]
    [DockerSupportsArchInlineData("linux/386", "linux-x86", "/app", Skip = "There's no apphost for linux-x86 so we can't execute self-contained, and there's no .NET runtime base image for linux-x86 so we can't execute framework-dependent.")]
    [DockerSupportsArchInlineData("windows/amd64", "win-x64", "C:\\app")]
    [DockerSupportsArchInlineData("linux/amd64", "linux-x64", "/app")]
    [DockerAvailableTheory(Skip = "https://github.com/dotnet/sdk/issues/49502")]
    public async Task CanPackageForAllSupportedContainerRIDs(string dockerPlatform, string rid, string workingDir)
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(CanPackageForAllSupportedContainerRIDs));
        string publishDirectory = BuildLocalApp(tfm: ToolsetInfo.CurrentTargetFramework, rid: rid);

        // Build the image
        Registry registry = new(DockerRegistryManager.BaseImageSource, logger, RegistryMode.Push);
        var isWin = rid.StartsWith("win");
        ImageBuilder? imageBuilder = await registry.GetImageManifestAsync(
            DockerRegistryManager.RuntimeBaseImage,
            isWin ? DockerRegistryManager.Net8PreviewWindowsSpecificImageTag : DockerRegistryManager.Net9ImageTag,
            rid,
            ToolsetUtils.RidGraphManifestPicker,
            cancellationToken: default).ConfigureAwait(false);
        Assert.NotNull(imageBuilder);

        Layer l = Layer.FromDirectory(publishDirectory, isWin ? "C:\\app" : "/app", isWin, imageBuilder.ManifestMediaType);

        imageBuilder.AddLayer(l);
        imageBuilder.SetWorkingDirectory(workingDir);

        string[] entryPoint = DecideEntrypoint(rid, "MinimalTestApp", workingDir);
        imageBuilder.SetEntrypointAndCmd(entryPoint, Array.Empty<string>());

        BuiltImage builtImage = imageBuilder.Build();

        // Load the image into the local registry
        var sourceReference = new SourceImageReference(registry, DockerRegistryManager.RuntimeBaseImage, DockerRegistryManager.Net9ImageTag, null);
        var destinationReference = new DestinationImageReference(registry, NewImageName(), new[] { rid });
        await new DockerCli(_loggerFactory).LoadAsync(builtImage, sourceReference, destinationReference, default).ConfigureAwait(false);

        // Run the image
        ContainerCli.RunCommand(
            _testOutput,
            "--rm",
            "--tty",
            "--platform",
            dockerPlatform,
            $"{NewImageName()}:{rid}")
            .Execute()
            .Should()
            .Pass();

        static string[] DecideEntrypoint(string rid, string appName, string workingDir)
        {
            var binary = rid.StartsWith("win", StringComparison.Ordinal) ? $"{appName}.exe" : appName;
            return new[] { $"{workingDir}/{binary}" };
        }
    }

    [DockerAvailableFact(Skip = "https://github.com/dotnet/sdk/issues/49502")]
    public async void CheckDownloadErrorMessageWhenSourceRepositoryThrows()
    {
        var loggerFactory = new TestLoggerFactory(_testOutput);
        var logger = loggerFactory.CreateLogger(nameof(CheckDownloadErrorMessageWhenSourceRepositoryThrows));
        string rid = "win-x64";
        string publishDirectory = BuildLocalApp(tfm: ToolsetInfo.CurrentTargetFramework, rid: rid);

        // Build the image
        Registry registry = new(DockerRegistryManager.BaseImageSource, logger, RegistryMode.Push);
        ImageBuilder? imageBuilder = await registry.GetImageManifestAsync(
            DockerRegistryManager.RuntimeBaseImage,
            DockerRegistryManager.Net8PreviewWindowsSpecificImageTag,
            rid,
            ToolsetUtils.RidGraphManifestPicker,
            cancellationToken: default).ConfigureAwait(false);
        Assert.NotNull(imageBuilder);

        Layer l = Layer.FromDirectory(publishDirectory, "C:\\app", true, imageBuilder.ManifestMediaType);

        imageBuilder.AddLayer(l);
        imageBuilder.SetWorkingDirectory("C:\\app");

        string[] entryPoint = DecideEntrypoint(rid, "MinimalTestApp", "C:\\app");
        imageBuilder.SetEntrypointAndCmd(entryPoint, Array.Empty<string>());

        BuiltImage builtImage = imageBuilder.Build();

        // Load the image into the local registry
        var sourceReference = new SourceImageReference(registry, "some_random_image", DockerRegistryManager.Net9ImageTag, null);
        string archivePath = Path.Combine(TestSettings.TestArtifactsDirectory, nameof(CheckDownloadErrorMessageWhenSourceRepositoryThrows));
        var destinationReference = new DestinationImageReference(new ArchiveFileRegistry(archivePath), NewImageName(), new[] { rid });

        (var taskLog, var errors) = SetupTaskLog();
        var telemetry = new Telemetry(sourceReference, destinationReference, taskLog);

        await ImagePublisher.PublishImageAsync(builtImage, sourceReference, destinationReference, taskLog, telemetry, CancellationToken.None)
                .ConfigureAwait(false);

        // Assert the error message
        Assert.True(taskLog.HasLoggedErrors);
        Assert.NotNull(errors);
        Assert.Single(errors);
        Assert.Contains("Unable to download image from the repository", errors[0]);

        static string[] DecideEntrypoint(string rid, string appName, string workingDir)
        {
            var binary = rid.StartsWith("win", StringComparison.Ordinal) ? $"{appName}.exe" : appName;
            return new[] { $"{workingDir}/{binary}" };
        }

        static (Microsoft.Build.Utilities.TaskLoggingHelper, List<string?> errors) SetupTaskLog()
        {
            // We can use any Task, we just need TaskLoggingHelper
            Tasks.CreateNewImage cni = new();
            List<string?> errors = new();
            IBuildEngine buildEngine = A.Fake<IBuildEngine>();
            A.CallTo(() => buildEngine.LogWarningEvent(A<BuildWarningEventArgs>.Ignored)).Invokes((BuildWarningEventArgs e) => errors.Add(e.Message));
            A.CallTo(() => buildEngine.LogErrorEvent(A<BuildErrorEventArgs>.Ignored)).Invokes((BuildErrorEventArgs e) => errors.Add(e.Message));
            A.CallTo(() => buildEngine.LogMessageEvent(A<BuildMessageEventArgs>.Ignored)).Invokes((BuildMessageEventArgs e) => errors.Add(e.Message));
            cni.BuildEngine = buildEngine;
            return (cni.Log, errors);
        }
    }

    [DockerAvailableFact(checkContainerdStoreAvailability: true)]
    public void EnforcesOciSchemaForMultiRIDTarballOutput()
    {
        string imageName = NewImageName();
        string tag = "1.0";

        // Create new console app
        DirectoryInfo newProjectDir = CreateNewProject("webapp");

        // Run PublishContainer for multi-arch with ContainerGenerateLabels
        var publishResult = new DotnetCommand(
            _testOutput,
            "publish",
            "/t:PublishContainer",
            "/p:RuntimeIdentifiers=\"linux-x64;linux-arm64\"",
            $"/p:ContainerBaseImage={DockerRegistryManager.FullyQualifiedBaseImageAspNet}",
            $"/p:ContainerRepository={imageName}",
            $"/p:ContainerImageTag={tag}",
            "/p:EnableSdkContainerSupport=true",
            "/p:ContainerArchiveOutputPath=archive.tar.gz",
            "-getProperty:GeneratedImageIndex",
            "-getItem:GeneratedContainers",
            "/bl")
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute();

        publishResult.Should().Pass();
        publishResult.StdOut.Should().NotBeNull();
        var jsonDump = JsonDocument.Parse(publishResult.StdOut);
        var index = JsonDocument.Parse(jsonDump.RootElement.GetProperty("Properties").GetProperty("GeneratedImageIndex").ToString());
        var containers = jsonDump.RootElement.GetProperty("Items").GetProperty("GeneratedContainers").EnumerateArray().ToArray();

        index.RootElement.GetProperty("mediaType").GetString().Should().Be("application/vnd.oci.image.index.v1+json");
        containers.Should().HaveCount(2);
        foreach (var container in containers)
        {
            container.GetProperty("ManifestMediaType").GetString().Should().Be("application/vnd.oci.image.manifest.v1+json");
        }
        // Cleanup
        newProjectDir.Delete(true);
    }
}
