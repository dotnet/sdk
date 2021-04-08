using Microsoft.TemplateEngine.Core.Contracts;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class CustomOperationModel : ICustomOperationModel
    {
        public string Type { get; set; }

        public string Condition { get; set; }

        internal JObject Configuration { get; set; }

        internal static ICustomOperationModel FromJObject(JObject jObject)
        {
            CustomOperationModel model = new CustomOperationModel
            {
                Type = jObject.ToString(nameof(Type)),
                Condition = jObject.ToString(nameof(Condition)),
                Configuration = jObject.Get<JObject>(nameof(Configuration)),
            };

            return model;
        }
    }
}
