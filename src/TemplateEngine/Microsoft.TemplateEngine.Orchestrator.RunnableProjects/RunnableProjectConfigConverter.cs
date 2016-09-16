using System;
using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class RunnableProjectConfigConverter
    {
        public static IRunnableProjectConfig FromJObject(JObject o, IComponentManager componentManager)
        {
            return SimpleConfigModel.FromJObject(o);
        }
    }
}