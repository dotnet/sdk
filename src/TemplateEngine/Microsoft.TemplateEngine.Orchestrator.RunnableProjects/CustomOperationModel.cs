using Microsoft.TemplateEngine.Core.Contracts;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class CustomOperationModel : ICustomOperationModel
    {
        public string Type { get; set; }

        public string Condition { get; set; }

        public JObject Configuration { get; set; }

        public static ICustomOperationModel FromJObject(JObject jObject)
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
