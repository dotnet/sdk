// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Edge
{
    public class DefaultTemplateEngineHost : ITemplateEngineHost
    {
        private static readonly IReadOnlyList<(Type, IIdentifiedComponent)> NoComponents = [];
        private readonly IReadOnlyDictionary<string, string> _hostDefaults;
        private readonly IReadOnlyList<(Type InterfaceType, IIdentifiedComponent Instance)> _hostBuiltInComponents;

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

            loggerFactory ??= NullLoggerFactory.Instance;
            LoggerFactory = loggerFactory;
            Logger = LoggerFactory.CreateLogger("Template Engine");

            WorkingDirectory = Environment.CurrentDirectory;
        }

        public IPhysicalFileSystem FileSystem { get; private set; }

        public string HostIdentifier { get; }

        public string WorkingDirectory { get; }

        public IReadOnlyList<string> FallbackHostTemplateConfigNames { get; }

        public string Version { get; }

        public virtual IReadOnlyList<(Type InterfaceType, IIdentifiedComponent Instance)> BuiltInComponents => _hostBuiltInComponents;

        public ILogger Logger { get; }

        public ILoggerFactory LoggerFactory { get; }

        // stub that will be built out soon.
        public virtual bool TryGetHostParamDefault(string paramName, out string? value)
        {
            switch (paramName)
            {
                case "HostIdentifier":
                    value = HostIdentifier;
                    return true;
                case "WorkingDirectory":
                    value = WorkingDirectory;
                    return true;
            }

            return _hostDefaults.TryGetValue(paramName, out value);
        }

        public void VirtualizeDirectory(string path)
        {
            FileSystem = new InMemoryFileSystem(path, FileSystem);
        }

        public void Dispose()
        {
            LoggerFactory?.Dispose();
        }

        #region Obsoleted
        [Obsolete]
        bool ITemplateEngineHost.OnPotentiallyDestructiveChangesDetected(IReadOnlyList<IFileChange> changes, IReadOnlyList<IFileChange> destructiveChanges)
        {
            return true;
        }
        #endregion
    }
}
