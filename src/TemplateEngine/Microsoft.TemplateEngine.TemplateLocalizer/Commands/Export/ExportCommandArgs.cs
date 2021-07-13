// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.TemplateLocalizer.Commands.Export
{
    /// <summary>
    /// Model class representing the arguments of <see cref="ExportCommand"/>.
    /// </summary>
    internal sealed class ExportCommandArgs
    {
        public ExportCommandArgs(IEnumerable<string>? templatePath, IEnumerable<string>? language, bool recursive, bool dryRun)
        {
            TemplatePaths = templatePath;
            Languages = language;
            SearchSubdirectories = recursive;
            DryRun = dryRun;
        }

        /// <summary>
        /// Gets the paths to template.json files or containing directories.
        /// </summary>
        public IEnumerable<string>? TemplatePaths { get; init; }

        /// <summary>
        /// Gets the languages for which the localization files should be created.
        /// If null, the default language set supported by dotnet will be used.
        /// </summary>
        public IEnumerable<string>? Languages { get; init; }

        /// <summary>
        /// Gets if subdirectories should be searched by <see cref="TemplateJsonProviders"/>.
        /// </summary>
        public bool SearchSubdirectories { get; init; }

        /// <summary>
        /// Gets the value indicating whether the export process should skip
        /// flushing the file changes to file system.
        /// </summary>
        public bool DryRun { get; init; }
    }
}
