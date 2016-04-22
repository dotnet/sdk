using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Win32;
using Mutant.Chicken.Abstractions;
using Mutant.Chicken.Expressions.Cpp;
using Mutant.Chicken.Runner;

namespace Mutant.Chicken.Orchestrator.VsTemplates
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

        public IReadOnlyList<IPathMatcher> Exclude => new List<IPathMatcher> { new ExtensionPathMatcher(".vstemplate") };

        public IReadOnlyList<IPathMatcher> Include => new List<IPathMatcher> { new SpecificFilesMatcher(_pathMap.Keys) };

        public IReadOnlyList<IOperationProvider> Operations => new List<IOperationProvider>
        {
            new ExpandVariables(),
            new Conditional("$if$", "$else$", "$elseif$", "$endif$", false, false, CppStyleEvaluatorDefinition.CppStyleEvaluator)
        };

        public VariableCollection RootVariableCollection { get; }

        public IReadOnlyDictionary<IPathMatcher, IRunSpec> Special => _special;

        public bool TryGetTargetRelPath(string sourceRelPath, out string targetRelPath)
        {
            return _pathMap.TryGetValue(sourceRelPath, out targetRelPath);
        }
    }

    internal class NoOpRunSpec : IRunSpec
    {
        private static readonly IReadOnlyList<IOperationProvider> NoOperations = new IOperationProvider[0];

        public bool TryGetTargetRelPath(string sourceRelPath, out string targetRelPath)
        {
            targetRelPath = null;
            return false;
        }

        public IReadOnlyList<IOperationProvider> GetOperations(IReadOnlyList<IOperationProvider> sourceOperations)
        {
            return NoOperations;
        }

        public VariableCollection ProduceCollection(VariableCollection parent)
        {
            return new VariableCollection();
        }
    }

    internal class SpecificFilesMatcher : IPathMatcher
    {
        private HashSet<string> _files;

        public SpecificFilesMatcher(IEnumerable<string> files)
        {
            _files = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);
        }

        public bool IsMatch(string path)
        {
            return _files.Contains(path);
        }
    }
}