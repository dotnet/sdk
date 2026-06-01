// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Configurer
{
    class CliFolderPathCalculatorCore
    {
        public const string DotnetHomeVariableName = "DOTNET_CLI_HOME";
        public const string DotnetProfileDirectoryName = ".dotnet";

        private readonly Func<string, string?> _getEnvironmentVariable;

        /// <summary>
        /// Creates an instance that reads environment variables from the process environment.
        /// </summary>
        public CliFolderPathCalculatorCore()
            : this(Environment.GetEnvironmentVariable)
        {
        }

        /// <summary>
        /// Creates an instance that reads environment variables via the supplied delegate.
        /// Use this from MSBuild tasks to route reads through TaskEnvironment.
        /// </summary>
        public CliFolderPathCalculatorCore(Func<string, string?> getEnvironmentVariable)
        {
            _getEnvironmentVariable = getEnvironmentVariable ?? throw new ArgumentNullException(nameof(getEnvironmentVariable));
        }

        public string? GetDotnetUserProfileFolderPath()
        {
            string? homePath = GetDotnetHomePath();
            if (homePath is null)
            {
                return null;
            }

            return Path.Combine(homePath, DotnetProfileDirectoryName);
        }

        /// <summary>
        /// Convenience property that returns the .dotnet user profile folder path
        /// using the default (process environment) configuration.
        /// </summary>
        public static string DotnetUserProfileFolderPath =>
            new CliFolderPathCalculatorCore().GetDotnetUserProfileFolderPath()
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DotnetProfileDirectoryName);

        public string? GetDotnetHomePath()
        {
            var home = _getEnvironmentVariable(DotnetHomeVariableName);
            if (string.IsNullOrEmpty(home))
            {
                // Route the user-profile lookup through the injected env-var delegate so that
                // MSBuild tasks running with an isolated TaskEnvironment do not read ambient
                // process state. Mirror what Environment.GetFolderPath(SpecialFolder.UserProfile)
                // does internally: USERPROFILE on Windows, HOME on Unix.
                var userProfileVariableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? "USERPROFILE"
                    : "HOME";
                home = _getEnvironmentVariable(userProfileVariableName);
                if (string.IsNullOrEmpty(home))
                {
                    // Fall back to the framework helper to preserve existing behavior for callers
                    // that aren't running under an isolated TaskEnvironment (e.g. HOMEDRIVE+HOMEPATH
                    // on Windows when USERPROFILE is not set).
                    home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    if (string.IsNullOrEmpty(home))
                    {
                        return null;
                    }
                }
            }

            return home;
        }

    }
}
