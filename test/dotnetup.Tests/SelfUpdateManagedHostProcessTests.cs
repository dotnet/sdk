// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class SelfUpdateManagedHostProcessTests
{
    private static string s_dotnetPath = null!;
    private static string s_assemblyPath = null!;
    private static string s_buildDirectory = null!;

    [ClassInitialize]
    public static void BuildAsset(TestContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        string repoRoot = Path.GetFullPath(typeof(SelfUpdateManagedHostProcessTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>().Single(attribute => attribute.Key == "RepoRoot").Value!);
        s_dotnetPath = SelfUpdateTestFiles.ResolveDotnetHostPath();
        string artifactsRoot = Environment.GetEnvironmentVariable("ArtifactsDir") ?? Path.GetTempPath();
        s_buildDirectory = Path.Combine(artifactsRoot, "managed-dotnetup-" + Guid.NewGuid().ToString("N"));
        string outputDirectory = Path.Combine(s_buildDirectory, "out");
        var framework = new FrameworkName(typeof(SelfUpdateManagedHostProcessTests).Assembly
            .GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        var startInfo = new ProcessStartInfo(s_dotnetPath) { UseShellExecute = false, WorkingDirectory = repoRoot };
        foreach (string argument in new[]
        {
            "build",
            Path.Combine(repoRoot, "test", "TestAssets", "ManagedDotnetup", "ManagedDotnetup.csproj"),
            "--output", outputDirectory,
            "/p:CurrentTargetFramework=net" + framework.Version.ToString(2),
            "/p:ArtifactsDir=" + s_buildDirectory + Path.DirectorySeparatorChar,
            "/nodeReuse:false",
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        Assert.IsNotNull(process);
        try
        {
            Assert.IsTrue(process.WaitForExit(120_000), "Managed dotnetup asset build timed out.");
            Assert.AreEqual(0, process.ExitCode, "Managed dotnetup asset build failed.");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(10_000);
            }
        }

        s_assemblyPath = Path.Combine(outputDirectory, "dotnetup.dll");
    }

    [ClassCleanup]
    public static void CleanupAsset()
    {
        if (Directory.Exists(s_buildDirectory))
        {
            Directory.Delete(s_buildDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void DotnetHostedDllCannotSelfUpdate()
    {
        using var environment = new TestEnvironment();
        var result = DotnetupTestUtilities.RunDotnetupProcess(
            [s_assemblyPath, "self", "update"],
            captureOutput: true,
            workingDirectory: environment.TempRoot,
            executablePath: s_dotnetPath,
            timeout: TimeSpan.FromSeconds(30),
            environmentVariables: new()
            {
                ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
                ["DOTNET_CLI_UI_LANGUAGE"] = "en-US",
                ["DOTNET_DOTNETUP_DATA_DIR"] = Path.Combine(environment.TempRoot, "data"),
                ["DOTNET_CLI_HOME"] = Path.Combine(environment.TempRoot, "home"),
            });

        Assert.AreEqual(1, result.exitCode, result.output);
        Assert.Contains(Strings.SelfUpdateUnsupportedHost, result.output);
    }
}
