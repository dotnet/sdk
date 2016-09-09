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

        // Each key value pair represents a manual instruction option.
        // The key is the text of the instruction
        // The value is a conditional to evaluate to determine if the instruction is valid in this context.
        // The instructions get resolved when turning the model into the actual - at most 1 will be chosen.
        public IReadOnlyList<KeyValuePair<string, string>> ManualInstructionInfo { get; private set; }

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

                List<KeyValuePair<string, string>> instructionOptions = new List<KeyValuePair<string, string>>();
                
                foreach (JToken instructionToken in (JArray)action["ManualInstructions"])
                {
                    KeyValuePair<string, string> instruction = new KeyValuePair<string, string>(instructionToken.ToString("text"), instructionToken.ToString("condition"));
                    instructionOptions.Add(instruction);
                }

                PostActionModel model = new PostActionModel()
                {
                    Description = action.ToString(nameof(model.Description)),
                    ActionId = action.ToGuid(nameof(model.ActionId)),
                    ContinueOnError = action.ToBool(nameof(model.ContinueOnError)),
                    Args = args,
                    ManualInstructionInfo = instructionOptions,
                    ConfigFile = action.ToString(nameof(model.ConfigFile))
                };

                modelList.Add(model);
            }

            return modelList;
        }
    }
}
