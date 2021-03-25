// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.Installer;

namespace Microsoft.TemplateEngine.Abstractions.TemplatePackage
{
    /// <summary>
    /// This is more advanced <see cref="ITemplatePackage"/>. Managed means that this templates source can be:<br/>
    /// Uninstalled via <see cref="ManagedProvider"/>.<see cref="IManagedTemplatePackageProvider.UninstallAsync(IManagedTemplatePackage)"/><br/>
    /// Check for latest version via <see cref="ManagedProvider"/>.<see cref="IManagedTemplatePackageProvider.GetLatestVersionsAsync(IEnumerable{IManagedTemplatePackage}, System.Threading.CancellationToken)"/><br/>
    /// Updated  via <see cref="ManagedProvider"/>.<see cref="IManagedTemplatePackageProvider.UpdateAsync(IEnumerable{ManagedTemplatePackageUpdate})"/>
    /// </summary>
    public interface IManagedTemplatePackage : ITemplatePackage
    {
        /// <summary>
        /// The name to be used when displaying source in UI.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// This can be NuGet PackageId, path to .nupkg, folder name, or something similar that
        /// identifies this templates source to user.
        /// </summary>
        string Identifier { get; }

        /// <summary>
        /// Installer that created this source.
        /// This serves as helper for grouping sources by installer
        /// so caller doesn't need to keep track of installer->source relation.
        /// </summary>
        IInstaller Installer { get; }

        /// <summary>
        /// Installer that created this source.
        /// This serves as helper for grouping sources by <see cref="IManagedTemplatePackageProvider"/>
        /// so caller doesn't need to keep track of "managed provider"->"source" relation.
        /// </summary>
        IManagedTemplatePackageProvider ManagedProvider { get; }

        /// <summary>
        /// Version of templates source.
        /// </summary>
        string Version { get; }

        /// <summary>
        /// This is list of details that we show to user about this templates source like author and other information
        /// that might be important to user.
        /// </summary>
        IReadOnlyDictionary<string, string> GetDisplayDetails();
    }
}
