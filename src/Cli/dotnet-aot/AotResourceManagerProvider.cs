// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Resources;
using Microsoft.DotNet.Cli.Resources;

namespace Microsoft.DotNet.Cli;

internal static class AotResourceManagerProvider
{
    private static readonly object s_lock = new();
    private static string? s_sdkDirectory;
    private static bool s_configured;

    internal static bool IsConfigured
    {
        get
        {
            lock (s_lock)
            {
                return s_configured;
            }
        }
    }

    internal static bool TryConfigure(string? sdkDirectory, out Exception? error)
    {
        try
        {
            Configure(sdkDirectory);
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = exception;
            return false;
        }
    }

    internal static void Configure(string? sdkDirectory)
    {
        string? fullSdkDirectory = sdkDirectory is null
            ? null
            : Path.GetFullPath(sdkDirectory);

        lock (s_lock)
        {
            if (s_configured)
            {
                if (!string.Equals(s_sdkDirectory, fullSdkDirectory, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"The NativeAOT resource provider is already configured for '{s_sdkDirectory}'.");
                }

                return;
            }

            switch (AotResourceModeConfiguration.Current)
            {
                case AotResourceMode.Embedded:
                    ValidateSdkDirectory(fullSdkDirectory);
                    StringResourceManagerProvider.Register(
                        (baseName, generatedOwnerAssembly) =>
                            CreateEmbeddedManager(
                                baseName,
                                generatedOwnerAssembly,
                                fullSdkDirectory!));
                    break;

                case AotResourceMode.ExternalLocalized:
                    ValidateSdkDirectory(fullSdkDirectory);
                    StringResourceManagerProvider.Register(
                        (baseName, generatedOwnerAssembly) =>
                            CreateExternalLocalizedManager(
                                baseName,
                                generatedOwnerAssembly,
                                fullSdkDirectory!));
                    break;

                case AotResourceMode.ExternalAll:
                    ValidateSdkDirectory(fullSdkDirectory);
                    ValidateExternalNeutralResources(fullSdkDirectory!);
                    StringResourceManagerProvider.Register(
                        (baseName, generatedOwnerAssembly) =>
                            CreateExternalAllManager(
                                baseName,
                                generatedOwnerAssembly,
                                fullSdkDirectory!));
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unknown NativeAOT resource mode '{AotResourceModeConfiguration.Current}'.");
            }

            s_sdkDirectory = fullSdkDirectory;
            s_configured = true;
        }
    }

    private static StringResourceManager CreateEmbeddedManager(
        string baseName,
        Assembly generatedOwnerAssembly,
        string sdkDirectory)
    {
        AotResourceDescriptor descriptor = GetDescriptor(baseName, generatedOwnerAssembly);
        if (string.Equals(descriptor.ManagedAssemblyName, "dotnet", StringComparison.Ordinal)
            || string.Equals(descriptor.ManagedAssemblyName, "System.CommandLine", StringComparison.Ordinal))
        {
            return new EmbeddedStringResourceManager(baseName, generatedOwnerAssembly);
        }

        return SatelliteStringResourceManager.FromSatelliteDirectory(
            baseName,
            sdkDirectory,
            generatedOwnerAssembly,
            descriptor.ManagedAssemblyName,
            SatelliteStringResourceProbeMode.FallbackOnFailure);
    }

    private static StringResourceManager CreateExternalLocalizedManager(
        string baseName,
        Assembly generatedOwnerAssembly,
        string sdkDirectory)
    {
        AotResourceDescriptor descriptor = GetDescriptor(baseName, generatedOwnerAssembly);
        return SatelliteStringResourceManager.FromSatelliteDirectory(
            baseName,
            sdkDirectory,
            generatedOwnerAssembly,
            descriptor.ManagedAssemblyName,
            SatelliteStringResourceProbeMode.FallbackOnFailure);
    }

    private static StringResourceManager CreateExternalAllManager(
        string baseName,
        Assembly generatedOwnerAssembly,
        string sdkDirectory)
    {
        AotResourceDescriptor descriptor = GetDescriptor(baseName, generatedOwnerAssembly);
        string ownerAssemblyPath = Path.Join(
            sdkDirectory,
            descriptor.ManagedAssemblyName + ".dll");

        return SatelliteStringResourceManager.FromAssemblyFiles(
            baseName,
            ownerAssemblyPath,
            sdkDirectory,
            generatedOwnerAssembly,
            descriptor.ManagedAssemblyName,
            SatelliteStringResourceProbeMode.FallbackOnFailure);
    }

    internal static void ValidateExternalNeutralResources(string sdkDirectory)
    {
        ValidateSdkDirectory(sdkDirectory);
        foreach (AotResourceDescriptor descriptor in AotResourceCatalog.All)
        {
            string ownerAssemblyPath = Path.Join(
                sdkDirectory,
                descriptor.ManagedAssemblyName + ".dll");

            StringResourceManager.ValidateAssemblyFile(
                descriptor.BaseName,
                ownerAssemblyPath,
                descriptor.GeneratedOwnerAssembly,
                descriptor.ManagedAssemblyName);
        }
    }

    private static AotResourceDescriptor GetDescriptor(
        string baseName,
        Assembly generatedOwnerAssembly)
    {
        if (!AotResourceCatalog.TryGet(baseName, out AotResourceDescriptor descriptor))
        {
            throw new MissingManifestResourceException(
                $"Resource table '{baseName}' is not present in the NativeAOT external resource catalog.");
        }

        if (!string.Equals(
            descriptor.GeneratedOwnerAssembly.FullName,
            generatedOwnerAssembly.FullName,
            StringComparison.Ordinal))
        {
            throw new FileLoadException(
                $"Resource table '{baseName}' was requested by unexpected assembly '{generatedOwnerAssembly.FullName}'.");
        }

        return descriptor;
    }

    private static void ValidateSdkDirectory(string? sdkDirectory)
    {
        if (string.IsNullOrEmpty(sdkDirectory) || !Directory.Exists(sdkDirectory))
        {
            throw new DirectoryNotFoundException(
                $"The NativeAOT external resource SDK directory '{sdkDirectory}' does not exist.");
        }
    }
}
