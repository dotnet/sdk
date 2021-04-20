// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IGenerator : IIdentifiedComponent
    {
        Task<ICreationResult> CreateAsync(IEngineEnvironmentSettings environmentSettings, ITemplate template, IParameterSet parameters, IComponentManager componentManager, string targetDirectory);

        ICreationEffects GetCreationEffects(IEngineEnvironmentSettings environmentSettings, ITemplate template, IParameterSet parameters, IComponentManager componentManager, string targetDirectory);

        IParameterSet GetParametersForTemplate(IEngineEnvironmentSettings environmentSettings, ITemplate template);

        bool TryGetTemplateFromConfigInfo(IFileSystemInfo config, out ITemplate template, IFileSystemInfo localeConfig, IFile hostTemplateConfigFile, string baselineName = null);

        IList<ITemplate> GetTemplatesAndLangpacksFromDir(IMountPoint source, out IList<ILocalizationLocator> localizations);

        object ConvertParameterValueToType(IEngineEnvironmentSettings environmentSettings, ITemplateParameter parameter, string untypedValue, out bool valueResolutionError);
    }
}
