// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;
using Microsoft.NET.TestFramework;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

internal sealed class SelfUpdateTestFiles : IDisposable
{
    public const string OriginalIdentity = "0.2.0-preview.1.26465.6|win-x64";
    public const string ReplacementIdentity = "0.2.0-preview.1.26465.7|win-x64";

    private static readonly Lazy<string> s_assetOutput = new(BuildAsset);
    private readonly DirectoryInfo _directory;

    public SelfUpdateTestFiles(bool executable = false, string mode = "valid")
    {
        _directory = executable
            ? Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(s_assetOutput.Value)!, Guid.NewGuid().ToString("N")))
            : Directory.CreateTempSubdirectory("selfupdate-test-");
        Paths = new SelfUpdatePaths(Path.Combine(_directory.FullName, OperatingSystem.IsWindows() ? "dotnetup.exe" : "dotnetup"));
        BackupPath = Paths.CreateBackupPath();
        Replacement = new SelfUpdateReplacement(Paths, BackupPath, OriginalIdentity);

        if (executable)
        {
            foreach (var path in Directory.EnumerateFiles(s_assetOutput.Value))
            {
                File.Copy(path, Path.Combine(_directory.FullName, Path.GetFileName(path)));
            }

            var apphost = Path.Combine(_directory.FullName, OperatingSystem.IsWindows() ? "SelfUpdateProcess.exe" : "SelfUpdateProcess");
            File.Copy(apphost, Paths.InstalledPath);
            File.Copy(apphost, Paths.StagedPath);
            WriteIdentity(Paths.InstalledPath, OriginalIdentity, append: true);
            WriteIdentity(Paths.StagedPath, ReplacementIdentity, append: true);
            File.WriteAllText(Paths.InstalledPath + ".mode", mode);
        }
        else
        {
            WriteIdentity(Paths.InstalledPath, OriginalIdentity);
            WriteIdentity(Paths.StagedPath, ReplacementIdentity);
        }
    }

    public SelfUpdatePaths Paths { get; }
    public string BackupPath { get; }
    public SelfUpdateReplacement Replacement { get; }
    public static string DotnetHostPath => ResolveDotnetHostPath();
    public static string ProcessAssemblyPath => Path.Combine(s_assetOutput.Value, "SelfUpdateProcess.dll");

    public static void WriteIdentity(string path, string identity, bool append = false)
    {
        using var stream = new FileStream(path, append ? FileMode.Append : FileMode.Create, FileAccess.Write);
        stream.Write(Encoding.ASCII.GetBytes("DOTNETUP-VR-REC\0\u0001\0\0\0\0\0\0\0" + identity.PadRight(224, '\0') + "END-VER\0"));
    }

    public void Dispose() => _directory.Delete(recursive: true);

    private static string BuildAsset()
    {
        var repoRoot = Path.GetFullPath(typeof(SelfUpdateTestFiles).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "RepoRoot").Value!);
        var dotnetPath = ResolveDotnetHostPath();
        var buildDirectory = Path.Combine(Environment.GetEnvironmentVariable("ArtifactsDir") ?? Path.GetTempPath(), "selfupdate-process-" + Guid.NewGuid().ToString("N"));
        // Embed a physical relative runtime path, even when the publish root is reached through /var on macOS.
        buildDirectory = ExecutablePathResolver.ResolveRealPath(Directory.CreateDirectory(buildDirectory).FullName)!;
        var output = Path.Combine(buildDirectory, "out");
        var framework = new FrameworkName(typeof(SelfUpdateTestFiles).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        var startInfo = new ProcessStartInfo(dotnetPath) { UseShellExecute = false, WorkingDirectory = repoRoot };
        foreach (var argument in new[]
        {
            // AppHostRelativeDotNet is embedded during publish, not build. Copies live in siblings of output.
            "publish", Path.Combine(repoRoot!, "test", "TestAssets", "SelfUpdateProcess", "SelfUpdateProcess.csproj"),
            "--output", output,
            "/p:CurrentTargetFramework=net" + framework.Version.ToString(2),
            "/p:AppHostRelativeDotNet=" + Path.GetRelativePath(output, Path.GetDirectoryName(dotnetPath)!),
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