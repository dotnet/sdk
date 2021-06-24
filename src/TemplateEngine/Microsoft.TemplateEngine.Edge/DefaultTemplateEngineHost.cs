// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Edge
{
    public class DefaultTemplateEngineHost : ITemplateEngineHost
    {
        private static readonly IReadOnlyList<(Type, IIdentifiedComponent)> NoComponents = Array.Empty<(Type, IIdentifiedComponent)>();
        private readonly IReadOnlyDictionary<string, string> _hostDefaults;
        private readonly IReadOnlyList<(Type InterfaceType, IIdentifiedComponent Instance)> _hostBuiltInComponents;
        [Obsolete]
        private Dictionary<string, Action<string, string[]>> _diagnosticLoggers = new Dictionary<string, Action<string, string[]>>();
        private ILoggerFactory _loggerFactory;
        private ILogger _logger;

        public DefaultTemplateEngineHost(
            string hostIdentifier,
            string version,
            Dictionary<string, string>? defaults = null,
            IReadOnlyList<(Type InterfaceType, IIdentifiedComponent Instance)>? builtIns = null,
            IReadOnlyList<string>? fallbackHostTemplateConfigNames = null,
            ILoggerFactory? loggerFactory = null)
        {
            HostIdentifier = hostIdentifier;
            Version = version;
            _hostDefaults = defaults ?? new Dictionary<string, string>();
            FileSystem = new PhysicalFileSystem();
            _hostBuiltInComponents = builtIns ?? NoComponents;
            FallbackHostTemplateConfigNames = fallbackHostTemplateConfigNames ?? new List<string>();

            if (loggerFactory == null)
            {
                loggerFactory = NullLoggerFactory.Instance;
            }
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger("Template Engine") ?? NullLogger.Instance;
        }

        public IPhysicalFileSystem FileSystem { get; private set; }

        public string HostIdentifier { get; }

        public IReadOnlyList<string> FallbackHostTemplateConfigNames { get; }

        public string Version { get; }

        public virtual IReadOnlyList<(Type InterfaceType, IIdentifiedComponent Instance)> BuiltInComponents => _hostBuiltInComponents;

        public ILogger Logger => _logger;

        public ILoggerFactory LoggerFactory => _loggerFactory;

        // stub that will be built out soon.
        public virtual bool TryGetHostParamDefault(string paramName, out string? value)
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

        #region Obsolete

#pragma warning disable SA1201 // Elements should appear in the correct order
        [Obsolete("Use " + nameof(Logger) + " instead")]
        public Action<string, TimeSpan, int>? OnLogTiming { get; set; }
#pragma warning restore SA1201 // Elements should appear in the correct order

        [Obsolete("The method is deprecated.")]
        bool ITemplateEngineHost.OnConfirmPartialMatch(string name)
        {
            return true;
        }

        [Obsolete("The method is deprecated.")]
        void ITemplateEngineHost.OnSymbolUsed(string symbol, object value)
        {
        }

        [Obsolete("The method is deprecated.")]
        bool ITemplateEngineHost.OnParameterError(ITemplateParameter parameter, string receivedValue, string message, out string newValue)
        {
            newValue = "";
            return false;
        }

        [Obsolete("Use " + nameof(Logger) + " instead")]
        public void RegisterDiagnosticLogger(string category, Action<string, string[]> messageHandler)
        {
            _diagnosticLoggers[category] = messageHandler;
        }

        [Obsolete("Use " + nameof(Logger) + " instead")]
        void ITemplateEngineHost.LogDiagnosticMessage(string message, string category, params string[] details)
        {
            if (_diagnosticLoggers.TryGetValue(category, out Action<string, string[]> messageHandler))
            {
                messageHandler(message, details);
            }
        }

        [Obsolete("Use " + nameof(Logger) + " instead")]
        void ITemplateEngineHost.LogTiming(string label, TimeSpan duration, int depth)
        {
            OnLogTiming?.Invoke(label, duration, depth);
        }

        [Obsolete("Use " + nameof(Logger) + " instead")]
        void ITemplateEngineHost.LogMessage(string message)
        {
            Console.WriteLine(message);
        }

        [Obsolete("Use " + nameof(Logger) + " instead")]
        void ITemplateEngineHost.OnCriticalError(string code, string message, string currentFile, long currentPosition)
        {
        }

        [Obsolete("Use " + nameof(Logger) + " instead")]
        bool ITemplateEngineHost.OnNonCriticalError(string code, string message, string currentFile, long currentPosition)
        {
            ((ITemplateEngineHost)this).LogMessage(string.Format($"Error: {message}"));
            return false;
        }

        [Obsolete]
        bool ITemplateEngineHost.OnPotentiallyDestructiveChangesDetected(IReadOnlyList<IFileChange> changes, IReadOnlyList<IFileChange> destructiveChanges)
        {
            return true;
        }
        #endregion
    }
}
