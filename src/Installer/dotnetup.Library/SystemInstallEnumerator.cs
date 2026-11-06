// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>Lists installs in a single root using the host's folder rules, without loading native code.</summary>
internal static class SystemInstallEnumerator
{
    internal static List<DotnetInstall> GetInstalls(DotnetInstallRoot installRoot)
    {
        var installs = new List<DotnetInstall>();
        AddInstalls(installs, installRoot, Path.Combine(installRoot.Path, "sdk"), InstallComponent.SDK);

        string sharedDirectory = Path.Combine(installRoot.Path, "shared");
        if (Directory.Exists(sharedDirectory))
        {
            foreach (string frameworkDirectory in Directory.EnumerateDirectories(sharedDirectory))
            {
                string frameworkName = Path.GetFileName(frameworkDirectory);
                InstallComponent component = InstallComponentExtensions.FromFrameworkName(frameworkName) ?? InstallComponent.Runtime;
                AddInstalls(installs, installRoot, frameworkDirectory, component, frameworkName);
            }
        }

        return installs;
    }

    private static void AddInstalls(
        List<DotnetInstall> installs,
        DotnetInstallRoot installRoot,
        string componentDirectory,
        InstallComponent component,
        string? frameworkName = null)
    {
        if (!Directory.Exists(componentDirectory))
        {
            return;
        }

        foreach (string versionDirectory in Directory.EnumerateDirectories(componentDirectory))
        {
            if (ReleaseVersion.TryParse(Path.GetFileName(versionDirectory), out ReleaseVersion? version)
                && InstallationValidator.IsInstallDirectory(versionDirectory, frameworkName))
            {
                installs.Add(new DotnetInstall(installRoot, version, component));
            }
        }
    }
}
