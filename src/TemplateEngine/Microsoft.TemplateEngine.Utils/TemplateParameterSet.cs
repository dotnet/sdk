using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Utils
{
    public class TemplateParameterSet : IParameterSet
    {
        private readonly IDictionary<string, ITemplateParameter> _parameters = new Dictionary<string, ITemplateParameter>(StringComparer.OrdinalIgnoreCase);

        public TemplateParameterSet(IList<ITemplateParameter> parameters)
        {
            foreach (ITemplateParameter param in parameters)
            {
                _parameters.Add(param.Name, param);
            }
        }

        public IEnumerable<ITemplateParameter> ParameterDefinitions => _parameters.Values;

        public IEnumerable<string> RequiredBrokerCapabilities => Enumerable.Empty<string>();

        public IDictionary<ITemplateParameter, object> ResolvedValues { get; } = new Dictionary<ITemplateParameter, object>();

        public bool TryGetParameterDefinition(string name, out ITemplateParameter parameter)
        {
            return _parameters.TryGetValue(name, out parameter);
        }
    }
}
