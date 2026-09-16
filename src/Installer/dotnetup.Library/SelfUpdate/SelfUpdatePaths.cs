// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

/// <summary>Names and validates sibling artifacts in a canonical dotnetup installation.</summary>
internal sealed class SelfUpdatePaths
{
    public SelfUpdatePaths(string installedPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installedPath);
        InstalledPath = Path.GetFullPath(installedPath);
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

    public string CreateBackupPath() => InstalledPath + ".old." + Guid.NewGuid().ToString("N");

    public void Validate()
    {
        ValidateLocation();
        using var executable = SelfUpdateFile.Open(InstalledPath);
    }

    public static string ReadIdentity(string path)
    {
        using var stream = SelfUpdateFile.Open(path);
        return DotnetupBuildIdentityReader.Read(stream);
    }

    internal void ValidateLocation()
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var expectedName = OperatingSystem.IsWindows() ? "dotnetup.exe" : "dotnetup";
        if (!string.Equals(Path.GetFileName(InstalledPath), expectedName, comparison))
        {
            throw new IOException("Self-update requires the canonical dotnetup executable name.");
        }

        ValidateDirectory(DirectoryPath);
    }

    internal void ValidateBackupPath(string backupPath)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var prefix = InstalledPath + ".old.";
        if (!backupPath.StartsWith(prefix, comparison) || !Guid.TryParseExact(backupPath[prefix.Length..], "N", out _))
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
        if ((attributes & FileAttributes.Directory) == 0 || (attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("Self-update requires a directory path without links or reparse points.");
        }
    }
}