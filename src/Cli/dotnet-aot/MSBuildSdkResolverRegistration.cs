// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.NET.Sdk.WorkloadMSBuildSdkResolver;
using NuGetSdkResolver = Microsoft.Build.NuGetSdkResolver.NuGetSdkResolver;

namespace Microsoft.DotNet.Cli;

/// <summary>
/// Registers the SDK resolvers needed by in-process MSBuild evaluations in the Native AOT CLI.
/// </summary>
/// <remarks>
/// Managed MSBuild normally discovers resolver assemblies dynamically from the versioned SDK layout.
/// Native AOT disables that reflection-based loading, so the resolver types linked into the native
/// image must instead be registered through MSBuild's static registration API. Registration happens
/// before the first evaluation because MSBuild snapshots the available resolvers when SDK resolution
/// is initialized.
/// </remarks>
internal static class MSBuildSdkResolverRegistration
{
    private static readonly object s_registrationLock = new();
    private static bool s_registered;

    internal static void Register()
    {
        lock (s_registrationLock)
        {
            if (s_registered)
            {
                return;
            }

            // Resolves SDKs supplied by installed workload manifests.
            SdkResolver.Register(new WorkloadSdkResolver());

            // Resolves versioned, package-based SDK references through NuGet.
            SdkResolver.Register(new NuGetSdkResolver());
            s_registered = true;
        }
    }
}
