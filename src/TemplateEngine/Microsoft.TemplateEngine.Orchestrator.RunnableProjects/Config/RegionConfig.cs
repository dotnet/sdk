using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config
{
    internal class RegionConfig : IOperationConfig
    {
        public int Order => -8000;

        public string Key => "regions";

        public IEnumerable<IOperationProvider> Process(JObject rawConfiguration, IDirectory templateRoot, IVariableCollection variables, IParameterSet parameters)
        {
            JArray regionSettings = (JArray)rawConfiguration["settings"];
            foreach (JToken child in regionSettings.Children())
            {
                JObject setting = (JObject)child;
                string id = setting.ToString("id");
                string start = setting.ToString("start");
                string end = setting.ToString("end");
                bool include = setting.ToBool("include");
                bool regionTrim = setting.ToBool("trim");
                bool regionWholeLine = setting.ToBool("wholeLine");

                yield return new Region(start, end, include, regionWholeLine, regionTrim, id);
            }
        }
    }
}