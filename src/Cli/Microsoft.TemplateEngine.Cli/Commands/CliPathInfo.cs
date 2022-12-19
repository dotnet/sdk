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
            HostSettingsDir = GetDefaultHostSettingsDir(host, globalDir: GlobalSettingsDir);
            HostVersionSettingsDir = GetDefaultHostVersionSettingsDir(host, globalDir: GlobalSettingsDir);
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
            var definedSettingsLocation = string.IsNullOrEmpty(settingsLocation)
                ? Path.Combine(CliFolderPathCalculator.DotnetHomePath, ".templateengine")
                : settingsLocation;

            Reporter.Verbose.WriteLine($"Global Settings Location: {definedSettingsLocation}");

            return Path.Combine(definedSettingsLocation, ".templateengine");
        }

        private static string GetDefaultHostSettingsDir(ITemplateEngineHost host, string? userDir = null, string? globalDir = null)
        {
            ValidatePathArguments(host, userDir, globalDir);

            return Path.Combine(globalDir ?? GetGlobalSettingsDir(userDir!), host.HostIdentifier);
        }

        private static string GetDefaultHostVersionSettingsDir(ITemplateEngineHost host, string? userDir = null, string? globalDir = null)
        {
            ValidatePathArguments(host, userDir, globalDir);

            if (string.IsNullOrWhiteSpace(host.Version))
            {
                throw new ArgumentException($"{nameof(host.Version)} of {nameof(host)} cannot be null or whitespace.", nameof(host));
            }

            return Path.Combine(globalDir ?? GetGlobalSettingsDir(userDir!), host.HostIdentifier, host.Version);
        }

        private static void ValidatePathArguments(ITemplateEngineHost host, string? userDir = null, string? globalDir = null)
        {
            if (string.IsNullOrWhiteSpace(host.HostIdentifier))
            {
                throw new ArgumentException($"{nameof(host.HostIdentifier)} of {nameof(host)} cannot be null or whitespace.", nameof(host));
            }
            if (string.IsNullOrWhiteSpace(userDir) && string.IsNullOrWhiteSpace(globalDir))
            {
                throw new ArgumentException($"both {nameof(userDir)} and {nameof(globalDir)} cannot be null or whitespace.", nameof(userDir));
            }
        }
    }
}
