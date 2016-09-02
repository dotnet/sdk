using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class PostActionModel : IPostActionModel
    {
        public int Order { get; private set; }

        public IReadOnlyList<IPostActionOperationModel> Operations { get; private set; }

        public IReadOnlyList<IPostActionOperationModel> AlternateOperations { get; private set; }

        public static IPostActionModel FromJObject(JObject jObject)
        {
            List<IPostActionOperationModel> operationList = new List<IPostActionOperationModel>();
            JArray operations = (JArray)jObject["operations"];
            foreach (JToken token in operations)
            {
                operationList.Add(new PostActionOperationModel(token.ToString()));
            }

            List<IPostActionOperationModel> altOperationList = new List<IPostActionOperationModel>();
            JArray altOperations = (JArray)jObject["alternateOperations"];
            foreach (JToken token in altOperations)
            {
                altOperationList.Add(new PostActionOperationModel(token.ToString()));
            }

            PostActionModel model = new PostActionModel
            {
                Order = jObject.ToInt32(nameof(Order)),
                Operations = operationList,
                AlternateOperations = altOperationList
            };

            return model;
        }
    }
}
