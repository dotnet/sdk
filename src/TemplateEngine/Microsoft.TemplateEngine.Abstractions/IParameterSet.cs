using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IParameterSet
    {
        IEnumerable<ITemplateParameter> ParameterDefinitions { get; }

        IEnumerable<string> RequiredBrokerCapabilities { get; }

        IDictionary<ITemplateParameter, object> ResolvedValues { get; }

        bool TryGetParameterDefinition(string name, out ITemplateParameter parameter);
    }
}
