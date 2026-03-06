// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Result of an installation operation.
/// </summary>
/// <param name="Install">The DotnetInstall for the completed installation.</param>
/// <param name="WasAlreadyInstalled">True if the SDK was already installed and no work was done.</param>
internal sealed record InstallResult(DotnetInstall Install, bool WasAlreadyInstalled);

internal class InstallerOrchestratorSingleton
{
    public static InstallerOrchestratorSingleton Instance { get; } = new();

    private InstallerOrchestratorSingleton()
    {
    }

    private static ScopedMutex ModifyInstallStateMutex() => new(Constants.MutexNames.ModifyInstallationStates);

    // Throws DotnetInstallException on failure, returns InstallResult on success
#pragma warning disable CA1822 // Intentionally an instance method on a singleton
    public InstallResult Install(DotnetInstallRequest installRequest, bool noProgress = false)
    {
        // Validate channel format before attempting resolution
        if (!ChannelVersionResolver.IsValidChannelFormat(installRequest.Channel.Name))
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.InvalidChannel,
                $"'{installRequest.Channel.Name}' is not a valid .NET version or channel. " +
                $"Use a version like '9.0', '9.0.100', or a channel keyword: {string.Join(", ", ChannelVersionResolver.KnownChannelKeywords)}.",
                version: null, // Don't include user input in telemetry
                component: installRequest.Component.ToString());
        }

        // Map InstallRequest to DotnetInstallObject by converting channel to fully specified version
        ReleaseManifest releaseManifest = new();
        ReleaseVersion? versionToInstall = installRequest.ResolvedVersion
            ?? new ChannelVersionResolver(releaseManifest).Resolve(installRequest);

        if (versionToInstall == null)
        {
            // Channel format was valid, but the version doesn't exist
            throw new DotnetInstallException(
                DotnetInstallErrorCode.VersionNotFound,
                $"Could not find .NET version '{installRequest.Channel.Name}'. The version may not exist or may not be supported.",
                version: null, // Don't include user input in telemetry
                component: installRequest.Component.ToString());
        }

        DotnetInstall install = new(
            installRequest.InstallRoot,
            versionToInstall,
            installRequest.Component);

        string? customManifestPath = installRequest.Options.ManifestPath;
        string componentDescription = installRequest.Component.GetDisplayName();

        // Check if the install already exists and we don't need to do anything
        using (var finalizeLock = ModifyInstallStateMutex())
        {
            var manifestManager = new DotnetupSharedManifest(customManifestPath);
            var manifestData = manifestManager.ReadManifest();

            if (InstallAlreadyExists(manifestData, install))
            {
                // Still record the install spec so the user's requested channel is tracked,
                // even though the version is already installed (possibly via a different channel).
                // Skip manifest write for untracked installs.
                if (!installRequest.Options.Untracked)
                {
                    manifestManager.AddInstallSpec(installRequest.InstallRoot, new InstallSpec
                    {
                        Component = installRequest.Component,
                        VersionOrChannel = installRequest.Channel.Name,
                        InstallSource = installRequest.Options.InstallSource switch
                        {
                            InstallRequestSource.GlobalJson => InstallSource.GlobalJson,
                            _ => InstallSource.Explicit,
                        },
                        GlobalJsonPath = installRequest.Options.GlobalJsonPath
                    });
                }

                Console.WriteLine($"\n{componentDescription} {versionToInstall} is already installed, skipping installation.");
                return new InstallResult(install, WasAlreadyInstalled: true);
            }

            // Guard: error if the target directory contains .NET artifacts but isn't tracked in the manifest.
            // This prevents silently mixing managed and unmanaged installations.
            // Skip this guard for untracked installs.
            if (!installRequest.Options.Untracked
                && !IsRootInManifest(manifestData, installRequest.InstallRoot)
                && HasDotnetArtifacts(installRequest.InstallRoot.Path))
            {
                throw new DotnetInstallException(
                    DotnetInstallErrorCode.Unknown,
                    $"The install path '{installRequest.InstallRoot.Path}' already contains a .NET installation that is not tracked by dotnetup. " +
                    "To avoid conflicts, use a different install path or remove the existing installation first.",
                    version: versionToInstall.ToString(),
                    component: installRequest.Component.ToString());
            }

            // Fail fast: if the muxer must be updated and it is currently locked,
            // throw before the expensive download.  The check is inside the mutex
            // so it does not race with other dotnetup processes.
            if (installRequest.Options.RequireMuxerUpdate && installRequest.InstallRoot.Path is not null)
            {
                MuxerHandler.EnsureMuxerIsWritable(installRequest.InstallRoot.Path);
            }
        }

        IProgressTarget progressTarget = noProgress ? new NonUpdatingProgressTarget() : new SpectreProgressTarget();

        using DotnetArchiveExtractor installer = new(installRequest, versionToInstall, releaseManifest, progressTarget);
        installer.Prepare();

        // Extract and commit the install to the directory
        using (var finalizeLock = ModifyInstallStateMutex())
        {
            var manifestManager = new DotnetupSharedManifest(customManifestPath);
            var manifestData = manifestManager.ReadManifest();

            if (InstallAlreadyExists(manifestData, install))
            {
                return new InstallResult(install, WasAlreadyInstalled: true);
            }

            installer.Commit();

            ArchiveInstallationValidator validator = new();
            if (validator.Validate(install, out string? validationFailure))
            {
                // Skip manifest writes for untracked installs.
                if (!installRequest.Options.Untracked)
                {
                    // Record the install spec for the channel that was requested
                    manifestManager.AddInstallSpec(installRequest.InstallRoot, new InstallSpec
                    {
                        Component = installRequest.Component,
                        VersionOrChannel = installRequest.Channel.Name,
                        InstallSource = installRequest.Options.InstallSource switch
                        {
                            InstallRequestSource.GlobalJson => InstallSource.GlobalJson,
                            _ => InstallSource.Explicit,
                        },
                        GlobalJsonPath = installRequest.Options.GlobalJsonPath
                    });

                    // Record the installation with its resolved version
                    manifestManager.AddInstallation(installRequest.InstallRoot, new Installation
                    {
                        Component = installRequest.Component,
                        Version = versionToInstall.ToString(),
                        Subcomponents = installer.ExtractedSubcomponents.ToList()
                    });
                }
            }
            else
            {
                throw new DotnetInstallException(
                    DotnetInstallErrorCode.Unknown,
                    $"Installation validation failed: {validationFailure}",
                    version: versionToInstall.ToString(),
                    component: installRequest.Component.ToString());
            }
        }

        return new InstallResult(install, WasAlreadyInstalled: false);
    }
#pragma warning restore CA1822

    private static bool InstallAlreadyExists(DotnetupManifestData manifestData, DotnetInstall install)
    {
        var root = manifestData.DotnetRoots.FirstOrDefault(r =>
            string.Equals(Path.GetFullPath(r.Path), Path.GetFullPath(install.InstallRoot.Path!), StringComparison.OrdinalIgnoreCase));
        return root?.Installations.Any(existing =>
            existing.Version == install.Version.ToString() &&
            existing.Component == install.Component) ?? false;
    }

    private static bool IsRootInManifest(DotnetupManifestData manifestData, DotnetInstallRoot installRoot)
    {
        return manifestData.DotnetRoots.Any(root =>
            string.Equals(Path.GetFullPath(root.Path), Path.GetFullPath(installRoot.Path), StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasDotnetArtifacts(string? path)
    {
        if (path is null || !Directory.Exists(path))
        {
            return false;
        }

        // Check for common .NET installation markers
        string dotnetExe = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        return File.Exists(Path.Combine(path, dotnetExe))
            || Directory.Exists(Path.Combine(path, "sdk"))
            || Directory.Exists(Path.Combine(path, "shared"));
    }
}
