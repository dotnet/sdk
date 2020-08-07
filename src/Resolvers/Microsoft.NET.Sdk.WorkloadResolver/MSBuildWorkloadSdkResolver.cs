using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

#if NET
using Microsoft.DotNet.Cli;
#else
using Microsoft.DotNet.MSBuildSdkResolver;
#endif

namespace Microsoft.NET.Sdk.WorkloadResolver
{
    public class MSBuildWorkloadSdkResolver : SdkResolver
    {
        public override string Name => "Microsoft.DotNet.MSBuildWorkloadSdkResolver";

        public override int Priority => 4000;

#if NETFRAMEWORK
        private readonly NETCoreSdkResolver _sdkResolver;
#endif

        public MSBuildWorkloadSdkResolver()
        {
#if NETFRAMEWORK
            _sdkResolver = new NETCoreSdkResolver();
#endif
        }

        public override SdkResult Resolve(SdkReference sdkReference, SdkResolverContext resolverContext, SdkResultFactory factory)
        {
            var manifests = GetWorkloadManifests(resolverContext);

            if (sdkReference.Name.Equals("Microsoft.NET.SDK.WorkloadAutoImportPropsLocator", StringComparison.OrdinalIgnoreCase))
            {
                List<string> autoImportSdkPaths = new List<string>();
                foreach (var sdkPack in manifests.SdkPackVersions)
                {
                    string sdkPackSdkFolder = Path.Combine(GetWorkloadPackPath(resolverContext, sdkPack.Key, sdkPack.Value), "Sdk");
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
                if (manifests.SdkPackVersions.TryGetValue(sdkReference.Name, out string sdkVersion))
                {
                    string workloadPackPath = GetWorkloadPackPath(resolverContext, sdkReference.Name, sdkVersion);
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
                        return factory.IndicateSuccess(Enumerable.Empty<string>(),
                            sdkReference.Version,
                            propertiesToAdd: propertiesToAdd,
                            itemsToAdd: itemsToAdd);
                    }
                }
            }
            return null;
        }

        private WorkloadManifest GetWorkloadManifests(SdkResolverContext context)
        {
            //  TODO: Caching
            List<WorkloadManifest> manifests = new List<WorkloadManifest>();

            string workloadManifestRoot = Environment.GetEnvironmentVariable("DOTNETSDK_WORKLOAD_MANIFEST_ROOT");

            if (string.IsNullOrEmpty(workloadManifestRoot))
            {
                workloadManifestRoot = GetWorkloadManifestRoot(context);
            }

            foreach (var workloadManifestFolder in Directory.GetDirectories(workloadManifestRoot))
            {
                manifests.Add(WorkloadManifest.LoadFromFolder(workloadManifestFolder));
            }

            return WorkloadManifest.Merge(manifests);
        }

        private string GetSdkDirectory(SdkResolverContext context)
        {
#if NET
            var sdkDirectory = Path.GetDirectoryName(typeof(DotnetFiles).Assembly.Location);
            return sdkDirectory;

#else
            //  TODO: Implement for .NET Framework
            string dotnetExeDir = _sdkResolver.GetDotnetExeDirectory();
            string globalJsonStartDir = Path.GetDirectoryName(context.SolutionFilePath ?? context.ProjectFilePath);
            var sdkResolutionResult = _sdkResolver.ResolveNETCoreSdkDirectory(globalJsonStartDir, context.MSBuildVersion, context.IsRunningInVisualStudio, dotnetExeDir);

            return sdkResolutionResult.ResolvedSdkDirectory;
#endif

        }

        private string GetDotNetRoot(SdkResolverContext context)
        {
            var sdkDirectory = GetSdkDirectory(context);
            var dotnetRoot = Directory.GetParent(sdkDirectory).Parent.FullName;
            return dotnetRoot;
        }

        private string GetWorkloadPackPath(SdkResolverContext context, string packId, string packVersion)
        {
            var dotnetRoot = GetDotNetRoot(context);
            return Path.Combine(dotnetRoot, "packs", packId, packVersion);
        }

        private static readonly char[] dashOrPlus = new[] { '-', '+' };

        private string GetWorkloadManifestRoot(SdkResolverContext context)
        {
            var dotnetRoot = GetDotNetRoot(context);

            string versionBand = GetSdkVersionBand(context);

            return Path.Combine(dotnetRoot, "workloadmanifests", versionBand);
        }

        private string GetSdkVersionBand(SdkResolverContext context)
        {
            var sdkDirectory = GetSdkDirectory(context);
            //  TODO: Is the directory name OK, or should we read the .version file (we don't want to use the Cli.Utils library to read it on .NET Framework)
            var sdkVersion = Path.GetFileName(sdkDirectory);

            int indexOfDashOrPlus = sdkVersion.IndexOfAny(dashOrPlus);
            if (indexOfDashOrPlus >= 0)
            {
                sdkVersion = sdkVersion.Substring(0, indexOfDashOrPlus);
            }

            //  TODO: Add logging for what versions it's looking for

            var version = Version.Parse(sdkVersion);
            var versionBand = new Version(version.Major, version.Minor, (version.Build / 100) * 100);

            return versionBand.ToString();
        }
    }
}
