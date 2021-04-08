using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config
{
    internal class RegionConfig : IOperationConfig
    {
        public string Key => Region.OperationName;

        public Guid Id => new Guid("3D33B3BF-F40E-43EB-A14D-F40516F880CD");

        public IEnumerable<IOperationProvider> ConfigureFromJObject(JObject rawConfiguration, IDirectory templateRoot)
        {
            string id = rawConfiguration.ToString("id");
            string start = rawConfiguration.ToString("start");
            string end = rawConfiguration.ToString("end");
            bool include = rawConfiguration.ToBool("include");
            bool regionTrim = rawConfiguration.ToBool("trim");
            bool regionWholeLine = rawConfiguration.ToBool("wholeLine");
            bool onByDefault = rawConfiguration.ToBool("onByDefault");

            yield return new Region(start.TokenConfig(), end.TokenConfig(), include, regionWholeLine, regionTrim, id, onByDefault);
        }
    }
}
