// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.TemplateEngine.Edge.Installers.NuGet
{
    internal class NuGetManagedTemplatePackage : IManagedTemplatePackage
    {
        public const string AuthorKey = "Author";
        public const string LocalPackageKey = "LocalPackage";
        public const string NuGetSourceKey = "NuGetSource";
        public const string PackageIdKey = "PackageId";
        public const string PackageVersionKey = "Version";

        private IEngineEnvironmentSettings _settings;

        public NuGetManagedTemplatePackage(IEngineEnvironmentSettings settings, IInstaller installer, string mountPoint, IReadOnlyDictionary<string, string> details)
        {
            Installer = installer;
            MountPointUri = mountPoint;
            Details = details;
            _settings = settings;
        }

        public string Author => Details.TryGetValue(AuthorKey, out string author) ? author : null;
        public string DisplayName => string.IsNullOrWhiteSpace(Version) ? Identifier : $"{Identifier}::{Version}";
        public string Identifier => Details.TryGetValue(PackageIdKey, out string identifier) ? identifier : null;
        public IInstaller Installer { get; }
        public DateTime LastChangeTime => (_settings.Host.FileSystem as IFileLastWriteTimeSource)?.GetLastWriteTimeUtc(MountPointUri) ?? default;
        public bool LocalPackage => Details.TryGetValue(LocalPackageKey, out string isLocalPackage) && bool.TryParse(isLocalPackage, out bool result) ? result : false;
        public string MountPointUri { get; }
        public string NuGetSource => Details.TryGetValue(NuGetSourceKey, out string nugetSource) ? nugetSource : null;
        public ITemplatePackageProvider Provider => Installer.Provider;
        public IManagedTemplatePackageProvider ManagedProvider => Installer.Provider;
        public string Version => Details.TryGetValue(PackageVersionKey, out string version) ? version : null;
        internal IReadOnlyDictionary<string, string> Details { get; }

        public IReadOnlyDictionary<string, string> GetDisplayDetails()
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
