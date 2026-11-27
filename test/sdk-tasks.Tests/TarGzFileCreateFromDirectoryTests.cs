// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.DotNet.Build.Tasks;

namespace Microsoft.CoreSdkTasks.Tests;

[TestClass]
public class TarGzFileCreateFromDirectoryTests
{
    [TestMethod]
    public void ItPreservesTheDefaultTarArguments()
    {
        var task = new TarGzFileCreateFromDirectory
        {
            DestinationArchive = "archive.tar.gz",
            SourceDirectory = "source",
            IsPigzAvailable = () => false,
        };

        GetCommandLine(task).Should().StartWith("-czf archive.tar.gz ");
    }

    [TestMethod]
    public void ItNormalizesOwnershipForSourceBuild()
    {
        var task = new TarGzFileCreateFromDirectory
        {
            DestinationArchive = "archive.tar.gz",
            NormalizeSourceBuildOwnership = true,
            SourceDirectory = "source",
            IsPigzAvailable = () => false,
        };

        GetCommandLine(task).Should().StartWith("--numeric-owner --owner=2001 --group=2001 -czf archive.tar.gz ");
    }

    [TestMethod]
    public void ItUsesPigzWhenAvailable()
    {
        var task = new TarGzFileCreateFromDirectory
        {
            DestinationArchive = "archive.tar.gz",
            SourceDirectory = "source",
            IsPigzAvailable = () => true,
        };

        GetCommandLine(task).Should().StartWith("--use-compress-program=pigz -cf archive.tar.gz ");
    }

    [TestMethod]
    public void ItUsesPigzWithSourceBuildOwnershipNormalization()
    {
        var task = new TarGzFileCreateFromDirectory
        {
            DestinationArchive = "archive.tar.gz",
            NormalizeSourceBuildOwnership = true,
            SourceDirectory = "source",
            IsPigzAvailable = () => true,
        };

        GetCommandLine(task).Should().StartWith("--numeric-owner --owner=2001 --group=2001 --use-compress-program=pigz -cf archive.tar.gz ");
    }

    [TestMethod]
    public void ItFallsBackToGzipWhenUsePigzIsFalse()
    {
        var task = new TarGzFileCreateFromDirectory
        {
            DestinationArchive = "archive.tar.gz",
            SourceDirectory = "source",
            UsePigz = false,
            IsPigzAvailable = () => true,
        };

        GetCommandLine(task).Should().StartWith("-czf archive.tar.gz ");
    }

    [TestMethod]
    public void ItProbesPathForPigz()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"pigz-probe-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            TarGzFileCreateFromDirectory.IsPigzOnPath(null).Should().BeFalse();
            TarGzFileCreateFromDirectory.IsPigzOnPath(string.Empty).Should().BeFalse();
            TarGzFileCreateFromDirectory.IsPigzOnPath(tempDir).Should().BeFalse();

            string executableName = OperatingSystem.IsWindows() ? "pigz.exe" : "pigz";
            string candidatePath = Path.Combine(tempDir, executableName);
            File.WriteAllText(candidatePath, string.Empty);

            string missingDir = Path.Combine(tempDir, "nonexistent");

            if (OperatingSystem.IsWindows())
            {
                TarGzFileCreateFromDirectory.IsPigzOnPath(tempDir).Should().BeTrue();
                TarGzFileCreateFromDirectory.IsPigzOnPath($"\"{tempDir}\"").Should().BeTrue();
                TarGzFileCreateFromDirectory.IsPigzOnPath($"{missingDir}{Path.PathSeparator}{tempDir}").Should().BeTrue();
            }
            else
            {
                File.SetUnixFileMode(candidatePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                TarGzFileCreateFromDirectory.IsPigzOnPath(tempDir).Should().BeFalse();

                File.SetUnixFileMode(candidatePath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                TarGzFileCreateFromDirectory.IsPigzOnPath(tempDir).Should().BeTrue();
                TarGzFileCreateFromDirectory.IsPigzOnPath($"{missingDir}{Path.PathSeparator}{tempDir}").Should().BeTrue();
            }
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [UnsupportedOSPlatform("windows")]
    public void ItPreservesUnixPathEntriesWithoutTrimming()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"pigz-path-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            string pigzPath = Path.Combine(tempDir, "pigz");
            File.WriteAllText(pigzPath, string.Empty);
            File.SetUnixFileMode(pigzPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            TarGzFileCreateFromDirectory.IsPigzOnPath($"{tempDir} ").Should().BeFalse();
            TarGzFileCreateFromDirectory.IsPigzOnPath($" {tempDir}").Should().BeFalse();

            string spacedDir = Path.Combine(tempDir, "bin ");
            Directory.CreateDirectory(spacedDir);
            string spacedPigzPath = Path.Combine(spacedDir, "pigz");
            File.WriteAllText(spacedPigzPath, string.Empty);
            File.SetUnixFileMode(spacedPigzPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            TarGzFileCreateFromDirectory.IsPigzOnPath(spacedDir).Should().BeTrue();
            TarGzFileCreateFromDirectory.IsPigzOnPath(spacedDir.TrimEnd()).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [UnsupportedOSPlatform("windows")]
    public void ItChecksEffectiveExecuteAccessOnUnix()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"pigz-access-{Guid.NewGuid():N}");
        string inaccessibleDir = Path.Combine(tempDir, "inaccessible");
        string executableDir = Path.Combine(tempDir, "executable");
        Directory.CreateDirectory(inaccessibleDir);
        Directory.CreateDirectory(executableDir);

        try
        {
            string inaccessiblePigz = Path.Combine(inaccessibleDir, "pigz");
            File.WriteAllText(inaccessiblePigz, string.Empty);

            // Mode 0641 grants execute only to others; for the owning non-root user, owner bits (rw-) deny execution.
            File.SetUnixFileMode(
                inaccessiblePigz,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherExecute);

            if (geteuid() != 0)
            {
                TarGzFileCreateFromDirectory.IsPigzOnPath(inaccessibleDir).Should().BeFalse();

                string executablePigz = Path.Combine(executableDir, "pigz");
                File.WriteAllText(executablePigz, string.Empty);
                File.SetUnixFileMode(executablePigz, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

                TarGzFileCreateFromDirectory.IsPigzOnPath($"{inaccessibleDir}{Path.PathSeparator}{executableDir}").Should().BeTrue();
            }
            else
            {
                // Root can execute any file with at least one execute bit set, but not one with none.
                TarGzFileCreateFromDirectory.IsPigzOnPath(inaccessibleDir).Should().BeTrue();

                File.SetUnixFileMode(inaccessiblePigz, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
                TarGzFileCreateFromDirectory.IsPigzOnPath(inaccessibleDir).Should().BeFalse();
            }
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static string GetCommandLine(TarGzFileCreateFromDirectory task)
    {
        MethodInfo method = typeof(TarGzFileCreateFromDirectory).GetMethod(
            "GenerateCommandLineCommands",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        return (string)method.Invoke(task, null)!;
    }

    [DllImport("libc")]
    private static extern uint geteuid();
}
