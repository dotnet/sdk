using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Win32;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Engine;
using Microsoft.TemplateEngine.Abstractions.Runner;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Expressions.Cpp;

namespace Microsoft.TemplateEngine.Orchestrator.VsTemplates
{
    internal class VsTemplateGlobalRunSpec : IGlobalRunSpec
    {
        private readonly IReadOnlyDictionary<string, string> _pathMap;
        private readonly Dictionary<IPathMatcher, IRunSpec> _special;

        public VsTemplateGlobalRunSpec(IParameterSet parameters, IReadOnlyDictionary<string, string> pathMap, IReadOnlyList<string> copyOnly)
        {
            _pathMap = pathMap;
            _special = new Dictionary<IPathMatcher, IRunSpec>(copyOnly.Count);

            foreach (string copyOnlyFile in copyOnly)
            {
                _special[new SpecificFilesMatcher(new[] { copyOnlyFile })] = new NoOpRunSpec();
            }

            string registeredOrganization;

            using (RegistryKey key = Registry.LocalMachine?.OpenSubKey(@"Software\Microsoft\Windows NT\CurrentVersion"))
            {
                registeredOrganization = key?.GetValue("RegisteredOrganization", "")?.ToString() ?? "";
            }

            VariableCollection sys = new VariableCollection(VariableCollection.Environment(true, false, "${0}$"))
            {
                ["$year$"] = DateTime.Now.Year.ToString(),
                ["$registeredorganization$"] = registeredOrganization,
                ["$targetframeworkversion$"] = "4.6",
                ["$machinename$"] = Environment.MachineName,
                ["$clrVersion$"] = typeof(VsTemplateGenerator).GetTypeInfo().Assembly.ImageRuntimeVersion,
                ["$registeredorganization$"] = registeredOrganization,
                ["$time$"] = DateTime.Now.ToString("G"),
                ["$specificsolutionname$"] = "",
                ["$webnamespace$"] = ""
            };

            for (int i = 0; i < 11; ++i)
            {
                sys[$"$guid{i}$"] = Guid.NewGuid();
            }

            VariableCollection vc = new VariableCollection(sys);

            foreach (ITemplateParameter param in parameters.Parameters)
            {
                if (param.Priority != TemplateParameterPriority.Required)
                {
                    vc[$"${param.Name}$"] = param.DefaultValue;
                }
            }

            foreach (KeyValuePair<ITemplateParameter, string> param in parameters.ParameterValues)
            {
                vc[$"${param.Key.Name}$"] = param.Value;
            }

            RootVariableCollection = vc;
        }

        public IReadOnlyList<IPathMatcher> CopyOnly
        {
            get
            {
                //TODO: Make this the set of files from the vstemplate that aren't supposed to have replacements processed
                return new IPathMatcher[0];
            }
        }

        public IReadOnlyList<IPathMatcher> Exclude => new List<IPathMatcher> { new ExtensionPathMatcher(".vstemplate") };

        public IReadOnlyList<IPathMatcher> Include => new List<IPathMatcher> { new SpecificFilesMatcher(_pathMap.Keys) };

        public IReadOnlyList<IOperationProvider> Operations => new List<IOperationProvider>
        {
            new ExpandVariables(),
            new Conditional("$if$", "$else$", "$elseif$", "$endif$", false, false, CppStyleEvaluatorDefinition.CppStyleEvaluator)
        };

        public IVariableCollection RootVariableCollection { get; }

        public IReadOnlyDictionary<IPathMatcher, IRunSpec> Special => _special;

        public bool TryGetTargetRelPath(string sourceRelPath, out string targetRelPath)
        {
            return _pathMap.TryGetValue(sourceRelPath, out targetRelPath);
        }
    }
}