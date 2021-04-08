using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config
{
    internal class IncludeConfig : IOperationConfig
    {
        public string Key => Include.OperationName;

        public Guid Id => new Guid("3FAE1942-7257-4247-B44D-2DDE07CB4A4A");

        public IEnumerable<IOperationProvider> ConfigureFromJObject(JObject rawConfiguration, IDirectory templateRoot)
        {
            string startToken = rawConfiguration.ToString("start");
            string endToken = rawConfiguration.ToString("end");
            string id = rawConfiguration.ToString("id");
            bool onByDefault = rawConfiguration.ToBool("onByDefault");

            yield return new Include(startToken.TokenConfig(), endToken.TokenConfig(), x => templateRoot.FileInfo(x).OpenRead(), id, onByDefault);
        }
    }
}
