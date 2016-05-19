using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IParameterSet
    {
        IEnumerable<ITemplateParameter> Parameters { get; }

        IEnumerable<string> RequiredBrokerCapabilities { get; }

        IDictionary<ITemplateParameter, string> ParameterValues { get; }

        bool TryGetParameter(string name, out ITemplateParameter parameter);
    }
}