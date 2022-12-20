// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal sealed class CliPathInfo : IPathInfo
    {
        public CliPathInfo(
            ITemplateEngineHost host,
            IEnvironment environment,
            string? settingsLocation)
        {
            UserProfileDir = GetUserProfileDir(environment);
            GlobalSettingsDir = GetGlobalSettingsDir(settingsLocation);

            ValidatePathArguments(host, GlobalSettingsDir);
            HostSettingsDir = Path.Combine(GlobalSettingsDir, host.HostIdentifier);

            if (string.IsNullOrWhiteSpace(host.Version))
            {
                throw new ArgumentException($"{nameof(host.Version)} of {nameof(host)} cannot be null or whitespace.", nameof(host));
            }
            HostVersionSettingsDir = Path.Combine(GlobalSettingsDir, host.HostIdentifier, host.Version);
        }

        public string UserProfileDir { get; }

        public string GlobalSettingsDir { get; }

        public string HostSettingsDir { get; }

        public string HostVersionSettingsDir { get; }

        private static string GetUserProfileDir(IEnvironment environment) => environment.GetEnvironmentVariable(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "USERPROFILE"
                : "HOME")
            ?? throw new NotSupportedException("HOME or USERPROFILE environment variable is not defined, the environment is not supported");

        private static string GetGlobalSettingsDir(string? settingsLocation)
        {
            var definedSettingsLocation = string.IsNullOrWhitespace(settingsLocation)
                ? Path.Combine(CliFolderPathCalculator.DotnetHomePath, ".templateengine")
                : settingsLocation;

            Reporter.Verbose.WriteLine($"Global Settings Location: {definedSettingsLocation}");

            return definedSettingsLocation;
        }

        private static void ValidatePathArguments(ITemplateEngineHost host, string globalDir)
        {
            if (string.IsNullOrWhiteSpace(host.HostIdentifier))
            {
                throw new ArgumentException($"{nameof(host.HostIdentifier)} of {nameof(host)} cannot be null or whitespace.", nameof(host));
            }
            if (string.IsNullOrWhiteSpace(globalDir))
            {
                throw new ArgumentException($"{nameof(globalDir)} cannot be null or whitespace.");
            }
        }
    }
}
