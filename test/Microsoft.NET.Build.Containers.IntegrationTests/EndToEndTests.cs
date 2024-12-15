// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
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

    [DockerAvailableFact]
    public async Task ApiEndToEndWithRegistryPushAndPull()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(ApiEndToEndWithRegistryPushAndPull));
        string publishDirectory = BuildLocalApp();

        // Build the image

        Registry registry = new(DockerRegistryManager.LocalRegistry, logger, RegistryMode.Push);

        ImageBuilder imageBuilder = await registry.GetImageManifestAsync(
            DockerRegistryManager.RuntimeBaseImage,
            DockerRegistryManager.Net9PreviewImageTag,
            "linux-x64",
            ToolsetUtils.RidGraphManifestPicker,
            cancellationToken: default).ConfigureAwait(false);

        Assert.NotNull(imageBuilder);

        Layer l = Layer.FromDirectory(publishDirectory, "/app", false, imageBuilder.ManifestMediaType);

        imageBuilder.AddLayer(l);

        imageBuilder.SetEntrypointAndCmd(new[] { "/app/MinimalTestApp" }, Array.Empty<string>());

        BuiltImage builtImage = imageBuilder.Build();

        // Push the image back to the local registry
        var sourceReference = new SourceImageReference(registry, DockerRegistryManager.RuntimeBaseImage, DockerRegistryManager.Net9PreviewImageTag);
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

    [DockerAvailableFact]
    public async Task ApiEndToEndWithLocalLoad()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(ApiEndToEndWithLocalLoad));
        string publishDirectory = BuildLocalApp(tfm: ToolsetInfo.CurrentTargetFramework);

        // Build the image

        Registry registry = new(DockerRegistryManager.LocalRegistry, logger, RegistryMode.Push);

        ImageBuilder imageBuilder = await registry.GetImageManifestAsync(
            DockerRegistryManager.RuntimeBaseImage,
            DockerRegistryManager.Net9PreviewImageTag,
            "linux-x64",
            ToolsetUtils.RidGraphManifestPicker,
            cancellationToken: default).ConfigureAwait(false);
        Assert.NotNull(imageBuilder);

        Layer l = Layer.FromDirectory(publishDirectory, "/app", false, imageBuilder.ManifestMediaType);

        imageBuilder.AddLayer(l);

        imageBuilder.SetEntrypointAndCmd(new[] { "/app/MinimalTestApp" }, Array.Empty<string>());

        BuiltImage builtImage = imageBuilder.Build();

        // Load the image into the local registry
        var sourceReference = new SourceImageReference(registry, DockerRegistryManager.RuntimeBaseImage, DockerRegistryManager.Net9PreviewImageTag);
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

    [DockerAvailableFact]
    public async Task ApiEndToEndWithArchiveWritingAndLoad()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(ApiEndToEndWithArchiveWritingAndLoad));
        string publishDirectory = BuildLocalApp(tfm: ToolsetInfo.CurrentTargetFramework);

        // Build the image

        Registry registry = new(DockerRegistryManager.LocalRegistry, logger, RegistryMode.Push);

        ImageBuilder imageBuilder = await registry.GetImageManifestAsync(
            DockerRegistryManager.RuntimeBaseImage,
            DockerRegistryManager.Net9PreviewImageTag,
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
        var sourceReference = new SourceImageReference(registry, DockerRegistryManager.RuntimeBaseImage, DockerRegistryManager.Net9PreviewImageTag);
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

        if (tfm == ToolsetInfo.CurrentTargetFramework)
        {
            publishCommand.Arguments.AddRange(new[] { "-p", $"RuntimeFrameworkVersion={DockerRegistryManager.RuntimeFrameworkVersion}" });
        }

        publishCommand.Execute()
            .Should().Pass();

        string publishDirectory = Path.Join(workingDirectory, "MinimalTestApp", "bin", "Debug", _oldFramework, rid, "publish");
        return publishDirectory;
    }

    [DockerAvailableFact]
    public async Task EndToEnd_MultiProjectSolution()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(EndToEnd_MultiProjectSolution));
        DirectoryInfo newSolutionDir = new(Path.Combine(TestSettings.TestArtifactsDirectory, $"CreateNewImageTest_EndToEnd_MultiProjectSolution"));

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

        // Add 'EnableSdkContainerSupport' property to the ConsoleApp and set TFM
        using (FileStream stream = File.Open(Path.Join(newSolutionDir.FullName, "ConsoleApp", "ConsoleApp.csproj"), FileMode.Open, FileAccess.ReadWrite))
        {
            XDocument document = await XDocument.LoadAsync(stream, LoadOptions.None, CancellationToken.None);
            document
                .Descendants()
                .First(e => e.Name.LocalName == "PropertyGroup")?
                .Add(new XElement("EnableSdkContainerSupport", "true"));
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

    [DockerAvailableTheory(Skip = "https://github.com/dotnet/sdk/issues/45181")]
    [InlineData("webapi", false)]
    [InlineData("webapi", true)]
    [InlineData("worker", false)]
    [InlineData("worker", true)]
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

            (string packagePath, string packageVersion) = ToolsetUtils.GetContainersPackagePath();

            new DotnetCommand(_testOutput, "nuget", "add", "source", Path.GetDirectoryName(packagePath), "--name", "local-temp")
                .WithEnvironmentVariable("NUGET_PACKAGES", privateNuGetAssets.FullName)
                .WithWorkingDirectory(newProjectDir.FullName)
                .Execute()
                .Should().Pass();

            // Add package to the project
            new DotnetCommand(_testOutput, "add", "package", "Microsoft.NET.Build.Containers", "-f", ToolsetInfo.CurrentTargetFramework, "-v", packageVersion)
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
        string imageTag = $"1.0-{projectType}-{addPackageReference}";

        // Build & publish the project
        CommandResult commandResult = new DotnetCommand(
            _testOutput,
            "publish",
            "/p:PublishProfile=DefaultContainer",
            "/p:RuntimeIdentifier=linux-x64",
            $"/p:ContainerBaseImage={DockerRegistryManager.FullyQualifiedBaseImageAspNet}",
            $"/p:ContainerRegistry={DockerRegistryManager.LocalRegistry}",
            $"/p:ContainerRepository={imageName}",
            $"/p:ContainerImageTag={imageTag}",
            "/p:UseRazorSourceGenerator=false",
            $"/p:RuntimeFrameworkVersion={DockerRegistryManager.RuntimeFrameworkVersion}")
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
            "--rm",
            "--name",
            containerName,
            "-P",
            "--detach",
            $"{DockerRegistryManager.LocalRegistry}/{imageName}:{imageTag}")
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
            var containerLogs =
            ContainerCli.LogsCommand(_testOutput, appContainerId)
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining("Worker running at");

            ContainerCli.StopCommand(_testOutput, appContainerId)
            .Execute()
            .Should().Pass();
        }
        else
        {
            throw new NotImplementedException("Unknown project type");
        }

        newProjectDir.Delete(true);
        privateNuGetAssets.Delete(true);
    }

    [DockerAvailableFact]
    public void EndToEnd_NoAPI_Console()
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

        (string packagePath, string packageVersion) = ToolsetUtils.GetContainersPackagePath();

        new DotnetCommand(_testOutput, "nuget", "add", "source", Path.GetDirectoryName(packagePath), "--name", "local-temp")
            .WithEnvironmentVariable("NUGET_PACKAGES", privateNuGetAssets.FullName)
            .WithWorkingDirectory(newProjectDir.FullName)
            .Execute()
            .Should().Pass();

        // Add package to the project
        new DotnetCommand(_testOutput, "add", "package", "Microsoft.NET.Build.Containers", "-f", _oldFramework , "-v", packageVersion)
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
            $"/p:ContainerImageTag={imageTag}",
            "/p:EnableSdkContainerSupport=true",
            $"/p:RuntimeFrameworkVersion={DockerRegistryManager.RuntimeFrameworkVersion}")
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
        Registry registry = new(DockerRegistryManager.BaseImageSource, logger, RegistryMode.Push);
        var isWin = rid.StartsWith("win");
        ImageBuilder? imageBuilder = await registry.GetImageManifestAsync(
            DockerRegistryManager.RuntimeBaseImage,
            isWin ? DockerRegistryManager.Net8PreviewWindowsSpecificImageTag : DockerRegistryManager.Net9PreviewImageTag,
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
        var sourceReference = new SourceImageReference(registry, DockerRegistryManager.RuntimeBaseImage, DockerRegistryManager.Net9PreviewImageTag);
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
}
