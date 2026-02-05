// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli.Commands.Workload.Install;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.VisualStudio.Setup.Configuration;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;

namespace Microsoft.DotNet.Cli.Commands.Workload.List;

/// <summary>
/// Provides functionality to query the status of .NET workloads in Visual Studio.
/// </summary>
#if NETCOREAPP
[SupportedOSPlatform("windows")]
#endif
internal static class VisualStudioWorkloads
{
    private static readonly object s_guard = new();

    /// <summary>
    /// Visual Studio product ID filters. We dont' want to query SKUs such as Server, TeamExplorer, TestAgent
    /// TestController and BuildTools.
    /// </summary>
    private static readonly string[] s_visualStudioProducts =
    [
        "Microsoft.VisualStudio.Product.Community",
        "Microsoft.VisualStudio.Product.Professional",
        "Microsoft.VisualStudio.Product.Enterprise",
    ];

    /// <summary>
    /// Default prefix to use for Visual Studio component and component group IDs.
    /// </summary>
    private static readonly string s_visualStudioComponentPrefix = "Microsoft.NET.Component";

    /// <summary>
    /// Well-known prefixes used by some workloads that can be replaced when generating component IDs.
    /// </summary>
    private static readonly string[] s_wellKnownWorkloadPrefixes = ["Microsoft.NET.", "Microsoft."];

    /// <summary>
    /// The SWIX package ID wrapping the SDK installer in Visual Studio. The ID should contain
    /// the SDK version as a suffix, e.g., "Microsoft.NetCore.Toolset.5.0.403".
    /// </summary>
    private static readonly string s_visualStudioSdkPackageIdPrefix = "Microsoft.NetCore.Toolset.";

