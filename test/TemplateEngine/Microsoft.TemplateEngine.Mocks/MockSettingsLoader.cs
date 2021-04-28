// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.TemplateEngine.Mocks
{
    public class MockSettingsLoader : ISettingsLoader
    {
        private readonly IEngineEnvironmentSettings _environmentSettings;
        private IComponentManager _components;

        public MockSettingsLoader(IEngineEnvironmentSettings environmentSettings)
        {
            _environmentSettings = environmentSettings;
            _components = new MockComponentManager();
        }

        public IComponentManager Components => _components;

        public IEngineEnvironmentSettings EnvironmentSettings => _environmentSettings;

        public ITemplatePackageManager TemplatePackagesManager => throw new NotImplementedException();

        public void AddProbingPath(string probeIn) => throw new NotImplementedException();

        public IFile FindBestHostTemplateConfigFile(IFileSystemInfo config) => throw new NotImplementedException();

        public Task<IReadOnlyList<ITemplateInfo>> GetTemplatesAsync(CancellationToken token) => throw new NotImplementedException();

        public Task<IReadOnlyList<ITemplateMatchInfo>> GetTemplatesAsync(Func<ITemplateMatchInfo, bool> matchFilter, IEnumerable<Func<ITemplateInfo, MatchInfo>> filters, CancellationToken token = default) => throw new NotImplementedException();

        public ITemplate LoadTemplate(ITemplateInfo info, string baselineName) => throw new NotImplementedException();

        public Task RebuildCacheAsync(CancellationToken token) => throw new NotImplementedException();
        public void ResetHostSettings() => throw new NotImplementedException();
        public void Save() => throw new NotImplementedException();

        public bool TryGetMountPoint(string mountPointUri, out IMountPoint mountPoint) => throw new NotImplementedException();
    }
}
