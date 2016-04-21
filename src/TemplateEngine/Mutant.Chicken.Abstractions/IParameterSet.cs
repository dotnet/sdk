using System.Collections.Generic;

namespace Mutant.Chicken.Abstractions
{
    public interface IParameterSet
    {
        IEnumerable<ITemplateParameter> Parameters { get; }

        IEnumerable<string> RequiredBrokerCapabilities { get; }

        IDictionary<ITemplateParameter, string> ParameterValues { get; }

        bool TryGetParameter(string name, out ITemplateParameter parameter);
    }
}