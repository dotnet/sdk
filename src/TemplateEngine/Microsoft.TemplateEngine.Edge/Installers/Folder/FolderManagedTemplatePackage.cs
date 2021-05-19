// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.TemplateEngine.Edge.Installers.Folder
{
    internal class FolderManagedTemplatePackage : IManagedTemplatePackage
    {
        private static readonly Dictionary<string, string> _emptyDictionary = new Dictionary<string, string>();
        private readonly IEngineEnvironmentSettings _settings;
        private readonly ILogger _logger;

        public FolderManagedTemplatePackage(IEngineEnvironmentSettings settings, IInstaller installer, IManagedTemplatePackageProvider provider, string mountPointUri)
        {
            if (string.IsNullOrWhiteSpace(mountPointUri))
            {
                throw new ArgumentException($"{nameof(mountPointUri)} cannot be null or empty", nameof(mountPointUri));
            }
            MountPointUri = mountPointUri;
            Installer = installer ?? throw new ArgumentNullException(nameof(installer));
            ManagedProvider = provider ?? throw new ArgumentNullException(nameof(provider));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = settings.Host.LoggerFactory.CreateLogger<FolderManagedTemplatePackage>();
        }

        public string DisplayName => Identifier;

        public string Identifier => MountPointUri;

        public IInstaller Installer { get; }

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
                    _logger.LogDebug($"Failed to get last changed time for {MountPointUri}, details: {e.ToString()}");
                    return default;
                }
            }
        }

        public string MountPointUri { get; }

        public ITemplatePackageProvider Provider => ManagedProvider;

        public IManagedTemplatePackageProvider ManagedProvider { get; }

        public string? Version => null;

        public IReadOnlyDictionary<string, string> GetDetails() => _emptyDictionary;
    }
}
