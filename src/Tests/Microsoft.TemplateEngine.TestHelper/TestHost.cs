﻿// Licensed to the .NET Foundation under one or more agreements.
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
        private IPhysicalFileSystem _fileSystem;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private readonly string _hostIdentifier;
        private readonly string _version;
        private readonly List<(LogLevel, string)> _messagesCollection = new List<(LogLevel, string)>();
        private IReadOnlyList<(Type, IIdentifiedComponent)> _builtIns;
        private IReadOnlyList<string> _fallbackNames;

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
            _logger = _loggerFactory.CreateLogger("Test Host");
            _fallbackNames = fallbackNames ?? new[] { "dotnetcli" };
        }

        internal Dictionary<string, string> HostParamDefaults { get; set; } = new Dictionary<string, string>();

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
            environment = environment ?? new DefaultEnvironment();
            
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
        void ITemplateEngineHost.OnSymbolUsed(string symbol, object value)
        {
            //do nothing
        }

        [Obsolete]
        bool ITemplateEngineHost.OnParameterError(ITemplateParameter parameter, string receivedValue, string message, out string newValue)
        {
            //do nothing
            newValue = receivedValue;
            return false;
        }

        [Obsolete]
        bool ITemplateEngineHost.OnNonCriticalError(string code, string message, string currentFile, long currentPosition)
        {
            //do nothing
            return false;
        }

        [Obsolete]
        void ITemplateEngineHost.OnCriticalError(string code, string message, string currentFile, long currentPosition)
        {
            //do nothing
        }

        [Obsolete]
        void ITemplateEngineHost.LogMessage(string message)
        {
            //do nothing
        }

        [Obsolete]
        bool ITemplateEngineHost.OnPotentiallyDestructiveChangesDetected(IReadOnlyList<IFileChange> changes, IReadOnlyList<IFileChange> destructiveChanges)
        {
            //do nothing
            return false;
        }

        [Obsolete]
        bool ITemplateEngineHost.OnConfirmPartialMatch(string name)
        {
            //do nothing
            return false;
        }

        [Obsolete]
        void ITemplateEngineHost.LogDiagnosticMessage(string message, string category, params string[] details)
        {
            //do nothing
        }

        [Obsolete]
        void ITemplateEngineHost.LogTiming(string label, TimeSpan duration, int depth)
        {
            //do nothing
        }
    }
}
