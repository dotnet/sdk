using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#if !NET45
using System.Runtime.InteropServices;
#endif
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Utils
{
    public static class EngineEnvironmentSettings
    {
        static EngineEnvironmentSettings()
        {
            Paths = new DefaultPathInfo();
            Environment = new DefaultEnvironment();
        }

        public static ITemplateEngineHost Host { get; set; }

        public static IEnvironment Environment { get; set; }

        public static IPathInfo Paths { get; set; }

        private class DefaultPathInfo : IPathInfo
        {
            private static string _userProfileDir;
            private static string _baseDir;

            public string UserProfileDir
            {
                get
                {
                    if (_userProfileDir == null)
                    {
#if !NET45
                    string profileDir =
                        Environment.GetEnvironmentVariable(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                            ? "USERPROFILE"
                            : "HOME");
#else
                        string profileDir = Environment.GetEnvironmentVariable("USERPROFILE");
#endif

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
                        _baseDir = Path.Combine(UserProfileDir, ".templateengine", Host.HostIdentifier, Host.Version);
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
