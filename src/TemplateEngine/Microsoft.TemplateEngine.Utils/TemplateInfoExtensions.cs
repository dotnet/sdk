// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
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

        public static ITemplate? LoadTemplate(this ITemplateInfo info, IEngineEnvironmentSettings engineEnvironment, string? baselineName)
        {
            IGenerator? generator;
            if (!engineEnvironment.Components.TryGetComponent(info.GeneratorId, out generator))
            {
                return null;
            }
            IMountPoint mountPoint;
            if (!engineEnvironment.TryGetMountPoint(info.MountPointUri, out mountPoint))
            {
                return null;
            }
            IFile config = mountPoint.FileInfo(info.ConfigPlace);
            IFile? localeConfig = string.IsNullOrEmpty(info.LocaleConfigPlace) ? null : mountPoint.FileInfo(info.LocaleConfigPlace);
            IFile? hostTemplateConfigFile = string.IsNullOrEmpty(info.HostConfigPlace) ? null : mountPoint.FileInfo(info.HostConfigPlace);
            ITemplate template;
            using (Timing.Over(engineEnvironment.Host.Logger, $"Template from config {config.MountPoint.MountPointUri}{config.FullPath}"))
            {
                if (generator!.TryGetTemplateFromConfigInfo(config, out template, localeConfig, hostTemplateConfigFile, baselineName))
                {
                    return template;
                }
                else
                {
                    //TODO: Log the failure to read the template info
                }
            }

            return null;
        }
    }
}
