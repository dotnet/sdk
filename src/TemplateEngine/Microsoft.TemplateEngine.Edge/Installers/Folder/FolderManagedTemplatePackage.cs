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
        public FolderManagedTemplatePackage(IEngineEnvironmentSettings settings, IInstaller installer, string mountPointUri)
        {
            MountPointUri = mountPointUri;
            Installer = installer;
            LastChangeTime = (settings.Host.FileSystem as IFileLastWriteTimeSource)?.GetLastWriteTimeUtc(mountPointUri) ?? File.GetLastWriteTime(mountPointUri);
        }

        public string DisplayName => Identifier;
        public string Identifier => MountPointUri;
        public IInstaller Installer { get; }
        public DateTime LastChangeTime { get; }
        public string MountPointUri { get; }
        public ITemplatePackageProvider Provider => Installer.Provider;
        public IManagedTemplatePackageProvider ManagedProvider => Installer.Provider;
        public string Version => null;

        public IReadOnlyDictionary<string, string> GetDetails() => new Dictionary<string, string>();
    }
}
