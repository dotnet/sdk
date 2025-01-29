// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Configurer
{
    static class CliFolderPathCalculatorCore
    {
        public const string DotnetHomeVariableName = "DOTNET_CLI_HOME";
        public const string DotnetProfileDirectoryName = ".dotnet";

        public static string? GetDotnetUserProfileFolderPath()
        {
            string? homePath = GetDotnetHomePath();
            if (homePath is null)
            {
                return null;
            }

            return Path.Combine(homePath, DotnetProfileDirectoryName);
        }

        public static string? GetDotnetHomePath()
        {
            var home = Environment.GetEnvironmentVariable(DotnetHomeVariableName);
            if (string.IsNullOrEmpty(home))
            {
                home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrEmpty(home))
                {
                    return null;
                }
            }

            return home;
        }
    }
}
