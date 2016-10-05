using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IGenerator : IIdentifiedComponent
    {
        Task Create(ITemplate template, IParameterSet parameters, IComponentManager componentManager, out ICreationResult creationResult);

        IParameterSet GetParametersForTemplate(ITemplate template);

        IEnumerable<ITemplate> GetTemplatesFromSource(IMountPoint source);

        bool TryGetTemplateFromConfig(IFileSystemInfo config, out ITemplate template, string localizationFile);

        bool TryGetTemplateFromSource(IMountPoint target, string name, out ITemplate template);

        object ConvertParameterValueToType(ITemplateParameter parameter, string untypedValue);
    }
}