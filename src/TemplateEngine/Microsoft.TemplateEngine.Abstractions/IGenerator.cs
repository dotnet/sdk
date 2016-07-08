using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IGenerator : IComponent
    {
        Task Create(ITemplate template, IParameterSet parameters);

        IParameterSet GetParametersForTemplate(ITemplate template);

        IEnumerable<ITemplate> GetTemplatesFromSource(IMountPoint source);

        bool TryGetTemplateFromSource(IMountPoint target, string name, out ITemplate template);
    }
}