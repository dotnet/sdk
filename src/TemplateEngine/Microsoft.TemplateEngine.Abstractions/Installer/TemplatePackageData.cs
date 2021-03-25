// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.TemplateEngine.Abstractions.Installer
{
    /// <summary>
    /// Represents serialized data for <see cref="ITemplatePackage"/>. Used to store information about template packages managed by built in providers.
    /// </summary>
    /// <remarks> Used in <see cref="ISerializableInstaller"/> methods for serialization purposes.</remarks>
    public struct TemplatePackageData
    {
        /// <summary>
        /// Details for the template package. Applicable for <see cref="IManagedTemplatePackage"/> only.
        /// </summary>
        /// <remarks>
        /// Additional properties required for certain <see cref="IManagedTemplatePackage"/> implementations should be stored in this collection.
        /// </remarks>
        public IReadOnlyDictionary<string, string> Details { get; set; }

        /// <summary>
        /// The ID of <see cref="IInstaller"/> that installed the template package. Applicable for <see cref="IManagedTemplatePackage"/> only.
        /// </summary>
        public Guid InstallerId { get; set; }

        /// <summary>
        /// The last time <see cref="ITemplatePackage"/> was changed.
        /// </summary>
        public DateTime LastChangeTime { get; set; }

        /// <summary>
        /// <see cref="ITemplatePackage.MountPointUri"/>.
        /// </summary>
        public string MountPointUri { get; set; }
    }
}
