using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.TemplateEngine
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
                    string profileDir;
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        profileDir = Environment.GetEnvironmentVariable("USERPROFILE");
                    }
                    else
                    {
                        profileDir = Environment.GetEnvironmentVariable("HOME");
                    }

                    _userProfileDir = profileDir;
                }

                return _userProfileDir;
            }
        }
    }
}