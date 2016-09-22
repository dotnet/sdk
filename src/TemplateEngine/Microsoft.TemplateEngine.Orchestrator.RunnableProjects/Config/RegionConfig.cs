using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config
{
    public class RegionConfig : IOperationConfig
    {
        public int Order => -8000;

        public string Key => "regions";

        public Guid Id => new Guid("3D33B3BF-F40E-43EB-A14D-F40516F880CD");

        public IEnumerable<IOperationProvider> ConfigureFromJObject(IComponentManager componentManager, JObject rawConfiguration, IDirectory templateRoot, IVariableCollection variables, IParameterSet parameters)
        {
            string id = rawConfiguration.ToString("id");
            string start = rawConfiguration.ToString("start");
            string end = rawConfiguration.ToString("end");
            bool include = rawConfiguration.ToBool("include");
            bool regionTrim = rawConfiguration.ToBool("trim");
            bool regionWholeLine = rawConfiguration.ToBool("wholeLine");

            yield return new Region(start, end, include, regionWholeLine, regionTrim, id);
        }
    }
}