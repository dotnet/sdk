// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Lists the SDKs and runtimes that a host would see in any dotnet root (system, user, or a global.json path),
/// from the on-disk layout and without loading native code.
/// Assumptions: only components dotnetup models are listed, and a version folder counts as an install only when it
/// passes <see cref="InstallationValidator.PassesValidationSmokeCheck"/>.
/// An SDK's <see cref="DotnetInstall.Version"/> is its build identity from <c>.version</c> when the file describes
/// the folder, with the folder version as <see cref="DotnetInstall.ProductVersion"/>. Shared frameworks record no build
/// identity, so runtimes and SDKs without a matching <c>.version</c> use the folder version.
/// </summary>
internal static class InstallRootEnumerator
{
    internal static List<DotnetInstall> GetInstalls(DotnetInstallRoot installRoot)
    {
        var installs = new List<DotnetInstall>();
        foreach (InstallComponent component in Enum.GetValues<InstallComponent>())
        {
            AddInstalls(installs, installRoot, component);
        }

        return installs;
    }

    private static void AddInstalls(List<DotnetInstall> installs, DotnetInstallRoot installRoot, InstallComponent component)
    {
        string componentRoot = component == InstallComponent.SDK
            ? Path.Combine(installRoot.Path, "sdk")
            : Path.Combine(installRoot.Path, "shared", component.GetFrameworkName());
        if (!Directory.Exists(componentRoot))
        {
            return;
        }

        foreach (string versionDirectory in Directory.EnumerateDirectories(componentRoot))
        {
            // A version that does not round-trip names a different folder, so its marker check fails.
            if (ReleaseVersion.TryParse(Path.GetFileName(versionDirectory), out ReleaseVersion? layoutVersion))
            {
                var install = new DotnetInstall(installRoot, layoutVersion, component);
                if (InstallationValidator.PassesValidationSmokeCheck(install))
                {
                    installs.Add(WithBuildIdentity(install));
                }
            }
        }
    }

    private static DotnetInstall WithBuildIdentity(DotnetInstall install)
    {
        if (install.Component != InstallComponent.SDK
            || LayoutVersionFile.ReadFromDirectory(install.ComponentDirectory)?.GetBuildVersionFor(install.LayoutVersion) is not { } buildVersion)
        {
            return install;
        }

        return new DotnetInstall(install.InstallRoot, buildVersion, install.Component).WithProductVersion(install.LayoutVersion);
    }
}
