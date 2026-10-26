// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text.Json;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;

namespace Microsoft.DotNet.Cli.Commands.Workload.Install;

internal partial class NetSdkMsiInstallerClient
{
    private class WorkloadPackGroupJson
    {
        public string GroupPackageId { get; set; }
        public string GroupPackageVersion { get; set; }

        public List<WorkloadPackJson> Packs { get; set; } = [];
    }

    private class WorkloadPackJson
    {
        public string PackId { get; set; }

        public string PackVersion { get; set; }
    }

    private Dictionary<(string packId, string packVersion), List<WorkloadPackGroupJson>> GetWorkloadPackGroups()
    {
        Dictionary<(string packId, string packVersion), List<WorkloadPackGroupJson>> ret = [];

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnvironmentVariableNames.WORKLOAD_DISABLE_PACK_GROUPS)))
        {
            //  If workload pack groups are disabled via environment variable, then just return an empty Dictionary
            return ret;
        }

        var manifests = _workloadResolver.GetInstalledManifests();
        foreach (var manifest in manifests)
        {
            var packGroupFile = Path.Combine(manifest.ManifestDirectory, "WorkloadPackGroups.json");
            if (File.Exists(packGroupFile))
            {
                var packGroups = JsonSerializer.Deserialize<IList<WorkloadPackGroupJson>>(File.ReadAllText(packGroupFile));
                foreach (var packGroup in packGroups)
                {
                    foreach (var packJson in packGroup.Packs)
                    {
                        var pack = (packId: packJson.PackId, packVersion: packJson.PackVersion);
                        if (!ret.TryGetValue(pack, out var groupsWithPack))
                        {
                            groupsWithPack = [];
                            ret[pack] = groupsWithPack;
                        }
                        groupsWithPack.Add(packGroup);
                    }
                }
            }
        }

        return ret;
    }

    private List<WorkloadDownload> GetMsisForWorkloads(IEnumerable<WorkloadId> workloads)
    {
        var packs = workloads
            .SelectMany(workloadId => _workloadResolver.GetPacksInWorkload(workloadId))
            .Distinct()
            .Select(packId => _workloadResolver.TryGetPackInfo(packId))
            .Where(pack => pack != null);

        return GetMsisForPacks(packs);
    }

    private List<WorkloadDownload> GetMsisForPacks(IEnumerable<PackInfo> packInfos)
    {
        List<WorkloadDownload> msisToInstall = [];
        HashSet<(string packId, string packVersion)> packsProcessed = [];

        var groupsForPacks = GetWorkloadPackGroups();

        foreach (var pack in packInfos)
        {
            if (packsProcessed.Contains((pack.Id, pack.Version)))
            {
                continue;
            }

            if (groupsForPacks.TryGetValue((pack.Id, pack.Version), out var groups))
            {
                var group = groups.First();
                msisToInstall.Add(new WorkloadDownload(group.GroupPackageId, $"{group.GroupPackageId}.Msi.{HostArchitecture}", group.GroupPackageVersion));
                foreach (var packFromGroup in group.Packs)
                {
                    packsProcessed.Add((packFromGroup.PackId, packFromGroup.PackVersion));
                }
            }
            else
            {
                msisToInstall.Add(GetWorkloadDownloadForPack(pack));
                packsProcessed.Add((pack.Id, pack.Version));
            }
        }

        return msisToInstall;
    }

    private static WorkloadDownload GetWorkloadDownloadForPack(PackInfo packInfo)
    {
        return new WorkloadDownload(packInfo.ResolvedPackageId, GetMsiPackageId(packInfo), packInfo.Version);
    }
}
