// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Build.Containers.UnitTests;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;
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
        bool normalized = ContainerHelpers.NormalizeRepository(callerMemberName, out string? normalizedName);
        if (!normalized)
        {
            return normalizedName!;
        }

        return callerMemberName;
    }

    public void Dispose()
    {
        _loggerFactory.Dispose();
    }

    [DockerAvailableFact]
    public async Task ApiEndToEndWithRegistryPushAndPull()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(ApiEndToEndWithRegistryPushAndPull));
        string publishDirectory = BuildLocalApp();

        // Build the image

        Registry registry = new Registry(ContainerHelpers.TryExpandRegistryToUri(DockerRegistryManager.LocalRegistry), logger);

        ImageBuilder imageBuilder = await registry.GetImageManifestAsync(
            DockerRegistryManager.RuntimeBaseImage,
            DockerRegistryManager.Net8PreviewImageTag,
            "linux-x64",
            ToolsetUtils.GetRuntimeGraphFilePath(),
            cancellationToken: default).ConfigureAwait(false);

        Assert.NotNull(imageBuilder);

        Layer l = Layer.FromDirectory(publishDirectory, "/app", false);

        imageBuilder.AddLayer(l);

        imageBuilder.SetEntrypointAndCmd(new[] { "/app/MinimalTestApp" }, Array.Empty<string>());

        BuiltImage builtImage = imageBuilder.Build();

        // Push the image back to the local registry
        var sourceReference = new ImageReference(registry, DockerRegistryManager.RuntimeBaseImage, DockerRegistryManager.Net8PreviewImageTag);
        var destinationReference = new ImageReference(registry, NewImageName(), "latest");

        await registry.PushAsync(builtImage, sourceReference, destinationReference, cancellationToken: default).ConfigureAwait(false);

        // pull it back locally
        ContainerCli.PullCommand(_testOutput, $"{DockerRegistryManager.LocalRegistry}/{NewImageName()}:latest")
            .Execute()
            .Should().Pass();

        // Run the image
        ContainerCli.RunCommand(_testOutput, "--rm", "--tty", $"{DockerRegistryManager.LocalRegistry}/{NewImageName()}:latest")
            .Execute()
            .Should().Pass();
    }

    [DockerAvailableFact]
    public async Task ApiEndToEndWithLocalLoad()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(ApiEndToEndWithLocalLoad));
        string publishDirectory = BuildLocalApp(tfm: "net8.0");

        // Build the image

        Registry registry = new Registry(ContainerHelpers.TryExpandRegistryToUri(DockerRegistryManager.LocalRegistry), logger);

        ImageBuilder imageBuilder = await registry.GetImageManifestAsync(
            DockerRegistryManager.RuntimeBaseImage,
            DockerRegistryManager.Net8PreviewImageTag,
            "linux-x64",
            ToolsetUtils.GetRuntimeGraphFilePath(),
            cancellationToken: default).ConfigureAwait(false);
        Assert.NotNull(imageBuilder);

        Layer l = Layer.FromDirectory(publishDirectory, "/app", false);

        imageBuilder.AddLayer(l);

        imageBuilder.SetEntrypointAndCmd(new[] { "/app/MinimalTestApp" }, Array.Empty<string>());

        BuiltImage builtImage = imageBuilder.Build();

        // Load the image into the local registry
        var sourceReference = new ImageReference(registry, DockerRegistryManager.RuntimeBaseImage, DockerRegistryManager.Net7ImageTag);
        var destinationReference = new ImageReference(registry, NewImageName(), "latest");

        await new DockerCli(_loggerFactory).LoadAsync(builtImage, sourceReference, destinationReference, default).ConfigureAwait(false);

        // Run the image
        ContainerCli.RunCommand(_testOutput, "--rm", "--tty", $"{NewImageName()}:latest")
            .Execute()
            .Should().Pass();
    }

    private string BuildLocalApp([CallerMemberName] string testName = "TestName", string tfm = ToolsetInfo.CurrentTargetFramework, string rid = "linux-x64")
    {
        string workingDirectory = Path.Combine(TestSettings.TestArtifactsDirectory, testName);

        DirectoryInfo d = new DirectoryInfo(Path.Combine(workingDirectory, "MinimalTestApp"));
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

        var publishCommand =
            new DotnetCommand(_testOutput, "publish", "-bl", "MinimalTestApp", "-r", rid, "-f", tfm, "-c", "Debug")
                .WithWorkingDirectory(workingDirectory);

        if (tfm == ToolsetInfo.CurrentTargetFramework)
        {
            publishCommand.Arguments.AddRange(new[] { "-p", $"RuntimeFrameworkVersion=8.0.0-preview.3.23174.8" });
        }

        publishCommand.Execute()
            .Should().Pass();

        string publishDirectory = Path.Join(workingDirectory, "MinimalTestApp", "bin", "Debug", tfm, rid, "publish");
        return publishDirectory;
    }

    [DockerAvailableTheory(Skip = "https://github.com/dotnet/sdk/issues/33858")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EndToEnd_NoAPI_Web(bool addPackageReference)
    {
        DirectoryInfo newProjectDir = new DirectoryInfo(Path.Combine(TestSettings.TestArtifactsDirectory, $"CreateNewImageTest_{addPackageReference}"));
        DirectoryInfo privateNuGetAssets = new DirectoryInfo(Path.Combine(TestSettings.TestArtifactsDirectory, "ContainerNuGet"));

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

        var packageDirPath = Path.Combine(TestContext.Current.TestExecutionDirectory, "Container", "package");
        var packagedir = new DirectoryInfo(packageDirPath);

        // do not pollute the primary/global NuGet package store with the private package(s)
        FileInfo[] nupkgs = packagedir.GetFiles("*.nupkg");
        if (nupkgs == null || nupkgs.Length == 0)
        {
            // Build Microsoft.NET.Build.Containers.csproj & wait.
            // for now, fail.
            Assert.Fail("No nupkg found in expected package folder. You may need to rerun the build");
        }

        new DotnetNewCommand(_testOutput, "webapi", "-f", ToolsetInfo.CurrentTargetFramework)
            .WithVirtualHive()
            .WithWorkingDirectory(newProjectDir.FullName)
            // do not pollute the primary/global NuGet package store with the private package(s)
            .WithEnvironmentVariable("NUGET_PACKAGES", privateNuGetAssets.FullName)
            .Execute()
            .Should().Pass();

        if (addPackageReference)
        {
            File.Copy(Path.Combine(TestContext.Current.TestExecutionDirectory, "NuGet.config"), Path.Combine(newProjectDir.FullName, "NuGet.config"));

            new DotnetCommand(_testOutput, "nuget", "add", "source", packagedir.FullName, "--name", "local-temp")
                .WithEnvironmentVariable("NUGET_PACKAGES", privateNuGetAssets.FullName)
                .WithWorkingDirectory(newProjectDir.FullName)
                .Execute()
                .Should().Pass();

            // Add package to the project
            new DotnetCommand(_testOutput, "add", "package", "Microsoft.NET.Build.Containers", "-f", ToolsetInfo.CurrentTargetFramework, "-v", TestContext.Current.ToolsetUnderTest.SdkVersion)
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

            project.Root?.Add(new XElement("PropertyGroup", new XElement("EnableSDKContainerSupport", "true")));
            project.Save(projectPath);
        }

        string imageName = NewImageName();
        string imageTag = "1.0";

        // Build & publish the project
        CommandResult commandResult = new DotnetCommand(
            _testOutput,
            "publish",
            "/p:publishprofile=DefaultContainer",
            "/p:runtimeidentifier=linux-x64",
            $"/p:ContainerBaseImage={DockerRegistryManager.FullyQualifiedBaseImageAspNet}",
            $"/p:ContainerRegistry={DockerRegistryManager.LocalRegistry}",
            $"/p:ContainerRepository={imageName}",
            $"/p:ContainerImageTag={imageTag}",
            $"/p:RuntimeFrameworkVersion=8.0.0-preview.3.23174.8")
            .WithEnvironmentVariable("NUGET_PACKAGES", privateNuGetAssets.FullName)
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute();

        commandResult.Should().Pass();

        if (addPackageReference)
        {
            commandResult.Should().HaveStdOutContaining("warning : Microsoft.NET.Build.Containers NuGet package is explicitly referenced. Consider removing the package reference to Microsoft.NET.Build.Containers as it is now part of .NET SDK.");
        }
        else
        {
            commandResult.Should().NotHaveStdOutContaining("warning");
        }

        ContainerCli.PullCommand(_testOutput, $"{DockerRegistryManager.LocalRegistry}/{imageName}:{imageTag}")
            .Execute()
            .Should().Pass();

        var containerName = "test-container-1";
        CommandResult processResult = ContainerCli.RunCommand(
            _testOutput,
            "--rm",
            "--name",
            containerName,
            "--publish",
            "5017:8080",
            "--detach",
            $"{DockerRegistryManager.LocalRegistry}/{imageName}:{imageTag}")
        .Execute();
        processResult.Should().Pass();
        Assert.NotNull(processResult.StdOut);

        string appContainerId = processResult.StdOut.Trim();

        bool everSucceeded = false;

        HttpClient client = new();

        // Give the server a moment to catch up, but no more than necessary.
        for (int retry = 0; retry < 10; retry++)
        {
            try
            {
                var response = await client.GetAsync("http://localhost:5017/weatherforecast").ConfigureAwait(false);

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

        Assert.True(everSucceeded, "http://localhost:5017/weatherforecast never responded.");

        ContainerCli.StopCommand(_testOutput, appContainerId)
            .Execute()
            .Should().Pass();

        newProjectDir.Delete(true);
        privateNuGetAssets.Delete(true);
    }

    [DockerAvailableFact]
    public void EndToEnd_NoAPI_Console()
    {
        DirectoryInfo newProjectDir = new DirectoryInfo(Path.Combine(TestSettings.TestArtifactsDirectory, "CreateNewImageTest"));
        DirectoryInfo privateNuGetAssets = new DirectoryInfo(Path.Combine(TestSettings.TestArtifactsDirectory, "ContainerNuGet"));

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

        var packageDirPath = Path.Combine(TestContext.Current.TestExecutionDirectory, "Container", "package");
        var packagedir = new DirectoryInfo(packageDirPath);

        FileInfo[] nupkgs = packagedir.GetFiles("*.nupkg");
        if (nupkgs == null || nupkgs.Length == 0)
        {
            // Build Microsoft.NET.Build.Containers.csproj & wait.
            // for now, fail.
            Assert.Fail("No nupkg found in expected package folder. You may need to rerun the build");
        }

        new DotnetNewCommand(_testOutput, "console", "-f", ToolsetInfo.CurrentTargetFramework)
            .WithVirtualHive()
            .WithWorkingDirectory(newProjectDir.FullName)
            // do not pollute the primary/global NuGet package store with the private package(s)
            .WithEnvironmentVariable("NUGET_PACKAGES", privateNuGetAssets.FullName)
            .Execute()
            .Should().Pass();

        File.Copy(Path.Combine(TestContext.Current.TestExecutionDirectory, "NuGet.config"), Path.Combine(newProjectDir.FullName, "NuGet.config"));

        new DotnetCommand(_testOutput, "nuget", "add", "source", packagedir.FullName, "--name", "local-temp")
            .WithEnvironmentVariable("NUGET_PACKAGES", privateNuGetAssets.FullName)
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        // Add package to the project
        new DotnetCommand(_testOutput, "add", "package", "Microsoft.NET.Build.Containers", "-f", ToolsetInfo.CurrentTargetFramework, "-v", TestContext.Current.ToolsetUnderTest.SdkVersion)
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
            $"/p:ContainerBaseImage={DockerRegistryManager.FullyQualifiedBaseImageAspNet}",
            $"/p:ContainerRegistry={DockerRegistryManager.LocalRegistry}",
            $"/p:ContainerRepository={imageName}",
            $"/p:RuntimeFrameworkVersion=8.0.0-preview.3.23174.8",
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

    [DockerSupportsArchInlineData("linux/arm/v7", "linux-arm", "/app")]
    [DockerSupportsArchInlineData("linux/arm64/v8", "linux-arm64", "/app")]
    [DockerSupportsArchInlineData("linux/386", "linux-x86", "/app", Skip = "There's no apphost for linux-x86 so we can't execute self-contained, and there's no .NET runtime base image for linux-x86 so we can't execute framework-dependent.")]
    [DockerSupportsArchInlineData("windows/amd64", "win-x64", "C:\\app")]
    [DockerSupportsArchInlineData("linux/amd64", "linux-x64", "/app")]
    [DockerAvailableTheory]
    public async Task CanPackageForAllSupportedContainerRIDs(string dockerPlatform, string rid, string workingDir)
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(CanPackageForAllSupportedContainerRIDs));
        string publishDirectory = BuildLocalApp(tfm: ToolsetInfo.CurrentTargetFramework, rid: rid);

        // Build the image
        Registry registry = new(ContainerHelpers.TryExpandRegistryToUri(DockerRegistryManager.BaseImageSource), logger);
        var isWin = rid.StartsWith("win");
        ImageBuilder? imageBuilder = await registry.GetImageManifestAsync(
            DockerRegistryManager.RuntimeBaseImage,
            isWin ? DockerRegistryManager.Net8PreviewWindowsSpecificImageTag : DockerRegistryManager.Net8PreviewImageTag,
            rid,
            ToolsetUtils.GetRuntimeGraphFilePath(),
            cancellationToken: default).ConfigureAwait(false);
        Assert.NotNull(imageBuilder);

        Layer l = Layer.FromDirectory(publishDirectory, isWin ? "C:\\app" : "/app", isWin);

        imageBuilder.AddLayer(l);
        imageBuilder.SetWorkingDirectory(workingDir);

        string[] entryPoint = DecideEntrypoint(rid, "MinimalTestApp", workingDir);
        imageBuilder.SetEntrypointAndCmd(entryPoint, Array.Empty<string>());

        BuiltImage builtImage = imageBuilder.Build();

        // Load the image into the local registry
        var sourceReference = new ImageReference(registry, DockerRegistryManager.RuntimeBaseImage, DockerRegistryManager.Net7ImageTag);
        var destinationReference = new ImageReference(registry, NewImageName(), rid);
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

    [DockerAvailableFact]
    public void CanPublishForMultipleRids()
    {
        DirectoryInfo newProjectDir = new DirectoryInfo(Path.Combine(TestSettings.TestArtifactsDirectory, nameof(CanPublishForMultipleRids)));
        DirectoryInfo privateNuGetAssets = new DirectoryInfo(Path.Combine(TestSettings.TestArtifactsDirectory, "ContainerNuGet"));

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

        var packageDirPath = Path.Combine(TestContext.Current.TestExecutionDirectory, "Container", "package");
        var packagedir = new DirectoryInfo(packageDirPath);

        foreach (var rid in new[]{"amd64", "arm64"}) {
            new RunExeCommand(_testOutput, "docker", "image", "rm", nameof(CanPublishForMultipleRids).ToLowerInvariant()+$":1.0.0-{rid}")
                .WithWorkingDirectory(newProjectDir.FullName)
                .Execute();
        }

        FileInfo[] nupkgs = packagedir.GetFiles("*.nupkg");
        if (nupkgs == null || nupkgs.Length == 0)
        {
            // Build Microsoft.NET.Build.Containers.csproj & wait.
            // for now, fail.
            Assert.Fail("No nupkg found in expected package folder. You may need to rerun the build");
        }

        new DotnetNewCommand(_testOutput, "webapi")
            .WithVirtualHive()
            .WithWorkingDirectory(newProjectDir.FullName)
            // do not pollute the primary/global NuGet package store with the private package(s)
            .WithEnvironmentVariable("NUGET_PACKAGES", privateNuGetAssets.FullName)
            .Execute()
            .Should().Pass();

        File.Copy(Path.Combine(TestContext.Current.TestExecutionDirectory, "NuGet.config"), Path.Combine(newProjectDir.FullName, "NuGet.config"));

        new DotnetCommand(_testOutput, "nuget", "add", "source", packagedir.FullName, "--name", "local-temp")
            .WithEnvironmentVariable("NUGET_PACKAGES", privateNuGetAssets.FullName)
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        // Add package to the project
        new DotnetCommand(_testOutput, "add", "package", "Microsoft.NET.Build.Containers", "-v", TestContext.Current.ToolsetUnderTest.SdkVersion)
            .WithEnvironmentVariable("NUGET_PACKAGES", privateNuGetAssets.FullName)
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        // now we need to add rids to the project
        string projectFile = Path.Combine(newProjectDir.FullName, $"{nameof(CanPublishForMultipleRids)}.csproj");

        var project = Microsoft.Build.Construction.ProjectRootElement.Open(projectFile);
        project.PropertyGroups.First().AddProperty("RuntimeIdentifiers", "linux-x64;linux-arm64");
        project.Save();

        new DotnetCommand(_testOutput, "publish", "-bl", "/t:Containerize", "-p", "Version=1.0.0", "--self-contained", "false")
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        // we should have two images
        ListImageTag(nameof(CanPublishForMultipleRids))
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining($"1.0.0-amd64")
            .And.HaveStdOutContaining($"1.0.0-arm64")
            .And.NotHaveStdOutContaining($"1.0.0-x64");
    }

    [DockerAvailableFact]
    public void CanPublishSolution()
    {
        DirectoryInfo solutiondir = new DirectoryInfo(Path.Combine(TestSettings.TestArtifactsDirectory, nameof(CanPublishSolution)));
        DirectoryInfo privateNuGetAssets = new DirectoryInfo(Path.Combine(TestSettings.TestArtifactsDirectory, "ContainerNuGet"));

        string project1 = nameof(project1);
        string project2 = nameof(project2);

        if (solutiondir.Exists)
        {
            solutiondir.Delete(recursive: true);
        }

        if (privateNuGetAssets.Exists)
        {
            privateNuGetAssets.Delete(recursive: true);
        }

        solutiondir.Create();
        privateNuGetAssets.Create();

        var packageDirPath = Path.Combine(TestContext.Current.TestExecutionDirectory, "Container", "package");
        var packagedir = new DirectoryInfo(packageDirPath);

        foreach (var rid in new[]{project1, project2}) {
            new RunExeCommand(_testOutput, "docker", "image", "rm", project1.ToLowerInvariant()+$":1.0.0")
                .WithWorkingDirectory(solutiondir.FullName)
                .Execute();
        }

        FileInfo[] nupkgs = packagedir.GetFiles("*.nupkg");
        if (nupkgs == null || nupkgs.Length == 0)
        {
            // Build Microsoft.NET.Build.Containers.csproj & wait.
            // for now, fail.
            Assert.Fail("No nupkg found in expected package folder. You may need to rerun the build");
        }

        new DotnetNewCommand(_testOutput, "sln")
            .WithVirtualHive()
            .WithWorkingDirectory(solutiondir.FullName)
            // do not pollute the primary/global NuGet package store with the private package(s)
            .WithEnvironmentVariable("NUGET_PACKAGES", privateNuGetAssets.FullName)
            .Execute()
            .Should().Pass();

        new DotnetNewCommand(_testOutput, "webapi", "-o", project1)
            .WithVirtualHive()
            .WithWorkingDirectory(solutiondir.FullName)
            // do not pollute the primary/global NuGet package store with the private package(s)
            .WithEnvironmentVariable("NUGET_PACKAGES", privateNuGetAssets.FullName)
            .Execute()
            .Should().Pass();

        new DotnetNewCommand(_testOutput, "webapi", "-o", project2)
            .WithVirtualHive()
            .WithWorkingDirectory(solutiondir.FullName)
            // do not pollute the primary/global NuGet package store with the private package(s)
            .WithEnvironmentVariable("NUGET_PACKAGES", privateNuGetAssets.FullName)
            .Execute()
            .Should().Pass();

        new DotnetCommand(_testOutput, "sln", "add", project1)
            .WithWorkingDirectory(solutiondir.FullName)
            // do not pollute the primary/global NuGet package store with the private package(s)
            .WithEnvironmentVariable("NUGET_PACKAGES", privateNuGetAssets.FullName)
            .Execute()
            .Should().Pass();

        new DotnetCommand(_testOutput, "sln", "add", project2)
            .WithWorkingDirectory(solutiondir.FullName)
            // do not pollute the primary/global NuGet package store with the private package(s)
            .WithEnvironmentVariable("NUGET_PACKAGES", privateNuGetAssets.FullName)
            .Execute()
            .Should().Pass();

        File.Copy(Path.Combine(TestContext.Current.TestExecutionDirectory, "NuGet.config"), Path.Combine(solutiondir.FullName, "NuGet.config"));

        new DotnetCommand(_testOutput, "nuget", "add", "source", packagedir.FullName, "--name", "local-temp")
            .WithEnvironmentVariable("NUGET_PACKAGES", privateNuGetAssets.FullName)
            .WithWorkingDirectory(solutiondir.FullName)
            .Execute()
            .Should().Pass();

        // Add package to the projects
        new DotnetCommand(_testOutput, "add", "package", "Microsoft.NET.Build.Containers", "-v", TestContext.Current.ToolsetUnderTest.SdkVersion)
            .WithEnvironmentVariable("NUGET_PACKAGES", privateNuGetAssets.FullName)
            .WithWorkingDirectory(Path.Combine(solutiondir.FullName, project1))
            .Execute()
            .Should().Pass();

        new DotnetCommand(_testOutput, "add", "package", "Microsoft.NET.Build.Containers", "-v", TestContext.Current.ToolsetUnderTest.SdkVersion)
            .WithEnvironmentVariable("NUGET_PACKAGES", privateNuGetAssets.FullName)
            .WithWorkingDirectory(Path.Combine(solutiondir.FullName, project2))
            .Execute()
            .Should().Pass();

        new DotnetCommand(_testOutput, "build", "-bl", "/t:Containerize", "--self-contained", "false")
            .WithWorkingDirectory(solutiondir.FullName)
            .Execute()
            .Should().Pass();

        // we should have two images
        ListImageTag(nameof(project1))
            .WithWorkingDirectory(solutiondir.FullName)
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining("latest");

        ListImageTag(nameof(project2))
            .WithWorkingDirectory(solutiondir.FullName)
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining("latest");
    }

    RunExeCommand ListImageTag(string imageName) => new(_testOutput, "docker", "image", "list", imageName.ToLowerInvariant(), "--format", "\"{{.Tag}}\"");
}
