using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config
{
    public class IncludeConfig : IOperationConfig
    {
        public int Order => -9000;

        public string Key => "include";

        public Guid Id => new Guid("3FAE1942-7257-4247-B44D-2DDE07CB4A4A");

        public IEnumerable<IOperationProvider> ConfigureFromJObject(IComponentManager componentManager, JObject rawConfiguration, IDirectory templateRoot, IVariableCollection variables, IParameterSet parameters)
        {
            string startToken = rawConfiguration.ToString("start");
            string endToken = rawConfiguration.ToString("end");
            string id = rawConfiguration.ToString("id");

            yield return new Include(startToken, endToken, x => templateRoot.FileInfo(x).OpenRead(), id);
        }
    }
}