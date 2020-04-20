using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Microsoft.NET.Sdk.WorkloadResolver
{
    public class MSBuildWorkloadSdkResolver : SdkResolver
    {
        public override string Name => "Microsoft.DotNet.MSBuildWorkloadSdkResolver";

        public override int Priority => 4000;

        WorkloadManifest _manifests;
        public MSBuildWorkloadSdkResolver()
        {
            LoadManifests();
        }

        public override SdkResult Resolve(SdkReference sdkReference, SdkResolverContext resolverContext, SdkResultFactory factory)
        {
            if (sdkReference.Name.Equals("Microsoft.NET.SDK.WorkloadAutoImportPropsLocator", StringComparison.OrdinalIgnoreCase))
            {
                List<string> autoImportSdkPaths = new List<string>();
                foreach (var sdkPack in _manifests.SdkPackVersions)
                {
                    string sdkPackSdkFolder = Path.Combine(GetWorkloadPackPath(sdkPack.Key, sdkPack.Value), "Sdk");
                    string autoImportPath = Path.Combine(sdkPackSdkFolder, "AutoImport.props");
                    if (File.Exists(autoImportPath))
                    {
                        autoImportSdkPaths.Add(sdkPackSdkFolder);
                    }
                }
                return factory.IndicateSuccess(autoImportSdkPaths, sdkReference.Version);
            }
            else if (sdkReference.Name == "TestSdk")
            {
                var propertiesToAdd = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                propertiesToAdd["TestProperty1"] = "AOEU";
                propertiesToAdd["TestProperty2"] = "ASDF";

                Dictionary<string, SdkResultItem> itemsToAdd = new Dictionary<string, SdkResultItem>(StringComparer.OrdinalIgnoreCase);

                itemsToAdd["TestItem1"] = new SdkResultItem("TestItem1Value",
                    new Dictionary<string, string>()
                    { {"a", "b" } });

                itemsToAdd["TestItem2"] = new SdkResultItem("TestItem2Value",
                    new Dictionary<string, string>()
                    { {"c", "d" },
                      {"e", "f" }});

                return factory.IndicateSuccess(Enumerable.Empty<string>(),
                    sdkReference.Version,
                    propertiesToAdd,
                    itemsToAdd);
            }
            else
            {
                if (_manifests.SdkPackVersions.TryGetValue(sdkReference.Name, out string sdkVersion))
                {
                    string workloadPackPath = GetWorkloadPackPath(sdkReference.Name, sdkVersion);
                    if (Directory.Exists(workloadPackPath))
                    {
                        return factory.IndicateSuccess(Path.Combine(workloadPackPath, "Sdk"), sdkReference.Version);
                    }
                    else
                    {
                        var itemsToAdd = new Dictionary<string, SdkResultItem>();
                        itemsToAdd.Add("MissingWorkloadPack",
                            new SdkResultItem(sdkReference.Name,
                                metadata: new Dictionary<string, string>()
                                {
                                    { "Version", sdkVersion }
                                }));

                        Dictionary<string, string> propertiesToAdd = new Dictionary<string, string>();
                        propertiesToAdd["WorkloadMissing"] = "true";
                        return factory.IndicateSuccess(Enumerable.Empty<string>(),
                            sdkReference.Version,
                            propertiesToAdd: propertiesToAdd,
                            itemsToAdd: itemsToAdd);
                    }
                }
            }
            return null;
        }

        private void LoadManifests()
        {
            List<WorkloadManifest> manifests = new List<WorkloadManifest>();

            string workloadManifestRoot = Environment.GetEnvironmentVariable("DOTNETSDK_WORKLOAD_MANIFEST_ROOT");

            if (string.IsNullOrEmpty(workloadManifestRoot))
            {

                string workloadManifestsFolder = @"C:\git\msbuild-sdk-resolver-test\testresolver\workloadmanifests";
                string sdkVersionBand = "5.0.100";

                workloadManifestRoot = Path.Combine(workloadManifestsFolder, sdkVersionBand);
            }

            foreach (var workloadManifestFolder in Directory.GetDirectories(workloadManifestRoot))
            {
                manifests.Add(WorkloadManifest.LoadFromFolder(workloadManifestFolder));
            }

            _manifests = WorkloadManifest.Merge(manifests);
        }

        private string GetWorkloadPackPath(string packId, string packVersion)
        {
            string workloadPackBase = @"C:\git\msbuild-sdk-resolver-test\testresolver\packs";
            return Path.Combine(workloadPackBase, packId, packVersion);
        }
    }
}
