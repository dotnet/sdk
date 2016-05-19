using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IGenerator : IComponent
    {
        Task Create(ITemplate template, IParameterSet parameters);

        IParameterSet GetParametersForTemplate(ITemplate template);

        IEnumerable<ITemplate> GetTemplatesFromSource(IConfiguredTemplateSource source);

        bool TryGetTemplateFromSource(IConfiguredTemplateSource target, string name, out ITemplate template);
    }
}