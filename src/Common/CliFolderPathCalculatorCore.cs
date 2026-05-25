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

        public string? GetDotnetHomePath()
        {
            var home = _getEnvironmentVariable(DotnetHomeVariableName);
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
