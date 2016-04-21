using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Mutant.Chicken.Abstractions;

namespace Mutant.Chicken.Orchestrator.VsTemplates
{
    public class VsTemplateGenerator : IGenerator
    {
        public string Name => "VS Templates";

        public Task Create(ITemplate template, IParameterSet parameters)
        {
            ProcessParameters(parameters);
            VsTemplate tmplt = (VsTemplate)template;
            //XElement defaultName = tmplt.VsTemplateFile.Root.Descendants().FirstOrDefault(x => x.Name.LocalName == "DefaultName");
            //IEnumerable<XElement> projects = tmplt.VsTemplateFile.Root.Descendants().Where(x => x.Name.LocalName == "Project");
            
            VsTemplateOrchestrator o = new VsTemplateOrchestrator();
            string dir = Path.GetDirectoryName(tmplt.SourceFile.FullPath);
            o.Run(new VsTemplateGlobalRunSpec(parameters), dir, Directory.GetCurrentDirectory());
            return Task.FromResult(true);
        }

        public IParameterSet GetParametersForTemplate(ITemplate template)
        {
            return new ParameterSet();
        }

        public IEnumerable<ITemplate> GetTemplatesFromSource(IConfiguredTemplateSource source)
        {
            foreach (var entry in source.Entries)
            {
                if (entry.Kind == TemplateSourceEntryKind.File && entry.FullPath.EndsWith(".vstemplate"))
                {
                    VsTemplate tmp = null;
                    try
                    {
                        tmp = new VsTemplate((TemplateSourceFile)entry, source, this);
                    }
                    catch
                    {
                    }

                    if (tmp != null)
                    {
                        yield return tmp;
                    }
                }
                else if (entry.Kind == TemplateSourceEntryKind.Folder)
                {
                    foreach (ITemplate template in GetTemplatesFromDir(source, (TemplateSourceFolder)entry))
                    {
                        yield return template;
                    }
                }
            }
        }

        public bool TryGetTemplateFromSource(IConfiguredTemplateSource target, string name, out ITemplate template)
        {
            template = GetTemplatesFromSource(target).FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            return template != null;
        }

        private IEnumerable<ITemplate> GetTemplatesFromDir(IConfiguredTemplateSource source, TemplateSourceFolder folder)
        {
            foreach (ITemplateSourceEntry entry in folder.Children)
            {
                if (entry.Kind == TemplateSourceEntryKind.File && entry.FullPath.EndsWith(".vstemplate"))
                {
                    VsTemplate tmp = null;
                    try
                    {
                        tmp = new VsTemplate((TemplateSourceFile)entry, source, this);
                    }
                    catch
                    {
                    }

                    if (tmp != null)
                    {
                        yield return tmp;
                    }
                }
                else if (entry.Kind == TemplateSourceEntryKind.Folder)
                {
                    foreach (ITemplate template in GetTemplatesFromDir(source, (TemplateSourceFolder)entry))
                    {
                        yield return template;
                    }
                }
            }
        }

        private static void ProcessParameters(IParameterSet parameters)
        {
            ParameterSet p = (ParameterSet)parameters;
            ITemplateParameter safeProjectName = new Parameter("safeprojectname", TemplateParameterPriority.Required, "string");
            p.AddParameter(safeProjectName);
            ITemplateParameter projectName;
            p.TryGetParameter("projectname", out projectName);
            p.ParameterValues[safeProjectName] = p.ParameterValues[projectName];
        }
        private class Parameter : ITemplateParameter
        {
            public Parameter(string name, TemplateParameterPriority priority, string type, bool isName = false, string documentation = null)
            {
                Name = name;
                Priority = priority;
                Type = type;
                IsName = isName;
                Documentation = documentation;
            }

            public string Documentation { get; }

            public bool IsName { get; }

            public string Name { get; }

            public TemplateParameterPriority Priority { get; }

            public string Type { get; }
        }

        private class ParameterSet : IParameterSet
        {
            private readonly IDictionary<string, ITemplateParameter> _parameters = new Dictionary<string, ITemplateParameter>(StringComparer.OrdinalIgnoreCase);

            public ParameterSet()
            {
                AddParameter(new Parameter("projectname", TemplateParameterPriority.Implicit, "string", true));
            }

            public IEnumerable<ITemplateParameter> Parameters => _parameters.Values;

            public IDictionary<ITemplateParameter, string> ParameterValues { get; } = new Dictionary<ITemplateParameter, string>();

            public IEnumerable<string> RequiredBrokerCapabilities => Enumerable.Empty<string>();

            public void AddParameter(ITemplateParameter param)
            {
                _parameters[param.Name] = param;
            }

            public bool TryGetParameter(string name, out ITemplateParameter parameter)
            {
                if(_parameters.TryGetValue(name, out parameter))
                {
                    return true;
                }

                parameter = new Parameter(name, TemplateParameterPriority.Optional, "string");
                return true;
            }
        }
    }
}
