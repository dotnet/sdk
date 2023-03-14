// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Globalization;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Localization;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Validation
{
    /// <summary>
    /// Information available for template validation.
    /// </summary>
    internal interface ITemplateValidationInfo
    {
        /// <summary>
        /// Gets template configuration.
        /// </summary>
        TemplateConfigModel ConfigModel { get; }

        /// <summary>
        /// Gets source template directory.
        /// </summary>
        IDirectory TemplateSourceRoot { get; }

        /// <summary>
        /// Gets template configuration file.
        /// </summary>
        IFile? ConfigFile { get; }

        /// <summary>
        /// Gets localizations available for the template.
        /// </summary>
        IReadOnlyDictionary<CultureInfo, TemplateLocalizationInfo> Localizations { get; }

        /// <summary>
        /// Gets host files available for the template.
        /// </summary>
        IReadOnlyDictionary<string, IFile> HostFiles { get; }

        /// <summary>
        /// Gets the list of validation errors for template.
        /// </summary>
        IReadOnlyList<IValidationEntry> ValidationErrors { get; }

        /// <summary>
        /// Adds validation error to the list.
        /// </summary>
        /// <param name="validationEntry"></param>
        void AddValidationError(IValidationEntry validationEntry);
    }
}
