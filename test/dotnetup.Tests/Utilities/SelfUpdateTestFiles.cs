// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;
using Microsoft.NET.TestFramework;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

internal sealed class SelfUpdateTestFiles : IDisposable
{
    public const string OriginalVersion = "0.2.0-preview.1.26465.6";
    public const string ReplacementVersion = "0.2.0-preview.1.26465.7";

    private static readonly ConcurrentDictionary<string, Lazy<string>> s_assetOutputs = new();
    private readonly DirectoryInfo _directory;

    public SelfUpdateTestFiles(bool executable = true, string mode = "valid")
    {
        _directory = Directory.CreateDirectory(Path.Combine(SdkTestContext.GetRepoRoot()!, "artifacts",
            "selfupdate-test-" + Guid.NewGuid().ToString("N")));
        Paths = new SelfUpdatePaths(Path.Combine(_directory.FullName, OperatingSystem.IsWindows() ? "dotnetup.exe" : "dotnetup"));
        BackupPath = Paths.CreateBackupPath();
        Replacement = new SelfUpdateReplacement(Paths, BackupPath);

        if (executable)
        {
            WriteExecutable(Paths.InstalledPath, OriginalVersion);
            WriteExecutable(Paths.StagedPath, ReplacementVersion);
            File.WriteAllText(Paths.InstalledPath + ".mode", mode);
        }
        else
        {
            File.WriteAllText(Paths.InstalledPath, "original");
            File.WriteAllText(Paths.StagedPath, "replacement");
        }
    }

    public SelfUpdatePaths Paths { get; }
    public string BackupPath { get; }
    public SelfUpdateReplacement Replacement { get; }
    public static string DotnetHostPath => ResolveDotnetHostPath();
    public static string ProcessAssemblyPath => Directory.GetFiles(GetAssetOutput(OriginalVersion), "*.dll")
        .Single(path => Path.GetFileName(path).StartsWith("SelfUpdateProcess", StringComparison.Ordinal));

    public static void WriteExecutable(string path, string version)
    {
        var output = GetAssetOutput(version);
        var directory = Path.GetDirectoryName(path)!;
        foreach (var dependency in Directory.EnumerateFiles(output))
        {
            var destination = Path.Combine(directory, Path.GetFileName(dependency));
            if (!File.Exists(destination))
            {
                File.Copy(dependency, destination);
            }
        }

        var assemblyName = Path.GetFileNameWithoutExtension(Directory.GetFiles(output, "*.runtimeconfig.json").Single())
            .Replace(".runtimeconfig", "", StringComparison.Ordinal);
        File.Copy(Path.Combine(output, assemblyName + (OperatingSystem.IsWindows() ? ".exe" : "")), path, overwrite: true);
    }

    public void Dispose() => _directory.Delete(recursive: true);

    private static string GetAssetOutput(string version)
        => s_assetOutputs.GetOrAdd(version, static value => new Lazy<string>(() => BuildAsset(value))).Value;

    private static string BuildAsset(string version)
    {
        var repoRoot = Path.GetFullPath(typeof(SelfUpdateTestFiles).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "RepoRoot").Value!);
        var dotnetPath = ResolveDotnetHostPath();
        var buildDirectory = Path.Combine(repoRoot, "artifacts", "selfupdate-process-" + Guid.NewGuid().ToString("N"));
        buildDirectory = ExecutablePathResolver.ResolveRealPath(Directory.CreateDirectory(buildDirectory).FullName)!;
        var output = Path.Combine(buildDirectory, "out");
        var assemblyName = "SelfUpdateProcess" + Guid.NewGuid().ToString("N");
        var framework = new FrameworkName(typeof(SelfUpdateTestFiles).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        var startInfo = new ProcessStartInfo(dotnetPath) { UseShellExecute = false, WorkingDirectory = repoRoot };
        foreach (var argument in new[]
        {
            // Copies live one directory below artifacts, like buildDirectory.
            "publish", Path.Combine(repoRoot!, "test", "TestAssets", "SelfUpdateProcess", "SelfUpdateProcess.csproj"),
            "--output", output,
            "/p:CurrentTargetFramework=net" + framework.Version.ToString(2),
            "/p:AppHostRelativeDotNet=" + Path.GetRelativePath(buildDirectory, Path.GetDirectoryName(dotnetPath)!),
            "/p:AssemblyName=" + assemblyName,
            "/p:Version=" + version,
            "/p:InformationalVersion=" + version,
            "/p:IncludeSourceRevisionInInformationalVersion=false",
            "/p:ArtifactsDir=" + buildDirectory + Path.DirectorySeparatorChar,
            "/p:BaseOutputPath=" + Path.Combine(buildDirectory, "bin") + Path.DirectorySeparatorChar,
            "/p:BaseIntermediateOutputPath=" + Path.Combine(buildDirectory, "obj") + Path.DirectorySeparatorChar,
            "/nodeReuse:false",
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        Assert.IsNotNull(process);
        try
        {
            Assert.IsTrue(process.WaitForExit(120_000), "Self-update process fixture publish timed out.");
            Assert.AreEqual(0, process.ExitCode, "Self-update process fixture publish failed.");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                Assert.IsTrue(process.WaitForExit(10_000));
            }
        }

        return output;
    }

    private static string ResolveDotnetHostPath()
    {
        var processPath = Environment.ProcessPath!;
        var dotnetPath = string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase)
            ? processPath
            : Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? SdkTestContext.Current.ToolsetUnderTest.DotNetHostPath;
        return ExecutablePathResolver.ResolveRealPath(dotnetPath)!;
    }
}