// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// Defines the location of a template.
    /// </summary>
    public interface ITemplateLocator
    {
        /// <summary>
        /// Gets generator ID the template should be processed by.
        /// </summary>
        Guid GeneratorId { get; }

        /// <summary>
        /// Gets template mount point URI.
        /// </summary>
        string MountPointUri { get; }

        /// <summary>
        /// Gets the path to template configuration inside mount point.
        /// </summary>
        string ConfigPlace { get; }
    }

    public interface IExtendedTemplateLocator : ITemplateLocator
    {
        /// <summary>
        /// Gets the path to the localization configuration inside the mount point.
        /// </summary>
        string? LocaleConfigPlace { get; }

        /// <summary>
        /// Gets the path to the host configuration inside the mount point.
        /// </summary>
        string? HostConfigPlace { get; }
    }
}
