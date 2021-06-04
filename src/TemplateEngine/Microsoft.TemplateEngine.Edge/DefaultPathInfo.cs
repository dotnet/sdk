// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Edge
{
    /// <summary>
    /// Default implementation of <see cref="IPathInfo"/>. If custom settings location are not passed, the following locations to be used: <br/>
    /// - global settings: [user profile directory]/.templateengine <br/>
    /// - host settings: [user profile directory]/.templateengine/[<see cref="ITemplateEngineHost.HostIdentifier"/>] <br/>
    /// - host version settings: [user profile directory]/.templateengine/[<see cref="ITemplateEngineHost.HostIdentifier"/>]/[<see cref="ITemplateEngineHost.Version"/>].
    /// </summary>
    public sealed class DefaultPathInfo : IPathInfo
    {
        /// <summary>
        /// Creates the instance.
        /// </summary>
        /// <param name="environment"><see cref="IEnvironment"/> implementation to be used to get location of user profile directory.</param>
        /// <param name="host"><see cref="ITemplateEngineHost"/> implementation.</param>
        /// <param name="globalSettingsDir">
        /// If specified, the directory will be used for storing global settings.
        /// Default location: [user profile directory]/.templateengine.
        /// </param>
        /// <param name="hostSettingsDir">
        /// If specified, the directory will be used for storing host settings.
        /// Default location: host settings: [user profile directory]/.templateengine/[<see cref="ITemplateEngineHost.HostIdentifier"/>].
        /// </param>
        /// <param name="hostVersionSettingsDir">
        /// If specified, the directory will be used for storing host version settings.
        /// Default location: host settings: [user profile directory]/.templateengine/[<see cref="ITemplateEngineHost.HostIdentifier"/>]/[<see cref="ITemplateEngineHost.Version"/>].
        /// </param>
        public DefaultPathInfo(
            IEnvironment environment,
            ITemplateEngineHost host,
            string? globalSettingsDir = null,
            string? hostSettingsDir = null,
            string? hostVersionSettingsDir = null)
        {
            UserProfileDir = GetUserProfileDir(environment);

            if (string.IsNullOrWhiteSpace(globalSettingsDir))
            {
                globalSettingsDir = GetDefaultGlobalSettingsDir(UserProfileDir);
            }
            GlobalSettingsDir = globalSettingsDir!;

            if (string.IsNullOrWhiteSpace(hostSettingsDir))
            {
                hostSettingsDir = GetDefaultHostSettingsDir(host, userDir: UserProfileDir);
            }
            HostSettingsDir = hostSettingsDir!;

            if (string.IsNullOrWhiteSpace(hostVersionSettingsDir))
            {
                hostVersionSettingsDir = GetDefaultHostVersionSettingsDir(host, userDir: UserProfileDir);
            }
            HostVersionSettingsDir = hostVersionSettingsDir!;
        }

        internal DefaultPathInfo(
            IEngineEnvironmentSettings engineEnvironmentSettings,
            string? settingsLocation)
        {
            UserProfileDir = GetUserProfileDir(engineEnvironmentSettings.Environment);

            GlobalSettingsDir = settingsLocation ?? GetDefaultGlobalSettingsDir(UserProfileDir);
            HostSettingsDir = GetDefaultHostSettingsDir(engineEnvironmentSettings.Host, globalDir: GlobalSettingsDir);
            HostVersionSettingsDir = GetDefaultHostVersionSettingsDir(engineEnvironmentSettings.Host, globalDir: GlobalSettingsDir);
        }

        /// <inheritdoc/>
        public string UserProfileDir { get; }

        /// <summary>
        /// Gets global settings directory.
        /// If not specified via constructor, the default location is [<see cref="UserProfileDir"/>]/.templateengine.
        /// </summary>
        public string GlobalSettingsDir { get; }

        /// <summary>
        /// Gets host settings directory.
        /// If not specified via constructor, the default location is [<see cref="GlobalSettingsDir"/>]/[host identifier].
        /// </summary>
        public string HostSettingsDir { get; }

        /// <summary>
        /// Gets host version settings directory.
        /// If not specified via constructor, the default location is [<see cref="GlobalSettingsDir"/>]/[host identifier]/[host version].
        /// </summary>
        public string HostVersionSettingsDir { get; }

        private static string GetUserProfileDir (IEnvironment environment)
        {
            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            return environment.GetEnvironmentVariable(isWindows ? "USERPROFILE" : "HOME")
                ?? throw new NotSupportedException("HOME or USERPROFILE environment variable is not defined, the environment is not supported");
        }

        private static string GetDefaultGlobalSettingsDir (string userDir)
        {
            return Path.Combine(userDir, ".templateengine");
        }

        private static string GetDefaultHostSettingsDir(ITemplateEngineHost host, string? userDir = null, string? globalDir = null)
        {
            if (string.IsNullOrWhiteSpace(host.HostIdentifier))
            {
                throw new ArgumentException($"{nameof(host.HostIdentifier)} of {nameof(host)} cannot be null or whitespace.", nameof(host));
            }
            if (string.IsNullOrWhiteSpace(userDir) && string.IsNullOrWhiteSpace(globalDir))
            {
                throw new ArgumentException($"both {nameof(userDir)} and {nameof(globalDir)} cannot be null or whitespace.", nameof(userDir));
            }
            return Path.Combine(globalDir ?? GetDefaultGlobalSettingsDir(userDir!), host.HostIdentifier);
        }

        private static string GetDefaultHostVersionSettingsDir(ITemplateEngineHost host, string? userDir = null, string? globalDir = null)
        {
            if (string.IsNullOrWhiteSpace(host.HostIdentifier))
            {
                throw new ArgumentException($"{nameof(host.HostIdentifier)} of {nameof(host)} cannot be null or whitespace.", nameof(host));
            }
            if (string.IsNullOrWhiteSpace(host.Version))
            {
                throw new ArgumentException($"{nameof(host.Version)} of {nameof(host)} cannot be null or whitespace.", nameof(host));
            }
            if (string.IsNullOrWhiteSpace(userDir) && string.IsNullOrWhiteSpace(globalDir))
            {
                throw new ArgumentException($"both {nameof(userDir)} and {nameof(globalDir)} cannot be null or whitespace.", nameof(userDir));
            }
            return Path.Combine(globalDir ?? GetDefaultGlobalSettingsDir(userDir!), host.HostIdentifier, host.Version);
        }
    }
}
