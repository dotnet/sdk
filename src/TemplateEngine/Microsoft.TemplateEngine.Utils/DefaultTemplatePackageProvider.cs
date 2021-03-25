// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

#nullable enable

namespace Microsoft.TemplateEngine.Utils
{
    /// <summary>
    /// This is generic provider that can be used by different factories that have
    /// fixed list of ".nupkgs" or "folders". And don't want to re-implement this interface.
    /// </summary>
    public class DefaultTemplatePackageProvider : ITemplatePackageProvider
    {
        private readonly IEngineEnvironmentSettings _environmentSettings;
        private readonly IEnumerable<string> _nupkgs;
        private readonly IEnumerable<string> _folders;

        public ITemplatePackageProviderFactory Factory { get; }

        public DefaultTemplatePackageProvider(ITemplatePackageProviderFactory factory, IEngineEnvironmentSettings environmentSettings, IEnumerable<string>? nupkgs = null, IEnumerable<string>? folders = null)
        {
            Factory = factory;
            _environmentSettings = environmentSettings;
            _nupkgs = nupkgs ?? Array.Empty<string>();
            _folders = folders ?? Array.Empty<string>();
        }

        public event Action? SourcesChanged;

        public void TriggerSourcesChangedEvent()
        {
            SourcesChanged?.Invoke();
        }

        public Task<IReadOnlyList<ITemplatePackage>> GetAllSourcesAsync(CancellationToken cancellationToken)
        {
            var expandedNupkgs = _nupkgs.SelectMany(p => InstallRequestPathResolution.Expand(p, _environmentSettings));
            var expandedFolders = _folders.SelectMany(p => InstallRequestPathResolution.Expand(p, _environmentSettings));

            var list = new List<ITemplatePackage>();
            foreach (var nupkg in expandedNupkgs)
            {
                list.Add(new TemplatePackage(this, nupkg, GetLastWriteTimeUtc(nupkg)));
            }
            foreach (var folder in expandedFolders)
            {
                list.Add(new TemplatePackage(this, folder, GetLastWriteTimeUtc(folder)));
            }
            return Task.FromResult<IReadOnlyList<ITemplatePackage>>(list);
        }

        private DateTime GetLastWriteTimeUtc(string path)
        {
            if (_environmentSettings.Host.FileSystem is IFileLastWriteTimeSource fileSystem)
                return fileSystem.GetLastWriteTimeUtc(path);
            return File.GetLastWriteTimeUtc(path);
        }
    }
}
