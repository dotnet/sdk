using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IGenerator : IIdentifiedComponent
    {
        Task Create(ITemplate template, IParameterSet parameters, IComponentManager componentManager, out ICreationResult creationResult);

        IParameterSet GetParametersForTemplate(ITemplate template);

        bool TryGetTemplateFromConfigInfo(IFileSystemInfo config, out ITemplate template, IFileSystemInfo localeConfig);

        IList<ITemplate> GetTemplatesAndLangpacksFromDir(IMountPoint source, out IList<ILocalizationLocator> localizations);

        object ConvertParameterValueToType(ITemplateParameter parameter, string untypedValue);
    }
}