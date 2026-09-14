// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Microsoft.DotNet.Cli.Installer.Windows;

internal static class MsiPackageData
{
    public static bool TryGetMsiPath(
        string packageDataPath,
        [NotNullWhen(true)] out string? msiPath,
        out string manifestPath,
        Action<string>? logMessage = null)
    {
        msiPath = default;
        manifestPath = Path.GetFullPath(Path.Combine(packageDataPath, "msi.json"));

        if (!File.Exists(manifestPath))
        {
            logMessage?.Invoke($"MSI manifest file does not exist, '{manifestPath}'");
            return false;
        }

        MsiManifest? msiManifest = JsonSerializer.Deserialize(
            File.ReadAllText(manifestPath),
            MsiManifestJsonSerializerContext.Default.MsiManifest);
        string possibleMsiPath = Path.Combine(Path.GetDirectoryName(manifestPath)!, msiManifest?.Payload ?? string.Empty);

        if (!File.Exists(possibleMsiPath))
        {
            logMessage?.Invoke($"MSI package not found, '{possibleMsiPath}'");
            return false;
        }

        msiPath = possibleMsiPath;
        return true;
    }
}
