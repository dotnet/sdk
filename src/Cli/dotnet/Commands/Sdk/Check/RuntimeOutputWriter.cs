// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.NativeWrapper;

namespace Microsoft.DotNet.Cli.Commands.Sdk.Check;

internal class RuntimeOutputWriter(
    IEnumerable<NetRuntimeInfo> runtimeInfo,
    ProductCollection productCollection,
    IProductCollectionProvider productCollectionProvider,
    IReporter reporter) : BundleOutputWriter(productCollection, productCollectionProvider, reporter)
{
    private readonly IEnumerable<NetRuntimeInfo> _runtimeInfo = runtimeInfo;

    public void PrintRuntimeInfo()
    {
        _reporter.WriteLine(CliCommandStrings.RuntimeSectionHeader);

        var table = new PrintableTable<NetRuntimeInfo>();
        table.AddColumn(CliCommandStrings.NameColumnHeader, runtime => runtime.Name.ToString());
        table.AddColumn(CliCommandStrings.VersionColumnHeader, runtime => runtime.Version.ToString());
        table.AddColumn(CliCommandStrings.StatusColumnHeader, runtime => GetRuntimeStatusMessage(runtime));

        table.PrintRows(_runtimeInfo.OrderBy(sdk => sdk.Version), l => _reporter.WriteLine(l));

        _reporter.WriteLine();
    }

    private string GetRuntimeStatusMessage(NetRuntimeInfo runtime)
    {
        bool? endOfLife = BundleIsEndOfLife(runtime);
        bool? isMaintenance = BundleIsMaintenance(runtime);
        bool? runtimePatchExists = NewerRuntimePatchExists(runtime);
        if (endOfLife == true)
        {
            return string.Format(CliCommandStrings.OutOfSupportMessage, $"{runtime.Version.Major}.{runtime.Version.Minor}");
        }
        else if (isMaintenance == true)
        {
            return string.Format(CliCommandStrings.MaintenanceMessage, $"{runtime.Version.Major}.{runtime.Version.Minor}");
        }
        else if (runtimePatchExists == true)
        {
            return string.Format(CliCommandStrings.NewPatchAvailableMessage, NewestRuntimePatchVersion(runtime));
        }
        else if (endOfLife == false && isMaintenance == false && runtimePatchExists == false)
        {
            return CliCommandStrings.BundleUpToDateMessage;
        }
        else
        {
            return CliCommandStrings.VersionCheckFailure;
        }
    }

    private bool? NewerRuntimePatchExists(NetRuntimeInfo bundle)
    {
        var newestPatchVesion = NewestRuntimePatchVersion(bundle);
        if (newestPatchVesion == null)
        {
            return null;
        }

        return newestPatchVesion > bundle.Version;
    }

    private ReleaseVersion? NewestRuntimePatchVersion(NetRuntimeInfo bundle)
    {
        var product = _productCollection.FirstOrDefault(product => product.ProductVersion.Equals($"{bundle.Version.Major}.{bundle.Version.Minor}"));
        return product?.LatestRuntimeVersion;
    }
}
