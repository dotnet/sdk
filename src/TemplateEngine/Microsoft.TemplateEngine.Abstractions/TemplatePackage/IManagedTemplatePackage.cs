// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.Installer;

namespace Microsoft.TemplateEngine.Abstractions.TemplatePackage
{
    /// <summary>
    /// Represents the package that can be managed by <see cref="IManagedTemplatePackageProvider"/>. <see cref="IManagedTemplatePackageProvider"/> can additionally install, update and uninstall template package.
    /// </summary>
    public interface IManagedTemplatePackage : ITemplatePackage
    {
        /// <summary>
        /// Gets the name to be used when displaying template package in UI.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Gets the identifier of template package.
        /// Identifier should be unique in scope of <see cref="IManagedTemplatePackageProvider"/> that manages the <see cref="IManagedTemplatePackage"/>.
        /// </summary>
        /// <remarks>
        /// This can be NuGet PackageId, path to .nupkg, folder name, depends on <see cref="IInstaller"/> implementation.
        /// </remarks>
        string Identifier { get; }

        /// <summary>
        /// Gets <see cref="IInstaller"/> that installed the template package.
        /// This serves as helper for grouping template package by installer so caller doesn't need to keep track of installer->template package relation.
        /// </summary>
        IInstaller Installer { get; }

        /// <summary>
        /// Gets <see cref="IManagedTemplatePackageProvider"/> that manages the template package.
        /// This serves as helper for grouping packages by <see cref="IManagedTemplatePackageProvider"/>
        /// so caller doesn't need to keep track of "managed provider"->"managed package" relation.
        /// </summary>
        IManagedTemplatePackageProvider ManagedProvider { get; }

        /// <summary>
        /// Gets the version of the template package.
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Gets additional details about template package. The details depend on <see cref="IInstaller"/> implementation.
        /// </summary>
        IReadOnlyDictionary<string, string> GetDetails();
    }
}
