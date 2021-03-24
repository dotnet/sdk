using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.TemplateEngine.Abstractions.Installer
{
    public class InstallRequest
    {
        public string DisplayName => string.IsNullOrWhiteSpace(Version) ? Identifier : $"{Identifier}::{Version}";

        /// <summary>
        /// This can be null, but if multiple installers return <c>true</c> from <see cref="IInstaller.CanInstallAsync"/>
        /// installation will fail. Application should give user list of <see cref="IInstaller.Name"/> that returned
        /// <c>true</c> on <see cref="IInstaller.CanInstallAsync"/>. And ability for user to set this property.
        /// </summary>
        public string InstallerName { get; set; }

        /// <summary>
        /// Could be folder name, NuGet PackageId, path to .nupkg...
        /// </summary>
        public string Identifier { get; set; }

        /// <summary>
        /// Specific version to be installed or null to install latest.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Additional details, like NuGet Server(Source), that specific installer uses.
        /// </summary>
        public Dictionary<string, string> Details { get; set; }

        public override string ToString() => DisplayName;
    }
}
