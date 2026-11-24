// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

/// <summary>
/// Provisions isolated uninstall manifests and fake component directories, including externally edited state.
/// Never downloads packages or changes the process-wide environment.
/// </summary>
internal sealed class UninstallTestEnvironment : IDisposable
{
    internal TestEnvironment Environment { get; } = new();

    private DotnetInstallRoot Root => new(Environment.InstallPath, InstallArchitecture.x64);

    internal void AddInstallation(InstallComponent component, string version, params string[] extraSubcomponents)
    {
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        Environment.StubComponentDirectories(Root.Path, (component, version));
        var primary = component == InstallComponent.SDK
            ? $"sdk/{version}"
            : $"shared/{component.GetFrameworkName()}/{version}";
        foreach (var path in extraSubcomponents)
        {
            Directory.CreateDirectory(Path.Combine(Root.Path, path));
        }

        new DotnetupSharedManifest(Environment.ManifestPath).AddInstallation(Root, new Installation
        {
            Component = component,
            Version = version,
            Subcomponents = [primary, .. extraSubcomponents]
        });
    }

    internal void AddSpecs(InstallComponent component, params string[] channels)
    {
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var manifest = new DotnetupSharedManifest(Environment.ManifestPath);
        foreach (var channel in channels)
        {
            manifest.AddInstallSpec(Root, new InstallSpec
            {
                Component = component, VersionOrChannel = channel, InstallSource = InstallSource.Explicit
            });
        }
    }

    internal DotnetRootEntry ReadRoot()
    {
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        return new DotnetupSharedManifest(Environment.ManifestPath).ReadManifest().DotnetRoots.Single();
    }

    internal void AlterManifest(Action<DotnetRootEntry> alter)
    {
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var data = new DotnetupSharedManifest(Environment.ManifestPath).ReadManifest();
        alter(data.DotnetRoots.Single());
        File.WriteAllText(Environment.ManifestPath,
            JsonSerializer.Serialize(data, DotnetupManifestJsonContext.Default.DotnetupManifestData));
    }

    public void Dispose() => Environment.Dispose();
}
