// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.DotNet.Installer.Windows.Security;
using Microsoft.Win32.Msi;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Installer.Windows
{
    /// <summary>
    /// Manages caching workload pack MSI packages.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal class MsiPackageCache : InstallerBase
    {
        /// <summary>
        /// The root directory of the package cache where MSI workload packs are stored.
        /// </summary>
        public readonly string PackageCacheRoot;

        public MsiPackageCache(InstallElevationContextBase elevationContext, ISetupLogger logger,
            bool verifySignatures, string packageCacheRoot = null) : base(elevationContext, logger, verifySignatures)
        {
            PackageCacheRoot = string.IsNullOrWhiteSpace(packageCacheRoot)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "dotnet", "workloads")
                : packageCacheRoot;
        }

        /// <summary>
        /// Moves the MSI payload described by the manifest file to the cache.
        /// </summary>
        /// <param name="packageId">The ID of the workload pack package containing an MSI.</param>
        /// <param name="packageVersion">The package version.</param>
        /// <param name="manifestPath">The JSON manifest associated with the workload pack MSI.</param>
        public void CachePayload(string packageId, string packageVersion, string manifestPath)
        {
            if (!File.Exists(manifestPath))
            {
                throw new FileNotFoundException($"CachePayload: Manifest file not found: {manifestPath}");
            }

            Elevate();

            if (IsElevated)
            {
                string packageDirectory = GetPackageDirectory(packageId, packageVersion);

                // Delete the package directory and create a new one that's secure. If all the files were properly
                // cached, the client would not request this action.
                if (Directory.Exists(packageDirectory))
                {
                    Directory.Delete(packageDirectory, recursive: true);
                }

                SecurityUtils.CreateSecureDirectory(packageDirectory);

                // We cannot assume that the MSI adjacent to the manifest is the one to cache. We'll trust
                // the manifest to provide the MSI filename.
                MsiManifest msiManifest = JsonConvert.DeserializeObject<MsiManifest>(File.ReadAllText(manifestPath));
                // Only use the filename+extension of the payload property in case the manifest has been altered.
                string msiPath = Path.Combine(Path.GetDirectoryName(manifestPath), Path.GetFileName(msiManifest.Payload));

                string cachedMsiPath = Path.Combine(packageDirectory, Path.GetFileName(msiPath));
                string cachedManifestPath = Path.Combine(packageDirectory, Path.GetFileName(manifestPath));

                SecurityUtils.MoveAndSecureFile(manifestPath, cachedManifestPath, Log);
                SecurityUtils.MoveAndSecureFile(msiPath, cachedMsiPath, Log);
            }
            else if (IsClient)
            {
                Dispatcher.SendCacheRequest(InstallRequestType.CachePayload, manifestPath, packageId, packageVersion);
            }
        }

        /// <summary>
        /// Gets the full path of the cache directory for the specified package ID and version.
        /// </summary>
        /// <param name="packageId">The ID of the MSI workload pack package.</param>
        /// <param name="packageVersion">The version of the MSI workload pack package.</param>
        /// <returns>The directory where the MSI package will be cached.</returns>
        public string GetPackageDirectory(string packageId, string packageVersion)
        {
            return Path.Combine(PackageCacheRoot, packageId, packageVersion);
        }

        /// <summary>
        /// Determines if the workload pack MSI is cached and tries to retrieve its payload from the cache.
        /// </summary>
        /// <param name="packageId">The package ID of NuGet package carrying the MSI payload.</param>
        /// <param name="packageVersion">The version of the package.</param>
        /// <param name="payload">Contains the payload if the method returns <see langword="true"/>; otherwise the default value of <see cref="MsiPayload"/>.</param>
        /// <returns><see langwork="true"/> if the MSI is cached; <see langword="false"/> otherwise.</returns>
        public bool TryGetPayloadFromCache(string packageId, string packageVersion, out MsiPayload payload)
        {
            string packageCacheDirectory = GetPackageDirectory(packageId, packageVersion);
            payload = default;

            if (!TryGetMsiPathFromPackageData(packageCacheDirectory, out string msiPath, out string manifestPath))
            {
                return false;
            }

            VerifyPackageSignature(msiPath);

            payload = new MsiPayload(manifestPath, msiPath);

            return true;
        }

        public bool TryGetMsiPathFromPackageData(string packageDataPath, out string msiPath, out string manifestPath)
        {
            msiPath = default;
            manifestPath = Path.Combine(packageDataPath, "msi.json");

            // It's possible that the MSI is cached, but without the JSON manifest we cannot
            // trust that the MSI in the cache directory is the correct file.
            if (!File.Exists(manifestPath))
            {
                Log?.LogMessage($"MSI manifest file does not exist, '{manifestPath}'");
                return false;
            }

            // The msi.json manifest contains the name of the actual MSI. The filename does not necessarily match the package
            // ID as it may have been shortened to support VS caching.
            MsiManifest msiManifest = JsonConvert.DeserializeObject<MsiManifest>(File.ReadAllText(manifestPath));
            string possibleMsiPath = Path.Combine(Path.GetDirectoryName(manifestPath), msiManifest.Payload);

            if (!File.Exists(possibleMsiPath))
            {
                Log?.LogMessage($"MSI package not found, '{possibleMsiPath}'");
                return false;
            }

            msiPath = possibleMsiPath;
            return true;
        }

        /// <summary>
        /// Verifies the AuthentiCode signature of an MSI package if the executing command itself is running
        /// from a signed module.
        /// </summary>
        /// <param name="msiPath">The path of the MSI to verify.</param>
        private void VerifyPackageSignature(string msiPath)
        {
            if (VerifySignatures)
            {
                bool isAuthentiCodeSigned = AuthentiCode.IsSigned(msiPath);

                // Need to capture the error now as other OS calls might change the last error.
                uint lastError = !isAuthentiCodeSigned ? unchecked((uint)Marshal.GetLastWin32Error()) : Error.SUCCESS;

                bool isTrustedOrganization = AuthentiCode.IsSignedByTrustedOrganization(msiPath, AuthentiCode.TrustedOrganizations);

                if (isAuthentiCodeSigned && isTrustedOrganization)
                {
                    Log?.LogMessage($"Successfully verified AuthentiCode signature for {msiPath}.");
                }
                else
                {
                    // Summarize the failure and then report additional details.
                    Log?.LogMessage($"Failed to verify signature for {msiPath}. AuthentiCode signed: {isAuthentiCodeSigned}, Trusted organization: {isTrustedOrganization}.");
                    IEnumerable<X509Certificate2> certificates = AuthentiCode.GetCertificates(msiPath);

                    // Dump all the certificates if there are any.
                    if (certificates.Any())
                    {
                        Log?.LogMessage($"Certificate(s):");

                        foreach (X509Certificate2 certificate in certificates)
                        {
                            Log?.LogMessage($"       Subject={certificate.Subject}");
                            Log?.LogMessage($"        Issuer={certificate.Issuer}");
                            Log?.LogMessage($"    Not before={certificate.NotBefore}");
                            Log?.LogMessage($"     Not after={certificate.NotAfter}");
                            Log?.LogMessage($"    Thumbprint={certificate.Thumbprint}");
                            Log?.LogMessage($"     Algorithm={certificate.SignatureAlgorithm.FriendlyName}");
                        }
                    }

                    if (!isAuthentiCodeSigned)
                    {
                        // If it was a WinTrust failure, we can exit using that error code and include a proper message from the OS.
                        ExitOnError(lastError, $"Failed to verify authenticode signature for {msiPath}.");
                    }

                    if (!isTrustedOrganization)
                    {
                        throw new SecurityException(string.Format(LocalizableStrings.AuthentiCodeNoTrustedOrg, msiPath));
                    }
                }
            }
            else
            {
                Log?.LogMessage($"Skipping signature verification for {msiPath}.");
            }
        }
    }
}
