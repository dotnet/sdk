using Microsoft.TemplateEngine.Abstractions;

#if !NET451
using System;
#endif
using System.IO;
#if !NET451
using System.Runtime.InteropServices;
#endif

namespace Microsoft.TemplateEngine.Utils
{
    public static class EngineEnvironmentSettings
    {
        static EngineEnvironmentSettings()
        {
            Paths = new DefaultPathInfo();
        }

        public static ITemplateEngineHost Host { get; set; }

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
#if !NET451
                    string profileDir =
                        Environment.GetEnvironmentVariable(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                            ? "USERPROFILE"
                            : "HOME");
#else
                        string profileDir = System.Environment.GetEnvironmentVariable("USERPROFILE");
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
                        _baseDir = Path.Combine(UserProfileDir, ".netnew", Host.HostIdentifier, Host.Version.ToString());
                    }

                    return _baseDir;
                }
            }
        }
    }
}
