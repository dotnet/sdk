// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Edge
{
    public sealed class EngineEnvironmentSettings : IEngineEnvironmentSettings
    {
        public EngineEnvironmentSettings(
            ITemplateEngineHost host,
            bool virtualizeSettings = false,
            string? settingsLocation = null,
            IEnvironment? environment = null,
            IComponentManager? componentManager = null,
            IPathInfo? pathInfo = null)
        {
            Host = host ?? throw new ArgumentNullException(nameof(host));
            Environment = environment ?? new DefaultEnvironment();
            Paths = pathInfo ?? new DefaultPathInfo(this, settingsLocation);
            if (virtualizeSettings)
            {
                Host.VirtualizeDirectory(Paths.GlobalSettingsDir);
            }
            Components = componentManager ?? new ComponentManager(this);
        }

        [Obsolete("ISettingsLoader is obsolete, see ISettingsLoader obsolete description for details.")]
        public ISettingsLoader SettingsLoader
        {
            get
            {
                throw new Exception("ISettingsLoader is no longer supported.");
            }
        }

        public ITemplateEngineHost Host { get; }

        public IEnvironment Environment { get;  }

        public IPathInfo Paths { get;  }

        public IComponentManager Components { get; }

        private class DefaultPathInfo : IPathInfo
        {
            public DefaultPathInfo(IEngineEnvironmentSettings engineEnvironmentSettings, string? hiveLocation)
            {
                bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                UserProfileDir = engineEnvironmentSettings.Environment.GetEnvironmentVariable(isWindows
                    ? "USERPROFILE"
                    : "HOME") ?? throw new NotSupportedException("HOME or USERPROFILE environment variable is not defined, the environment is not supported");
                GlobalSettingsDir = hiveLocation ?? Path.Combine(UserProfileDir, ".templateengine");
                HostSettingsDir = Path.Combine(GlobalSettingsDir, engineEnvironmentSettings.Host.HostIdentifier);
                HostVersionSettingsDir = Path.Combine(GlobalSettingsDir, engineEnvironmentSettings.Host.HostIdentifier, engineEnvironmentSettings.Host.Version);
            }

            public string UserProfileDir { get; }

            public string GlobalSettingsDir { get; }

            public string HostSettingsDir { get; }

            public string HostVersionSettingsDir { get; }
        }

        private class DefaultEnvironment : IEnvironment
        {
            private const int DefaultBufferWidth = 160;
            private readonly IReadOnlyDictionary<string, string> _environmentVariables;

            public DefaultEnvironment()
            {
                Dictionary<string, string> variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                IDictionary env = System.Environment.GetEnvironmentVariables();

                foreach (string key in env.Keys.OfType<string>())
                {
                    variables[key] = (env[key] as string) ?? string.Empty;
                }

                _environmentVariables = variables;
                NewLine = System.Environment.NewLine;
            }

            public string NewLine { get; }

            // Console.BufferWidth can throw if there's no console, such as when output is redirected, so
            // first check if it is redirected, and fall back to a default value if needed.
            public int ConsoleBufferWidth => Console.IsOutputRedirected ? DefaultBufferWidth : Console.BufferWidth;

            public string ExpandEnvironmentVariables(string name)
            {
                return System.Environment.ExpandEnvironmentVariables(name);
            }

            public string? GetEnvironmentVariable(string name)
            {
                _environmentVariables.TryGetValue(name, out string? result);
                return result;
            }

            public IReadOnlyDictionary<string, string> GetEnvironmentVariables()
            {
                return _environmentVariables;
            }
        }
    }
}
