using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IGenerator : IIdentifiedComponent
    {
        Task Create(ITemplateEngineHost host, ITemplate template, IParameterSet parameters, IComponentManager componentManager);

        IParameterSet GetParametersForTemplate(ITemplate template);

        IEnumerable<ITemplate> GetTemplatesFromSource(IMountPoint source, IComponentManager componentManager);

        bool TryGetTemplateFromConfig(IFileSystemInfo config, IComponentManager componentManager, out ITemplate template);

        bool TryGetTemplateFromSource(IMountPoint target, string name, IComponentManager componentManager, out ITemplate template);

        object ConvertParameterValueToType(ITemplateParameter parameter, string untypedValue);
    }
}