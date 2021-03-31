// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.TemplateEngine.Edge.Installers.Folder
{
    internal class FolderManagedTemplatePackage : IManagedTemplatePackage
    {
        private const string DebugLogCategory = "Installer";
        private IEngineEnvironmentSettings _settings;
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
                    return (_settings.Host.FileSystem as IFileLastWriteTimeSource)?.GetLastWriteTimeUtc(MountPointUri) ?? File.GetLastWriteTime(MountPointUri);
                }
                catch (Exception e)
                {
                    _settings.Host.LogDiagnosticMessage($"Failed to get last changed time for {MountPointUri}, details: {e.ToString()}", DebugLogCategory);
                    return default;
                }
            }
        }
        public string MountPointUri { get; }
        public ITemplatePackageProvider Provider => ManagedProvider;
        public IManagedTemplatePackageProvider ManagedProvider { get; }
        public string Version => null;

        private readonly static Dictionary<string, string> _emptyDictionary = new Dictionary<string, string>();
        public IReadOnlyDictionary<string, string> GetDetails() => _emptyDictionary;
    }
}
