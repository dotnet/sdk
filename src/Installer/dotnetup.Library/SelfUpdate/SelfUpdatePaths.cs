// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

/// <summary>Names and opens self-update artifacts with consistent path validation and sharing.</summary>
internal sealed class SelfUpdatePaths
{
    public SelfUpdatePaths(string installedPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installedPath);
        InstalledPath = ResolvePath(installedPath);
        DirectoryPath = Path.GetDirectoryName(InstalledPath)!;
        StagedPath = InstalledPath + ".new";
        UpdateLockPath = Path.Combine(DirectoryPath, "dotnetup.update.lock");
        ActivityLockPath = Path.Combine(DirectoryPath, "dotnetup.activity.lock");
    }

    public string InstalledPath { get; }
    public string DirectoryPath { get; }
    public string StagedPath { get; }
    public string UpdateLockPath { get; }
    public string ActivityLockPath { get; }

    public string CreateBackupPath() => InstalledPath + ".old." + Guid.NewGuid().ToString("N")[..8];

    internal static string ResolvePath(string path)
    {
        // Resolve only the directory: executable and recovery-file symlinks must still be rejected.
        var directory = Path.GetDirectoryName(path);
        var realDirectory = ExecutablePathResolver.ResolveRealPath(string.IsNullOrEmpty(directory) ? "." : directory);
        return Path.Combine(realDirectory!, Path.GetFileName(path));
    }

    internal static bool IsBackupIdentifier(ReadOnlySpan<char> identifier)
    {
        if (identifier.Length != 8)
        {
            return false;
        }

        foreach (var character in identifier)
        {
            if (!char.IsAsciiHexDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Validates a self-update target: the canonical name plus <see cref="ValidateExecutable"/>.</summary>
    public void Validate()
    {
        ValidateLocation();
        ValidateExecutable();
    }

    /// <summary>
    /// Validates that the executable and its directories exist without links or reparse points,
    /// regardless of the executable's file name. The non-safe command gate uses this so renamed
    /// executables can run ordinary commands; only self-update requires the canonical name.
    /// </summary>
    public void ValidateExecutable()
    {
        using var executable = OpenFile(InstalledPath);
    }

    public static FileStream OpenFile(string path, FileAccess access = FileAccess.Read)
    {
        path = ResolvePath(path);
        ValidateDirectory(Path.GetDirectoryName(path)!);
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw UnsupportedLocation(path);
        }

        if ((attributes & (FileAttributes.Directory | FileAttributes.Device)) != 0)
        {
            throw new IOException($"Self-update requires a regular file: '{path}'.");
        }

        return new FileStream(path, FileMode.Open, access, FileShare.Read | FileShare.Delete);
    }

    public static bool Exists(string path)
    {
        try
        {
            _ = File.GetAttributes(path);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    public static void RequireAbsent(string path)
    {
        if (Exists(path))
        {
            throw new IOException($"Self-update will not overwrite an occupied recovery path: '{path}'.");
        }
    }

    internal void ValidateLocation()
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var expectedName = OperatingSystem.IsWindows() ? "dotnetup.exe" : "dotnetup";
        if (!string.Equals(Path.GetFileName(InstalledPath), expectedName, comparison))
        {
            throw new SelfUpdateLocationException(DotnetInstallErrorCode.DotnetupNonCanonicalExecutableName,
                string.Format(CultureInfo.CurrentCulture, Strings.SelfUpdateRequiresCanonicalName, expectedName, InstalledPath));
        }

        ValidateDirectory(DirectoryPath);
    }

    internal void ValidateBackupPath(string backupPath)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var prefix = InstalledPath + ".old.";
        if (!backupPath.StartsWith(prefix, comparison) || !IsBackupIdentifier(backupPath.AsSpan(prefix.Length)))
        {
            throw new IOException("Self-update requires a transaction-specific sibling backup path.");
        }
    }

    internal static void ValidateDirectory(string directoryPath)
    {
        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(directoryPath));
        if (parent is not null)
        {
            ValidateDirectory(parent);
        }

        var attributes = File.GetAttributes(directoryPath);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw UnsupportedLocation(directoryPath);
        }

        if ((attributes & FileAttributes.Directory) == 0)
        {
            throw new IOException($"Self-update requires a directory: '{directoryPath}'.");
        }
    }

    private static SelfUpdateLocationException UnsupportedLocation(string path)
        => new(DotnetInstallErrorCode.DotnetupUnsupportedInstallLocation,
            string.Format(CultureInfo.CurrentCulture, Strings.SelfUpdateUnsupportedLocation, path));
}