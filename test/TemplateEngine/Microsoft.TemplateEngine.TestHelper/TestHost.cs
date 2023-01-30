// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.TestHelper
{
    public partial class TestHost : ITemplateEngineHost
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private readonly string _hostIdentifier;
        private readonly string _version;
        private IPhysicalFileSystem _fileSystem;
        private readonly IReadOnlyList<(Type, IIdentifiedComponent)> _builtIns;
        private readonly IReadOnlyList<string> _fallbackNames;

        internal TestHost(
            [CallerMemberName] string hostIdentifier = "",
            string version = "1.0.0",
            bool loadDefaultGenerator = true,
            IReadOnlyList<(Type, IIdentifiedComponent)>? additionalComponents = null,
            IPhysicalFileSystem? fileSystem = null,
            IReadOnlyList<string>? fallbackNames = null,
            IEnumerable<ILoggerProvider>? addLoggerProviders = null)
        {
            _hostIdentifier = string.IsNullOrWhiteSpace(hostIdentifier) ? "TestRunner" : hostIdentifier;
            _version = string.IsNullOrWhiteSpace(version) ? "1.0.0" : version;

            var builtIns = new List<(Type, IIdentifiedComponent)>();
            if (additionalComponents != null)
            {
                builtIns.AddRange(additionalComponents);
            }
            builtIns.AddRange(Edge.Components.AllComponents);
            if (loadDefaultGenerator)
            {
                builtIns.AddRange(Orchestrator.RunnableProjects.Components.AllComponents);
            }

            _builtIns = builtIns;
            HostParamDefaults = new Dictionary<string, string>();
            _fileSystem = fileSystem ?? new PhysicalFileSystem();

            _loggerFactory = new TestLoggerFactory();
            addLoggerProviders?.ToList().ForEach(_loggerFactory.AddProvider);
            _logger = _loggerFactory.CreateLogger(hostIdentifier);
            _fallbackNames = fallbackNames ?? new[] { "dotnetcli" };
        }

        public Dictionary<string, string> HostParamDefaults { get; set; } = new Dictionary<string, string>();

        IPhysicalFileSystem ITemplateEngineHost.FileSystem => _fileSystem;

        string ITemplateEngineHost.HostIdentifier => _hostIdentifier;

        IReadOnlyList<string> ITemplateEngineHost.FallbackHostTemplateConfigNames => _fallbackNames;

        string ITemplateEngineHost.Version => _version;

        IReadOnlyList<(Type, IIdentifiedComponent)> ITemplateEngineHost.BuiltInComponents => _builtIns;

        ILogger ITemplateEngineHost.Logger => _logger;

        ILoggerFactory ITemplateEngineHost.LoggerFactory => _loggerFactory;

        public static ITemplateEngineHost GetVirtualHost(
            [CallerMemberName] string hostIdentifier = "",
            IEnvironment? environment = null,
            IReadOnlyList<(Type, IIdentifiedComponent)>? additionalComponents = null,
            IReadOnlyDictionary<string, string>? defaultParameters = null)
        {
            TestHost host = new TestHost(hostIdentifier: hostIdentifier, additionalComponents: additionalComponents);
            environment ??= new DefaultEnvironment();

            if (defaultParameters != null)
            {
                foreach (var parameter in defaultParameters)
                {
                    host.HostParamDefaults[parameter.Key] = parameter.Value;
                }
            }
            ((ITemplateEngineHost)host).VirtualizeDirectory(new DefaultPathInfo(environment, host).GlobalSettingsDir);
            return host;
        }

        bool ITemplateEngineHost.TryGetHostParamDefault(string paramName, out string? value)
        {
            return HostParamDefaults.TryGetValue(paramName, out value);
        }

        void ITemplateEngineHost.VirtualizeDirectory(string path)
        {
            _fileSystem = new InMemoryFileSystem(path, _fileSystem);
        }

        public void Dispose()
        {
            _loggerFactory?.Dispose();
        }

        [Obsolete]
        bool ITemplateEngineHost.OnPotentiallyDestructiveChangesDetected(IReadOnlyList<IFileChange> changes, IReadOnlyList<IFileChange> destructiveChanges)
        {
            //do nothing
            return false;
        }
    }
}
