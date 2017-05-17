// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
            EnvironmentSettings.SettingsLoader.Components.Register(type);
        }

        public void Register(Assembly assembly)
        {
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

        public IReadOnlyCollection<IFilteredTemplateInfo> ListTemplates(bool exactMatchesOnly, params Func<ITemplateInfo, MatchInfo?>[] filters)
        {
            EnsureInitialized();
            return ((SettingsLoader)EnvironmentSettings.SettingsLoader).UserTemplateCache.List(exactMatchesOnly, filters);
        }

        public async Task<ICreationResult> CreateAsync(ITemplateInfo info, string name, string outputPath, IReadOnlyDictionary<string, string> parameters, bool skipUpdateCheck, string baselineName)
        {
            TemplateCreationResult instantiateResult = await _templateCreator.InstantiateAsync(info, name, name, outputPath, parameters, skipUpdateCheck, forceCreation: false, baselineName: baselineName).ConfigureAwait(false);
            return instantiateResult.ResultInfo;
        }

        public IEnumerable<string>  Uninstall(string path)
        {
            EnsureInitialized();
            return Installer.Uninstall(new[] { path });
        }

        public IEnumerable<string>  Uninstall(params string[] paths)
        {
            EnsureInitialized();
            return Installer.Uninstall(paths);
        }

        public IEnumerable<string>  Uninstall(IEnumerable<string> paths)
        {
            EnsureInitialized();
            return Installer.Uninstall(paths);
        }
    }
}
