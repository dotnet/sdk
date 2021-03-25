// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Abstractions.Installer
{
    /// <summary>
    /// Defines constants used for installer implementation.
    /// </summary>
    public static class InstallerConstants
    {
        /// <summary>
        /// Defines the key for <see cref="InstallRequest.Details"/> to specify additional NuGet sources to be used on installation. Supported by NuGet installer.
        /// </summary>
        /// <remarks><seealso cref="NuGetSourcesSeparator"/></remarks>
        public const string NuGetSourcesKey = "NuGetSources";

        /// <summary>
        /// Defines the separator to be used when specifying multiple additional NuGet sources to be used on installation. Supported by NuGet installer.
        /// </summary>
        /// <remarks><seealso cref="NuGetSourcesKey"/></remarks>
        public const char NuGetSourcesSeparator = ';';

        /// <summary>
        /// Defines the key for <see cref="InstallRequest.Details"/> to specify that interactive mode should be used on installation. Supported by NuGet installer.
        /// </summary>
        public const string InteractiveModeKey = "Interactive";
    }
}
