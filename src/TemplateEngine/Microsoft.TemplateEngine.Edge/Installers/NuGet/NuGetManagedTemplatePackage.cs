// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Edge.Installers.NuGet
{
    internal class NuGetManagedTemplatePackage : IManagedTemplatePackage
    {
        private const string AuthorKey = "Author";
        private const string LocalPackageKey = "LocalPackage";
        private const string OwnersKey = "Owners";
        private const string ReservedKey = "Reserved";
        private const string NuGetSourceKey = "NuGetSource";
        private const string PackageIdKey = "PackageId";
        private const string PackageVersionKey = "Version";

        private readonly IEngineEnvironmentSettings _settings;
        private readonly ILogger _logger;

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
            _logger = settings.Host.LoggerFactory.CreateLogger<NuGetInstaller>();

            Details = new Dictionary<string, string>
            {
                [PackageIdKey] = packageIdentifier
            };
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
            _logger = settings.Host.LoggerFactory.CreateLogger<NuGetInstaller>();
        }

        public ITemplatePackageProvider Provider => ManagedProvider;

        public IManagedTemplatePackageProvider ManagedProvider { get; }

        public string DisplayName => string.IsNullOrWhiteSpace(Version) ? Identifier : $"{Identifier}::{Version}";

        public string Identifier => Details[PackageIdKey];

        public IInstaller Installer { get; }

        public string MountPointUri { get; }

        public DateTime LastChangeTime
        {
            get
            {
                try
                {
                    return _settings.Host.FileSystem.GetLastWriteTimeUtc(MountPointUri);
                }
                catch (Exception e)
                {
                    _logger.LogDebug($"Failed to get last changed time for {MountPointUri}, details: {e}");
                    return default;
                }
            }
        }

        public string? Reserved
        {
            get => Details.TryGetValue(ReservedKey, out string reserved) ? reserved : false.ToString();
            set => UpdateOrRemoveValue(Details, ReservedKey, value, (entry) => !string.IsNullOrEmpty(entry));
        }

        public string? Author
        {
            get => Details.TryGetValue(AuthorKey, out string author) ? author : null;
            set => UpdateOrRemoveValue(Details, AuthorKey, value, (entry) => !string.IsNullOrEmpty(entry));
        }

        public string? Owners
        {
            get => Details.TryGetValue(OwnersKey, out string owners) ? owners : null;
            set => UpdateOrRemoveValue(Details, OwnersKey, value, (entry) => !string.IsNullOrEmpty(entry));
        }

        public bool IsLocalPackage
        {
            get
            {
                if (Details.TryGetValue(LocalPackageKey, out string val) && bool.TryParse(val, out bool isLocalPackage))
                {
                    return isLocalPackage;
                }
                return false;
            }
            set => UpdateOrRemoveValue(Details, LocalPackageKey, value.ToString(), (value) => value == true.ToString());
        }

        public string? NuGetSource
        {
            get => Details.TryGetValue(NuGetSourceKey, out string nugetSource) ? nugetSource : null;
            set => UpdateOrRemoveValue(Details, NuGetSourceKey, value, (entry) => !string.IsNullOrEmpty(entry));
        }

        public string? Version
        {
            get => Details.TryGetValue(PackageVersionKey, out string version) ? version : null;
            set => UpdateOrRemoveValue(Details, PackageVersionKey, value, (entry) => !string.IsNullOrEmpty(entry));
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
            var details = new Dictionary<string, string>();

            details.TryAdd(AuthorKey, Author ?? string.Empty, (entry) => !string.IsNullOrEmpty(entry));
            details.TryAdd(OwnersKey, Owners ?? string.Empty, (entry) => !string.IsNullOrEmpty(entry));
            details.TryAdd(ReservedKey, Reserved ?? string.Empty, (entry) => !string.IsNullOrEmpty(entry));
            details.TryAdd(NuGetSourceKey, NuGetSource ?? string.Empty, (entry) => !string.IsNullOrEmpty(entry));

            return details;
        }

        private void UpdateOrRemoveValue<TKey, TValue>(IDictionary<TKey, TValue> dict, TKey key, TValue? value, Predicate<TValue> condition)
        {
            if (value is null || !dict.TryAdd(key, value, condition))
            {
                dict.Remove(key);
            }
        }
    }
}
