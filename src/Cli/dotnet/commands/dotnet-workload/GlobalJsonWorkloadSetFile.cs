// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Workloads.Workload
{
    internal class GlobalJsonWorkloadSetsFile
    {
        string _path;

        public GlobalJsonWorkloadSetsFile(SdkFeatureBand sdkFeatureBand, string dotnetDir)
        {
            _path = Path.Combine(WorkloadInstallType.GetInstallStateFolder(sdkFeatureBand, dotnetDir), "default.json");
        }

        public void RecordWorkloadSetInGlobalJson(string globalJsonPath, string workloadSetVersion)
        {
            using (var accessor = GetAccessor())
            {
                accessor.GlobalJsonWorkloadSetVersions[globalJsonPath] = workloadSetVersion;
            }
        }

        public Accessor GetAccessor()
        {
            throw new NotImplementedException();
        }

        public class Accessor : IDisposable
        {
            //  Key is path to global.json file, value is workload set version
            public Dictionary<string, string> GlobalJsonWorkloadSetVersions { get; set; }

            public void Dispose() => throw new NotImplementedException();
        }
    }
}
