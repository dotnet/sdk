// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;

namespace Microsoft.TemplateEngine.Utils
{
    [Obsolete("Use Microsoft.TemplateEngine.Edge.DefaultTemplateEngineHost instead.")]
    public class DefaultTemplateEngineHost : ITemplateEngineHost
    {
        private static readonly IReadOnlyList<(Type Type, IIdentifiedComponent Instance)> NoComponents = Array.Empty<(Type Type, IIdentifiedComponent Instance)>();
        private readonly IReadOnlyDictionary<string, string> _hostDefaults;
        private readonly IReadOnlyList<(Type InterfaceType, IIdentifiedComponent Instance)> _hostBuiltInComponents;
        private Dictionary<string, Action<string, string[]>> _diagnosticLoggers;
        private ILoggerFactory _loggerFactory;
        private ILogger _logger;

        public DefaultTemplateEngineHost(string hostIdentifier, string version)
                    : this(hostIdentifier, version, null)
        {
        }

        public DefaultTemplateEngineHost(string hostIdentifier, string version, Dictionary<string, string> defaults)
            : this(hostIdentifier, version, defaults, NoComponents, null)
        {
        }

        public DefaultTemplateEngineHost(string hostIdentifier, string version, Dictionary<string, string> defaults, IReadOnlyList<(Type InterfaceType, IIdentifiedComponent Instance)> builtIns)
            : this(hostIdentifier, version, defaults, builtIns, null)
        {
        }

        public DefaultTemplateEngineHost(string hostIdentifier, string version, Dictionary<string, string> defaults, IReadOnlyList<string> fallbackHostTemplateConfigNames)
            : this(hostIdentifier, version, defaults, NoComponents, fallbackHostTemplateConfigNames)
        {
        }

        public DefaultTemplateEngineHost(string hostIdentifier, string version, Dictionary<string, string> defaults, IReadOnlyList<(Type InterfaceType, IIdentifiedComponent Instance)> builtIns, IReadOnlyList<string> fallbackHostTemplateConfigNames)
        {
            HostIdentifier = hostIdentifier;
            Version = version;
            _hostDefaults = defaults ?? new Dictionary<string, string>();
            FileSystem = new PhysicalFileSystem();
            _hostBuiltInComponents = builtIns ?? NoComponents;
            FallbackHostTemplateConfigNames = fallbackHostTemplateConfigNames ?? new List<string>();
            _diagnosticLoggers = new Dictionary<string, Action<string, string[]>>();
            _loggerFactory = NullLoggerFactory.Instance;
            _logger = _loggerFactory.CreateLogger("Template Engine") ?? NullLogger.Instance;
        }

        public IPhysicalFileSystem FileSystem { get; private set; }

        public Action<string, TimeSpan, int> OnLogTiming { get; set; }

        public string HostIdentifier { get; }

        public IReadOnlyList<string> FallbackHostTemplateConfigNames { get; }

        public string Version { get; }

        public virtual IReadOnlyList<(Type InterfaceType, IIdentifiedComponent Instance)> BuiltInComponents => _hostBuiltInComponents;

        public ILogger Logger => _logger;

        public ILoggerFactory LoggerFactory => _loggerFactory;

        public virtual void LogMessage(string message)
        {
            Console.WriteLine(message);
        }

        public virtual void OnCriticalError(string code, string message, string currentFile, long currentPosition)
        {
        }

        public virtual bool OnNonCriticalError(string code, string message, string currentFile, long currentPosition)
        {
            LogMessage(string.Format($"Error: {message}"));
            return false;
        }

        public virtual bool OnParameterError(ITemplateParameter parameter, string receivedValue, string message, out string newValue)
        {
            newValue = null;
            return false;
        }

        public virtual void OnSymbolUsed(string symbol, object value)
        {
        }

        // stub that will be built out soon.
        public virtual bool TryGetHostParamDefault(string paramName, out string value)
        {
            switch (paramName)
            {
                case "HostIdentifier":
                    value = HostIdentifier;
                    return true;
            }

            return _hostDefaults.TryGetValue(paramName, out value);
        }

        public void VirtualizeDirectory(string path)
        {
            FileSystem = new InMemoryFileSystem(path, FileSystem);
        }

        public bool OnPotentiallyDestructiveChangesDetected(IReadOnlyList<IFileChange> changes, IReadOnlyList<IFileChange> destructiveChanges)
        {
            return true;
        }

        public bool OnConfirmPartialMatch(string name)
        {
            return true;
        }

        public void RegisterDiagnosticLogger(string category, Action<string, string[]> messageHandler)
        {
            _diagnosticLoggers[category] = messageHandler;
        }

        public void LogDiagnosticMessage(string message, string category, params string[] details)
        {
            if (_diagnosticLoggers.TryGetValue(category, out Action<string, string[]> messageHandler))
            {
                messageHandler(message, details);
            }
        }

        public void LogTiming(string label, TimeSpan duration, int depth)
        {
            OnLogTiming?.Invoke(label, duration, depth);
        }
    }
}
