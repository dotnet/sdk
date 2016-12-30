
//using Microsoft.TemplateEngine.Abstractions;
//using Microsoft.TemplateEngine.Orchestrator.RunnableProjects;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;

//namespace Microsoft.TemplateEngine.Mocks
//{
//    public class MockParameterSet : IParameterSet
//    {
//        private readonly IDictionary<string, ITemplateParameter> _parameters = new Dictionary<string, ITemplateParameter>(StringComparer.OrdinalIgnoreCase);

//        public MockParameterSet(IRunnableProjectConfig config)
//        {
//            foreach (KeyValuePair<string, MockParameter> p in config.Parameters)
//            {
//                p.Value.Name = p.Key;
//                _parameters[p.Key] = p.Value;
//            }
//        }

//        public IEnumerable<ITemplateParameter> ParameterDefinitions => _parameters.Values;

//        public IDictionary<ITemplateParameter, object> ResolvedValues { get; } = new Dictionary<ITemplateParameter, object>();

//        public IEnumerable<string> RequiredBrokerCapabilities => Enumerable.Empty<string>();

//        public void AddParameter(ITemplateParameter param)
//        {
//            _parameters[param.Name] = param;
//        }

//        public bool TryGetParameterDefinition(string name, out ITemplateParameter parameter)
//        {
//            if (_parameters.TryGetValue(name, out parameter))
//            {
//                return true;
//            }

//            parameter = new MockParameter
//            {
//                Name = name,
//                Requirement = TemplateParameterPriority.Optional,
//                IsVariable = true,
//                Type = "string"
//            };

//            return true;
//        }
//    }
//}
