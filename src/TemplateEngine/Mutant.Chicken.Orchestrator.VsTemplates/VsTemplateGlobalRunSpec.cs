using System;
using System.Collections.Generic;
using Mutant.Chicken.Abstractions;
using Mutant.Chicken.Expressions.Cpp;
using Mutant.Chicken.Runner;

namespace Mutant.Chicken.Orchestrator.VsTemplates
{
    internal class VsTemplateGlobalRunSpec : IGlobalRunSpec
    {
        public VsTemplateGlobalRunSpec(IParameterSet parameters)
        {
            VariableCollection sys = new VariableCollection(VariableCollection.Environment())
            {
                ["$year$"] = DateTime.Now.Year.ToString(),
                ["$registeredorganization$"] = "",
                ["$targetframeworkversion$"] = "4.6"
            };

            for (int i = 0; i < 10; ++i)
            {
                sys[$"$guid{i}$"] = Guid.NewGuid();
            }

            VariableCollection vc = new VariableCollection(sys);

            foreach (var param in parameters.ParameterValues)
            {
                vc[$"${param.Key.Name}$"] = param.Value;
            }

            RootVariableCollection = vc;
        }

        public IReadOnlyList<IPathMatcher> Exclude => new List<IPathMatcher> { new ExtensionPathMatcher(".vstemplate") };

        public IReadOnlyList<IPathMatcher> Include => new List<IPathMatcher> { new AllFilesMatcher() };

        public IReadOnlyList<IOperationProvider> Operations => new List<IOperationProvider>
        {
            new ExpandVariables(),
            new Conditional("$if$", "$else$", "$elseif$", "$endif$", false, false, CppStyleEvaluatorDefinition.CppStyleEvaluator)
        };

        public VariableCollection RootVariableCollection { get; }

        public IReadOnlyDictionary<IPathMatcher, IRunSpec> Special => new Dictionary<IPathMatcher, IRunSpec>();
    }
}