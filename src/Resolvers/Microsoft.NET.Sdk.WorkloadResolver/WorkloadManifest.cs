using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.NET.Sdk.WorkloadResolver
{
    class WorkloadManifest
    {
        public Dictionary<string, List<string>> Workloads { get; set; } = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> SdkPackVersions { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static WorkloadManifest LoadFromFolder(string manifestFolder)
        {
            string windowsDesktopSdkName = "Microsoft.NET.Sdk.WindowsDesktop2";
            string androidWorkloadSdkName = "Xamarin.Android.Workload";

            var manifestName = Path.GetFileName(manifestFolder);

            if (manifestName.Equals(windowsDesktopSdkName, StringComparison.OrdinalIgnoreCase))
            {
                var manifest = new WorkloadManifest();
                manifest.SdkPackVersions[windowsDesktopSdkName] = "1.0.5";
                manifest.Workloads["WindowsDesktop Workload"] = new List<string>() { windowsDesktopSdkName };
                return manifest;
            }
            else if (manifestName.Equals(androidWorkloadSdkName, StringComparison.OrdinalIgnoreCase))
            {
                var manifest = new WorkloadManifest();
                manifest.SdkPackVersions[androidWorkloadSdkName] = "1.0.1";
                manifest.Workloads["Xamarin.Android Workload"] = new List<string>() { androidWorkloadSdkName };
                return manifest;
            }
            else
            {
                throw new NotImplementedException("Workload manifest loading not implemented");
            }            
        }

        public static WorkloadManifest Merge(IEnumerable<WorkloadManifest> manifests)
        {
            if (!manifests.Any())
            {
                return new WorkloadManifest();
            }
            else if (manifests.Count() == 1)
            {
                return manifests.Single();
            }
            else
            {
                var mergedManifest = new WorkloadManifest();
                foreach (var manifest in manifests)
                {
                    foreach (var workload in manifest.Workloads)
                    {
                        mergedManifest.Workloads.Add(workload.Key, workload.Value);
                    }
                    foreach (var sdkPackVersion in manifest.SdkPackVersions)
                    {
                        mergedManifest.SdkPackVersions.Add(sdkPackVersion.Key, sdkPackVersion.Value);
                    }
                }
                return mergedManifest;
            }
        }
    }
}