    /// <summary>
    /// Gets a dictionary of mapping possible Visual Studio component IDs to .NET workload IDs in the current SDK.
    /// </summary>
    /// <param name="workloadResolver">The workload resolver used to obtain available workloads.</param>
    /// <returns>A dictionary of Visual Studio component IDs corresponding to workload IDs.</returns>
    internal static Dictionary<string, string> GetAvailableVisualStudioWorkloads(IWorkloadResolver workloadResolver)
    {
        Dictionary<string, string> visualStudioComponentWorkloads = new(StringComparer.OrdinalIgnoreCase);

        // Iterate through all the available workload IDs and generate potential Visual Studio
        // component IDs that map back to the original workload ID. This ensures that we
        // can do reverse lookups for special cases where a workload ID contains a prefix
        // corresponding with the full VS component ID prefix. For example,
        // Microsoft.NET.Component.runtime.android would be a valid component ID for both
        // microsoft-net-runtime-android and runtime-android.
        foreach (var workload in workloadResolver.GetAvailableWorkloads())
        {
            string workloadId = workload.Id.ToString();

            // Old style VS components simply replaced '-' with '.' in the workload ID.
            string componentId = workloadId.Replace('-', '.');

            visualStudioComponentWorkloads.Add(componentId, workloadId);

            // Starting in .NET 9.0 and VS 17.12, workload components will follow the VS naming convention.
            foreach (string wellKnownPrefix in s_wellKnownWorkloadPrefixes)
            {
                if (componentId.StartsWith(wellKnownPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    componentId = componentId.Substring(wellKnownPrefix.Length);
                    break;
                }
            }

            componentId = $"{s_visualStudioComponentPrefix}.{componentId}";
            visualStudioComponentWorkloads.Add(componentId, workloadId);
        }

        return visualStudioComponentWorkloads;
    }

    /// <summary>
    ///  Finds all workloads installed by all Visual Studio instances given that the
    ///  SDK installed by an instance matches the feature band of the currently executing SDK.
    /// </summary>
    /// <param name="workloadResolver">The workload resolver used to obtain available workloads.</param>
    /// <param name="installedWorkloads">The collection of installed workloads to update.</param>
    /// <param name="sdkFeatureBand">
    ///  The feature band of the executing SDK.
    ///  If <see langword="null"/>, then workloads from all feature bands in VS will be returned.
    /// </param>
    internal static unsafe void GetInstalledWorkloads(
        IWorkloadResolver workloadResolver,
        InstalledWorkloadsCollection installedWorkloads,
        SdkFeatureBand? sdkFeatureBand = null)
    {
        if (!ComClassFactory.TryCreate(CLSID.SetupConfiguration, out ComClassFactory? factory, out HRESULT result))
        {
            // Query API not registered, good indication there are no VS installations of 15.0 or later.
            // If we hit any other errors here, assert so we can investigate.
            Debug.Assert(result == HRESULT.REGDB_E_CLASSNOTREG);
            return;
        }

        using (factory)
        {
            using var setupConfiguration = factory.TryCreateInstance<ISetupConfiguration2>(out result);

            GetInstalledWorkloads(
                workloadResolver,
                installedWorkloads,
                setupConfiguration,
                sdkFeatureBand);
        }
    }

    /// <inheritdoc cref="GetInstalledWorkloads(IWorkloadResolver, InstalledWorkloadsCollection, SdkFeatureBand?)"/>
    /// <param name="setupConfiguration">The Visual Studio setup interface.</param>
    internal static unsafe void GetInstalledWorkloads(
        IWorkloadResolver workloadResolver,
        InstalledWorkloadsCollection installedWorkloads,
        ISetupConfiguration2* setupConfiguration,
        SdkFeatureBand? sdkFeatureBand = null)
    {
        Dictionary<string, string> visualStudioWorkloadIds = GetAvailableVisualStudioWorkloads(workloadResolver);
        HashSet<string> installedWorkloadComponents = [];

        // Visual Studio instances contain a large set of packages and we have to perform a linear
        // search to determine whether a matching SDK was installed and look for each installable
        // workload from the SDK. The search is optimized to only scan each set of packages once.

        using ComScope<IEnumSetupInstances> enumInstances = default;
        setupConfiguration->EnumInstances(enumInstances).ThrowOnFailure();

        using ComScope<ISetupInstance> setupInstance = default;
        uint fetched;

        HRESULT result;

        // Enumerate all Visual Studio instances.
        while ((result = enumInstances.Pointer->Next(1, setupInstance, &fetched)) == HRESULT.S_OK)
        {
            using ComScope<ISetupInstance2> setupInstance2 = setupInstance.QueryInterface<ISetupInstance2>();
            setupInstance.Dispose();

            using BSTR versionString = default;
            setupInstance2.Pointer->GetInstallationVersion(&versionString);
            if (!Version.TryParse(versionString, out Version? version) || version.Major < 17)
            {
                continue;
            }

            // Check to see if we have a Visual Studio product we care about.
            // (Notably Community, Professional, Enterprise).
            using ComScope<ISetupPackageReference> product = default;
            setupInstance2.Pointer->GetProduct(product).ThrowOnFailure();
            using BSTR productId = default;
            product.Pointer->GetId(&productId).ThrowOnFailure();

            bool found = false;
            for (int i = 0; i < s_visualStudioProducts.Length; i++)
            {
                if (productId.AsSpan().SequenceEqual(s_visualStudioProducts[i]))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                // Not a Visual Studio product we care about.
                continue;
            }

            // Now walk through all packages to find installed workloads and see if the SDK is installed.

            installedWorkloadComponents.Clear();

            using ComSafeArrayScope<ISetupPackageReference> packages = default;
            setupInstance2.Pointer->GetPackages(packages).ThrowOnFailure();

            bool hasMatchingSdk = false;

            for (int i = 0; i < packages.Length; i++)
            {
                using ComScope<ISetupPackageReference> package = packages[i];
                using BSTR packageId = default;
                package.Pointer->GetId(&packageId).ThrowOnFailure();

                if (packageId.IsNull || packageId.Length == 0)
                {
                    // Visual Studio already verifies the setup catalog at build time. If the package ID is empty
                    // the catalog is likely corrupted.
                    continue;
                }

                // Check if the package owning SDK is installed via VS. Note: if a user checks to add a workload in VS
                // but does not install the SDK, this will cause those workloads to be ignored.
                ReadOnlySpan<char> packageIdSpan = packageId.AsSpan();
                if (packageIdSpan.StartsWith(s_visualStudioSdkPackageIdPrefix))
                {
                    // After trimming the package prefix we should be left with a valid semantic version. If we can't
                    // parse the version we'll skip this instance.
                    ReadOnlySpan<char> versionSpan = packageIdSpan[s_visualStudioSdkPackageIdPrefix.Length..];
                    if (versionSpan.IsEmpty
                        || !ReleaseVersion.TryParse(versionSpan.ToString(), out ReleaseVersion visualStudioSdkVersion))
                    {
                        break;
                    }

                    // The feature band of the SDK in VS must match that of the SDK on which we're running.
                    if (sdkFeatureBand is not null && !sdkFeatureBand.Equals(new SdkFeatureBand(visualStudioSdkVersion)))
                    {
                        break;
                    }

                    hasMatchingSdk = true;
                    continue;
                }

                if (visualStudioWorkloadIds.TryGetAlternateLookup<ReadOnlySpan<char>>(out var altLookup)
                    && altLookup.TryGetValue(packageId, out string? workloadId))
                {
                    installedWorkloadComponents.Add(workloadId);
                }
            }

            if (hasMatchingSdk)
            {
                foreach (string id in installedWorkloadComponents)
                {
                    installedWorkloads.Add(id, $"VS {versionString}");
                }
            }
        }
    }

    /// <summary>
    /// Writes install records for VS Workloads so we later install the packs via the CLI for workloads managed by VS.
    /// This is to fix a bug where updating the manifests in the CLI will cause VS to also be told to use these newer workloads via the workload resolver.
    /// ...  but these workloads don't have their corresponding packs installed as VS doesn't update its workloads as the CLI does.
    /// </summary>
    /// <returns>Updated list of workloads including any that may have had new install records written</returns>
    internal static IEnumerable<WorkloadId> WriteSDKInstallRecordsForVSWorkloads(IInstaller workloadInstaller, IWorkloadResolver workloadResolver,
        IEnumerable<WorkloadId> workloadsWithExistingInstallRecords, IReporter reporter)
    {
        // Do this check to avoid adding an unused & unnecessary method to FileBasedInstallers
        if (OperatingSystem.IsWindows() && workloadInstaller is NetSdkMsiInstallerClient client)
        {
            InstalledWorkloadsCollection vsWorkloads = new();
            GetInstalledWorkloads(workloadResolver, vsWorkloads);

            // Remove VS workloads with an SDK installation record, as we've already created the records for them, and don't need to again.
            var vsWorkloadsAsWorkloadIds = vsWorkloads.AsEnumerable().Select(w => new WorkloadId(w.Key));
            var workloadsToWriteRecordsFor = vsWorkloadsAsWorkloadIds.Except(workloadsWithExistingInstallRecords);

            if (workloadsToWriteRecordsFor.Any())
            {
                reporter.WriteLine(
                    string.Format(CliCommandStrings.WriteCLIRecordForVisualStudioWorkloadMessage,
                    string.Join(", ", workloadsToWriteRecordsFor.Select(w => w.ToString()).ToArray()))
                );

                client.WriteWorkloadInstallRecords(workloadsToWriteRecordsFor);

                return [.. workloadsWithExistingInstallRecords, .. workloadsToWriteRecordsFor];
            }
        }

        return workloadsWithExistingInstallRecords;
    }
}
