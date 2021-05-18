// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.TemplateEngine.Utils
{
    /// <summary>
    /// The class provides ITemplateInfo extension methods.
    /// </summary>
    public static class TemplateInfoExtensions
    {
        /// <summary>
        /// Gets the language defined in <paramref name="template"/>.
        /// </summary>
        /// <param name="template">template definition.</param>
        /// <returns>The language defined in the template or null if no language is defined.</returns>
        /// <remarks>The tags are read in <see cref="SimpleConfigModel.ConvertedDeprecatedTagsToParameterSymbols"/> method. The single value for the tag is guaranteed.</remarks>
        public static string? GetLanguage(this ITemplateInfo template)
        {
            return template.GetTagValue("language");
        }

        /// <summary>
        /// Gets the type defined in <paramref name="template"/>.
        /// </summary>
        /// <param name="template">template definition.</param>
        /// <returns>The type defined in the template or null if no type is defined.</returns>
        /// <remarks>The tags are read in <see cref="SimpleConfigModel.ConvertedDeprecatedTagsToParameterSymbols"/> method. The single value for the tag is guaranteed.</remarks>
        public static string? GetTemplateType(this ITemplateInfo template)
        {
            return template.GetTagValue("type");
        }

        /// <summary>
        /// Helper method that returns <see cref="ITemplatePackage"/> that contains <paramref name="template"/>.
        /// </summary>
        public static async Task<ITemplatePackage> GetTemplatePackageAsync(this ITemplateInfo template, IEngineEnvironmentSettings settings)
        {
            IReadOnlyList<ITemplatePackage> templatePackages = await settings.SettingsLoader.TemplatePackagesManager.GetTemplatePackagesAsync().ConfigureAwait(false);
            return templatePackages.Single(s => s.MountPointUri == template.MountPointUri);
        }

        /// <summary>
        /// Gets the possible values for the tag <paramref name="tagName"/> in <paramref name="template"/>.
        /// </summary>
        /// <param name="template">template definition.</param>
        /// <param name="tagName">tag name.</param>
        /// <returns>The value of tag defined in the template or null if the tag is not defined in the template.</returns>
        public static string? GetTagValue(this ITemplateInfo template, string tagName)
        {
            if (template.TagsCollection == null || !template.TagsCollection.TryGetValue(tagName, out string tag))
            {
                return null;
            }
            return tag;
        }

        /// <summary>
        /// Gets the template parameter by <paramref name="parameterName"/>.
        /// </summary>
        /// <param name="template">template.</param>
        /// <param name="parameterName">parameter name.</param>
        /// <returns> first <see cref="ITemplateParameter"/> with <paramref name="parameterName"/> or null if the parameter with such name does not exist.</returns>
        public static ITemplateParameter? GetParameter(this ITemplateInfo template, string parameterName)
        {
            return template.Parameters.FirstOrDefault(
                param => param.Name.Equals(parameterName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets the choice template parameter by <paramref name="parameterName"/>.
        /// </summary>
        /// <param name="template">template.</param>
        /// <param name="parameterName">parameter name.</param>
        /// <returns> first choice <see cref="ITemplateParameter"/> with <paramref name="parameterName"/> or null if the parameter with such name does not exist.</returns>
        public static ITemplateParameter? GetChoiceParameter(this ITemplateInfo template, string parameterName)
        {
            return template.Parameters.FirstOrDefault(
 param => param.Name.Equals(parameterName, StringComparison.OrdinalIgnoreCase)
                                  && param.DataType.Equals("choice", StringComparison.OrdinalIgnoreCase));
        }
    }
}
