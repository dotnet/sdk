// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Commands.Workload.Install;

internal interface IWorkloadManifestInstaller
{
    PackageId GetManifestPackageId(ManifestId manifestId, SdkFeatureBand featureBand);

    Task ExtractManifestAsync(string nupkgPath, string targetPath);
}
