// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions.Installer
{
    /// <summary>
    /// The template package installation request to be processed by <see cref="IInstaller.InstallAsync"/>.
    /// </summary>
    public sealed class InstallRequest
    {
        public InstallRequest (string identifier, string version = null, string installerName = null, Dictionary<string, string> details = null)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new ArgumentException("identifier cannot be null or empty", nameof(identifier));
            }
            PackageIdentifier = identifier;
            Version = version;
            InstallerName = installerName;
            Details = details ?? new Dictionary<string, string>();
        }

        /// <summary>
        /// Name to be used when display the request.
        /// </summary>
        public string DisplayName => string.IsNullOrWhiteSpace(Version) ? PackageIdentifier : $"{PackageIdentifier}::{Version}";

        /// <summary>
        /// Installer to be used to install the request.
        /// </summary>
        /// <remarks>
        /// This can be null, but if multiple installers return <c>true</c> from <see cref="IInstaller.CanInstallAsync"/>
        /// installation will fail. The application should select the installer to be used in this case.
        /// </remarks>
        public string InstallerName { get; private set; }

        /// <summary>
        /// The identifier for template package to be installed. The format of identifier is defined by <see cref="IInstaller"/> implementation.
        /// </summary>
        /// <remarks>
        /// Could be folder name, NuGet PackageId, path to .nupkg...
        /// </remarks>
        public string PackageIdentifier { get; private set; }

        /// <summary>
        /// Specific version to be installed or null to install latest.
        /// </summary>
        public string Version { get; private set; }

        /// <summary>
        /// Additional details, like NuGet Server(Source), that specific installer uses.
        /// </summary>
        /// <remarks>The keys supported by default installers are defined in <see cref="InstallerConstants"/>.</remarks>
        public Dictionary<string, string> Details { get; private set; }

        public override string ToString() => DisplayName;
    }
}
