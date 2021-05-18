// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;

namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// Represents template engine environment settings.
    /// </summary>
    public interface IEngineEnvironmentSettings
    {
        /// <summary>
        /// Manages template cache and template engine settings.
        /// </summary>
        [Obsolete("ISettingsLoader is obsolete.")]
        ISettingsLoader SettingsLoader { get; }

        /// <summary>
        /// Gets host-specific properties, loggers and provides access to file system.
        /// </summary>
        ITemplateEngineHost Host { get; }

        /// <summary>
        /// Provides access to environment settings, such as environment variables.
        /// </summary>
        IEnvironment Environment { get; }

        /// <summary>
        /// Gets main file paths used by template engine.
        /// </summary>
        IPathInfo Paths { get; }

        /// <summary>
        /// Component manager for this instance of settings.
        /// </summary>
        IComponentManager Components { get; }
    }
}
