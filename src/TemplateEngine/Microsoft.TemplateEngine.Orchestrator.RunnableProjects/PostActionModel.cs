using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class PostActionModel : IPostActionModel
    {
        public string Description { get; private set; }

        public Guid ActionId { get; private set; }

        public bool ContinueOnError { get; private set; }

        public IReadOnlyDictionary<string, string> Args { get; private set; }

        public string ManualInstructions { get; private set; }

        public string ConfigFile { get; private set; }

        public static IReadOnlyList<IPostActionModel> ListFromJArray(JArray jObject)
        {
            List<IPostActionModel> modelList = new List<IPostActionModel>();

            if (jObject == null)
            {
                return modelList;
            }

            foreach (JToken action in jObject)
            {
                Dictionary<string, string> args = new Dictionary<string, string>();

                foreach (JProperty argInfo in action.PropertiesOf("Args"))
                {
                    args.Add(argInfo.Name, argInfo.Value.ToString());
                }

                PostActionModel model = new PostActionModel()
                {
                    Description = action.ToString(nameof(model.Description)),
                    ActionId = action.ToGuid(nameof(model.ActionId)),
                    ContinueOnError = action.ToBool(nameof(model.ContinueOnError)),
                    Args = args,
                    ManualInstructions = action.ToString(nameof(model.ManualInstructions)),
                    ConfigFile = action.ToString(nameof(model.ConfigFile))
                };

                modelList.Add(model);
            }

            return modelList;
        }
    }
}
