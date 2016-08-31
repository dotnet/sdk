#if !NET451
using System;
#endif
using System.IO;
#if !NET451
using System.Runtime.InteropServices;
#endif

namespace Microsoft.TemplateEngine.Utils
{
    public static class HappyPath
    {
        private static string _userProfileDir;

        public static string ProcessPath(this string path)
        {
            if(string.IsNullOrEmpty(path))
            {
                return path;
            }

            if(path[0] != '~')
            {
                return path;
            }

            return Path.Combine(UserProfileDir, path.Substring(1));
        }

        public static string UserProfileDir
        {
            get
            {
                if(_userProfileDir == null)
                {
#if !NET451
                    string profileDir =
                        Environment.GetEnvironmentVariable(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                            ? "USERPROFILE"
                            : "HOME");
#else
                    string profileDir = "USERPROFILE";
#endif

                    _userProfileDir = profileDir;
                }

                return _userProfileDir;
            }
        }
    }
}