// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal interface IWorkloadManifestUpdater
    {
        Task UpdateAdvertisingManifestsAsync(SdkFeatureBand featureBand);

        IEnumerable<(ManifestId manifestId, ManifestVersion existingVersion, ManifestVersion newVersion)> CalculateManifestUpdates(SdkFeatureBand featureBand);
    }
}
