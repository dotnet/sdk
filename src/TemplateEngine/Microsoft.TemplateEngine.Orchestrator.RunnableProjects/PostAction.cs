using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    public class PostAction : IPostAction
    {
        public string Description { get; private set; }

        public Guid ActionId { get; private set; }

        public bool AbortOnFail { get; private set; }

        public IReadOnlyDictionary<string, string> Args { get; private set; }

        public string ManualInstructions { get; private set; }

        public string ConfigFile { get; private set; }


        string IPostAction.Description => Description;

        Guid IPostAction.ActionId => ActionId;

        bool IPostAction.ContinueOnError => AbortOnFail;

        IReadOnlyDictionary<string, string> IPostAction.Args => Args;

        string IPostAction.ManualInstructions => ManualInstructions;

        string IPostAction.ConfigFile => ConfigFile;

        public static List<IPostAction> ListFromModel(IReadOnlyList<IPostActionModel> modelList)
        {
            List<IPostAction> actionList = new List<IPostAction>();

            foreach (IPostActionModel model in modelList)
            {
                IPostAction postAction = new PostAction()
                {
                    Description = model.Description,
                    ActionId = model.ActionId,
                    AbortOnFail = model.ContinueOnError,
                    Args = model.Args,
                    ManualInstructions = model.ManualInstructions,
                    ConfigFile = model.ConfigFile,
                };

                actionList.Add(postAction);
            }

            return actionList;
        }
    }
}
