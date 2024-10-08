// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Text.Json;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Installer.Windows;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.Win32;
using Microsoft.Win32.Msi;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal partial class NetSdkMsiInstallerClient
    {
        protected List<WorkloadSetRecord> GetWorkloadSetRecords()
        {
            Log?.LogMessage($"Detecting installed workload sets for {HostArchitecture}.");

            var workloadSetRecords = new List<WorkloadSetRecord>();

            using RegistryKey installedManifestsKey = Registry.LocalMachine.OpenSubKey(@$"SOFTWARE\Microsoft\dotnet\InstalledWorkloadSets\{HostArchitecture}");
            if (installedManifestsKey != null)
            {
                foreach (string workloadSetFeatureBand in installedManifestsKey.GetSubKeyNames())
                {
                    using RegistryKey workloadSetFeatureBandKey = installedManifestsKey.OpenSubKey(workloadSetFeatureBand);
                    foreach (string workloadSetPackageVersion in workloadSetFeatureBandKey.GetSubKeyNames())
                    {
                        using RegistryKey workloadSetPackageVersionKey = workloadSetFeatureBandKey.OpenSubKey(workloadSetPackageVersion);

                        string workloadSetVersion = WorkloadSetVersion.FromWorkloadSetPackageVersion(new SdkFeatureBand(workloadSetFeatureBand), workloadSetPackageVersion);

                        WorkloadSetRecord record = new WorkloadSetRecord()
                        {
                            ProviderKeyName = (string)workloadSetPackageVersionKey.GetValue("DependencyProviderKey"),
                            WorkloadSetVersion = workloadSetVersion,
                            WorkloadSetPackageVersion = workloadSetPackageVersion,
                            WorkloadSetFeatureBand = workloadSetFeatureBand,
                            ProductCode = (string)workloadSetPackageVersionKey.GetValue("ProductCode"),
                            ProductVersion = new Version((string)workloadSetPackageVersionKey.GetValue("ProductVersion")),
                            UpgradeCode = (string)workloadSetPackageVersionKey.GetValue("UpgradeCode"),
                        };

                        Log.LogMessage($"Found workload set record, version: {workloadSetVersion}, feature band: {workloadSetFeatureBand}, ProductCode: {record.ProductCode}, provider key: {record.ProviderKeyName}");
                        workloadSetRecords.Add(record);
                    }
                }
            }

            return workloadSetRecords;
        }

        //  Manifest IDs are lowercased on disk, we need to map them back to the original casing to generate the right UpgradeCode
        private static readonly string[] CasedManifestIds =
            [
                "Microsoft.NET.Sdk.Android",
                "Microsoft.NET.Sdk.Aspire",
                "Microsoft.NET.Sdk.iOS",
                "Microsoft.NET.Sdk.MacCatalyst",
                "Microsoft.NET.Sdk.macOS",
                "Microsoft.NET.Sdk.Maui",
                "Microsoft.NET.Sdk.tvOS",
                "Microsoft.NET.Workload.Emscripten.Current",
                "Microsoft.NET.Workload.Emscripten.net6",
                "Microsoft.NET.Workload.Emscripten.net7",
                "Microsoft.NET.Workload.Mono.ToolChain.Current",
                "Microsoft.NET.Workload.Mono.ToolChain.net6",
                "Microsoft.NET.Workload.Mono.ToolChain.net7",
            ];

        private static readonly IReadOnlyDictionary<string, string> ManifestIdCasing = CasedManifestIds.ToDictionary(id => id.ToLowerInvariant()).AsReadOnly();

        protected List<WorkloadManifestRecord> GetWorkloadManifestRecords()
        {
            Log?.LogMessage($"Detecting installed workload manifests for {HostArchitecture}.");

            var manifestRecords = new List<WorkloadManifestRecord>();
            HashSet<(string id, string version)> discoveredManifests = new();

            using RegistryKey installedManifestsKey = Registry.LocalMachine.OpenSubKey(@$"SOFTWARE\Microsoft\dotnet\InstalledManifests\{HostArchitecture}");
            if (installedManifestsKey != null)
            {
                foreach (string manifestPackageId in installedManifestsKey.GetSubKeyNames())
                {
                    const string ManifestSeparator = ".Manifest-";

                    int separatorIndex = manifestPackageId.IndexOf(ManifestSeparator);
                    if (separatorIndex < 0 || manifestPackageId.Length < separatorIndex + ManifestSeparator.Length + 1)
                    {
                        Log.LogMessage($"Found apparent manifest package ID '{manifestPackageId} which did not correctly parse into manifest ID and feature band.");
                        continue;
                    }

                    string manifestId = manifestPackageId.Substring(0, separatorIndex);
                    string manifestFeatureBand = manifestPackageId.Substring(separatorIndex + ManifestSeparator.Length);

                    using RegistryKey manifestKey = installedManifestsKey.OpenSubKey(manifestPackageId);
                    foreach (string manifestVersion in manifestKey.GetSubKeyNames())
                    {
                        using RegistryKey manifestVersionKey = manifestKey.OpenSubKey(manifestVersion);

                        WorkloadManifestRecord record = new WorkloadManifestRecord
                        {
                            ManifestId = manifestId,
                            ManifestVersion = manifestVersion,
                            ManifestFeatureBand = manifestFeatureBand,
                            ProductCode = (string)manifestVersionKey.GetValue("ProductCode"),
                            UpgradeCode = (string)manifestVersionKey.GetValue("UpgradeCode"),
                            ProductVersion = new Version((string)manifestVersionKey.GetValue("ProductVersion")),
                            ProviderKeyName = (string)manifestVersionKey.GetValue("DependencyProviderKey")
                        };

                        Log.LogMessage($"Found workload manifest record, Id: {manifestId}, version: {manifestVersion}, feature band: {manifestFeatureBand}, ProductCode: {record.ProductCode}, provider key: {record.ProviderKeyName}");
                        manifestRecords.Add(record);
                        discoveredManifests.Add((manifestId, manifestVersion));
                    }
                }
            }

            //  Workload manifest MSIs for 8.0.100 don't yet write the same type of installation records to the registry that workload packs do.
            //  So to find what is installed, we look for the manifests on disk, and then map that to installed MSIs
            //  To do the mapping to installed MSIs, we rely on the fact that the MSI UpgradeCode is generated in a stable fashion from the
            //  NuGet package identity and platform: Utils.CreateUuid(UpgradeCodeNamespaceUuid, $"{Package.Identity};{Platform}")
            //  The NuGet package identity used is the vanilla (non-MSI) manifest package, for example Microsoft.NET.Workload.Mono.ToolChain.Current.Manifest-8.0.100 version 8.0.0
            string sdkManifestFolder = Path.Combine(DotNetHome, "sdk-manifests");

            foreach (var manifestFeatureBandFolder in Directory.GetDirectories(sdkManifestFolder))
            {
                if (!ReleaseVersion.TryParse(Path.GetFileName(manifestFeatureBandFolder), out ReleaseVersion releaseVersion))
                {
                    //  Ignore folders which aren't valid version numbers
                    Log.LogMessage($"Skipping invalid feature band version folder: {manifestFeatureBandFolder}");
                    continue;
                }
                if (releaseVersion.Major < 8)
                {
                    //  Ignore manifests prior to 8.0.100, they were not side-by-side
                    continue;
                }
                var manifestFeatureBand = new SdkFeatureBand(releaseVersion);

                foreach (var manifestIDFolder in Directory.GetDirectories(manifestFeatureBandFolder))
                {
                    var lowerCasedManifestID = Path.GetFileName(manifestIDFolder);
                    if (ManifestIdCasing.TryGetValue(lowerCasedManifestID, out string manifestID))
                    {
                        foreach (var manifestVersionFolder in Directory.GetDirectories(manifestIDFolder))
                        {
                            string manifestVersionString = Path.GetFileName(manifestVersionFolder);

                            if (discoveredManifests.Contains((manifestID, manifestVersionString)))
                            {
                                continue;
                            }

                            Log.LogMessage($"Discovered manifest installation in {manifestVersionFolder} which didn't have corresponding installation record in Registry");

                            if (NuGetVersion.TryParse(manifestVersionString, out NuGetVersion manifestVersion))
                            {
                                var packageIdentity = new PackageIdentity(manifestID + ".Manifest-" + manifestFeatureBand, manifestVersion);
                                string uuidName = $"{packageIdentity};{HostArchitecture}";
                                var upgradeCode = '{' + CreateUuid(UpgradeCodeNamespaceUuid, uuidName).ToString() + '}';
                                Log.LogMessage($"Looking for upgrade code {upgradeCode} for {uuidName}");
                                List<string> relatedProductCodes;
                                try
                                {
                                    relatedProductCodes = WindowsInstaller.FindRelatedProducts(upgradeCode.ToString()).ToList();
                                }
                                catch (WindowsInstallerException)
                                {
                                    Console.WriteLine("Error getting related products for " + upgradeCode);
                                    throw;
                                }
                                DependencyProvider dependencyProvider;
                                if (relatedProductCodes.Count == 1 &&
                                    DetectPackage(relatedProductCodes[0], out Version installedVersion) == DetectState.Present &&
                                    (dependencyProvider = DependencyProvider.GetFromProductCode(relatedProductCodes[0])) != null)
                                {
                                    var manifestRecord = new WorkloadManifestRecord();
                                    manifestRecord.ProductCode = relatedProductCodes[0];
                                    manifestRecord.UpgradeCode = upgradeCode;
                                    manifestRecord.ManifestId = manifestID;
                                    manifestRecord.ManifestVersion = manifestVersion.ToString();
                                    manifestRecord.ManifestFeatureBand = manifestFeatureBand.ToString();
                                    manifestRecord.ProductVersion = installedVersion;
                                    manifestRecord.ProviderKeyName = dependencyProvider.ProviderKeyName;

                                    manifestRecords.Add(manifestRecord);

                                    Log.LogMessage($"Found installed manifest: {manifestRecord.ProviderKeyName}, {manifestRecord.ProductCode}");
                                }
                                else if (relatedProductCodes.Count > 1)
                                {
                                    Log.LogMessage($"Found multiple product codes for {uuidName}, which is not expected for side-by-side manifests: {string.Join(' ', relatedProductCodes)}");
                                }
                                else
                                {
                                    Log.LogMessage($"Found manifest on disk for {uuidName}, but did not find installation information in registry.");
                                }

                            }
                            else
                            {
                                Log.LogMessage($"Skipping invalid manifest version for {manifestVersionFolder}");
                            }
                        }
                    }
                    else
                    {
                        Log.LogMessage($"Skipping unknown manifest ID {lowerCasedManifestID}.");
                    }
                }
            }

            return manifestRecords;
        }

        //  From dotnet/arcade: https://github.com/dotnet/arcade/blob/c3f5cbfb2829795294f5c2d9fa5a0522f47e91fb/src/Microsoft.DotNet.Build.Tasks.Workloads/src/Msi/MsiBase.wix.cs#L38
        /// <summary>
        /// The UUID namespace to use for generating an upgrade code.
        /// </summary>
        internal static readonly Guid UpgradeCodeNamespaceUuid = Guid.Parse("C743F81B-B3B5-4E77-9F6D-474EFF3A722C");


        //  From dotnet/arcade: https://github.com/dotnet/arcade/blob/c3f5cbfb2829795294f5c2d9fa5a0522f47e91fb/src/Microsoft.DotNet.Build.Tasks.Workloads/src/Utils.cs#L128
        /// <summary>
        /// Generates a version 3 UUID given a namespace UUID and name. This is based on the algorithm described in
        /// RFC 4122 (https://tools.ietf.org/html/rfc4122), section 4.3.
        /// </summary>
        /// <param name="namespaceUuid">The UUID representing the namespace.</param>
        /// <param name="name">The name for which to generate a UUID within the given namespace.</param>
        /// <returns>A UUID generated using the given namespace UUID and name.</returns>
        public static Guid CreateUuid(Guid namespaceUuid, string name)
        {
            // 1. Convert the name to a canonical sequence of octets (as defined by the standards or conventions of its name space); put the name space ID in network byte order. 
            byte[] namespaceBytes = namespaceUuid.ToByteArray();
            // Octet 0-3
            int timeLow = IPAddress.HostToNetworkOrder(BitConverter.ToInt32(namespaceBytes, 0));
            // Octet 4-5
            short timeMid = IPAddress.HostToNetworkOrder(BitConverter.ToInt16(namespaceBytes, 4));
            // Octet 6-7
            short timeHiVersion = IPAddress.HostToNetworkOrder(BitConverter.ToInt16(namespaceBytes, 6));

            // 2. Compute the hash of the namespace ID concatenated with the name
            byte[] nameBytes = Encoding.Unicode.GetBytes(name);
            byte[] hashBuffer = new byte[namespaceBytes.Length + nameBytes.Length];

            Buffer.BlockCopy(BitConverter.GetBytes(timeLow), 0, hashBuffer, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(timeMid), 0, hashBuffer, 4, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(timeHiVersion), 0, hashBuffer, 6, 2);
            Buffer.BlockCopy(namespaceBytes, 8, hashBuffer, 8, 8);
            Buffer.BlockCopy(nameBytes, 0, hashBuffer, 16, nameBytes.Length);
            byte[] hash;

            using (System.Security.Cryptography.SHA256 sha256 = System.Security.Cryptography.SHA256.Create())
            {
                hash = sha256.ComputeHash(hashBuffer);
            }

            Array.Resize(ref hash, 16);

            // 3. Set octets zero through 3 of the time_low field to octets zero through 3 of the hash. 
            timeLow = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(hash, 0));
            Buffer.BlockCopy(BitConverter.GetBytes(timeLow), 0, hash, 0, 4);

            // 4. Set octets zero and one of the time_mid field to octets 4 and 5 of the hash. 
            timeMid = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(hash, 4));
            Buffer.BlockCopy(BitConverter.GetBytes(timeMid), 0, hash, 4, 2);

            // 5. Set octets zero and one of the time_hi_and_version field to octets 6 and 7 of the hash. 
            timeHiVersion = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(hash, 6));

            // 6. Set the four most significant bits (bits 12 through 15) of the time_hi_and_version field to the appropriate 4-bit version number from Section 4.1.3. 
            timeHiVersion = (short)((timeHiVersion & 0x0fff) | 0x3000);
            Buffer.BlockCopy(BitConverter.GetBytes(timeHiVersion), 0, hash, 6, 2);

            // 7. Set the clock_seq_hi_and_reserved field to octet 8 of the hash. 
            // 8. Set the two most significant bits (bits 6 and 7) of the clock_seq_hi_and_reserved to zero and one, respectively.
            hash[8] = (byte)((hash[8] & 0x3f) | 0x80);

            // Steps 9-11 are essentially no-ops, but provided for completion sake
            // 9. Set the clock_seq_low field to octet 9 of the hash.
            // 10. Set octets zero through five of the node field to octets 10 through 15 of the hash.
            // 11. Convert the resulting UUID to local byte order. 

            return new Guid(hash);
        }

        /// <summary>
        /// Detect installed workload pack records. Only the default registry hive is searched. Finding a workload pack
        /// record does not necessarily guarantee that the MSI is installed.
        /// </summary>
        protected List<WorkloadPackRecord> GetWorkloadPackRecords()
        {
            Log?.LogMessage($"Detecting installed workload packs for {HostArchitecture}.");
            List<WorkloadPackRecord> workloadPackRecords = new();
            using RegistryKey installedPacksKey = Registry.LocalMachine.OpenSubKey(@$"SOFTWARE\Microsoft\dotnet\InstalledPacks\{HostArchitecture}");

            static void SetRecordMsiProperties(WorkloadPackRecord record, RegistryKey key)
            {
                record.ProviderKeyName = (string)key.GetValue("DependencyProviderKey");
                record.ProductCode = (string)key.GetValue("ProductCode");
                record.ProductVersion = new Version((string)key.GetValue("ProductVersion"));
                record.UpgradeCode = (string)key.GetValue("UpgradeCode");
            }

            if (installedPacksKey != null)
            {
                foreach (string packId in installedPacksKey.GetSubKeyNames())
                {
                    using RegistryKey packKey = installedPacksKey.OpenSubKey(packId);

                    foreach (string packVersion in packKey.GetSubKeyNames())
                    {
                        using RegistryKey packVersionKey = packKey.OpenSubKey(packVersion);

                        WorkloadPackRecord record = new WorkloadPackRecord
                        {
                            MsiId = packId,
                            MsiNuGetVersion = packVersion,
                        };

                        SetRecordMsiProperties(record, packVersionKey);

                        record.InstalledPacks.Add((new WorkloadPackId(packId), new NuGetVersion(packVersion)));

                        Log?.LogMessage($"Found workload pack record, Id: {packId}, version: {packVersion}, ProductCode: {record.ProductCode}, provider key: {record.ProviderKeyName}");

                        workloadPackRecords.Add(record);
                    }
                }
            }

            //  Workload pack group installation records are in a similar format as the pack installation records.  They use the "InstalledPackGroups" key,
            //  and under the key for each pack group/version are keys for the workload pack IDs and versions that are in the pack gorup.
            using RegistryKey installedPackGroupsKey = Registry.LocalMachine.OpenSubKey(@$"SOFTWARE\Microsoft\dotnet\InstalledPackGroups\{HostArchitecture}");
            if (installedPackGroupsKey != null)
            {
                foreach (string packGroupId in installedPackGroupsKey.GetSubKeyNames())
                {
                    using RegistryKey packGroupKey = installedPackGroupsKey.OpenSubKey(packGroupId);
                    foreach (string packGroupVersion in packGroupKey.GetSubKeyNames())
                    {
                        using RegistryKey packGroupVersionKey = packGroupKey.OpenSubKey(packGroupVersion);

                        WorkloadPackRecord record = new WorkloadPackRecord
                        {
                            MsiId = packGroupId,
                            MsiNuGetVersion = packGroupVersion
                        };

                        SetRecordMsiProperties(record, packGroupVersionKey);

                        Log?.LogMessage($"Found workload pack group record, Id: {packGroupId}, version: {packGroupVersion}, ProductCode: {record.ProductCode}, provider key: {record.ProviderKeyName}");

                        foreach (string packId in packGroupVersionKey.GetSubKeyNames())
                        {
                            using RegistryKey packIdKey = packGroupVersionKey.OpenSubKey(packId);
                            foreach (string packVersion in packIdKey.GetSubKeyNames())
                            {
                                record.InstalledPacks.Add((new WorkloadPackId(packId), new NuGetVersion(packVersion)));
                                Log?.LogMessage($"Found workload pack in group, Id: {packId}, version: {packVersion}");
                            }
                        }

                        workloadPackRecords.Add(record);
                    }
                }
            }

            return workloadPackRecords;
        }
    }
}
