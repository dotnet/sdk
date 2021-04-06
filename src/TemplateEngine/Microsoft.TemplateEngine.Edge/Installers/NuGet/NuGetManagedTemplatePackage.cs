// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.TemplateEngine.Edge.Installers.NuGet
{
    internal class NuGetManagedTemplatePackage : IManagedTemplatePackage
    {
        private const string AuthorKey = "Author";
        private const string LocalPackageKey = "LocalPackage";
        private const string NuGetSourceKey = "NuGetSource";
        private const string PackageIdKey = "PackageId";
        private const string PackageVersionKey = "Version";
        private const string DebugLogCategory = "Installer";
        private IEngineEnvironmentSettings _settings;

        public NuGetManagedTemplatePackage(
          IEngineEnvironmentSettings settings,
          IInstaller installer,
          IManagedTemplatePackageProvider provider,
          string mountPointUri,
          string packageIdentifier)
        {
            if (string.IsNullOrWhiteSpace(mountPointUri))
            {
                throw new ArgumentException($"{nameof(mountPointUri)} cannot be null or empty", nameof(mountPointUri));
            }
            if (string.IsNullOrWhiteSpace(packageIdentifier))
            {
                throw new ArgumentException($"{nameof(packageIdentifier)} cannot be null or empty", nameof(packageIdentifier));
            }
            MountPointUri = mountPointUri;
            Installer = installer ?? throw new ArgumentNullException(nameof(installer));
            ManagedProvider = provider ?? throw new ArgumentNullException(nameof(provider));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            Details = new Dictionary<string, string>();
            Details[PackageIdKey] = packageIdentifier;
        }

        /// <summary>
        /// Private constructor used for de-serialization only.
        /// </summary>
        private NuGetManagedTemplatePackage(
            IEngineEnvironmentSettings settings,
            IInstaller installer,
            IManagedTemplatePackageProvider provider,
            string mountPointUri,
            IReadOnlyDictionary<string, string> details)
        {
            if (string.IsNullOrWhiteSpace(mountPointUri))
            {
                throw new ArgumentException($"{nameof(mountPointUri)} cannot be null or empty", nameof(mountPointUri));
            }
            MountPointUri = mountPointUri;
            Installer = installer ?? throw new ArgumentNullException(nameof(installer));
            ManagedProvider = provider ?? throw new ArgumentNullException(nameof(provider));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Details = details?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? throw new ArgumentNullException(nameof(details));
            if (Details.TryGetValue(PackageIdKey, out string packageId))
            {
                if (string.IsNullOrWhiteSpace(packageId))
                {
                    throw new ArgumentException($"{nameof(details)} should contain key {PackageIdKey} with non-empty value", nameof(details));
                }
            }
            else
            {
                throw new ArgumentException($"{nameof(details)} should contain key {PackageIdKey}", nameof(details));
            }
        }

        public string Author
        {
            get
            {
                return Details.TryGetValue(AuthorKey, out string author) ? author : null;
            }
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    Details[AuthorKey] = value;
                }
                else
                {
                    Details.Remove(AuthorKey);
                }
            }
        }
        public string DisplayName => string.IsNullOrWhiteSpace(Version) ? Identifier : $"{Identifier}::{Version}";
        public string Identifier => Details.TryGetValue(PackageIdKey, out string identifier) ? identifier : null;
        public IInstaller Installer { get; }

        public DateTime LastChangeTime
        {
            get
            {
                try
                {
                    return (_settings.Host.FileSystem as IFileLastWriteTimeSource)?.GetLastWriteTimeUtc(MountPointUri) ?? File.GetLastWriteTime(MountPointUri);
                }
                catch (Exception e)
                {
                    _settings.Host.LogDiagnosticMessage($"Failed to get last changed time for {MountPointUri}, details: {e.ToString()}", DebugLogCategory);
                    return default;
                }
            }
        }
        public bool LocalPackage
        {
            get
            {
                if (Details.TryGetValue(LocalPackageKey, out string val) && bool.TryParse(val, out bool isLocalPackage))
                {
                    return isLocalPackage;
                }
                return false;
            }
            set
            {
                if (value)
                {
                    Details[LocalPackageKey] = true.ToString();
                }
                else
                {
                    Details.Remove(LocalPackageKey);
                }
            }
        }
        public string MountPointUri { get; }
        public string NuGetSource
        {
            get
            {
                return Details.TryGetValue(NuGetSourceKey, out string nugetSource) ? nugetSource : null;
            }
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    Details[NuGetSourceKey] = value;
                }
                else
                {
                    Details.Remove(NuGetSourceKey);
                }
            }
        }
        public ITemplatePackageProvider Provider => ManagedProvider;
        public IManagedTemplatePackageProvider ManagedProvider { get; }
        public string Version
        {
            get
            {
                return Details.TryGetValue(PackageVersionKey, out string version) ? version : null;
            }
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    Details[PackageVersionKey] = value;
                }
                else
                {
                    Details.Remove(PackageVersionKey);
                }
            }
        }
        internal Dictionary<string, string> Details { get; }

        public static NuGetManagedTemplatePackage Deserialize(
            IEngineEnvironmentSettings settings,
            IInstaller installer,
            IManagedTemplatePackageProvider provider,
            string mountPointUri,
            IReadOnlyDictionary<string, string> details)
        {
            return new NuGetManagedTemplatePackage(settings, installer, provider, mountPointUri, details);
        }

        public IReadOnlyDictionary<string, string> GetDetails()
        {
            Dictionary<string, string> details = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(Author))
            {
                details[AuthorKey] = Author;
            }
            if (!string.IsNullOrWhiteSpace(NuGetSource))
            {
                details[NuGetSourceKey] = NuGetSource;
            }
            return details;
        }
    }
}
