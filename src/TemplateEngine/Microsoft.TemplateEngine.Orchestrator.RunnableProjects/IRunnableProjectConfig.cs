using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Engine;
using Microsoft.TemplateEngine.Abstractions.Mount;
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

        IFile SourceFile { set; }

        string Identity { get; }

        IRunnableProjectConfig ReprocessWithParameters(IParameterSet parameters, IVariableCollection rootVariableCollection, IFile configFile, IOperationProvider[] operations);
    }
}