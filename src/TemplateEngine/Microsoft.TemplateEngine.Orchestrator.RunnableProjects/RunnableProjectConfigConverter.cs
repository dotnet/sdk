using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class RunnableProjectConfigConverter
    {
        public static IRunnableProjectConfig FromJObject(JObject o)
        {
            return SimpleConfigModel.FromJObject(o);
        }
    }
}