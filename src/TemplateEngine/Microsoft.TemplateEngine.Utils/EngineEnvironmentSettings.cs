using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Utils
{
    public class EngineEnvironmentSettings : IEngineEnvironmentSettings
    {
        public EngineEnvironmentSettings(ITemplateEngineHost host, Func<IEngineEnvironmentSettings, ISettingsLoader> settingsLoaderFactory)
            : this(host, settingsLoaderFactory, null)
        {
        }

        public EngineEnvironmentSettings(ITemplateEngineHost host, Func<IEngineEnvironmentSettings, ISettingsLoader> settingsLoaderFactory, string hiveLocation)
        {
            Host = host;
            Paths = new DefaultPathInfo(this, hiveLocation);
            Environment = new DefaultEnvironment();
            SettingsLoader = settingsLoaderFactory(this);
        }

        public ISettingsLoader SettingsLoader { get; }

        public ITemplateEngineHost Host { get; set; }

        public IEnvironment Environment { get; set; }

        public IPathInfo Paths { get; set; }

        private class DefaultPathInfo : IPathInfo
        {
            private string _userProfileDir;
            private string _baseDir;
            private readonly IEngineEnvironmentSettings _parent;

            public DefaultPathInfo(IEngineEnvironmentSettings parent, string hiveLocation)
            {
                _parent = parent;
                _baseDir = hiveLocation;
            }

            public string UserProfileDir
            {
                get
                {
                    if (_userProfileDir == null)
                    {
                        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

                        string profileDir = _parent.Environment.GetEnvironmentVariable(isWindows
                            ? "USERPROFILE"
                            : "HOME");

                        _userProfileDir = profileDir;
                    }

                    return _userProfileDir;
                }
            }

            public string BaseDir
            {
                get
                {
                    if (_baseDir == null)
                    {
                        _baseDir = Path.Combine(UserProfileDir, ".templateengine", _parent.Host.HostIdentifier, _parent.Host.Version);
                    }

                    return _baseDir;
                }
            }
        }

        private class DefaultEnvironment : IEnvironment
        {
            private readonly IReadOnlyDictionary<string, string> _environmentVariables;

            public DefaultEnvironment()
            {
                Dictionary<string, string> variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                IDictionary env = System.Environment.GetEnvironmentVariables();

                foreach(string key in env.Keys.OfType<string>())
                {
                    variables[key] = (env[key] as string) ?? string.Empty;
                }

                _environmentVariables = variables;
                NewLine = System.Environment.NewLine;
            }

            public string NewLine { get; }

            private const int DefaultBufferWidth = 160;

            // Console.BufferWidth can throw if there's no console, such as when output is redirected, so
            // first check if it is redirected, and fall back to a default value if needed.
            public int ConsoleBufferWidth => Console.IsOutputRedirected ? DefaultBufferWidth : Console.BufferWidth;

            public string ExpandEnvironmentVariables(string name)
            {
                return System.Environment.ExpandEnvironmentVariables(name);
            }

            public string GetEnvironmentVariable(string name)
            {
                _environmentVariables.TryGetValue(name, out string result);
                return result;
            }

            public IReadOnlyDictionary<string, string> GetEnvironmentVariables()
            {
                return _environmentVariables;
            }
        }
    }
}
