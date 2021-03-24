using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    class TemplatePackagesManager : ITemplatePackagesManager
    {
        private readonly IEngineEnvironmentSettings environmentSettings;

        Dictionary<ITemplatePackagesProvider, Task<IReadOnlyList<ITemplatePackage>>> cachedSources;

        public TemplatePackagesManager(IEngineEnvironmentSettings environmentSettings)
        {
            this.environmentSettings = environmentSettings;
        }

        private void EnsureLoaded()
        {
            if (cachedSources != null)
                return;
            cachedSources = new Dictionary<ITemplatePackagesProvider, Task<IReadOnlyList<ITemplatePackage>>>();
            var providers = environmentSettings.SettingsLoader.Components.OfType<ITemplatePackagesProviderFactory>().Select(f => f.CreateProvider(environmentSettings));
            foreach (var provider in providers)
            {
                provider.SourcesChanged += () =>
                {
                    cachedSources[provider] = provider.GetAllSourcesAsync(default);
                    SourcesChanged?.Invoke();
                };
                cachedSources[provider] = Task.Run(() => provider.GetAllSourcesAsync(default));
            }
        }

        public event Action SourcesChanged;

        public IManagedTemplatePackagesProvider GetManagedProvider(string name)
        {
            EnsureLoaded();
            return cachedSources.Keys.OfType<IManagedTemplatePackagesProvider>().FirstOrDefault(p => p.Factory.Name == name);
        }

        public IManagedTemplatePackagesProvider GetManagedProvider(Guid id)
        {
            EnsureLoaded();
            return cachedSources.Keys.OfType<IManagedTemplatePackagesProvider>().FirstOrDefault(p => p.Factory.Id == id);
        }

        public async Task<IReadOnlyList<(IManagedTemplatePackagesProvider Provider, IReadOnlyList<IManagedTemplatePackage> ManagedSources)>> GetManagedSourcesGroupedByProvider(bool force = false)
        {
            EnsureLoaded();
            var sources = await GetManagedTemplatePackages(force).ConfigureAwait(false);
            var list = new List<(IManagedTemplatePackagesProvider Provider, IReadOnlyList<IManagedTemplatePackage> ManagedSources)>();
            foreach (var source in sources.GroupBy(s => s.ManagedProvider))
            {
                list.Add((source.Key, source.ToList()));
            }
            return list;
        }

        public async Task<IReadOnlyList<IManagedTemplatePackage>> GetManagedTemplatePackages(bool force = false)
        {
            EnsureLoaded();
            return (await GetTemplatePackages(force).ConfigureAwait(false)).OfType<IManagedTemplatePackage>().ToList();
        }

        public async Task<IReadOnlyList<ITemplatePackage>> GetTemplatePackages(bool force)
        {
            EnsureLoaded();
            if (force)
            {
                foreach (var provider in cachedSources.Keys)
                {
                    cachedSources[provider] = Task.Run(() => provider.GetAllSourcesAsync(default));
                }
            }

            var sources = new List<ITemplatePackage>();
            foreach (var task in cachedSources.Values)
            {
                sources.AddRange(await task);
            }
            return sources;
        }
    }
}
