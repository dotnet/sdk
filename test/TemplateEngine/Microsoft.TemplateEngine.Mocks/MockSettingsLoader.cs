using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.GlobalSettings;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.TemplatePackages;

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

        public IGlobalSettings GlobalSettings => throw new NotImplementedException();

        public ITemplatePackagesManager TemplatePackagesManager => throw new NotImplementedException();

        public void AddProbingPath(string probeIn)
        {
            throw new NotImplementedException();
        }

        public IFile FindBestHostTemplateConfigFile(IFileSystemInfo config)
        {
            throw new NotImplementedException();
        }

        public void GetTemplates(HashSet<ITemplateInfo> templates)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyList<ITemplateInfo>> GetTemplatesAsync(CancellationToken token) => throw new NotImplementedException();

        public ITemplate LoadTemplate(ITemplateInfo info, string baselineName)
        {
            throw new NotImplementedException();
        }

        public Task RebuildCacheFromSettingsIfNotCurrent(bool forceRebuild)
        {
            throw new NotImplementedException();
        }

        public void Save()
        {
            throw new NotImplementedException();
        }

        public bool TryGetFileFromIdAndPath(string mountPointUri, string filePathInsideMount, out IFile file, out IMountPoint mountPoint)
        {
            throw new NotImplementedException();
        }

        public bool TryGetMountPoint(string mountPointUri, out IMountPoint mountPoint)
        {
            throw new NotImplementedException();
        }

        public void WriteTemplateCache(IList<ITemplateInfo> templates, string locale)
        {
            throw new NotImplementedException();
        }

        public void WriteTemplateCache(IList<ITemplateInfo> templates, string locale, bool hasContentChanges)
        {
            throw new NotImplementedException();
        }
    }
}
