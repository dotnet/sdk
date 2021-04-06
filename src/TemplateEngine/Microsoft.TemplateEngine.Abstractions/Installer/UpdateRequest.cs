// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.TemplateEngine.Abstractions.Installer
{
    /// <summary>
    /// The template package update request to be processed by <see cref="IInstaller.UpdateAsync"/>.
    /// </summary>
    public sealed class UpdateRequest
    {
        public UpdateRequest(IManagedTemplatePackage templatePackage, string targetVersion)
        {
            TemplatePackage = templatePackage ?? throw new ArgumentNullException(nameof(templatePackage));
            if (string.IsNullOrWhiteSpace(targetVersion))
            {
                throw new ArgumentException("Version cannot be null or empty", nameof(targetVersion));
            }
            Version = targetVersion;
        }

        /// <summary>
        /// <see cref="IManagedTemplatePackage"/> to be updated.
        /// </summary>
        public IManagedTemplatePackage TemplatePackage { get; }

        /// <summary>
        /// Target version for the update.
        /// </summary>
        public string Version { get; }
    }
}
