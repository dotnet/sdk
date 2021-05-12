// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

#nullable enable

namespace Microsoft.TemplateEngine.Utils
{
    /// <summary>
    /// Generic provider that can be used by different factories that have a fixed list of ".nupkgs" or folders.
    /// </summary>
    public class DefaultTemplatePackageProvider : ITemplatePackageProvider
    {
        private readonly IEngineEnvironmentSettings _environmentSettings;
        private IEnumerable<string> _nupkgs;
        private IEnumerable<string> _folders;

        public DefaultTemplatePackageProvider(ITemplatePackageProviderFactory factory, IEngineEnvironmentSettings environmentSettings, IEnumerable<string>? nupkgs = null, IEnumerable<string>? folders = null)
        {
            Factory = factory;
            _environmentSettings = environmentSettings;
            _nupkgs = nupkgs ?? Array.Empty<string>();
            _folders = folders ?? Array.Empty<string>();
        }

        public event Action? TemplatePackagesChanged;

        public ITemplatePackageProviderFactory Factory { get; }

        /// <summary>
        /// Updates list of packages and triggers <see cref="TemplatePackagesChanged"/> event.
        /// </summary>
        /// <param name="nupkgs">List of "*.nupkg" files.</param>
        /// <param name="folders">List of folders.</param>
        public void UpdatePackages(IEnumerable<string>? nupkgs = null, IEnumerable<string>? folders = null)
        {
            _nupkgs = nupkgs ?? Array.Empty<string>();
            _folders = folders ?? Array.Empty<string>();
            TemplatePackagesChanged?.Invoke();
        }

        public Task<IReadOnlyList<ITemplatePackage>> GetAllTemplatePackagesAsync(CancellationToken cancellationToken)
        {
            var expandedNupkgs = _nupkgs.SelectMany(p => InstallRequestPathResolution.ExpandMaskedPath(p, _environmentSettings));
            var expandedFolders = _folders.SelectMany(p => InstallRequestPathResolution.ExpandMaskedPath(p, _environmentSettings));

            var list = new List<ITemplatePackage>();
            foreach (var nupkg in expandedNupkgs)
            {
                list.Add(new TemplatePackage(this, nupkg, _environmentSettings.Host.FileSystem.GetLastWriteTimeUtc(nupkg)));
            }
            foreach (var folder in expandedFolders)
            {
                list.Add(new TemplatePackage(this, folder, _environmentSettings.Host.FileSystem.GetLastWriteTimeUtc(folder)));
            }
            return Task.FromResult<IReadOnlyList<ITemplatePackage>>(list);
        }
    }
}
