using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.Runner;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IGenerator : IIdentifiedComponent
    {
        Task Create(IOrchestrator basicOrchestrator, ITemplate template, IParameterSet parameters);

        IParameterSet GetParametersForTemplate(ITemplate template);

        IEnumerable<ITemplate> GetTemplatesFromSource(IMountPoint source);

        bool TryGetTemplateFromConfig(IFileSystemInfo config, out ITemplate template);

        bool TryGetTemplateFromSource(IMountPoint target, string name, out ITemplate template);
    }
}