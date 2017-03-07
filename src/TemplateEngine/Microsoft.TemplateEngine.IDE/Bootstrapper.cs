using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.IDE
{
    public class Bootstrapper
    {
        private readonly ITemplateEngineHost _host;
        private readonly Action<IEngineEnvironmentSettings, IInstaller> _onFirstRun;
        private readonly Paths _paths;
        private readonly TemplateCreator _templateCreator;
        private readonly TemplateCache _templateCache;

        private EngineEnvironmentSettings EnvironmentSettings { get; }

        private IInstaller Installer { get; }

        public Bootstrapper(ITemplateEngineHost host, Action<IEngineEnvironmentSettings, IInstaller> onFirstRun, bool virtualizeConfiguration)
        {
            _host = host;
            EnvironmentSettings = new EngineEnvironmentSettings(host, x => new SettingsLoader(x));
            Installer = new Installer(EnvironmentSettings);
            _onFirstRun = onFirstRun;
            _paths = new Paths(EnvironmentSettings);
            _templateCreator = new TemplateCreator(EnvironmentSettings);
            _templateCache = new TemplateCache(EnvironmentSettings);

            if (virtualizeConfiguration)
            {
                EnvironmentSettings.Host.VirtualizeDirectory(_paths.User.BaseDir);
            }
        }

        private void EnsureInitialized()
        {
            if (!_paths.Exists(_paths.User.BaseDir) || !_paths.Exists(_paths.User.FirstRunCookie))
            {
                _onFirstRun?.Invoke(EnvironmentSettings, Installer);
                _paths.WriteAllText(_paths.User.FirstRunCookie, "");
            }
        }

        public void Register(Type type)
        {
            EnsureInitialized();
            EnvironmentSettings.SettingsLoader.Components.Register(type);
        }

        public void Register(Assembly assembly)
        {
            EnsureInitialized();

            foreach (Type type in assembly.GetTypes())
            {
                EnvironmentSettings.SettingsLoader.Components.Register(type);
            }
        }

        public void Install(string path)
        {
            EnsureInitialized();
            Installer.InstallPackages(new[] { path });
        }

        public void Install(params string[] paths)
        {
            EnsureInitialized();
            Installer.InstallPackages(paths);
        }

        public void Install(IEnumerable<string> paths)
        {
            EnsureInitialized();
            Installer.InstallPackages(paths);
        }

        public IReadOnlyCollection<IFilteredTemplateInfo> ListTemplates(bool exactMatchesOnly, params Func<ITemplateInfo, string, MatchInfo?>[] filters)
        {
            EnsureInitialized();
            return _templateCreator.List(exactMatchesOnly, filters);
        }

        public async Task<ICreationResult> CreateAsync(ITemplateInfo info, string name, string outputPath, IReadOnlyDictionary<string, string> parameters, bool skipUpdateCheck)
        {
            TemplateCreationResult instantiateResult = await _templateCreator.InstantiateAsync(info, name, name, outputPath, parameters, skipUpdateCheck, forceCreation: false).ConfigureAwait(false);
            return instantiateResult.ResultInfo;
        }
    }
}
