using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public interface IRunnableProjectConfig
    {
        IReadOnlyDictionary<string, Parameter> Parameters { get; }

        IReadOnlyDictionary<string, Dictionary<string, JObject>> Special { get; }

        IReadOnlyDictionary<string, JObject> Config { get; }

        IReadOnlyList<FileSource> Sources { get; }

        string DefaultName { get; }

        string Name { get; }

        string ShortName { get; }

        string Author { get; }

        IReadOnlyDictionary<string, string> Tags { get; }

        IReadOnlyList<string> Classifications { get; }

        string GroupIdentity { get; }

        ITemplateSourceFile SourceFile { set; }

        IRunnableProjectConfig ReprocessWithParameters(IParameterSet parameters, VariableCollection rootVariableCollection, ITemplateSourceFile configFile, IOperationProvider[] operations);
    }
}