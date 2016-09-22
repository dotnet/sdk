using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core.Contracts;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config
{
    public interface IOperationConfig : IIdentifiedComponent
    {
        int Order { get; }

        string Key { get; }

        IEnumerable<IOperationProvider> ConfigureFromJObject(IComponentManager componentManager, JObject rawConfiguration, IDirectory templateRoot, IVariableCollection variables, IParameterSet parameters);
    }
}
