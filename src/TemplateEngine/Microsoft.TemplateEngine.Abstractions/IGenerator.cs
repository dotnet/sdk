// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// Defines template generator supported by template engine.
    /// This interface should be implemented to create generator loadable by template engine infrastructure.
    /// </summary>
    public interface IGenerator : IIdentifiedComponent
    {
        /// <summary>
        /// Generates the <paramref name="template"/> with given parameters.
        /// </summary>
        /// <param name="environmentSettings">template engine environment settings.</param>
        /// <param name="template">template to generate.</param>
        /// <param name="parameters">template parameters.</param>
        /// <param name="targetDirectory">target output directory to generate template to.</param>
        /// <param name="cancellationToken">cancellation token.</param>
        /// <returns><see cref="ICreationResult"/> containing post actions and primary outputs after template generation.</returns>
        Task<ICreationResult> CreateAsync(
            IEngineEnvironmentSettings environmentSettings,
            ITemplate template,
            IParameterSet parameters,
            string targetDirectory,
            CancellationToken cancellationToken);

        /// <summary>
        /// Dry runs the <paramref name="template"/> with given parameters.
        /// </summary>
        /// <param name="environmentSettings">template engine environment settings.</param>
        /// <param name="template">template to dry run.</param>
        /// <param name="parameters">template parameters.</param>
        /// <param name="targetDirectory">target output directory to generate template to.</param>
        /// <param name="cancellationToken">cancellation token.</param>
        /// <returns><see cref="ICreationEffects"/> containing file changes, post actions and primary outputs that would have been done after template generation.</returns>
        Task<ICreationEffects> GetCreationEffectsAsync(
            IEngineEnvironmentSettings environmentSettings,
            ITemplate template,
            IParameterSet parameters,
            string targetDirectory,
            CancellationToken cancellationToken);

        /// <summary>
        /// Returns a <see cref="IParameterSet"/> for the given <paramref name="template"/>.
        /// This method returns the list of input parameters that can be set by the host / Edge before running the template.
        /// After setting the values the host should pass <see cref="IParameterSet"/> to <see cref="GetCreationEffectsAsync(IEngineEnvironmentSettings, ITemplate, IParameterSet, string, CancellationToken)"/> or <see cref="CreateAsync(IEngineEnvironmentSettings, ITemplate, IParameterSet, string, CancellationToken)"/> methods.
        /// Host may use <see cref="ConvertParameterValueToType(IEngineEnvironmentSettings, ITemplateParameter, string, out bool)"/> to convert input value to required parameter type.
        /// </summary>
        /// <param name="environmentSettings">template engine environment settings.</param>
        /// <param name="template">template to get parameters from.</param>
        /// <returns><see cref="IParameterSet"/> with parameters available in <paramref name="template"/>.</returns>
        IParameterSet GetParametersForTemplate(IEngineEnvironmentSettings environmentSettings, ITemplate template);

        /// <summary>
        /// Gets an <see cref="ITemplate"/> from the given <see cref="IFileSystemInfo" /> configuration entry.
        /// </summary>
        /// <param name="config">configuration file.</param>
        /// <param name="template">loaded template.</param>
        /// <param name="localeConfig">localization configuration file.</param>
        /// <param name="hostTemplateConfigFile">host template configuration file.</param>
        /// <param name="baselineName">baseline to load.</param>
        /// <returns>true if <paramref name="template"/> can be read, false otherwise.</returns>
        bool TryGetTemplateFromConfigInfo(IFileSystemInfo config, out ITemplate template, IFileSystemInfo localeConfig, IFile hostTemplateConfigFile, string baselineName = null);

        /// <summary>
        /// Scans the given mount point <paramref name="source"/> for templates and returns the list of <see cref="ITemplate"/>s that are found.
        /// </summary>
        /// <param name="source">the mount point to load templates from.</param>
        /// <param name="localizations">localization information for templates.</param>
        /// <returns>the list of templates found in <paramref name="source"/> mount point.</returns>
        IList<ITemplate> GetTemplatesAndLangpacksFromDir(IMountPoint source, out IList<ILocalizationLocator> localizations);

        /// <summary>
        /// Converts the given <paramref name="untypedValue"/> to the type of <paramref name="parameter"/>.
        /// </summary>
        /// <param name="environmentSettings">template engine environment settings.</param>
        /// <param name="parameter">template parameter defining the type.</param>
        /// <param name="untypedValue">the value to be converted to parameter type.</param>
        /// <param name="valueResolutionError">true if value could not be converted, false otherwise.</param>
        /// <returns>the converted <paramref name="untypedValue"/> value to the type of <paramref name="parameter"/> or null if the value cannot be converted.</returns>
        object ConvertParameterValueToType(IEngineEnvironmentSettings environmentSettings, ITemplateParameter parameter, string untypedValue, out bool valueResolutionError);
    }
}
