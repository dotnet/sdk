using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class CreationResult : ICreationResult
    {
        public IReadOnlyList<IPostAction> PostActions { get; set; }

        public IReadOnlyList<ICreationPath> PrimaryOutputs { get; set; }

        public void TEMP_CONSOLE_DEBUG_CreationResult()
        {
            ITemplateEngineHost host = EngineEnvironmentSettings.Host;

            foreach (IPostAction postActionInfo in PostActions)
            {
                host.LogMessage(string.Format("Placeholder for post action processing of action: {0}", postActionInfo.Description));

                host.LogMessage(string.Format("\tActionId: {0}", postActionInfo.ActionId));
                host.LogMessage(string.Format("\tContinueOnError: {0}", postActionInfo.ContinueOnError));
                host.LogMessage(string.Format("\tConfigFile: {0}", postActionInfo.ConfigFile));
                host.LogMessage(string.Format("\tManual Instructions: {0}", postActionInfo.ManualInstructions));
                host.LogMessage(string.Format("\tArgs"));

                foreach (KeyValuePair<string, string> arg in postActionInfo.Args)
                {
                    host.LogMessage(string.Format("\t\tKey = {0} | Value = {1}", arg.Key, arg.Value));
                }
            }

            host.LogMessage("Primary Outputs (artifacts)");
            foreach (ICreationPath pathInfo in PrimaryOutputs)
            {
                host.LogMessage(string.Format("\tPath: {0}", pathInfo.Path));
            }
        }
    }
}
