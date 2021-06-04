// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Edge
{
    /// <summary>
    /// Default implementation of <see cref="IEngineEnvironmentSettings"/>.
    /// </summary>
    public sealed class EngineEnvironmentSettings : IEngineEnvironmentSettings
    {
        /// <summary>
        /// Creates the instance.
        /// </summary>
        /// <param name="host">template engine host creating the instance.</param>
        /// <param name="virtualizeSettings">if true, the settings directory will be virtualized, so the settings will be stored in memory only.</param>
        /// <param name="settingsLocation">the base location of settings. If specified, the following settings paths will be used: <br/>
        /// - <see cref="IPathInfo.GlobalSettingsDir"/> - [<paramref name="settingsLocation"/>] <br/>
        /// - <see cref="IPathInfo.HostSettingsDir"/> - [<paramref name="settingsLocation"/>]/[<see cref="ITemplateEngineHost.HostIdentifier"/>] <br/>
        /// - <see cref="IPathInfo.HostVersionSettingsDir"/> - [<paramref name="settingsLocation"/>]/[<see cref="ITemplateEngineHost.HostIdentifier"/>]/[<see cref="ITemplateEngineHost.Version"/>]. <br/>
        /// If <paramref name="settingsLocation"/> is specified, do not provide <paramref name="pathInfo"/>.
        /// </param>
        /// <param name="environment">implementation of <see cref="IEnvironment"/> to use. If not specified, <see cref="DefaultEnvironment"/> will be used.</param>
        /// <param name="componentManager">implementation of <see cref="IComponentManager"/> to use. If not specified, built-in implementation will be used.</param>
        /// <param name="pathInfo">implememtation of <see cref="IPathInfo"/> to use. If not specified, <see cref="DefaultPathInfo"/> will be used (if <paramref name="settingsLocation"/> is used, settings location will be overriden as mentioned in <paramref name="settingsLocation"/> description). <br/>
        /// If <paramref name="pathInfo"/> is specified, do not provide <paramref name="settingsLocation"/>.
        /// </param>
        public EngineEnvironmentSettings(
            ITemplateEngineHost host,
            bool virtualizeSettings = false,
            string? settingsLocation = null,
            IEnvironment? environment = null,
            IComponentManager? componentManager = null,
            IPathInfo? pathInfo = null)
        {
            if (pathInfo != null && !string.IsNullOrWhiteSpace(settingsLocation))
            {
                throw new ArgumentException($"{nameof(settingsLocation)} won't be used if {nameof(pathInfo)} is specified.", nameof(settingsLocation));
            }

            Host = host ?? throw new ArgumentNullException(nameof(host));
            Environment = environment ?? new DefaultEnvironment();
            Paths = pathInfo ?? new DefaultPathInfo(this, settingsLocation);
            if (virtualizeSettings)
            {
                Host.VirtualizeDirectory(Paths.GlobalSettingsDir);
            }
            Components = componentManager ?? new ComponentManager(this);
        }

        [Obsolete("ISettingsLoader is obsolete, see obsolete messages for individual properties/methods of ISettingsLoader for details.")]
        public ISettingsLoader SettingsLoader
        {
            get
            {
                throw new NotSupportedException("ISettingsLoader is no longer supported, see Obsolete message for details.");
            }
        }

        public ITemplateEngineHost Host { get; }

        public IEnvironment Environment { get;  }

        public IPathInfo Paths { get;  }

        public IComponentManager Components { get; }
    }
}
