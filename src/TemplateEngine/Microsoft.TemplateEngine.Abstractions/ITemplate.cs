// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// Defines the template that can be run by <see cref="IGenerator"/>.
    /// </summary>
    public interface ITemplate : ITemplateInfo
    {
        /// <summary>
        /// Gets generator that runs the template.
        /// </summary>
        IGenerator Generator { get; }

        /// <summary>
        /// Gets configuration file system entry.
        /// </summary>
        IFileSystemInfo Configuration { get; }

        /// <summary>
        /// Gets localization file system entry.
        /// </summary>
        IFileSystemInfo LocaleConfiguration { get; }

        /// <summary>
        /// Gets directory with template source files.
        /// </summary>
        IDirectory TemplateSourceRoot { get; }

        /// <summary>
        /// Indicates whether he template should be created in a subdirectory under the output directory.
        /// </summary>
        bool IsNameAgreementWithFolderPreferred { get; }
    }
}
