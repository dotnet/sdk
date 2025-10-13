// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.Watch.UnitTests;

public class DotNetWatchLauncherTests(ITestOutputHelper logger)
{
    private TestAssetsManager TestAssets { get; } = new(logger);

    [Fact]
    public async Task AspireWatchLaunch()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchAppWithProjectDeps")
            .WithSource();

        var path = Path.ChangeExtension(typeof(DotNetWatchLauncher).Assembly.Location, PathUtilities.ExecutableExtension);
        var sdkRootDirectory = TestContext.Current.ToolsetUnderTest.SdkFolderUnderTest;
        var projectDir = Path.Combine(testAsset.Path, "AppWithDeps");
        var projectPath = Path.Combine(projectDir, "App.WithDeps.csproj");

        var startInfo = new ProcessStartInfo
        {
            FileName = path,
            Arguments = $@"--sdk ""{sdkRootDirectory}"" --project ""{projectPath}"" --verbose",
            UseShellExecute = false,
            RedirectStandardInput = true,
            WorkingDirectory = projectDir,
        };

        using var process = new AwaitableProcess(logger);
        process.Start(startInfo);

        await process.GetOutputLineAsync(success: line => line.Contains("dotnet watch ⌚ Waiting for changes"), failure: _ => false);
    }
}
